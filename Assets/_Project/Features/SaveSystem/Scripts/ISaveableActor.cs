namespace CreativeAI.Gameplay
{
    /// <summary>
    /// セーブ復元に参加するプレイヤー等アクターの境界(seam)。
    /// 現在HPの実体は担当班(PlayerStatus)が持つため、システム班の SaveService はこの窓口越しに読み書きする。
    /// これにより保存フォーマット(システム班)とHPの実装(プレイヤー班)を JSON/インターフェースで分離する。
    /// 座標・向きは tag="Player" のリグ root を SaveService が直接 Transform で扱うため、ここには含めない。
    /// </summary>
    public interface ISaveableActor
    {
        /// <summary>保存時に現在HPを返す。</summary>
        float CaptureHp();

        /// <summary>復元時に現在HPを設定する。最大HP等でのクランプは実装側の責務。</summary>
        void RestoreHp(float hp);
    }
}
