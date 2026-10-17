using SoundKit;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 足音・着地音の再生と、敵AIへの音イベント発行(SoundEventBus)を担当する。プレイヤーのルートに置く。
    ///
    /// 鳴らすタイミングは移動クリップの AnimationEvent(OnFootstep / OnLand)で決まる。
    /// イベントはモデル層の <see cref="PlayerAnimationEventRelay"/> から中継される。
    /// 本番アニメーションに差し替えたら、足が接地するフレームに OnFootstep、着地に OnLand を置く。
    ///
    /// 敵AIへの通知は音の割り当てと関係なく行う(音素材が無くても敵の聴覚の挙動が変わらないようにするため)。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerFootstep : MonoBehaviour
    {
        // ブレンドツリーでは再生中の複数クリップから同時にイベントが届く。主に再生されているクリップの分だけ鳴らす
        private const float MinClipWeight = 0.5f;

        // 移動速度がこれだけ歩き速度を上回っていたらダッシュ音にする(歩きの加減速の揺れを拾わないための余裕)
        private const float SprintSpeedMargin = 0.5f;

        [Header("音")]
        [Tooltip("歩行時の足音。音程の揺らぎは SoundCue 側で設定する")]
        [SerializeField]
        private SoundCue _walkSound;

        [Tooltip("ダッシュ時の足音")]
        [SerializeField]
        private SoundCue _runSound;

        [Tooltip("着地音")]
        [SerializeField]
        private SoundCue _landingSound;

        [Header("AIへの感知設定")]
        [Tooltip("歩き足音がAIに届く半径（メートル）。")]
        [SerializeField]
        private float _walkRadius = 6f;

        [Tooltip("走り足音がAIに届く半径（メートル）。")]
        [SerializeField]
        private float _runRadius = 12f;

        private PlayerController _playerController;
        private CharacterController _characterController;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            // PlayerController は [RequireComponent(typeof(CharacterController))]
            _characterController = GetComponent<CharacterController>();
        }

        /// <summary>移動クリップの OnFootstep イベント(PlayerAnimationEventRelay 経由)。</summary>
        public void OnFootstepEvent(AnimationEvent evt)
        {
            if (evt.animatorClipInfo.weight <= MinClipWeight)
                return;

            Vector3 velocity = _characterController.velocity;
            float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            PlayFootstep(horizontalSpeed > _playerController.MoveSpeed + SprintSpeedMargin);
        }

        /// <summary>着地クリップの OnLand イベント(PlayerAnimationEventRelay 経由)。</summary>
        public void OnLandEvent(AnimationEvent evt)
        {
            if (evt.animatorClipInfo.weight <= MinClipWeight)
                return;

            WorldSoundPlayer.Play(_landingSound, transform.position, this);
        }

        /// <summary>足音を1回再生し、SoundEventBusに発行する。</summary>
        private void PlayFootstep(bool isSprinting)
        {
            WorldSoundPlayer.Play(isSprinting ? _runSound : _walkSound, transform.position, this);

            // 音が鳴った瞬間のワールド座標を取得して敵AIへ通知
            SoundType soundType = isSprinting ? SoundType.Run : SoundType.Walk;
            float radius = isSprinting ? _runRadius : _walkRadius;

            SoundEventBus.Emit(new SoundEventData(soundType, transform.position, radius));
        }
    }
}
