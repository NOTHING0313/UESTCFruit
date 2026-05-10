# 06. 测试脚本说明

当前 `ECS/Test` 下保留了多组测试脚本，建议每次修改 Core 后至少运行核心测试。

## 1. ECSWorldCoreLogicTestBootstrap

验证 World Core 第一阶段交付闭环：

- MovementSystem 推进 Position
- WorldBuffTargetResolver 访问 Health / Position / Stat
- WorldViewReader 只读访问 View / Position / Health
- DamageResolveSystem 扣减生命值
- DeadCleanupSystem 清理死亡 Entity

## 2. ECSCoreEntityComponentTestBootstrap

验证 Entity / Component 基础能力：

- Entity 创建、销毁、ID 复用
- Version 校验
- Component Set / Get / Remove
- Store 和 ArcheType 同步

## 3. ECSLifecycleBufferTestBootstrap

验证生命周期和 Buffer 行为：

- Tick 中结构变化进入 StructuralChangeBuffer
- 结构变化在帧末播放
- SystemChangeBuffer 正确处理 System 增删

## 4. ECSFrameSyncBufferTestBootstrap

验证帧同步友好结构：

- InputSnapshotBuffer 按帧保存输入
- WorldInputApplier 按帧写入 PlayerInputComponent
- SimulationFrameCommandBuffer 按帧执行外部指令

## 5. ECSInputSystemTest

验证输入系统：

- 键盘和鼠标输入映射
- pressed / held / released 状态
- inputFrame 防止旧输入被误消费

## 6. ECSQueryCacheRegressionTestBootstrap

验证 Query 缓存与 ArcheType 版本：

- 组件组合变化后 Query 结果更新
- QueryCache 在 ArcheTypeVersion 变化后失效
- ExecuteSorted 返回稳定顺序

## 7. 使用方式

1. 在 Unity 场景中新建空 GameObject。
2. 挂载目标测试 Bootstrap。
3. 运行场景。
4. 查看 Console 输出。

建议每次改动后至少运行：

```text
ECSWorldCoreLogicTestBootstrap
ECSCoreEntityComponentTestBootstrap
ECSLifecycleBufferTestBootstrap
```
