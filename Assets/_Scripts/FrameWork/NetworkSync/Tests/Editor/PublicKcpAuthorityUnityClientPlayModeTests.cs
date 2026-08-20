using NUnit.Framework;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// Unity Runtime → Ubuntu 公网 KCP Authority Gate。
    /// </summary>
    public sealed class PublicKcpAuthorityUnityClientPlayModeTests
    {
        private SceneSetup[] _originalSceneSetup;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.IsFalse(Application.isPlaying,
                "PublicKcpAuthorityUnityClientPlayModeTests SetUp Error: Unity Is Already In PlayMode");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                Assert.IsFalse(scene.isDirty,
                    $"PublicKcpAuthorityUnityClientPlayModeTests SetUp Error: Scene Has Unsaved Changes, Scene={scene.path}");
            }

            _originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            yield return new EnterPlayMode();

            Assert.IsTrue(Application.isPlaying,
                "PublicKcpAuthorityUnityClientPlayModeTests SetUp Error: Unity Did Not Enter PlayMode");
        }

        [Explicit("External public KCP authority smoke. Restart Ubuntu KCP Authority Host before each run.")]
        [UnityTest]
        public IEnumerator TwoPlayers_100Frames_PublicKcpAuthorityRoundTrip_BitExact()
        {
            IEnumerator test = PublicKcpAuthorityUnityClientValidationTestBootstrap.Run();
            while (test.MoveNext()) yield return test.Current;
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