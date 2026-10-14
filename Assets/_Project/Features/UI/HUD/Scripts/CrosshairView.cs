using UnityEngine;
using UnityEngine.UI;

namespace CreativeAI.Gameplay
{
    /// <summary>
    /// 弓を構えている間だけクロスヘアーを表示する。**クロスヘアーの Image と同じ GameObject に置く**(シーン側)。
    ///
    /// 以前は弓(プレイヤー Prefab)がシーンの Image を直接参照していたが、Prefab からシーンは参照できないため、
    /// UI 側が PlayerRegistry からプレイヤーを受け取って状態を見る形にした。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CrosshairView : MonoBehaviour
    {
        private Image _image;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _image.enabled = false;
        }

        private void Update()
        {
            // プレイヤーがまだ生成されていない間は null
            PlayerController player = PlayerRegistry.Current;
            _image.enabled = player != null && player.IsAiming;
        }
    }
}
