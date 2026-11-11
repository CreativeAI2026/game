using System;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// クラゲボスのわっか拘束中のカメラ演出へ、拘束側で起きた出来事を知らせるイベント。
    ///
    /// 拘束(<see cref="KurageRingRestraint"/>)はカメラの作り方を知らず、カメラ(KurageRestraintCamera)は
    /// ボスの参照を持たずにシーンへ常駐できるよう、間を静的イベントで繋ぐ。
    /// TutorialBoss の掴みは段階(引き寄せ→電撃→脱出)が別物なので <see cref="GrabEscapeEvents"/> とは分けている。
    /// </summary>
    public static class KurageRestraintCameraEvents
    {
        /// <summary>拘束開始。引数はボスの Transform(カメラの構図をボスの方角から決めるために使う)。</summary>
        public static Action<Transform> OnBegin;

        /// <summary>レバガチャの入力の向きが変わった(もがいた)。引数はその時の移動入力。</summary>
        public static Action<Vector2> OnStruggle;

        /// <summary>締め付けのダメージが入る瞬間。</summary>
        public static Action OnSqueeze;

        /// <summary>拘束終了。引数は true でレバガチャによる脱出成功、false でそれ以外(プレイヤー死亡・ボス撃破など)。</summary>
        public static Action<bool> OnEnd;
    }
}
