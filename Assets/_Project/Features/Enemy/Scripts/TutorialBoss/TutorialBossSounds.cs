using SoundKit;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// チュートリアルボスの行動に合わせて鳴らす音(足音・鎌の連撃・触手のワイヤー・拘束)をまとめて持ち、鳴らす。
    /// TutorialBossController と同じ GameObject(Animator のある GameObject)に置く。
    ///
    /// 足音は移動クリップ(歩き・走り・横歩き・後ろ歩き)の AnimationEvent「OnFootstep」で鳴らす。
    /// 他の音はステートから呼ばれる。発見音と BGM の切り替えは <see cref="TBossAlertAudio"/> が持つ。
    /// </summary>
    public class TutorialBossSounds : MonoBehaviour
    {
        // ブレンドツリーでは再生中の複数クリップから同時にイベントが届く。主に再生されているクリップの分だけ鳴らす
        // (PlayerFootstep と同じ考え方)
        private const float MinFootstepClipWeight = 0.5f;

        [Header("移動")]
        [Tooltip("足音。移動クリップの AnimationEvent「OnFootstep」で鳴らす")]
        [SerializeField]
        private SoundCue _footstepSound;

        [Header("通常攻撃1（近接攻撃）")]
        [Tooltip("鎌を振る音（AnimationEventで呼ばれる)")]
        [SerializeField]
        private SoundCue _attack1SwingSound;

        [Header("通常攻撃2(鎌の連撃)")]
        [Tooltip("鎌のリーチを伸ばす音(OnAttack2ReachExtend)")]
        [SerializeField]
        private SoundCue _attack2ReachExtendSound;

        [Tooltip("鎌のリーチを戻す音(OnAttack2ReachRetract)")]
        [SerializeField]
        private SoundCue _attack2ReachRetractSound;

        [Tooltip("1振り目の音(AnimationEventで呼ばれる)")]
        [SerializeField]
        private SoundCue _attack2Swing1Sound;

        [Tooltip("2振り目の音(AnimationEventで呼ばれる)")]
        [SerializeField]
        private SoundCue _attack2Swing2Sound;

        [Header("特殊攻撃(触手のワイヤー)")]
        [Tooltip("腕を伸ばす音。特殊攻撃の構え始め(Aim)で鳴らす")]
        [SerializeField]
        private SoundCue _wireArmExtendSound;

        [Tooltip("ワイヤーを伸ばす音。射出(Shoot)の瞬間に鳴らす")]
        [SerializeField]
        private SoundCue _wireShootSound;

        [Header("拘束")]
        [Tooltip("プレイヤーを掴んだ瞬間の音")]
        [SerializeField]
        private SoundCue _grabSound;

        [Tooltip(
            "電撃の間鳴り続ける音。ループする素材を使う。電撃エフェクトの位置(プレイヤーの首元)から鳴らす"
        )]
        [SerializeField]
        private SoundCue _electricLoopSound;

        [Tooltip("拘束から脱出した瞬間の音")]
        [SerializeField]
        private SoundCue _escapeSound;

        [Header("焦りへの追い込み")]
        [Tooltip(
            "追い込み開始の合図(目がギラッと光る瞬間)の音。背を向けて逃げているプレイヤーには目の光が見えず、"
                + "この音だけが合図になるため、遠くまで届く設定にする"
        )]
        [SerializeField]
        private SoundCue _pressureCueSound;

        [Header("死亡")]
        [Tooltip("倒れたときのSE。AnimationEventで呼ぶ")]
        [SerializeField]
        private SoundCue _fallDownSound;

        /// <summary>電撃の鳴り続ける音。鳴らす位置(電撃エフェクト)は拘束ステートが用意する。</summary>
        public SoundCue ElectricLoopSound => _electricLoopSound;

        /// <summary>移動クリップの AnimationEvent「OnFootstep」。</summary>
        public void OnFootstep(AnimationEvent evt)
        {
            if (evt.animatorClipInfo.weight <= MinFootstepClipWeight)
                return;

            PlayAtSelf(_footstepSound);
        }

        public void PlayAttack1Swing() => PlayAtSelf(_attack1SwingSound);

        public void PlayAttack2ReachExtend() => PlayAtSelf(_attack2ReachExtendSound);

        public void PlayAttack2ReachRetract() => PlayAtSelf(_attack2ReachRetractSound);

        public void PlayAttack2Swing1() => PlayAtSelf(_attack2Swing1Sound);

        public void PlayAttack2Swing2() => PlayAtSelf(_attack2Swing2Sound);

        public void PlayWireArmExtend() => PlayAtSelf(_wireArmExtendSound);

        public void PlayWireShoot() => PlayAtSelf(_wireShootSound);

        public void PlayGrab() => PlayAtSelf(_grabSound);

        public void PlayEscape() => PlayAtSelf(_escapeSound);

        /// <summary>追い込み開始の合図の音を、目の位置から鳴らす。</summary>
        public void PlayPressureCue(Vector3 position) =>
            WorldSoundPlayer.Play(_pressureCueSound, position, this);

        public void PlayFallDownSound() => WorldSoundPlayer.Play(_fallDownSound, transform.position, this);

        private void PlayAtSelf(SoundCue cue)
        {
            WorldSoundPlayer.Play(cue, transform.position, this);
        }
    }
}
