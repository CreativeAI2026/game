using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// マップピン。穴の空いたしずく形の 2D 画像を縦に立て、縦軸まわりに横回転させながら上下に浮かせる。
    /// 子に画像を持ち、このオブジェクト自身を回す・上下させる(基準位置は生成時の位置)。
    /// </summary>
    public sealed class MapPin : MonoBehaviour
    {
        public const string SpriteResource = "EventMapPin";
        private const float HoverHeight = 0.5f; // 先端が指定位置より浮く高さ
        private const float Scale = 2f; // 画像(高さ1.5)の拡大率。大きいほど目立つ

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
            // 縦に立てる(先端が下)。画像の先端が基準なので、HoverHeight だけ地面から浮かせる。
            face.transform.localPosition = Vector3.up * HoverHeight;
            face.transform.localScale = Vector3.one * Scale;
            var renderer = face.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return root.AddComponent<MapPin>();
        }
    }
}
