using UnityEditor;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// 音素材の名前変更・移動を見張り、素材のパスで管理している保存ファイルを新しいパスへ移す。
    ///   ・音量そろえ（AudioLoudnessNormalizer）の、そろえる前の元の素材
    ///   ・音声エディタ（AudioEditStore）の、上書き前のファイルと編集の記録
    /// 移さないと旧パスに取り残され、元の素材から作り直せなくなる（劣化が重なる）・「元に戻す」が使えなくなる。
    /// 削除された素材の分は消さない（git などで素材を戻したときに使えるようにするため。
    /// 中身が違うファイルに対しては使われないので、残しても害は無い）。
    /// </summary>
    public class AudioBackupFollower : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            for (int i = 0; i < movedAssets.Length; i++)
            {
                AudioLoudnessNormalizer.MoveOriginal(movedFromAssetPaths[i], movedAssets[i]);
                AudioEditStore.MoveRecord(movedFromAssetPaths[i], movedAssets[i]);
            }
        }
    }
}
