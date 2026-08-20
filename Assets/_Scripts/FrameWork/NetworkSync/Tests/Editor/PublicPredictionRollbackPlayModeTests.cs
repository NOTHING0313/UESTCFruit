using NUnit.Framework;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// Unity 公网 Prediction + Authority + Rollback 完整生产链路 PlayMode Gate。
    /// </summary>
    public sealed class PublicPredictionRollbackPlayModeTests
    {
        private SceneSetup[] _originalSceneSetup;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.IsFalse(Application.isPlaying,
                "PublicPredictionRollbackPlayModeTests SetUp Error: Unity Is Already In PlayMode");

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                Assert.IsFalse(scene.isDirty,
                    $"PublicPredictionRollbackPlayModeTests SetUp Error: Scene Has Unsaved Changes, Save Before Running Public Rollback Test. Scene={scene.path}");
            }

            _originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            yield return new EnterPlayMode();

            Assert.IsTrue(Application.isPlaying,
                "PublicPredictionRollbackPlayModeTests SetUp Error: Unity Did Not Enter PlayMode");
        }

        [Explicit("External public UDP prediction rollback test. Restart Ubuntu Authority Host before each run.")]
        [UnityTest]
        public IEnumerator TwoPlayers_Frame120_PublicPredictionMismatch_NetworkAuthorityRollbackDriver_Converges()
        {
            IEnumerator test = PublicPredictionRollbackValidationTestBootstrap.Run();
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