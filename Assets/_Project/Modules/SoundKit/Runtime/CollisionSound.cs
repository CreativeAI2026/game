using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// 物がぶつかったときの音（跳ねる・転がって当たる等）。Rigidbody を持つ物に付ける。
    /// ぶつかった速さで音量を変え、弱い接触（転がっているだけ等）では鳴らさない。
    /// </summary>
    [AddComponentMenu("SoundKit/Collision Sound")]
    public class CollisionSound : MonoBehaviour
    {
        /// <summary>弱くぶつかったときの音量の下限。小さすぎると鳴っていないのと同じになるため</summary>
        private const float MinVolume = 0.2f;

        [SerializeField]
        private SoundCue _impactSound;

        [Tooltip("この速さ（m/s）以上でぶつかったときだけ鳴らす。転がっているだけの接触で鳴り続けないようにする")]
        [SerializeField]
        [Min(0f)]
        private float _minImpactSpeed = 1f;

        [Tooltip("この速さ（m/s）でぶつかると最大の音量になる")]
        [SerializeField]
        [Min(0.1f)]
        private float _fullVolumeImpactSpeed = 6f;

        [Tooltip("続けて鳴らさない間隔（秒）。跳ねた直後の細かい接触で連打にならないようにする")]
        [SerializeField]
        [Min(0f)]
        private float _cooldown = 0.1f;

        private float _lastPlayedTime = float.NegativeInfinity;

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time - _lastPlayedTime < _cooldown)
                return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < _minImpactSpeed)
                return;

            _lastPlayedTime = Time.time;
            float volume = Mathf.Max(MinVolume, Mathf.InverseLerp(_minImpactSpeed, _fullVolumeImpactSpeed, speed));
            WorldSoundPlayer.Play(_impactSound, collision.GetContact(0).point, this, volume);
        }
    }
}
