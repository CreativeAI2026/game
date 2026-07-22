using System;
using UnityEngine;

namespace CreativeAI.Core
{
    public enum GameMode
    {
        Field,
        Battle,
    }

    /// <summary>
    /// 現在のゲームモード(Field / Battle)を1つだけ保持する常駐 SSOT。状態を持つだけで、
    /// 各システムはこれを読む/購読して自分で自制する。書き込み(EnterBattle/ExitBattle)を
    /// 叩くのは進行側(EventPlayer)のみ。documents/Specification.md §6 参照。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameModeManager : MonoBehaviour
    {
        public static GameModeManager Instance { get; private set; }

        /// <summary>各システムが自制のため読む。既定は Field。</summary>
        public GameMode CurrentMode { get; private set; } = GameMode.Field;

        /// <summary>モードが変わったら通知。UI(HUD差替) / EventTrigger(発火抑止) 等が購読。</summary>
        public event Action<GameMode> OnModeChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>進行側(会話の battle ステップ到達)のみ叩く。</summary>
        public void EnterBattle() => SetMode(GameMode.Battle);

        /// <summary>進行側(決着)のみ叩く。</summary>
        public void ExitBattle() => SetMode(GameMode.Field);

        private void SetMode(GameMode mode)
        {
            if (mode == CurrentMode)
                return;
            CurrentMode = mode;
            OnModeChanged?.Invoke(mode);
        }
    }
}
