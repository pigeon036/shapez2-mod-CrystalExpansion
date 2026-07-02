using Game.Content.Features;
using Game.Core.Serialization;
using Game.Core.Simulation;

namespace CrystalExpansion.Buildings.Dissolver;

// 溶解器仿真状态——纯数据容器，所有字段均为 BeltLaneState / FluidContainerState。
// 与晶化器不同，无渲染器引用字段需要 null 化（溶解器渲染器直接从仿真对象读取数据）。
[SyncableIdentifier("DissolverSimState")]
public class DissolverSimulationState : ISimulationState, ISyncable
{
    public readonly BeltLaneState InputLaneState = new();
    public readonly BeltLaneState ProcessingLaneState = new();
    public readonly BeltLaneState EndLaneState = new();
    public readonly FluidContainerState InTankState = new();
    public readonly FluidContainerState OutTankState = new();

    public void Sync(ISerializationVisitor visitor)
    {
        InputLaneState.Sync(visitor);
        ProcessingLaneState.Sync(visitor);
        EndLaneState.Sync(visitor);
        InTankState.Sync(visitor);
        OutTankState.Sync(visitor);
    }
}
