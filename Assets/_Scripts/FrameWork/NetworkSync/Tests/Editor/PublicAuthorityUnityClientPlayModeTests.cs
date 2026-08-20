using NUnit.Framework;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// Unity Editor 驱动真实 PlayMode 公网 Authority Smoke。
    /// </summary>
    public sealed class PublicAuthorityUnityClientPlayModeTests
    {
        private SceneSetup[] _originalSceneSetup;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.IsFalse(Application.isPlaying,
                "PublicAuthorityUnityClientPlayModeTests SetUp Error: Unity Is Already In PlayMode");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                Assert.IsFalse(scene.isDirty,
                    $"PublicAuthorityUnityClientPlayModeTests SetUp Error: Scene Has Unsaved Changes, Save Before Running Public Smoke. Scene={scene.path}");
            }

            _originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            yield return new EnterPlayMode();

            Assert.IsTrue(Application.isPlaying,
                "PublicAuthorityUnityClientPlayModeTests SetUp Error: Unity Did Not Enter PlayMode");

            Assert.AreEqual(1, SceneManager.sceneCount,
                $"PublicAuthorityUnityClientPlayModeTests SetUp Error: Expected Empty Single Scene, ActualSceneCount={SceneManager.sceneCount}");
        }

        [Explicit("External public UDP authority smoke. Requires Ubuntu Authority Host restart before each run.")]
        [UnityTest]
        public IEnumerator TwoPlayers_100Frames_PublicAuthorityRoundTrip_BitExact()
        {
            IEnumerator smoke = PublicAuthorityUnityClientValidationTestBootstrap.Run();
            while (smoke.MoveNext()) yield return smoke.Current;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();

            if (_originalSceneSetup != null)
            {
                EditorSceneManager.RestoreSceneManagerSetup(_originalSceneSetup);
                _originalSceneSetup = null;
            }
        }
    }
}