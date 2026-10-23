using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CreativeAI.UI.CharacterUI
{
    // Copies only render data. No gameplay object, Animator, collider or script is instantiated.
    [RequireComponent(typeof(RawImage))]
    public sealed class ModelPreview
        : MonoBehaviour,
            IDragHandler,
            IPointerDownHandler,
            IPointerClickHandler
    {
        [SerializeField]
        private float _rotationSpeed;

        [SerializeField]
        private bool _allowDrag;

        [SerializeField]
        private TMPro.TMP_Text _emptyLabel;
        private readonly List<Mesh> _meshes = new();
        private readonly List<Renderer> _renderers = new();
        private GameObject _stage;
        private GameObject _content;
        private Transform _pivot;
        private Camera _camera;
        private RenderTexture _texture;
        private RawImage _image;
        private float _yaw;
        private float _radius = 1;
        private bool _dragged;
        public event System.Action Clicked;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _dragged = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (
                !_allowDrag
                || eventData.button != PointerEventData.InputButton.Left
                || _dragged
                || eventData.dragging
            )
                return;
            float threshold =
                EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 5;
            if ((eventData.position - eventData.pressPosition).sqrMagnitude > threshold * threshold)
                return;
            Clicked?.Invoke();
        }

        public RenderTexture Texture => _texture;
        public float Yaw => _yaw;
        public int MeshCount => _renderers.Count;

        private void Awake() => _yaw = _allowDrag ? 180f : 0f;

        private void OnEnable() => EnsureResources();

        private void EnsureResources()
        {
            if (_stage != null)
                return;
            _image = GetComponent<RawImage>();
            _image.raycastTarget = _allowDrag;
            _stage = new GameObject("UI Preview Stage") { hideFlags = HideFlags.DontSave };
            _stage.transform.position = new Vector3(0, -10000, 0);
            DontDestroyOnLoad(_stage);
            _pivot = new GameObject("Visuals").transform;
            _pivot.SetParent(_stage.transform, false);
            var cameraObject = new GameObject("Preview Camera");
            cameraObject.transform.SetParent(_stage.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.cullingMask = 1 << 31;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.orthographic = true;
            _camera.nearClipPlane = .01f;
            _camera.farClipPlane = 100;
            var data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            var lightObject = new GameObject("Preview Light");
            lightObject.transform.SetParent(_stage.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(30, -30, 0);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << 31;
            light.intensity = 1.2f;
            light.enabled = false;
            _texture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32)
            {
                name = "UI Model Preview",
                hideFlags = HideFlags.DontSave,
            };
            _texture.Create();
            _image.enabled = false;
            _image.texture = _texture;
            _camera.targetTexture = _texture;
        }

        public void SetSources(Transform origin, IEnumerable<Renderer> sources)
        {
            if (!isActiveAndEnabled)
                return;
            EnsureResources();
            ClearVisuals();
            _image.enabled = false;
            if (_emptyLabel != null)
                _emptyLabel.gameObject.SetActive(true);
            if (origin == null)
                return;
            _pivot.localRotation = Quaternion.identity;
            _content = new GameObject("Model") { layer = 31 };
            _content.transform.SetParent(_pivot, false);
            var transforms = new Dictionary<Transform, Transform> { [origin] = _content.transform };
            foreach (var source in sources)
            {
                if (
                    source == null
                    || !source.enabled
                    || source is not (MeshRenderer or SkinnedMeshRenderer)
                )
                    continue;
                Mesh mesh = null;
                if (source is SkinnedMeshRenderer skin)
                {
                    mesh = new Mesh { name = "Preview baked mesh" };
                    skin.BakeMesh(mesh, true);
                    _meshes.Add(mesh);
                }
                else if (source.TryGetComponent<MeshFilter>(out var filter))
                    mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;
                var go = CopyTransform(source.transform, origin, transforms).gameObject;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                var properties = new MaterialPropertyBlock();
                source.GetPropertyBlock(properties);
                renderer.SetPropertyBlock(properties);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _renderers.Add(renderer);
            }
            if (_renderers.Count > 0)
            {
                _image.enabled = true;
                if (_emptyLabel != null)
                    _emptyLabel.gameObject.SetActive(false);
                Bounds bounds = _renderers[0].bounds;
                foreach (var renderer in _renderers)
                    bounds.Encapsulate(renderer.bounds);
                Vector3 center = _pivot.InverseTransformPoint(bounds.center);
                _content.transform.localPosition -= center;
                _radius = Mathf.Max(.01f, bounds.extents.magnitude);
            }
            foreach (var renderer in _renderers)
                renderer.enabled = false;
            _pivot.localRotation = Quaternion.Euler(0, _yaw, 0);
        }

        // Reproduce the transform chain rather than decomposing a matrix into lossyScale.
        // This preserves weapon meshes beneath rotated, nonuniformly scaled authoring nodes.
        private Transform CopyTransform(
            Transform source,
            Transform origin,
            Dictionary<Transform, Transform> copies
        )
        {
            if (copies.TryGetValue(source, out var existing))
                return existing;
            var node = new GameObject(source.name) { layer = 31 }.transform;
            if (source.IsChildOf(origin))
            {
                node.SetParent(CopyTransform(source.parent, origin, copies), false);
                node.localPosition = source.localPosition;
                node.localRotation = source.localRotation;
                node.localScale = source.localScale;
            }
            else
            {
                node.SetParent(_content.transform, false);
                node.localPosition = origin.InverseTransformPoint(source.position);
                node.localRotation = Quaternion.Inverse(origin.rotation) * source.rotation;
                var scale = source.lossyScale;
                var rootScale = origin.lossyScale;
                node.localScale = new Vector3(
                    scale.x / rootScale.x,
                    scale.y / rootScale.y,
                    scale.z / rootScale.z
                );
            }
            copies[source] = node;
            return node;
        }

        private void LateUpdate()
        {
            if (_camera == null || _renderers.Count == 0)
                return;
            _yaw += _rotationSpeed * Time.unscaledDeltaTime;
            _pivot.localRotation = Quaternion.Euler(0, _yaw, 0);
            var rect = _image.rectTransform.rect;
            _camera.aspect = Mathf.Max(.1f, rect.width / Mathf.Max(1, rect.height));
            _camera.orthographicSize = _radius * 1.12f / Mathf.Min(1, _camera.aspect);
            _camera.transform.localPosition = new Vector3(0, 0, -_radius * 3 - 1);
            _camera.farClipPlane = _radius * 6 + 2;
            var light = _stage.GetComponentInChildren<Light>();
            try
            {
                foreach (var renderer in _renderers)
                    renderer.enabled = true;
                light.enabled = true;
                var request = new UniversalRenderPipeline.SingleCameraRequest
                {
                    destination = _texture,
                };
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(_camera, request);
                else
                    _camera.Render();
            }
            finally
            {
                light.enabled = false;
                foreach (var renderer in _renderers)
                    renderer.enabled = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_allowDrag && eventData.button == PointerEventData.InputButton.Left)
            {
                _dragged = true;
                _yaw -= eventData.delta.x * .4f;
            }
        }

        private void ClearVisuals()
        {
            if (_content != null)
            {
                _content.SetActive(false);
                Destroy(_content);
                _content = null;
            }
            _renderers.Clear();
            foreach (var mesh in _meshes)
                Destroy(mesh);
            _meshes.Clear();
        }

        private void OnDisable()
        {
            _dragged = false;
            ClearVisuals();
            if (_image != null)
            {
                _image.enabled = false;
                _image.texture = null;
            }
            if (_emptyLabel != null)
                _emptyLabel.gameObject.SetActive(false);
            if (_camera != null)
                _camera.targetTexture = null;
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
            if (_stage != null)
                Destroy(_stage);
            _stage = null;
            _texture = null;
        }
    }
}
