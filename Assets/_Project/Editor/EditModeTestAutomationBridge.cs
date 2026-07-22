using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;

namespace CreativeAI.EditorTools
{
    [InitializeOnLoad]
    internal static class EditModeTestAutomationBridge
    {
        private const string RequestPath = "Temp/TestAutomation/run-editmode.request";
        private const string ResultsPath = "Temp/TestAutomation/editmode-results.xml";
        private const string SummaryPath = "Temp/TestAutomation/editmode-summary.json";
        private const string RefreshRequestPath = "Temp/TestAutomation/refresh.request";
        private const string RefreshSummaryPath = "Temp/TestAutomation/refresh-summary.json";
        private const string RunningSessionKey =
            "CreativeAI.EditModeTestAutomationBridge.IsRunning";
        private const string RefreshingSessionKey =
            "CreativeAI.EditModeTestAutomationBridge.IsRefreshing";
        private const double PollIntervalSeconds = 0.25d;

        private static TestRunnerApi _testRunnerApi;
        private static TestCallbacks _callbacks;
        private static bool _running;
        private static bool _refreshing;
        private static double _nextPollTime;

        static EditModeTestAutomationBridge()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;

            if (SessionState.GetBool(RunningSessionKey, false))
                ReattachToActiveRun();

            if (SessionState.GetBool(RefreshingSessionKey, false))
                CompleteRefresh();
        }

        private static void Update()
        {
            if (_running || _refreshing || EditorApplication.timeSinceStartup < _nextPollTime)
                return;

            _nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSeconds;
            bool testRequested = File.Exists(RequestPath);
            bool refreshRequested = File.Exists(RefreshRequestPath);
            if (!testRequested && !refreshRequested)
                return;

            if (
                EditorApplication.isCompiling
                || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode
                || IsAnyTestRunActive()
            )
                return;

            if (refreshRequested)
                StartRefresh();
            else
                StartRun();
        }

        private static void StartRefresh()
        {
            _refreshing = true;
            SessionState.SetBool(RefreshingSessionKey, true);
            try
            {
                PrepareOutputDirectory();
                DeleteIfExists(RefreshSummaryPath);
                AssetDatabase.Refresh();
                CompleteRefresh();
            }
            catch (Exception exception)
            {
                WriteRefreshSummary("failed", exception.ToString());
                CleanupRefresh();
            }
        }

        private static void CompleteRefresh()
        {
            WriteRefreshSummary("passed", string.Empty);
            CleanupRefresh();
        }

        private static void WriteRefreshSummary(string status, string message)
        {
            try
            {
                PrepareOutputDirectory();
                string temporaryPath = RefreshSummaryPath + ".tmp";
                File.WriteAllText(
                    temporaryPath,
                    JsonUtility.ToJson(
                        new RefreshSummary { status = status, message = message },
                        true
                    )
                );
                DeleteIfExists(RefreshSummaryPath);
                File.Move(temporaryPath, RefreshSummaryPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void CleanupRefresh()
        {
            _refreshing = false;
            SessionState.SetBool(RefreshingSessionKey, false);
            DeleteIfExists(RefreshRequestPath);
        }

        private static void StartRun()
        {
            try
            {
                PrepareOutputDirectory();
                DeleteIfExists(ResultsPath);
                DeleteIfExists(SummaryPath);

                _running = true;
                SessionState.SetBool(RunningSessionKey, true);
                RegisterCallbacks();
                _testRunnerApi.Execute(
                    new ExecutionSettings(new Filter { testMode = TestMode.EditMode })
                );
            }
            catch (Exception exception)
            {
                WriteErrorSummary("start-error", exception);
                Cleanup();
            }
        }

        private static void ReattachToActiveRun()
        {
            if (!IsAnyTestRunActive())
            {
                WriteErrorSummary(
                    "domain-reload-error",
                    new InvalidOperationException(
                        "The test run ended during domain reload before callbacks were restored."
                    )
                );
                Cleanup();
                return;
            }

            _running = true;
            RegisterCallbacks();
        }

        private static void RegisterCallbacks()
        {
            UnregisterCallbacks();
            _testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbacks = new TestCallbacks();
            _testRunnerApi.RegisterCallbacks(_callbacks);
        }

        private static void UnregisterCallbacks()
        {
            if (_testRunnerApi != null && _callbacks != null)
                _testRunnerApi.UnregisterCallbacks(_callbacks);

            _callbacks = null;
            if (_testRunnerApi != null)
                UnityEngine.Object.DestroyImmediate(_testRunnerApi);
            _testRunnerApi = null;
        }

        private static bool IsAnyTestRunActive()
        {
            try
            {
                var method = typeof(TestRunnerApi).GetMethod(
                    "IsRunActive",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                return method != null && (bool)method.Invoke(null, null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Complete(ITestResultAdaptor result)
        {
            try
            {
                PrepareOutputDirectory();
                TestRunnerApi.SaveResultToFile(result, ResultsPath);

                var failedTestNames = new List<string>();
                CollectFailedTests(result, failedTestNames);
                WriteSummary(
                    new TestSummary
                    {
                        status = result.FailCount == 0 ? "passed" : "failed",
                        passed = result.PassCount,
                        failed = result.FailCount,
                        skipped = result.SkipCount,
                        duration = result.Duration,
                        failedTestNames = failedTestNames,
                    }
                );
            }
            catch (Exception exception)
            {
                WriteErrorSummary("result-error", exception);
            }
            finally
            {
                Cleanup();
            }
        }

        private static void CollectFailedTests(
            ITestResultAdaptor result,
            List<string> failedTestNames
        )
        {
            if (!result.HasChildren)
            {
                if (result.ResultState.StartsWith("Failed", StringComparison.Ordinal))
                    failedTestNames.Add(result.FullName);
                return;
            }

            foreach (var child in result.Children)
                CollectFailedTests(child, failedTestNames);
        }

        private static void WriteErrorSummary(string status, Exception exception)
        {
            try
            {
                PrepareOutputDirectory();
                WriteSummary(
                    new TestSummary
                    {
                        status = status,
                        failed = 1,
                        failedTestNames = new List<string> { exception.ToString() },
                    }
                );
            }
            catch (Exception writeException)
            {
                Debug.LogException(writeException);
            }
        }

        private static void WriteSummary(TestSummary summary)
        {
            string temporaryPath = SummaryPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(summary, true));
            DeleteIfExists(SummaryPath);
            File.Move(temporaryPath, SummaryPath);
        }

        private static void Cleanup()
        {
            UnregisterCallbacks();
            _running = false;
            SessionState.SetBool(RunningSessionKey, false);
            DeleteIfExists(RequestPath);
        }

        private static void PrepareOutputDirectory()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SummaryPath));
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private sealed class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                Complete(result);
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }
        }

        [Serializable]
        private sealed class TestSummary
        {
            public string status;
            public int passed;
            public int failed;
            public int skipped;
            public double duration;
            public List<string> failedTestNames = new();
        }

        [Serializable]
        private sealed class RefreshSummary
        {
            public string status;
            public string message;
        }
    }
}
