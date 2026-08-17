namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 矢の着弾に反応するためのインターフェース。
    /// IDamageableとは異なりダメージ値を持たない着弾通知のみを担う。
    /// 3Dボタンやギミックなど、HP概念のないオブジェクトでも矢のヒットに応答できるようにする。
    /// </summary>
    public interface IArrowHittable
    {
        void OnArrowHit();
    }
}
