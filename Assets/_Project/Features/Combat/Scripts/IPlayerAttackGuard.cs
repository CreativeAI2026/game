using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// プレイヤーの攻撃を受け止められる敵。攻撃側（近接判定・矢）は、当たった相手にダメージを与える前にこれを問い合わせる。
    /// 敵の攻撃がプレイヤーに当たった時に、先に剣のガードを問い合わせる（<see cref="EnemyAttackHit"/>）のと同じ形にして、
    /// 防御の扱いを攻撃側で一元的に行う。
    /// </summary>
    public interface IPlayerAttackGuard
    {
        /// <summary>
        /// プレイヤーの攻撃が当たった時に、ダメージを与える前に呼ばれる。防げなかった攻撃でも、当たるたびに必ず呼ばれる。
        /// </summary>
        /// <param name="kind">攻撃の種類</param>
        /// <param name="hitPoint">当たった位置</param>
        /// <returns>受け止めたら true。攻撃側はダメージもヒットの演出も出さない</returns>
        bool ReceivePlayerAttack(PlayerAttackKind kind, Vector3 hitPoint);
    }
}
