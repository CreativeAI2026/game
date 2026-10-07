// セットアップ: 任意の GameObject にアタッチし、CinemachineImpulseSource を追加して _impulseSource に割り当てる。
//   揺らしたいカメラ（メイン・AimCamera 両方）に CinemachineImpulseListener を追加し、ChannelMask を揃える（デフォルト: 1）。
// Cinemachine 2.x（Unity 2022 以前）は using Cinemachine;、3.x（Unity 6 以降）は using Unity.Cinemachine; を使う。

using Unity.Cinemachine;
using UnityEngine;

namespace CreativeAI.Gameplay
{
    public class CameraShakeManager : MonoBehaviour
    {
        // シーンに1つだけ存在するシングルトンインスタンス
        public static CameraShakeManager Instance { get; private set; }

        [Tooltip("CinemachineImpulseSource コンポーネントへの参照（同じ GameObject にアタッチ）")]
        [SerializeField]
        private CinemachineImpulseSource _impulseSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// 画面を揺らす。
        /// </summary>
        /// <param name="amplitude">揺れの大きさ（目安: 小=0.2 / 中=0.5 / 大=1.0）</param>
        public void Shake(float amplitude)
        {
            if (_impulseSource == null)
            {
                Debug.LogWarning("[CameraShakeManager] _impulseSource が設定されていません。");
                return;
            }
            _impulseSource.GenerateImpulse(amplitude);
        }
    }
}
