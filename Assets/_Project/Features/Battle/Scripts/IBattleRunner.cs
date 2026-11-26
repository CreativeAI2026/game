using System.Collections;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// battle ステップの実行役。ボスシーンに置いた BattleArena を名前(arena)で呼んで戦闘を始め、
    /// 決着まで待つコルーチン。実体は BattleRunner で、テストは差し替える。
    /// 敗北時はシーン再読込で再開されるので完了しない想定。
    /// </summary>
    public interface IBattleRunner
    {
        IEnumerator Run(string arenaId);
    }
}
