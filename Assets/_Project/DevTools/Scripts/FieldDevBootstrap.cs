using System.Collections;
using CreativeAI.Gameplay;
using CreativeAI.Session;
using CreativeAI.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CreativeAI.DevTools
{
    /// <summary>
    /// 開発用シーンを Title を経由せず直接 Play したとき、常駐システム(マネージャ / Inventory / UIRoot / 会話UI)を
    /// Title と同じ手順(GameSession)で生成する開発用ブートストラップ。Title 経由なら何もしない(冪等)。
    /// Field シーン側に常駐のコピーを持たせないためのもの。直接 Play 時は所持品にテスト品を積み、
    /// <c>dev_state.json</c>(<see cref="DevState"/>)があればその進行度・フラグ・大事なもの・武器・HP で始める。
    /// シーンに直接置いたプレイヤーが Awake で常駐を読むので、シーン内のどれよりも先に Awake する。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class FieldDevBootstrap : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("直接 Play 時に所持品へテスト品を積む(UI開発用)。Title 経由では積まれない。")]
        private bool _seedTestItems = true;

        [SerializeField]
        [Tooltip("直接Play時にプレイヤーを生成します。既存のMain Cameraは再利用します。")]
        private bool _spawnPlayerRig;

        [
            SerializeField,
            Tooltip("UI確認用プレイヤーをゲーム画面に表示せず、操作と物理を停止します。")
        ]
        private bool _previewOnlyPlayer;

        [SerializeField]
        [Tooltip(
            "リグを出す位置の SpawnPoint ID。既定は start(「はじめから」は new_game を使うので、直接再生とは別の位置)。"
        )]
        private string _spawnPointId = SpawnPoint.StartId;

        private DevState _devState;

        private void Awake()
        {
            // タイトルから始めたときは、セーブの復元や「はじめから」の初期状態を上書きしないよう何もしない。
            // (下の判定は「プレイヤーが居る」も見るが、プレイヤーはシーンの Awake より後に作られるので通り抜けてしまう)
            if (GameSession.StartedFromTitle)
                return;

            // Title 経由で既に常駐が生成済みなら何もしない(UIManager の有無で判定)。
            if (
                UIManager.Instance != null
                && InventoryManager.Instance != null
                && (!_spawnPlayerRig || PlayerRegistry.Current != null)
            )
                return;

            var config = ResidentBootstrapConfig.Load();
            GameSession.EnsureResidents(config != null ? config.uiRootPrefab : null);
            EnsureEventSystem();

            // dev_state はプレイヤーを置く前に読む(開始位置を上書きできるように)。
            _devState = LoadDevState();

            // プレイヤーリグも Title と同じ経路で出し、配置も本番と同じ SpawnPoint 経由にする。
            // プレイヤーは Awake で MainCamera を探すので、カメラリグを先に出す。
            if (_spawnPlayerRig)
            {
                if (Camera.main == null)
                    GameSession.EnsureCameraRig(config != null ? config.cameraRigPrefab : null);
                var player = GameSession.EnsurePlayerRig(
                    config != null ? config.playerRigPrefab : null
                );
                if (player != null)
                {
                    SpawnPoint.Place(player, _spawnPointId);
                    // dev_state に開始位置があれば、シーンの SpawnPoint より優先する(個人用の開始位置)。
                    _devState?.ApplySpawn(player);
                    if (_previewOnlyPlayer)
                    {
                        player.AddComponent<PreviewOnlyPlayer>();
                        if (player.TryGetComponent(out PlayerController controller))
                            controller.enabled = false;
                        if (player.TryGetComponent(out PlayerInputHandler input))
                        {
                            input.cursorLocked = false;
                            input.enabled = false;
                        }
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                        if (player.TryGetComponent(out CharacterController collider))
                            collider.enabled = false;
                    }
                }
            }

            // 開発用: テスト品を積む(本番 Title フローでは呼ばれない = まっさら)。
            if (_seedTestItems)
                DevTestItems.Seed(InventoryManager.Instance);

            _devState?.Apply(ProgressManager.Instance, InventoryManager.Instance);
        }

        // UI のクリックを受ける EventSystem は Title にしか置いていないので、直接 Play では無ければ作る(UI 確認シーンは自前で持つ)。
        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject(nameof(EventSystem));
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            go.AddComponent<PersistentEventSystem>();
        }

        // 武器・HP はリグ側の部品が持つ。PlayerStatus.Start が HP を満タンに戻すので、1フレーム待ってから入れる。
        private IEnumerator Start()
        {
            if (_devState == null)
                yield break;
            yield return null;

            var player = GameObject.FindWithTag("Player");
            if (player != null)
                _devState.ApplyPlayer(
                    WeaponManager.Instance,
                    player.GetComponentInChildren<PlayerStatus>()
                );
        }

        // 開発用: dev_state.json を読む(イベント配置の確認用)。ファイルを読むのはエディタだけ。
        private static DevState LoadDevState()
        {
#if UNITY_EDITOR
            var path = DevState.ResolvePath();
            if (!System.IO.File.Exists(path))
                return null;

            DevState state;
            try
            {
                state = DevState.Parse(System.IO.File.ReadAllText(path));
            }
            catch (System.ArgumentException e)
            {
                Debug.LogWarning(
                    $"[FieldDevBootstrap] {path} を読めません(JSON の書き方を確認): {e.Message}"
                );
                return null;
            }
            if (state == null)
                return null;
            if (!state.HasHp)
            {
                Debug.LogError(
                    $"[FieldDevBootstrap] {path} に hp がありません(必須)。この状態は反映しません。"
                );
                return null;
            }
            Debug.Log(
                $"[FieldDevBootstrap] {path} の状態で開始(進行度 {state.progress}, HP {state.hp})"
            );
            return state;
#else
            return null;
#endif
        }
    }
}
