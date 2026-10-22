using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦り度のしきい値・重みを実プレイから調整するため、各指標の値を一定間隔でCSVに記録する開発用コンポーネント。
    /// エディターと開発ビルドでのみ動く（製品ビルドでは何もしない）。
    ///
    /// 保存先: エディター = プロジェクト直下の Logs/Panic（git 管理外）、開発ビルド = persistentDataPath/PanicLogs。
    /// 1回の再生ごとに1ファイル。Excel で文字化けしないよう BOM 付き UTF-8 で書く。
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

        [Tooltip("「今焦った」の印を付けるキー。ゲームビューにフォーカスがある時のみ受け付ける")]
        [SerializeField]
        private Key _markerKey = Key.F3;

        private PlayerStatus _playerStatus;
        private StreamWriter _writer;
        private float _nextSampleTime;
        private float _nextFlushTime;
        private int _markerCount;

        /// <summary>記録中のファイルの絶対パス。記録していなければ null。</summary>
        public string FilePath { get; private set; }

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_panicDetector == null)
                _panicDetector = FindAnyObjectByType<PanicDetector>(FindObjectsInactive.Exclude);

            if (_panicDetector == null)
            {
                Debug.LogError($"{name}: PanicDetector が見つからないため焦り度ログを記録しません");
                enabled = false;
                return;
            }
            _playerStatus = _panicDetector.GetComponent<PlayerStatus>();

            if (_boss == null)
                _boss = FindAnyObjectByType<TutorialBossController>(FindObjectsInactive.Exclude);
#else
            // 開発中の調整用なので、製品ビルドではファイルを作らない
            enabled = false;
#endif
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

            _writer?.Dispose();
            _writer = null;
        }

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

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_markerKey].wasPressedThisFrame)
            {
                _markerCount++;
                WriteRow("marker", _markerCount.ToString(CultureInfo.InvariantCulture));
                Debug.Log($"[焦り度ログ] 印 #{_markerCount} を記録しました（{Time.time:F1}秒）");
            }

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
#if UNITY_EDITOR
            string directory = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Logs", "Panic")
            );
#else
            string directory = Path.Combine(Application.persistentDataPath, "PanicLogs");
#endif
            string path = Path.Combine(directory, $"panic_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            try
            {
                Directory.CreateDirectory(directory);
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
                $"[焦り度ログ] 記録を開始しました: {path}（{_markerKey} キーで「今焦った」の印）"
            );
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
                ",boss_state,boss_pressure,boss_pressure_category,pressure_remaining,breather_remaining,next_attack_in"
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
