using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスのわっかに締め付けられている間だけ映る演出カメラ。
    /// 締め付けているわっかが見える高さから、回り込みながら寄る。
    /// 回り込みはボスの反対側へ向かうので、進むほど奥にボスが入ってくる。
    /// もがく(レバガチャ)と画面が傾き、締め付けのダメージで画面が揺れて少し画角が狭まる。
    ///
    /// 掴まれている間は <see cref="PlayerCameraController"/> が視点入力の反映を止めるため、
    /// このカメラが無いと画面が完全に静止してしまう。拘束中の画面の動きはすべてここで作る。
    /// 合図は <see cref="KurageRestraintCameraEvents"/> から受け取るだけで、ボスもプレイヤーも直接参照しない。
    /// 追従ターゲット(プレイヤーの上半身)は <see cref="CameraRig"/> が割り当てる。
    ///
    /// セットアップ: CinemachineCamera に付け、CinemachineFollow の Binding Mode は World Space にする。
    /// 画面の揺れを受けるには CinemachineImpulseListener も付ける(ChannelMask は他のカメラと揃える)。
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    [RequireComponent(typeof(CinemachineFollow))]
    public sealed class KurageRestraintCamera : MonoBehaviour
    {
        private enum Mode
        {
            Off,
            Restraining,
            Releasing,
        }

        [Header("切り替え")]
        [Tooltip("拘束中に使う Priority。通常カメラより大きくする")]
        [SerializeField]
        private int _activePriority = 30;

        [Header("構図")]
        [Tooltip("ボスがプレイヤーの奥に写る向きから、横へどれだけずらして見始めるか(度)")]
        [SerializeField]
        private float _startYawOffset = 35f;

        [Tooltip(
            "回り込みの速さ(度/秒)。負で開始角を戻す向き(ボスが画面の中央へ寄ってくる向き)に回る"
        )]
        [SerializeField]
        private float _orbitSpeedStart = -5f;

        [Tooltip("回り込みの最大の速さ(度/秒)。締め付けが進むほどこの速さへ近づく")]
        [SerializeField]
        private float _orbitSpeedMax = -13f;

        [Tooltip("回り込みが最大の速さになるまでの時間(秒)")]
        [SerializeField]
        private float _orbitRampDuration = 3f;

        [Header("寄り")]
        [Tooltip("拘束開始時のターゲットからの距離(m)")]
        [SerializeField]
        private float _startDistance = 7f;

        [Tooltip("寄り切った時のターゲットからの距離(m)")]
        [SerializeField]
        private float _endDistance = 5.4f;

        [Tooltip("拘束開始時のターゲットからの高さ(m)。わっかを見下ろす角度になる")]
        [SerializeField]
        private float _startHeight = 1.6f;

        [Tooltip("寄り切った時のターゲットからの高さ(m)")]
        [SerializeField]
        private float _endHeight = 1.2f;

        [Tooltip("寄り切るまでの時間(秒)")]
        [SerializeField]
        private float _pushInDuration = 2.5f;

        [Header("もがき(レバガチャ)")]
        [Tooltip("1回もがいた時に画面の傾きへ与える勢い(度/秒)")]
        [SerializeField]
        private float _struggleRollImpulse = 16f;

        [Tooltip("傾きが戻る強さ。大きいほど速く戻る")]
        [SerializeField]
        private float _rollSpring = 90f;

        [Tooltip("傾きの揺れ止まりの強さ。小さいほど何度も揺り返す")]
        [SerializeField]
        private float _rollDamping = 12f;

        [Tooltip("画面の傾きの上限(度)")]
        [SerializeField]
        private float _maxRoll = 7f;

        [Header("締め付け")]
        [Tooltip("締め付けのダメージで起こす画面の揺れの大きさ(小=0.2 / 中=0.5 / 大=1.0)")]
        [SerializeField]
        private float _squeezeShake = 0.3f;

        [Tooltip("締め付けのダメージで画角を狭める量(度)")]
        [SerializeField]
        private float _squeezeFovNarrow = 4f;

        [Tooltip("狭まった画角が戻る速さ(度/秒)")]
        [SerializeField]
        private float _fovRecoverySpeed = 10f;

        [Header("脱出")]
        [Tooltip("脱出した瞬間に引く距離(m)")]
        [SerializeField]
        private float _escapeDistance = 5.5f;

        [Tooltip("脱出後、通常カメラへ戻し始めるまでの時間(秒)")]
        [SerializeField]
        private float _releaseDuration = 0.35f;

        private CinemachineCamera _camera;
        private CinemachineFollow _follow;
        private Mode _mode = Mode.Off;
        private int _idlePriority;
        private float _baseFov;
        private float _baseDutch;
        private float _elapsed;
        private float _releaseTimer;
        private float _yaw;
        private float _distance;
        private float _height;
        private float _distanceAtRelease;
        private float _roll;
        private float _rollVelocity;
        private float _fovNarrow;

        private void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();
            _follow = GetComponent<CinemachineFollow>();
            _idlePriority = _camera.Priority.Value;
            _baseFov = _camera.Lens.FieldOfView;
            _baseDutch = _camera.Lens.Dutch;

            // 回り込みは世界座標の向きで計算するので、ターゲットのローカル軸に縛られると構図が崩れる
            Assert.AreEqual(
                BindingMode.WorldSpace,
                _follow.TrackerSettings.BindingMode,
                $"{name}: CinemachineFollow の Binding Mode を World Space にしてください"
            );
        }

        private void OnEnable()
        {
            KurageRestraintCameraEvents.OnBegin += Begin;
            KurageRestraintCameraEvents.OnStruggle += Struggle;
            KurageRestraintCameraEvents.OnSqueeze += Squeeze;
            KurageRestraintCameraEvents.OnEnd += End;
        }

        private void OnDisable()
        {
            KurageRestraintCameraEvents.OnBegin -= Begin;
            KurageRestraintCameraEvents.OnStruggle -= Struggle;
            KurageRestraintCameraEvents.OnSqueeze -= Squeeze;
            KurageRestraintCameraEvents.OnEnd -= End;
            Stop();
        }

        private void Begin(Transform boss)
        {
            Transform target = _camera.Target.TrackingTarget;
            if (target == null)
            {
                Debug.LogError(
                    $"{name}: Tracking Target が未設定です。CameraRig の上半身追従カメラに登録してください",
                    this
                );
                return;
            }

            Vector3 toBoss = boss.position - target.position;
            toBoss.y = 0f;
            float bossYaw =
                toBoss.sqrMagnitude > 0.0001f
                    ? Mathf.Atan2(toBoss.x, toBoss.z) * Mathf.Rad2Deg
                    : target.eulerAngles.y;

            // ボスの反対側に立つ = ボスがプレイヤーの奥に写る。そこから横へずらした角度で見始める
            _yaw = bossYaw + 180f + _startYawOffset;
            _elapsed = 0f;
            _releaseTimer = 0f;
            _roll = 0f;
            _rollVelocity = 0f;
            _fovNarrow = 0f;
            _mode = Mode.Restraining;
            _camera.Priority = _activePriority;

            // 通常カメラからのブレンドを正しい位置から始めるため、この場で構図を作っておく
            UpdateFraming();
            Apply();
        }

        private void Struggle(Vector2 move)
        {
            if (_mode != Mode.Restraining)
                return;

            // もがいた向きへ画面を傾ける。上下だけ入れた時も反応させたいので、大きい方の軸を使う
            float input = Mathf.Abs(move.x) >= Mathf.Abs(move.y) ? move.x : move.y;
            _rollVelocity -= Mathf.Sign(input) * _struggleRollImpulse;
        }

        private void Squeeze()
        {
            if (_mode != Mode.Restraining)
                return;

            _fovNarrow = _squeezeFovNarrow;

            // 揺れの発生元はシーンに1つだけ常駐する。無いシーンでも拘束は動かしたいので揺れだけ諦める
            if (CameraShakeManager.Instance != null)
                CameraShakeManager.Instance.Shake(_squeezeShake);
        }

        private void End(bool escaped)
        {
            if (_mode != Mode.Restraining)
                return;

            // 脱出は押し返した感を出すため一度引いてから通常カメラへ返す。
            // それ以外(死亡・撃破)は死亡演出などが続くので、すぐカメラを明け渡す
            if (!escaped)
            {
                Stop();
                return;
            }

            _mode = Mode.Releasing;
            _releaseTimer = 0f;
            _distanceAtRelease = _distance;
        }

        private void Update()
        {
            if (_mode == Mode.Off)
                return;

            float deltaTime = Time.deltaTime;
            _elapsed += deltaTime;

            float ramp =
                _orbitRampDuration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _orbitRampDuration);
            _yaw += Mathf.Lerp(_orbitSpeedStart, _orbitSpeedMax, ramp) * deltaTime;

            if (_mode == Mode.Releasing)
            {
                _releaseTimer += deltaTime;
                if (_releaseTimer >= _releaseDuration)
                {
                    Stop();
                    return;
                }
                _distance = Mathf.Lerp(
                    _distanceAtRelease,
                    _escapeDistance,
                    _releaseDuration <= 0f ? 1f : _releaseTimer / _releaseDuration
                );
            }
            else
            {
                UpdateFraming();
            }

            UpdateRoll(deltaTime);
            _fovNarrow = Mathf.MoveTowards(_fovNarrow, 0f, _fovRecoverySpeed * deltaTime);
            Apply();
        }

        /// <summary>締め付けが進むほど寄る。</summary>
        private void UpdateFraming()
        {
            float push = _pushInDuration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _pushInDuration);
            push = push * push * (3f - 2f * push);
            _distance = Mathf.Lerp(_startDistance, _endDistance, push);
            _height = Mathf.Lerp(_startHeight, _endHeight, push);
        }

        /// <summary>もがきで与えた勢いをバネで戻す。</summary>
        private void UpdateRoll(float deltaTime)
        {
            _rollVelocity += (-_rollSpring * _roll - _rollDamping * _rollVelocity) * deltaTime;
            _roll = Mathf.Clamp(_roll + _rollVelocity * deltaTime, -_maxRoll, _maxRoll);
        }

        private void Apply()
        {
            Vector3 direction = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            _follow.FollowOffset = direction * _distance + Vector3.up * _height;
            _camera.Lens.Dutch = _baseDutch + _roll;
            _camera.Lens.FieldOfView = _baseFov - _fovNarrow;
        }

        private void Stop()
        {
            if (_mode == Mode.Off)
                return;

            _mode = Mode.Off;
            _camera.Priority = _idlePriority;
            _camera.Lens.Dutch = _baseDutch;
            _camera.Lens.FieldOfView = _baseFov;
        }
    }
}
