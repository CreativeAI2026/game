using UnityEngine;

namespace CreativeAI.Session
{
    /// <summary>
    /// セッション常駐(UIRoot・カメラリグ・プレイヤーリグ)の Prefab 参照を1箇所に集約する設定アセット。
    /// Resources から読み、タイトルからの開始(<see cref="GameStarter"/>)と
    /// 開発用の直接 Play(<see cref="FieldDevBootstrap"/>)の両方が使う。
    /// </summary>
    [CreateAssetMenu(
        fileName = "ResidentBootstrapConfig",
        menuName = "CreativeAI/Resident Bootstrap Config"
    )]
    public sealed class ResidentBootstrapConfig : ScriptableObject
    {
        [Tooltip(
            "セッション常駐の UI レイヤー(UIRoot Prefab)。会話UI・即時食材使用UI も UIRoot が子として束ねる"
        )]
        public GameObject uiRootPrefab;

        [Tooltip("プレイヤーリグ Prefab")]
        public GameObject playerRigPrefab;

        [Tooltip(
            "カメラリグ Prefab(Main Camera + Brain、通常/エイムカメラ)。プレイヤーリグとは別に常駐させる"
        )]
        public GameObject cameraRigPrefab;

        /// <summary>Resources から読む。見つからなければ警告して null。</summary>
        public static ResidentBootstrapConfig Load()
        {
            var config = Resources.Load<ResidentBootstrapConfig>(nameof(ResidentBootstrapConfig));
            if (config == null)
                Debug.LogWarning(
                    "[ResidentBootstrapConfig] Resources に見つかりません。"
                        + "Assets > Create > CreativeAI > Resident Bootstrap Config で Resources に作成してください。"
                );
            return config;
        }
    }
}
