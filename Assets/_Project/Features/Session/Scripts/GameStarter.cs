using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using UnityEngine;

[assembly: InternalsVisibleTo("CreativeAI.Tests.EditMode")]
[assembly: InternalsVisibleTo("CreativeAI.Tests.PlayMode")]

namespace CreativeAI.Session
{
    /// <summary>
    /// タイトルの「はじめる / 続きから」と、タイトルへ戻る処理の中身。常駐を作り、フィールドを読み込んでプレイヤーを置く。
    /// 使う Prefab は <see cref="ResidentBootstrapConfig"/> から読む(開発用の直接 Play と同じもの)。
    /// </summary>
    public sealed class GameStarter : IGameStarter, ITitleReturner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            var starter = new GameStarter();
            GameStartService.Current = starter;
            TitleReturnService.Current = starter;
        }

        /// <summary>常駐を新しく作ってフィールドへ(所持品はまっさら)。ロード後に開始位置へ置く。</summary>
        public bool StartNewGame(string sceneName)
        {
            if (!EnsureSession(out var config))
                return false;

            // はじめから: 前回のプレイの状態(進行度・フラグ・所持品・プレイヤー)を持ち越さない。
            ResetForNewGame();

            // はじめから: 前回のプレイで拾った記録を持ち越さず、アイテムを全部置き直す。
            PickedItemRegistry.Clear();

            SceneController.Instance.LoadScene(
                sceneName,
                onSceneActivated: () =>
                {
                    var player = SpawnRigs(config);
                    ShowUi();
                    SpawnPoint.Place(player, NewGameSpawnId());
                }
            );
            return true;
        }

        /// <summary>常駐を作り、セーブを戻して保存シーンへ。ロード後にプレイヤーを保存座標へ置く。</summary>
        public bool ContinueGame(string fallbackSceneName)
        {
            if (!EnsureSession(out var config))
                return false;

            // 進行度・フラグ・所持品はここで同期復元される(ItemCatalog は Resources 経由でシーン非依存)。
            var data = SaveService.Load();
            if (data == null)
                return false;

            // 保存したシーンが読めない(名前が変わった等)ときは開始シーンの開始位置から再開する。
            // 座標はそのシーンのものではないので使わない。
            var sceneLoadable = SceneController.CanLoad(data.sceneName);
            if (!sceneLoadable && !string.IsNullOrEmpty(data.sceneName))
                Debug.LogWarning(
                    $"[GameStarter] 保存シーン {data.sceneName} を読み込めないため {fallbackSceneName} から再開します。"
                );
            var scene = sceneLoadable ? data.sceneName : fallbackSceneName;

            // 座標・現在HP はシーンロード後でないと配置できないため、完了コールバックで復元する。
            SceneController.Instance.LoadScene(
                scene,
                onSceneActivated: () =>
                {
                    var player = SpawnRigs(config);
                    ShowUi();
                    if (!sceneLoadable)
                        SpawnPoint.Place(player, SpawnPoint.StartId);
                    SaveService.RestorePlayerState(data, restorePosition: sceneLoadable);
                }
            );
            return true;
        }

        /// <summary>プレイを打ち切ってタイトルへ戻る(戦闘に負けたとき)。</summary>
        public void ReturnToTitle()
        {
            if (SceneController.Instance == null)
            {
                Debug.LogError(
                    "[GameStarter] SceneController が無いためタイトルへ戻れません(Title から始めてください)。"
                );
                return;
            }
            SceneController.Instance.LoadScene(SceneNames.Title, onSceneActivated: LeaveSession);
        }

        // 倒れたプレイヤーは次の開始に持ち越せないので消す。カメラはタイトルのカメラと重なるので消し、
        // どちらも次の開始で SpawnRigs が作り直す。常駐 UI はタイトルの上に出ないよう隠す。
        private static void LeaveSession()
        {
            DestroyPlayer();
            var cameraRig = Object.FindAnyObjectByType<CameraRig>();
            if (cameraRig != null)
                Object.Destroy(cameraRig.gameObject);
            if (GameModeManager.Instance != null)
                GameModeManager.Instance.ExitBattle();
            if (UIRoot.Instance != null)
                UIRoot.Instance.SetVisible(false);
            // プレイ中はカーソルを固定しているので、タイトルのボタンを押せるように戻す。
            Cursor.lockState = CursorLockMode.None;
        }

        private static void DestroyPlayer()
        {
            var player = PlayerRegistry.Current;
            if (player != null)
                Object.Destroy(player.gameObject);
        }

        // 常駐物は同じプレイの間ずっと残るので、タイトルへ戻ってからの「はじめから」では前のプレイの状態が残っている。
        // 進行度・フラグ・所持品を空に戻し、プレイヤーは作り直す(武器・HP・死亡状態などをまとめて初期化するため)。
        // プレイヤーはロード後に SpawnRigs が新しく作る。カメラは PlayerRegistry 経由で新しいプレイヤーを追う。
        internal static void ResetForNewGame()
        {
            if (ProgressManager.Instance != null)
                ProgressManager.Instance.LoadState(0, new Dictionary<string, string>());
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.Clear();
            if (GameModeManager.Instance != null)
                GameModeManager.Instance.ExitBattle();

            DestroyPlayer();
        }

        // 常駐 UI はタイトルの上に出てしまうので、暗転が降りている間は隠し、読み込みが終わったらここで戻す。
        private static void ShowUi()
        {
            if (UIRoot.Instance != null)
                UIRoot.Instance.SetVisible(true);
        }

        // はじめからの開始位置。専用の目印(new_game)があればそこ、無いシーンでは start に置く。
        private static string NewGameSpawnId() =>
            SpawnPoint.Find(SpawnPoint.NewGameId) != null
                ? SpawnPoint.NewGameId
                : SpawnPoint.StartId;

        /// <summary>SceneController が居るか確かめ、セッション常駐を作る。居なければ何も作らず false。</summary>
        internal static bool EnsureSession(out ResidentBootstrapConfig config)
        {
            config = null;
            if (SceneController.Instance == null)
            {
                Debug.LogError(
                    "[GameStarter] SceneController.Instance is null. Title シーンに PersistentSystems(SceneController)がありません。"
                );
                return false;
            }

            config = ResidentBootstrapConfig.Load();
            GameSession.EnsureResidents(config != null ? config.uiRootPrefab : null);
            GameSession.MarkStartedFromTitle();
            if (UIRoot.Instance != null)
                UIRoot.Instance.SetVisible(false);
            return true;
        }

        // フィールドに入った時点でカメラリグ→プレイヤーリグの順に出し、プレイヤーを返す。
        // タイトルのカメラを掴まないようロード後に出す。プレイヤーは Awake で MainCamera を探すのでカメラが先。
        private static GameObject SpawnRigs(ResidentBootstrapConfig config)
        {
            GameSession.EnsureCameraRig(config != null ? config.cameraRigPrefab : null);
            return GameSession.EnsurePlayerRig(config != null ? config.playerRigPrefab : null);
        }
    }
}
