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
        public const string MaterialResource = "MapPinAlwaysOnTop"; // 壁に隠されず常に手前に描く
        private const float HoverHeight = 0.5f; // 先端が指定位置より浮く高さ
        private const float Scale = 2f; // 画像(高さ1.5)の拡大率。大きいほど目立つ
        private const float FullSizeDistance = 15f; // これより遠いと、見失わないよう大きくする
        private const float MaxDistanceBoost = 3f; // 大きくする倍率の上限

        [SerializeField]
        private float _spinDegreesPerSecond = 90f;

        [SerializeField]
        private float _bobAmplitude = 0.15f;

        [SerializeField]
        private float _bobHertz = 0.6f;

        private Vector3 _basePosition;
        private Transform _face;
        private MapPinPathLine _pathLine;

        private void Awake() => _basePosition = transform.position;

        private void Update()
        {
            transform.Rotate(0f, _spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            var offset = Mathf.Sin(Time.time * _bobHertz * Mathf.PI * 2f) * _bobAmplitude;
            transform.position = _basePosition + Vector3.up * offset;
            ApplyDistanceScale();
            _pathLine?.Tick(_basePosition);
        }

        private void OnDestroy() => _pathLine?.Destroy();

        /// <summary>ピンと地面の線をまとめて消す(エディットモードでも OnDestroy が走らないため明示する)。</summary>
        public void Remove()
        {
            _pathLine?.Destroy();
            _pathLine = null;
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        /// <summary>プレイヤーからピンまでの道筋を表す地面の線が出ているか。</summary>
        public bool PathLineVisible => _pathLine != null && _pathLine.Visible;

        // 遠くでは小さくて見えなくなるので、カメラから離れるほど大きくする(上限あり)。
        private void ApplyDistanceScale()
        {
            var cam = Camera.main;
            if (_face == null || cam == null)
                return;
            var distance = Vector3.Distance(cam.transform.position, transform.position);
            var boost = Mathf.Clamp(distance / FullSizeDistance, 1f, MaxDistanceBoost);
            _face.localScale = Vector3.one * (Scale * boost);
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
            // 壁の後ろでも見えるよう、深度テストを無視する材質にし、隠れ判定でも描画から外されないようにする。
            var material = Resources.Load<Material>(MaterialResource);
            if (material != null)
                renderer.sharedMaterial = material;
            else
                Debug.LogWarning($"[MapPin] Resources/{MaterialResource} が見つかりません。");
            renderer.allowOcclusionWhenDynamic = false;

            var pin = root.AddComponent<MapPin>();
            pin._face = face.transform;
            pin._pathLine = new MapPinPathLine();
            return pin;
        }
    }
}
