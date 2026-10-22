using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// TutorialBoss の目を光らせる演出（追い込み開始の合図）。見た目だけを受け持ち、いつ光らせるかは知らない。
    ///
    /// 目1つにつき2枚の板ポリゴンを重ねる:
    ///   光の芯 … 小さく強い光の点が一瞬膨らんでから落ち着く。追い込み中は弱く光り続ける（残光）
    ///   光の筋 … 細い光の線が目を中心に左右へ一気に伸び（終わり際で減速）、少し留まってから細くなりながら消える
    /// 筋はボスの頭の傾きに関係なく、画面上で常に水平に伸ばす（板を常にカメラへ向け、カメラの右方向を筋の向きにする）。
    ///
    /// ボスのモデルに目のボーンが無いため、目の位置は頭のボーンの子に置いた空のオブジェクト（_eyeAnchors）で指定する。
    /// 大きさはすべてワールド座標（m）で指定する（ボスの拡大率の影響を受けない）。
    /// </summary>
    [DisallowMultipleComponent]
    public class TBossEyeGlint : MonoBehaviour
    {
        // 筋の板の高さを、芯の線の太さの何倍にするか。芯の線の周りのにじみ（シェーダー側）が板の縁で切れないよう余白を取る
        private const float StreakQuadHeightPerThickness = 10f;

        // 芯が膨らむ時の最大の大きさ（落ち着いた大きさに対する倍率）
        private const float CorePopOvershoot = 1.3f;

        // 消える時に筋をどこまで細くするか（元の太さに対する倍率）
        private const float StreakFadeThinness = 0.3f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");

        [Header("参照")]
        [Tooltip(
            "光らせる目の位置。頭のボーン（mixamorig:Head）の子に置いた空のオブジェクト（左右2つ）"
        )]
        [SerializeField]
        private Transform[] _eyeAnchors = new Transform[0];

        [Tooltip("目の光のマテリアル（シェーダー CreativeAI/TBossEyeGlint）")]
        [SerializeField]
        private Material _glintMaterial;

        [Header("色と強さ")]
        [Tooltip("光の色")]
        [ColorUsage(false, true)]
        [SerializeField]
        private Color _color = new Color(1f, 0.35f, 0.1f);

        [Tooltip("ギラッと光る瞬間の強さ。1 より大きいほど中心が白っぽく飽和して強く見える")]
        [SerializeField]
        [Min(0f)]
        private float _flashIntensity = 4f;

        [Tooltip("追い込み中に光り続ける残光の強さ")]
        [SerializeField]
        [Min(0f)]
        private float _residualIntensity = 0.8f;

        [Header("大きさ（m）")]
        [Tooltip("光の芯の直径")]
        [SerializeField]
        [Min(0.001f)]
        private float _coreSize = 0.12f;

        [Tooltip("光の筋が伸びきった時の全長（左右合わせて）")]
        [SerializeField]
        [Min(0.001f)]
        private float _streakLength = 1.4f;

        [Tooltip("光の筋の芯の線の太さ（周りの淡いにじみはこの数倍に広がる）")]
        [SerializeField]
        [Min(0.001f)]
        private float _streakThickness = 0.015f;

        [Tooltip("目の位置からカメラ側へ浮かせる距離。目の穴の縁に光が欠けないようにする")]
        [SerializeField]
        [Min(0f)]
        private float _cameraOffset = 0.05f;

        [Header("時間（秒）")]
        [Tooltip("光の芯が膨らみきるまでの時間")]
        [SerializeField]
        [Min(0.001f)]
        private float _corePopSeconds = 0.06f;

        [Tooltip("光の筋が伸びきるまでの時間")]
        [SerializeField]
        [Min(0.001f)]
        private float _extendSeconds = 0.12f;

        [Tooltip("伸びきった光の筋が留まる時間")]
        [SerializeField]
        [Min(0f)]
        private float _holdSeconds = 0.1f;

        [Tooltip("光の筋が消えるまでの時間")]
        [SerializeField]
        [Min(0.001f)]
        private float _fadeSeconds = 0.35f;

        [Tooltip("残光がつく・消えるまでの時間")]
        [SerializeField]
        [Min(0.001f)]
        private float _residualFadeSeconds = 0.3f;

        private MeshRenderer[] _coreRenderers;
        private MeshRenderer[] _streakRenderers;
        private MaterialPropertyBlock _propertyBlock;
        private Mesh _quadMesh;

        // ギラッと光ってからの経過時間。光っていなければ負
        private float _flashElapsed = -1f;
        private float _residual;
        private float _residualTarget;
        private bool _isVisible;

        /// <summary>合図の音を鳴らす位置（両目の中間）。</summary>
        public Vector3 CuePosition
        {
            get
            {
                Vector3 sum = Vector3.zero;
                foreach (Transform anchor in _eyeAnchors)
                {
                    sum += anchor.position;
                }
                return _eyeAnchors.Length > 0 ? sum / _eyeAnchors.Length : transform.position;
            }
        }

        private float FlashDuration => _extendSeconds + _holdSeconds + _fadeSeconds;

        private void Awake()
        {
            Assert.IsNotNull(_glintMaterial, $"{name}: 目の光のマテリアルが設定されていません");
            Assert.IsTrue(
                _eyeAnchors.Length > 0,
                $"{name}: 目の位置（_eyeAnchors）が設定されていません"
            );

            _propertyBlock = new MaterialPropertyBlock();
            _quadMesh = CreateQuadMesh();
            _coreRenderers = new MeshRenderer[_eyeAnchors.Length];
            _streakRenderers = new MeshRenderer[_eyeAnchors.Length];
            for (int i = 0; i < _eyeAnchors.Length; i++)
            {
                Assert.IsNotNull(_eyeAnchors[i], $"{name}: 目の位置 {i} が空です");
                _coreRenderers[i] = CreateQuad($"EyeGlintCore_{i}");
                _streakRenderers[i] = CreateQuad($"EyeGlintStreak_{i}");
            }
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_quadMesh != null)
            {
                Destroy(_quadMesh);
            }
        }

        /// <summary>ギラッと1回光らせる（芯が膨らみ、筋が左右へ伸びて消える）。</summary>
        public void Play()
        {
            _flashElapsed = 0f;
        }

        /// <summary>残光（追い込み中、目が弱く光り続ける）をつける・消す。</summary>
        public void SetSustain(bool isOn)
        {
            _residualTarget = isOn ? 1f : 0f;
        }

        /// <summary>再生中に Inspector の右クリックメニューから合図の見た目だけを確かめる。</summary>
        [ContextMenu("合図を再生（確認用・再生中のみ）")]
        private void PlayForCheck()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"{name}: 目の光の確認は再生中に行ってください");
                return;
            }
            Play();
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            _residual = Mathf.MoveTowards(
                _residual,
                _residualTarget,
                deltaTime / _residualFadeSeconds
            );

            bool isFlashing = _flashElapsed >= 0f;
            if (isFlashing)
            {
                _flashElapsed += deltaTime;
                if (_flashElapsed > FlashDuration)
                {
                    _flashElapsed = -1f;
                    isFlashing = false;
                }
            }

            if (!isFlashing && _residual <= 0f)
            {
                SetVisible(false);
                return;
            }

            Camera viewCamera = Camera.main;
            if (viewCamera == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            ApplyPose(viewCamera, isFlashing ? _flashElapsed : FlashDuration);
        }

        /// <summary>経過時間 elapsed の見た目（大きさ・強さ）を、カメラに向けた板へ反映する。</summary>
        private void ApplyPose(Camera viewCamera, float elapsed)
        {
            Transform cameraTransform = viewCamera.transform;
            Quaternion facing = Quaternion.LookRotation(
                cameraTransform.forward,
                cameraTransform.up
            );

            // --- 芯: 膨らんでから落ち着き、筋が消えるのに合わせて残光の強さへ下がる ---
            float popProgress = Mathf.Clamp01(elapsed / _corePopSeconds);
            float settleProgress = Mathf.Clamp01(
                (elapsed - _corePopSeconds) / Mathf.Max(0.001f, _extendSeconds)
            );
            float coreScale =
                elapsed < _corePopSeconds
                    ? Mathf.Lerp(0f, CorePopOvershoot, popProgress)
                    : Mathf.Lerp(CorePopOvershoot, 1f, settleProgress);

            float fadeProgress = Mathf.Clamp01(
                (elapsed - _extendSeconds - _holdSeconds) / _fadeSeconds
            );
            float flashStrength = 1f - fadeProgress;
            float coreIntensity = Mathf.Max(
                _flashIntensity * flashStrength,
                _residualIntensity * _residual
            );
            // 残光だけの時は膨らみの途中の大きさにせず、落ち着いた大きさで光らせる
            if (flashStrength <= 0f)
            {
                coreScale = 1f;
            }

            // --- 筋: 左右へ一気に伸び（終わり際で減速）、留まった後、細くなりながら消える ---
            float extendProgress = Mathf.Clamp01(elapsed / _extendSeconds);
            float easedExtend = 1f - Mathf.Pow(1f - extendProgress, 3f);
            float streakWidth = _streakLength * easedExtend;
            float streakThickness =
                _streakThickness
                * Mathf.Lerp(1f, StreakFadeThinness, fadeProgress)
                * StreakQuadHeightPerThickness;
            float streakIntensity = _flashIntensity * flashStrength;

            for (int i = 0; i < _eyeAnchors.Length; i++)
            {
                Vector3 eyePosition = _eyeAnchors[i].position;
                Vector3 toCamera = (cameraTransform.position - eyePosition).normalized;
                Vector3 position = eyePosition + toCamera * _cameraOffset;

                ApplyQuad(
                    _coreRenderers[i],
                    position,
                    facing,
                    new Vector3(_coreSize, _coreSize, 1f) * coreScale,
                    coreIntensity,
                    0f
                );
                ApplyQuad(
                    _streakRenderers[i],
                    position,
                    facing,
                    new Vector3(streakWidth, streakThickness, 1f),
                    streakIntensity,
                    1f
                );
            }
        }

        private void ApplyQuad(
            MeshRenderer quad,
            Vector3 position,
            Quaternion rotation,
            Vector3 worldSize,
            float intensity,
            float shape
        )
        {
            Transform quadTransform = quad.transform;
            quadTransform.SetPositionAndRotation(position, rotation);
            // 板は自分の子なので、親の拡大率で割ってワールドの大きさにする
            Vector3 parentScale = transform.lossyScale;
            quadTransform.localScale = new Vector3(
                worldSize.x / parentScale.x,
                worldSize.y / parentScale.y,
                1f
            );

            quad.enabled = intensity > 0f && worldSize.x > 0f && worldSize.y > 0f;
            quad.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorId, _color);
            _propertyBlock.SetFloat(IntensityId, intensity);
            _propertyBlock.SetFloat(ShapeId, shape);
            quad.SetPropertyBlock(_propertyBlock);
        }

        private void SetVisible(bool isVisible)
        {
            if (_isVisible == isVisible)
                return;

            _isVisible = isVisible;
            foreach (MeshRenderer quad in _coreRenderers)
            {
                quad.enabled = isVisible;
            }
            foreach (MeshRenderer quad in _streakRenderers)
            {
                quad.enabled = isVisible;
            }
        }

        private MeshRenderer CreateQuad(string quadName)
        {
            var quadObject = new GameObject(quadName);
            // 実行中だけの演出なので、シーンにもプレハブにも保存しない
            quadObject.hideFlags = HideFlags.DontSave;
            quadObject.transform.SetParent(transform, false);
            quadObject.AddComponent<MeshFilter>().sharedMesh = _quadMesh;

            var quadRenderer = quadObject.AddComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = _glintMaterial;
            quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
            quadRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            quadRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            quadRenderer.enabled = false;
            return quadRenderer;
        }

        // 中心が原点で 1×1 の板。UV は 0〜1（シェーダーで中心からの距離として使う）
        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh { name = "EyeGlintQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
