using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// マップピン。穴の空いたしずく形の小さい 2D 画像を、目的地の少し上で常にカメラへ向けて浮かせる。
    /// 近くでは一定の大きさ、遠くでは画面上の大きさが変わらないよう拡大する(小さすぎて見失わないため)。
    /// 子に画像を持ち、このオブジェクト自身を上下させる(基準位置は生成時の位置)。
    /// 会話・戦闘・メニュー中は隠す。
    /// </summary>
    public sealed class MapPin : MonoBehaviour
    {
        private const float HoverHeight = 0.5f; // 先端が指定位置より浮く高さ
        private const float SpriteHeight = 1.5f; // 画像の高さ(ワールド長、拡大前)
        private const float Scale = 0.6f; // 画像の拡大率。小さいマークにする
        private const float FullSizeDistance = 10f; // これより遠いと、画面上の大きさを保つよう拡大する
        private const float BobAmplitude = 0.1f;
        private const float BobHertz = 0.6f;
        private const float FadeSeconds = 0.25f; // 出入りの透明度が変わりきるまで

        private static readonly List<MapPin> s_active = new();

        private Vector3 _basePosition;
        private Transform _face;
        private SpriteRenderer _renderer;
        private float _alpha; // 画像の透明度(0 で見えない)

        /// <summary>今ある(作られて消えていない)ピン。HUD が距離や方向の表示に使う。</summary>
        public static IReadOnlyList<MapPin> Active => s_active;

        /// <summary>プレイヤー。居なければ null。</summary>
        public static Transform PlayerTransform
        {
            get
            {
                var player = PlayerRegistry.Current;
                return player != null ? player.transform : null;
            }
        }

        /// <summary>ピンの先端の位置(浮き沈みを除く)。</summary>
        public Vector3 Anchor => _basePosition;

        /// <summary>ピンの頭の少し上。距離の表示を出す位置に使う。</summary>
        public Vector3 Top =>
            _basePosition + Vector3.up * (HoverHeight + Scale * SpriteHeight + 0.3f);

        /// <summary>見せている最中か。会話・戦闘・メニュー中は false。</summary>
        public bool Shown { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            s_active.Clear();
        }

        private void Awake() => _basePosition = transform.position;

        private void OnEnable() => s_active.Add(this);

        private void OnDisable() => s_active.Remove(this);

        private void Update()
        {
            Shown = !IsBusy();
            UpdateFade();
            if (Shown || _alpha > 0f)
                Bob(); // 消える間も浮かせて、止まって見えないようにする
        }

        // カメラが動いた後で、向きと大きさを合わせる。
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (_face == null || cam == null)
                return;
            _face.rotation = cam.transform.rotation;
            var distance = Vector3.Distance(cam.transform.position, transform.position);
            _face.localScale = Vector3.one * (Scale * Mathf.Max(1f, distance / FullSizeDistance));
        }

        private void Bob()
        {
            var offset = Mathf.Sin(Time.time * BobHertz * Mathf.PI * 2f) * BobAmplitude;
            transform.position = _basePosition + Vector3.up * offset;
        }

        // 出し入れで急に現れたり消えたりしないよう、画像を透明度で出し入れする。
        private void UpdateFade()
        {
            _alpha = Mathf.MoveTowards(_alpha, Shown ? 1f : 0f, Time.deltaTime / FadeSeconds);
            if (_face == null)
                return;
            _face.gameObject.SetActive(Shown || _alpha > 0f);
            if (_renderer != null)
                _renderer.color = new Color(1f, 1f, 1f, _alpha);
        }

        /// <summary>ピンを消す。</summary>
        public void Remove()
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        // 会話・戦闘・メニューの最中は、画面の邪魔になるので隠す。
        private static bool IsBusy()
        {
            return !EventPlaybackService.IsFreeRoaming || UIPanelService.IsOpen;
        }

        /// <summary>指定位置にピンを作る。画像が無ければ null。</summary>
        public static MapPin Create(Vector3 position, Transform parent = null)
        {
            var visuals = MapPinVisuals.Instance;
            if (visuals == null || visuals.PinSprite == null)
                return null;

            var root = new GameObject("MapPin");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            var face = new GameObject("Face");
            face.transform.SetParent(root.transform, false);
            // 先端が下。画像の先端が基準なので、HoverHeight だけ地面から浮かせる。
            face.transform.localPosition = Vector3.up * HoverHeight;
            face.transform.localScale = Vector3.one * Scale;
            var renderer = face.AddComponent<SpriteRenderer>();
            renderer.sprite = visuals.PinSprite;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 壁の後ろでも見えるよう、深度テストを無視する材質にし、隠れ判定でも描画から外されないようにする。
            if (visuals.PinMaterial != null)
                renderer.sharedMaterial = visuals.PinMaterial;
            renderer.allowOcclusionWhenDynamic = false;

            var pin = root.AddComponent<MapPin>();
            pin._face = face.transform;
            pin._renderer = renderer;
            renderer.color = new Color(1f, 1f, 1f, 0f); // 出始めは透明から
            return pin;
        }
    }
}
