using SoundKit;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの攻撃に合わせて鳴らす音をまとめて持ち、鳴らす。KurageBossController と同じ GameObject に置く。
    /// 地面の触手・輪っかの音は、本体ではなくその位置(出現位置・落下地点)から鳴らす。
    /// </summary>
    public class KurageBossSounds : MonoBehaviour
    {
        [Header("触手の突き")]
        [Tooltip("触手を一気に伸ばす音")]
        [SerializeField]
        private SoundCue _stabSound;

        [Header("地面からの触手")]
        [Tooltip("地面にポータルが開く音。出現位置を知らせる警告を兼ねる")]
        [SerializeField]
        private SoundCue _portalOpenSound;

        [Tooltip("触手が地面から突き出る音")]
        [SerializeField]
        private SoundCue _tentacleEmergeSound;

        [Tooltip("触手が薙ぎ払う音")]
        [SerializeField]
        private SoundCue _tentacleSweepSound;

        [Header("輪っか")]
        [Tooltip("輪っかを投げ上げる音")]
        [SerializeField]
        private SoundCue _ringThrowSound;

        [Tooltip("落下地点の警告が出た時の音")]
        [SerializeField]
        private SoundCue _ringWarningSound;

        [Tooltip("輪っかがプレイヤーの胸の高さまで降りてきた音")]
        [SerializeField]
        private SoundCue _ringLandSound;

        [Tooltip("輪っかがプレイヤーを締め付けた瞬間の音")]
        [SerializeField]
        private SoundCue _ringSqueezeSound;

        [Tooltip("レバガチャで輪っかを振りほどいた音")]
        [SerializeField]
        private SoundCue _ringBreakSound;

        public void PlayStab() => PlayAt(_stabSound, transform.position);

        public void PlayPortalOpen(Vector3 position) => PlayAt(_portalOpenSound, position);

        public void PlayTentacleEmerge(Vector3 position) => PlayAt(_tentacleEmergeSound, position);

        public void PlayTentacleSweep(Vector3 position) => PlayAt(_tentacleSweepSound, position);

        public void PlayRingThrow(Vector3 position) => PlayAt(_ringThrowSound, position);

        public void PlayRingWarning(Vector3 position) => PlayAt(_ringWarningSound, position);

        public void PlayRingLand(Vector3 position) => PlayAt(_ringLandSound, position);

        public void PlayRingSqueeze(Vector3 position) => PlayAt(_ringSqueezeSound, position);

        public void PlayRingBreak(Vector3 position) => PlayAt(_ringBreakSound, position);

        private void PlayAt(SoundCue cue, Vector3 position) =>
            WorldSoundPlayer.Play(cue, position, this);
    }
}
