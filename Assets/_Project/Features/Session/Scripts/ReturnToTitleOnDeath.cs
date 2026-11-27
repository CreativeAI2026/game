using System.Collections;
using CreativeAI.Gameplay;
using UnityEngine;

namespace CreativeAI.Session
{
    /// <summary>
    /// プレイヤーが倒れたら、倒れる動きを見せてからタイトルへ戻す(「続きから」で直近のセーブから再開する)。
    /// どこで倒れても同じ流れにする。倒れたときの動き・操作停止は PlayerDeathHandler が受け持つ。
    /// Prefab には付けず、タイトルから始めたプレイでだけ <see cref="GameStarter"/> が生成したプレイヤーに付ける
    /// (開発用の直接 Play・体験用ビルドでは倒れたままにする)。
    /// </summary>
    [RequireComponent(typeof(PlayerStatus))]
    public sealed class ReturnToTitleOnDeath : MonoBehaviour
    {
        // 倒れる動きを見せてから、タイトルへ戻し始めるまでの秒数
        private const float ReturnDelaySeconds = 3f;

        private PlayerStatus _status;
        private bool _returning;

        private void Awake() => _status = GetComponent<PlayerStatus>();

        private void OnEnable() => _status.Died += OnDied;

        private void OnDisable() => _status.Died -= OnDied;

        private void OnDied()
        {
            if (_returning)
                return;
            _returning = true;
            StartCoroutine(ReturnToTitleAfterDelay());
        }

        private static IEnumerator ReturnToTitleAfterDelay()
        {
            yield return new WaitForSeconds(ReturnDelaySeconds);
            GameStarter.ReturnToTitle();
        }
    }
}
