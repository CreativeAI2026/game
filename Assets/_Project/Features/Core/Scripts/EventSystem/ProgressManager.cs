using System;
using System.Collections.Generic;
using UnityEngine;

namespace CreativeAI.Core.EventSystem
{
    /// <summary>
    /// メインストーリーの進行度(整数1つ)とフラグ(key→値)を保持する常駐 SSOT。
    /// 状態を持つだけで、イベントの中身は知らない。進行度・フラグを読ませ／書かせ、
    /// 変わったら通知する。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ProgressManager : MonoBehaviour
    {
        public static ProgressManager Instance { get; private set; }

        /// <summary>メイン進行度。EventTrigger / NpcVisibility が条件評価のため読む。</summary>
        public int Progress { get; private set; }

        /// <summary>進行度が変わったときに通知(引数なし。購読側は Progress を読み直す)。</summary>
        public event Action OnProgressChanged;

        private readonly Dictionary<string, string> _flags = new();

        /// <summary>保存用にフラグ全体を読む(スナップショット)。</summary>
        public IReadOnlyDictionary<string, string> Flags => _flags;

        /// <summary>
        /// ロード時に進行度・フラグをまとめて復元する。値の異同に関わらず OnProgressChanged を通知する
        /// (購読側に読み直させる)。
        /// </summary>
        public void LoadState(int progress, IReadOnlyDictionary<string, string> flags)
        {
            _flags.Clear();
            if (flags != null)
            {
                foreach (var kv in flags)
                    if (!string.IsNullOrEmpty(kv.Key))
                        _flags[kv.Key] = kv.Value;
            }
            Progress = progress;
            OnProgressChanged?.Invoke();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>choice で書いた分岐結果を読む。未設定キーは空文字を返す。</summary>
        public string GetFlag(string key) =>
            key != null && _flags.TryGetValue(key, out var value) ? value : string.Empty;

        /// <summary>choice ステップで進行側(EventPlayer)がフラグを書く。</summary>
        public void SetFlag(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[ProgressManager] SetFlag ignored: key is null or empty.");
                return;
            }
            _flags[key] = value;
        }

        /// <summary>
        /// イベント終了時に進行側(EventPlayer)が進行度を進める。
        /// 値が変わったときだけ OnProgressChanged を通知する。
        /// デバッグで巻き戻せるよう、後退は弾かない。
        /// </summary>
        public void AdvanceTo(int progress)
        {
            if (progress == Progress)
                return;
            Progress = progress;
            OnProgressChanged?.Invoke();
        }
    }
}
