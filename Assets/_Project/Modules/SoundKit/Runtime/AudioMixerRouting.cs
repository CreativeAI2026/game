using UnityEngine;
using UnityEngine.Audio;

namespace SoundKit
{
    /// <summary>
    /// 音の種類ごとの流し先（ミキサーグループ）と、音量を変えるためにミキサーで公開した値の名前をまとめたデータ。
    ///
    /// 鳴らす側（WorldSoundPlayer・BgmPlayer など）はシーンをまたいで使う static なため、
    /// Resources から1つだけ読み込んで参照する。ファイルはどこかの Resources フォルダ直下に AudioMixerRouting という名前で置く。
    /// 未設定でも音自体は鳴る（ミキサーを通らないだけ）。設定漏れは初回にエラーで知らせる。
    /// </summary>
    [CreateAssetMenu(
        menuName = "SoundKit/AudioMixerRouting（Resources 直下に置く）",
        fileName = ResourcePath
    )]
    public class AudioMixerRouting : ScriptableObject
    {
        private const string ResourcePath = "AudioMixerRouting";

        [Tooltip("音量を変えるミキサー")]
        [SerializeField]
        private AudioMixer _mixer;

        [Header("流し先のグループ（ミキサーを展開すると出てくる子要素を入れる）")]
        [SerializeField]
        private AudioMixerGroup _bgmGroup;

        [SerializeField]
        private AudioMixerGroup _ambientGroup;

        [SerializeField]
        private AudioMixerGroup _effectGroup;

        [SerializeField]
        private AudioMixerGroup _uiGroup;

        [Header("ミキサーで公開した音量の名前（Exposed Parameters と一字一句同じにする）")]
        [SerializeField]
        private string _masterVolumeName = "MasterVolume";

        [SerializeField]
        private string _bgmVolumeName = "BgmVolume";

        [SerializeField]
        private string _ambientVolumeName = "AmbientVolume";

        [SerializeField]
        private string _effectVolumeName = "EffectVolume";

        [SerializeField]
        private string _uiVolumeName = "UIVolume";

        private static AudioMixerRouting _instance;
        private static bool _loadAttempted;
        private static bool _missingGroupReported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _loadAttempted = false;
            _missingGroupReported = false;
        }

        /// <summary>読み込めなかった場合は null（エラーは1度だけ出す）。</summary>
        public static AudioMixerRouting Instance
        {
            get
            {
                if (_loadAttempted)
                    return _instance;

                _loadAttempted = true;
                _instance = Resources.Load<AudioMixerRouting>(ResourcePath);
                if (_instance == null)
                    Debug.LogError(
                        $"[音] Resources/{ResourcePath}.asset が見つからない。音はミキサーを通らず、音量設定が効かない。"
                    );
                return _instance;
            }
        }

        public AudioMixer Mixer => _mixer;

        /// <summary>
        /// 指定した種類の流し先。未設定なら null を返す（AudioSource は null だとミキサーを通らずそのまま鳴る）。
        /// </summary>
        public static AudioMixerGroup GetGroup(SoundCategory category)
        {
            var routing = Instance;
            if (routing == null)
                return null;

            AudioMixerGroup group = category switch
            {
                SoundCategory.Bgm => routing._bgmGroup,
                SoundCategory.Ambient => routing._ambientGroup,
                SoundCategory.Effect => routing._effectGroup,
                SoundCategory.UI => routing._uiGroup,
                _ => null,
            };

            if (group == null && !_missingGroupReported)
            {
                _missingGroupReported = true;
                Debug.LogError(
                    $"[音] AudioMixerRouting の {category} のグループが未設定。その種類の音は音量設定が効かない。",
                    routing
                );
            }
            return group;
        }

        public string GetVolumeParameterName(VolumeChannel channel) =>
            channel switch
            {
                VolumeChannel.Master => _masterVolumeName,
                VolumeChannel.Bgm => _bgmVolumeName,
                VolumeChannel.Ambient => _ambientVolumeName,
                VolumeChannel.Effect => _effectVolumeName,
                VolumeChannel.UI => _uiVolumeName,
                _ => null,
            };
    }
}
