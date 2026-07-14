using System.Collections;
using UnityEngine;

namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// EventTrigger に発火を託され、1本の会話イベントを頭から順に再生し切る指揮役(非常駐)。
    /// 各ステップで会話UI(IDialogueView) / Inventory(IItemGiver) / ProgressManager を叩き、
    /// 終了時に進行度を進める。documents/Specification.md §4, EventImplementation.md 参照。
    /// </summary>
    public sealed class EventPlayer : MonoBehaviour, IEventPlayer
    {
        [SerializeField]
        private ProgressManager _progress; // 未設定なら ProgressManager.Instance にフォールバック

        [SerializeField]
        private MonoBehaviour _dialogueView; // IDialogueView を実装する MonoBehaviour(UI 側)

        [SerializeField]
        private MonoBehaviour _itemGiver; // IItemGiver を実装する MonoBehaviour(Gameplay 側)

        [SerializeField]
        private MonoBehaviour _battleRunner; // IBattleRunner を実装する MonoBehaviour(戦闘班)

        [SerializeField]
        private GameModeManager _gameMode; // 未設定なら GameModeManager.Instance にフォールバック

        private IDialogueView _view;
        private IItemGiver _items;
        private IBattleRunner _battle;

        private IDialogueView View => _view ??= _dialogueView as IDialogueView;
        private IItemGiver Items => _items ??= _itemGiver as IItemGiver;
        private IBattleRunner BattleRunner => _battle ??= _battleRunner as IBattleRunner;
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

        public void Play(EventDefinition ev)
        {
            if (ev == null)
                return;
            StartCoroutine(PlayRoutine(ev));
        }

        /// <summary>
        /// 会話ステップを順に再生し、終了時に AdvanceTo する本体。
        /// テストは fake を注入し、この IEnumerator を駆動して検証する。
        /// </summary>
        public IEnumerator PlayRoutine(EventDefinition ev)
        {
            if (ev == null)
                yield break;

            if (View == null)
                Debug.LogWarning(
                    $"[EventPlayer] IDialogueView 未設定 (event={ev.Id}). 会話は表示されません。"
                );

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
                        if (BattleRunner != null)
                            yield return BattleRunner.Run(step.EnemyKey);
                        else
                            Debug.LogWarning(
                                $"[EventPlayer] IBattleRunner 未設定 (enemy={step.EnemyKey}). 戦闘をスキップ。"
                            );
                        mode?.ExitBattle();
                        break;
                }
            }

            if (ev.HasNextProgress)
                Progress?.AdvanceTo(ev.NextProgress);
        }
    }
}
