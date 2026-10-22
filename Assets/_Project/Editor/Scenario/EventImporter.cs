#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CreativeAI.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CreativeAI.EditorTools
{
    /// <summary>
    /// 物語班が手書きする events.json を検証し、1イベント = 1つの EventDefinition に変換する
    /// 純粋パーサ(ファイル IO・AssetDatabase を持たない=テスト可能)。
    /// 実際の .asset 書き出しは EventImporterMenu が担う。
    /// </summary>
    public static class EventImporter
    {
        /// <summary>
        /// line ステップの portrait に書ける立ち絵キーのカタログ(打ち間違い検出)。DialogueCharacterDefinition の PortraitKey と同じ
        /// ({キャラ _id}_{表情} の snake_case)なので、表情を足すときは定義アセット(Features/UI/ConversationUI/Data/Characters)にも足す。
        /// </summary>
        public static readonly IReadOnlyCollection<string> PortraitKeys = new HashSet<string>(
            StringComparer.Ordinal
        )
        {
            "hero_normal",
            "hero_smile",
            "hero_confused",
            "hero_surprised",
            "hero_question",
            "hero_anger",
            "girl_normal",
            "girl_fear",
            "girl_resolve",
            "girl_smile",
            "girl_wry_smile",
            "girl_surprised",
            "robot_normal",
            "gramophone_normal",
        };

        /// <summary>
        /// giveWeapon の weaponKey として許される値。武器は剣/弓/鎌/ハンマーの4種で固定なので
        /// item カタログではなくこの集合で照合する(ScenarioReference.md「武器カタログ」)。
        /// ゲーム側の WeaponManager.WeaponKeys と同じ内容に保つこと。
        /// </summary>
        public static readonly IReadOnlyCollection<string> WeaponKeys = new HashSet<string>(
            StringComparer.Ordinal
        )
        {
            "sword",
            "bow",
            "scythe",
            "hammer",
        };

        /// <summary>
        /// command ステップで書ける演出コマンド名。ConversationView のルータに無い名前は何も起きないので取り込み時に弾く。
        /// camera.* / background.* は外部委譲用に接頭辞だけ見て通す(IsKnownCommand)。
        /// </summary>
        public static readonly IReadOnlyCollection<string> CommandNames = new HashSet<string>(
            StringComparer.Ordinal
        )
        {
            "window.hide",
            "window.show",
            "portrait.left.hide",
            "portrait.right.hide",
            "portrait.left.obscure",
            "portrait.right.obscure",
            "portrait.left.reveal",
            "portrait.right.reveal",
            "portrait.left.shake",
            "portrait.right.shake",
            "portrait.left.jump",
            "portrait.right.jump",
            "wait",
            "conversation.close",
        };

        /// <summary>
        /// choice の選択肢数の下限・上限。
        /// 上限3は選択肢UIの都合: 3択ぶんの高さを基準に中央寄せするので4つ以上は会話ウィンドウに被る。
        /// </summary>
        public const int MinChoiceOptions = 2;
        public const int MaxChoiceOptions = 3;

        /// <summary>
        /// id の書式(小文字英字・数字・_ の snake_case)。id はそのまま .asset のファイル名になるため、
        /// 大文字違い(Mac は区別しないので Boss と boss が同じファイルになり黙って上書き)や / などを弾く。
        /// </summary>
        private static readonly Regex IdPattern = new("^[a-z0-9_]+$");

        /// <summary>wait の秒数の上限。会話UIは範囲外を黙って 0〜10 秒に丸めるので、書き間違いに気づけるよう取り込み時に弾く。</summary>
        public const float MaxWaitSeconds = 10f;

        private static bool IsKnownCommand(string command) =>
            CommandNames.Contains(command)
            || command.StartsWith("camera.", StringComparison.Ordinal)
            || command.StartsWith("background.", StringComparison.Ordinal);

        /// <summary>
        /// itemKey を弾くための有効キー集合。null なら「未提供」= 警告どまり。
        /// エディタ側(EventImporterMenu)が ItemData から構築して渡す。
        /// (敵は events.json に書かず EventTrigger に配線するため enemyKey の照合は持たない。)
        /// </summary>
        public sealed class ImportCatalog
        {
            /// <summary>giveItem の itemKey 照合用。全カテゴリの ItemData キー。</summary>
            public IReadOnlyCollection<string> ItemKeys { get; }

            /// <summary>
            /// hasItem の itemKey 照合用。**大事なもの(Important)のキーだけ**を持つ。
            /// </summary>
            public IReadOnlyCollection<string> KeyItemKeys { get; }

            /// <summary>
            /// 立ち絵アセットに実際に登録済みの portrait キー(null なら照合しない)。
            /// <see cref="PortraitKeys"/> にあっても絵が無いキーは実行時に既定の立ち絵へ落ちるので、それを警告するために持つ。
            /// </summary>
            public IReadOnlyCollection<string> RegisteredPortraitKeys { get; }

            public ImportCatalog(
                IReadOnlyCollection<string> itemKeys,
                IReadOnlyCollection<string> keyItemKeys = null,
                IReadOnlyCollection<string> registeredPortraitKeys = null
            )
            {
                ItemKeys = itemKeys;
                KeyItemKeys = keyItemKeys;
                RegisteredPortraitKeys = registeredPortraitKeys;
            }
        }

        public enum Severity
        {
            Error,
            Warning,
        }

        /// <summary>1件の検証結果。EventId は特定できない段階では null。</summary>
        public sealed class Diagnostic
        {
            public Severity Severity { get; }
            public string EventId { get; }
            public string Message { get; }

            public Diagnostic(Severity severity, string eventId, string message)
            {
                Severity = severity;
                EventId = eventId;
                Message = message;
            }

            public override string ToString() =>
                EventId == null ? $"[{Severity}] {Message}" : $"[{Severity}] ({EventId}) {Message}";
        }

        /// <summary>取り込み結果。Events はエラーの無かったイベントのみ(そのまま書き出せる)。</summary>
        public sealed class Report
        {
            public List<Diagnostic> Diagnostics { get; } = new();

            /// <summary>エラーが無く、そのまま .asset 化できる EventDefinition 群。</summary>
            public List<EventDefinition> Events { get; } = new();

            public int ErrorCount => Diagnostics.Count(d => d.Severity == Severity.Error);
            public int WarningCount => Diagnostics.Count(d => d.Severity == Severity.Warning);
            public bool HasErrors => ErrorCount > 0;

            public void Error(string eventId, string message) =>
                Diagnostics.Add(new Diagnostic(Severity.Error, eventId, message));

            public void Warn(string eventId, string message) =>
                Diagnostics.Add(new Diagnostic(Severity.Warning, eventId, message));
        }

        /// <summary>
        /// events.json を検証して EventDefinition 群を組み立てる。例外は投げず不正は Report.Diagnostics に積む。
        /// </summary>
        /// <param name="catalog">itemKey の有効集合。null なら itemKey は警告どまり。</param>
        public static Report Parse(string json, ImportCatalog catalog = null)
        {
            var report = new Report();

            if (string.IsNullOrWhiteSpace(json))
            {
                report.Error(null, "events.json が空です。");
                return report;
            }

            JObject root;
            try
            {
                root = JObject.Parse(
                    json,
                    new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    }
                );
            }
            catch (JsonException ex)
            {
                report.Error(null, $"JSON の解析に失敗しました: {ex.Message}");
                return report;
            }

            if (root["events"] is not JArray events)
            {
                report.Error(null, "トップレベルに配列 \"events\" がありません。");
                return report;
            }

            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] is not JObject ev)
                {
                    report.Error(null, $"events[{i}] がオブジェクトではありません。");
                    continue;
                }

                ParseEvent(ev, i, seenIds, report, catalog);
            }

            return report;
        }

        private static void ParseEvent(
            JObject ev,
            int index,
            HashSet<string> seenIds,
            Report report,
            ImportCatalog catalog
        )
        {
            // --- id ---
            var id = (ev["id"] as JValue)?.Value as string;
            var label = string.IsNullOrEmpty(id) ? $"events[{index}]" : id;
            if (string.IsNullOrEmpty(id))
            {
                report.Error(label, "id が未指定です(必須)。");
                return; // id が無いと以降の診断・出力先を確定できない
            }
            if (!IdPattern.IsMatch(id))
            {
                report.Error(
                    id,
                    "id は小文字の英字・数字・_ だけで書いてください(例: cave_encounter)。"
                );
                return;
            }
            if (!seenIds.Add(id))
            {
                report.Error(id, "id が重複しています。");
                return;
            }

            bool ok = true;

            // --- conditions ---
            var conditions = new List<EventCondition>();
            if (ev["conditions"] is not JArray condArray)
            {
                report.Error(id, "conditions がありません(必須)。");
                ok = false;
            }
            else
            {
                for (int c = 0; c < condArray.Count; c++)
                {
                    var cond = ParseCondition(condArray[c] as JObject, id, c, report, catalog);
                    if (cond == null)
                        ok = false;
                    else
                        conditions.Add(cond);
                }
            }

            // progress 条件を必ず1つ含む(進行度==でちょうど1回だけ発火する前提)。
            var progressValues = conditions
                .Where(c => c.Type == ConditionType.Progress)
                .Select(c => c.ProgressValue)
                .ToList();
            if (progressValues.Count == 0)
            {
                report.Error(id, "conditions に progress 条件が必須です(1つ以上)。");
                ok = false;
            }

            // --- steps ---
            var steps = new List<EventStep>();
            if (ev["steps"] is not JArray stepArray || stepArray.Count == 0)
            {
                report.Error(id, "steps がありません(必須・1つ以上)。");
                ok = false;
            }
            else
            {
                ok &= AppendSteps(stepArray, steps, id, report, catalog, false);
                // 分岐内の戦闘は禁止し、共通ステップだけで従来の配置制約を確認する。
                ok &= ValidateBattlePlacement(
                    stepArray
                        .Select(token =>
                            token is JObject obj
                            && (obj["kind"] as JValue)?.Value is string name
                            && Enum.TryParse<StepKind>(name, true, out var kind)
                                ? (StepKind?)kind
                                : null
                        )
                        .ToArray(),
                    id,
                    report
                );
                WarnBranchKeys(stepArray, id, report);
            }

            // --- nextProgress(必須・progress の value より大きい) ---
            // 進行度==で発火 → 終了時に nextProgress へ進めて value と一致しなくなる。
            // これで「どのイベントもちょうど1回だけ発火する」を保証する。
            int? nextProgress = null;
            if (!ev.TryGetValue("nextProgress", out var np))
            {
                report.Error(id, "nextProgress が必須です。");
                ok = false;
            }
            else if (np.Type != JTokenType.Integer)
            {
                report.Error(id, "nextProgress は整数で指定してください。");
                ok = false;
            }
            else
            {
                nextProgress = np.Value<int>();
                foreach (var pv in progressValues)
                {
                    if (nextProgress.Value <= pv)
                    {
                        report.Error(
                            id,
                            $"nextProgress ({nextProgress}) は progress の value ({pv}) より大きくしてください。"
                        );
                        ok = false;
                    }
                }
            }

            if (ok)
                report.Events.Add(
                    EventDefinition.Create(
                        id,
                        conditions.ToArray(),
                        steps.ToArray(),
                        nextProgress.Value
                    )
                );
        }

        // 元JSONの位置で検証し、アセット内だけ前方ジャンプと合流先へ変換する。
        private static bool AppendSteps(
            JArray input,
            List<EventStep> output,
            string id,
            Report report,
            ImportCatalog catalog,
            bool insideBranch
        )
        {
            int errors = report.ErrorCount;
            foreach (var token in input)
            {
                string location = token.Path;
                int start = location.IndexOf("steps[", StringComparison.Ordinal);
                if (start >= 0)
                    location = location.Substring(start);
                var step = token as JObject;
                string kind = (step?["kind"] as JValue)?.Value as string;
                if (insideBranch && (kind == "choice" || kind == "branch" || kind == "battle"))
                {
                    report.Error(
                        id,
                        $"{location}: branch 内に choice / branch / battle は配置できません。"
                    );
                    continue;
                }
                if (kind != "branch")
                {
                    var parsed = ParseStep(
                        step,
                        id,
                        output.Count,
                        report,
                        catalog,
                        out _,
                        location
                    );
                    if (parsed != null)
                        output.Add(parsed);
                    continue;
                }
                bool hasFlag = step.ContainsKey("flag"),
                    hasItem = step.ContainsKey("hasItem");
                string key = (step[hasFlag ? "flag" : "hasItem"] as JValue)?.Value as string;
                if (
                    hasFlag == hasItem
                    || string.IsNullOrWhiteSpace(key)
                    || step.ContainsKey("nextProgress")
                )
                {
                    report.Error(
                        id,
                        $"{location}: branch は flag / hasItem の片方だけを指定し、空にしないでください。nextProgress はイベントのみです。"
                    );
                    continue;
                }
                var arms = new List<(string value, JToken steps)>();
                if (hasFlag)
                {
                    if (
                        step["cases"] is not JObject cases
                        || !cases.Properties().Any()
                        || step.ContainsKey("then")
                        || step.ContainsKey("else")
                    )
                    {
                        report.Error(
                            id,
                            $"{location}: flag branch は空でない cases が必須です。then / else は使えません。"
                        );
                        continue;
                    }
                    foreach (var arm in cases.Properties())
                    {
                        if (string.IsNullOrEmpty(arm.Name))
                            report.Error(id, $"{location}.cases: キーは空にできません。");
                        arms.Add((arm.Name, arm.Value));
                    }
                }
                else
                {
                    if (step.ContainsKey("cases"))
                    {
                        report.Error(id, $"{location}: hasItem branch に cases は使えません。");
                        continue;
                    }
                    ParseCondition(
                        new JObject { ["type"] = "hasItem", ["itemKey"] = key },
                        id,
                        output.Count,
                        report,
                        catalog,
                        location
                    );
                    arms.Add(("then", step["then"] ?? new JArray()));
                    arms.Add(("else", step["else"] ?? new JArray()));
                }
                int branchIndex = output.Count;
                output.Add(null);
                var targets = new List<EventBranchCase>();
                var jumps = new List<int>();
                foreach (var arm in arms)
                {
                    targets.Add(new EventBranchCase(arm.value, output.Count));
                    if (arm.steps is not JArray children)
                        report.Error(
                            id,
                            $"{location}: branch '{arm.value}' はステップ配列にしてください。"
                        );
                    else
                        AppendSteps(children, output, id, report, catalog, true);
                    jumps.Add(output.Count);
                    output.Add(null);
                }
                int join = output.Count;
                foreach (int jump in jumps)
                    output[jump] = EventStep.Jump(join);
                output[branchIndex] = hasFlag
                    ? EventStep.BranchFlag(key, targets.ToArray(), join)
                    : EventStep.BranchItem(key, targets[0].Target, targets[1].Target, join);
            }
            return errors == report.ErrorCount;
        }

        private static void WarnBranchKeys(JArray steps, string id, Report report)
        {
            foreach (
                var branch in steps
                    .OfType<JObject>()
                    .Where(s =>
                        ((s["kind"] as JValue)?.Value as string) == "branch"
                        && s["flag"]?.Type == JTokenType.String
                        && s["cases"] is JObject
                    )
            )
            {
                string flag = ((branch["flag"] as JValue)?.Value as string);
                var choices = steps
                    .OfType<JObject>()
                    .Where(s =>
                        ((s["kind"] as JValue)?.Value as string) == "choice"
                        && ((s["flag"] as JValue)?.Value as string) == flag
                    )
                    .ToArray();
                if (choices.Length == 0)
                    continue;
                var values = choices
                    .SelectMany(s => (s["options"] as JArray ?? new JArray()).OfType<JObject>())
                    .Select(s => ((s["value"] as JValue)?.Value as string))
                    .Where(s => s != null)
                    .ToHashSet(StringComparer.Ordinal);
                var keys = ((JObject)branch["cases"])
                    .Properties()
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var missing in values.Except(keys))
                    report.Warn(
                        id,
                        $"branch '{flag}' の cases に選択肢 '{missing}' が不足しています。"
                    );
                foreach (var extra in keys.Except(values))
                    report.Warn(
                        id,
                        $"branch '{flag}' の cases に余分なキー '{extra}' があります。"
                    );
            }
        }

        private static EventCondition ParseCondition(
            JObject cond,
            string id,
            int i,
            Report report,
            ImportCatalog catalog,
            string sourcePath = null
        )
        {
            string location = sourcePath ?? $"conditions[{i}]";
            if (cond == null)
            {
                report.Error(id, $"{location} がオブジェクトではありません。");
                return null;
            }

            var type = (cond["type"] as JValue)?.Value as string;
            switch (type)
            {
                case "progress":
                    if (cond["value"]?.Type != JTokenType.Integer)
                    {
                        report.Error(id, $"{location} progress の value は整数(必須)。");
                        return null;
                    }
                    return EventCondition.Progress(cond["value"].Value<int>());

                case "flag":
                    var key = (cond["key"] as JValue)?.Value as string;
                    var val = (cond["value"] as JValue)?.Value as string;
                    if (string.IsNullOrEmpty(key) || val == null)
                    {
                        report.Error(id, $"{location} flag は key と value が必須。");
                        return null;
                    }
                    return EventCondition.Flag(key, val);

                case "hasItem":
                    var itemKey = (cond["itemKey"] as JValue)?.Value as string;
                    if (string.IsNullOrEmpty(itemKey))
                    {
                        report.Error(
                            id,
                            $"{location} hasItem は itemKey(大事なものの key)が必須。"
                        );
                        return null;
                    }
                    // hasItem は「大事なもの」専用。
                    // 打ち間違いや装備品/食材の key を書くと実行時は常に false になり
                    // イベントが永久に発火しないため、取り込み時に弾く。
                    // giveItem と同じく、カタログ未提供なら警告どまり(アセット未作成時に全滅させない)。
                    if (catalog?.KeyItemKeys != null)
                    {
                        if (!catalog.KeyItemKeys.Contains(itemKey))
                        {
                            report.Error(
                                id,
                                $"{location} hasItem の itemKey '{itemKey}' は大事なものカタログに存在しません。"
                            );
                            return null;
                        }
                    }
                    else
                    {
                        report.Warn(
                            id,
                            $"{location} hasItem の itemKey '{itemKey}' は未検証(大事なものカタログ未提供)。"
                        );
                    }
                    return EventCondition.HasItem(itemKey);

                default:
                    report.Error(
                        id,
                        $"{location} の type '{type}' は不正(progress / flag / hasItem のみ)。"
                    );
                    return null;
            }
        }

        private static EventStep ParseStep(
            JObject step,
            string id,
            int i,
            Report report,
            ImportCatalog catalog,
            out StepKind? kind,
            string sourcePath = null
        )
        {
            string location = sourcePath ?? $"steps[{i}]";
            kind = null;
            if (step == null)
            {
                report.Error(id, $"{location} がオブジェクトではありません。");
                return null;
            }

            var kindStr = (step["kind"] as JValue)?.Value as string;
            switch (kindStr)
            {
                case "line":
                {
                    kind = StepKind.Line;
                    var speaker = (step["speaker"] as JValue)?.Value as string;
                    var portrait = (step["portrait"] as JValue)?.Value as string;
                    var text = (step["text"] as JValue)?.Value as string;
                    if (string.IsNullOrEmpty(text))
                    {
                        report.Error(id, $"{location} line は text が必須。");
                        return null;
                    }
                    if (!string.IsNullOrEmpty(portrait) && !PortraitKeys.Contains(portrait))
                    {
                        report.Error(
                            id,
                            $"{location} line の portrait '{portrait}' はカタログに存在しません。"
                        );
                        return null;
                    }
                    // カタログにはあるが立ち絵アセット未登録 = 実行時は既定の立ち絵に落ちる。
                    // 絵の準備待ちでも書き進められるようエラーにはせず警告にとどめる。
                    if (
                        !string.IsNullOrEmpty(portrait)
                        && catalog?.RegisteredPortraitKeys != null
                        && !catalog.RegisteredPortraitKeys.Contains(portrait)
                    )
                        report.Warn(
                            id,
                            $"{location} line の portrait '{portrait}' は立ち絵アセット未登録です(実行時は既定の立ち絵になります)。"
                        );
                    return EventStep.Line(speaker, portrait, text);
                }

                case "choice":
                {
                    kind = StepKind.Choice;
                    // JSON 側のキー名は "flag"。内部フィールドは flagKey。
                    var flagKey = (step["flag"] as JValue)?.Value as string;
                    if (string.IsNullOrEmpty(flagKey))
                    {
                        report.Error(id, $"{location} choice は flag(書き込むキー)が必須。");
                        return null;
                    }
                    // 選択肢UIは3択ぶんの高さを基準に中央寄せするレイアウトなので、4つ以上は
                    // 会話ウィンドウに被る(DialogueChoicePresenter.UpdateLayout)。
                    // 1つだけの choice は選ばせる意味が無く、書き間違いの方が多いので弾く。
                    if (
                        step["options"] is not JArray options
                        || options.Count < MinChoiceOptions
                        || options.Count > MaxChoiceOptions
                    )
                    {
                        report.Error(
                            id,
                            $"{location} choice の options は{MinChoiceOptions}〜{MaxChoiceOptions}個にしてください。"
                        );
                        return null;
                    }
                    var parsed = new List<ChoiceOption>();
                    bool optionsOk = true;
                    for (int o = 0; o < options.Count; o++)
                    {
                        var opt = options[o] as JObject;
                        var optText = (opt?["text"] as JValue)?.Value as string;
                        var optValue = (opt?["value"] as JValue)?.Value as string;
                        if (string.IsNullOrEmpty(optText) || string.IsNullOrEmpty(optValue))
                        {
                            report.Error(
                                id,
                                $"{location} choice の options[{o}] は text と value が必須。"
                            );
                            optionsOk = false;
                            continue;
                        }
                        parsed.Add(new ChoiceOption(optText, optValue));
                    }
                    if (
                        parsed.Select(o => o.Value).Distinct(StringComparer.Ordinal).Count()
                        != parsed.Count
                    )
                    {
                        report.Error(id, $"{location} choice の value が重複しています。");
                        optionsOk = false;
                    }
                    if (!optionsOk)
                        return null;
                    return EventStep.Choice(flagKey, parsed.ToArray());
                }

                case "giveItem":
                {
                    kind = StepKind.GiveItem;
                    var itemKey = (step["itemKey"] as JValue)?.Value as string;
                    if (string.IsNullOrEmpty(itemKey))
                    {
                        report.Error(id, $"{location} giveItem は itemKey が必須。");
                        return null;
                    }
                    if (catalog?.ItemKeys != null)
                    {
                        if (!catalog.ItemKeys.Contains(itemKey))
                        {
                            report.Error(
                                id,
                                $"{location} giveItem の itemKey '{itemKey}' が ItemData カタログに存在しません。"
                            );
                            return null;
                        }
                    }
                    else
                    {
                        report.Warn(
                            id,
                            $"{location} giveItem の itemKey '{itemKey}' は未検証(item カタログ未提供)。"
                        );
                    }
                    return EventStep.GiveItem(
                        itemKey,
                        (step["message"] as JValue)?.Value as string
                    );
                }

                case "giveWeapon":
                {
                    kind = StepKind.GiveWeapon;
                    var weaponKey = (step["weaponKey"] as JValue)?.Value as string;
                    if (string.IsNullOrEmpty(weaponKey))
                    {
                        report.Error(id, $"{location} giveWeapon は weaponKey が必須。");
                        return null;
                    }
                    // 武器は剣/弓/鎌/ハンマーの4種で固定。それ以外は打ち間違いとして弾く。
                    if (!WeaponKeys.Contains(weaponKey))
                    {
                        report.Error(
                            id,
                            $"{location} giveWeapon の weaponKey '{weaponKey}' は不正(sword / bow / scythe / hammer のみ)。"
                        );
                        return null;
                    }
                    return EventStep.GiveWeapon(
                        weaponKey,
                        (step["message"] as JValue)?.Value as string
                    );
                }

                case "command":
                {
                    kind = StepKind.Command;
                    var command = ((step["command"] as JValue)?.Value as string)?.Trim();
                    if (string.IsNullOrEmpty(command))
                    {
                        report.Error(id, $"{location} command は command(コマンド名)が必須。");
                        return null;
                    }
                    if (!IsKnownCommand(command))
                    {
                        report.Error(id, $"{location} command の '{command}' は未対応です。");
                        return null;
                    }
                    var arg = (step["arg"] as JValue)?.Value?.ToString();
                    // wait だけは秒数が要る(欠けると何も待たずに素通りする)。
                    if (command == "wait")
                    {
                        if (
                            !float.TryParse(
                                arg,
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out var seconds
                            )
                        )
                        {
                            report.Error(
                                id,
                                $"{location} command 'wait' は arg に秒数(数値)が必須。"
                            );
                            return null;
                        }
                        if (seconds < 0f || seconds > MaxWaitSeconds)
                        {
                            report.Error(
                                id,
                                $"{location} command 'wait' の秒数 {arg} は 0〜{MaxWaitSeconds} の範囲にしてください。"
                            );
                            return null;
                        }
                    }
                    return EventStep.Command(command, arg);
                }

                case "battle":
                {
                    kind = StepKind.Battle;
                    // 敵は JSON に書かない。シーンの EventTrigger の Enemy スロットに Prefab を配線する。
                    if (step["enemyKey"] != null)
                    {
                        report.Error(
                            id,
                            $"{location} battle に enemyKey は書けません(敵は EventTrigger に配線)。"
                        );
                        return null;
                    }
                    return EventStep.Battle();
                }

                default:
                    report.Error(
                        id,
                        $"{location} の kind '{kindStr}' は不正(line / choice / giveItem / giveWeapon / battle / command のみ)。"
                    );
                    return null;
            }
        }

        /// <summary>
        /// battle 配置制約: 先頭・末尾は line。battle は会話の途中のみ。
        /// かつ 1イベントにつき battle は最大1つ。
        /// kind が解析できなかったステップ(null)は既に別途エラー済みなのでここでは無視する。
        /// </summary>
        private static bool ValidateBattlePlacement(StepKind?[] kinds, string id, Report report)
        {
            if (kinds.Length == 0)
                return true;

            bool ok = true;
            int battleCount = kinds.Count(k => k == StepKind.Battle);

            // 先頭・末尾 line は「battle が単独・末尾にならない」ための battle 制約。
            // battle を含まないイベントには適用しない。
            if (battleCount > 0)
            {
                if (kinds[0] is StepKind first && first != StepKind.Line)
                {
                    report.Error(id, "steps の先頭は line である必要があります(battle 配置制約)。");
                    ok = false;
                }
                if (kinds[^1] is StepKind last && last != StepKind.Line)
                {
                    report.Error(id, "steps の末尾は line である必要があります(battle 配置制約)。");
                    ok = false;
                }
            }

            if (battleCount > 1)
            {
                report.Error(id, $"battle は1イベントにつき最大1つです(現在 {battleCount} 個)。");
                ok = false;
            }
            return ok;
        }
    }
}
#endif
