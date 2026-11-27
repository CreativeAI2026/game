using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 会話イベントの command のうち、プレイヤーを動かすもの(瞬間移動・歩かせる)を実行する。会話UIではなくここで扱う。
    /// 歩かせている最中のプレイヤーを覚え、イベントを打ち切ったときに止める。
    /// </summary>
    public sealed class EventPlayerMovement
    {
        /// <summary>
        /// プレイヤーを <see cref="SpawnPoint"/>(arg が ID)へ移す command 名。
        /// 引き戻しに使う。画面を隠したいときは前後に background.black.fill / clear を並べる。
        /// </summary>
        public const string MoveCommand = "player.moveTo";

        /// <summary>
        /// プレイヤーを <see cref="SpawnPoint"/>(arg が ID)まで歩かせる command 名。着くまで次のステップへ進まない。
        /// <see cref="MoveCommand"/> と違い、瞬間移動せず歩きのモーションで向かう。引き戻しに使う。
        /// </summary>
        public const string WalkCommand = "player.walkTo";

        /// <summary>歩きが終わらないとき(壁に引っかかる等)に、想定の歩行時間へ掛ける余裕の倍率。</summary>
        private const float WalkTimeoutScale = 2f;

        /// <summary>歩行時間の下限に足す秒数。ごく近い目標でも、動き出しの時間は待つ。</summary>
        private const float WalkTimeoutPadding = 2f;

        // 歩かせている最中のプレイヤー。イベントを途中で打ち切ったとき、歩く指示を残さないための後始末に使う。
        private PlayerController _walking;

        /// <summary>command がプレイヤーを動かすものか。</summary>
        public static bool Handles(string command) =>
            command == MoveCommand || command == WalkCommand;

        /// <summary>command を実行する。歩かせるときは着くまで待つ。</summary>
        public IEnumerator Run(string command, string spawnPointId)
        {
            if (command == MoveCommand)
                Move(spawnPointId);
            else if (command == WalkCommand)
                yield return Walk(spawnPointId);
        }

        /// <summary>歩かせている最中なら止める。イベントを打ち切っても、あとで勝手に歩き出さないようにする。</summary>
        public void Stop()
        {
            if (_walking != null)
                _walking.StopWalking();
            _walking = null;
        }

        private static void Move(string spawnPointId)
        {
            if (TryFindPlayerAndPoint(MoveCommand, spawnPointId, out var player, out _))
                SpawnPoint.Place(player.gameObject, spawnPointId);
        }

        // 歩いて着くまで待つ。
        // 時間内に着かない(壁に引っかかる等)ときは、止まって瞬間移動で置き、操作が戻れなくなるのを防ぐ。
        private IEnumerator Walk(string spawnPointId)
        {
            if (!TryFindPlayerAndPoint(WalkCommand, spawnPointId, out var player, out var point))
                yield break;
            var target = point.transform.position;
            var flat = target - player.transform.position;
            flat.y = 0f;
            var timeout =
                flat.magnitude / Mathf.Max(player.WalkSpeed, 0.01f) * WalkTimeoutScale
                + WalkTimeoutPadding;

            _walking = player;
            player.WalkTo(target);
            var elapsed = 0f;
            while (player.IsWalking && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (player.IsWalking)
            {
                Debug.LogWarning(
                    $"[EventPlayerMovement] {WalkCommand} が {timeout:F1} 秒で着かなかったため、'{spawnPointId}' へ直接置きました。"
                );
                Stop();
                SpawnPoint.Place(player.gameObject, spawnPointId);
            }
            _walking = null;
        }

        // 動かす相手(プレイヤー)と、目的地の目印を探す。見つからなければ、動かさずに警告して false。
        // 目印が無いとき SpawnPoint.Place は原点へ置いてしまうので、先に確かめる。
        private static bool TryFindPlayerAndPoint(
            string command,
            string spawnPointId,
            out PlayerController player,
            out SpawnPoint point
        )
        {
            player = PlayerRegistry.Current;
            point = SpawnPoint.Find(spawnPointId);
            if (player != null && point != null)
                return true;

            Debug.LogWarning(
                $"[EventPlayerMovement] {command} を実行できません"
                    + $"(player={(player != null ? player.name : "なし")}, SpawnPoint ID='{spawnPointId}')。"
            );
            return false;
        }
    }
}
