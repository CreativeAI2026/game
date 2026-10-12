using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 現在のプレイヤーを UI・カメラなど「シーン側」から参照するための窓口。
    ///
    /// プレイヤーは Prefab として実行時に生成されるため、Prefab からシーン上の UI やカメラを参照できない。
    /// そこで向きを逆にし、シーン側がここからプレイヤーを受け取る(プレイヤーは UI やカメラを知らない)。
    /// PlayerController が Awake で登録、OnDestroy で解除する。
    /// まだ生成されていない間は <see cref="Current"/> が null なので、利用側は <see cref="Changed"/> で待つ。
    /// </summary>
    public static class PlayerRegistry
    {
        /// <summary>現在のプレイヤー。未生成なら null。</summary>
        public static PlayerController Current { get; private set; }

        /// <summary>プレイヤーが登録・解除されたときに通知(解除時は null)。</summary>
        public static event Action<PlayerController> Changed;

        public static void Register(PlayerController player)
        {
            Current = player;
            Changed?.Invoke(player);
        }

        public static void Unregister(PlayerController player)
        {
            if (Current != player)
                return;
            Current = null;
            Changed?.Invoke(null);
        }

        // ドメインリロード無しで Play を繰り返したときに、前回のプレイヤーや購読が残らないようにする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
            Changed = null;
        }
    }
}
