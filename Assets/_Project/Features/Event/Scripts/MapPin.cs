using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// マップピン。穴の空いたしずく形の 2D 画像を縦に立て、縦軸まわりに横回転させながら上下に浮かせる。
    /// 子に画像を持ち、このオブジェクト自身を回す・上下させる(基準位置は生成時の位置)。
    /// 会話・戦闘・メニュー中は隠す。地面の道筋の線は、出ているピンのうちプレイヤーに一番近い1つだけに出す。
    /// </summary>
    public sealed class MapPin : MonoBehaviour
    {
        private const string SpriteResource = "EventMapPin";
        private const string MaterialResource = "MapPinAlwaysOnTop"; // 壁に隠されず常に手前に描く
        private const float HoverHeight = 0.5f; // 先端が指定位置より浮く高さ
        private const float SpriteHeight = 1.5f; // 画像の高さ(ワールド長、拡大前)
        private const float Scale = 2f; // 画像の拡大率。大きいほど目立つ
        private const float FullSizeDistance = 15f; // これより遠いと、見失わないよう大きくする
        private const float MaxDistanceBoost = 3f; // 大きくする倍率の上限
        private const float SpinDegreesPerSecond = 90f;
        private const float BobAmplitude = 0.15f;
        private const float BobHertz = 0.6f;
        private const float FadeSeconds = 0.25f; // 出入りの透明度が変わりきるまで

        private static readonly List<MapPin> s_active = new();
        private static Transform s_player;

        private Vector3 _basePosition;
        private Transform _face;
        private SpriteRenderer _renderer;
        private float _alpha; // 画像の透明度(0 で見えない)
        private MapPinPathLine _pathLine;

        /// <summary>今ある(作られて消えていない)ピン。HUD が距離や方向の表示に使う。</summary>
        public static IReadOnlyList<MapPin> Active => s_active;

        /// <summary>プレイヤー(Player タグ)。見つからなければ null。</summary>
        public static Transform PlayerTransform
        {
            get
            {
                if (s_player == null)
                {
                    var go = GameObject.FindGameObjectWithTag("Player");
                    s_player = go != null ? go.transform : null;
                }
                return s_player;
            }
        }

        /// <summary>ピンの先端の位置(浮き沈みを除く)。</summary>
        public Vector3 Anchor => _basePosition;

        /// <summary>ピンの頭の少し上。距離の表示を出す位置に使う。</summary>
        public Vector3 Top =>
            _basePosition
            + Vector3.up
                * (
                    HoverHeight + (_face != null ? _face.localScale.y : Scale) * SpriteHeight + 0.3f
                );

        /// <summary>見せている最中か。会話・戦闘・メニュー中は false。</summary>
        public bool Shown { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            s_active.Clear();
            s_player = null;
        }

        private void Awake() => _basePosition = transform.position;

        private void OnEnable() => s_active.Add(this);

        private void OnDisable() => s_active.Remove(this);

        private void OnDestroy() => _pathLine?.Destroy();

        private void Update()
        {
            Shown = !IsBusy();
            UpdateFade();
            if (!Shown)
            {
                _pathLine?.Tick(_basePosition, false);
                if (_alpha > 0f)
                    Animate(); // 消える間も回して、止まって見えないようにする
                return;
            }

            Animate();
            _pathLine?.Tick(_basePosition, IsClosestShownPin());
        }

        private void Animate()
        {
            transform.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            var offset = Mathf.Sin(Time.time * BobHertz * Mathf.PI * 2f) * BobAmplitude;
            transform.position = _basePosition + Vector3.up * offset;
            ApplyDistanceScale();
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

        /// <summary>ピンと地面の線をまとめて消す(エディットモードでは OnDestroy が走らないため明示する)。</summary>
        public void Remove()
        {
            _pathLine?.Destroy();
            _pathLine = null;
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        // 会話・戦闘・メニューの最中は、画面の邪魔になるので隠す。
        private static bool IsBusy()
        {
            var mode = GameModeManager.Instance;
            return EventPlaybackService.IsPlaying
                || UiPanelService.IsOpen
                || (mode != null && mode.CurrentMode == GameMode.Battle);
        }

        // 見せているピンのうち、プレイヤーに一番近いのが自分か(同じ近さなら先に作られたほうを優先)。
        private bool IsClosestShownPin()
        {
            var player = PlayerTransform;
            if (player == null)
                return true;

            var mine = (_basePosition - player.position).sqrMagnitude;
            foreach (var other in s_active)
            {
                if (other == this || !other.Shown)
                    continue;
                var theirs = (other._basePosition - player.position).sqrMagnitude;
                if (
                    theirs < mine
                    || (theirs == mine && s_active.IndexOf(other) < s_active.IndexOf(this))
                )
                    return false;
            }
            return true;
        }

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
            pin._renderer = renderer;
            renderer.color = new Color(1f, 1f, 1f, 0f); // 出始めは透明から
            pin._pathLine = new MapPinPathLine();
            return pin;
        }
    }
}
