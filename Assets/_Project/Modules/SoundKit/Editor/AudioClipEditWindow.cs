using System.IO;
using UnityEditor;
using UnityEngine;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// 音声エディタ。音素材の前後の切り取り（無音の自動検出つき）・音程・再生速度・切り取った端のフェードを、
    /// 波形を見ながら決めて試聴し、素材を上書きする。加工は ffmpeg で行う。
    ///
    /// 上書きする前のファイルはプロジェクト直下の AudioEditBackup に保存し、「元に戻す」で戻せる。
    /// 保存した後に開き直すと、編集前の素材に前回の編集内容を掛け直した状態から再開する（劣化を重ねないため）。
    /// </summary>
    public class AudioClipEditWindow : EditorWindow
    {
        private const string Title = "音声エディタ";
        private const float WaveformHeight = 140f;

        private readonly AudioWaveformView _waveform = new AudioWaveformView();
        private readonly AudioPreviewPlayer _player = new AudioPreviewPlayer();

        // コンパイル後にウィンドウが作り直されても、開いていた素材を覚えておくため保存する
        [SerializeField]
        private AudioClip _clip;

        private AudioClipEditing.Source _source;
        private AudioEditSettings _settings;
        private string _savedSettingsJson;
        private string _error;

        // 編集後の試聴データ。編集内容が変わるまで使い回す（毎回 ffmpeg を呼ぶと待たされるため）
        private float[] _previewSamples;
        private string _previewSettingsJson;
        private bool _isPlayingEdited;

        private Vector2 _scroll;

        [MenuItem("Tools/SoundKit/音声エディタ")]
        private static void Open() => GetWindow<AudioClipEditWindow>(Title);

        [MenuItem("Assets/SoundKit/音声エディタで開く")]
        private static void OpenSelected()
        {
            var window = GetWindow<AudioClipEditWindow>(Title);
            window.TrySwitchTo(Selection.activeObject as AudioClip);
        }

        [MenuItem("Assets/SoundKit/音声エディタで開く", true)]
        private static bool CanOpenSelected() =>
            Selection.activeObject is AudioClip clip
            && AudioClipEditing.IsSupported(AssetDatabase.GetAssetPath(clip));

        private bool HasUnsavedChanges =>
            _settings != null && JsonUtility.ToJson(_settings) != _savedSettingsJson;

        private void OnEnable()
        {
            // コンパイル後などでウィンドウが作り直されたときは、開いていた素材を読み直す
            if (_clip != null)
                Load(_clip);
        }

        private void OnDisable() => _player.Dispose();

        /// <summary>
        /// 開いている素材の名前変更・移動・削除に追従する。読み込んだときのパスのまま ffmpeg に渡すと、
        /// 見つからずに失敗するため（日本語を含むパスだと ffmpeg は「Illegal byte sequence」と表示する）。
        /// </summary>
        private void OnProjectChange()
        {
            if (_source == null)
                return;

            if (_clip == null)
            {
                // 削除された
                _player.Stop();
                _source = null;
                _settings = null;
                _previewSamples = null;
                _error = null;
                Repaint();
                return;
            }

            if (AssetDatabase.GetAssetPath(_clip) == _source.AssetPath)
                return;

            // 名前変更・移動。保存していない編集内容は引き継ぐ（保存済みの記録は AudioBackupFollower が移している）
            AudioEditSettings unsaved = HasUnsavedChanges ? _settings.Clone() : null;
            Load(_clip);
            if (unsaved != null && _source != null)
            {
                unsaved.Clamp(_source.Duration);
                _settings = unsaved;
            }
            Repaint();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is AudioClip clip && clip != _clip)
                TrySwitchTo(clip);
        }

        private void Update()
        {
            // 再生位置の線を動かすため、鳴っている間は描き直す
            if (_player.IsPlaying)
                Repaint();
        }

        private void TrySwitchTo(AudioClip clip)
        {
            if (clip == null || clip == _clip)
                return;
            if (
                HasUnsavedChanges
                && _clip != null
                && !EditorUtility.DisplayDialog(
                    Title,
                    $"「{_clip.name}」の編集を保存していません。破棄して「{clip.name}」に切り替えますか？",
                    "破棄して切り替える",
                    "やめる"
                )
            )
            {
                return;
            }
            Load(clip);
        }

        private void Load(AudioClip clip)
        {
            _player.Stop();
            _clip = clip;
            _source = null;
            _settings = null;
            _previewSamples = null;
            _error = null;
            if (clip == null)
                return;

            if (
                !AudioClipEditing.TryLoad(AssetDatabase.GetAssetPath(clip), out _source, out _error)
            )
                return;

            _settings =
                _source.Previous?.Clone() ?? AudioEditSettings.CreateDefault(_source.Duration);
            _savedSettingsJson = JsonUtility.ToJson(_settings);
            _waveform.SetSamples(_source.Samples, _source.Channels);
        }

        private void OnGUI()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                var picked = (AudioClip)
                    EditorGUILayout.ObjectField("音素材", _clip, typeof(AudioClip), false);
                if (picked != _clip)
                    TrySwitchTo(picked);

                if (!AudioClipEditing.IsFfmpegAvailable)
                {
                    EditorGUILayout.HelpBox(
                        "ffmpeg（ffmpeg.exe と ffprobe.exe）が見つかりません。インストール済みの場合は、Unity を開き直してください。",
                        MessageType.Error
                    );
                    return;
                }
                if (_error != null)
                {
                    EditorGUILayout.HelpBox(_error, MessageType.Error);
                    return;
                }
                if (_source == null)
                {
                    EditorGUILayout.HelpBox(
                        "Project ウィンドウで音素材（mp3・ogg・wav）を選んでください。",
                        MessageType.Info
                    );
                    return;
                }

                DrawInfo();
                DrawWaveform();
                DrawTrim();
                DrawSilenceDetection();
                DrawPitchSpeedFade();
                DrawPlayback();
                DrawSaveButtons();
            }
        }

        private void DrawInfo()
        {
            string format =
                $"{_source.SampleRate} Hz・{_source.Channels} ch・{_source.Duration:0.000} 秒";
            EditorGUILayout.LabelField("元の素材", format);
            if (_source.Previous != null)
                EditorGUILayout.HelpBox(
                    "前回保存した編集の続きです（編集前の素材に、前回の編集内容を掛け直して表示しています）。",
                    MessageType.None
                );
        }

        private void DrawWaveform()
        {
            Rect rect = GUILayoutUtility.GetRect(10f, WaveformHeight, GUILayout.ExpandWidth(true));
            double playhead = -1;
            if (_player.IsPlaying)
            {
                // 編集後の音は速度を変えているため、元の素材の時間に直して線を引く
                playhead = _isPlayingEdited
                    ? _settings.TrimStart + _player.PlaybackSeconds * _settings.Speed
                    : _player.PlaybackSeconds;
            }

            if (
                _waveform.Draw(
                    rect,
                    _source.Duration,
                    ref _settings.TrimStart,
                    ref _settings.TrimEnd,
                    playhead
                )
            )
                Repaint();
            EditorGUILayout.LabelField(
                "波形をドラッグすると、近い方の端（残す範囲の始まり・終わり）が動きます。",
                EditorStyles.miniLabel
            );
        }

        private void DrawTrim()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("切り取り", EditorStyles.boldLabel);
            _settings.TrimStart = EditorGUILayout.DoubleField("始まり（秒）", _settings.TrimStart);
            _settings.TrimEnd = EditorGUILayout.DoubleField("終わり（秒）", _settings.TrimEnd);
            _settings.Clamp(_source.Duration);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "書き出した後の長さ",
                    $"{_settings.OutputDuration:0.000} 秒"
                );
                if (GUILayout.Button("全体に戻す", GUILayout.Width(90)))
                {
                    _settings.TrimStart = 0;
                    _settings.TrimEnd = _source.Duration;
                }
            }
        }

        private void DrawSilenceDetection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("無音の自動検出", EditorStyles.boldLabel);
            AudioSilenceDetector.ThresholdDb = EditorGUILayout.FloatField(
                new GUIContent(
                    "無音とみなす大きさ（dB）",
                    "この大きさ以下が続く部分を無音とみなす。小さい音まで残したいときは下げる"
                ),
                AudioSilenceDetector.ThresholdDb
            );
            AudioSilenceDetector.MarginSeconds = EditorGUILayout.FloatField(
                new GUIContent(
                    "前後に残す余白（秒）",
                    "音の立ち上がり・消え際が欠けないよう、検出した範囲より少し広く残す"
                ),
                AudioSilenceDetector.MarginSeconds
            );

            if (!GUILayout.Button("前後の無音を自動で選ぶ"))
                return;
            if (
                AudioSilenceDetector.TryFindSoundRange(
                    _source.Samples,
                    _source.Channels,
                    _source.SampleRate,
                    AudioSilenceDetector.ThresholdDb,
                    AudioSilenceDetector.MarginSeconds,
                    out double start,
                    out double end
                )
            )
            {
                _settings.TrimStart = start;
                _settings.TrimEnd = end;
            }
            else
            {
                EditorUtility.DisplayDialog(
                    Title,
                    "全体が無音とみなされました。「無音とみなす大きさ」を下げてください。",
                    "OK"
                );
            }
        }

        private void DrawPitchSpeedFade()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("音程・速度", EditorStyles.boldLabel);
            _settings.PitchSemitones = EditorGUILayout.Slider(
                new GUIContent(
                    "音程（半音）",
                    "速さは変えずに音の高さだけを変える。12 で1オクターブ"
                ),
                _settings.PitchSemitones,
                -AudioEditSettings.MaxPitchSemitones,
                AudioEditSettings.MaxPitchSemitones
            );
            _settings.Speed = EditorGUILayout.Slider(
                new GUIContent("再生速度（倍）", "音の高さは変えずに速さだけを変える"),
                _settings.Speed,
                AudioEditSettings.MinSpeed,
                AudioEditSettings.MaxSpeed
            );
            if (!AudioClipEditing.UsesRubberband)
            {
                EditorGUILayout.HelpBox(
                    "この ffmpeg には rubberband が入っていないため、音程・速度の変更は代わりの方法（atempo）で行います。"
                        + "無音から始まる音では、音の始まりが少し前にずれることがあります（rubberband 入りの ffmpeg を入れると直ります）。",
                    MessageType.Warning
                );
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(
                new GUIContent("フェード", "切り取った端だけに掛ける（切っていない端はそのまま）"),
                EditorStyles.boldLabel
            );
            _settings.FadeInMs = EditorGUILayout.FloatField("始まり（ミリ秒）", _settings.FadeInMs);
            _settings.FadeOutMs = EditorGUILayout.FloatField(
                "終わり（ミリ秒）",
                _settings.FadeOutMs
            );
            _settings.Clamp(_source.Duration);
        }

        private void DrawPlayback()
        {
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 元の音"))
                {
                    _isPlayingEdited = false;
                    _player.Play(_source.Samples, _source.Channels, _source.SampleRate);
                }
                if (GUILayout.Button("▶ 編集後の音"))
                    PlayEdited();
                if (GUILayout.Button("■ 停止", GUILayout.Width(70)))
                    _player.Stop();
            }
        }

        private void PlayEdited()
        {
            string json = JsonUtility.ToJson(_settings);
            if (_previewSamples == null || json != _previewSettingsJson)
            {
                if (
                    !AudioClipEditing.TryRenderPreview(
                        _source,
                        _settings,
                        out _previewSamples,
                        out string error
                    )
                )
                {
                    _previewSamples = null;
                    Debug.LogError($"[{Title}] 試聴用の加工に失敗しました。\n{error}");
                    EditorUtility.DisplayDialog(
                        Title,
                        $"試聴用の加工に失敗しました。\n\n{FirstLines(error)}\n\n全文は Console に出しました。",
                        "OK"
                    );
                    return;
                }
                _previewSettingsJson = json;
            }

            _isPlayingEdited = true;
            _player.Play(_previewSamples, _source.Channels, _source.SampleRate);
        }

        /// <summary>ダイアログに出すための先頭数行（ffmpeg の出力は長いため、全文は Console に出す）。</summary>
        private static string FirstLines(string text)
        {
            const int MaxLines = 3;
            string[] lines = text.Split(new[] { '\n' }, MaxLines + 1);
            return string.Join("\n", lines, 0, System.Math.Min(lines.Length, MaxLines)).Trim();
        }

        private void DrawSaveButtons()
        {
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!HasUnsavedChanges))
                {
                    if (GUILayout.Button("上書き保存", GUILayout.Height(28)))
                        Save();
                }
                using (new EditorGUI.DisabledScope(!AudioEditStore.CanRestore(_source.AssetPath)))
                {
                    if (GUILayout.Button("元に戻す", GUILayout.Height(28), GUILayout.Width(100)))
                        Restore();
                }
            }
            EditorGUILayout.HelpBox(
                $"上書きする前のファイルはプロジェクト直下の {AudioEditStore.BackupFolder} に保存し、「元に戻す」で戻せます"
                    + "（この PC にだけ残ります。git には入りません）。",
                MessageType.None
            );
        }

        private void Save()
        {
            string assetPath = _source.AssetPath;
            if (
                !EditorUtility.DisplayDialog(
                    Title,
                    $"「{Path.GetFileName(assetPath)}」を上書きします。",
                    "上書き保存",
                    "やめる"
                )
            )
                return;

            _player.Stop();
            bool wasNormalized = AudioLoudnessNormalizer.WasNormalized(assetPath);
            if (!AudioClipEditing.TryApply(_source, _settings, out string error))
            {
                Debug.LogError($"[{Title}] {assetPath}\n{error}");
                EditorUtility.DisplayDialog(
                    Title,
                    $"保存に失敗しました。\n\n{FirstLines(error)}\n\n全文は Console に出しました。",
                    "OK"
                );
                return;
            }

            Load(_clip);
            if (wasNormalized)
            {
                EditorUtility.DisplayDialog(
                    Title,
                    "この素材は音量をそろえ済みでした。編集したため、音量をそろえ直してください"
                        + "（Tools > SoundKit > ゲームで使う音素材の音量をそろえる、または SoundCue の「設定を素材に反映」）。",
                    "OK"
                );
            }
        }

        private void Restore()
        {
            if (
                !EditorUtility.DisplayDialog(
                    Title,
                    "初めて編集する前のファイルに戻します。編集の記録も消えます。",
                    "元に戻す",
                    "やめる"
                )
            )
                return;

            _player.Stop();
            AudioEditStore.Restore(_source.AssetPath);
            Load(_clip);
        }
    }
}
