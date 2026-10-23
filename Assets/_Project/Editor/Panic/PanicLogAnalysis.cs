using System.Collections.Generic;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 焦り度ログの「場面」。戦闘中のログを、人が覚えていられる出来事（交戦の開始・ボスの攻撃の出だし・被弾・掴まれた）で
    /// 区切ったひとまとまり。テストする人の印（「焦った」）は秒単位では正確でないため、この単位で検知と照らし合わせる。
    /// </summary>
    public sealed class PanicLogScene
    {
        public string FileName;
        public int Index;
        public float StartTime;
        public float EndTime;

        /// <summary>この場面が始まったきっかけ（「ボスの攻撃（鎌）」「被弾」など）。</summary>
        public string Trigger;

        public bool HasMarker;
        public bool HasPanic;
        public bool HasTenseOrAbove;
        public float MaxPanicScore;

        /// <summary>焦り度が最も高かった時の、焦りの系統。</summary>
        public string CategoryAtMax = string.Empty;

        /// <summary>指標ごとの、この場面での最大スコア（列名 → 0〜1）。</summary>
        public readonly Dictionary<string, float> MaxSignalScores = new Dictionary<string, float>();

        public string ControlScheme = string.Empty;
    }

    /// <summary>テストする人が付けた「焦った」の印1つと、検知との照合結果。</summary>
    public sealed class PanicLogMarker
    {
        public string FileName;
        public float Time;
        public PanicLogScene Scene;

        /// <summary>印の場面か、ひとつ前の場面で「焦り」と判定していたか。</summary>
        public bool MatchedPanic;

        /// <summary>印の場面か、ひとつ前の場面で「動揺」以上と判定していたか。</summary>
        public bool MatchedTenseOrAbove;
    }

    /// <summary>検知が「焦り」と判定していた区間1つと、印との照合結果。</summary>
    public sealed class PanicLogDetection
    {
        public string FileName;
        public float StartTime;
        public float EndTime;
        public PanicLogScene StartScene;
        public float MaxPanicScore;
        public string CategoryAtMax = string.Empty;

        /// <summary>判定していた場面か、その次の場面に印が付いていたか。</summary>
        public bool HasMarker;
    }

    /// <summary>終了時の振り返りで、テストする人が場面1つに答えた結果。</summary>
    public sealed class PanicLogReviewAnswer
    {
        public string FileName;
        public string Category;
        public string MainSignal;
        public string RecentEvent;

        /// <summary>「焦っていた」「焦っていなかった」「覚えていない」「未回答」のどれか。</summary>
        public string Answer;
    }

    /// <summary>焦り度ログ（複数ファイル）の集計結果。</summary>
    public sealed class PanicLogAnalysis
    {
        public int FileCount;
        public float RecordedSeconds;
        public float CombatSeconds;
        public readonly List<PanicLogScene> Scenes = new List<PanicLogScene>();
        public readonly List<PanicLogMarker> Markers = new List<PanicLogMarker>();
        public readonly List<PanicLogDetection> Detections = new List<PanicLogDetection>();
        public readonly List<PanicLogReviewAnswer> ReviewAnswers = new List<PanicLogReviewAnswer>();

        /// <summary>指標の列名の一覧（全ファイルの和、出てきた順）。</summary>
        public readonly List<string> SignalColumns = new List<string>();
    }
}
