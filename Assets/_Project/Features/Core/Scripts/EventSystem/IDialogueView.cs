using System;
using System.Collections;
using System.Collections.Generic;

namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// 会話UIの seam。実体は UI アセンブリ(CreativeAI.UI)で実装し、EventPlayer に注入する。
    /// Core を最下層に保つため、EventPlayer は具象UIではなくこの契約に依存する。
    /// </summary>
    public interface IDialogueView
    {
        /// <summary>1行表示し、プレイヤーが送るまで待つ(コルーチン)。</summary>
        IEnumerator ShowLine(string speaker, string portrait, string text);

        /// <summary>選択肢を提示し、選ばれた値を onSelected で返す(コルーチン)。</summary>
        IEnumerator ShowChoice(IReadOnlyList<ChoiceOption> options, Action<string> onSelected);

        /// <summary>
        /// giveItem の入手演出。itemKey から絵と名前を引くのは UI 側(Core は Gameplay を参照しないため)。
        /// message 省略時は UI が「〜を手に入れた。」を組み立てる。送り入力まで待つ(コルーチン)。
        /// </summary>
        IEnumerator ShowItemGet(string itemKey, string message);

        /// <summary>
        /// giveWeapon の入手演出。ShowItemGet の武器版(3Dモデルを回して見せる)。
        /// </summary>
        IEnumerator ShowWeaponGet(string weaponKey, string message);

        /// <summary>
        /// command ステップの演出コマンド(window.hide / portrait.left.shake / wait など)を実行する。
        /// 対応コマンドは documents/ScenarioReference.md「演出コマンド」。
        /// </summary>
        IEnumerator RunCommand(string command, string argument);
    }

    /// <summary>
    /// 実行時に有効な IDialogueView を Core 側へ登録する seam。会話UI(UI アセンブリ)が生成時に
    /// 自身を登録し、EventPlayer は Inspector 未配線時のフォールバックとしてここを見る
    /// (ItemGiverService と同じ思想)。EventPlayer は常駐・会話UIも常駐生成のため drag 配線できず、
    /// かつ Core は UI を参照できないため、具象ではなくこの契約経由で受け取る。
    /// </summary>
    public static class DialogueViewService
    {
        public static IDialogueView Current { get; set; }
    }
}
