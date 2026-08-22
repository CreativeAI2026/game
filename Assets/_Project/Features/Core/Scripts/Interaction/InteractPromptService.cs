namespace CreativeAI.Core.Interaction
{
    /// <summary>
    /// 「近づいた対象に何ができるか」を画面に1つだけ出すための受け渡し口
    /// (<see cref="CreativeAI.Core.EventSystem.EventPlaybackService"/> と同じ静的サービスの流儀)。
    ///
    /// ワールド側(扉など、CreativeAI.Gameplay)と表示側(常駐UI、CreativeAI.UI)は
    /// アセンブリが片方向参照(UI → Gameplay)なので、ワールド側から UI を直接触れない。
    /// ここを間に挟んで、ワールド側は「出す/消す」だけ、UI 側は購読して描くだけにする。
    ///
    /// 同時に複数の対象の範囲に入ることがあるので、<b>最後に Show した対象が勝つ</b>。
    /// 消すのは自分が出しているときだけ(<see cref="Hide"/>)なので、離れた対象が
    /// 別の対象のプロンプトを消してしまうことはない。
    /// </summary>
    public static class InteractPromptService
    {
        /// <summary>いま出ているラベル。何も出ていなければ null。</summary>
        public static string Label { get; private set; }

        /// <summary>いまプロンプトを出している対象(重複表示の調停用)。</summary>
        public static object Owner { get; private set; }

        /// <summary>ラベルが変わった(消えた場合は null)。</summary>
        public static event System.Action<string> LabelChanged;

        public static void Show(object owner, string label)
        {
            if (owner == null || string.IsNullOrEmpty(label))
                return;
            if (ReferenceEquals(Owner, owner) && Label == label)
                return;
            Owner = owner;
            Label = label;
            LabelChanged?.Invoke(Label);
        }

        /// <summary>自分が出しているプロンプトを消す(他人のものには触らない)。</summary>
        public static void Hide(object owner)
        {
            if (owner == null || !ReferenceEquals(Owner, owner))
                return;
            Owner = null;
            Label = null;
            LabelChanged?.Invoke(null);
        }

        /// <summary>シーン遷移などで持ち主ごと消えたとき用の強制クリア。</summary>
        public static void Clear()
        {
            if (Owner == null && Label == null)
                return;
            Owner = null;
            Label = null;
            LabelChanged?.Invoke(null);
        }
    }
}
