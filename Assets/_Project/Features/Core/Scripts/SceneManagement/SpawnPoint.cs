using UnityEngine;

namespace CreativeAI.Core.SceneManagement
{
    /// <summary>
    /// フィールドの到着位置の目印。プレイヤーリグはシーンに埋め込まず持ち越されるので
    /// (PlayerImplementation.md)、到着時にどこへ置くかをシーン上のこのオブジェクトで決める。
    /// ID はそのシーン内で一意。向きは GameObject の回転をそのまま使う。
    /// 指定 ID が見つからない場合は原点へフォールバックして警告する(遷移は失敗させない)。
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField]
        [Tooltip(
            "到着位置の ID。そのシーン内で一意。Start Spawn / Dest Spawn にこの文字列を入れる"
        )]
        private string _id = "start";

        public string Id => _id;

        /// <summary>ロード済みのシーンから ID で探す。無ければ null。</summary>
        public static SpawnPoint Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            SpawnPoint hit = null;
            foreach (var point in FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
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

            var position = point != null ? point.transform.position : Vector3.zero;
            var rotation = point != null ? point.transform.rotation : Quaternion.identity;

            var controller = target.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            target.transform.SetPositionAndRotation(position, rotation);
            if (controller != null)
                controller.enabled = true;

            return point != null;
        }
    }
}
