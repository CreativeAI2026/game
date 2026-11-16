using UnityEngine;

namespace CreativeAI.Core
{
    /// <summary>
    /// マップピンとピンまでの道筋の見た目(画像・材質)をまとめた設定アセット。
    /// ピンも道筋もコードで生成するので、参照はこのアセット1つに集め、Resources から読む。
    /// </summary>
    [CreateAssetMenu(fileName = "MapPinVisuals", menuName = "CreativeAI/Map Pin Visuals")]
    public sealed class MapPinVisuals : ScriptableObject
    {
        private const string ResourcePath = "MapPinVisuals";
        private static MapPinVisuals _instance;

        [SerializeField]
        private Sprite _pinSprite;

        [Tooltip("壁に隠されず常に手前に描く材質")]
        [SerializeField]
        private Material _pinMaterial;

        [SerializeField]
        private Material _pathLineMaterial;

        /// <summary>Resources から読んだ設定。見つからなければ警告して null。</summary>
        public static MapPinVisuals Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;
                _instance = Resources.Load<MapPinVisuals>(ResourcePath);
                if (_instance == null)
                    Debug.LogWarning($"[MapPin] Resources/{ResourcePath} が見つかりません。");
                return _instance;
            }
        }

        /// <summary>ピンの画像。</summary>
        public Sprite PinSprite => _pinSprite;

        /// <summary>ピンの材質。</summary>
        public Material PinMaterial => _pinMaterial;

        /// <summary>ピンまでの道筋の線の材質。</summary>
        public Material PathLineMaterial => _pathLineMaterial;
    }
}
