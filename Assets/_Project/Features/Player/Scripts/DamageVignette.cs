using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ■ セットアップ手順
//   1. Canvas 上に空の GameObject（例: "DamageVignette"）を作成し、このスクリプトをアタッチする
//   2. その GameObject の子（または同じ）に Image コンポーネントを追加する
//      - RectTransform : Stretch / Stretch（全画面を覆う）
//      - Color         : 赤 (R=1, G=0, B=0) で Alpha=0 に設定しておく
//      - Raycast Target: OFF にする（クリックを通過させるため）
//   3. Inspector の _vignetteImage フィールドにその Image を割り当てる

namespace CreativeAI.Gameplay
{
    public class DamageVignette : MonoBehaviour
    {
        // シーンに1つだけ存在するシングルトンインスタンス
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

        [Tooltip("ビネットの最大アルファ値（0～1）")]
        [SerializeField]
        [Range(0f, 1f)]
        private float _maxAlpha = 0.7f;

        private Coroutine _vignetteCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 初期状態は完全透明にしておく
            SetAlpha(0f);
        }

        /// <summary>
        /// 被弾ビネット演出を開始する。PlayerStatus.TakeDamage() から呼び出す。
        /// 連続で呼ばれた場合は前のコルーチンをキャンセルしてやり直す。
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

        private IEnumerator VignetteCoroutine()
        {
            // ─── フェードイン ───
            float elapsed = 0f;
            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.deltaTime;
                SetAlpha(Mathf.Clamp01(elapsed / _fadeInDuration) * _maxAlpha);
                yield return null;
            }
            SetAlpha(_maxAlpha);

            // ─── 持続 ───
            yield return new WaitForSeconds(_sustainDuration);

            // ─── フェードアウト ───
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
