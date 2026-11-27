using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// シーンに置く1回の戦闘(遭遇戦)。戦う敵を隠しておき、始まったら出して GameModeManager に戦闘の開始を要求する。
    /// 敵を全員倒したら決着を GameModeManager に伝える(戻り先のフィールドか会話は GameModeManager が決める)。
    /// 始まり方は2通り。名前(<see cref="_encounterId"/>)が無ければ、プレイヤーが箱の縁から <see cref="_entryDepth"/> 以上奥へ
    /// 入ったら始まる(戦闘開始で閉じる扉・柵(<see cref="BattleEncounterLock"/>)にプレイヤーが重なり、外へ押し出されないように)。
    /// 名前があれば入っても始まらず、会話イベントの battle(<see cref="FightFromEvent"/>)からだけ始まる(二重開始を避けるため)。
    /// 倒した後に再戦できるよう、同じボスを使い回さず、戦闘ごとに敵と箱を置く。
    /// </summary>
    // 敵の Awake/Start より先に隠す。Start が走る前に隠せば、表示した時点で初めて動き出す(クラゲは Start で攻撃を始めるため)。
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BattleEncounter : MonoBehaviour
    {
        private static readonly List<BattleEncounter> Registered = new();
        private static readonly Color GizmoColor = new Color(1f, 0.35f, 0.2f, 0.25f);
        private static readonly Color EntryGizmoColor = new Color(1f, 0.35f, 0.2f, 0.9f);

        [Tooltip("戦闘エリアに入るまで隠しておき、入ったら出す敵(シーンに置いたボスのルート)")]
        [SerializeField]
        private GameObject[] _enemies = Array.Empty<GameObject>();

        [SerializeField]
        private string _playerTag = "Player";

        [Tooltip(
            "会話イベントの battle ステップ(events.json の arena)が呼ぶ名前。空なら箱に入ると始まる。名前を付けるとイベントからだけ始まり、入っても始まらない"
        )]
        [SerializeField, FormerlySerializedAs("_arenaId")]
        private string _encounterId = "";

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

        private bool IsEventDriven => !string.IsNullOrEmpty(_encounterId);

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
            if (IsEventDriven)
                Registered.Add(this);
            Assert.IsTrue(_enemies.Length > 0, $"{name}: 出す敵が1体も設定されていません");
            Assert.IsTrue(_box.isTrigger, $"{name}: BoxCollider の Is Trigger がオフです");

            foreach (var enemy in _enemies)
            {
                Assert.IsNotNull(enemy, $"{name}: 敵の欄に空きがあります");
                enemy.SetActive(false);
            }
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
            if (
                !IsEventDriven
                && !HasStarted
                && _playerInside != null
                && IsDeepEnough(_playerInside.position)
            )
                StartBattle();
        }

        /// <summary>名前(events.json の arena)で戦闘を探す。無ければ null。</summary>
        public static BattleEncounter Find(string encounterId) =>
            Registered.Find(encounter => encounter._encounterId == encounterId);

        /// <summary>
        /// 会話イベントの battle の手順から、名前で探した戦闘を始めて決着まで待つ(コルーチン)。
        /// 見つからない・すでに始めていたら警告して何もしない。
        /// 決着せずにシーンごと消えたとき(倒れてタイトルへ戻る等)は、イベントを先へ進めないよう待ち続け、発火側と一緒に打ち切られる。
        /// </summary>
        public static IEnumerator FightFromEvent(string encounterId)
        {
            var encounter = Find(encounterId);
            if (encounter == null)
            {
                Debug.LogWarning(
                    $"[BattleEncounter] '{encounterId}' がシーンに無いため戦闘をスキップします。"
                );
                yield break;
            }

            bool ended = false;
            void OnEnded() => ended = true;
            encounter.BattleEnded += OnEnded;
            try
            {
                if (!encounter.StartFromEvent())
                {
                    Debug.LogWarning(
                        $"[BattleEncounter] '{encounterId}' はすでに戦闘を始めているためスキップします。"
                    );
                    yield break;
                }
                while (!ended)
                    yield return null;
            }
            finally
            {
                if (encounter != null)
                    encounter.BattleEnded -= OnEnded;
            }
        }

        /// <summary>
        /// 会話イベントから戦闘を始める。すでに始めていれば何もせず false を返す。
        /// </summary>
        public bool StartFromEvent()
        {
            return !HasStarted && StartBattle();
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
            Registered.Remove(this);
            foreach (var status in _aliveEnemies)
            {
                if (status != null)
                    status.OnDeathTriggered -= OnEnemyDefeated;
            }
            _aliveEnemies.Clear();

            // 戦闘中にシーンを切り替えたときに、次のシーンへ戦闘モードを持ち越さない。
            if (_inBattle && GameModeManager.Instance != null)
                GameModeManager.Instance.EndBattle();
        }

        // 戦闘を始める。もう戦闘中なら始めず false。
        private bool StartBattle()
        {
            var mode = GameModeManager.Instance;
            Assert.IsNotNull(
                mode,
                $"{name}: GameModeManager がありません。シーンに FieldDevBootstrap を置いてください"
            );
            if (!mode.TryStartBattle())
                return false;

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
            BattleStarted?.Invoke();
            return true;
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

            // 戻り先(フィールドか会話)は GameModeManager が覚えている。
            _inBattle = false;
            GameModeManager.Instance.EndBattle();
            BattleEnded?.Invoke();
        }

        // 再生しなくても Scene ビューで戦闘エリアの範囲が見えるように、選択していなくても描く。
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
