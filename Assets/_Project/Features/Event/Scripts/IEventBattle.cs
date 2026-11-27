using System.Collections;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 会話イベントの battle ステップで戦う口。本番はシーンに置いた <see cref="BattleEncounter"/> を名前(events.json の arena)で呼ぶ。
    /// テスト・会話プレビューは差し替えて、戦闘を飛ばしたり止めたりする。
    /// </summary>
    public interface IEventBattle
    {
        /// <summary>名前で指した戦闘を始め、決着まで待つ(コルーチン)。</summary>
        IEnumerator Fight(string encounterId);
    }
}
