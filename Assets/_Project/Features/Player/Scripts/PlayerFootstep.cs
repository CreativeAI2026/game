using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 足オブジェクトにアタッチする足音コンポーネント。
    /// AudioSourceは生成・破棄せず、このオブジェクトに固定でアタッチされたものを使う。
    /// AnimationEventには依存せず、PlayerControllerのGrounded状態と移動速度を参照して
    /// 自前で接地・移動を判定し、一定間隔で足音を再生する。
    /// 音の再生と同時に SoundEventBus へ音イベントを発行し、敵AIに感知させる。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PlayerFootstep : MonoBehaviour
    {
        [Header("接地判定の参照")]
        [Tooltip("PlayerControllerへの参照。Grounded状態と移動速度の取得に使う。")]
        [SerializeField]
        private PlayerController _playerController;

        [Header("足音クリップ")]
        [Tooltip("歩行時。ランダムに1つ選ばれる。")]
        [SerializeField]
        private AudioClip[] _walkFootStepClips;

        [Tooltip("ダッシュ時。ランダムに1つ選ばれる。")]
        [SerializeField]
        private AudioClip[] _dashFootStepClips;

        [Header("足音の再生間隔")]
        [Tooltip("歩き時の足音間隔（秒）。")]
        [SerializeField]
        private float _walkInterval = 0.5f;

        [Tooltip("ダッシュ時の足音間隔（秒）。")]
        [SerializeField]
        private float _runInterval = 0.3f;

        [Header("音量")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _volume = 0.5f;

        [Header("AIへの感知設定")]
        [Tooltip("歩き足音がAIに届く半径（メートル）。")]
        [SerializeField]
        private float _walkRadius = 6f;

        [Tooltip("走り足音がAIに届く半径（メートル）。")]
        [SerializeField]
        private float _runRadius = 12f;

        // 足音の再生間隔タイマー
        private float _footstepTimer;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (_playerController == null)
            {
                return;
            }

            // 接地していない、または移動入力がない場合は再生しない
            if (!_playerController.Grounded)
            {
                _footstepTimer = 0f;
                return;
            }

            // CharacterControllerの水平速度で移動中かどうかを判定
            // （PlayerControllerの_speedはprivateなため、velocityから取る）
            CharacterController cc = _playerController.GetComponent<CharacterController>();
            if (cc == null)
            {
                return;
            }

            float horizontalSpeed = new Vector3(cc.velocity.x, 0f, cc.velocity.z).magnitude;

            // ほぼ静止しているなら再生しない
            if (horizontalSpeed < 0.1f)
            {
                _footstepTimer = 0f;
                return;
            }

            bool isSprinting = horizontalSpeed > _playerController.MoveSpeed + 0.5f;
            float interval = isSprinting ? _runInterval : _walkInterval;

            _footstepTimer += Time.deltaTime;
            if (_footstepTimer >= interval)
            {
                _footstepTimer = 0f;
                PlayFootstep(isSprinting);
            }
        }

        /// <summary>
        /// 足音を1回再生し、SoundEventBusに発行する。
        /// AnimationEventからも呼び出せるが、通常はUpdate内のタイマーから呼ばれる。
        /// </summary>
        public void PlayFootstep(bool isSprinting)
        {
            AudioClip[] clips = isSprinting ? _dashFootStepClips : _walkFootStepClips;

            if (clips == null || clips.Length == 0)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            _audioSource.pitch = Random.Range(0.8f, 1.2f);
            _audioSource.PlayOneShot(clip, _volume);

            // 音が鳴った瞬間のワールド座標を取得して敵AIへ通知
            SoundType soundType = isSprinting ? SoundType.Run : SoundType.Walk;
            float radius = isSprinting ? _runRadius : _walkRadius;

            SoundEventBus.Emit(new SoundEventData(soundType, transform.position, radius));
        }
    }
}
