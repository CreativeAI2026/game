namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 矢が命中したときに呼び出されるインターフェース。
    /// 3Dボタンなど、矢の着弾を受け取りたいオブジェクトに実装する。
    /// </summary>
    public interface IArrowHittable
    {
        void OnArrowHit();
    }
}
