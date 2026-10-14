using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 武器などを実行時にモデルのソケット(<see cref="PlayerModel.GetSocket"/>)へ取り付ける。
    ///
    /// 武器はロジック層(Player/Weapons の下)に置いておき、起動時にモデルのボーン下のソケットへ移る。
    /// そのためモデルを差し替えても武器のオブジェクトや参照は消えない。
    /// 持ち位置・向きはソケット側(モデル層)に持つので、モデルごとに合わせ直せる。
    ///
    /// 編集中の位置合わせはコンポーネントの ⋮ メニューから行う:
    /// 「プレビュー: ソケットの位置に移動」で手に持った位置に表示し、Scene ビューで動かしてから
    /// 「プレビュー: 現在の位置をソケットに保存」でソケットへ書き戻す。
    /// </summary>
    public class PlayerSocketAttachment : MonoBehaviour
    {
        [Tooltip("取り付け先のソケット")]
        [SerializeField]
        private PlayerSocketId _socket;

        private void Awake()
        {
            Transform socket = PlayerModel.Of(this).GetSocket(_socket);
            transform.SetParent(socket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

#if UNITY_EDITOR
        [ContextMenu("プレビュー: ソケットの位置に移動")]
        private void PreviewAtSocket()
        {
            Transform socket = PlayerModel.Of(this).GetSocket(_socket);
            Undo.RecordObject(transform, "Preview At Socket");
            transform.SetPositionAndRotation(socket.position, socket.rotation);
        }

        [ContextMenu("プレビュー: 現在の位置をソケットに保存")]
        private void SaveCurrentPoseToSocket()
        {
            Transform socket = PlayerModel.Of(this).GetSocket(_socket);
            Undo.RecordObject(socket, "Save Pose To Socket");
            socket.SetPositionAndRotation(transform.position, transform.rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
        }
#endif
    }
}
