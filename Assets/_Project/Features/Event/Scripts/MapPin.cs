using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// マップピン。地面と水平に寝かせた 2D 画像を、縦軸まわりに回しながら上下に浮かせる。
    /// 子に画像を持ち、このオブジェクト自身を回す・上下させる(基準位置は生成時の位置)。
    /// </summary>
    public sealed class MapPin : MonoBehaviour
    {
        public const string SpriteResource = "EventMapPin";

        [SerializeField]
        private float _spinDegreesPerSecond = 90f;

        [SerializeField]
        private float _bobAmplitude = 0.15f;

        [SerializeField]
        private float _bobHertz = 0.6f;

        private Vector3 _basePosition;

        private void Awake() => _basePosition = transform.position;

        private void Update()
        {
            transform.Rotate(0f, _spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            var offset = Mathf.Sin(Time.time * _bobHertz * Mathf.PI * 2f) * _bobAmplitude;
            transform.position = _basePosition + Vector3.up * offset;
        }

        /// <summary>指定位置にピンを作る。画像が無ければ null。</summary>
        public static MapPin Create(Vector3 position, Transform parent = null)
        {
            var sprite = Resources.Load<Sprite>(SpriteResource);
            if (sprite == null)
            {
                Debug.LogWarning($"[MapPin] Resources/{SpriteResource} が見つかりません。");
                return null;
            }

            var root = new GameObject("MapPin");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            var face = new GameObject("Face");
            face.transform.SetParent(root.transform, false);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 地面と水平(表が上)
            face.transform.localScale = Vector3.one * 1.2f;
            var renderer = face.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return root.AddComponent<MapPin>();
        }
    }
}
