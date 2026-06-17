namespace CreativeAI.Gameplay
{
    // ダメージ処理を行うインターフェース。すべてのHP持ちキャラクターにつく
    public interface IDamageable
    {
        void TakeDamage(float damage, bool isCritical);
    }
}
