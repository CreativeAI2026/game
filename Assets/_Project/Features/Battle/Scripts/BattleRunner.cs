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

                // 敗北時はセーブ再開でシーンごと作り直されるため、このコルーチンは完了しない想定。
                // arena が破棄された場合も(シーン切替)待ちを抜ける。
                while (!ended && arena != null)
                    yield return null;
            }
            finally
            {
                if (arena != null)
                    arena.BattleEnded -= OnEnded;
            }
        }
    }
}
