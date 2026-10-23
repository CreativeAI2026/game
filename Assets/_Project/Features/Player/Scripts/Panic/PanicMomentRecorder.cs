using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 検知が「焦り」と判定した瞬間の画面を小さく撮り、場面の説明と直前の出来事と一緒に残す（PanicSessionLogger が付けて使う）。
    /// 残した場面は、ゲームの終了時の振り返り（PanicReviewPanel）でテストする人に見せる。
    /// 画像はログと同じフォルダの「<ログ名>_moments」に、場面の一覧は「<ログ名>_moments.csv」に保存する。
    ///
    /// 直前の出来事（掴まれた・続けて斬られた・ボスの攻撃）の判定には時刻を使うが、これは記録する側の処理で、
    /// テストする人に時刻を答えてもらうわけではない。
    /// </summary>
    public class PanicMomentRecorder : MonoBehaviour
    {
        // 1回の起動で残す場面の上限（画像はメモリにも持つので抑える）
        private const int MaxMoments = 20;

        // 同じ焦りの続きを何枚も撮らないよう、前の場面からこれだけ空ける（秒）
        private const float MinSecondsBetweenMoments = 10f;

        // 振り返りで見せる画像の大きさ（16:9）
        private const int ThumbnailWidth = 480;
        private const int ThumbnailHeight = 270;

        // 「直前の出来事」とみなす時間（秒）
        private const float RecentEventSeconds = 4f;
        private const float RecentGrabSeconds = 6f;

        private PanicDetector _detector;
        private PlayerStatus _playerStatus;
        private TutorialBossController _boss;
        private string _imageDirectory;
        private string _imageDirectoryName;
        private string _listPath;

        private readonly List<PanicMoment> _moments = new List<PanicMoment>();
        private readonly Queue<float> _hitTimes = new Queue<float>();
        private float _lastMomentTime = float.NegativeInfinity;
        private float _lastGrabTime = float.NegativeInfinity;
        private float _lastBossAttackTime = float.NegativeInfinity;
        private string _lastBossAttackName;
        private string _previousBossState = string.Empty;

        /// <summary>残した場面（古い順）。</summary>
        public IReadOnlyList<PanicMoment> Moments => _moments;

        /// <summary>記録を始める。directory はログのフォルダ、baseName はログのファイル名（拡張子なし）。</summary>
        public void Begin(PanicDetector detector, TutorialBossController boss, string directory, string baseName)
        {
            _detector = detector;
            _boss = boss;
            _playerStatus = detector.GetComponent<PlayerStatus>();
            _imageDirectoryName = baseName + "_moments";
            _imageDirectory = Path.Combine(directory, _imageDirectoryName);
            _listPath = Path.Combine(directory, baseName + "_moments.csv");

            _detector.OnPanicLevelChanged += OnPanicLevelChanged;
            _playerStatus.Damaged += OnPlayerDamaged;
        }

        private void OnDestroy()
        {
            if (_detector != null)
            {
                _detector.OnPanicLevelChanged -= OnPanicLevelChanged;
                _playerStatus.Damaged -= OnPlayerDamaged;
            }

            foreach (PanicMoment moment in _moments)
            {
                if (moment.Thumbnail != null)
                    Destroy(moment.Thumbnail);
            }
        }

        private void Update()
        {
            if (_boss == null)
                return;

            // ボスの攻撃・掴みが始まった時刻を覚えておく（直前の出来事として説明に添える）
            string state = _boss.CurrentStateName;
            if (state != _previousBossState)
            {
                if (state == "TBossCapturedState")
                {
                    _lastGrabTime = Time.time;
                }
                string attackName = PanicMomentWording.BossAttackName(state);
                if (attackName != null)
                {
                    _lastBossAttackName = attackName;
                    _lastBossAttackTime = Time.time;
                }
                _previousBossState = state;
            }
        }

        private void OnPlayerDamaged()
        {
            _hitTimes.Enqueue(Time.time);
        }

        private void OnPanicLevelChanged(PanicLevel level)
        {
            if (level != PanicLevel.Panic || _moments.Count >= MaxMoments)
                return;
            if (Time.time - _lastMomentTime < MinSecondsBetweenMoments)
                return;

            _lastMomentTime = Time.time;
            var moment = new PanicMoment
            {
                Index = _moments.Count + 1,
                Time = Time.time,
                Category = _detector.DominantCategory,
                PanicScore = _detector.PanicScore,
                MainSignal = FindMainSignal(),
                RecentEvent = DescribeRecentEvent(),
            };
            moment.Description = PanicMomentWording.Describe(moment.MainSignal);
            _moments.Add(moment);
            StartCoroutine(CaptureAtEndOfFrame(moment));
        }

        // 総合焦り度への効き（重み×スコア）が最も大きい指標を、判定の中心とする
        private string FindMainSignal()
        {
            string main = string.Empty;
            float best = -1f;
            foreach (PanicSignal signal in _detector.Signals)
            {
                float evidence = signal.Weight * signal.Score;
                if (evidence > best)
                {
                    best = evidence;
                    main = signal.DisplayName;
                }
            }
            return main;
        }

        // 掴まれた > 続けて斬られた > 斬られた > ボスの攻撃 の順に、覚えていやすい出来事を1つ選ぶ
        private string DescribeRecentEvent()
        {
            float now = Time.time;
            while (_hitTimes.Count > 0 && now - _hitTimes.Peek() > RecentEventSeconds)
            {
                _hitTimes.Dequeue();
            }

            if (now - _lastGrabTime <= RecentGrabSeconds)
                return "触手に掴まれたあと";
            if (_hitTimes.Count >= 2)
                return $"続けて{_hitTimes.Count}回攻撃を受けた直後";
            if (_hitTimes.Count == 1)
                return "攻撃を受けた直後";
            if (now - _lastBossAttackTime <= RecentEventSeconds)
                return $"ボスの{_lastBossAttackName}の直後";
            return string.Empty;
        }

        private IEnumerator CaptureAtEndOfFrame(PanicMoment moment)
        {
            // 描画が終わった後でないと、その瞬間の画面を撮れない
            yield return new WaitForEndOfFrame();

            Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
            moment.Thumbnail = Downscale(screen, ThumbnailWidth, ThumbnailHeight);
            Destroy(screen);

            SaveImage(moment);
            AppendToList(moment);
        }

        private static Texture2D Downscale(Texture2D source, int width, int height)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0);
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            var result = new Texture2D(width, height, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            return result;
        }

        private void SaveImage(PanicMoment moment)
        {
            string fileName = $"moment_{moment.Index:00}.png";
            try
            {
                Directory.CreateDirectory(_imageDirectory);
                File.WriteAllBytes(Path.Combine(_imageDirectory, fileName), moment.Thumbnail.EncodeToPNG());
                moment.ImageFileName = Path.Combine(_imageDirectoryName, fileName);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 画像が保存できなくても、振り返りではメモリ上の画像を見せられる
                Debug.LogWarning($"[焦り度ログ] 場面の画像を保存できませんでした: {fileName}\n{e.Message}");
            }
        }

        // 場面の一覧を1行ずつ書き足す（強制終了でも、そこまでの場面は残るように）
        private void AppendToList(PanicMoment moment)
        {
            try
            {
                bool isNew = !File.Exists(_listPath);
                using var writer = new StreamWriter(_listPath, true, new UTF8Encoding(true));
                if (isNew)
                {
                    writer.WriteLine("moment,time,category,main_signal,description,recent_event,panic_score,image");
                }
                writer.WriteLine(
                    string.Join(
                        ",",
                        moment.Index.ToString(CultureInfo.InvariantCulture),
                        moment.Time.ToString("0.###", CultureInfo.InvariantCulture),
                        moment.Category.ToDisplayName(),
                        moment.MainSignal,
                        moment.Description,
                        moment.RecentEvent,
                        moment.PanicScore.ToString("0.#", CultureInfo.InvariantCulture),
                        moment.ImageFileName
                    )
                );
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[焦り度ログ] 場面の一覧を書けませんでした: {_listPath}\n{e.Message}");
            }
        }
    }
}
