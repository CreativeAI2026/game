using System;
using System.Collections;
using System.Globalization;
using CreativeAI.Gameplay;
using SoundKit;
using UnityEngine;

namespace CreativeAI.UI
{
    /// <summary>
    /// 演出コマンドのうち、背景(暗転・フラッシュ・画像)・音(SE/BGM)・カメラ揺れを担当する。
    /// 音と背景画像は、events.json にはキーだけを書き、キーと素材の対応は ConversationView に登録したものから引く
    /// (キャラの portrait キーと同じ作り)。素材が登録されていなければ警告だけ出して続行する。
    /// </summary>
    internal sealed class DialogueStageCommands
    {
        public const float DefaultFadeSeconds = 1f;
        public const float DefaultFlashSeconds = 0.4f;
        public const float DefaultPictureFadeSeconds = 0.5f;
        public const float DefaultShakeAmplitude = 0.5f;

        private readonly DialogueBackdropPresenter _backdrop;
        private readonly Func<string, SoundCue> _findSound;
        private readonly Func<string, Sprite> _findBackground;

        /// <param name="findSound">sound.se / sound.bgm のキーから SoundCue を引く。無ければ null。</param>
        /// <param name="findBackground">background.image のキーから背景画像を引く。無ければ null。</param>
        public DialogueStageCommands(
            DialogueBackdropPresenter backdrop,
            Func<string, SoundCue> findSound = null,
            Func<string, Sprite> findBackground = null
        )
        {
            _backdrop = backdrop;
            _findSound = findSound;
            _findBackground = findBackground;
        }

        /// <summary>このクラスが受け持つコマンドなら実行コルーチンを返す。そうでなければ null。</summary>
        public IEnumerator Create(string command, string argument)
        {
            string[] args = SplitArguments(argument);
            switch (command?.Trim().ToLowerInvariant())
            {
                case "background.black":
                    return Run(() => _backdrop.SetColor(Color.black));
                case "background.color":
                    return Run(() => _backdrop.SetColor(ParseColor(Arg(args, 0), Color.black)));
                case "background.black.fill":
                    return _backdrop.FadeTo(
                        Color.black,
                        ParseSeconds(Arg(args, 0), DefaultFadeSeconds)
                    );
                case "background.fill":
                    return _backdrop.FadeTo(
                        ParseColor(Arg(args, 0), Color.black),
                        ParseSeconds(Arg(args, 1), DefaultFadeSeconds)
                    );
                case "background.black.clear":
                case "background.clear":
                    return _backdrop.FadeToClear(ParseSeconds(Arg(args, 0), DefaultFadeSeconds));
                case "background.flash":
                    return _backdrop.Flash(
                        ParseColor(Arg(args, 0), Color.white),
                        ParseSeconds(Arg(args, 1), DefaultFlashSeconds)
                    );
                case "background.image":
                    return ShowPicture(
                        Arg(args, 0),
                        ParseSeconds(Arg(args, 1), DefaultPictureFadeSeconds)
                    );
                case "background.image.clear":
                    return _backdrop.ClearPicture(
                        ParseSeconds(Arg(args, 0), DefaultPictureFadeSeconds)
                    );
                case "camera.shake":
                    return Run(() => ShakeCamera(ParseNumber(Arg(args, 0), DefaultShakeAmplitude)));
                case "sound.se":
                    return Run(() => PlaySe(Arg(args, 0)));
                case "sound.bgm":
                    return Run(() =>
                        PlayBgm(
                            Arg(args, 0),
                            ParseSeconds(Arg(args, 1), BgmPlayer.DefaultFadeDuration)
                        )
                    );
                case "sound.bgm.stop":
                    return Run(() =>
                        BgmPlayer.Stop(
                            BgmLayer.Main,
                            ParseSeconds(Arg(args, 0), BgmPlayer.DefaultFadeDuration)
                        )
                    );
                default:
                    return null;
            }
        }

        private IEnumerator ShowPicture(string key, float seconds)
        {
            var sprite = string.IsNullOrEmpty(key) ? null : _findBackground?.Invoke(key);
            if (sprite == null)
            {
                Debug.LogWarning(
                    $"[ConversationView] 背景画像 '{key}' が登録されていません"
                        + "(ConversationView の Backgrounds にキーと画像を登録してください)。"
                );
                yield break;
            }
            yield return _backdrop.ShowPicture(sprite, seconds);
        }

        private static void ShakeCamera(float amplitude)
        {
            var shake = CameraShakeManager.Instance;
            if (shake == null)
            {
                Debug.LogWarning(
                    "[ConversationView] CameraShakeManager がシーンに無いため揺らせません。"
                );
                return;
            }
            shake.Shake(amplitude);
        }

        private void PlaySe(string key)
        {
            var cue = LoadCue(key);
            if (cue != null)
                SoundEffectPlayer.Play(cue, null);
        }

        private void PlayBgm(string key, float fadeSeconds)
        {
            var cue = LoadCue(key);
            if (cue != null)
                BgmPlayer.Play(cue, BgmLayer.Main, fadeSeconds);
        }

        private SoundCue LoadCue(string key)
        {
            var cue = string.IsNullOrEmpty(key) ? null : _findSound?.Invoke(key);
            if (cue == null)
                Debug.LogWarning(
                    $"[ConversationView] 音 '{key}' が登録されていません"
                        + "(ConversationView の Sounds にキーと SoundCue を登録してください)。"
                );
            return cue;
        }

        private static IEnumerator Run(Action action)
        {
            action();
            yield break;
        }

        private static string[] SplitArguments(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                return Array.Empty<string>();
            string[] parts = argument.Split(',');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();
            return parts;
        }

        private static string Arg(string[] args, int index) =>
            index < args.Length ? args[index] : null;

        private static float ParseNumber(string text, float fallback) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : fallback;

        private static float ParseSeconds(string text, float fallback) =>
            Mathf.Clamp(ParseNumber(text, fallback), 0f, DialogueBackdropPresenter.MaxFadeSeconds);

        private static Color ParseColor(string text, Color fallback) =>
            !string.IsNullOrEmpty(text) && ColorUtility.TryParseHtmlString(text, out Color color)
                ? color
                : fallback;
    }
}
