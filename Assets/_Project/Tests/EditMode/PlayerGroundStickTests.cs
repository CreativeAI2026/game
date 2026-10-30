using CreativeAI.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CreativeAI.Tests.EditMode
{
    /// <summary>
    /// 坂を下る時に地面へ押し付ける速さ(<see cref="PlayerController.GroundStickSpeed"/>)の検証。
    /// 押し付けが足元の地面の下がる速さに追いつかないと、プレイヤーが浮いて落下モーションになる(Issue #120)。
    /// </summary>
    public class PlayerGroundStickTests
    {
        // PlayerArmature Prefab の値に合わせている
        private const float SprintSpeed = 12f;
        private const float MoveSpeed = 5f;
        private const float SlopeLimit = 45f;

        // Issue #120 で報告された Field_Area01 の階段の傾き
        private const float StairSlope = 26.6f;

        private static float SlopeDescentSpeed(float horizontalSpeed, float slopeDegrees)
        {
            return horizontalSpeed * Mathf.Tan(slopeDegrees * Mathf.Deg2Rad);
        }

        [TestCase(SprintSpeed)]
        [TestCase(MoveSpeed)]
        public void 階段を下る速さ以上で押し付ける(float horizontalSpeed)
        {
            float stick = PlayerController.GroundStickSpeed(horizontalSpeed, SlopeLimit);

            Assert.That(stick, Is.GreaterThanOrEqualTo(SlopeDescentSpeed(horizontalSpeed, StairSlope)));
        }

        [Test]
        public void 登れる上限の坂をダッシュで下っても押し付けが追いつく()
        {
            float stick = PlayerController.GroundStickSpeed(SprintSpeed, SlopeLimit);

            // tan(45°) の浮動小数誤差を許容する
            Assert.That(
                stick,
                Is.GreaterThanOrEqualTo(SlopeDescentSpeed(SprintSpeed, SlopeLimit) - 1e-4f)
            );
        }

        [Test]
        public void 止まっている時は最低限の速さで押し付ける()
        {
            Assert.That(PlayerController.GroundStickSpeed(0f, SlopeLimit), Is.EqualTo(2f));
        }
    }
}
