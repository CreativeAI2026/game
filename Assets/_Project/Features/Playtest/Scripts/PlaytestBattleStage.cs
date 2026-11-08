using System;
using System.Collections.Generic;
using CreativeAI.Core;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.Assertions;

namespace CreativeAI.Playtest
{
    /// <summary>
    /// ボス体験用ビルドの「戦闘ステージ」。再生開始時は敵を隠してフィールドのまま始め(所持品・キャラ画面で装備を整えられる)、
    /// プレイヤーがこのトリガーの中へ入ったら敵を出して戦闘モードにする。敵を全員倒したらフィールドモードへ戻す。
    /// 入口に立った瞬間ではなく、箱の縁から <see cref="_entryDepth"/> 以上奥へ入ってから始める
    /// (戦闘開始で閉じる扉・柵(<see cref="PlaytestArenaLock"/>)にプレイヤーが重なり、外へ押し出されないように)。
    ///
    /// 本来 <see cref="GameModeManager.EnterBattle"/> を叩くのは会話イベントの battle ステップ(EventPlayer)だけだが、
    /// battle ステップは敵をプレハブから新しく生成するため、シーンに置いて調整済みのボスを使えない。
    /// そのため体験用ビルドに限り、ここから直接モードを切り替える。
    /// </summary>
    // 敵の Awake/Start より先に隠す。Start が走る前に隠せば、表示した時点で初めて動き出す(クラゲは Start で攻撃を始めるため)。
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PlaytestBattleStage : MonoBehaviour
    {
        private static readonly Color GizmoColor = new Color(1f, 0.35f, 0.2f, 0.25f);
        private static readonly Color EntryGizmoColor = new Color(1f, 0.35f, 0.2f, 0.9f);

        [Tooltip("戦闘ステージに入るまで隠しておき、入ったら出す敵(シーンに置いたボスのルート)")]
        [SerializeField]
        private GameObject[] _enemies = Array.Empty<GameObject>();

        [SerializeField]
        private string _playerTag = "Player";

        [Tooltip(
            "箱の縁(水平方向)からこの距離(m)以上奥へ入ったら戦闘を始める。Scene ビューの濃い線の内側が始まる範囲"
        )]
        [SerializeField, Min(0f)]
        private float _entryDepth = 2f;

        private readonly List<EnemyStatus> _aliveEnemies = new();
        private BoxCollider _box;
        private Transform _playerInside;
        private bool _inBattle;

        /// <summary>戦闘を始めた(敵を出した)か。決着後も true のまま。</summary>
        public bool HasStarted { get; private set; }

        /// <summary>戦闘を始めた瞬間(敵を出して戦闘モードにした直後)。</summary>
        public event Action BattleStarted;

        /// <summary>敵を全員倒してフィールドモードへ戻した瞬間。</summary>
        public event Action BattleEnded;

        // 追加した時点で Is Trigger にする(付け忘れるとただの壁になり、入っても戦闘が始まらない)。
        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            Assert.IsTrue(_enemies.Length > 0, $"{name}: 出す敵が1体も設定されていません");
            Assert.IsTrue(_box.isTrigger, $"{name}: BoxCollider の Is Trigger がオフです");

            foreach (var enemy in _enemies)
            {
                Assert.IsNotNull(enemy, $"{name}: 敵の欄に空きがあります");
                enemy.SetActive(false);
            }
        }

        private void Start()
        {
            // シーン切替で前のシーンの戦闘モードが残っていても、ここからはフィールドで始める。
            // GameModeManager は FieldDevBootstrap の Awake で作られるので、Awake ではなくここで見る。
            var mode = GameModeManager.Instance;
            Assert.IsNotNull(
                mode,
                $"{name}: GameModeManager がありません。シーンに FieldDevBootstrap を置いてください"
            );
            if (mode.CurrentMode == GameMode.Battle)
                mode.ExitBattle();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(_playerTag))
                _playerInside = other.transform;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(_playerTag))
                _playerInside = null;
        }

        private void Update()
        {
            if (!HasStarted && _playerInside != null && IsDeepEnough(_playerInside.position))
                StartBattle();
        }

        private bool IsDeepEnough(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition) - _box.center;
            Vector3 scale = transform.lossyScale;
            float depthX = (_box.size.x * 0.5f - Mathf.Abs(local.x)) * Mathf.Abs(scale.x);
            float depthZ = (_box.size.z * 0.5f - Mathf.Abs(local.z)) * Mathf.Abs(scale.z);
            return depthX >= _entryDepth && depthZ >= _entryDepth;
        }

        private void OnDestroy()
        {
            foreach (var status in _aliveEnemies)
            {
                if (status != null)
                    status.OnDeathTriggered -= OnEnemyDefeated;
            }
            _aliveEnemies.Clear();

            // 戦闘中にシーンを切り替えたときに、次のシーンへ戦闘モードを持ち越さない。
            if (_inBattle && GameModeManager.Instance != null)
                GameModeManager.Instance.ExitBattle();
        }

        private void StartBattle()
        {
            HasStarted = true;
            foreach (var enemy in _enemies)
            {
                enemy.SetActive(true);
                var status = enemy.GetComponentInChildren<EnemyStatus>();
                Assert.IsNotNull(status, $"{enemy.name}: EnemyStatus が無く、決着を検知できません");
                status.OnDeathTriggered += OnEnemyDefeated;
                _aliveEnemies.Add(status);
            }

            _inBattle = true;
            GameModeManager.Instance.EnterBattle();
            BattleStarted?.Invoke();
        }

        // EnemyStatus.Die は IsDead を立ててから通知するので、ここで倒れた敵を見分けられる。
        private void OnEnemyDefeated()
        {
            for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
            {
                var status = _aliveEnemies[i];
                if (status != null && !status.IsDead)
                    continue;
                if (status != null)
                    status.OnDeathTriggered -= OnEnemyDefeated;
                _aliveEnemies.RemoveAt(i);
            }
            if (_aliveEnemies.Count > 0)
                return;

            _inBattle = false;
            GameModeManager.Instance.ExitBattle();
            BattleEnded?.Invoke();
        }

        // 再生しなくても Scene ビューで戦闘ステージの範囲が見えるように、選択していなくても描く。
        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out BoxCollider box))
                return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = GizmoColor;
            Gizmos.DrawCube(box.center, box.size);

            // 戦闘が始まる範囲(縁から _entryDepth 内側)
            Vector3 scale = transform.lossyScale;
            var inner = new Vector3(
                Mathf.Max(0f, box.size.x - 2f * _entryDepth / Mathf.Abs(scale.x)),
                box.size.y,
                Mathf.Max(0f, box.size.z - 2f * _entryDepth / Mathf.Abs(scale.z))
            );
            Gizmos.color = EntryGizmoColor;
            Gizmos.DrawWireCube(box.center, inner);
        }
    }
}
