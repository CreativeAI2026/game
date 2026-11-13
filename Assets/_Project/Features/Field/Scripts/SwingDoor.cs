using UnityEngine;
using UnityEngine.Serialization;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 蝶番で回って開く押し戸。扉板(Leaf)だけを縦の軸まわりに回し、廊下側から室内側へ押し開ける。
    /// 扉ルートのローカル +Z が廊下側(サイン面)、-Z が室内側という配置を前提にする。
    /// glb は固定側と Leaf を別オブジェクトで書き出す前提で、Leaf が見つからなければ警告して何もしない。
    /// 蝶番の位置は扉板の実寸から決める(開く側の面の、蝶番側の縦の辺)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwingDoor : MonoBehaviour
    {
        public const string DefaultLeafName = "Leaf";
        private const float DefaultOpenAngle = 90f;

        [Tooltip("動く扉板。未設定なら子から \"Leaf\" を名前で探す。")]
        [SerializeField]
        private Transform _leaf;

        [Tooltip("開き切ったときの角度(度)。0 なら 90°。")]
        [SerializeField, Range(0f, 180f)]
        private float _openAngle = DefaultOpenAngle;

        [Tooltip("ON で蝶番を +X 側(制御パネルと反対側)に付ける。")]
        [SerializeField, FormerlySerializedAs("_slideToPositiveX")]
        private bool _hingeAtPositiveX = true;

        [Tooltip("ON で +Z 側(廊下側)へ開く。OFF なら -Z 側(室内側)へ押し開ける。")]
        [SerializeField]
        private bool _openTowardPositiveZ;

        [Tooltip("開閉にかける秒数。")]
        [SerializeField, Min(0.01f)]
        private float _duration = 0.45f;

        private Vector3 _closedPos; // 扉ルートから見た扉板の位置
        private Quaternion _closedRot; // 扉ルートから見た扉板の向き
        private Vector3 _hinge; // 扉ルートから見た蝶番の位置
        private float _signedAngle;
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
                    $"[SwingDoor] {name}: 扉板 \"{DefaultLeafName}\" が見つかりません。"
                        + "扉板が結合された glb になっていないか確認してください。",
                    this
                );
                enabled = false;
                return;
            }

            _closedPos = transform.InverseTransformPoint(_leaf.position);
            _closedRot = Quaternion.Inverse(transform.rotation) * _leaf.rotation;
            _hinge = FindHinge(transform, _leaf, _hingeAtPositiveX, _openTowardPositiveZ);

            // 後から足した項目は既存シーンだと 0 で読まれるので既定値へ戻す
            float angle = _openAngle > 0f ? _openAngle : DefaultOpenAngle;
            // Y 軸まわりの正の回転は +X を -Z へ向ける。蝶番から見た扉板の向き(±X)を、開く側(±Z)へ回す。
            float sign = (_hingeAtPositiveX ? -1f : 1f) * (_openTowardPositiveZ ? -1f : 1f);
            _signedAngle = sign * angle;
            _ready = true;
            // 閉じて止まっている間は Update を回さない(開け閉めの指示で動かし始める)
            enabled = false;
        }

        /// <summary>子孫から扉板を名前で探す(glb のノード名がそのまま子になる)。</summary>
        public static Transform FindLeaf(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == DefaultLeafName)
                    return t;
            return null;
        }

        /// <summary>
        /// 扉ルートから見た蝶番の位置。扉板の外形の、蝶番側(±X)・開く側(±Z)の角にする
        /// (開いた扉板が枠や袖壁へめり込まない)。レンダラーが無ければ扉板の原点。
        /// </summary>
        private static Vector3 FindHinge(
            Transform root,
            Transform leaf,
            bool positiveX,
            bool positiveZ
        )
        {
            var renderers = leaf.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return root.InverseTransformPoint(leaf.position);

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var r in renderers)
            {
                var b = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z
                    );
                    var p = root.InverseTransformPoint(r.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            return new Vector3(positiveX ? max.x : min.x, 0f, positiveZ ? max.z : min.z);
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void Open() => SetOpen(true);

        public void Close() => SetOpen(false);

        public void SetOpen(bool open)
        {
            if (!_ready)
                return;
            IsOpen = open;
            enabled = true;
        }

        /// <summary>開閉の途中経過を進める。テストから任意の dt で回せるよう分けてある。</summary>
        public void Step(float deltaTime)
        {
            if (!_ready)
                return;
            float target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(_progress, target))
            {
                // 止まったら Update を止める
                enabled = false;
                return;
            }

            _progress = Mathf.MoveTowards(_progress, target, deltaTime / _duration);
            // 端で滑らかに止まるよう smoothstep(Animator を足さずに済ませる)。
            float eased = _progress * _progress * (3f - 2f * _progress);
            var turn = Quaternion.AngleAxis(_signedAngle * eased, Vector3.up);
            _leaf.SetPositionAndRotation(
                transform.TransformPoint(_hinge + turn * (_closedPos - _hinge)),
                transform.rotation * turn * _closedRot
            );
        }

        private void Update() => Step(Time.deltaTime);
    }
}
