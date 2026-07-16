using System.Collections;
using UnityEngine;

namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// EventTrigger に発火を託され、1本の会話イベントを頭から順に再生し切る指揮役。
    /// Title フローで EnsureResident により常駐生成され、EventPlayerService.Current に自身を登録する
    /// (非常駐の EventTrigger はこの seam 経由で受け取るため、per-field 配線は不要)。
    /// 各ステップで会話UI(IDialogueView) / Inventory(IItemGiver) / ProgressManager を叩き、
    /// 終了時に進行度を進める。documents/Specification.md §4, §6, EventImplementation.md 参照。
    /// </summary>
    public sealed class EventPlayer : MonoBehaviour, IEventPlayer
    {
        public static EventPlayer Instance { get; private set; }

        /// <summary>セッション常駐生成の入口。既に在ればそれを返す(SessionBootstrap から呼ぶ)。</summary>
        public static EventPlayer EnsureResident()
        {
            if (Instance != null)
                return Instance;
            return new GameObject(nameof(EventPlayer)).AddComponent<EventPlayer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EventPlayerService.Current = this; // EventTrigger の発火先 seam に自身を登録
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (ReferenceEquals(EventPlayerService.Current, this))
                EventPlayerService.Current = null;
        }

        [SerializeField]
        private ProgressManager _progress; // 未設定なら ProgressManager.Instance にフォールバック

        [SerializeField]
        private MonoBehaviour _dialogueView; // IDialogueView を実装する MonoBehaviour(UI 側)

        [SerializeField]
        private MonoBehaviour _itemGiver; // IItemGiver 実装。未設定なら ItemGiverService.Current(= InventoryManager)にフォールバック

        [SerializeField]
        private MonoBehaviour _battleRunner; // IBattleRunner 実装。未設定なら BattleRunnerService.Current(= BattleRunner)にフォールバック

        [SerializeField]
        private GameModeManager _gameMode; // 未設定なら GameModeManager.Instance にフォールバック

        private IDialogueView _view;
        private IItemGiver _items;
        private IBattleRunner _battle;

        private IDialogueView View =>
            _view ??= (_dialogueView as IDialogueView) ?? DialogueViewService.Current;
        private IItemGiver Items =>
            _items ??= (_itemGiver as IItemGiver) ?? ItemGiverService.Current;
        private IBattleRunner BattleRunner =>
            _battle ??= (_battleRunner as IBattleRunner) ?? BattleRunnerService.Current;
        private ProgressManager Progress =>
            _progress != null ? _progress : ProgressManager.Instance;
        private GameModeManager GameModes =>
            _gameMode != null ? _gameMode : GameModeManager.Instance;

        /// <summary>Inspector 配線の代わりに依存を注入する(ランタイム bootstrap / テスト用)。</summary>
        public void Inject(
            ProgressManager progress,
            IDialogueView view,
            IItemGiver items,
            IBattleRunner battle = null,
            GameModeManager gameMode = null
        )
        {
            _progress = progress;
            _view = view;
            _items = items;
            _battle = battle;
            _gameMode = gameMode;
        }

        public void Play(EventDefinition ev, BattleSetup battle = default)
        {
            if (ev == null)
                return;
            StartCoroutine(PlayRoutine(ev, battle));
        }

        /// <summary>
        /// 会話ステップを順に再生し、終了時に AdvanceTo する本体。
        /// battle ステップは <paramref name="battle"/>(トリガーが配線した敵)を使う。
        /// テストは fake を注入し、この IEnumerator を駆動して検証する。
        /// </summary>
        public IEnumerator PlayRoutine(EventDefinition ev, BattleSetup battle = default)
        {
            if (ev == null)
                yield break;

            if (View == null)
                Debug.LogWarning(
                    $"[EventPlayer] IDialogueView 未設定 (event={ev.Id}). 会話は表示されません。"
                );

            // 会話イベント中は操作不能。右上ナビ(セーブ/インベ入口)を隠すため再生中フラグを立てる
            // (documents/Specification.md §2.2, §5)。中断されても finally で必ず戻す。
            EventPlaybackService.SetPlaying(true);
            try
            {
                foreach (var step in ev.Steps)
                {
                    if (step == null)
                        continue;

                    switch (step.Kind)
                    {
                        case StepKind.Line:
                            if (View != null)
                                yield return View.ShowLine(step.Speaker, step.Portrait, step.Text);
                            break;

                        case StepKind.Choice:
                            string picked = null;
                            if (View != null)
                                yield return View.ShowChoice(step.Options, v => picked = v);
                            if (!string.IsNullOrEmpty(picked))
                                Progress?.SetFlag(step.FlagKey, picked);
                            break;

                        case StepKind.GiveItem:
                            Items?.Give(step.ItemKey);
                            break;

                        case StepKind.Battle:
                            var mode = GameModes;
                            mode?.EnterBattle();
                            if (BattleRunner == null)
                                Debug.LogWarning(
                                    $"[EventPlayer] IBattleRunner 未設定 (event={ev.Id}). 戦闘をスキップ。"
                                );
                            else if (!battle.HasEnemy)
                                Debug.LogWarning(
                                    $"[EventPlayer] battle ステップに敵 Prefab が未配線 (event={ev.Id})."
                                        + " EventTrigger の Enemy スロットにアサインしてください。戦闘をスキップ。"
                                );
                            else
                                yield return BattleRunner.Run(battle);
                            mode?.ExitBattle();
                            break;
                    }
                }

                if (ev.HasNextProgress)
                    Progress?.AdvanceTo(ev.NextProgress);
            }
            finally
            {
                EventPlaybackService.SetPlaying(false);
            }
        }
    }
}
