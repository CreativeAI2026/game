using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoundKit.UGUI
{
    /// <summary>
    /// uGUI ボタンの選択音・決定音を鳴らす。ボタンに付ける（音そのものは UISoundSet の共通の音を使う）。
    ///
    /// 選択音は、キー・パッドでカーソルを動かしたときと、マウスを重ねたときだけ鳴らす。
    /// 画面を開いたときにプログラムが最初のボタンを選ぶ場合や、クリックで選ばれた場合まで鳴らすと、
    /// 開くたび・押すたびに選択音が重なってうるさいため。
    /// </summary>
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("SoundKit/UI Button Sound")]
    public class UIButtonSound : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        [Tooltip("オンにすると、押したときに決定音ではなく戻る音を鳴らす（閉じる・戻るボタン用）")]
        [SerializeField]
        private bool _isBackButton;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClicked);
        }

        private void OnClicked()
        {
            if (_isBackButton)
                UISounds.PlayBack(this);
            else
                UISounds.PlayDecide(this);
        }

        public void OnSelect(BaseEventData eventData)
        {
            // キー・パッドでのカーソル移動だけが AxisEventData で届く
            if (eventData is AxisEventData)
                UISounds.PlaySelect(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button.IsInteractable())
                UISounds.PlaySelect(this);
        }
    }
}
