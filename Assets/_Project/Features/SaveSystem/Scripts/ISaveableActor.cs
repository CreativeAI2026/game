namespace CreativeAI.Gameplay
{
    /// <summary>
    /// セーブ復元に参加するアクターの seam。HP の実体(PlayerStatus)を SaveService がこの窓口越しに読み書きする。
    /// 座標・向きは SaveService が Player タグのリグ root を直接扱うので含めない。
    /// </summary>
    public interface ISaveableActor
    {
        /// <summary>保存時に現在HPを返す。</summary>
        float CaptureHp();

        /// <summary>復元時に現在HPを設定する。最大HP等でのクランプは実装側の責務。</summary>
        void RestoreHp(float hp);
    }
}
