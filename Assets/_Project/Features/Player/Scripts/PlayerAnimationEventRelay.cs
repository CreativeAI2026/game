using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの AnimationEvent をロジック層へ中継する。
    ///
    /// AnimationEvent は Animator と同じ GameObject 上のメソッドしか呼べないため、
    /// モデル層のルート(Animator / <see cref="PlayerModel"/> と同じ GameObject)に置く。
    /// - 攻撃系: WeaponManager の選択中の武器ルートに付いた <see cref="IWeaponAnimationEventHandler"/> へ渡す
    /// - 足音系: ロジック層の <see cref="PlayerFootstep"/> へ渡す
    ///
    /// public メソッド名がそのままクリップ上のイベントの Function 名になる。改名するとクリップ側の設定が切れる。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimationEventRelay : MonoBehaviour
    {
        private WeaponManager _weaponManager;
        private PlayerFootstep _footstep;
        private PlayerFlinchHandler _flinchHandler;

        private void Awake()
        {
            _weaponManager = GetComponentInParent<WeaponManager>();
            _footstep = GetComponentInParent<PlayerFootstep>();
            _flinchHandler = GetComponentInParent<PlayerFlinchHandler>();
            Assert.IsNotNull(_weaponManager, $"{name}: 親階層に WeaponManager が見つかりません");
            Assert.IsNotNull(_footstep, $"{name}: 親階層に PlayerFootstep が見つかりません");
            Assert.IsNotNull(_flinchHandler, $"{name}: 親階層に PlayerFlinchHandler が見つかりません");
        }

        // --- 攻撃系。関数名は既存の Slash1〜3.anim に設定済みのもの(旧 AnimationEventReceiver)を引き継いでいる ---

        public void TriggerEnableHitbox(AnimationEvent evt) =>
            RelayToWeapon(WeaponAnimationEventType.HitboxOn, evt);

        public void TriggerDisableHitbox(AnimationEvent evt) =>
            RelayToWeapon(WeaponAnimationEventType.HitboxOff, evt);

        public void ComboWindowOpen(AnimationEvent evt) =>
            RelayToWeapon(WeaponAnimationEventType.ComboWindowOpen, evt);

        public void RecoveryStart(AnimationEvent evt) =>
            RelayToWeapon(WeaponAnimationEventType.RecoveryStart, evt);

        public void ThrowRelease(AnimationEvent evt) =>
            RelayToWeapon(WeaponAnimationEventType.ThrowRelease, evt);

        public void DrawComplete(AnimationEvent evt) =>
            RelayToWeapon(WeaponAnimationEventType.DrawComplete, evt);

        public void ReleaseEnd(AnimationEvent evt) =>
            RelayToWeapon(WeaponAnimationEventType.ReleaseEnd, evt);

        /// <summary>
        /// 行動の終わり。攻撃・パリィ・投擲は武器へ、怯みは PlayerFlinchHandler へ渡す
        /// (どちらも発火元のステートで自分宛てか判定するので、両方に渡してよい)。
        /// </summary>
        public void ActionEnd(AnimationEvent evt)
        {
            RelayToWeapon(WeaponAnimationEventType.ActionEnd, evt);
            _flinchHandler.OnActionEnd(evt.animatorStateInfo);
        }

        // --- 足音系。関数名は StarterAssets の移動クリップに設定済みのもの ---

        public void OnFootstep(AnimationEvent evt) => _footstep.OnFootstepEvent(evt);

        public void OnLand(AnimationEvent evt) => _footstep.OnLandEvent(evt);

        private void RelayToWeapon(WeaponAnimationEventType type, AnimationEvent evt)
        {
            GameObject weapon = _weaponManager.CurrentWeaponObject;
            if (weapon == null)
                return; // 武器0本(未入手)のときは受け先がない

            // イベントを使わない武器は実装していないので、見つからなければ何もしない
            if (weapon.TryGetComponent(out IWeaponAnimationEventHandler handler))
                handler.OnWeaponAnimationEvent(type, evt.animatorStateInfo);
        }
    }
}
