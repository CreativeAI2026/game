using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// SoundCue のインスペクタ。ゲームを動かさずに大きさを確かめられるよう、試聴と、素材の大きさの設定を素材へ反映するボタンを足す。
    /// 試聴は Volume と流し先（ミキサーのグループ）を通して鳴らす。距離による減衰と音程の揺らぎは含まない。
    /// </summary>
    [CustomEditor(typeof(SoundCue))]
    public class SoundCueEditor : Editor
    {
        private static AudioSource _previewSource;

        /// <summary>素材ごとの「今の設定どおりに作られているか」。設定を変えるたびに測り直すと重いため、変更が落ち着いてから更新する。</summary>
        private readonly Dictionary<string, bool> _upToDate = new Dictionary<string, bool>();
        private bool _statusDirty = true;

        private SoundCue Cue => (SoundCue)target;

        private void OnEnable() => _statusDirty = true;

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
                _statusDirty = true;

            // スライダーを動かしている最中は測り直さない
            if (
                _statusDirty
                && Event.current.type == EventType.Layout
                && GUIUtility.hotControl == 0
            )
                RefreshStatus();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("試聴・素材への反映", EditorStyles.boldLabel);
            DrawClipRows();
            DrawButtons();
            EditorGUILayout.HelpBox(
                "試聴は Volume とミキサーの流し先を通して鳴らします（離れたときの減衰・音程の揺らぎは含みません）。\n"
                    + "「素材の大きさ（dB）」と「音割れしても上げる」は、素材に反映するまで音に出ません。",
                MessageType.None
            );
        }

        private void DrawClipRows()
        {
            foreach (AudioClip clip in Cue.Clips.Where(clip => clip != null))
            {
                string path = AssetDatabase.GetAssetPath(clip);
                bool upToDate = _upToDate.TryGetValue(path, out bool value) && value;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("▶", GUILayout.Width(28)))
                        Play(clip);
                    EditorGUILayout.LabelField(Path.GetFileName(path));

                    var previousColor = GUI.contentColor;
                    GUI.contentColor = upToDate ? previousColor : new Color(1f, 0.6f, 0.3f);
                    EditorGUILayout.LabelField(
                        upToDate ? "反映済み" : "未反映",
                        GUILayout.Width(60)
                    );
                    GUI.contentColor = previousColor;
                }
            }
        }

        private void DrawButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!Cue.HasClip))
                {
                    if (GUILayout.Button("▶ 試聴（ランダムに1つ）"))
                        Play(Cue.PickClip());
                }
                if (GUILayout.Button("■ 停止", GUILayout.Width(70)) && _previewSource != null)
                    _previewSource.Stop();
            }

            bool hasPending = _upToDate.Values.Any(upToDate => !upToDate);
            using (new EditorGUI.DisabledScope(!hasPending))
            {
                if (GUILayout.Button("設定を素材に反映"))
                {
                    AudioLoudnessNormalizer.NormalizeClips(
                        Cue.Clips.Where(clip => clip != null).Select(AssetDatabase.GetAssetPath)
                    );
                    RefreshStatus();
                }
            }
        }

        private void RefreshStatus()
        {
            _statusDirty = false;
            _upToDate.Clear();

            Dictionary<string, AudioLoudnessNormalizer.ClipSetting> settings =
                AudioLoudnessNormalizer.CollectSettings();
            foreach (AudioClip clip in Cue.Clips.Where(clip => clip != null))
            {
                string path = AssetDatabase.GetAssetPath(clip);
                _upToDate[path] = AudioLoudnessNormalizer.IsUpToDate(
                    path,
                    AudioLoudnessNormalizer.GetSetting(settings, path)
                );
            }
        }

        private void Play(AudioClip clip)
        {
            if (clip == null)
                return;

            AudioSource source = GetPreviewSource();
            source.Stop();
            source.outputAudioMixerGroup = AudioMixerRouting.GetGroup(Cue.Category);
            source.PlayOneShot(clip, Cue.Volume);
        }

        private static AudioSource GetPreviewSource()
        {
            if (_previewSource != null)
                return _previewSource;

            // シーンに保存されず、Hierarchy にも出ない試聴専用の音源
            var go = EditorUtility.CreateGameObjectWithHideFlags(
                "SoundCuePreview",
                HideFlags.HideAndDontSave,
                typeof(AudioSource)
            );
            _previewSource = go.GetComponent<AudioSource>();
            _previewSource.playOnAwake = false;
            _previewSource.spatialBlend = 0f;
            // ゲーム中のポーズ（AudioListener.pause）の最中でも試聴できるように
            _previewSource.ignoreListenerPause = true;
            return _previewSource;
        }
    }
}
