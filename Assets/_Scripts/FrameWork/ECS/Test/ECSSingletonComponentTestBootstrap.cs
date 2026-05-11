using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// 验证 World Singleton Component API 的创建、覆盖、移除以及与 Entity 销毁的映射清理规则。
/// </summary>
public sealed class ECSSingletonComponentTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS Singleton Component Test] Start</color>");

        TestSetAndGetSingleton();
        TestOverwriteSingletonKeepsSameEntity();
        TestRemoveSingletonDestroysEntityAndClearsMapping();
        TestDestroySingletonEntityClearsMapping();
        TestSingletonCanBeUpdatedByRef();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS Singleton Component Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS Singleton Component Test] Failed count = {_failedCount}");
    }

    /// <summary>验证 SetSingleton 会创建内部 Entity，并允许通过 TryGetSingleton / GetSingleton 读取。</summary>
    private void TestSetAndGetSingleton()
    {
        Debug.Log("<color=cyan>[Singleton Test 1] Set And Get Singleton</color>");

        World world = new World();
        Entity entity = world.SetSingleton(new TestGameTimeComponent(10, 0.5f));

        bool hasEntity = entity.IsValid && world.IsAlive(entity);
        bool hasSingleton = world.HasSingleton<TestGameTimeComponent>();
        bool tryGetSuccess = world.TryGetSingleton(out TestGameTimeComponent value) && value.frame == 10 && NearlyEqual(value.timeScale, 0.5f);
        bool entitySuccess = world.TryGetSingletonEntity<TestGameTimeComponent>(out Entity singletonEntity) && singletonEntity == entity;

        Expect(hasEntity, "SetSingleton should create a valid alive singleton entity.");
        Expect(hasSingleton, "HasSingleton should return true after SetSingleton.");
        Expect(tryGetSuccess, "TryGetSingleton should return stored singleton data.");
        Expect(entitySuccess, "TryGetSingletonEntity should return the internal singleton entity.");
    }

    /// <summary>验证重复 SetSingleton 只覆盖组件数据，不重复创建 Entity。</summary>
    private void TestOverwriteSingletonKeepsSameEntity()
    {
        Debug.Log("<color=cyan>[Singleton Test 2] Overwrite Keeps Same Entity</color>");

        World world = new World();
        Entity firstEntity = world.SetSingleton(new TestGameTimeComponent(1, 1f));
        Entity secondEntity = world.SetSingleton(new TestGameTimeComponent(2, 2f));

        bool sameEntity = firstEntity == secondEntity;
        bool valueSuccess = world.TryGetSingleton(out TestGameTimeComponent value) && value.frame == 2 && NearlyEqual(value.timeScale, 2f);
        bool aliveCountSuccess = world.AliveEntityCount == 1;

        Expect(sameEntity, "Repeated SetSingleton should reuse the same entity.");
        Expect(valueSuccess, "Repeated SetSingleton should overwrite component data.");
        Expect(aliveCountSuccess, $"Repeated SetSingleton should not create extra entities. Alive = {world.AliveEntityCount}");
    }

    /// <summary>验证 RemoveSingleton 会清理映射并销毁承载 Entity。</summary>
    private void TestRemoveSingletonDestroysEntityAndClearsMapping()
    {
        Debug.Log("<color=cyan>[Singleton Test 3] Remove Singleton Destroys Entity And Clears Mapping</color>");

        World world = new World();
        Entity entity = world.SetSingleton(new TestGameTimeComponent(3, 1f));
        bool removed = world.RemoveSingleton<TestGameTimeComponent>();

        bool removeSuccess = removed;
        bool mappingCleared = !world.HasSingleton<TestGameTimeComponent>() && !world.TryGetSingletonEntity<TestGameTimeComponent>(out _);
        bool entityDead = !world.IsAlive(entity);

        Expect(removeSuccess, "RemoveSingleton should return true when singleton exists.");
        Expect(mappingCleared, "RemoveSingleton should clear singleton mapping.");
        Expect(entityDead, "RemoveSingleton should destroy the internal singleton entity.");
    }

    /// <summary>验证通过 DestroyEntity 销毁 Singleton 承载 Entity 时，World 会同步清理 Singleton 映射。</summary>
    private void TestDestroySingletonEntityClearsMapping()
    {
        Debug.Log("<color=cyan>[Singleton Test 4] Destroy Singleton Entity Clears Mapping</color>");

        World world = new World();
        Entity entity = world.SetSingleton(new TestGameTimeComponent(4, 1f));
        world.DestroyEntity(entity);

        bool entityDead = !world.IsAlive(entity);
        bool mappingCleared = !world.HasSingleton<TestGameTimeComponent>() && !world.TryGetSingletonEntity<TestGameTimeComponent>(out _);

        Expect(entityDead, "DestroyEntity should destroy singleton entity.");
        Expect(mappingCleared, "DestroyEntity should clear singleton mapping when the target is singleton entity.");
    }

    /// <summary>验证 GetSingleton 返回 ref，可直接修改底层组件数据。</summary>
    private void TestSingletonCanBeUpdatedByRef()
    {
        Debug.Log("<color=cyan>[Singleton Test 5] Singleton Can Be Updated By Ref</color>");

        World world = new World();
        world.SetSingleton(new TestGameTimeComponent(5, 1f));

        ref TestGameTimeComponent value = ref world.GetSingleton<TestGameTimeComponent>();
        value.frame = 6;
        value.timeScale = 1.5f;

        bool success = world.TryGetSingleton(out TestGameTimeComponent result) && result.frame == 6 && NearlyEqual(result.timeScale, 1.5f);
        Expect(success, "GetSingleton should expose writable ref to singleton component.");
    }

    /// <summary>输出测试结果。</summary>
    private void Expect(bool condition, string message)
    {
        if (condition)
        {
            Debug.Log($"<color=green>[PASS]</color> {message}");
            return;
        }

        _failedCount++;
        Debug.LogError($"[FAIL] {message}");
    }

    /// <summary>比较浮点数是否近似相等。</summary>
    private static bool NearlyEqual(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0001f;
    }
}

/// <summary>测试用全局时间 Singleton Component。</summary>
public struct TestGameTimeComponent : IComponentData
{
    public int frame;
    public float timeScale;

    public TestGameTimeComponent(int frame, float timeScale)
    {
        this.frame = frame;
        this.timeScale = timeScale;
    }
}

}
