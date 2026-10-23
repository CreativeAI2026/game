using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦り度ログ1回分の付随情報（いつ・どの版で・どの操作機器で遊んだか）を集め、CSV と並べてテキストに書く。
    /// ジッターのように操作機器（マウスかパッドか）で値の出方が変わる指標を、分けて分析するために残す。
    /// </summary>
    public class PanicSessionInfo
    {
        private readonly DateTime _startedAt = DateTime.Now;
        private readonly Dictionary<string, float> _secondsPerControlScheme =
            new Dictionary<string, float>();
        private float _recordedSeconds;

        /// <summary>今フレームの操作機器と経過時間を足し込む。</summary>
        public void Tick(string controlScheme, float deltaTime)
        {
            _recordedSeconds += deltaTime;
            _secondsPerControlScheme.TryGetValue(controlScheme, out float seconds);
            _secondsPerControlScheme[controlScheme] = seconds + deltaTime;
        }

        /// <summary>情報を書き出す。記録の開始時と終了時に呼び、終了時の内容で上書きする。</summary>
        public void Write(string path, string csvFileName, int markerCount, bool isFinished)
        {
            var text = new StringBuilder();
            text.AppendLine($"記録ファイル: {csvFileName}");
            text.AppendLine($"記録開始: {_startedAt:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine(
                isFinished
                    ? $"記録終了: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                    : "記録終了: （記録中。ゲームを終了すると書き込まれる）"
            );
            text.AppendLine($"記録時間: {_recordedSeconds:F0} 秒");
            text.AppendLine($"「焦った」の印の回数: {markerCount}");
            text.AppendLine($"ゲームの版: {Application.version}");
            text.AppendLine($"Unity: {Application.unityVersion}");
            text.AppendLine(
                $"シーン: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}"
            );
            text.AppendLine($"画面: {Screen.width} x {Screen.height}");
            text.AppendLine("操作機器ごとの時間:");
            foreach (KeyValuePair<string, float> pair in _secondsPerControlScheme)
            {
                text.AppendLine($"  {pair.Key}: {pair.Value:F0} 秒");
            }

            try
            {
                File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning(
                    $"[焦り度ログ] 情報ファイルを書けませんでした: {path}\n{e.Message}"
                );
            }
        }
    }
}
