using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 会話中の入手表示。アイテム画像か3D武器モデル(RenderTexture に1回だけ撮る)を出し、登場・退場の演出を行う。
    /// 武器の撮影用リグはシーンから離れた場所に組み、片付けるときに破棄する。
    /// </summary>
    public sealed class DialogueRewardView : MonoBehaviour
    {
        // 武器モデルだけを撮るためのレイヤー(キャラタブのプレビューと同じ)
        private const int RewardLayer = 31;

        [Header("アイテム")]
        [SerializeField]
        private Image _itemImage;

        [SerializeField]
        private Image _itemBackdrop;

        [SerializeField]
        private Sprite _defaultItemSprite; // アイテムの画像が引けなかったときの画像

        [SerializeField]
        private Vector2 _itemSize = new(256f, 256f);

        [SerializeField]
        private Vector2 _position = new(0f, 220f); // Canvas 中央からのオフセット(ウィンドウの上あたり)

        [Header("武器")]
        [SerializeField]
        private RawImage _weaponImage;

        [SerializeField]
        private Image _weaponBackdrop;

        [SerializeField]
        private Vector3 _weaponModelEuler = new(10f, -20f, 0f); // 静止表示するモデルの傾き(見栄えの3/4アングル)

        [SerializeField]
        private int _weaponTextureSize = 512; // RenderTexture の解像度

        [SerializeField]
        private Vector2 _weaponImageSize = new(640f, 360f); // 武器表示枠(横長。刀に合わせた比率)

        [SerializeField]
        private float _weaponFrameFill = 1.3f; // 投影サイズへの余白率(1.0でぴったり。小さいほど大きく写る)

        [SerializeField]
        private Color _weaponBackdropColor = new(0f, 0f, 0f, 0.4f); // 武器背後の枠(a=0で既定の色)

        private GameObject _weaponRig;
        private RenderTexture _renderTexture;

        private void OnDisable() => HideAll();

        private void OnDestroy() => HideAll();

        /// <summary>アイテム画像を登場前の状態で出す。<paramref name="icon"/> が null なら既定の画像を使う。</summary>
        public void ShowItem(Sprite icon)
        {
            HideItem();
            if (icon == null)
                icon = _defaultItemSprite;
            if (icon == null)
            {
                Debug.LogWarning("[DialogueRewardView] 受け取りアイテムの Sprite が未設定です。");
                return;
            }
            if (_itemImage == null || _itemBackdrop == null)
            {
                Debug.LogWarning("[DialogueRewardView] Prefab のアイテム表示枠が未設定です。");
                return;
            }
            _itemImage.sprite = icon;
            SetRect(_itemImage.rectTransform, _position, _itemSize);
            SetRect(_itemBackdrop.rectTransform, _position, _itemSize + new Vector2(150f, 110f));
            _itemBackdrop.gameObject.SetActive(true);
            _itemImage.gameObject.SetActive(true);
            PrepareEntrance(_itemBackdrop.gameObject);
            PrepareEntrance(_itemImage.gameObject);
        }

        /// <summary>武器モデルを撮って登場前の状態で出す。出せなければ false。</summary>
        public bool ShowWeapon(GameObject prefab)
        {
            HideWeapon();
            if (prefab == null)
            {
                Debug.LogWarning("[DialogueRewardView] 武器モデルが未設定です。");
                return false;
            }
            if (_weaponImage == null || _weaponBackdrop == null)
            {
                Debug.LogWarning("[DialogueRewardView] Prefab の武器表示枠が未設定です。");
                return false;
            }

            _weaponRig = new GameObject("WeaponGetRig");
            _weaponRig.transform.position = new Vector3(0f, -10000f, 0f);
            var model = Instantiate(prefab, _weaponRig.transform);
            model.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.Euler(_weaponModelEuler)
            );
            foreach (var child in model.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = RewardLayer;
            if (!TryComputeBounds(model, out var bounds))
            {
                Debug.LogWarning(
                    "[DialogueRewardView] 武器モデルに Renderer がなく表示できません。"
                );
                HideWeapon();
                return false;
            }

            Vector2 boxSize =
                _weaponImageSize.x > 1f && _weaponImageSize.y > 1f
                    ? _weaponImageSize
                    : new Vector2(640f, 360f);
            float aspect = boxSize.x / Mathf.Max(1f, boxSize.y);
            float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
            float distance = radius * 2f + 1f;
            var cameraObject = new GameObject("WeaponGetCamera");
            cameraObject.transform.SetParent(_weaponRig.transform, true);
            cameraObject.transform.SetPositionAndRotation(
                bounds.center + Vector3.back * distance,
                Quaternion.identity
            );
            var camera = cameraObject.AddComponent<Camera>();
            // モデルは動かないので、毎フレームは描かず最後に1回だけ RenderTexture に撮る
            camera.enabled = false;
            camera.cullingMask = 1 << RewardLayer;
            camera.orthographic = true;
            float neededSize = Mathf.Max(
                bounds.extents.y,
                bounds.extents.x / Mathf.Max(0.01f, aspect)
            );
            camera.orthographicSize =
                Mathf.Max(0.01f, neededSize)
                * Mathf.Clamp(_weaponFrameFill > 0.01f ? _weaponFrameFill : 1.3f, 0.6f, 3f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = distance + radius * 2f + 1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = false;

            AddLight(
                bounds.center + new Vector3(radius, radius * 1.5f, -distance),
                radius * 20f,
                4f
            );
            AddLight(
                bounds.center + new Vector3(-radius, radius * 0.5f, -distance * 0.5f),
                radius * 20f,
                1.5f
            );

            int height = Mathf.Clamp(_weaponTextureSize, 64, 2048);
            int width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 64, 2048);
            _renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
            camera.targetTexture = _renderTexture;
            RenderOnce(camera, _renderTexture);

            _weaponBackdrop.color =
                _weaponBackdropColor.a > 0.001f
                    ? _weaponBackdropColor
                    : new Color(0f, 0f, 0f, 0.35f);
            SetRect(_weaponBackdrop.rectTransform, _position, boxSize);
            _weaponImage.texture = _renderTexture;
            SetRect(_weaponImage.rectTransform, _position, boxSize);
            _weaponBackdrop.gameObject.SetActive(true);
            _weaponImage.gameObject.SetActive(true);
            PrepareEntrance(_weaponBackdrop.gameObject);
            PrepareEntrance(_weaponImage.gameObject);
            return true;
        }

        /// <summary>アイテム画像の登場演出。</summary>
        public IEnumerator AnimateItemIn() => AnimateIn(Targets(_itemBackdrop, _itemImage));

        /// <summary>アイテム画像の退場演出。</summary>
        public IEnumerator AnimateItemOut() => AnimateOut(Targets(_itemBackdrop, _itemImage));

        /// <summary>武器の登場演出。</summary>
        public IEnumerator AnimateWeaponIn() => AnimateIn(Targets(_weaponBackdrop, _weaponImage));

        /// <summary>武器の退場演出。</summary>
        public IEnumerator AnimateWeaponOut() => AnimateOut(Targets(_weaponBackdrop, _weaponImage));

        /// <summary>出ている入手表示をすべて片付ける。</summary>
        public void HideAll()
        {
            HideItem();
            HideWeapon();
        }

        /// <summary>アイテム画像を片付ける。</summary>
        public void HideItem()
        {
            SetActive(_itemImage, false);
            SetActive(_itemBackdrop, false);
        }

        /// <summary>武器の表示を片付け、撮影用のリグと RenderTexture を破棄する。</summary>
        public void HideWeapon()
        {
            SetActive(_weaponImage, false);
            SetActive(_weaponBackdrop, false);
            if (_weaponRig != null)
                Destroy(_weaponRig);
            _weaponRig = null;
            if (_renderTexture == null)
                return;
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }

        private static GameObject[] Targets(Graphic backdrop, Graphic image) =>
            new[]
            {
                backdrop != null ? backdrop.gameObject : null,
                image != null ? image.gameObject : null,
            };

        private static void SetActive(Graphic graphic, bool active)
        {
            if (graphic != null)
                graphic.gameObject.SetActive(active);
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void PrepareEntrance(GameObject target)
        {
            if (!target.TryGetComponent(out CanvasGroup group))
                group = target.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            target.transform.localScale = Vector3.one * 0.86f;
        }

        private static IEnumerator AnimateIn(GameObject[] targets)
        {
            const float duration = 0.28f;
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                float scale = Mathf.Lerp(0.86f, 1f, t) + Mathf.Sin(t * Mathf.PI) * 0.025f;
                ApplyVisual(targets, t, scale);
                yield return null;
            }
            ApplyVisual(targets, 1f, 1f);
        }

        private static IEnumerator AnimateOut(GameObject[] targets)
        {
            const float duration = 0.18f;
            for (float elapsed = 0f; elapsed < duration; elapsed += FrameDelta())
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                ApplyVisual(targets, 1f - t, Mathf.Lerp(1f, 0.94f, t));
                yield return null;
            }
            ApplyVisual(targets, 0f, 0.94f);
        }

        private static void ApplyVisual(GameObject[] targets, float alpha, float scale)
        {
            foreach (var target in targets)
            {
                if (target == null)
                    continue;
                if (target.TryGetComponent(out CanvasGroup group))
                    group.alpha = alpha;
                target.transform.localScale = Vector3.one * scale;
            }
        }

        private static void RenderOnce(Camera camera, RenderTexture destination)
        {
            if (GraphicsSettings.currentRenderPipeline != null)
                RenderPipeline.SubmitRenderRequest(
                    camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = destination }
                );
            else
                camera.Render();
        }

        private static float FrameDelta() => Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);

        private void AddLight(Vector3 position, float range, float intensity)
        {
            var lightObject = new GameObject("WeaponGetLight");
            lightObject.transform.SetParent(_weaponRig.transform, true);
            lightObject.transform.position = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.cullingMask = 1 << RewardLayer;
            light.range = range;
            light.intensity = intensity;
        }

        private static bool TryComputeBounds(GameObject target, out Bounds bounds)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
