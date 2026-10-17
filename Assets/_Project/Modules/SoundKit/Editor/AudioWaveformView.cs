using System;
using UnityEditor;
using UnityEngine;

namespace SoundKit.EditorTools
{
    /// <summary>
    /// 音声エディタの波形表示。残す範囲を明るく、切り取る範囲を暗く描き、範囲の両端をドラッグで動かせる。
    /// 毎回すべてのサンプルを見ると重いため、読み込み時に区間ごとの最大値（ピーク）を求めておき、それを描く。
    /// </summary>
    public class AudioWaveformView
    {
        // 前もって求めておくピークの数。画面の横幅より十分多ければ見た目は変わらない
        private const int MaxPeakCount = 4096;

        private static readonly Color BackgroundColor = new Color(0.13f, 0.13f, 0.13f);
        private static readonly Color KeptColor = new Color(1f, 0.6f, 0.15f);
        private static readonly Color TrimmedColor = new Color(0.4f, 0.4f, 0.4f);
        private static readonly Color HandleColor = new Color(0.3f, 0.85f, 1f);
        private static readonly Color PlayheadColor = Color.white;

        private enum DragTarget { None, Start, End }

        private float[] _peaks = Array.Empty<float>();
        private DragTarget _dragTarget;

        public void SetSamples(float[] samples, int channels)
        {
            int frameCount = samples.Length / channels;
            int peakCount = Math.Max(1, Math.Min(frameCount, MaxPeakCount));
            _peaks = new float[peakCount];

            for (int peak = 0; peak < peakCount; peak++)
            {
                int from = (int)((long)frameCount * peak / peakCount) * channels;
                int to = (int)((long)frameCount * (peak + 1) / peakCount) * channels;
                float max = 0f;
                for (int i = from; i < to; i++) max = Math.Max(max, Math.Abs(samples[i]));
                _peaks[peak] = Math.Min(1f, max);
            }
        }

        /// <summary>
        /// 波形を描き、範囲の両端のドラッグを受け付ける。範囲を動かしたら true。
        /// playheadSeconds は再生中の位置（元の素材の時間）。再生していなければ負の値。
        /// </summary>
        public bool Draw(Rect rect, double duration, ref double trimStart, ref double trimEnd, double playheadSeconds)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            bool changed = HandleDrag(controlId, rect, duration, ref trimStart, ref trimEnd);

            if (Event.current.type != EventType.Repaint) return changed;

            EditorGUI.DrawRect(rect, BackgroundColor);
            DrawPeaks(rect, duration, trimStart, trimEnd);
            DrawVerticalLine(rect, TimeToX(rect, duration, trimStart), HandleColor, 2f);
            DrawVerticalLine(rect, TimeToX(rect, duration, trimEnd), HandleColor, 2f);
            if (playheadSeconds >= 0) DrawVerticalLine(rect, TimeToX(rect, duration, playheadSeconds), PlayheadColor, 1f);

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            return changed;
        }

        private void DrawPeaks(Rect rect, double duration, double trimStart, double trimEnd)
        {
            int width = Mathf.Max(1, Mathf.FloorToInt(rect.width));
            float centerY = rect.y + rect.height * 0.5f;
            for (int x = 0; x < width; x++)
            {
                int from = _peaks.Length * x / width;
                int to = Math.Max(from + 1, _peaks.Length * (x + 1) / width);
                float max = 0f;
                for (int i = from; i < to && i < _peaks.Length; i++) max = Math.Max(max, _peaks[i]);

                double time = duration * (x + 0.5) / width;
                bool kept = time >= trimStart && time <= trimEnd;
                float height = Mathf.Max(1f, max * rect.height);
                EditorGUI.DrawRect(new Rect(rect.x + x, centerY - height * 0.5f, 1f, height), kept ? KeptColor : TrimmedColor);
            }
        }

        private bool HandleDrag(int controlId, Rect rect, double duration, ref double trimStart, ref double trimEnd)
        {
            Event current = Event.current;
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (current.button != 0 || !rect.Contains(current.mousePosition)) return false;
                    // 押した位置に近い方の端をつかむ（線から離れた所を押しても、近い方の端がそこへ動く）
                    float startX = TimeToX(rect, duration, trimStart);
                    float toStart = Mathf.Abs(current.mousePosition.x - startX);
                    float toEnd = Mathf.Abs(current.mousePosition.x - TimeToX(rect, duration, trimEnd));
                    // 両端が重なっているときは、押した側（左なら始まり、右なら終わり）をつかむ
                    bool pickStart = toStart < toEnd || (Mathf.Approximately(toStart, toEnd) && current.mousePosition.x <= startX);
                    _dragTarget = pickStart ? DragTarget.Start : DragTarget.End;
                    GUIUtility.hotControl = controlId;
                    current.Use();
                    return MoveHandle(rect, duration, current.mousePosition.x, ref trimStart, ref trimEnd);

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId) return false;
                    current.Use();
                    return MoveHandle(rect, duration, current.mousePosition.x, ref trimStart, ref trimEnd);

                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId) return false;
                    GUIUtility.hotControl = 0;
                    _dragTarget = DragTarget.None;
                    current.Use();
                    return false;
            }
            return false;
        }

        private bool MoveHandle(Rect rect, double duration, float mouseX, ref double trimStart, ref double trimEnd)
        {
            double time = Math.Max(0, Math.Min(duration, (mouseX - rect.x) / rect.width * duration));
            if (_dragTarget == DragTarget.Start) trimStart = Math.Min(time, trimEnd);
            else if (_dragTarget == DragTarget.End) trimEnd = Math.Max(time, trimStart);
            return _dragTarget != DragTarget.None;
        }

        private static float TimeToX(Rect rect, double duration, double time)
            => rect.x + (float)(duration > 0 ? time / duration : 0) * rect.width;

        private static void DrawVerticalLine(Rect rect, float x, Color color, float width)
            => EditorGUI.DrawRect(new Rect(x - width * 0.5f, rect.y, width, rect.height), color);
    }
}
