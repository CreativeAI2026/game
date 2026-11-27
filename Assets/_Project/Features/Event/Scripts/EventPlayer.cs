using System;
using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 会話イベントを順に再生し、終了時に進行度を進める指揮役。
    /// イベントの間で持ち越す状態は無いので常駐させず、発火する側(EventTrigger / EventInteractor)が持って
    /// <see cref="Play"/> を自分のコルーチンとして回す。発火側が無効になったら <see cref="Abort"/> で後始末する。
    /// 同時に再生できるのは1本だけで、いま再生しているものは <see cref="Playing"/> が持つ。
    /// </summary>
    public sealed class EventPlayer
    {
        /// <summary>いまイベントを再生している EventPlayer。再生していなければ null。</summary>
        public static EventPlayer Playing { get; private set; }

        /// <summary>この EventPlayer がイベントを再生中か。</summary>
        public bool IsPlaying => Playing == this;

        // Domain Reload を切った Editor で Play を繰り返したとき、前回の再生中を持ち越さないよう、Play 開始時に戻す
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetState() => Playing = null;

        // 渡されたものがあればそれを、無ければ本番の窓口・常駐を毎回見る。
        private readonly IEventProgress _progress;
        private readonly IEventGameMode _gameMode;
        private readonly IDialogueView _view;
        private readonly IItemGiver _items;
        private readonly IWeaponGiver _weapons;
        private readonly IEventBattle _battle;
        private readonly EventPlayerMovement _movement = new();
        private string _playingId; // 再生中のイベントの ID(二重発火の警告に出す)

        /// <summary>
        /// 依存を指定して作る(プレビュー / テスト用)。null の依存は本番の窓口・常駐を使う。
        /// </summary>
        public EventPlayer(
            IEventProgress progress = null,
            IEventGameMode gameMode = null,
            IDialogueView view = null,
            IItemGiver items = null,
            IWeaponGiver weapons = null,
            IEventBattle battle = null
        )
        {
            _progress = progress;
            _gameMode = gameMode;
            _view = view;
            _items = items;
            _weapons = weapons;
            _battle = battle ?? new EncounterBattle();
        }

        private IDialogueView View =>
            _view ?? (UIManager.Instance != null ? UIManager.Instance.DialogueView : null);
        private IItemGiver Items => _items ?? InventoryManager.Instance;
        private IWeaponGiver Weapons => _weapons ?? WeaponManager.Instance;
        private IEventProgress Progress => _progress ?? ProgressManager.Instance;
        private IEventGameMode GameModes => _gameMode ?? GameModeManager.Instance;

        // 持ち物から選ばせ、正解なら true で返る。やめた・間違えて終了(EndOnWrong)・UI が無いときは false。
        // 間違いは消費せずセリフを出し、既定では選び直させる。
        private IEnumerator RunUseItem(EventDefinition ev, EventStep step, Action<bool> done)
        {
            if (View == null || Items == null)
            {
                Debug.LogWarning(
                    $"[EventPlayer] useItem を実行できません(event={ev.Id}, 会話UI={(View != null ? "あり" : "なし")}, IItemGiver={(Items != null ? "あり" : "なし")})。"
                );
                done(false);
                yield break;
            }

            while (true)
            {
                string picked = null;
                yield return View.ShowItemPicker(step.Prompt, value => picked = value);
                if (picked == null)
                {
                    // やめた: セリフがあれば出してから打ち切る。
                    if (!string.IsNullOrEmpty(step.CancelText))
                        yield return View.ShowLine(step.Speaker, step.Portrait, step.CancelText);
                    done(false);
                    yield break;
                }

                if (IsCorrectUse(step, picked))
                {
                    // 使った実感を出す: セリフがあれば、次のステップへ進む前に出す。
                    if (!string.IsNullOrEmpty(step.UsedText))
                        yield return View.ShowLine(step.Speaker, step.Portrait, step.UsedText);
                    done(true);
                    yield break;
                }

                yield return View.ShowLine(step.Speaker, step.Portrait, step.Text);
                if (step.EndOnWrong)
                {
                    done(false);
                    yield break;
                }
            }
        }

        // 選んだアイテムが正解の候補で、個数が足りれば true(消費する設定ならここで消費する)。
        private bool IsCorrectUse(EventStep step, string itemKey)
        {
            UseItemCandidate match = null;
            foreach (var candidate in step.Accept)
            {
                if (candidate != null && candidate.ItemKey == itemKey)
                {
                    match = candidate;
                    break;
                }
            }
            if (match == null || Items.CountItem(itemKey) < match.Count)
                return false;
            return !step.Consume || Items.Consume(itemKey, match.Count);
        }

        /// <summary>
        /// 会話ステップを順に再生し、終了時に進行度を進める(コルーチン)。
        /// battle ステップは、ボスシーンの BattleEncounter を名前(arena)で呼んで戦う。
        /// 別のイベントを再生中か、フィールド以外(戦闘・メニューの最中)なら何もしない。
        /// </summary>
        public IEnumerator Play(EventDefinition ev)
        {
            if (ev == null)
                yield break;

            // 同時に再生できるのは1本だけ。2本目が会話UIを取り合い、先に終わった側がモードを戻してしまうため弾く。
            if (Playing != null)
            {
                Debug.LogWarning(
                    $"[EventPlayer] 別のイベント('{Playing._playingId}')を再生中のため '{ev.Id}' を無視しました。"
                );
                yield break;
            }
            // 会話はメニューを閉じた探索中からだけ始まる(戦闘中・メニュー中は GameModeManager が断る)。
            if (GameModes != null && !GameModes.TryStartDialogue())
            {
                Debug.LogWarning(
                    $"[EventPlayer] 探索中ではない({GameModeManager.Current})ため '{ev.Id}' を無視しました。"
                );
                yield break;
            }

            if (View == null)
                Debug.LogWarning(
                    $"[EventPlayer] IDialogueView 未設定 (event={ev.Id}). 会話は表示されません。"
                );

            // ここで会話になっている(操作不能)。途中で打ち切っても finally で必ず会話を終える。
            Playing = this;
            _playingId = ev.Id;
            try
            {
                for (int stepIndex = 0; stepIndex < ev.Steps.Count; stepIndex++)
                {
                    var step = ev.Steps[stepIndex];
                    if (step == null)
                        continue;

                    if (step.Kind == StepKind.Branch || step.Kind == StepKind.Jump)
                    {
                        int target = step.ResolveTarget(
                            key => Progress?.GetFlag(key),
                            key => Items?.HasImportantItem(key) ?? false
                        );
                        if (target <= stepIndex || target > ev.Steps.Count)
                        {
                            Debug.LogError(
                                $"[EventPlayer] 分岐先 {target} が不正です（位置={stepIndex}, event={ev.Id}）。"
                            );
                            if (View != null)
                                yield return View.Close();
                            yield break;
                        }
                        stepIndex = target - 1;
                        continue;
                    }

                    // 戦闘中に会話ウィンドウが残らないよう、battle の前に閉じる。
                    if (step.Kind == StepKind.Battle && View != null)
                        yield return View.Close();

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
                            {
                                Progress?.SetFlag(step.FlagKey, picked);
                            }
                            break;

                        case StepKind.SetFlag:
                            Progress?.SetFlag(step.FlagKey, step.FlagValue ?? string.Empty);
                            break;

                        case StepKind.GiveItem:
                            if (Items == null)
                                Debug.LogWarning(
                                    $"[EventPlayer] 所持品が無い (event={ev.Id}). giveItem '{step.ItemKey}' は演出だけで所持品には入りません。"
                                );
                            else
                                Items.Give(step.ItemKey);
                            // 入手演出は会話UI側(絵と名前は itemKey から UI が引く)。
                            // 会話UIが無い場面(テスト等)は在庫に入るだけで演出は出ない。
                            if (View != null)
                                yield return View.ShowItemGet(step.ItemKey, step.Message);
                            break;

                        case StepKind.GiveWeapon:
                            if (Weapons == null)
                                Debug.LogWarning(
                                    $"[EventPlayer] 武器の届け先が無い (event={ev.Id}). giveWeapon '{step.WeaponKey}' をスキップ。"
                                        + " 武器の常駐(WeaponManager)が無いシーンでは武器は渡されません。"
                                );
                            else
                                Weapons.GiveWeapon(step.WeaponKey);
                            if (View != null)
                                yield return View.ShowWeaponGet(step.WeaponKey, step.Message);
                            break;

                        case StepKind.Battle:
                            // 戦闘モードの出入りは BattleEncounter が行い、決着したら会話モードへ戻す。
                            yield return _battle.Fight(step.EncounterId);
                            break;

                        case StepKind.UseItem:
                            bool useItemDone = false;
                            yield return RunUseItem(ev, step, ok => useItemDone = ok);
                            if (!useItemDone)
                            {
                                // やめた・間違えて終了: 進行度も 1 回きりの印も進めずに打ち切る。
                                if (View != null)
                                    yield return View.Close();
                                yield break;
                            }
                            break;

                        case StepKind.Command:
                            if (EventPlayerMovement.Handles(step.CommandName))
                                yield return _movement.Run(step.CommandName, step.Arg);
                            else if (View != null)
                                yield return View.RunCommand(step.CommandName, step.Arg);
                            break;
                    }
                }

                // 最後の台詞を出したまま終わらないよう、会話ウィンドウを閉じてから進行度を進める。
                if (View != null)
                    yield return View.Close();
                // 1回きりのイベントは、見終えた印をセーブされるフラグに残す(二度と発火しなくなる)。
                if (ev.Repeat == EventRepeat.Once)
                    Progress?.SetFlag(ProgressFlags.Seen(ev.Id), ProgressFlags.SeenValue);
                if (ev.AdvancesProgress)
                    Progress?.AdvanceTo(ev.NextProgress);
            }
            finally
            {
                Finish();
            }
        }

        /// <summary>
        /// 再生中なら途中で打ち切る(進行度は進めない)。会話ウィンドウはその場で消し、フィールドへ戻す。
        /// 再生を回していたコルーチンが止まったとき(発火側が無効になった・シーンごと消えた)に発火側が呼ぶ。
        /// </summary>
        public void Abort()
        {
            if (!IsPlaying)
                return;
            Debug.LogWarning("[EventPlayer] 再生中のイベントを打ち切りました(進行度は進めない)。");
            View?.HideImmediate();
            Finish();
        }

        private void Finish()
        {
            _movement.Stop();
            if (Playing == this)
                Playing = null;
            _playingId = null;
            GameModes?.EndDialogue();
        }

        // 本番の戦闘。シーンに置いた BattleEncounter を名前で呼ぶ。
        private sealed class EncounterBattle : IEventBattle
        {
            public IEnumerator Fight(string encounterId) =>
                BattleEncounter.FightFromEvent(encounterId);
        }
    }
}
