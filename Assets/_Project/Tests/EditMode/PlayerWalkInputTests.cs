using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 歩いて向かうときの入力(カメラ基準)の計算。目標の方角・カメラの向き・到着の判定を確かめる。
    /// </summary>
    public class PlayerWalkInputTests
    {
        private const float Arrive = 0.2f;

        private static void AssertInput(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, $"x: {actual}");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, $"y: {actual}");
        }

        [Test]
        public void TryGetWalkInput_TargetAhead_CameraFacingForward_IsForwardInput()
        {
            var walking = PlayerController.TryGetWalkInput(
                Vector3.zero,
                new Vector3(0f, 0f, 5f),
                0f,
                Arrive,
                out var input
            );

            Assert.IsTrue(walking);
            AssertInput(new Vector2(0f, 1f), input);
        }

        [Test]
        public void TryGetWalkInput_TargetToTheRight_CameraFacingForward_IsRightInput()
        {
            PlayerController.TryGetWalkInput(
                Vector3.zero,
                new Vector3(5f, 0f, 0f),
                0f,
                Arrive,
                out var input
            );

            AssertInput(new Vector2(1f, 0f), input);
        }

        [Test]
        public void TryGetWalkInput_CameraTurned_TargetInCameraDirection_IsForwardInput()
        {
            // カメラが +x を向いている(yaw 90)とき、+x にある目標は「前」。
            PlayerController.TryGetWalkInput(
                Vector3.zero,
                new Vector3(5f, 0f, 0f),
                90f,
                Arrive,
                out var input
            );

            AssertInput(new Vector2(0f, 1f), input);
        }

        [Test]
        public void TryGetWalkInput_WithinArriveDistance_IsArrivedWithNoInput()
        {
            var walking = PlayerController.TryGetWalkInput(
                Vector3.zero,
                new Vector3(0.1f, 0f, 0.1f),
                0f,
                Arrive,
                out var input
            );

            Assert.IsFalse(walking);
            Assert.AreEqual(Vector2.zero, input);
        }

        [Test]
        public void TryGetWalkInput_IgnoresHeightDifference()
        {
            // 高さだけ離れていても、水平に着いていれば着いたとみなす(階の違いで永遠に歩き続けない)。
            var walking = PlayerController.TryGetWalkInput(
                Vector3.zero,
                new Vector3(0f, 10f, 0f),
                0f,
                Arrive,
                out _
            );

            Assert.IsFalse(walking);
        }
    }
}
