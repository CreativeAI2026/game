using CreativeAI.Core;
using CreativeAI.Gameplay;
using CreativeAI.Tests;
using NUnit.Framework.Interfaces;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(ResetStaticsAfterEachTest))]

namespace CreativeAI.Tests
{
    /// <summary>
    /// 各テストのあとに、ゲーム全体で共有している静的な状態を初期状態へ戻す。
    /// このプロジェクトは Play 開始時のドメインリロードを切っているので、static はテストをまたいで残る。
    /// テストごとに戻し忘れると次のテストへ持ち越されるため、EditMode / PlayMode の全テストで自動で戻す。
    /// 前に戻すと [SetUp] で差し込んだものを消してしまうので、戻すのは後だけ。
    /// </summary>
    public sealed class ResetStaticsAfterEachTest : ITestRunCallback
    {
        public void RunStarted(ITest testsToRun) { }

        public void RunFinished(ITestResult testResults) => ResetAll();

        public void TestStarted(ITest test) { }

        public void TestFinished(ITestResult result)
        {
            if (!result.Test.IsSuite)
                ResetAll();
        }

        /// <summary>静的な状態を増やしたら、ここにも戻す処理を足す。</summary>
        public static void ResetAll()
        {
            EventStatics.ResetForPlaySession();
            UiPanelService.ResetState();
            InteractPromptService.Clear();
            PickedItemRegistry.Clear();
            ItemCatalog.InjectForTests(null);
        }
    }
}
