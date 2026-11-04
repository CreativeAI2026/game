using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using CreativeAI.UI.CharacterUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace CreativeAI.Tests.PlayMode
{
    /// <summary>
    /// 本物のキャラクター画面で3Dプレビューが所持・装備・選択・ドラッグに追従するかの検証。
    /// UI_Sandbox シーンで実 Prefab を動かし、プレビュー用カメラが要求時だけ描画することも見る。
    /// プレビューは dirty フラグを LateUpdate でまとめて反映するので、秒数ではなく状態が揃うまで待つ。
    /// </summary>
    public class ModelPreviewPlayModeTests
    {
        private const int Sword = 1001;
        private const int Bow = 1002;
        private const int Scythe = 1003;
        private const int Hammer = 1004;

        private GameObject _player;
        private WeaponManager _manager;
        private UiRouter _router;
        private CharacterPreviewController _controller;
        private ModelPreview _character;
        private ModelPreview _weapon;
        private CharacterWeaponSelector _selector;
        private Transform _bar;
        private int _bareMeshes;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/UI/UI_Sandbox.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(
                    UnityEngine.SceneManagement.LoadSceneMode.Single
                )
            );
#else
            Assert.Ignore("UI_Sandbox はビルド対象外の Editor 用テストシーン");
            yield break;
#endif
            yield return null;
            Assert.NotNull(
                PlayerRegistry.Current,
                "UI_Sandbox を直接再生すると、テスト側で生成しなくてもプレイヤーが立つ"
            );
            _player = PlayerRegistry.Current.gameObject;
            _manager = _player.GetComponent<WeaponManager>();
            _router = UIRoot.Instance.GetComponentInChildren<UiRouter>(true);
            Assert.NotNull(_router);
            _router.Open(UiRouter.UiId.Character);
            yield return null;
            _controller = UIRoot.Instance.GetComponentInChildren<CharacterPreviewController>(true);
            Assert.NotNull(_controller, "CharacterPanel Prefab にプレビューの参照が入っている");
            var previews = _controller.GetComponentsInChildren<ModelPreview>(true);
            _character = previews.Single(p => p.name == "Character3DPreview");
            _weapon = previews.Single(p => p.name == "Weapon3DPreview");
            _selector = _controller.GetComponentInChildren<CharacterWeaponSelector>(true);
            _bar = _controller
                .GetComponentsInChildren<Transform>(true)
                .Single(t => t.name == "WeaponChangeBar");

            _manager.RestoreWeapons(System.Array.Empty<string>(), WeaponManager.NoWeapon);
            yield return PlayModeWait.Until(
                () => !_bar.gameObject.activeSelf && _character.MeshCount > 0,
                "素手のプレビュー"
            );
            yield return PlayModeWait.Frames();
            _bareMeshes = _character.MeshCount;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_router != null)
                _router.CloseAll();
            if (_player != null)
                foreach (var root in _player.scene.GetRootGameObjects())
                    Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Unarmed_HidesWeaponUi_AndRendersOnlyOnRequest()
        {
            Assert.AreEqual(
                1,
                Object
                    .FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .Count(c => c.CompareTag("MainCamera")),
                "MainCamera を2つ目として作らず、UI シーンのカメラを使い回す"
            );
            Assert.False(_selector.gameObject.activeSelf);
            Assert.False(_bar.gameObject.activeSelf);
            foreach (
                var stage in Object
                    .FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .Where(c => c.name == "Preview Camera")
            )
            {
                Assert.False(stage.enabled, "プレビュー用カメラは要求したときだけ描画する");
                Assert.AreEqual(
                    UIRoot.Instance.gameObject.scene,
                    stage.gameObject.scene,
                    "プレビュー用の資源は常駐 UI と一緒にシーン遷移をまたぐ"
                );
            }
            Assert.IsNull(_weapon.Texture);
            AssertVisible(_character, "preview-character-unarmed");
            yield break;
        }

        [UnityTest]
        public IEnumerator Ownership_TogglesWeaponUiAndCharacterMeshes()
        {
            _manager.GiveWeapon("sword");
            yield return PlayModeWait.Until(
                () => _bar.gameObject.activeSelf && _character.MeshCount > _bareMeshes,
                "剣を装備"
            );
            Assert.True(_selector.gameObject.activeSelf);
            Assert.AreEqual(Sword, _selector.SelectedWeapon.id);
            Assert.Greater(_weapon.MeshCount, 0);
            AssertVisible(_character, "preview-character-equipped");
            AssertVisible(_weapon, "preview-weapon-sword");

            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(
                        null,
                        _character.transform.position
                    ),
                },
                hits
            );
            Assert.IsNotEmpty(hits);
            Assert.AreEqual(
                _character.gameObject,
                hits[0].gameObject,
                "ポインタ入力はパネル背景より先にプレビューが受け取る"
            );

            _manager.Unequip();
            yield return PlayModeWait.Until(
                () => _character.MeshCount == _bareMeshes,
                "装備を外したキャラクター"
            );
            Assert.True(_selector.gameObject.activeSelf, "所持はしているので武器選択は出たまま");
            Assert.True(_bar.gameObject.activeSelf);

            _manager.RestoreWeapons(System.Array.Empty<string>(), WeaponManager.NoWeapon);
            yield return PlayModeWait.Until(
                () => !_bar.gameObject.activeSelf,
                "何も所持していない"
            );
            Assert.AreEqual(_bareMeshes, _character.MeshCount);
            Assert.IsNull(_weapon.Texture);

            _manager.GiveWeapon("hammer");
            yield return PlayModeWait.Until(() => _bar.gameObject.activeSelf, "ハンマーを所持");
            Assert.True(_selector.gameObject.activeSelf);
            Assert.AreEqual(Hammer, _selector.SelectedWeapon.id);
            Assert.Greater(_weapon.MeshCount, 0);
        }

        [UnityTest]
        public IEnumerator Drag_RotatesOnlyWithLeftButton_AndLeavesPlayerAlone()
        {
            var beforePosition = _player.transform.position;
            var beforeRotation = _player.transform.rotation;
            float beforeYaw = _character.Yaw;

            _character.OnDrag(Drag(PointerEventData.InputButton.Left, 100));
            Assert.AreEqual(beforeYaw - 40, _character.Yaw, .01f);
            Assert.AreEqual(beforePosition, _player.transform.position);
            Assert.AreEqual(beforeRotation, _player.transform.rotation);

            _character.OnDrag(Drag(PointerEventData.InputButton.Right, 100));
            Assert.AreEqual(beforeYaw - 40, _character.Yaw, .01f, "右ドラッグでは回らない");
            yield break;
        }

        [UnityTest]
        public IEnumerator BrowsingMeleeWeapons_AnimatesIdleAndAttack_WithoutChangingEquipment()
        {
            GiveAll();
            _manager.SelectNext();
            var beforePosition = _player.transform.position;
            var beforeRotation = _player.transform.rotation;
            int equippedBeforeBrowsing = _manager.CurrentWeaponIndex;

            foreach (int id in new[] { Scythe, Hammer, Sword })
            {
                SelectInSelector(id);
                Assert.Greater(_weapon.MeshCount, 0, "選んだその場でメッシュが切り替わる");
                yield return PlayModeWait.Until(
                    () => _controller.PreviewWeaponId == id,
                    "プレビューの姿勢が選択に追従する " + id
                );
                Assert.AreEqual(
                    equippedBeforeBrowsing,
                    _manager.CurrentWeaponIndex,
                    "選んで見るだけでは実際の装備は変わらない"
                );
                Assert.True(
                    _controller.PreviewIsAnimating,
                    "近接武器3種は共通の待機モーションで動く"
                );
                Assert.False(_controller.PreviewIsAttacking, "武器を切り替えると攻撃は中断する");

                double animationTime = _controller.PreviewAnimationTime;
                var bones = PoseBones();
                var idleStart = bones.Select(b => b.localRotation).ToArray();
                yield return PlayModeWait.Until(
                    () => AnyBoneMoved(bones, idleStart, .05f),
                    "待機モーションでボーンが動く"
                );
                Assert.Greater(_controller.PreviewAnimationTime, animationTime);

                var pointer = Click(PointerEventData.InputButton.Left);
                _character.OnPointerDown(pointer);
                _character.OnDrag(Drag(PointerEventData.InputButton.Left, 20));
                _character.OnPointerClick(pointer);
                Assert.False(_controller.PreviewIsAttacking, "ドラッグでは攻撃しない");

                _character.OnPointerDown(pointer);
                _character.OnPointerClick(pointer);
                Assert.True(
                    _controller.PreviewIsAttacking,
                    "左クリックで選んだ武器の通常攻撃が始まる"
                );
                var attackStart = bones.Select(b => b.localRotation).ToArray();
                yield return PlayModeWait.Until(
                    () => AnyBoneMoved(bones, attackStart, 5f),
                    "攻撃でボーンが目に見えて動く"
                );
                AssertVisible(_character, "preview-attack-" + id);
                Assert.AreEqual(beforePosition, _player.transform.position);
                Assert.AreEqual(beforeRotation, _player.transform.rotation);

                yield return PlayModeWait.Until(
                    () => !_controller.PreviewIsAttacking,
                    "攻撃が終わって待機に戻る"
                );
                Assert.True(_controller.PreviewIsAnimating);

                var right = Click(PointerEventData.InputButton.Right);
                _character.OnPointerDown(right);
                _character.OnPointerClick(right);
                Assert.False(_controller.PreviewIsAttacking, "右クリックでは攻撃しない");
                AssertVisible(_character, "preview-character-weapon-" + id);
                AssertVisible(_weapon, "preview-weapon-" + id);
            }
        }

        [UnityTest]
        public IEnumerator WeaponChange_CancelsAttack_AndBowShowsItsModel()
        {
            GiveAll();
            SelectInSelector(Sword);
            yield return PlayModeWait.Until(() => _controller.PreviewWeaponId == Sword, "剣の姿勢");
            ClickAttack();
            Assert.True(_controller.PreviewIsAttacking);

            SelectInSelector(Bow);
            yield return PlayModeWait.Frames();
            Assert.False(_controller.PreviewIsAttacking, "武器を切り替えると攻撃は中断する");
            Assert.Greater(_weapon.MeshCount, 0);
            Assert.False(_weapon.transform.parent.Find("ModelUnavailable").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator WeaponPreview_SpinsAtConfiguredSpeed()
        {
            _manager.GiveWeapon("sword");
            yield return PlayModeWait.Until(() => _weapon.MeshCount > 0, "剣のプレビュー");

            // コルーチンは LateUpdate より前に動くので、直後の LateUpdate が足すのは記録前のフレームの時間になる。
            // 経過時刻で比べると重いフレームのぶんずれるため、LateUpdate が足した時間そのものを合計する
            float yaw = _weapon.Yaw;
            float elapsed = 0f;
            float end = Time.realtimeSinceStartup + .5f;
            do
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            } while (Time.realtimeSinceStartup < end);
            Assert.AreEqual(20 * elapsed, _weapon.Yaw - yaw, .1f);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator PreviewWeaponsMenu_GivesAndRemovesAllWeapons()
        {
            Assert.True(
                UnityEditor.EditorApplication.ExecuteMenuItem(
                    "Tools/CreativeAI/UI/Preview Weapons/All"
                )
            );
            yield return PlayModeWait.Until(
                () => _bar.gameObject.activeSelf && _character.MeshCount > _bareMeshes,
                "全武器を付与"
            );
            Assert.AreEqual(4, _manager.OwnedCount);
            Assert.True(_selector.gameObject.activeSelf);

            Assert.True(
                UnityEditor.EditorApplication.ExecuteMenuItem(
                    "Tools/CreativeAI/UI/Preview Weapons/None"
                )
            );
            yield return PlayModeWait.Until(
                () => !_bar.gameObject.activeSelf && _character.MeshCount == _bareMeshes,
                "全武器を削除"
            );
            Assert.AreEqual(0, _manager.OwnedCount);
            Assert.False(_selector.gameObject.activeSelf);
        }
#endif

        [UnityTest]
        public IEnumerator Attack_FinishesWhileGameTimeIsPaused()
        {
            _manager.GiveWeapon("sword");
            yield return PlayModeWait.Until(() => _controller.PreviewWeaponId == Sword, "剣の姿勢");
            var pauseScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                ClickAttack();
                Assert.True(_controller.PreviewIsAttacking);
                yield return PlayModeWait.Until(
                    () => !_controller.PreviewIsAttacking,
                    "ゲーム内時間が止まっていても攻撃は終わって待機に戻る"
                );
            }
            finally
            {
                Time.timeScale = pauseScale;
            }
        }

        [UnityTest]
        public IEnumerator Close_DisposesPreview_AndReopenRendersAgain()
        {
            _manager.GiveWeapon("sword");
            yield return PlayModeWait.Until(() => _controller.PreviewWeaponId == Sword, "剣の姿勢");
            ClickAttack();
            Assert.True(_controller.PreviewIsAttacking);
            var oldTexture = _character.Texture;

            _router.CloseAll();
            Assert.IsNull(_character.Texture);
            Assert.False(_controller.PreviewIsAttacking, "閉じると再生中の攻撃も破棄する");
            Assert.True(
                _player.GetComponentsInChildren<Renderer>(true).All(r => r.forceRenderingOff),
                "UI 用の元プレイヤーはパネルの外では見えない"
            );
            Assert.False(oldTexture.IsCreated());

            yield return null;
            _router.Open(UiRouter.UiId.Character);
            yield return PlayModeWait.Until(
                () => _character.Texture != null && _character.MeshCount > 0,
                "開き直したプレビュー"
            );
            yield return PlayModeWait.Frames();
            AssertVisible(_character, "preview-reopened");
        }

        private void GiveAll()
        {
            foreach (var key in new[] { "sword", "scythe", "hammer", "bow" })
                _manager.GiveWeapon(key);
        }

        private void SelectInSelector(int id)
        {
            for (int i = 0; i < 4; i++)
            {
                _selector.Select(i);
                if (_selector.SelectedWeapon.id == id)
                    break;
            }
            Assert.AreEqual(id, _selector.SelectedWeapon.id);
        }

        private void ClickAttack()
        {
            var click = Click(PointerEventData.InputButton.Left);
            _character.OnPointerDown(click);
            _character.OnPointerClick(click);
        }

        private static PointerEventData Click(PointerEventData.InputButton button) =>
            new(EventSystem.current) { button = button };

        private static PointerEventData Drag(PointerEventData.InputButton button, float dx) =>
            new(EventSystem.current) { button = button, delta = new Vector2(dx, 0) };

        private static Transform[] PoseBones() =>
            Resources
                .FindObjectsOfTypeAll<Animator>()
                .Single(a => a.name == "UI Character Pose" && a.gameObject.scene.IsValid())
                .GetComponentsInChildren<Transform>();

        private static bool AnyBoneMoved(Transform[] bones, Quaternion[] from, float degrees) =>
            bones.Where((bone, i) => Quaternion.Angle(bone.localRotation, from[i]) > degrees).Any();

        private static void AssertVisible(ModelPreview preview, string fileName)
        {
            Assert.NotNull(preview.Texture);
            var old = RenderTexture.active;
            var image = new Texture2D(
                preview.Texture.width,
                preview.Texture.height,
                TextureFormat.RGBA32,
                false
            );
            try
            {
                RenderTexture.active = preview.Texture;
                image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
                image.Apply();
                var pixels = image.GetPixels32();
                Assert.Greater(
                    pixels.Count(p => p.a > 20),
                    100,
                    "プレビューに描画された形が映っている"
                );
                Directory.CreateDirectory("Temp/ModelPreviewEvidence");
                File.WriteAllBytes(
                    "Temp/ModelPreviewEvidence/" + fileName + ".png",
                    image.EncodeToPNG()
                );
            }
            finally
            {
                RenderTexture.active = old;
                Object.Destroy(image);
            }
        }
    }
}
