using System.Collections;
using CreativeAI.Gameplay;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace CreativeAI.UI
{
    /// <summary>
    /// 被弾時の赤ビネット演出を管理する(シーン側の UI)。
    /// 連続被弾時は前回の演出をキャンセルして再スタートすることで、
    /// 被弾のたびに視覚フィードバックをリセットする。
    ///
    /// プレイヤーは UI を知らず、この UI が PlayerRegistry からプレイヤーを受け取って
    /// PlayerStatus.Damaged を購読する(プレイヤーが後から生成されても購読できる)。
    /// </summary>
    public class DamageVignetteView : MonoBehaviour
    {
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
        private PlayerStatus _status;

        private void Awake()
        {
            Assert.IsNotNull(_vignetteImage, $"{name}: _vignetteImage が未設定です");
            SetAlpha(0f);
        }

        private void OnEnable()
        {
            PlayerRegistry.Changed += Bind;
            Bind(PlayerRegistry.Current);
        }

        private void OnDisable()
        {
            PlayerRegistry.Changed -= Bind;
            Bind(null);
        }

        /// <summary>購読先のプレイヤーを切り替える(未生成・破棄時は null)。</summary>
        private void Bind(PlayerController player)
        {
            if (_status != null)
                _status.Damaged -= TriggerVignette;

            _status = player != null ? player.GetComponent<PlayerStatus>() : null;

            if (_status != null)
                _status.Damaged += TriggerVignette;
        }

        /// <summary>
        /// 被弾ビネット演出を開始する。PlayerStatus.Damaged から呼ばれる。
        /// 連続被弾時は前のコルーチンをキャンセルして最初からやり直す。
        /// </summary>
        private void TriggerVignette()
        {
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
            Color c = _vignetteImage.color;
            c.a = Mathf.Clamp01(alpha);
            _vignetteImage.color = c;
        }
    }
}
