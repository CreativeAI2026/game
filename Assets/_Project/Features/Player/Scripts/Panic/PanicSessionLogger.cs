using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦り度のしきい値・重みを実プレイから調整するため、各指標の値を一定間隔でCSVに記録する開発用コンポーネント。
    /// エディターと Development Build では常に記録する。ふつうのビルドでは、テスト用に
    /// 「ビルドでも記録する」（_recordInPlayerBuild）をオンにした時だけ記録する（製品版ではオフのまま）。
    ///
    /// 保存先は <see cref="PanicLogLocation"/> が決める（ビルドでは実行ファイルと同じフォルダの PanicLogs）。
    /// 1回の起動ごとに CSV と情報ファイル（<see cref="PanicSessionInfo"/>）を1組。Excel で文字化けしないよう BOM 付き UTF-8 で書く。
    /// 記録中は画面の隅に <see cref="PanicLogIndicator"/> で知らせる。
    ///
    /// 行の種類（row_type 列）:
    ///   sample … 一定間隔の記録
    ///   signal … 指標が焦りの兆候を検知した瞬間（detail 列に指標名）
    ///   marker … プレイヤーが「今焦った」と印を付けた瞬間（detail 列に通し番号）
    ///   level  … 焦りの段階が切り替わった瞬間（detail 列に新しい段階）
    ///   pressure … ボスの追い込みが始まった・終わった瞬間（detail 列に「開始:系統」または「終了:次の状態」）
    /// ボス（TutorialBossController）がいれば、焦りへのボスの反応（追い込みの状態・次の攻撃までの時間など）も列に記録する。
    /// marker 行の前後でスコアがどう動いていたかを見て、取りこぼし・誤検知を調べる。
    /// </summary>
    public class PanicSessionLogger : MonoBehaviour
    {
        // 書き出しをまとめる間隔（秒）。毎行書くとディスクアクセスが増え、溜めすぎると強制終了時に失われるため
        private const float FlushInterval = 1f;

        private static readonly PanicCategory[] Categories = (PanicCategory[])
            Enum.GetValues(typeof(PanicCategory));

        [Tooltip("記録する PanicDetector。未設定ならシーンから探す")]
        [SerializeField]
        private PanicDetector _panicDetector;

        [Tooltip("焦りに応じて追い込むボス。未設定ならシーンから探す（いなければボスの列は空欄）")]
        [SerializeField]
        private TutorialBossController _boss;

        [Tooltip("1秒あたりの sample 行の記録回数")]
        [SerializeField]
        [Min(1f)]
        private float _samplesPerSecond = 10f;

        [Tooltip(
            "交戦中だけ sample 行を記録するか。false なら探索中も記録する（signal・marker 行は常に記録）"
        )]
        [SerializeField]
        private bool _recordOnlyInCombat = false;

        [Tooltip(
            "「今焦った」の印を付けるキー。ゲームで使っていないキーにする。\n"
                + "G は WASD に手を置いたまま押せる。F3 はノートPCだと Fn との同時押しが要ることがあるので予備"
        )]
        [SerializeField]
        private Key[] _markerKeys = { Key.G, Key.F3 };

        [Tooltip("マウスの側面ボタン（戻る・進む）でも印を付けられるようにするか。親指で押せる")]
        [SerializeField]
        private bool _markWithMouseSideButtons = true;

        [Tooltip("パッドのセレクト（ビュー）ボタンでも印を付けられるようにするか")]
        [SerializeField]
        private bool _markWithGamepadSelect = true;

        [Tooltip(
            "ふつうのビルドでも記録するか。テスト用のビルドを他の人に渡す時だけオンにする（製品版ではオフ）。\n"
                + "エディターと Development Build では、この設定に関係なく常に記録する"
        )]
        [SerializeField]
        private bool _recordInPlayerBuild = false;

        [Tooltip("記録中であることと保存先を、画面の右上に表示するか")]
        [SerializeField]
        private bool _showIndicator = true;

        [Tooltip(
            "ゲームの終了時に、焦りと判定した場面を見せて「焦っていたか」を答えてもらうか（ビルドでのみ動く）。\n"
                + "エディターでは、このコンポーネントの右クリックメニューから確認用に開ける"
        )]
        [SerializeField]
        private bool _reviewOnQuit = true;

        [Tooltip("振り返りで見せる場面の最大数（答える人の負担を抑えるため）")]
        [SerializeField]
        [Min(1)]
        private int _maxReviewMoments = 8;

        private PlayerStatus _playerStatus;
        private PanicMomentRecorder _momentRecorder;
        private string _reviewPath;
        private bool _hasReviewed;
        private PlayerInput _playerInput;
        private PanicLogIndicator _indicator;
        private PanicSessionInfo _info;
        private string _infoPath;
        private StreamWriter _writer;
        private float _nextSampleTime;
        private float _nextFlushTime;
        private int _markerCount;

        /// <summary>記録中のファイルの絶対パス。記録していなければ null。</summary>
        public string FilePath { get; private set; }

        /// <summary>この起動で記録するか。</summary>
        private bool ShouldRecord
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return _recordInPlayerBuild;
#endif
            }
        }

        private void Awake()
        {
            if (!ShouldRecord)
            {
                // 製品版ではファイルを作らない
                enabled = false;
                return;
            }

            if (_panicDetector == null)
                _panicDetector = FindAnyObjectByType<PanicDetector>(FindObjectsInactive.Exclude);

            if (_panicDetector == null)
            {
                Debug.LogError($"{name}: PanicDetector が見つからないため焦り度ログを記録しません");
                enabled = false;
                return;
            }
            _playerStatus = _panicDetector.GetComponent<PlayerStatus>();
            // 操作機器（キーボード・マウスかパッドか）の判別用。無ければ「不明」として記録する
            _playerInput = _panicDetector.GetComponent<PlayerInput>();

            if (_boss == null)
                _boss = FindAnyObjectByType<TutorialBossController>(FindObjectsInactive.Exclude);
        }

        // 記録を止める時（ゲームの終了を含む）に、情報ファイルを最終の内容で書き直す
        private void OnApplicationQuit()
        {
            FinishInfo();
        }

        private void OnEnable()
        {
            _panicDetector.OnPanicSignalDetected += OnSignalDetected;
            _panicDetector.OnPanicLevelChanged += OnLevelChanged;
        }

        private void OnDisable()
        {
            _panicDetector.OnPanicSignalDetected -= OnSignalDetected;
            _panicDetector.OnPanicLevelChanged -= OnLevelChanged;
            _writer?.Flush();
        }

        // 列見出しに指標の一覧を使うため、PanicDetector の Awake（指標一覧の生成）が済んだ後に開く。
        // ボスの追い込み判断もボスの Awake で作られるので、購読はここで行う
        private void Start()
        {
            OpenFile();

            if (_boss != null)
            {
                _boss.PanicPressure.PressureStarted += OnPressureStarted;
                _boss.PanicPressure.PressureEnded += OnPressureEnded;
            }
        }

        private void OnDestroy()
        {
            if (_boss != null && _boss.PanicPressure != null)
            {
                _boss.PanicPressure.PressureStarted -= OnPressureStarted;
                _boss.PanicPressure.PressureEnded -= OnPressureEnded;
            }

            Application.wantsToQuit -= OnWantsToQuit;
            FinishInfo();
            _writer?.Dispose();
            _writer = null;
        }

        private void FinishInfo()
        {
            if (_info == null)
                return;

            _info.Write(_infoPath, Path.GetFileName(FilePath), _markerCount, isFinished: true);
        }

        private string CurrentControlScheme =>
            _playerInput != null && !string.IsNullOrEmpty(_playerInput.currentControlScheme)
                ? _playerInput.currentControlScheme
                : "不明";

        private void OnPressureStarted(PanicCategory category)
        {
            if (_writer == null)
                return;

            WriteRow("pressure", $"開始:{category.ToDisplayName()}");
        }

        private void OnPressureEnded()
        {
            if (_writer == null)
                return;

            WriteRow("pressure", $"終了:{_boss.PanicPressure.Mode.ToDisplayName()}");
        }

        private void Update()
        {
            if (_writer == null)
                return;

            if (WasMarkerPressedThisFrame())
            {
                _markerCount++;
                WriteRow("marker", _markerCount.ToString(CultureInfo.InvariantCulture));
                Debug.Log($"[焦り度ログ] 印 #{_markerCount} を記録しました（{Time.time:F1}秒）");
                if (_indicator != null)
                {
                    _indicator.NotifyMarker();
                }
            }

            _info.Tick(CurrentControlScheme, Time.unscaledDeltaTime);

            if (Time.time >= _nextSampleTime)
            {
                // 一時停止明けに溜まった分をまとめて書かないよう、基準は前回予定時刻ではなく現在時刻にする
                _nextSampleTime = Time.time + 1f / _samplesPerSecond;
                if (!_recordOnlyInCombat || _panicDetector.IsInCombat)
                {
                    WriteRow("sample", string.Empty);
                }
            }

            if (Time.unscaledTime >= _nextFlushTime)
            {
                _nextFlushTime = Time.unscaledTime + FlushInterval;
                _writer.Flush();
            }
        }

        /// <summary>印のキー・マウスの側面ボタン・パッドのセレクトボタンのどれかが、このフレームに押されたか。</summary>
        private bool WasMarkerPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                foreach (Key key in _markerKeys)
                {
                    if (keyboard[key].wasPressedThisFrame)
                        return true;
                }
            }

            var mouse = Mouse.current;
            if (
                _markWithMouseSideButtons
                && mouse != null
                && (mouse.backButton.wasPressedThisFrame || mouse.forwardButton.wasPressedThisFrame)
            )
            {
                return true;
            }

            var gamepad = Gamepad.current;
            return _markWithGamepadSelect
                && gamepad != null
                && gamepad.selectButton.wasPressedThisFrame;
        }

        /// <summary>記録中の表示に出す、印の付け方の説明。</summary>
        private string MarkerInputLabel
        {
            get
            {
                var parts = new List<string>();
                if (_markerKeys.Length > 0)
                    parts.Add(
                        string.Join(" か ", Array.ConvertAll(_markerKeys, k => k.ToString()))
                            + " キー"
                    );
                if (_markWithMouseSideButtons)
                    parts.Add("マウスの側面ボタン");
                if (_markWithGamepadSelect)
                    parts.Add("パッドのセレクトボタン");
                return string.Join("・", parts);
            }
        }

        private void OnLevelChanged(PanicLevel level)
        {
            if (_writer == null)
                return;

            WriteRow("level", level.ToDisplayName());
        }

        private void OnSignalDetected(string signalName)
        {
            if (_writer == null)
                return;

            WriteRow("signal", signalName);
        }

        private void OpenFile()
        {
            string directory = PanicLogLocation.PrepareDirectory();
            if (directory == null)
            {
                Debug.LogError("[焦り度ログ] 保存できる場所が無いため記録しません");
                enabled = false;
                return;
            }

            string baseName = $"panic_{DateTime.Now:yyyyMMdd_HHmmss}";
            string path = Path.Combine(directory, baseName + ".csv");

            try
            {
                _writer = new StreamWriter(path, false, new UTF8Encoding(true));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError(
                    $"[焦り度ログ] ファイルを作れなかったため記録しません: {path}\n{e.Message}"
                );
                enabled = false;
                return;
            }

            FilePath = path;
            _writer.WriteLine(BuildHeader());
            Debug.Log(
                $"[焦り度ログ] 記録を開始しました: {path}（{MarkerInputLabel}で「今焦った」の印）"
            );

            PanicLogLocation.WriteGuide(directory);
            _info = new PanicSessionInfo();
            _infoPath = Path.Combine(directory, baseName + "_info.txt");
            // 強制終了で終了時の書き込みが失われても、開始時の情報は残るよう先に一度書く
            _info.Write(_infoPath, Path.GetFileName(path), _markerCount, isFinished: false);

            if (_showIndicator)
            {
                _indicator = gameObject.AddComponent<PanicLogIndicator>();
                _indicator.hideFlags = HideFlags.DontSave;
                _indicator.Show(directory, MarkerInputLabel);
            }

            _momentRecorder = gameObject.AddComponent<PanicMomentRecorder>();
            _momentRecorder.hideFlags = HideFlags.DontSave;
            _momentRecorder.Begin(_panicDetector, _boss, directory, baseName);
            _reviewPath = Path.Combine(directory, baseName + "_review.csv");

            // エディターで再生を止める時に終了を止めてしまわないよう、終了時の振り返りはビルドでだけ使う
            if (_reviewOnQuit && !Application.isEditor)
            {
                Application.wantsToQuit += OnWantsToQuit;
            }
        }

        /// <summary>
        /// ゲームを終了しようとした時に呼ばれる。振り返る場面があれば終了を一度止めて振り返りを出し、
        /// 答え終わったら（または答えずに終了したら）改めて終了する。
        /// </summary>
        private bool OnWantsToQuit()
        {
            if (_hasReviewed || _momentRecorder.Moments.Count == 0)
                return true;

            OpenReview(() =>
            {
                _hasReviewed = true;
                Application.Quit();
            });
            return false;
        }

        [ContextMenu("終了時の振り返りを表示（確認用・再生中のみ）")]
        private void OpenReviewForCheck()
        {
            if (!Application.isPlaying || _momentRecorder == null)
            {
                Debug.LogWarning($"{name}: 振り返りの確認は、記録中の再生中に行ってください");
                return;
            }
            if (_momentRecorder.Moments.Count == 0)
            {
                Debug.LogWarning($"{name}: まだ「焦り」と判定した場面がありません");
                return;
            }
            OpenReview(null);
        }

        private void OpenReview(Action onFinished)
        {
            List<PanicMoment> moments = PanicReviewSelection.Select(
                _momentRecorder.Moments,
                _maxReviewMoments
            );

            // 振り返りの 1・2・3 キーで武器が切り替わらないよう、ゲームの操作を止めておく
            bool wasInputActive = _playerInput != null && _playerInput.inputIsActive;
            if (wasInputActive)
                _playerInput.DeactivateInput();

            var panel = gameObject.AddComponent<PanicReviewPanel>();
            panel.hideFlags = HideFlags.DontSave;
            panel.Open(
                moments,
                AppendReviewAnswer,
                () =>
                {
                    Destroy(panel);
                    if (wasInputActive)
                        _playerInput.ActivateInput();
                    onFinished?.Invoke();
                }
            );
        }

        // 答えを1つずつ書き足す（途中で強制終了しても、答えた分は残るように）
        private void AppendReviewAnswer(PanicMoment moment, string answer)
        {
            try
            {
                bool isNew = !File.Exists(_reviewPath);
                using var writer = new StreamWriter(_reviewPath, true, new UTF8Encoding(true));
                if (isNew)
                {
                    writer.WriteLine(
                        "moment,time,category,main_signal,description,recent_event,panic_score,image,answer"
                    );
                }
                writer.WriteLine(
                    string.Join(
                        ",",
                        moment.Index.ToString(CultureInfo.InvariantCulture),
                        Format(moment.Time),
                        moment.Category.ToDisplayName(),
                        moment.MainSignal,
                        moment.Description,
                        moment.RecentEvent,
                        Format(moment.PanicScore),
                        moment.ImageFileName,
                        answer
                    )
                );
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning(
                    $"[焦り度ログ] 振り返りの答えを書けませんでした: {_reviewPath}\n{e.Message}"
                );
            }
        }

        private string BuildHeader()
        {
            var header = new StringBuilder(
                "time,row_type,detail,in_combat,enemy,enemy_attacking,enemy_distance,player_hp_ratio,panic_score,level,dominant_category"
            );
            foreach (PanicCategory category in Categories)
            {
                header.Append(',').Append(category.ToDisplayName()).Append("_焦り度");
            }
            foreach (PanicSignal signal in _panicDetector.Signals)
            {
                header.Append(',').Append(signal.DisplayName).Append("_スコア");
                header
                    .Append(',')
                    .Append(signal.DisplayName)
                    .Append('_')
                    .Append(signal.RawValueUnit);
            }
            header.Append(
                ",boss_state,boss_pressure,boss_pressure_category,pressure_remaining,breather_remaining,next_attack_in,control_scheme"
            );
            return header.ToString();
        }

        private void WriteRow(string rowType, string detail)
        {
            PanicContext context = _panicDetector.Context;
            EnemyBaseController enemy = context.EngagedEnemy;
            float hpRatio =
                _playerStatus.CurrentMaxHp > 0f
                    ? _playerStatus.CurrentHp / _playerStatus.CurrentMaxHp
                    : 0f;

            var row = new StringBuilder();
            row.Append(Format(Time.time));
            row.Append(',').Append(rowType);
            row.Append(',').Append(EscapeCell(detail));
            row.Append(',').Append(context.IsInCombat ? 1 : 0);
            row.Append(',').Append(enemy != null ? EscapeCell(enemy.name) : string.Empty);
            row.Append(',').Append(context.IsEnemyAttacking ? 1 : 0);
            row.Append(',').Append(Format(context.EnemyDistance));
            row.Append(',').Append(Format(hpRatio));
            row.Append(',').Append(Format(_panicDetector.PanicScore));
            row.Append(',').Append(_panicDetector.Level.ToDisplayName());
            row.Append(',').Append(_panicDetector.DominantCategory.ToDisplayName());
            foreach (PanicCategory category in Categories)
            {
                row.Append(',').Append(Format(_panicDetector.GetCategoryScore(category)));
            }
            foreach (PanicSignal signal in _panicDetector.Signals)
            {
                row.Append(',').Append(Format(signal.Score));
                row.Append(',').Append(Format(signal.RawValue));
            }
            AppendBossColumns(row);
            row.Append(',').Append(EscapeCell(CurrentControlScheme));
            _writer.WriteLine(row.ToString());
        }

        // 焦りへのボスの反応。ボスがいなければ列の数だけ空欄にする（列がずれないように）
        private void AppendBossColumns(StringBuilder row)
        {
            if (_boss == null)
            {
                row.Append(",,,,,,");
                return;
            }

            TBossPanicPressure pressure = _boss.PanicPressure;
            bool isPressing = pressure.Mode == TBossPressureMode.Pressing;
            row.Append(',').Append(_boss.CurrentStateName);
            row.Append(',').Append(pressure.Mode.ToDisplayName());
            row.Append(',').Append(isPressing ? pressure.Category.ToDisplayName() : string.Empty);
            row.Append(',').Append(Format(pressure.PressureRemaining));
            row.Append(',').Append(Format(pressure.BreatherRemaining));
            row.Append(',').Append(Format(_boss.TimeUntilNextAttack));
        }

        // 端末の地域設定で小数点がカンマになると列がずれるため、常に InvariantCulture で書く
        private static string Format(float value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);

        // 名前にカンマや引用符が入っても列がずれないよう、CSV の規則で囲む
        private static string EscapeCell(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
                return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
