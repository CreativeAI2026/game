using System.Collections;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// battle ステップの実体(IBattleRunner)。events.json の arena で指された BattleArena を探して戦闘を始めさせ、
    /// 敵を全員倒す(BattleEnded)まで待って EventPlayer に制御を返す。
    /// 敵・戦闘モードの出入り・入口の扉はすべて BattleArena の側が受け持つ。
    /// 状態もコルーチンも持たないので MonoBehaviour にしない。EventPlayer が1つ作って使う。
    /// </summary>
    public sealed class BattleRunner : IBattleRunner
    {
        // 倒れる動きを見せてから、タイトルへ戻し始めるまでの秒数
        private const float DefeatDelaySeconds = 3f;

        public IEnumerator Run(string arenaId)
        {
            var arena = BattleArena.Find(arenaId);
            if (arena == null)
            {
                Debug.LogWarning(
                    $"[BattleRunner] arena '{arenaId}' の BattleArena がシーンに無いため戦闘をスキップします。"
                );
                yield break;
            }

            bool ended = false;
            void OnEnded() => ended = true;
            arena.BattleEnded += OnEnded;
            try
            {
                if (!arena.StartFromEvent())
                {
                    Debug.LogWarning(
                        $"[BattleRunner] arena '{arenaId}' はすでに戦闘を始めているためスキップします。"
                    );
                    yield break;
                }

                var player = PlayerRegistry.Current;
                var status = player != null ? player.GetComponent<PlayerStatus>() : null;

                // arena が破棄された場合も(シーン切替)待ちを抜ける。
                while (!ended && arena != null && !IsDefeated(status))
                    yield return null;

                if (IsDefeated(status))
                {
                    yield return new WaitForSeconds(DefeatDelaySeconds);
                    ReturnToTitle();
                    // タイトルの読み込みで arena が破棄されるまで待つ(負けたイベントを先へ進めない)。
                    while (arena != null)
                        yield return null;
                }
            }
            finally
            {
                if (arena != null)
                    arena.BattleEnded -= OnEnded;
            }
        }

        private static bool IsDefeated(PlayerStatus status) => status != null && status.IsDead;

        // タイトルの「続きから」でセーブした所から再開できるので、敗北はタイトルへ戻して扱う。
        private static void ReturnToTitle()
        {
            if (TitleReturnService.Current != null)
                TitleReturnService.Current.ReturnToTitle();
            else
                Debug.LogError("[BattleRunner] タイトルへ戻す処理が登録されていません。");
        }
    }
}
