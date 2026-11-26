using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// フィールドの到着位置の目印(ID はシーン内で一意、向きは回転を使う)。持ち越したプレイヤーリグをここに置く。
    /// ID が見つからなければ原点へフォールバックして警告する。
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        /// <summary>「はじめる」で置く開始位置の ID。</summary>
        public const string StartId = "start";

        [SerializeField]
        [Tooltip("到着位置の ID。そのシーン内で一意。「はじめる」の開始位置は start")]
        private string _id = StartId;

        /// <summary>ロード済みのシーンから ID で探す。無ければ null。</summary>
        public static SpawnPoint Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            SpawnPoint hit = null;
            foreach (var point in FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude))
            {
                if (point._id != id)
                    continue;

                if (hit != null)
                {
                    Debug.LogWarning(
                        $"[SpawnPoint] ID '{id}' が複数あります({hit.name} / {point.name})。"
                            + $"{hit.name} を使います。ID はシーン内で一意にしてください。"
                    );
                    continue;
                }
                hit = point;
            }
            return hit;
        }

        /// <summary>
        /// 指定 ID の位置・向きへ置く。見つからなければ原点に置いて警告し false を返す。
        /// CharacterController は自前で位置を保持するので、当てが効かないよう一旦切ってから動かす。
        /// </summary>
        public static bool Place(GameObject target, string id)
        {
            if (target == null)
                return false;

            var point = Find(id);
            if (point == null)
                Debug.LogWarning(
                    $"[SpawnPoint] ID '{id}' が見つかりません。原点に配置します。"
                        + "シーンに SpawnPoint を置いて ID を合わせてください。"
                );

            PlaceAt(
                target,
                point != null ? point.transform.position : Vector3.zero,
                point != null ? point.transform.rotation : Quaternion.identity
            );
            return point != null;
        }

        /// <summary>
        /// 指定の位置・向きへ置く。プレイヤーを動かすときは必ずこれを通す。
        /// CharacterController は自前で位置を保持していて、そのまま書き換えると次の Move で元の位置へ戻ることがあるので、
        /// 一旦切ってから動かす。
        /// </summary>
        public static void PlaceAt(GameObject target, Vector3 position, Quaternion rotation)
        {
            bool hasController = target.TryGetComponent(out CharacterController controller);
            if (hasController)
                controller.enabled = false;
            target.transform.SetPositionAndRotation(position, rotation);
            if (hasController)
                controller.enabled = true;
        }
    }
}
