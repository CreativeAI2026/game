using System;
using System.Collections;
using System.Collections.Generic;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 会話UIの seam。実体は UI アセンブリ(CreativeAI.UI)で実装し、EventPlayer に注入する。
    /// Gameplay は UI を参照できないため、EventPlayer は具象UIではなくこの契約に依存する。
    /// </summary>
    public interface IDialogueView
    {
        /// <summary>1行表示し、プレイヤーが送るまで待つ(コルーチン)。</summary>
        IEnumerator ShowLine(string speaker, string portrait, string text);

        /// <summary>選択肢を提示し、選ばれた値を onSelected で返す(コルーチン)。</summary>
        IEnumerator ShowChoice(IReadOnlyList<ChoiceOption> options, Action<string> onSelected);

        /// <summary>
        /// useItem ステップの持ち物ピッカー。持ち物から1つ選ばせ、選んだ itemKey を onPicked で返す(コルーチン)。
        /// やめたときは null、itemKey を持たないアイテムを選んだときは空文字。prompt は案内文(空なら UI の既定文)。
        /// </summary>
        IEnumerator ShowItemPicker(string prompt, Action<string> onPicked);

        /// <summary>
        /// giveItem の入手演出。itemKey から絵と名前を引くのは UI 側(見せ方は UI の担当のため)。
        /// message 省略時は UI が「〜を手に入れた。」を組み立てる。送り入力まで待つ(コルーチン)。
        /// </summary>
        IEnumerator ShowItemGet(string itemKey, string message);

        /// <summary>
        /// giveWeapon の入手演出。ShowItemGet の武器版(3Dモデルを回して見せる)。
        /// </summary>
        IEnumerator ShowWeaponGet(string weaponKey, string message);

        /// <summary>
        /// command ステップの演出コマンド(window.hide / portrait.left.shake / wait など)を実行する。
        /// </summary>
        IEnumerator RunCommand(string command, string argument);

        /// <summary>
        /// 会話ウィンドウを閉じる。イベント終了時と battle の直前に EventPlayer が呼ぶ。既に閉じていれば何もしない。
        /// </summary>
        IEnumerator Close();
    }

    /// <summary>
    /// 実行時の IDialogueView を登録する seam。Gameplay は UI を参照できず常駐同士で drag 配線もできないため、
    /// 会話UIが生成時に登録し、EventPlayer は Inspector 未配線時のフォールバックとして見る。
    /// </summary>
    public static class DialogueViewService
    {
        public static IDialogueView Current { get; set; }
    }
}
