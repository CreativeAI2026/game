using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスの地面攻撃で、地面のポータルから突き出す触手1本。
    /// ポータルが開く(警告) → ズバッと突き出す → うねうね → (指示があれば)鞭のように薙ぎ払う → ポータルへ戻る → 消える、を自分で進める。
    /// 出したボスのプールで使い回すので、消える時は破棄せず <see cref="Begin"/> で渡された戻し先へ返す。
    ///
    /// プレハブの構成: このコンポーネント(地面の出現位置) / Pivot(根元。突き出し・引っ込みはこれを上下させる) / 触手モデル(ボス本体から Ctrl+D で取り出したもの)。
    /// 薙ぎ払いから戻るまでは、触手を重さとしなりのある紐(<see cref="TentacleRope"/>)として動かし、外からは根元の向きだけを動かす。
    /// 根元の向きも決めた姿勢へ直接動かさず、目標へ加速・減速しながら追いつかせるので、動き出しと止まりがつながる。
    /// その先は慣性で付いてくるので、振れば先端が遅れ、速く振るほど遠心力で伸び、根元が止まれば先端が追い越して巻き込む。
    /// 触手モデルはどんな姿勢で置いてもよい。起動時に、根元が出現位置に来て先端が真上を向くよう合わせる。
    /// 地面より下に隠した部分はステージ(不透明な高台)に隠れて見えない。
    /// </summary>
    public class KurageGroundTentacle : MonoBehaviour
    {
        private enum Phase
        {
            PortalOpen,
            Thrust,
            Wiggle,
            SweepWindup,
            Sweep,
            Recoil,
            Retract,
            PortalClose,
        }

        [Header("参照")]
        [Tooltip("突き出し・引っ込みで上下させる根元。出現位置(この GameObject の原点)に置く")]
        [SerializeField]
        private Transform _pivot;

        [Tooltip("触手モデルのルート。起動時に Pivot の下で向きと位置を合わせる")]
        [SerializeField]
        private Transform _tentacleModel;

        [Tooltip("地面のポータル。開閉はこの Transform の拡大縮小で行う")]
        [SerializeField]
        private Transform _portal;

        [Tooltip("触手全体の当たり判定")]
        [SerializeField]
        private TentacleChainHitbox _hitbox;

        [Tooltip(
            "触手のボーン連鎖(根元から先端の順)。当たり判定のボーンと同じものを並べる。"
                + "ここのバネ設定で、うねりや薙ぎ払いに遅れ(しなり)を付ける。うねり自体は下の「うねり」で付ける"
        )]
        [SerializeField]
        private TBossLimbChainSettings _wiggle = new TBossLimbChainSettings();

        [Header("時間（秒）")]
        [Tooltip("ポータルが開いてから突き出すまで。プレイヤーが逃げるための警告の時間")]
        [SerializeField]
        private float _warningDuration = 1.0f;

        [Tooltip("ポータルが開く・閉じる時間")]
        [SerializeField]
        private float _portalOpenDuration = 0.25f;

        [Tooltip("突き出す時間。短いほどズバッと出る")]
        [SerializeField]
        private float _thrustDuration = 0.12f;

        [Tooltip("突き出した後にうねうねしている時間")]
        [SerializeField]
        private float _wiggleDuration = 1.2f;

        [Tooltip("薙ぎ払いの構え(根元を倒し、振る向きと逆へ引く)時間。薙ぎ払いの予兆になる")]
        [SerializeField]
        private float _sweepWindupDuration = 0.6f;

        [Tooltip(
            "根元を振る時間。ゆっくり動き出して加速し、終わり際に急に止まる。先端は勢いで遅れて付いてきて追い越す"
        )]
        [SerializeField]
        private float _sweepDuration = 0.45f;

        [Tooltip("根元を振った後も、勢いで先端が回ってくる間は当たり判定を残す時間")]
        [SerializeField]
        private float _sweepFollowThrough = 0.25f;

        [Tooltip("振り抜いた後、触手を立たせる時間。立ってからポータルへ戻る")]
        [SerializeField]
        private float _recoilDuration = 0.55f;

        [Tooltip("ポータルへ戻る時間")]
        [SerializeField]
        private float _retractDuration = 0.9f;

        [Header("動き")]
        [Tooltip("ポータルが開き切った時の大きさ(倍率)")]
        [SerializeField]
        private float _portalOpenScale = 1f;

        [Header("うねり")]
        [Tooltip("ボーン1本ごとに曲げる角度（度）。先へ行くほど積み重なってくねる")]
        [SerializeField]
        private float _writheAngle = 10f;

        [Tooltip("根元側のボーンの曲げに掛ける倍率。小さいほど根元は落ち着く")]
        [SerializeField]
        private float _writheBaseScale = 0.3f;

        [Tooltip("先端側のボーンの曲げに掛ける倍率。大きいほど先端が大きく暴れる")]
        [SerializeField]
        private float _writheTipScale = 2f;

        [Tooltip(
            "地面からこの長さ（m）の間で、うねりを0から最大へつなぐ。地面より下に入った部分はまっすぐにして、ポータルの穴から出入りさせる"
        )]
        [SerializeField]
        private float _groundBlendLength = 2f;

        [Tooltip("うねりの速さ（回/秒）")]
        [SerializeField]
        private float _writheFrequency = 0.6f;

        [Tooltip("ボーン1本ごとのうねりのずれ（ラジアン）。根元から先端へ波が伝わって見える")]
        [SerializeField]
        private float _writhePhasePerBone = 0.8f;

        [Tooltip(
            "ポータルへ戻る間、うねりを何倍の速さにするか。もがきながら引き込まれるように見せる"
        )]
        [SerializeField]
        private float _retractWritheSpeed = 3f;

        [Tooltip("ポータルへ戻る間、うねりの曲げを何倍にするか")]
        [SerializeField]
        private float _retractWritheBoost = 1.5f;

        [Header("薙ぎ払い")]
        [Tooltip("薙ぎ払いで振る角度（度）。プレイヤーの方向を真ん中にしてこの角度ぶん振る")]
        [SerializeField]
        private float _sweepArcAngle = 270f;

        [Tooltip("構えで、振り始めの向きよりさらに逆へ引く角度（度）。振る前の予備動作になる")]
        [SerializeField]
        private float _sweepCockAngle = 30f;

        [Tooltip("振り終わりの向きから、さらに振り抜く角度（度）")]
        [SerializeField]
        private float _sweepFollowAngle = 20f;

        [Tooltip("構えで、根元を振る向きと逆へ倒す角度（度）。真上からの角度")]
        [SerializeField]
        private float _windupRootTilt = 45f;

        [Tooltip(
            "構えで、継ぎ目ごとに上へ巻き上げる角度（度）。サソリの尾のように触手を立てて溜める"
        )]
        [SerializeField]
        private float _windupCoilAngle = 14f;

        [Tooltip("構えで触手に力を入れる倍率(曲がりにくさに掛ける)")]
        [SerializeField]
        private float _windupTension = 4f;

        [Tooltip("振る間、根元を真上から倒す角度（度）。大きいほど低い所を払う")]
        [SerializeField]
        private float _sweepRootTilt = 72f;

        [Tooltip("振る間、継ぎ目ごとに地面の方へ曲げる角度（度）")]
        [SerializeField]
        private float _sweepCurlAngle = 5f;

        [Tooltip(
            "振る間、触手に力を入れる倍率。張った触手が勢いを先端まで伝え、途中でだらんと寝ずに最後まで振り抜く"
        )]
        [SerializeField]
        private float _strikeTension = 12f;

        [Tooltip(
            "振り抜いた後、触手に力を入れて硬くする倍率。先までピンと立ち上がり、床を引きずらずに穴へ引き込まれる"
        )]
        [SerializeField]
        private float _retractTension = 20f;

        [Tooltip(
            "振り抜いた後に触手を立たせる時、継ぎ目ごとに上へ巻き上げる強さ(構えの巻き上げの何倍か)"
        )]
        [SerializeField]
        private float _riseCoil = 0.6f;

        [Tooltip("立たせ終わった時の、根元の真上からの角度（度）")]
        [SerializeField]
        private float _settleRootTilt = 10f;

        [Tooltip(
            "構えと立ち上がりで、根元の向きが目標へ追いつく速さ。大きいほどきびきび、小さいほどゆったり動き出して止まる"
        )]
        [SerializeField]
        private float _rootFollowSpeed = 9f;

        [Tooltip("振る間の、根元の向きが目標へ追いつく速さ。振りの勢いを鈍らせないよう速くする")]
        [SerializeField]
        private float _strikeFollowSpeed = 30f;

        [Tooltip("紐として動かす間も、筋肉でうねらせる強さ(うねりの何倍か)")]
        [SerializeField]
        private float _ropeWritheScale = 0.6f;

        [Tooltip("薙ぎ払いからポータルへ戻るまでの、触手の重さとしなり")]
        [SerializeField]
        private TentacleRopeSettings _rope = new TentacleRopeSettings();

        [Header("ダメージ倍率（攻撃力に掛ける）")]
        [SerializeField]
        private float _thrustDamageMultiplier = 0.8f;

        [SerializeField]
        private float _sweepDamageMultiplier = 0.8f;

        private TBossLimbChainDriver _wiggleDriver;
        private Quaternion[] _restLocalRotations;

        // 各ボーンの根元からの長さ（m）。地面より下に入っているボーンを判定するのに使う
        private float[] _arcLengths;
        private Vector3 _portalBaseScale;
        private float _chainLength;

        private EnemyStatus _attacker;
        private KurageBossSounds _sounds;
        private KurageRingRestraint _restraint;
        private Transform _player;
        private bool _sweep;
        private bool _begun;
        private float _sweepStartYaw;
        private float _sweepSign;

        // 薙ぎ払いから戻るまで触手を紐として動かす。切り替えの時に直前の動きの勢いを引き継ぐため、1フレーム前の位置を覚えておく
        private TentacleRope _ropeSimulation;
        private bool _usingRope;
        private Vector3[] _lastBonePositions;
        private float _lastDeltaTime = 1f / 60f;
        private Func<int, Vector3, Vector3> _muscleDirection;

        // 振る間に地面の方へ曲げる度合い(0〜1)。構えで強め、余韻で戻す
        private float _curl;

        // 触手に力を入れて硬くする度合い(曲がりにくさの倍率)。余韻から戻りにかけて強め、起き上がってから穴へ引き込まれるようにする
        private float _tension = 1f;
        private float _musclePower;

        // 根元の向き(方角と、真上から倒す角度。度)と、その変わる速さ。目標へ加速・減速しながら追いつかせる
        private float _rootYaw;
        private float _rootYawVelocity;
        private float _rootTilt;
        private float _rootTiltVelocity;
        private float _targetYaw;
        private float _targetTilt;
        private float _windupStartYaw;
        private float _windupStartTilt;
        private float _followSpeed;

        // うねりの位相。戻る間だけ速く進めるため、時間ではなく位相を積み上げる
        private float _writhePhase;

        private Phase _phase;
        private float _timer;

        // ポータルへ戻り切った時に、出した側のプールへ返す
        private Action<KurageGroundTentacle> _onFinished;

        private void Awake()
        {
            Assert.IsNotNull(_pivot, $"{name}: Pivot が設定されていません");
            Assert.IsNotNull(_tentacleModel, $"{name}: 触手モデルが設定されていません");
            Assert.IsNotNull(_portal, $"{name}: ポータルが設定されていません");
            Assert.IsNotNull(_hitbox, $"{name}: 当たり判定が設定されていません");
            Assert.IsTrue(_wiggle.bones.Length >= 2, $"{name}: うねうねさせるボーンが2本未満です");

            AlignTentacleUpright();

            // Animator が無いので、毎フレームこの姿勢へ戻してから揺れを乗せる(戻さないと揺れが積み重なる)
            Transform[] bones = _wiggle.bones;
            _restLocalRotations = new Quaternion[bones.Length];
            _arcLengths = new float[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                _restLocalRotations[i] = bones[i].localRotation;
                if (i > 0)
                    _arcLengths[i] =
                        _arcLengths[i - 1]
                        + Vector3.Distance(bones[i - 1].position, bones[i].position);
            }

            _ropeSimulation = new TentacleRope(bones);
            _lastBonePositions = new Vector3[bones.Length];
            _muscleDirection = MuscleDirection;

            _wiggleDriver = new TBossLimbChainDriver(_wiggle);
            _portalBaseScale = _portal.localScale;
            _portal.localScale = Vector3.zero;
            SetBuried(1f);
        }

        /// <summary>
        /// 攻撃を始める。出した側(クラゲボス)が出現位置に置いた直後に呼ぶ。
        /// </summary>
        /// <param name="restraint">わっかの拘束。締めている間は追撃しないため、当たり判定を出さない</param>
        /// <param name="sweep">突き出した後に、プレイヤーの方へ薙ぎ払うか</param>
        /// <param name="onFinished">ポータルへ戻り切った時に呼ぶ。出した側のプールへ返す</param>
        public void Begin(
            EnemyStatus attacker,
            KurageBossSounds sounds,
            KurageRingRestraint restraint,
            Transform player,
            bool sweep,
            Action<KurageGroundTentacle> onFinished
        )
        {
            _attacker = attacker;
            _sounds = sounds;
            _restraint = restraint;
            _player = player;
            _sweep = sweep;
            _onFinished = onFinished;
            ResetPose();
            _begun = true;
            EnterPhase(Phase.PortalOpen);
            _sounds.PlayPortalOpen(transform.position);
        }

        private void Update() => Tick(Time.deltaTime);

        private void LateUpdate() => ApplyPose(Time.deltaTime);

        /// <summary>段階を進める。Update から呼ぶ(動きを1フレームずつ確かめる時は外から直接呼べるよう時間を引数で受ける)。</summary>
        private void Tick(float deltaTime)
        {
            if (!_begun)
                return;

            // 出したボスが倒れて消えたら、攻撃を続けずに消える
            if (_attacker == null)
            {
                Destroy(gameObject);
                return;
            }

            // 拘束が始まる前に出していた触手でも、締めている間のプレイヤーには当てない
            if (_restraint.IsRestraining)
                _hitbox.Deactivate();

            _timer += deltaTime;
            switch (_phase)
            {
                case Phase.PortalOpen:
                    SetPortalOpen(_timer / _portalOpenDuration);
                    if (_timer >= _warningDuration)
                    {
                        EnterPhase(Phase.Thrust);
                        _hitbox.Activate(_attacker, _thrustDamageMultiplier);
                        _sounds.PlayTentacleEmerge(transform.position);
                    }
                    break;

                case Phase.Thrust:
                    // 出だしが一番速い(ズバッと出て止まる)動きにする
                    float t = Mathf.Clamp01(_timer / _thrustDuration);
                    SetBuried((1f - t) * (1f - t));
                    if (t >= 1f)
                    {
                        _hitbox.Deactivate();
                        EnterPhase(Phase.Wiggle);
                    }
                    break;

                case Phase.Wiggle:
                    if (_timer < _wiggleDuration)
                        break;
                    if (_sweep && _player != null)
                    {
                        DecideSweepDirection();
                        StartRope();
                        EnterPhase(Phase.SweepWindup);
                    }
                    else
                    {
                        EnterPhase(Phase.Retract);
                    }
                    break;

                case Phase.SweepWindup:
                    // 構えから振りまでは1本のつながった動き。うねうねの姿勢から途切れずに入り、
                    // 根元の追いつきも構えの終わりへ向けて振りの速さへ上げていく
                    UpdateWhipTargets(_timer);
                    _followSpeed = Mathf.Lerp(
                        _rootFollowSpeed,
                        _strikeFollowSpeed,
                        Ramp(_timer, 0.7f * _sweepWindupDuration, _sweepWindupDuration)
                    );
                    if (_timer >= _sweepWindupDuration)
                    {
                        EnterPhase(Phase.Sweep);
                        _hitbox.Activate(_attacker, _sweepDamageMultiplier);
                        _sounds.PlayTentacleSweep(transform.position);
                    }
                    break;

                case Phase.Sweep:
                    // 根元を止めた後(振り抜き)も、先端は勢いで回るので当たり判定を残す
                    UpdateWhipTargets(_sweepWindupDuration + _timer);
                    _followSpeed = _strikeFollowSpeed;
                    if (_timer >= _sweepDuration + _sweepFollowThrough)
                    {
                        _hitbox.Deactivate();
                        EnterPhase(Phase.Recoil);
                    }
                    break;

                case Phase.Recoil:
                    // 振り抜いた勢いが残ったまま、力を入れて触手を巻き上げ、立たせる(寝たまま戻らない)
                    float settle = Mathf.SmoothStep(0f, 1f, _timer / _recoilDuration);
                    _followSpeed = _rootFollowSpeed;
                    _targetTilt = Mathf.Lerp(_sweepRootTilt, _settleRootTilt, settle);
                    _curl = Mathf.Lerp(1f, -_riseCoil, settle);
                    _tension = Mathf.Lerp(_strikeTension, _retractTension, settle);
                    if (_timer >= _recoilDuration)
                        EnterPhase(Phase.Retract);
                    break;

                case Phase.Retract:
                    float r = Mathf.Clamp01(_timer / _retractDuration);
                    // 紐で動かしている時は、加減速をつないで引き込む(急に引くと先端が跳ねる)
                    SetBuried(_usingRope ? Mathf.SmoothStep(0f, 1f, r) : r * r);
                    // 紐で動かしている時は、根元を真っすぐ立てながら穴へ引き込む
                    _targetTilt = Mathf.Lerp(_settleRootTilt, 0f, Mathf.SmoothStep(0f, 1f, r));
                    if (r >= 1f)
                        EnterPhase(Phase.PortalClose);
                    break;

                case Phase.PortalClose:
                    SetPortalOpen(1f - _timer / _portalOpenDuration);
                    if (_timer >= _portalOpenDuration)
                        Finish();
                    break;
            }
        }

        /// <summary>ボーンの姿勢を作る。LateUpdate から呼ぶ。</summary>
        private void ApplyPose(float deltaTime)
        {
            if (!_begun)
                return;

            Transform[] bones = _wiggle.bones;
            for (int i = 0; i < bones.Length; i++)
                bones[i].localRotation = _restLocalRotations[i];

            // 地上に出ている間はずっとうねらせる。引っ込む時はまっすぐ張らず、速く大きくもがかせる
            float power = _phase is Phase.PortalOpen or Phase.PortalClose ? 0f : 1f;
            bool retracting = _phase == Phase.Retract;
            _writhePhase +=
                deltaTime
                * _writheFrequency
                * Mathf.PI
                * 2f
                * (retracting ? _retractWritheSpeed : 1f);
            float writhePower = power * (retracting ? _retractWritheBoost : 1f);

            if (_usingRope)
            {
                // 根元の向きだけを動かし、その先は紐の物理に任せる。筋肉のうねりも力として加え続ける
                UpdateRootDirection(deltaTime);
                _musclePower = writhePower * _ropeWritheScale;
                _ropeSimulation.Step(
                    deltaTime,
                    _rope,
                    RootDirection(),
                    transform.position,
                    _muscleDirection,
                    _tension
                );
                _ropeSimulation.WriteToBones();
            }
            else
            {
                ApplyWrithe(writhePower);
                // うねりはバネで遅れて追いつかせて柔らかさを出す
                _wiggleDriver.Apply(0f, power, deltaTime);
            }

            for (int i = 0; i < bones.Length; i++)
                _lastBonePositions[i] = bones[i].position;
            _lastDeltaTime = deltaTime;
        }

        /// <summary>使い回した時に前回の動きが残らないよう、ポータルを閉じて触手を地面の下へ戻す。</summary>
        private void ResetPose()
        {
            Transform[] bones = _wiggle.bones;
            for (int i = 0; i < bones.Length; i++)
                bones[i].localRotation = _restLocalRotations[i];
            _portal.localScale = Vector3.zero;
            SetBuried(1f);
            _hitbox.Deactivate();
            _usingRope = false;
            _tension = 1f;
            _curl = 0f;
            _musclePower = 0f;
            _writhePhase = 0f;
        }

        private void Finish()
        {
            _begun = false;
            _hitbox.Deactivate();
            _onFinished(this);
        }

        private void EnterPhase(Phase phase)
        {
            _phase = phase;
            _timer = 0f;
            if (phase == Phase.Thrust)
                _wiggleDriver.ResetState();
        }

        /// <summary>触手の根元を出現位置へ、先端を真上へ向ける。</summary>
        private void AlignTentacleUpright()
        {
            Transform[] bones = _wiggle.bones;
            Transform root = bones[0];
            Transform tip = bones[bones.Length - 1];

            Vector3 chain = tip.position - root.position;
            _chainLength = chain.magnitude;
            Quaternion deltaRotation = Quaternion.FromToRotation(chain.normalized, Vector3.up);
            Vector3 rotatedRootOffset = deltaRotation * (root.position - _tentacleModel.position);
            _tentacleModel.SetPositionAndRotation(
                _pivot.position - rotatedRootOffset,
                deltaRotation * _tentacleModel.rotation
            );
        }

        /// <summary>buried=1 で触手全体が地面の下、0 で根元まで地上に出る。</summary>
        private void SetBuried(float buried)
        {
            _pivot.localPosition = Vector3.down * (_chainLength * buried);
        }

        private void SetPortalOpen(float open)
        {
            _portal.localScale = _portalBaseScale * (_portalOpenScale * Mathf.Clamp01(open));
        }

        private void DecideSweepDirection()
        {
            Vector3 toPlayer = _player.position - transform.position;
            float playerYaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
            _sweepSign = UnityEngine.Random.value < 0.5f ? 1f : -1f;
            _sweepStartYaw = playerYaw - _sweepSign * _sweepArcAngle * 0.5f;
        }

        // 構えで引いた向き(振り始めよりさらに逆)と、振り抜いた向き
        private float CockYaw => _sweepStartYaw - _sweepSign * _sweepCockAngle;
        private float FollowThroughYaw =>
            _sweepStartYaw + _sweepSign * (_sweepArcAngle + _sweepFollowAngle);

        /// <summary>
        /// 紐での動きを始める。今の根元の向きから根元を動かし始め、直前の動きの勢いも引き継ぐ(うねうねから途切れずに移る)。
        /// </summary>
        private void StartRope()
        {
            _usingRope = true;
            _tension = 1f;
            _curl = 0f;
            _ropeSimulation.Begin(_lastBonePositions, _lastDeltaTime);

            Transform[] bones = _wiggle.bones;
            Vector3 rootSegment = bones[1].position - bones[0].position;
            _rootTilt = Vector3.Angle(Vector3.up, rootSegment);
            float currentYaw = Mathf.Atan2(rootSegment.x, rootSegment.z) * Mathf.Rad2Deg;
            // ほぼ真上を向いている時の方角は意味を持たないので、構えの向きから始める
            _rootYaw = _rootTilt > 1f ? CockYaw + Mathf.DeltaAngle(CockYaw, currentYaw) : CockYaw;
            _rootYawVelocity = 0f;
            _rootTiltVelocity = 0f;
            _targetYaw = _rootYaw;
            _targetTilt = _rootTilt;
            _windupStartYaw = _rootYaw;
            _windupStartTilt = _rootTilt;
            _followSpeed = _rootFollowSpeed;
        }

        /// <summary>根元の向きを、目標へ加速・減速しながら追いつかせる(行き過ぎない最速の追いつき方)。</summary>
        private void UpdateRootDirection(float deltaTime)
        {
            float omega = _followSpeed;
            _rootYawVelocity +=
                (omega * omega * (_targetYaw - _rootYaw) - 2f * omega * _rootYawVelocity)
                * deltaTime;
            _rootYaw += _rootYawVelocity * deltaTime;
            _rootTiltVelocity +=
                (omega * omega * (_targetTilt - _rootTilt) - 2f * omega * _rootTiltVelocity)
                * deltaTime;
            _rootTilt += _rootTiltVelocity * deltaTime;
        }

        /// <summary>
        /// 構えから振り抜きまでの根元の目標(向き・倒れ・巻き上げ・力の入れ具合)を、構えを始めてからの時間で決める。
        /// 鞭は根元が輪を描くように動き、向きが反転する瞬間も別の向きへ動き続けて止まらない。
        /// そのため各要素の変化を少しずつずらして重ね、構えの終わりで向きが反転する時も倒れ込みが続くようにする。
        /// </summary>
        private void UpdateWhipTargets(float clock)
        {
            float windup = _sweepWindupDuration;
            float strike = _sweepDuration;

            // 向き: 構えで逆へ引き(だんだん遅くなって折り返す)、振りで加速して振り抜く
            if (clock < windup)
            {
                float w = clock / windup;
                _targetYaw = Mathf.Lerp(_windupStartYaw, CockYaw, 1f - (1f - w) * (1f - w));
            }
            else
            {
                _targetYaw = Mathf.Lerp(
                    CockYaw,
                    FollowThroughYaw,
                    WhipSwing((clock - windup) / strike)
                );
            }

            // 倒れ: 構えの前半で逆へ倒し、構えの後半から振りの前半にかけて低く倒し込む(折り返しの間も止まらない)
            float lean = Ramp(clock, 0f, 0.6f * windup);
            float dive = Ramp(clock, 0.6f * windup, windup + 0.35f * strike);
            _targetTilt =
                _windupStartTilt
                + (_windupRootTilt - _windupStartTilt) * lean
                + (_sweepRootTilt - _windupRootTilt) * dive;

            // 巻き上げ: 構えで上へ巻き上げて溜め、構えの終わりから振りの前半にかけてほどいて地面側へ向ける
            float coil = Ramp(clock, 0f, 0.6f * windup);
            float uncoil = Ramp(clock, 0.75f * windup, windup + 0.5f * strike);
            _curl = -coil + 2f * uncoil;

            // 力の入れ具合: 構えで少し入れ、振りへ入るところで強く入れて触手を張らせる
            _tension =
                Mathf.Lerp(1f, _windupTension, coil)
                + (_strikeTension - _windupTension)
                    * Ramp(clock, 0.75f * windup, windup + 0.35f * strike);
        }

        private static float Ramp(float value, float from, float to) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

        /// <summary>
        /// 振りの進み具合(0〜1)。ゆっくり動き出して加速し、終わり際に急に止まる(速さが一番出るのは3分の2あたり)。
        /// 鞭は根元が加速して勢いを溜め、急に止めることで勢いが先端へ抜けて振り抜ける。
        /// </summary>
        private static float WhipSwing(float progress)
        {
            float u = Mathf.Clamp01(progress);
            return 4f * u * u * u - 3f * u * u * u * u;
        }

        private Vector3 RootDirection()
        {
            float yaw = _rootYaw * Mathf.Deg2Rad;
            Vector3 heading = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            return Quaternion.AngleAxis(_rootTilt, Vector3.Cross(Vector3.up, heading)) * Vector3.up;
        }

        /// <summary>紐で動かしている間、i 番目の継ぎ目で次の部分を向けたい向き(筋肉で曲げようとする形)。</summary>
        private Vector3 MuscleDirection(int index, Vector3 parentDirection)
        {
            // 振る間は継ぎ目ごとに地面の方へ曲げ(低い所を払う)、構えと立ち上がりでは上へ巻き上げる。うねりも筋肉の力として乗せ続ける
            Vector3 direction = Vector3.RotateTowards(
                parentDirection,
                Vector3.down,
                (_curl >= 0f ? _sweepCurlAngle : _windupCoilAngle) * _curl * Mathf.Deg2Rad,
                0f
            );
            return WritheRotation(index, _musclePower) * direction;
        }

        /// <summary>うねりをボーンへ乗せる(紐で動かしていない間)。</summary>
        private void ApplyWrithe(float power)
        {
            if (power <= 0f)
                return;

            Transform[] bones = _wiggle.bones;
            int count = bones.Length;
            float buriedLength = -_pivot.localPosition.y;
            // 根元は地面から生えている所なので揺らさない
            for (int i = 1; i < count; i++)
            {
                // 地面より下のボーンはまっすぐ(穴から出入りする)、地上に出たところから徐々にうねらせる
                float aboveGround =
                    buriedLength > 0f
                        ? Mathf.Clamp01((_arcLengths[i] - buriedLength) / _groundBlendLength)
                        : 1f;
                bones[i].rotation = WritheRotation(i, power * aboveGround) * bones[i].rotation;
            }
        }

        /// <summary>
        /// i 番目のボーンに乗せるうねりの回転。曲げを根元から先端へずらして揺らすことで波が伝わるようにし、
        /// 2つの向きを周期をずらして揺らして、平面の往復ではなくくねるように見せる。先端ほど大きく曲げる。
        /// </summary>
        private Quaternion WritheRotation(int index, float power)
        {
            float along = (float)index / (_wiggle.bones.Length - 1);
            float angle =
                _writheAngle * Mathf.Lerp(_writheBaseScale, _writheTipScale, along) * power;
            float phase = _writhePhase - index * _writhePhasePerBone;
            return Quaternion.AngleAxis(Mathf.Sin(phase) * angle, Vector3.right)
                * Quaternion.AngleAxis(Mathf.Sin(phase * 0.77f + 1.3f) * angle, Vector3.forward);
        }
    }
}
