using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CreativeAI.UI.ConversationUI
{
    /// <summary>アイテム画像と3D武器モデルの獲得表示、および一時リソースの解放を担当する。</summary>
    internal sealed class DialogueRewardPresenter
    {
        private readonly Transform _viewRoot;
        private GameObject _itemObject;
        private GameObject _itemBackdrop;
        private GameObject _weaponRig;
        private GameObject _weaponImage;
        private GameObject _weaponBackdrop;
        private RenderTexture _renderTexture;

        public DialogueRewardPresenter(
            Transform viewRoot,
            Image itemImage,
            Image itemBackdrop,
            RawImage weaponImage,
            Image weaponBackdrop
        )
        {
            _viewRoot = viewRoot;
            _itemObject = itemImage != null ? itemImage.gameObject : null;
            _itemBackdrop = itemBackdrop != null ? itemBackdrop.gameObject : null;
            _weaponImage = weaponImage != null ? weaponImage.gameObject : null;
            _weaponBackdrop = weaponBackdrop != null ? weaponBackdrop.gameObject : null;
        }

        public void ShowItem(Sprite icon, Vector2 position, Vector2 size)
        {
            HideItem();
            if (icon == null)
            {
                Debug.LogWarning("[ConversationView] 受け取りアイテムの Sprite が未設定です。");
                return;
            }
            if (_itemObject == null || _itemBackdrop == null)
            {
                Debug.LogWarning("[ConversationView] Prefab のアイテム表示枠が未設定です。");
                return;
            }
            var image = _itemObject.GetComponent<Image>();
            image.sprite = icon;
            SetRect(image.rectTransform, position, size);
            SetRect(
                _itemBackdrop.transform as RectTransform,
                position,
                size + new Vector2(150f, 110f)
            );
            _itemBackdrop.SetActive(true);
            _itemObject.SetActive(true);
            PrepareEntrance(_itemBackdrop);
            PrepareEntrance(_itemObject);
        }

        public GameObject ShowWeapon(
            GameObject prefab,
            Vector3 modelEuler,
            int textureSize,
            Vector2 imageSize,
            Vector2 position,
            float frameFill,
            Color backdropColor
        )
        {
            HideWeapon();
            if (prefab == null)
            {
                Debug.LogWarning("[ConversationView] 武器モデルが未設定です。");
                return null;
            }
            if (_weaponImage == null || _weaponBackdrop == null)
            {
                Debug.LogWarning("[ConversationView] Prefab の武器表示枠が未設定です。");
                return null;
            }

            _weaponRig = new GameObject("WeaponGetRig");
            _weaponRig.transform.position = new Vector3(0f, -10000f, 0f);
            var model = Object.Instantiate(prefab, _weaponRig.transform);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(modelEuler));
            if (!TryComputeBounds(model, out var bounds))
            {
                Debug.LogWarning("[ConversationView] 武器モデルに Renderer がなく表示できません。");
                HideWeapon();
                return null;
            }

            Vector2 boxSize =
                imageSize.x > 1f && imageSize.y > 1f ? imageSize : new Vector2(640f, 360f);
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
            camera.orthographic = true;
            float neededSize = Mathf.Max(
                bounds.extents.y,
                bounds.extents.x / Mathf.Max(0.01f, aspect)
            );
            camera.orthographicSize =
                Mathf.Max(0.01f, neededSize)
                * Mathf.Clamp(frameFill > 0.01f ? frameFill : 1.3f, 0.6f, 3f);
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

            int height = Mathf.Clamp(textureSize, 64, 2048);
            int width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 64, 2048);
            _renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
            camera.targetTexture = _renderTexture;

            var background = _weaponBackdrop.GetComponent<Image>();
            background.color =
                backdropColor.a > 0.001f ? backdropColor : new Color(0f, 0f, 0f, 0.35f);
            SetRect(background.rectTransform, position, boxSize);

            var rawImage = _weaponImage.GetComponent<RawImage>();
            rawImage.texture = _renderTexture;
            SetRect(rawImage.rectTransform, position, boxSize);
            _weaponBackdrop.SetActive(true);
            _weaponImage.SetActive(true);
            PrepareEntrance(_weaponBackdrop);
            PrepareEntrance(_weaponImage);
            return model;
        }

        public IEnumerator AnimateItemIn() => AnimateIn(_itemBackdrop, _itemObject);

        public IEnumerator AnimateItemOut() => AnimateOut(_itemBackdrop, _itemObject);

        public IEnumerator AnimateWeaponIn() => AnimateIn(_weaponBackdrop, _weaponImage);

        public IEnumerator AnimateWeaponOut() => AnimateOut(_weaponBackdrop, _weaponImage);

        public void HideAll()
        {
            HideItem();
            HideWeapon();
        }

        public void HideItem()
        {
            if (_itemObject != null)
                _itemObject.SetActive(false);
            if (_itemBackdrop != null)
                _itemBackdrop.SetActive(false);
        }

        public void HideWeapon()
        {
            if (_weaponImage != null)
                _weaponImage.SetActive(false);
            if (_weaponBackdrop != null)
                _weaponBackdrop.SetActive(false);
            Destroy(ref _weaponRig);
            if (_renderTexture == null)
                return;
            _renderTexture.Release();
            Object.Destroy(_renderTexture);
            _renderTexture = null;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null)
                return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void PrepareEntrance(GameObject target)
        {
            if (target == null)
                return;
            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
                group = target.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            target.transform.localScale = Vector3.one * 0.86f;
        }

        private static IEnumerator AnimateIn(params GameObject[] targets)
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

        private static IEnumerator AnimateOut(params GameObject[] targets)
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
                var group = target.GetComponent<CanvasGroup>();
                if (group != null)
                    group.alpha = alpha;
                target.transform.localScale = Vector3.one * scale;
            }
        }

        private static float FrameDelta() => Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);

        private void AddLight(Vector3 position, float range, float intensity)
        {
            var lightObject = new GameObject("WeaponGetLight");
            lightObject.transform.SetParent(_weaponRig.transform, true);
            lightObject.transform.position = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
        }

        private static void Place(
            RectTransform rect,
            Transform parent,
            Vector2 position,
            Vector2 size
        )
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.SetAsLastSibling();
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

        private static void Destroy(ref GameObject target)
        {
            if (target != null)
                Object.Destroy(target);
            target = null;
        }
    }
}
