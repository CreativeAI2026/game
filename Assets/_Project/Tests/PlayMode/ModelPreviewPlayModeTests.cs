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
            Assert.Ignore("UI_Sandbox is an Editor test scene outside the build scene list.");
            yield break;
#endif
            yield return null;
            Assert.NotNull(
                PlayerRegistry.Current,
                "Direct Play of UI_Sandbox must create the player without test-side spawning"
            );
            _player = PlayerRegistry.Current.gameObject;
            _manager = _player.GetComponent<WeaponManager>();
            _router = UIRoot.Instance.GetComponentInChildren<UiRouter>(true);
            Assert.NotNull(_router);
            _router.Open(UiRouter.UiId.Character);
            yield return null;
            _controller = UIRoot.Instance.GetComponentInChildren<CharacterPreviewController>(true);
            Assert.NotNull(_controller, "CharacterPanel prefab must contain preview references");
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
                "unarmed preview"
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
                "The UI scene's camera must be reused rather than creating a second MainCamera"
            );
            Assert.False(_selector.gameObject.activeSelf);
            Assert.False(_bar.gameObject.activeSelf);
            foreach (
                var stage in Object
                    .FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .Where(c => c.name == "Preview Camera")
            )
            {
                Assert.False(
                    stage.enabled,
                    "Preview cameras must render only on explicit requests"
                );
                Assert.AreEqual(
                    UIRoot.Instance.gameObject.scene,
                    stage.gameObject.scene,
                    "Preview resources must survive field scene transitions with the resident UI"
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
                "sword equipped"
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
                "Preview must receive pointer input ahead of the panel background"
            );

            _manager.Unequip();
            yield return PlayModeWait.Until(
                () => _character.MeshCount == _bareMeshes,
                "unequipped character"
            );
            Assert.True(_selector.gameObject.activeSelf, "Still owned, so the selector stays");
            Assert.True(_bar.gameObject.activeSelf);

            _manager.RestoreWeapons(System.Array.Empty<string>(), WeaponManager.NoWeapon);
            yield return PlayModeWait.Until(() => !_bar.gameObject.activeSelf, "nothing owned");
            Assert.AreEqual(_bareMeshes, _character.MeshCount);
            Assert.IsNull(_weapon.Texture);

            _manager.GiveWeapon("hammer");
            yield return PlayModeWait.Until(() => _bar.gameObject.activeSelf, "hammer owned");
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
            Assert.AreEqual(beforeYaw - 40, _character.Yaw, .01f, "Right drag does nothing");
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
                Assert.Greater(
                    _weapon.MeshCount,
                    0,
                    "Selection must update the mesh synchronously"
                );
                yield return PlayModeWait.Until(
                    () => _controller.PreviewWeaponId == id,
                    "preview pose follows selection " + id
                );
                Assert.AreEqual(
                    equippedBeforeBrowsing,
                    _manager.CurrentWeaponIndex,
                    "Browsing must not alter the live player equipment"
                );
                Assert.True(
                    _controller.PreviewIsAnimating,
                    "All three melee weapons share the idle"
                );
                Assert.False(_controller.PreviewIsAttacking, "Weapon changes cancel attacks");

                double animationTime = _controller.PreviewAnimationTime;
                var bones = PoseBones();
                var idleStart = bones.Select(b => b.localRotation).ToArray();
                yield return PlayModeWait.Until(
                    () => AnyBoneMoved(bones, idleStart, .05f),
                    "idle animates the preview bones"
                );
                Assert.Greater(_controller.PreviewAnimationTime, animationTime);

                var pointer = Click(PointerEventData.InputButton.Left);
                _character.OnPointerDown(pointer);
                _character.OnDrag(Drag(PointerEventData.InputButton.Left, 20));
                _character.OnPointerClick(pointer);
                Assert.False(
                    _controller.PreviewIsAttacking,
                    "Dragging must never trigger an attack"
                );

                _character.OnPointerDown(pointer);
                _character.OnPointerClick(pointer);
                Assert.True(
                    _controller.PreviewIsAttacking,
                    "A left click starts the selected weapon's normal melee attack"
                );
                var attackStart = bones.Select(b => b.localRotation).ToArray();
                yield return PlayModeWait.Until(
                    () => AnyBoneMoved(bones, attackStart, 5f),
                    "attack visibly moves the preview bones"
                );
                AssertVisible(_character, "preview-attack-" + id);
                Assert.AreEqual(beforePosition, _player.transform.position);
                Assert.AreEqual(beforeRotation, _player.transform.rotation);

                yield return PlayModeWait.Until(
                    () => !_controller.PreviewIsAttacking,
                    "attack finishes and returns to idle"
                );
                Assert.True(_controller.PreviewIsAnimating);

                var right = Click(PointerEventData.InputButton.Right);
                _character.OnPointerDown(right);
                _character.OnPointerClick(right);
                Assert.False(_controller.PreviewIsAttacking, "Right click does not attack");
                AssertVisible(_character, "preview-character-weapon-" + id);
                AssertVisible(_weapon, "preview-weapon-" + id);
            }
        }

        [UnityTest]
        public IEnumerator WeaponChange_CancelsAttack_AndBowShowsItsModel()
        {
            GiveAll();
            SelectInSelector(Sword);
            yield return PlayModeWait.Until(
                () => _controller.PreviewWeaponId == Sword,
                "sword pose"
            );
            ClickAttack();
            Assert.True(_controller.PreviewIsAttacking);

            SelectInSelector(Bow);
            yield return PlayModeWait.Frames();
            Assert.False(_controller.PreviewIsAttacking, "Weapon changes cancel attacks");
            Assert.Greater(_weapon.MeshCount, 0);
            Assert.False(_weapon.transform.parent.Find("ModelUnavailable").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator WeaponPreview_SpinsAtConfiguredSpeed()
        {
            _manager.GiveWeapon("sword");
            yield return PlayModeWait.Until(() => _weapon.MeshCount > 0, "sword preview");

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
                "all weapons given"
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
                "all weapons removed"
            );
            Assert.AreEqual(0, _manager.OwnedCount);
            Assert.False(_selector.gameObject.activeSelf);
        }
#endif

        [UnityTest]
        public IEnumerator Attack_FinishesWhileGameTimeIsPaused()
        {
            _manager.GiveWeapon("sword");
            yield return PlayModeWait.Until(
                () => _controller.PreviewWeaponId == Sword,
                "sword pose"
            );
            var pauseScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                ClickAttack();
                Assert.True(_controller.PreviewIsAttacking);
                yield return PlayModeWait.Until(
                    () => !_controller.PreviewIsAttacking,
                    "attack returns to idle while game time is paused"
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
            yield return PlayModeWait.Until(
                () => _controller.PreviewWeaponId == Sword,
                "sword pose"
            );
            ClickAttack();
            Assert.True(_controller.PreviewIsAttacking);
            var oldTexture = _character.Texture;

            _router.CloseAll();
            Assert.IsNull(_character.Texture);
            Assert.False(_controller.PreviewIsAttacking, "Closing disposes active attack playback");
            Assert.True(
                _player.GetComponentsInChildren<Renderer>(true).All(r => r.forceRenderingOff),
                "The UI-only source player must never be visible outside the panel"
            );
            Assert.False(oldTexture.IsCreated());

            yield return null;
            _router.Open(UiRouter.UiId.Character);
            yield return PlayModeWait.Until(
                () => _character.Texture != null && _character.MeshCount > 0,
                "reopened preview"
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
                    "Preview must contain rendered geometry"
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
