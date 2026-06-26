using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 被弾時の赤ビネット演出を管理する。
    /// 連続被弾時は前回の演出をキャンセルして再スタートすることで、
    /// 被弾のたびに視覚フィードバックをリセットする。
    /// </summary>
    public class DamageVignette : MonoBehaviour
    {
        public static DamageVignette Instance { get; private set; }

        [Tooltip("全画面を覆う赤いビネット用の Image コンポーネント")]
        [SerializeField]
        private Image _vignetteImage;

        [Tooltip("フェードインにかかる時間（秒）")]
        [SerializeField]
        private float _fadeInDuration = 0.08f;

        [Tooltip("最大透明度を維持する時間（秒）")]
        [SerializeField]
        private float _sustainDuration = 0.05f;

        [Tooltip("フェードアウトにかかる時間（秒）")]
        [SerializeField]
        private float _fadeOutDuration = 0.6f;

        [Tooltip("ビネットの最大アルファ値(0~1)")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _maxAlpha = 0.7f;

        private Coroutine _vignetteCoroutine;

        private void Start()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetAlpha(0f);
        }

        /// <summary>
        /// 被弾ビネット演出を開始する。PlayerStatus.TakeDamage() から呼び出される。
        /// 連続被弾時は前のコルーチンをキャンセルして最初からやり直す。
        /// </summary>
        public void TriggerVignette()
        {
            if (_vignetteImage == null)
            {
                Debug.LogWarning("[DamageVignette] _vignetteImage が設定されていません。");
                return;
            }

            if (_vignetteCoroutine != null)
                StopCoroutine(_vignetteCoroutine);

            _vignetteCoroutine = StartCoroutine(VignetteCoroutine());
        }

        // TODO : UniTask導入後に、これをUniTaskで実装しなおす
        private IEnumerator VignetteCoroutine()
        {
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(Mathf.Clamp01(elapsed / _fadeInDuration) * _maxAlpha);
                yield return null;
            }
            SetAlpha(_maxAlpha);

            yield return new WaitForSeconds(_sustainDuration);

            elapsed = 0f;
            while (elapsed < _fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(Mathf.Clamp01(1f - elapsed / _fadeOutDuration) * _maxAlpha);
                yield return null;
            }
            SetAlpha(0f);
            _vignetteCoroutine = null;
        }

        private void SetAlpha(float alpha)
        {
            if (_vignetteImage == null)
                return;
            Color c = _vignetteImage.color;
            c.a = Mathf.Clamp01(alpha);
            _vignetteImage.color = c;
        }
    }
}
