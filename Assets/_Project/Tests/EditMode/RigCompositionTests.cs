using System.IO;
using System.Linq;
using CreativeAI.Gameplay;
using CreativeAI.UI;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// プレイヤー・カメラの置き場所の取り決めを守っているかの検証。
    /// プレイヤーとカメラはそれぞれ常駐リグとして生成するので、フィールドシーンには置かない。
    /// </summary>
    public class RigCompositionTests
    {
        private const string FieldSceneFolder = "Assets/_Project/Scenes/Field";

        private static ResidentBootstrapConfig LoadConfig()
        {
            var config = Resources.Load<ResidentBootstrapConfig>(nameof(ResidentBootstrapConfig));
            Assert.IsNotNull(config, "Resources に ResidentBootstrapConfig がありません");
            return config;
        }

        [Test]
        public void PlayerRig_IsTaggedPlayer_AndHasNoCamera()
        {
            var rig = LoadConfig().playerRigPrefab;
            Assert.IsNotNull(rig, "playerRigPrefab が未割当です");

            Assert.AreEqual("Player", rig.tag);
            Assert.IsNotNull(rig.GetComponent<PlayerController>());
            Assert.IsEmpty(rig.GetComponentsInChildren<Camera>(true), "カメラは CameraRig に置く");
            Assert.IsEmpty(rig.GetComponentsInChildren<CinemachineBrain>(true));
            Assert.IsEmpty(rig.GetComponentsInChildren<CinemachineCamera>(true));
            Assert.IsEmpty(rig.GetComponentsInChildren<AudioListener>(true));
        }

        [Test]
        public void CameraRig_HasSingleMainCameraWithBrain_AndNoPlayer()
        {
            var rig = LoadConfig().cameraRigPrefab;
            Assert.IsNotNull(rig, "cameraRigPrefab が未割当です");
            Assert.IsNotNull(rig.GetComponent<CameraRig>());

            var cameras = rig.GetComponentsInChildren<Camera>(true);
            Assert.AreEqual(1, cameras.Length);
            Assert.AreEqual("MainCamera", cameras[0].tag);
            Assert.IsNotNull(cameras[0].GetComponent<CinemachineBrain>());
            Assert.IsEmpty(rig.GetComponentsInChildren<PlayerController>(true));
        }

        [Test]
        public void CameraRig_HasUnifiedNormalAimAndGrabCameras_WithScaledDistances()
        {
            var rig = LoadConfig().cameraRigPrefab;
            Assert.IsNotNull(rig, "cameraRigPrefab が未割当です");

            var vcams = rig.GetComponentsInChildren<CinemachineCamera>(true);
            var normalCam = vcams.FirstOrDefault(c => c.name == "NormalCamera");
            var aimCam = vcams.FirstOrDefault(c => c.name == "AimCamera");
            Assert.IsNotNull(normalCam, "NormalCamera が CameraRig にありません");
            Assert.IsNotNull(aimCam, "AimCamera が CameraRig にありません");

            var normalFollow = normalCam.GetComponent<CinemachineThirdPersonFollow>();
            var aimFollow = aimCam.GetComponent<CinemachineThirdPersonFollow>();
            Assert.IsNotNull(normalFollow);
            Assert.IsNotNull(aimFollow);
            Assert.AreEqual(
                10f,
                normalFollow.CameraDistance,
                1e-4f,
                "NormalCamera の距離は 2.5 倍スケール後の 10 に揃える"
            );
            Assert.AreEqual(
                5f,
                aimFollow.CameraDistance,
                1e-4f,
                "AimCamera の距離は 2.5 倍スケール後の 5 に揃える"
            );

            var grabController = rig.GetComponentInChildren<GrabCameraController>(true);
            Assert.IsNotNull(
                grabController,
                "束縛演出用の GrabCameraController が CameraRig に含まれていません"
            );
            Assert.IsNotNull(grabController.vcamMain);
            Assert.IsNotNull(grabController.vcamPull);
            Assert.IsNotNull(grabController.vcamDamage);
            Assert.IsNotNull(grabController.vcamEscape);
        }

        [Test]
        public void FieldScenes_HaveNoPlayerOrCamera()
        {
            var paths = Directory.GetFiles(
                FieldSceneFolder,
                "*.unity",
                SearchOption.AllDirectories
            );
            Assert.IsNotEmpty(paths);

            foreach (var path in paths.Select(p => p.Replace('\\', '/')))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var all = scene
                        .GetRootGameObjects()
                        .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                        .Select(t => t.gameObject)
                        .ToArray();

                    Assert.IsFalse(
                        all.Any(g => g.CompareTag("Player") || g.GetComponent<PlayerController>()),
                        $"{path}: プレイヤーはシーンに置かず、PlayerRig を生成する"
                    );
                    Assert.IsFalse(
                        all.Any(g =>
                            g.GetComponent<Camera>() || g.GetComponent<CinemachineBrain>()
                        ),
                        $"{path}: カメラはシーンに置かず、CameraRig を生成する"
                    );
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
