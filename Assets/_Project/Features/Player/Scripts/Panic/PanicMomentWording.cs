namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 焦り度の指標を、テストする人が場面を思い出せる言葉にする（振り返り画面で使う）。
    /// 指標の名前（PanicSignal.DisplayName）ではなく、その時に本人がしていたことで書く。
    /// </summary>
    public static class PanicMomentWording
    {
        /// <summary>指標の名前から、場面の説明を作る。</summary>
        public static string Describe(string signalDisplayName) =>
            signalDisplayName switch
            {
                "逃避" => "背を向けてボスから離れていたとき",
                "スパム" => "何も来ていないのに防御ボタンを連打していたとき",
                "ジッター" => "弓の狙いが定まらず、あちこち振っていたとき",
                "空間喪失" => "壁や物に引っかかって、思うように動けなかったとき",
                "意思崩壊" => "弓を構えては、撃たずに下ろしていたとき",
                "回避失敗" => "続けて攻撃を受けていたとき",
                "硬直" => "攻撃が来ても、動けなかったとき",
                "武器切替" => "武器を何度も切り替えていたとき",
                _ => "ボスと戦っていたとき",
            };

        /// <summary>ボスのステート名から、攻撃の呼び名を作る。攻撃でなければ null。</summary>
        public static string BossAttackName(string bossStateName) =>
            bossStateName switch
            {
                "TBossNormalAttackState" => "斬りかかり",
                "TBossNormalAttack2State" => "鎌の攻撃",
                "TBossSpecialAttackState" => "触手の攻撃",
                _ => null,
            };
    }
}
