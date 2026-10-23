using System.Collections;
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
    /// </summary>
    public class ModelPreviewPlayModeTests
    {
        [UnityTest]
        public IEnumerator RealPrefabs_OwnershipEquipmentSelectionDragAndLifetime()
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
            var player = PlayerRegistry.Current.gameObject;
            Assert.NotNull(EventSystem.current);
            Assert.AreEqual(
                1,
                Object
                    .FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .Count(c => c.CompareTag("MainCamera")),
                "The UI scene's camera must be reused rather than creating a second MainCamera"
            );
            var manager = player.GetComponent<WeaponManager>();
            var router = UIRoot.Instance.GetComponentInChildren<UiRouter>(true);
            Assert.NotNull(router);
            router.Open(UiRouter.UiId.Character);
            yield return null;
            var controller = UIRoot.Instance.GetComponentInChildren<CharacterPreviewController>(
                true
            );
            Assert.NotNull(controller, "CharacterPanel prefab must contain preview references");
            var previews = controller.GetComponentsInChildren<ModelPreview>(true);
            var character = previews.Single(p => p.name == "Character3DPreview");
            var weapon = previews.Single(p => p.name == "Weapon3DPreview");
            var selector = controller.GetComponentInChildren<CharacterWeaponSelector>(true);
            var bar = controller
                .GetComponentsInChildren<Transform>(true)
                .Single(t => t.name == "WeaponChangeBar");

            manager.RestoreWeapons(System.Array.Empty<string>(), WeaponManager.NoWeapon);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.False(selector.gameObject.activeSelf);
            Assert.False(bar.gameObject.activeSelf);
            Assert.Greater(character.MeshCount, 0);
            int bareMeshes = character.MeshCount;
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
            Assert.IsNull(weapon.Texture);
            AssertVisible(character, "preview-character-unarmed");
            manager.GiveWeapon("sword");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.True(selector.gameObject.activeSelf);
            Assert.True(bar.gameObject.activeSelf);
            Assert.AreEqual(1001, selector.SelectedWeapon.id);
            Assert.Greater(weapon.MeshCount, 0);
            Assert.Greater(character.MeshCount, bareMeshes);
            AssertVisible(character, "preview-character-equipped");
            AssertVisible(weapon, "preview-weapon-sword");
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(
                        null,
                        character.transform.position
                    ),
                },
                hits
            );
            Assert.IsNotEmpty(hits);
            Assert.AreEqual(
                character.gameObject,
                hits[0].gameObject,
                "Preview must receive pointer input ahead of the panel background"
            );
            if (File.Exists("Temp/preview-interactive.request"))
            {
                File.Delete("Temp/preview-interactive.request");
                float interactiveYaw = character.Yaw;
                File.WriteAllText(
                    "Temp/preview-interactive.ready",
                    "Drag character in Game view now"
                );
                yield return new WaitForSecondsRealtime(50);
                Assert.AreNotEqual(
                    interactiveYaw,
                    character.Yaw,
                    "Real pointer drag must reach the preview"
                );
            }

            manager.Unequip();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreEqual(bareMeshes, character.MeshCount);
            Assert.True(selector.gameObject.activeSelf);
            Assert.True(bar.gameObject.activeSelf);

            var beforePosition = player.transform.position;
            var beforeRotation = player.transform.rotation;
            float beforeYaw = character.Yaw;
            character.OnDrag(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    delta = new Vector2(100, 0),
                }
            );
            Assert.AreEqual(beforeYaw - 40, character.Yaw, .01f);
            Assert.AreEqual(beforePosition, player.transform.position);
            Assert.AreEqual(beforeRotation, player.transform.rotation);
            character.OnDrag(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Right,
                    delta = new Vector2(100, 0),
                }
            );
            Assert.AreEqual(beforeYaw - 40, character.Yaw, .01f);

            manager.GiveWeapon("scythe");
            manager.GiveWeapon("hammer");
            manager.GiveWeapon("bow");
            manager.SelectNext();
            int equippedBeforeBrowsing = manager.CurrentWeaponIndex;
            foreach (int id in new[] { 1003, 1004, 1001 })
            {
                for (int i = 0; i < 4; i++)
                {
                    selector.Select(i);
                    if (selector.SelectedWeapon.id == id)
                        break;
                }
                Assert.AreEqual(id, selector.SelectedWeapon.id);
                Assert.Greater(weapon.MeshCount, 0, "Selection must update the mesh synchronously");
                yield return null;
                Assert.AreEqual(id, controller.PreviewWeaponId);
                Assert.AreEqual(
                    equippedBeforeBrowsing,
                    manager.CurrentWeaponIndex,
                    "Browsing must not alter the live player equipment"
                );
                Assert.True(
                    controller.PreviewIsAnimating,
                    "All three melee weapons share the idle"
                );
                Assert.False(controller.PreviewIsAttacking, "Weapon changes cancel attacks");
                double animationTime = controller.PreviewAnimationTime;
                var poseRoot = Resources
                    .FindObjectsOfTypeAll<Animator>()
                    .Single(a => a.name == "UI Character Pose" && a.gameObject.scene.IsValid());
                var bones = poseRoot.GetComponentsInChildren<Transform>();
                var rotations = bones.Select(b => b.localRotation).ToArray();
                yield return new WaitForSecondsRealtime(.4f);
                Assert.True(
                    bones
                        .Where(
                            (bone, index) =>
                                Quaternion.Angle(bone.localRotation, rotations[index]) > .05f
                        )
                        .Any(),
                    "Idle must animate the preview bones"
                );
                Assert.Greater(controller.PreviewAnimationTime, animationTime);

                var pointer = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                };
                character.OnPointerDown(pointer);
                character.OnDrag(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Left,
                        delta = new Vector2(20, 0),
                    }
                );
                character.OnPointerClick(pointer);
                Assert.False(
                    controller.PreviewIsAttacking,
                    "Dragging must never trigger an attack"
                );
                character.OnPointerDown(pointer);
                character.OnPointerClick(pointer);
                Assert.True(
                    controller.PreviewIsAttacking,
                    "A left click starts the selected weapon's normal melee attack"
                );
                var attackRotations = bones.Select(b => b.localRotation).ToArray();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.True(
                    bones
                        .Where(
                            (bone, index) =>
                                Quaternion.Angle(bone.localRotation, attackRotations[index]) > 5f
                        )
                        .Any(),
                    "Attack must visibly move the preview bones"
                );
                AssertVisible(character, "preview-attack-" + id);
                Assert.AreEqual(beforePosition, player.transform.position);
                Assert.AreEqual(beforeRotation, player.transform.rotation);
                yield return new WaitForSecondsRealtime(2f);
                Assert.False(
                    controller.PreviewIsAttacking,
                    "Attack must finish and return to idle"
                );
                Assert.True(controller.PreviewIsAnimating);
                character.OnPointerDown(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Right,
                    }
                );
                character.OnPointerClick(
                    new PointerEventData(EventSystem.current)
                    {
                        button = PointerEventData.InputButton.Right,
                    }
                );
                Assert.False(controller.PreviewIsAttacking, "Right click does not attack");
                AssertVisible(character, "preview-character-weapon-" + id);
                AssertVisible(weapon, "preview-weapon-" + id);
            }
            // Cancel an active attack on weapon change.
            var leftClick = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
            };
            character.OnPointerDown(leftClick);
            character.OnPointerClick(leftClick);
            Assert.True(controller.PreviewIsAttacking);
            // The bow now ships with a model (Bow.fbx), so its preview shows it like other weapons.
            for (int i = 0; i < 4; i++)
            {
                selector.Select(i);
                if (selector.SelectedWeapon.id == 1002)
                    break;
            }
            Assert.AreEqual(1002, selector.SelectedWeapon.id);
            yield return null;
            Assert.False(controller.PreviewIsAttacking);
            Assert.Greater(weapon.MeshCount, 0);
            Assert.False(weapon.transform.parent.Find("ModelUnavailable").gameObject.activeSelf);
            for (int i = 0; i < 4; i++)
            {
                selector.Select(i);
                if (selector.SelectedWeapon.id == 1001)
                    break;
            }
            float yaw = weapon.Yaw;
            float start = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(20 * (Time.unscaledTime - start), weapon.Yaw - yaw, 3f);
            manager.SelectNext();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.Greater(character.MeshCount, bareMeshes);

            manager.RestoreWeapons(System.Array.Empty<string>(), WeaponManager.NoWeapon);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreEqual(bareMeshes, character.MeshCount);
            Assert.IsNull(weapon.Texture);
            Assert.False(bar.gameObject.activeSelf);
            manager.GiveWeapon("hammer");
            yield return new WaitForSecondsRealtime(.4f);
            Assert.True(selector.gameObject.activeSelf);
            Assert.True(bar.gameObject.activeSelf);
            Assert.AreEqual(1004, selector.SelectedWeapon.id);
            Assert.Greater(weapon.MeshCount, 0);

#if UNITY_EDITOR
            Assert.True(
                UnityEditor.EditorApplication.ExecuteMenuItem(
                    "Tools/CreativeAI/UI/Preview Weapons/All"
                )
            );
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreEqual(4, manager.OwnedCount);
            Assert.True(selector.gameObject.activeSelf);
            Assert.True(bar.gameObject.activeSelf);
            Assert.Greater(character.MeshCount, bareMeshes);
            Assert.True(
                UnityEditor.EditorApplication.ExecuteMenuItem(
                    "Tools/CreativeAI/UI/Preview Weapons/None"
                )
            );
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreEqual(0, manager.OwnedCount);
            Assert.AreEqual(bareMeshes, character.MeshCount);
            Assert.False(selector.gameObject.activeSelf);
            Assert.False(bar.gameObject.activeSelf);
            Assert.True(
                UnityEditor.EditorApplication.ExecuteMenuItem(
                    "Tools/CreativeAI/UI/Preview Weapons/All"
                )
            );
            yield return new WaitForSecondsRealtime(.4f);
#endif
            var pauseScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                character.OnPointerDown(leftClick);
                character.OnPointerClick(leftClick);
                Assert.True(controller.PreviewIsAttacking);
                yield return new WaitForSecondsRealtime(2f);
                Assert.False(
                    controller.PreviewIsAttacking,
                    "Attack returns to idle while game time is paused"
                );
            }
            finally
            {
                Time.timeScale = pauseScale;
            }
            character.OnPointerDown(leftClick);
            character.OnPointerClick(leftClick);
            Assert.True(controller.PreviewIsAttacking);
            var oldTexture = character.Texture;
            router.CloseAll();
            Assert.IsNull(character.Texture);
            Assert.False(controller.PreviewIsAttacking, "Closing disposes active attack playback");
            Assert.True(
                player.GetComponentsInChildren<Renderer>(true).All(r => r.forceRenderingOff),
                "The UI-only source player must never be visible outside the panel"
            );
            Assert.False(oldTexture.IsCreated());
            yield return null;
            router.Open(UiRouter.UiId.Character);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.NotNull(character.Texture);
            AssertVisible(character, "preview-reopened");
            router.CloseAll();

            foreach (var root in player.scene.GetRootGameObjects())
                Object.Destroy(root);
            yield return null;
        }

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
