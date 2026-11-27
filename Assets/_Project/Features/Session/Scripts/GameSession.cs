using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.Session
{
    /// <summary>
    /// 1回のプレイ(「はじめる/続きから」〜タイトルに戻るまで)の間ずっと残る常駐一式を作る(冪等)。
    /// タイトルからの開始(<see cref="GameStarter"/>)と、開発用の直接 Play(DevTools の FieldDevBootstrap)が同じ手順を通る。
    /// </summary>
    public static class GameSession
    {
        /// <summary>
        /// タイトルの「はじめから / 続きから」で始めたセッションか。開発用の直接 Play の初期化(FieldDevBootstrap)が、
        /// タイトル経由のセッションでセーブの復元を上書きしないよう、これを見て何もしない。
        /// </summary>
        public static bool StartedFromTitle { get; private set; }

        // エディタで再生を繰り返しても、前回の値を持ち越さない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStartedFromTitle() => StartedFromTitle = false;

        /// <summary>タイトルからの開始であることを記録する(<see cref="GameStarter"/> が呼ぶ)。</summary>
        public static void MarkStartedFromTitle() => StartedFromTitle = true;

        /// <summary>
        /// 常駐を生成順どおりに作る。既に在るものはそのまま使う。
        /// 順番: マネージャ → 所持品 → UI
        /// (UI は GameModeManager / InventoryManager を購読し、会話UIは UIManager が子から見つけるため)。
        /// カメラリグ・プレイヤーリグはタイトルのカメラと重ならないようフィールドに入ってから
        /// <see cref="EnsureCameraRig"/> → <see cref="EnsurePlayerRig"/> の順で別に呼ぶ。
        /// </summary>
        public static void EnsureResidents(GameObject uiRootPrefab)
        {
            ProgressManager.EnsureResident();
            GameModeManager.EnsureResident();
            FieldItemManager.EnsureResident();
            InventoryManager.EnsureResident();
            WeaponManager.EnsureResident();
            UIManager.EnsureResident(uiRootPrefab);
        }

        /// <summary>
        /// プレイヤーリグを1体だけ生成して常駐させ、それを返す。Player タグが既に居ればそれを返す。
        /// Prefab 未割当なら警告して null(フィールドは読み込めるがプレイヤーは出ない)。
        /// 常駐とカメラリグの後に呼ぶ(プレイヤーは GameModeManager を購読し、Start で Inventory を読み、
        /// Awake で MainCamera を探すため)。
        /// </summary>
        public static GameObject EnsurePlayerRig(GameObject prefab)
        {
            var existing = GameObject.FindWithTag("Player");
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
