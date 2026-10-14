using CreativeAI.Core;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 1回のプレイ(「はじめる/続きから」〜タイトルに戻るまで)の間ずっと残る常駐一式を作る(冪等)。
    /// 本番のタイトル(TitleUIController)と、開発用の直接 Play(FieldDevBootstrap)が同じ手順を通る。
    /// Core / Gameplay / UI の全部を参照できるのは UI 層なので、ここに置く。
    /// </summary>
    public static class GameSession
    {
        /// <summary>
        /// 常駐を生成順どおりに作る。既に在るものはそのまま使う。
        /// 順番: マネージャ → 所持品 → UI → 戦闘
        /// (UI は GameModeManager / InventoryManager を購読し、会話UIは生成時に DialogueViewService へ自己登録するため)。
        /// カメラリグ・プレイヤーリグはタイトルのカメラと重ならないようフィールドに入ってから
        /// <see cref="EnsureCameraRig"/> → <see cref="EnsurePlayerRig"/> の順で別に呼ぶ。
        /// </summary>
        public static void EnsureResidents(GameObject uiRootPrefab)
        {
            if (ProgressManager.Instance == null)
                new GameObject(nameof(ProgressManager)).AddComponent<ProgressManager>();
            if (GameModeManager.Instance == null)
                new GameObject(nameof(GameModeManager)).AddComponent<GameModeManager>();
            // 会話イベントの指揮役。常駐させるので EventTrigger ごとの配線は要らない。
            EventPlayer.EnsureResident();

            InventoryManager.EnsureResident();
            UIRoot.EnsureResident(uiRootPrefab);
            BattleRunnerService.Current ??= new BattleRunner();
        }

        /// <summary>
        /// プレイヤーリグを1体だけ生成して常駐させ、それを返す。Player タグが既に居ればそれを返す。
        /// Prefab 未割当なら警告して null(フィールドは読み込めるがプレイヤーは出ない)。
        /// 常駐とカメラリグの後に呼ぶ(プレイヤーは GameModeManager を購読し、Start で Inventory を読み、
        /// Awake で MainCamera を探すため)。
        /// </summary>
        public static GameObject EnsurePlayerRig(GameObject prefab, string playerTag = "Player")
        {
            var existing = GameObject.FindWithTag(playerTag);
            if (existing != null)
                return existing;

            if (prefab == null)
            {
                Debug.LogWarning(
                    "[GameSession] playerRigPrefab が未割当です。PlayerRig Prefab を Inspector にドラッグしてください。"
                );
                return null;
            }

            var player = Object.Instantiate(prefab);
            player.name = prefab.name; // "(Clone)" を避ける
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(player); // EditMode では呼べない
            return player;
        }

        /// <summary>
        /// カメラリグを1つだけ生成して常駐させ、それを返す。既に在ればそれを返す。
        /// プレイヤーとは独立しているので、プレイヤーが居なくても出せる(プレイヤーは PlayerRegistry 経由で後から追う)。
        /// Prefab 未割当なら警告して null(画面にカメラが無くなる)。
        /// </summary>
        public static CameraRig EnsureCameraRig(GameObject prefab)
        {
            var existing = Object.FindAnyObjectByType<CameraRig>();
            if (existing != null)
                return existing;

            if (prefab == null)
            {
                Debug.LogWarning(
                    "[GameSession] cameraRigPrefab が未割当です。CameraRig Prefab を Inspector にドラッグしてください。"
                );
                return null;
            }

            var rig = Object.Instantiate(prefab);
            rig.name = prefab.name;
            if (Application.isPlaying)
                Object.DontDestroyOnLoad(rig);
            return rig.GetComponent<CameraRig>();
        }
    }
}
