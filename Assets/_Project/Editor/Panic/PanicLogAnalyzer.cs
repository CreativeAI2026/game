using System.Collections.Generic;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 焦り度ログを場面に区切り、テストする人の印（「焦った」）と検知の判定を照らし合わせる。
    ///
    /// 人は押すのが遅れるうえ、時間の感覚もあいまいなので、「印から何秒以内」では照合しない。
    /// 印はその場面か、ひとつ前の場面で判定していれば一致とする。
    /// 判定のほうは、判定していた場面か、その次の場面に印があれば一致とする。
    /// 交戦の外で付けた印（倒れた直後など）は、直前の場面のものとして扱う。
    /// </summary>
    public static class PanicLogAnalyzer
    {
        // ログの段階名（PanicLevel.ToDisplayName と同じ）
        private const string PanicLevelName = "焦り";
        private const string TenseLevelName = "動揺";

        private const string CapturedStateName = "TBossCapturedState";

        // HP の割合がこれ以上下がったら被弾とみなす（記録の丸め誤差を除くため）
        private const float HitHpDropThreshold = 0.001f;

        public static PanicLogAnalysis Analyze(IEnumerable<PanicLogCsv> logs)
        {
            var analysis = new PanicLogAnalysis();
            foreach (PanicLogCsv log in logs)
            {
                analysis.FileCount++;
                foreach (string column in log.SignalScoreColumns)
                {
                    if (!analysis.SignalColumns.Contains(column))
                        analysis.SignalColumns.Add(column);
                }
                AnalyzeFile(log, analysis);
            }
            return analysis;
        }

        /// <summary>終了時の振り返りの答え（*_review.csv）を集計結果に加える。</summary>
        public static void AddReviewAnswers(PanicLogAnalysis analysis, IEnumerable<PanicLogCsv> reviews)
        {
            foreach (PanicLogCsv review in reviews)
            {
                foreach (string[] row in review.Rows)
                {
                    analysis.ReviewAnswers.Add(
                        new PanicLogReviewAnswer
                        {
                            FileName = review.FileName,
                            Category = review.Get(row, "category"),
                            MainSignal = review.Get(row, "main_signal"),
                            RecentEvent = review.Get(row, "recent_event"),
                            Answer = review.Get(row, "answer"),
                        }
                    );
                }
            }
        }

        private static void AnalyzeFile(PanicLogCsv log, PanicLogAnalysis analysis)
        {
            var scenes = new List<PanicLogScene>();
            var markers = new List<PanicLogMarker>();
            var detections = new List<PanicLogDetection>();

            PanicLogScene current = null;
            PanicLogDetection detection = null;
            bool wasInCombat = false;
            bool wasAttacking = false;
            float previousHp = -1f;
            string previousBossState = string.Empty;
            float previousTime = 0f;
            bool hasPreviousTime = false;

            foreach (string[] row in log.Rows)
            {
                float time = log.GetFloat(row, "time");
                string rowType = log.Get(row, "row_type");
                bool inCombat = log.Get(row, "in_combat") == "1";
                bool attacking = log.Get(row, "enemy_attacking") == "1";
                float hp = log.GetFloat(row, "player_hp_ratio");
                string bossState = log.Get(row, "boss_state");
                string level = log.Get(row, "level");

                if (hasPreviousTime && rowType == "sample")
                {
                    float delta = time - previousTime;
                    analysis.RecordedSeconds += delta;
                    if (wasInCombat)
                        analysis.CombatSeconds += delta;
                }

                if (!inCombat)
                {
                    if (current != null)
                    {
                        current.EndTime = time;
                        current = null;
                    }
                    detection = CloseDetection(detection, time);
                }
                else
                {
                    string trigger = FindTrigger(wasInCombat, attacking, wasAttacking, hp, previousHp, bossState, previousBossState);
                    if (trigger != null || current == null)
                    {
                        if (current != null)
                            current.EndTime = time;
                        current = new PanicLogScene
                        {
                            FileName = log.FileName,
                            Index = scenes.Count,
                            StartTime = time,
                            EndTime = time,
                            Trigger = trigger ?? "交戦中",
                        };
                        scenes.Add(current);
                    }

                    current.EndTime = time;
                    AccumulateScene(log, row, level, current);

                    bool isPanic = level == PanicLevelName;
                    if (isPanic && detection == null)
                    {
                        detection = new PanicLogDetection
                        {
                            FileName = log.FileName,
                            StartTime = time,
                            EndTime = time,
                            StartScene = current,
                        };
                        detections.Add(detection);
                    }
                    else if (!isPanic && rowType == "sample")
                    {
                        detection = CloseDetection(detection, time);
                    }

                    if (detection != null)
                    {
                        detection.EndTime = time;
                        float score = log.GetFloat(row, "panic_score");
                        if (score >= detection.MaxPanicScore)
                        {
                            detection.MaxPanicScore = score;
                            detection.CategoryAtMax = log.Get(row, "dominant_category");
                        }
                    }
                }

                if (rowType == "marker")
                {
                    // 交戦の外で付けた印（倒れた直後など）は、直前の場面のものとして扱う
                    PanicLogScene scene = current ?? (scenes.Count > 0 ? scenes[scenes.Count - 1] : null);
                    if (scene != null)
                    {
                        scene.HasMarker = true;
                        markers.Add(new PanicLogMarker { FileName = log.FileName, Time = time, Scene = scene });
                    }
                }

                wasInCombat = inCombat;
                wasAttacking = attacking;
                previousHp = hp;
                previousBossState = bossState;
                previousTime = time;
                hasPreviousTime = true;
            }

            MatchMarkers(scenes, markers);
            MatchDetections(scenes, detections);

            analysis.Scenes.AddRange(scenes);
            analysis.Markers.AddRange(markers);
            analysis.Detections.AddRange(detections);
        }

        /// <summary>場面の区切りになる出来事を探す。区切りでなければ null。</summary>
        private static string FindTrigger(
            bool wasInCombat,
            bool attacking,
            bool wasAttacking,
            float hp,
            float previousHp,
            string bossState,
            string previousBossState
        )
        {
            if (!wasInCombat)
                return "交戦開始";
            if (bossState == CapturedStateName && previousBossState != CapturedStateName)
                return "掴まれた";
            if (previousHp >= 0f && hp < previousHp - HitHpDropThreshold)
                return "被弾";
            if (attacking && !wasAttacking)
                return $"ボスの攻撃（{AttackName(bossState)}）";
            return null;
        }

        private static string AttackName(string bossState) =>
            bossState switch
            {
                "TBossNormalAttackState" => "通常攻撃",
                "TBossNormalAttack2State" => "鎌",
                "TBossSpecialAttackState" => "触手",
                "" => "種類不明",
                _ => bossState,
            };

        private static void AccumulateScene(PanicLogCsv log, string[] row, string level, PanicLogScene scene)
        {
            if (level == PanicLevelName)
                scene.HasPanic = true;
            if (level == PanicLevelName || level == TenseLevelName)
                scene.HasTenseOrAbove = true;

            float score = log.GetFloat(row, "panic_score");
            if (score >= scene.MaxPanicScore)
            {
                scene.MaxPanicScore = score;
                scene.CategoryAtMax = log.Get(row, "dominant_category");
            }

            foreach (string column in log.SignalScoreColumns)
            {
                float value = log.GetFloat(row, column);
                if (!scene.MaxSignalScores.TryGetValue(column, out float max) || value > max)
                    scene.MaxSignalScores[column] = value;
            }

            string scheme = log.Get(row, "control_scheme");
            if (!string.IsNullOrEmpty(scheme))
                scene.ControlScheme = scheme;
        }

        private static PanicLogDetection CloseDetection(PanicLogDetection detection, float time)
        {
            if (detection != null)
                detection.EndTime = time;
            return null;
        }

        // 印は押すのが遅れるので、印の場面か、ひとつ前の場面での判定を見る
        private static void MatchMarkers(List<PanicLogScene> scenes, List<PanicLogMarker> markers)
        {
            foreach (PanicLogMarker marker in markers)
            {
                int index = marker.Scene.Index;
                PanicLogScene previous = index > 0 ? scenes[index - 1] : null;
                marker.MatchedPanic = marker.Scene.HasPanic || (previous != null && previous.HasPanic);
                marker.MatchedTenseOrAbove =
                    marker.Scene.HasTenseOrAbove || (previous != null && previous.HasTenseOrAbove);
            }
        }

        // 判定していた場面のどれか、またはその次の場面に印があれば一致とする
        private static void MatchDetections(List<PanicLogScene> scenes, List<PanicLogDetection> detections)
        {
            foreach (PanicLogDetection detection in detections)
            {
                for (int i = detection.StartScene.Index; i < scenes.Count; i++)
                {
                    PanicLogScene scene = scenes[i];
                    bool isDuringDetection = scene.StartTime <= detection.EndTime;
                    bool isRightAfterDetection = i > 0 && scenes[i - 1].StartTime <= detection.EndTime;
                    if (!isDuringDetection && !isRightAfterDetection)
                        break;

                    if (scene.HasMarker)
                    {
                        detection.HasMarker = true;
                        break;
                    }
                }
            }
        }
    }
}
