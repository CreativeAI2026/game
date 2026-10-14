using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// <see cref="PlayerAnimationEventRelay"/> から AnimationEvent を受け取る武器側の窓口。
    /// WeaponManager に登録された武器のルートオブジェクトに付くコンポーネントが実装する。
    /// </summary>
    public interface IWeaponAnimationEventHandler
    {
        /// <param name="type">合図の種類。</param>
        /// <param name="sourceState">
        /// イベントを発火したクリップが再生されていたステート。
        /// 遷移のブレンド中は前後2つのクリップ両方からイベントが届くため、受け側はこれで自分宛てか判定する。
        /// </param>
        void OnWeaponAnimationEvent(WeaponAnimationEventType type, AnimatorStateInfo sourceState);
    }
}
