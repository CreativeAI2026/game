using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// ボーン連鎖を縄・ゴムのようにしなやかに見せるための設定。
    /// TutorialBossControllerのInspectorに直接並べて調整する。
    /// </summary>
    [System.Serializable]
    public class TBossLimbChainSettings
    {
        [Tooltip(
            "根元から先端の順に並べたボーン連鎖。\n"
                + "例: LeftShoulder → LeftArm → LeftForeArm → LeftHand → LeftHand_end\n"
                + "（_end_end は本モデルではスキンされていないため入れても見た目は変わらない）"
        )]
        public Transform[] bones;

        [Tooltip("アニメーションの姿勢へ追従するバネの強さ。大きいほど機敏で遅れが小さい。")]
        public float stiffness = 400f;

        [Tooltip("減衰比。1で揺り戻しなし、小さいほどゴムのように行き過ぎてから戻る。")]
        [Range(0.05f, 1f)]
        public float dampingRatio = 0.35f;

        [Tooltip("先端側ボーンの緩さ。1で全ボーン同じ硬さ、小さいほど先端がふにゃふにゃに遅れる。")]
        [Range(0.1f, 1f)]
        public float tipLooseness = 0.65f;

        [Tooltip("伸長量を各ボーンへ分配する重み。横軸0=根元、1=先端。")]
        public AnimationCurve extensionDistribution = AnimationCurve.Linear(0f, 0.4f, 1f, 1f);

        [Tooltip("伸ばしている間に連鎖を伝わる波の振幅（度）。0で無効。")]
        public float waveAmplitude = 8f;

        [Tooltip("波の周波数（Hz）。")]
        public float waveFrequency = 2.5f;

        [Tooltip("1ボーンあたりの波の位相ずれ。大きいほど鞭のように波が伝わって見える。")]
        public float wavePhasePerBone = 0.35f;

        [Tooltip(
            "波を掛ける軸の指定。長さ方向と直交する成分だけが使われる（ねじれ防止のため自動で直交化される）。"
        )]
        public Vector3 waveLocalAxis = Vector3.right;

        [Tooltip("伸ばしきった時に自重で垂れる角度（度）。0で無効。")]
        public float droopAngle = 10f;

        [Tooltip(
            "各ボーンの長さ方向を子ボーンの位置から自動判別する。\n"
                + "このモデルはBlender往復でボーン軸が標準的でない（LeftArmは+Zが長さ方向）ため、通常はONのままにする。"
        )]
        public bool autoDetectBoneAxis = true;

        [Tooltip("autoDetectBoneAxisがOFFの場合、または子が無いボーンで使う長さ方向のローカル軸。")]
        public Vector3 boneLengthLocalAxis = Vector3.up;

        [Tooltip(
            "伸長（localPositionの加算）を行うか。\n"
                + "Two Bone IK等で手の位置をRig側に任せる場合はOFFにし、リーチはIKターゲット側で表現する。"
        )]
        public bool applyExtension = true;
    }

    /// <summary>
    /// ボーン連鎖にバネ追従（ラグ）・進行波・自重ドループ・伸長を与えるドライバ。
    ///
    /// AnimationRiggingを使わず、Animatorが姿勢を書き終えた後（LateUpdate）に
    /// Transformを直接上書きする方式。SpecialAttackState／NormalAttack2Stateと同じ方針。
    ///
    /// 「縄・ゴムらしさ」はほぼ全てセグメント間の遅れから生まれるため、
    /// 各ボーンをアニメーションの姿勢へ「バネで」追従させ、先端側ほど硬さを落として
    /// 遅れを大きくする。これによりアニメーションの弧を壊さずにしなりと揺り戻しが出る。
    ///
    /// 伸長はメートル指定で受け取る。このモデルは階層途中に0.1倍のスケール補正ノードが
    /// 挟まっており、ボーンのlocalPositionはメートルではないため、親のワールドスケールで
    /// 割ってローカル単位へ変換している。
    /// </summary>
    public class TBossLimbChainDriver
    {
        // フレーム落ち時にバネが発散するのを防ぐための上限
        private const float MaxDeltaTime = 0.05f;
        private const float MaxAngularVelocity = 2000f;

        private readonly TBossLimbChainSettings _settings;

        private Quaternion[] _springRot;
        private Vector3[] _angularVelocity;
        private Vector3[] _lastWrittenLocalPos;
        private Vector3[] _lastAppliedOffset;
        private float[] _extensionWeights;

        // ボーンごとの長さ方向と、それに直交する波の軸。リグによって軸の向きが揃っていないため個別に持つ
        private Vector3[] _lengthAxis;
        private Vector3[] _waveAxis;

        private bool _initialized;
        private float _waveTime;

        // 伸長の累積防止に使う「前フレームに自分が書いた記録が有効か」のフラグ。
        // バネの初期化フラグ(_initialized)とは別に持つ。兼用すると、バネを毎フレーム
        // スナップさせる用途（プレビュー等）で記録が無効化され、伸長が累積してしまう。
        private bool _hasExtensionRecord;

        public TBossLimbChainDriver(TBossLimbChainSettings settings)
        {
            _settings = settings;
        }

        /// <summary>ボーン連鎖が2本以上設定されており、適用できる状態か。</summary>
        public bool IsValid =>
            _settings != null && _settings.bones != null && _settings.bones.Length >= 2;

        /// <summary>
        /// ステート開始時に呼ぶ。前回の攻撃で溜まったバネの速度や姿勢を破棄し、
        /// 初回フレームは目標姿勢へスナップさせて開始時のガクつきを防ぐ。
        /// </summary>
        public void ResetState()
        {
            // 伸長を戻してから記録を捨てる。先に記録だけ消すと、既に加算済みの分を差し戻せなくなり
            // 次のApplyでその上にさらに加算されてしまう。
            RestoreExtension();

            _initialized = false;
            _waveTime = 0f;
        }

        /// <summary>
        /// ステート終了時に呼ぶ。本モデルのクリップはHips以外に位置カーブを持たないため、
        /// Animatorが伸長分を書き戻してくれない。放置すると攻撃後も腕が伸びたまま残るので、
        /// 自分が加算したオフセットを明示的に取り消す。
        /// </summary>
        public void RestoreExtension()
        {
            if (!IsValid || _lastAppliedOffset == null)
            {
                return;
            }

            int count = Mathf.Min(_settings.bones.Length, _lastAppliedOffset.Length);
            for (int i = 1; i < count; i++)
            {
                Transform bone = _settings.bones[i];
                if (bone == null || _lastAppliedOffset[i] == Vector3.zero)
                {
                    continue;
                }

                // Animatorが既に書き戻している場合は二重に引かない
                if (bone.localPosition == _lastWrittenLocalPos[i])
                {
                    bone.localPosition -= _lastAppliedOffset[i];
                }

                _lastAppliedOffset[i] = Vector3.zero;
                _lastWrittenLocalPos[i] = bone.localPosition;
            }

            _hasExtensionRecord = false;
        }

        /// <summary>
        /// LateUpdateから毎フレーム呼ぶ。
        /// </summary>
        /// <param name="extension">連鎖全体で伸ばす長さ（メートル）。0で伸長なし。</param>
        /// <param name="wavePower">波とドループの強さ（0〜1）。通常は伸長の進捗を渡す。</param>
        /// <param name="deltaTime">経過時間。Animator.speedを0にしていても実時間で進むため揺れ続ける。</param>
        /// <param name="snapToTarget">
        /// trueの場合、バネの遅れを挟まず目標姿勢へ即座に合わせる（静止プレビュー用）。
        /// ResetStateを毎フレーム呼ぶ代わりにこちらを使うこと。ResetStateは伸長の記録も破棄するため、
        /// 毎フレーム呼ぶと累積防止が働かず腕が際限なく伸びる。
        /// </param>
        public void Apply(
            float extension,
            float wavePower,
            float deltaTime,
            bool snapToTarget = false
        )
        {
            if (!IsValid)
            {
                return;
            }

            float dt = Mathf.Min(deltaTime, MaxDeltaTime);
            if (dt <= 0f)
            {
                return;
            }

            EnsureBuffers();

            _waveTime += dt;

            int count = _settings.bones.Length;
            float power = Mathf.Clamp01(wavePower);
            float looseness = Mathf.Clamp(_settings.tipLooseness, 0.1f, 1f);
            float appliedExtension = _settings.applyExtension ? extension : 0f;

            for (int i = 0; i < count; i++)
            {
                Transform bone = _settings.bones[i];
                if (bone == null)
                {
                    continue;
                }

                // 根元を0、先端を1とした位置。波・ドループ・緩さを先端ほど強くするために使う
                float tipRatio = count > 1 ? (float)i / (count - 1) : 1f;

                Quaternion targetRot = BuildTargetRotation(
                    bone,
                    i,
                    tipRatio,
                    power,
                    _lengthAxis[i],
                    _waveAxis[i]
                );

                if (_initialized && !snapToTarget)
                {
                    IntegrateSpring(i, targetRot, looseness, dt);
                }
                else
                {
                    _springRot[i] = targetRot;
                    _angularVelocity[i] = Vector3.zero;
                }

                bone.localRotation = _springRot[i];

                // 根元ボーンを動かすと手足が胴体から外れてしまうため、伸長は2本目以降に分配する。
                // applyExtensionがOFFでも、前フレームに加算した分を戻すためにextension=0で呼ぶ必要がある。
                if (i > 0)
                {
                    ApplyExtension(bone, i, appliedExtension, _lengthAxis[i]);
                }
            }

            _initialized = true;
            _hasExtensionRecord = true;
        }

        /// <summary>
        /// アニメーションが書いた姿勢を基準に、進行波と自重ドループを乗せた目標姿勢を作る。
        /// アニメーションの回転を置き換えずに差分として乗せることで、振りの弧を壊さない。
        /// </summary>
        private Quaternion BuildTargetRotation(
            Transform bone,
            int index,
            float tipRatio,
            float power,
            Vector3 lengthAxis,
            Vector3 waveAxis
        )
        {
            Quaternion targetRot = bone.localRotation;

            if (power > 0f && !Mathf.Approximately(_settings.waveAmplitude, 0f))
            {
                float phase =
                    (_waveTime * _settings.waveFrequency - index * _settings.wavePhasePerBone)
                    * Mathf.PI
                    * 2f;
                float waveAngle = Mathf.Sin(phase) * _settings.waveAmplitude * power * tipRatio;
                targetRot *= Quaternion.AngleAxis(waveAngle, waveAxis);
            }

            if (power > 0f && !Mathf.Approximately(_settings.droopAngle, 0f) && bone.parent != null)
            {
                Vector3 downInParent = bone.parent.InverseTransformDirection(Vector3.down);
                if (downInParent.sqrMagnitude > 1e-6f)
                {
                    Vector3 boneDirInParent = targetRot * lengthAxis;
                    Quaternion toDown = Quaternion.FromToRotation(
                        boneDirInParent,
                        downInParent.normalized
                    );
                    Quaternion limitedDroop = Quaternion.RotateTowards(
                        Quaternion.identity,
                        toDown,
                        _settings.droopAngle * power * tipRatio
                    );
                    targetRot = limitedDroop * targetRot;
                }
            }

            return targetRot;
        }

        /// <summary>
        /// 回転のバネ・ダンパを1ステップ積分する。
        /// 減衰比で指定させることで、硬さを変えても揺り戻しの質感が保たれる。
        /// </summary>
        private void IntegrateSpring(int index, Quaternion targetRot, float looseness, float dt)
        {
            // 先端側ほど硬さを落として遅れを大きくする
            float stiffness = Mathf.Max(1f, _settings.stiffness * Mathf.Pow(looseness, index));
            float damping = _settings.dampingRatio * 2f * Mathf.Sqrt(stiffness);

            Quaternion delta = targetRot * Quaternion.Inverse(_springRot[index]);
            delta.ToAngleAxis(out float angle, out Vector3 axis);

            // ToAngleAxisは0〜360度で返すため、最短回転になるよう符号を付け直す
            if (angle > 180f)
            {
                angle -= 360f;
            }

            if (axis.sqrMagnitude > 1e-8f && !float.IsNaN(axis.x))
            {
                Vector3 torque =
                    axis.normalized * (angle * stiffness) - _angularVelocity[index] * damping;
                _angularVelocity[index] += torque * dt;
            }
            else
            {
                _angularVelocity[index] -= _angularVelocity[index] * damping * dt;
            }

            float speed = _angularVelocity[index].magnitude;
            if (speed > MaxAngularVelocity)
            {
                _angularVelocity[index] *= MaxAngularVelocity / speed;
                speed = MaxAngularVelocity;
            }

            if (speed > 1e-4f)
            {
                _springRot[index] =
                    Quaternion.AngleAxis(speed * dt, _angularVelocity[index] / speed)
                    * _springRot[index];
                _springRot[index].Normalize();
            }
        }

        /// <summary>
        /// セグメントを自分の向きに沿って伸ばす。子ボーンは親に追従するため、
        /// 各セグメントの伸びが積み上がって連鎖全体のリーチになる。
        /// </summary>
        private void ApplyExtension(Transform bone, int index, float extension, Vector3 lengthAxis)
        {
            Vector3 basePos = bone.localPosition;

            // 回転カーブしか持たないクリップ（本モデルのMixamoクリップはHips以外に位置カーブが無い）では
            // AnimatorやSampleAnimationが位置を書き戻さないため、前フレームに加算した分がそのまま残る。
            // 放置すると毎フレーム累積して腕が際限なく伸びるため、自分が書いた値と一致する場合は差し戻す。
            if (_hasExtensionRecord && basePos == _lastWrittenLocalPos[index])
            {
                basePos -= _lastAppliedOffset[index];
            }

            Vector3 offset = Vector3.zero;
            if (extension > 0.0001f)
            {
                float localExtension = ToLocalUnits(bone, extension) * _extensionWeights[index];
                Vector3 direction = basePos.sqrMagnitude > 1e-8f ? basePos.normalized : lengthAxis;
                offset = direction * localExtension;
            }

            bone.localPosition = basePos + offset;
            _lastWrittenLocalPos[index] = bone.localPosition;
            _lastAppliedOffset[index] = offset;
        }

        /// <summary>
        /// メートル指定の長さを、そのボーンのlocalPositionが属する単位系へ変換する。
        /// localPositionは親のスケールを通してワールドへ変換されるため、親のワールドスケールで割る。
        /// </summary>
        private static float ToLocalUnits(Transform bone, float meters)
        {
            float parentScale = bone.parent != null ? bone.parent.lossyScale.x : 1f;
            return Mathf.Approximately(parentScale, 0f) ? meters : meters / parentScale;
        }

        private void EnsureBuffers()
        {
            int count = _settings.bones.Length;

            if (_springRot == null || _springRot.Length != count)
            {
                _springRot = new Quaternion[count];
                _angularVelocity = new Vector3[count];
                _lastWrittenLocalPos = new Vector3[count];
                _lastAppliedOffset = new Vector3[count];
                _extensionWeights = new float[count];
                _lengthAxis = new Vector3[count];
                _waveAxis = new Vector3[count];
                _initialized = false;
            }

            // ResetState直後に再計算することで、Inspectorでカーブや軸設定を触った結果が次の攻撃から反映される
            if (!_initialized)
            {
                RecalculateExtensionWeights();
                RecalculateBoneAxes();
            }
        }

        /// <summary>
        /// 各ボーンの長さ方向と、波を掛ける直交軸を求める。
        /// このリグはボーンのローカル軸が揃っていない（LeftArmは+Zが長さ方向）ため、
        /// 固定軸を前提にするとドループが「垂れ」ではなく「ねじれ」になってしまう。
        /// そのため子ボーンの位置から長さ方向を実測する。
        /// </summary>
        private void RecalculateBoneAxes()
        {
            int count = _settings.bones.Length;
            Vector3 configuredLength = NormalizedOr(_settings.boneLengthLocalAxis, Vector3.up);
            Vector3 configuredWave = NormalizedOr(_settings.waveLocalAxis, Vector3.right);

            for (int i = 0; i < count; i++)
            {
                Transform bone = _settings.bones[i];
                Vector3 lengthAxis = configuredLength;

                if (_settings.autoDetectBoneAxis && bone != null)
                {
                    // 子のlocalPositionは「このボーンのローカル空間での子の位置」なので、
                    // そのまま長さ方向として使える
                    Transform next = (i + 1 < count) ? _settings.bones[i + 1] : null;
                    if (
                        next != null
                        && next.parent == bone
                        && next.localPosition.sqrMagnitude > 1e-8f
                    )
                    {
                        lengthAxis = next.localPosition.normalized;
                    }
                    else if (
                        bone.childCount > 0
                        && bone.GetChild(0).localPosition.sqrMagnitude > 1e-8f
                    )
                    {
                        lengthAxis = bone.GetChild(0).localPosition.normalized;
                    }
                }

                _lengthAxis[i] = lengthAxis;

                // 波が長さ方向の成分を含むと「うねり」ではなく「ねじれ」になるため直交化する
                Vector3 wave =
                    configuredWave - lengthAxis * Vector3.Dot(configuredWave, lengthAxis);
                if (wave.sqrMagnitude < 1e-6f)
                {
                    wave = Vector3.Cross(lengthAxis, Vector3.forward);
                    if (wave.sqrMagnitude < 1e-6f)
                    {
                        wave = Vector3.Cross(lengthAxis, Vector3.up);
                    }
                }

                _waveAxis[i] = NormalizedOr(wave, Vector3.right);
            }
        }

        private void RecalculateExtensionWeights()
        {
            int count = _extensionWeights.Length;
            if (count == 0)
            {
                return;
            }

            _extensionWeights[0] = 0f;

            float total = 0f;
            for (int i = 1; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 1f;
                float weight =
                    _settings.extensionDistribution != null
                        ? Mathf.Max(0f, _settings.extensionDistribution.Evaluate(t))
                        : 1f;
                _extensionWeights[i] = weight;
                total += weight;
            }

            if (total > 0.0001f)
            {
                for (int i = 1; i < count; i++)
                {
                    _extensionWeights[i] /= total;
                }
            }
            else
            {
                // カーブが全て0の場合でも伸長が死なないよう均等割りにフォールバックする
                float even = 1f / Mathf.Max(1, count - 1);
                for (int i = 1; i < count; i++)
                {
                    _extensionWeights[i] = even;
                }
            }
        }

        private static Vector3 NormalizedOr(Vector3 value, Vector3 fallback)
        {
            return value.sqrMagnitude > 1e-6f ? value.normalized : fallback;
        }
    }
}
