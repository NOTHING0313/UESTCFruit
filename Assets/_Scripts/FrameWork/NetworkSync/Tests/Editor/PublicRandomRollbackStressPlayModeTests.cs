using NUnit.Framework;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// Unity 公网随机 Prediction + Authority + Repeated Rollback 压力 Gate。
    /// </summary>
    public sealed class PublicRandomRollbackStressPlayModeTests
    {
        private SceneSetup[] _originalSceneSetup;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.IsFalse(Application.isPlaying,
                "PublicRandomRollbackStressPlayModeTests SetUp Error: Unity Is Already In PlayMode");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                Assert.IsFalse(scene.isDirty,
                    $"PublicRandomRollbackStressPlayModeTests SetUp Error: Scene Has Unsaved Changes, Save Before Running Public Stress. Scene={scene.path}");
            }

            _originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            yield return new EnterPlayMode();

            Assert.IsTrue(Application.isPlaying,
                "PublicRandomRollbackStressPlayModeTests SetUp Error: Unity Did Not Enter PlayMode");
        }

        [Explicit("External public UDP 2000-frame rollback stress. Restart Ubuntu Authority Host before each run.")]
        [UnityTest]
        public IEnumerator TwoPlayers_2000Frames_RandomDelayedPublicAuthority_RemainsConverged()
        {
            IEnumerator test = PublicRandomRollbackStressValidationTestBootstrap.Run();
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