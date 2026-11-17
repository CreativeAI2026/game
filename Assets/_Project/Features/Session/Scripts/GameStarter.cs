using System.Runtime.CompilerServices;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using UnityEngine;

[assembly: InternalsVisibleTo("CreativeAI.Tests.EditMode")]
[assembly: InternalsVisibleTo("CreativeAI.Tests.PlayMode")]

namespace CreativeAI.Session
{
    /// <summary>
    /// タイトルの「はじめる / 続きから」の中身。常駐を作り、フィールドを読み込んでプレイヤーを置く。
    /// 使う Prefab は <see cref="ResidentBootstrapConfig"/> から読む(開発用の直接 Play と同じもの)。
    /// </summary>
    public sealed class GameStarter : IGameStarter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => GameStartService.Current = new GameStarter();

        /// <summary>常駐を新しく作ってフィールドへ(所持品はまっさら)。ロード後に開始位置へ置く。</summary>
        public bool StartNewGame(string sceneName)
        {
            if (!EnsureSession(out var config))
                return false;

            // はじめから: 前回のプレイで拾った記録を持ち越さず、アイテムを全部置き直す。
            PickedItemRegistry.Clear();

            SceneController.Instance.LoadScene(
                sceneName,
                onSceneActivated: () => SpawnPoint.Place(SpawnRigs(config), SpawnPoint.StartId)
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
                    SaveService.RestorePlayerState(data);
                    if (!sceneLoadable)
                        SpawnPoint.Place(player, SpawnPoint.StartId);
                }
            );
            return true;
        }

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
