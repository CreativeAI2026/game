using System;
using CreativeAI.Core;
using UnityEngine;

namespace CreativeAI.Playtest
{
    /// <summary>
    /// 戦闘が始まる前(準備エリアにいる間)だけ、「ここで装備を整えてから戦闘ステージへ進む場所」だと画面上部に説明を出す。
    /// 所持品やキャラ画面を開いている間は、画面に重ならないよう隠す。
    /// </summary>
    [RequireComponent(typeof(PlaytestBattleStage))]
    public sealed class PlaytestPrepGuide : MonoBehaviour
    {
        // PlaytestSceneSwitcher のキー案内(画面最上部)の下に出す
        private const float TopMargin = 48f;
        private const int FontSize = 18;
        private const int Padding = 12;

        [Tooltip("1行目: この場所の説明")]
        [SerializeField]
        private string _title = "準備エリア：ここで装備を整えてから、戦闘ステージへ進んでください";

        [Tooltip("2行目: 準備に使う操作")]
        [SerializeField]
        private string _controls =
            "C: キャラ画面（装備の付け替え）   I: 所持品   Q / E: 武器の切り替え   1 / 2 / 3: 回復";

        [Tooltip("3行目: 戦闘ステージへの入り方(シーンごとに書き換える)")]
        [SerializeField]
        private string _entry = "先へ進むと戦闘が始まります。戦闘中は準備エリアへ戻れません";

        private PlaytestBattleStage _stage;
        private GUIContent _content;

        // GUIStyle は保存できる型なので、付けないとエディターの再読み込みで空のスタイルが入り、下の ??= で作り直されなくなる
        [NonSerialized]
        private GUIStyle _style;

        private void Awake()
        {
            _stage = GetComponent<PlaytestBattleStage>();
            _content = new GUIContent($"{_title}\n{_controls}\n{_entry}");
        }

        private void OnGUI()
        {
            if (_stage.HasStarted || UiPanelService.IsOpen)
                return;

            _style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = FontSize,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(Padding, Padding, Padding, Padding),
            };
            Vector2 size = _style.CalcSize(_content);
            var rect = new Rect((Screen.width - size.x) * 0.5f, TopMargin, size.x, size.y);
            GUI.Box(rect, _content, _style);
        }
    }
}
