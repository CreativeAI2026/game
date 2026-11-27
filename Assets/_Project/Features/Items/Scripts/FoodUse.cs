namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 食材を1つ食べる。所持品から1個減らし、プレイヤーの HP を回復する(所持品と HP の両方を変える処理)。
    /// 回復量は最大HPに対する食材ごとの固定割合(合成前20%/合成後50%)。
    /// </summary>
    public static class FoodUse
    {
        /// <summary>
        /// stack の食材を1つ食べる。食材でない・所持品に無い・死亡中は何もせず false(回復しないのに食材だけ減らさない)。
        /// </summary>
        public static bool TryEat(PlayerStatus status, ItemStack stack)
        {
            var inventory = InventoryManager.Instance;
            if (
                status == null
                || status.IsDead
                || inventory == null
                || stack == null
                || stack.Data is not FoodData food
                || !inventory.ConsumeFromStack(stack, 1)
            )
                return false;

            status.Heal(status.CurrentMaxHp * food.HealFraction);
            return true;
        }
    }
}
