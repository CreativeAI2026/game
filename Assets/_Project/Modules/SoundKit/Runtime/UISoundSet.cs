using UnityEngine;

namespace SoundKit
{
    /// <summary>
    /// どの画面でも共通の UI 音（選択・決定・戻る）。ボタンごとに音を割り当てさせないため、1か所にまとめる。
    /// ファイルはどこかの Resources フォルダ直下に UISoundSet という名前で置く（シーンをまたいで static から読むため）。
    /// 画面ごとに違う音（知らせ・結果画面など）は、その画面のスクリプトが SoundCue を持つ。
    /// </summary>
    [CreateAssetMenu(menuName = "SoundKit/UISoundSet（Resources 直下に置く）", fileName = ResourcePath)]
    public class UISoundSet : ScriptableObject
    {
        private const string ResourcePath = "UISoundSet";

        [Tooltip("カーソルを別のボタンへ動かしたとき")]
        [SerializeField]
        private SoundCue _select;

        [Tooltip("ボタンを押したとき")]
        [SerializeField]
        private SoundCue _decide;

        [Tooltip("戻る・閉じる・キャンセルしたとき")]
        [SerializeField]
        private SoundCue _back;

        private static UISoundSet _instance;
        private static bool _loadAttempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _loadAttempted = false;
        }

        /// <summary>読み込めなかった場合は null（エラーは1度だけ出す）。</summary>
        public static UISoundSet Instance
        {
            get
            {
                if (_loadAttempted)
                    return _instance;

                _loadAttempted = true;
                _instance = Resources.Load<UISoundSet>(ResourcePath);
                if (_instance == null)
                    Debug.LogError(
                        $"[音] Resources/{ResourcePath}.asset が見つからないため、UI の選択・決定・戻るの音が鳴らない。"
                    );
                return _instance;
            }
        }

        public SoundCue Select => _select;
        public SoundCue Decide => _decide;
        public SoundCue Back => _back;
    }
}
