using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// 操作で開くUI(キャラ / インベ / セーブ / 調合)のどれかが開いているかを伝える静的サービス。
    /// プレイヤー(Gameplay)は UI を参照できないので、UI 側が書き込み、プレイヤー側は購読して操作を止める・カーソルを出す。
    /// </summary>
    public static class UiPanelService
    {
        public static bool IsOpen { get; private set; }
        public static event System.Action<bool> OpenChanged;

        public static void SetOpen(bool open)
        {
            if (IsOpen == open)
                return;
            IsOpen = open;
            OpenChanged?.Invoke(open);
        }

        // ドメインリロード無しで Play を繰り返したときに、開いたままのフラグや購読が残らないようにする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetState()
        {
            IsOpen = false;
            OpenChanged = null;
        }
    }
}
