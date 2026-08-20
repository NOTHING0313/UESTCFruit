using NUnit.Framework;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// Unity 公网 Authority Rollback PlayMode Gate。
    /// </summary>
    public sealed class PublicRollbackCorePlayModeTests
    {
        private SceneSetup[] _sceneSetup;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _sceneSetup = EditorSceneManager.GetSceneManagerSetup();

            for (int i = 0; i < _sceneSetup.Length; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(_sceneSetup[i].path);
                if (scene.IsValid() && scene.isDirty)
                    Assert.Fail($"PublicRollbackCorePlayModeTests SetUp Error: Scene Is Dirty, Path={scene.path}");
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            Assert.IsTrue(Application.isPlaying, "PublicRollbackCorePlayModeTests SetUp Error: Failed To Enter PlayMode");
        }

        [Explicit("需要手动启动 Ubuntu NetworkSyncAuthorityHost，禁止普通 Run All 自动执行公网测试。")]
        [UnityTest]
        public IEnumerator TwoPlayers_Frame120_PublicAuthorityPredictionMismatch_RollbackConverges()
        {
            yield return PublicRollbackCoreValidationTestBootstrap.Run();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();

            if (_sceneSetup != null && _sceneSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(_sceneSetup);
        }
    }
}