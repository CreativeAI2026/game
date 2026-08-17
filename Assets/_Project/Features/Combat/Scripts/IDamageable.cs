namespace CreativeAI.Gameplay
{
    /// <summary>
    /// ダメージを受けられるオブジェクトの共通インターフェース。
    /// 近接・遠距離・環境ダメージなど攻撃手段に依存せず統一的にダメージを与えるために使用する。
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float damage, bool isCritical);
    }
}
