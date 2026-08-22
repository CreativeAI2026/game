using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 横に引いて開く扉。扉板(Leaf)だけを自分のローカル X 方向へ滑らせる。
    ///
    /// 扉の glb は「壁に固定される側(ケーシング + 制御パネル)」と「動く側(Leaf)」を
    /// <b>別オブジェクトのまま</b>書き出してある(blender-space の classroom_door.py 等)。
    /// 1メッシュに結合されていると開けられないので、Leaf が見つからないときは警告を出して
    /// 何もしない(モデルを差し替えたときに気付けるように)。
    ///
    /// マップの壁は1マス4u厚なので、扉板は袖壁の内側へ引き込めば完全に隠れる。
    /// 移動量は扉板の実寸から自動で決める(<see cref="_slideDistance"/> = 0 のとき)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlidingDoor : MonoBehaviour
    {
        public const string DefaultLeafName = "Leaf";

        [Tooltip("動く扉板。未設定なら子から \"Leaf\" を名前で探す。")]
        [SerializeField]
        private Transform _leaf;

        [Tooltip("開いたときに扉板を動かす距離(u)。0 なら扉板の幅 + 余白から自動。")]
        [SerializeField, Min(0f)]
        private float _slideDistance;

        [Tooltip("自動計算のときに扉板の幅へ足す余白(u)。袖壁の内側へ確実に隠すため。")]
        [SerializeField, Min(0f)]
        private float _slideMargin = 0.15f;

        [Tooltip("ON で +X 側(制御パネルと反対側)へ引き込む。")]
        [SerializeField]
        private bool _slideToPositiveX = true;

        [Tooltip("開閉にかける秒数。")]
        [SerializeField, Min(0.01f)]
        private float _duration = 0.45f;

        private Vector3 _closedLocalPos;
        private float _progress; // 0 = 閉、1 = 開
        private bool _ready;

        /// <summary>開いている(開き切っていなくても、開ける指示が出ていれば true)。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>動く扉板。生成ツール・テストからの確認用。</summary>
        public Transform Leaf => _leaf;

        private void Awake()
        {
            if (_leaf == null)
                _leaf = FindLeaf(transform);
            if (_leaf == null)
            {
                Debug.LogWarning(
                    $"[SlidingDoor] {name}: 扉板 \"{DefaultLeafName}\" が見つかりません。"
                        + "扉板が結合された glb になっていないか確認してください。",
                    this
                );
                return;
            }

            _closedLocalPos = _leaf.localPosition;
            if (_slideDistance <= 0f)
                _slideDistance = MeasureLeafWidth(_leaf) + _slideMargin;
            _ready = true;
        }

        /// <summary>子孫から扉板を名前で探す(glb のノード名がそのまま子になる)。</summary>
        public static Transform FindLeaf(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == DefaultLeafName)
                    return t;
            return null;
        }

        /// <summary>扉板の X 幅(u)。レンダラーが無い場合は 0。</summary>
        private static float MeasureLeafWidth(Transform leaf)
        {
            var renderers = leaf.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return 0f;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            // bounds はワールド軸なので、扉が南北向き(90°回転)なら Z 側が幅になる。
            return Mathf.Max(bounds.size.x, bounds.size.z);
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void Open() => SetOpen(true);

        public void Close() => SetOpen(false);

        public void SetOpen(bool open)
        {
            if (!_ready)
                return;
            IsOpen = open;
        }

        /// <summary>開閉の途中経過を進める。テストから任意の dt で回せるよう分けてある。</summary>
        public void Step(float deltaTime)
        {
            if (!_ready)
                return;
            float target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(_progress, target))
                return;

            _progress = Mathf.MoveTowards(_progress, target, deltaTime / _duration);
            // 端で滑らかに止まるよう smoothstep(Animator を足さずに済ませる)。
            float eased = _progress * _progress * (3f - 2f * _progress);
            float dir = _slideToPositiveX ? 1f : -1f;
            _leaf.localPosition =
                _closedLocalPos + new Vector3(dir * _slideDistance * eased, 0f, 0f);
        }

        private void Update() => Step(Time.deltaTime);
    }
}
