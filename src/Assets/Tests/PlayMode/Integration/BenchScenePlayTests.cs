// =============================================================
// 파일: BenchScenePlayTests.cs
// 역할: Bench 장면을 플레이 모드에서 짧게 돌려, 기준 프리셋 적용 -> 시험용 몹 -> 서버 틱 흉내 -> 성능 CSV -> 요약 파일까지
//       끝까지 이어지는지 확인한다.
// 레이어: test (PlayMode, Game.Tests.Integration)
// 관련: FR-163, NFR-021, DES-113 / SDD M1-05
// =============================================================
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Game.Bootstrap.DevTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.Integration
{
    public class BenchScenePlayTests
    {
        private const string BenchScenePath = "Assets/_Game/Scenes/Bench.unity";
        private int _savedLevel;

        [SetUp]
        public void SetUp()
        {
            _savedLevel = QualitySettings.GetQualityLevel();
            BenchRunner.EditorDurationSecondsOverride = 3;
            BenchRunner.QuitWhenFinished = false;
        }

        [TearDown]
        public void TearDown()
        {
            BenchRunner.EditorDurationSecondsOverride = 0;
            BenchRunner.QuitWhenFinished = true;
            QualitySettings.SetQualityLevel(_savedLevel, true);
        }

        [UnityTest]
        public IEnumerator BenchScene_RunsToEnd_WritesCsvAndSummary()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(BenchScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;

            float timeout = Time.realtimeSinceStartup + 20f;
            while (BenchRunner.LastSummaryPath == null && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
            Assert.IsNotNull(BenchRunner.LastSummaryPath, "Bench가 제한 시간 안에 끝나지 않았습니다.");
            string summary = File.ReadAllText(BenchRunner.LastSummaryPath);
            StringAssert.Contains("\"scenario\": \"P1\"", summary);
            StringAssert.Contains("\"purpose\": \"frame-verification\"", summary);
            StringAssert.Contains("\"preset\": \"High\"", summary, "기준 프리셋 「높음」으로 시작해야 합니다.");

            string csv = Path.ChangeExtension(BenchRunner.LastSummaryPath.Replace("_summary", string.Empty), ".csv");
            Assert.IsTrue(File.Exists(csv), "성능 CSV가 있어야 합니다: " + csv);
            Assert.GreaterOrEqual(ReadLinesWhileOpen(csv).Length, 2, "머리글 + 1초 이상");

            // 시험용 몹이 만들어졌는지(P1 = 30체)
            int agents = 0;
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.name.StartsWith("TestAgent")) agents++;
            }
            Assert.AreEqual(30, agents);

            File.Delete(BenchRunner.LastSummaryPath);
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(BenchScenePath));
        }

        /// <summary>측정기가 아직 파일을 열고 있어도 읽을 수 있게 읽기·쓰기 공유로 연다.</summary>
        private static string[] ReadLinesWhileOpen(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd().Split(new[] { (char)10 }, System.StringSplitOptions.RemoveEmptyEntries);
            }
        }
    }
}
#endif
