using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CreativeAI.Core
{
    /// <summary>
    /// EventTrigger から託された会話イベントを順に再生し、終了時に進行度を進める指揮役。
    /// 常駐生成され EventPlayerService.Current に自身を登録する(per-field 配線は不要)。
    /// </summary>
    public sealed class EventPlayer : MonoBehaviour, IEventPlayer
    {
        public static EventPlayer Instance { get; private set; }

        /// <summary>セッション常駐生成の入口。既に在ればそれを返す(UI 層の GameSession から呼ぶ)。</summary>
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
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (Instance == this)
                Instance = null;
            if (ReferenceEquals(EventPlayerService.Current, this))
                EventPlayerService.Current = null;
        }

        // シーンが切り替わるたびに進む番号。再生開始時から変わっていたら、そのイベントは打ち切る。
        // 常駐なので再生中のコルーチンはシーンをまたいで生き残る。戦闘に負けてセーブ再開(シーン再読込)すると
        // 敵が消えて battle が抜け、そのまま続きを流して進行度を進めてしまうのを防ぐ。
        private int _sceneGeneration;

        private void OnActiveSceneChanged(Scene previous, Scene next) => _sceneGeneration++;

        // Inject されたものを優先し、無ければ各 seam / Instance にフォールバックする。
        private IEventProgress _progress;
        private IEventGameMode _gameMode;
        private IDialogueView _view;
        private IItemGiver _items;
        private IWeaponGiver _weapons;
        private IBattleRunner _battle;

        private IDialogueView View => _view ??= DialogueViewService.Current;
        private IItemGiver Items => _items ??= ItemGiverService.Current;
        private IWeaponGiver Weapons => _weapons ??= WeaponGiverService.Current;
        private IBattleRunner BattleRunner => _battle ??= BattleRunnerService.Current;
        private IEventProgress Progress =>
            _progress is UnityEngine.Object obj && obj == null
                ? ProgressManager.Instance
                : _progress ?? ProgressManager.Instance;
        private IEventGameMode GameModes =>
            _gameMode is UnityEngine.Object obj && obj == null
                ? GameModeManager.Instance
                : _gameMode ?? GameModeManager.Instance;

        /// <summary>seam の代わりに依存を注入する(プレビュー / テスト用)。</summary>
        public void Inject(
            IEventProgress progress,
            IDialogueView view,
            IItemGiver items,
            IBattleRunner battle = null,
            IEventGameMode gameMode = null,
            IWeaponGiver weapons = null
        )
        {
            _progress = progress;
            _view = view;
            _items = items;
            _battle = battle;
            _gameMode = gameMode;
            _weapons = weapons;
        }

        /// <summary>一時的に依存を差し替え、破棄時に元の注入状態へ戻す。</summary>
        public IDisposable InjectScoped(
            IEventProgress progress,
            IDialogueView view,
            IItemGiver items,
            IBattleRunner battle = null,
            IEventGameMode gameMode = null,
            IWeaponGiver weapons = null
        )
        {
            var previous = (_progress, _view, _items, _battle, _gameMode, _weapons);
            Inject(progress, view, items, battle, gameMode, weapons);
            return new DependencyScope(() =>
            {
                (_progress, _view, _items, _battle, _gameMode, _weapons) = previous;
            });
        }

        private sealed class DependencyScope : IDisposable
        {
            private Action _restore;

            public DependencyScope(Action restore) => _restore = restore;

            public void Dispose()
            {
                var restore = _restore;
                _restore = null;
                restore?.Invoke();
            }
        }

        /// <summary>
        /// プレイヤーを <see cref="SpawnPoint"/>(arg が ID)へ移す command 名。会話UIではなくここで実行する。
        /// 引き戻しに使う。画面を隠したいときは前後に background.black.fill / clear を並べる。
        /// </summary>
        public const string PlayerMoveCommand = "player.moveTo";

        private const string PlayerTag = "Player";

        // 動かす相手(プレイヤー)と、目的地の目印を探す。見つからなければ、動かさずに警告して false。
        // 目印が無いとき SpawnPoint.Place は原点へ置いてしまうので、先に確かめる。
        private static bool TryFindPlayerAndPoint(
            string command,
            string spawnPointId,
            out GameObject player,
            out SpawnPoint point
        )
        {
            player = GameObject.FindWithTag(PlayerTag);
            point = SpawnPoint.Find(spawnPointId);
            if (player != null && point != null)
                return true;

            Debug.LogWarning(
                $"[EventPlayer] {command} を実行できません"
                    + $"(player={(player != null ? player.name : "なし")}, SpawnPoint ID='{spawnPointId}')。"
            );
            return false;
        }

        private static void MovePlayer(string spawnPointId)
        {
            if (TryFindPlayerAndPoint(PlayerMoveCommand, spawnPointId, out var player, out _))
                SpawnPoint.Place(player, spawnPointId);
        }

        /// <summary>
        /// プレイヤーを <see cref="SpawnPoint"/>(arg が ID)まで歩かせる command 名。着くまで次のステップへ進まない。
        /// <see cref="PlayerMoveCommand"/> と違い、瞬間移動せず歩きのモーションで向かう。引き戻しに使う。
        /// </summary>
        public const string PlayerWalkCommand = "player.walkTo";

        /// <summary>歩きが終わらないとき(壁に引っかかる等)に、想定の歩行時間へ掛ける余裕の倍率。</summary>
        private const float WalkTimeoutScale = 2f;

        /// <summary>歩行時間の下限に足す秒数。ごく近い目標でも、動き出しの時間は待つ。</summary>
        private const float WalkTimeoutPadding = 2f;

        // 歩かせている最中のプレイヤー。イベントを途中で打ち切ったとき、歩く指示を残さないための後始末に使う。
        private IPlayerWalker _walking;

        // 歩いて着くまで待つ。プレイヤーに歩かせる口(IPlayerWalker)が無いときは、動かさずに警告する。
        // 時間内に着かない(壁に引っかかる等)ときは、止まって瞬間移動で置き、操作が戻れなくなるのを防ぐ。
        private IEnumerator WalkPlayer(string spawnPointId)
        {
            if (
                !TryFindPlayerAndPoint(
                    PlayerWalkCommand,
                    spawnPointId,
                    out var player,
                    out var point
                )
            )
                yield break;
            if (!player.TryGetComponent(out IPlayerWalker walker))
            {
                Debug.LogWarning(
                    $"[EventPlayer] {PlayerWalkCommand} を実行できません(player='{player.name}' に IPlayerWalker がありません)。"
                );
                yield break;
            }

            var target = point.transform.position;
            var flat = target - player.transform.position;
            flat.y = 0f;
            var timeout =
                flat.magnitude / Mathf.Max(walker.WalkSpeed, 0.01f) * WalkTimeoutScale
                + WalkTimeoutPadding;

            _walking = walker;
            walker.WalkTo(target);
            var elapsed = 0f;
            while (walker.IsWalking && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (walker.IsWalking)
            {
                Debug.LogWarning(
                    $"[EventPlayer] {PlayerWalkCommand} が {timeout:F1} 秒で着かなかったため、'{spawnPointId}' へ直接置きました。"
                );
                StopWalking();
                SpawnPoint.Place(player, spawnPointId);
            }
            _walking = null;
        }

        // 歩かせている最中なら止める。イベントを打ち切っても、あとで勝手に歩き出さないようにする。
        private void StopWalking()
        {
            _walking?.StopWalking();
            _walking = null;
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

            // 同時に再生できるのは1本だけ。2本目が会話UIを取り合い、先に終わった側が再生中フラグを戻してしまうため弾く。
            if (EventPlaybackService.IsPlaying)
            {
                Debug.LogWarning(
                    $"[EventPlayer] 別のイベントを再生中のため '{ev.Id}' を無視しました。"
                );
                yield break;
            }

            if (View == null)
                Debug.LogWarning(
                    $"[EventPlayer] IDialogueView 未設定 (event={ev.Id}). 会話は表示されません。"
                );

            // 会話イベント中は操作不能。右上ナビ(セーブ/インベ入口)を隠すため再生中フラグを立てる。
            // 途中で打ち切っても finally で必ず戻す。
            int generation = _sceneGeneration;
            EventPlaybackService.SetPlaying(true);
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
                                    $"[EventPlayer] IItemGiver 未登録 (event={ev.Id}). giveItem '{step.ItemKey}' は演出だけで所持品には入りません。"
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
                                    $"[EventPlayer] IWeaponGiver 未登録 (event={ev.Id}). giveWeapon '{step.WeaponKey}' をスキップ。"
                                        + " プレイヤーリグ(WeaponManager)が居ないシーンでは武器は渡されません。"
                                );
                            else
                                Weapons.GiveWeapon(step.WeaponKey);
                            if (View != null)
                                yield return View.ShowWeaponGet(step.WeaponKey, step.Message);
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

                        case StepKind.Command:
                            if (step.CommandName == PlayerMoveCommand)
                                MovePlayer(step.Arg);
                            else if (step.CommandName == PlayerWalkCommand)
                                yield return WalkPlayer(step.Arg);
                            else if (View != null)
                                yield return View.RunCommand(step.CommandName, step.Arg);
                            break;
                    }

                    if (generation != _sceneGeneration)
                    {
                        Debug.LogWarning(
                            $"[EventPlayer] 再生中にシーンが切り替わったため '{ev.Id}' を打ち切りました(進行度は進めない)。"
                        );
                        if (View != null)
                            yield return View.Close();
                        yield break;
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
                StopWalking();
                EventPlaybackService.SetPlaying(false);
            }
        }
    }

    /// <summary>
    /// 会話イベント再生の指揮役。EventTrigger が条件成立時に発火を託す。
    /// </summary>
    public interface IEventPlayer
    {
        /// <summary>
        /// イベントを再生する。battle ステップがあれば <paramref name="battle"/> の Prefab を
        /// トリガー位置に出して戦う(敵未配線なら警告してスキップ)。battle が無いイベントでは
        /// <paramref name="battle"/> は使われない(default で可)。
        /// </summary>
        void Play(EventDefinition ev, BattleSetup battle = default);
    }

    /// <summary>
    /// 実行時の IEventPlayer を登録する seam。EventPlayer は常駐生成で drag 配線できないため EnsureResident 時に登録し、
    /// 非常駐の EventTrigger は Inspector 未配線時のフォールバックとして見る。
    /// </summary>
    public static class EventPlayerService
    {
        public static IEventPlayer Current { get; set; }
    }

    /// <summary>
    /// このプロジェクトは Editor の Enter Play Mode で Domain Reload を切っているため、static な値が前回の Play から持ち越される。
    /// 会話の途中で Play を止めると再生中フラグが立ったまま残り、次の Play で HUD が出ない・動けない・発火しない状態になるので、
    /// Play 開始時に Event 関連の static(再生中フラグ・各 seam)を初期化する。ビルドでは起動時に1回走るだけで無害。
    /// </summary>
    public static class EventStatics
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForPlaySession()
        {
            EventPlaybackService.ResetState();
            EventPlayerService.Current = null;
            DialogueViewService.Current = null;
            BattleRunnerService.Current = null;
            ItemGiverService.Current = null;
            WeaponGiverService.Current = null;
        }
    }
}
