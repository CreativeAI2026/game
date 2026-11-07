using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 敵の攻撃がプレイヤーに触れた時の処理(剣のガード・パリィ判定 → ダメージ)。
    /// 判定の形(武器の球・触手のカプセル列など)は攻撃ごとに違うが、当たった後の扱いは全攻撃で揃えるため一か所にまとめる。
    /// </summary>
    public static class EnemyAttackHit
    {
        /// <summary>
        /// プレイヤーに攻撃を当てる。ガード・パリィが成立したらダメージを与えず false を返す。
        /// </summary>
        /// <param name="player">Player タグの付いたコライダー</param>
        /// <param name="attacker">攻撃した敵。パリィされた時に崩す相手を探すのに使う</param>
        public static bool ApplyToPlayer(
            Collider player,
            float damage,
            Transform attacker,
            Vector3 hitPoint
        )
        {
            // ガード・パリィ判定を先に行い、成立した場合はダメージ処理をスキップする。
            // これにより防御側の応答を攻撃側で一元管理できる。
            SwordController sword = player.GetComponentInChildren<SwordController>();
            if (
                sword != null
                && sword.gameObject.activeInHierarchy
                && sword.ReceiveAttack(damage, true, attacker, hitPoint)
            )
            {
                return false;
            }

            if (!player.TryGetComponent(out IDamageable target))
            {
                return false;
            }

            target.TakeDamage(damage, false);
            return true;
        }
    }
}
