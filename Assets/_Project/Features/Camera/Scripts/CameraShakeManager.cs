// ■ セットアップ手順
//   1. シーン内の任意の GameObject（例: "CameraEffects"）にこのスクリプトをアタッチする
//   2. 同じ GameObject に CinemachineImpulseSource コンポーネントも追加し、
//      Inspector から _impulseSource フィールドに割り当てる
//   3. 揺らしたい Cinemachine Camera（メインカメラ・AimCamera 両方）に
//      CinemachineImpulseListener コンポーネントを追加する
//      Cinemachine 3.x では 1 つの Source から複数の Listener が同じシグナルを受け取れる。
//      ChannelMask は双方とも同じチャンネル（デフォルト: 1）に合わせておくこと。
//
// ■ Cinemachine バージョンについて
//   - Cinemachine 2.x (Unity 2022 以前) : using Cinemachine; を使用
//   - Cinemachine 3.x (Unity 6 以降)    : using Unity.Cinemachine; に変更してください
//      また CinemachineImpulseSource の型が変わる場合は適宜修正してください

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
