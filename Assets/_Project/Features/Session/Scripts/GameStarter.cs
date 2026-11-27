using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CreativeAI.Gameplay;
using UnityEngine;

[assembly: InternalsVisibleTo("CreativeAI.Tests.EditMode")]
[assembly: InternalsVisibleTo("CreativeAI.Tests.PlayMode")]

namespace CreativeAI.Session
{
    /// <summary>
    /// タイトルの「はじめる / 続きから」と、タイトルへ戻る処理の中身。常駐を作り、フィールドを読み込んでプレイヤーを置く。
    /// 使う Prefab は <see cref="ResidentBootstrapConfig"/> から読む(開発用の直接 Play と同じもの)。
    /// </summary>
    public static class GameStarter
    {
        /// <summary>常駐を新しく作ってフィールドへ(所持品はまっさら)。ロード後に開始位置へ置く。</summary>
        public static bool StartNewGame(string sceneName)
        {
            if (!EnsureSession(out var config))
                return false;

            // はじめから: 前回のプレイの状態(進行度・フラグ・所持品・拾った記録・プレイヤー)を持ち越さない。
            ResetForNewGame();

            SceneLoader.Instance.LoadScene(
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

        /// <summary>常駐を作り、セーブを戻してフィールドへ。ロード後にプレイヤーを保存座標へ置く。</summary>
        public static bool ContinueGame(string sceneName)
        {
            if (!EnsureSession(out var config))
                return false;

            // 進行度・フラグ・所持品はここで同期復元される(ItemCatalog は Resources 経由でシーン非依存)。
            var data = LoadService.Load();
            if (data == null)
                return false;

            // 座標・現在HP はシーンロード後でないと配置できないため、完了コールバックで復元する。
            SceneLoader.Instance.LoadScene(
                sceneName,
                onSceneActivated: () =>
                {
                    var player = SpawnRigs(config);
                    ShowUi();
                    // プレイヤー状態を持たない古いセーブは、開始位置から再開する。
                    if (!data.hasPlayerState)
                        SpawnPoint.Place(player, SpawnPoint.StartId);
                    LoadService.RestorePlayerState(data);
                }
            );
            return true;
        }

        /// <summary>プレイを打ち切ってタイトルへ戻る(プレイヤーが倒れたとき)。</summary>
        public static void ReturnToTitle()
        {
            if (SceneLoader.Instance == null)
            {
                Debug.LogError(
                    "[GameStarter] SceneLoader が無いためタイトルへ戻れません(Title から始めてください)。"
                );
                return;
            }
            SceneLoader.Instance.LoadScene(SceneNames.Title, onSceneActivated: LeaveSession);
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
                GameModeManager.Instance.ResetToField();
            if (UIManager.Instance != null)
                UIManager.Instance.SetVisible(false);
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
        // 進行度・フラグ・所持品・武器・拾った記録を空に戻し、プレイヤーは作り直す(HP・死亡状態などをまとめて初期化するため)。
        // プレイヤーはロード後に SpawnRigs が新しく作る。カメラは PlayerRegistry 経由で新しいプレイヤーを追う。
        internal static void ResetForNewGame()
        {
            if (ProgressManager.Instance != null)
                ProgressManager.Instance.LoadState(0, new Dictionary<string, string>());
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.Clear();
            if (FieldItemManager.Instance != null)
                FieldItemManager.Instance.Restore(null);
            if (WeaponManager.Instance != null)
                WeaponManager.Instance.Clear();
            if (GameModeManager.Instance != null)
                GameModeManager.Instance.ResetToField();

            DestroyPlayer();
        }

        // 常駐 UI はタイトルの上に出てしまうので、暗転が降りている間は隠し、読み込みが終わったらここで戻す。
        private static void ShowUi()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.SetVisible(true);
        }

        // はじめからの開始位置。専用の目印(new_game)があればそこ、無いシーンでは start に置く。
        private static string NewGameSpawnId() =>
            SpawnPoint.Find(SpawnPoint.NewGameId) != null
                ? SpawnPoint.NewGameId
                : SpawnPoint.StartId;

        /// <summary>SceneLoader が居るか確かめ、セッション常駐を作る。居なければ何も作らず false。</summary>
        internal static bool EnsureSession(out ResidentBootstrapConfig config)
        {
            config = null;
            if (SceneLoader.Instance == null)
            {
                Debug.LogError(
                    "[GameStarter] SceneLoader.Instance is null. Title シーンに PersistentSystems(SceneLoader)がありません。"
                );
                return false;
            }

            config = ResidentBootstrapConfig.Load();
            GameSession.EnsureResidents(config != null ? config.uiRootPrefab : null);
            GameSession.MarkStartedFromTitle();
            if (UIManager.Instance != null)
                UIManager.Instance.SetVisible(false);
            return true;
        }

        // フィールドに入った時点でカメラリグ→プレイヤーリグの順に出し、プレイヤーを返す。
        // タイトルのカメラを掴まないようロード後に出す。プレイヤーは Awake で MainCamera を探すのでカメラが先。
        // タイトルから始めたプレイでだけ、倒れたらタイトルへ戻す部品をここで付ける(Prefab には付けない)。
        private static GameObject SpawnRigs(ResidentBootstrapConfig config)
        {
            GameSession.EnsureCameraRig(config != null ? config.cameraRigPrefab : null);
            var player = GameSession.EnsurePlayerRig(
                config != null ? config.playerRigPrefab : null
            );
            if (player != null && !player.TryGetComponent(out ReturnToTitleOnDeath _))
                player.AddComponent<ReturnToTitleOnDeath>();
            return player;
        }
    }
}
