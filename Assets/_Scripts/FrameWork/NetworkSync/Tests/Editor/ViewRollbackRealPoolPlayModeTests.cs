using NUnit.Framework;
using System.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// 真实 GameObjectPoolCenter + GameObjectPoolViewInstanceProvider 的 PlayMode View Rollback Gate。
    /// </summary>
    public sealed class ViewRollbackRealPoolPlayModeTests
    {
        private SceneSetup[] _originalSceneSetup;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.IsFalse(Application.isPlaying,
                "ViewRollbackRealPoolPlayModeTests SetUp Error: Unity Is Already In PlayMode");

            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                Scene scene=SceneManager.GetSceneAt(i);
                Assert.IsFalse(scene.isDirty,
                    $"ViewRollbackRealPoolPlayModeTests SetUp Error: Scene Has Unsaved Changes, Scene={scene.path}");
            }

            _originalSceneSetup=EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);

            yield return new EnterPlayMode();

            Assert.IsTrue(Application.isPlaying,
                "ViewRollbackRealPoolPlayModeTests SetUp Error: Unity Did Not Enter PlayMode");
        }

        [UnityTest]
        public IEnumerator CreatedEntity_RollbackBeforeCreation_RealPoolReleasesAndReuses()
        {
            ViewRollbackPlayModeValidationTestBootstrap.RunCreatedEntityRollbackRealPoolStatic();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DestroyedEntity_RollbackBeforeDestroy_RealPoolRespawnsSameInstance()
        {
            ViewRollbackPlayModeValidationTestBootstrap.RunDestroyedEntityRollbackRealPoolStatic();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ConsumedViewEvent_RollbackResimulate_PlayModeDoesNotReplay()
        {
            ViewRollbackPlayModeValidationTestBootstrap.RunRollbackViewEventPlayModeStatic();
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return null;

            if(Application.isPlaying)
                yield return new ExitPlayMode();

            if(_originalSceneSetup!=null)
            {
                EditorSceneManager.RestoreSceneManagerSetup(_originalSceneSetup);
                _originalSceneSetup=null;
            }
        }
    }
}
