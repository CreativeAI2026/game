namespace CreativeAI.Core
{
    /// <summary>
    /// 合成画面を開く依頼の静的サービス。ワールド側(Gameplay)は UI を参照できないので、
    /// ワールド側は依頼するだけ、UI 側は購読して開くだけにする(<see cref="InteractPromptService"/> と同じ考え方)。
    /// </summary>
    public static class CraftUiRequestService
    {
        /// <summary>合成画面を開く依頼が出た。</summary>
        public static event System.Action Requested;

        public static void Request() => Requested?.Invoke();
    }
}
