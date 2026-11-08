using System;
using System.Collections.Generic;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

namespace CreativeAI.Playtest
{
    /// <summary>
    /// 戦闘が始まったら戦闘ステージを閉じ、プレイヤーも敵もステージの外へ出られないようにする。敵を全員倒したら元に戻す。
    /// - 扉: 閉める(扉板の当たり判定でプレイヤーを止める)。戦闘中は DoorInteractor が開閉を受け付けない。
    ///   NavMesh で歩く敵(TBoss)は当たり判定では止まらないので、閉めた扉板の位置で NavMesh を削る。
    /// - 戦闘中だけ出すもの(外した柵など): 再生開始時に隠し、戦闘開始で出す。
    /// - 戦闘中だけ隠すもの(準備エリアから上がる階段など): 戦闘開始で隠す。
    /// </summary>
    [RequireComponent(typeof(PlaytestBattleStage))]
    public sealed class PlaytestArenaLock : MonoBehaviour
    {
        [Tooltip("戦闘開始で閉める扉(準備エリアとつながる扉)")]
        [SerializeField]
        private SwingDoor[] _doorsToClose = Array.Empty<SwingDoor>();

        [Tooltip("戦闘中だけ出すもの(入口をふさぐ柵など)。再生開始時は隠れる")]
        [SerializeField]
        private GameObject[] _showDuringBattle = Array.Empty<GameObject>();

        [Tooltip("戦闘中だけ隠すもの(準備エリアから上がる階段など)")]
        [SerializeField]
        private GameObject[] _hideDuringBattle = Array.Empty<GameObject>();

        private readonly List<NavMeshObstacle> _doorObstacles = new();
        private PlaytestBattleStage _stage;

        private void Awake()
        {
            _stage = GetComponent<PlaytestBattleStage>();

            foreach (var door in _doorsToClose)
            {
                Assert.IsNotNull(door, $"{name}: 閉める扉の欄に空きがあります");
                _doorObstacles.Add(CreateDoorObstacle(door));
            }
            foreach (var target in _showDuringBattle)
            {
                Assert.IsNotNull(target, $"{name}: 戦闘中だけ出すものの欄に空きがあります");
                target.SetActive(false);
            }
            foreach (var target in _hideDuringBattle)
            {
                Assert.IsNotNull(target, $"{name}: 戦闘中だけ隠すものの欄に空きがあります");
                target.SetActive(true);
            }
        }

        private void OnEnable()
        {
            _stage.BattleStarted += Lock;
            _stage.BattleEnded += Unlock;
        }

        private void OnDisable()
        {
            _stage.BattleStarted -= Lock;
            _stage.BattleEnded -= Unlock;
        }

        private void Lock() => SetLocked(true);

        private void Unlock() => SetLocked(false);

        // 決着後は扉を閉めたままにする(戦闘外なので、プレイヤーは自分で開けて戻れる)。
        private void SetLocked(bool locked)
        {
            if (locked)
            {
                foreach (var door in _doorsToClose)
                    door.Close();
            }
            foreach (var obstacle in _doorObstacles)
                obstacle.enabled = locked;
            foreach (var target in _showDuringBattle)
                target.SetActive(locked);
            foreach (var target in _hideDuringBattle)
                target.SetActive(!locked);
        }

        /// <summary>
        /// 閉じた扉板と同じ位置・大きさで NavMesh を削る障害物を作る(無効のまま)。
        /// 再生開始時の扉は閉じているので、この時点の扉板の位置を使う。扉板は開閉で回るので、子にはしない。
        /// </summary>
        private NavMeshObstacle CreateDoorObstacle(SwingDoor door)
        {
            Transform leaf = door.Leaf != null ? door.Leaf : SwingDoor.FindLeaf(door.transform);
            Assert.IsNotNull(leaf, $"{door.name}: 扉板が見つかりません");
            var leafBox = leaf.GetComponent<BoxCollider>();
            Assert.IsNotNull(leafBox, $"{door.name}: 扉板に BoxCollider がありません");

            var holder = new GameObject($"{door.name}_ArenaLockObstacle");
            holder.transform.SetPositionAndRotation(
                leaf.TransformPoint(leafBox.center),
                leaf.rotation
            );

            var obstacle = holder.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = Vector3.Scale(leafBox.size, leaf.lossyScale);
            obstacle.carving = true;
            obstacle.enabled = false;
            return obstacle;
        }
    }
}
