using Game.Content.Features.Fluids;
using Game.Core.Serialization;
using Game.Core.Simulation;

namespace CrystalExpansion.Buildings.Crystallizer;

[SyncableIdentifier("CrystallizerState")]
public class CrystallizerSimulationState : ISimulationState, ISyncable
{
    public readonly BeltLaneState InputLaneState = new();
    public readonly BeltLaneState ProcessingLaneState = new();
    public readonly BeltLaneState OutputLaneState = new();
    public readonly FluidContainerState ContainerState = new();
    // 当前处理中使用的流体类型（用于 Update 判断是否需要消耗流体）
    public IFluid ProcessingFluid;
    // 渲染器引用——运行时通过 ShapeRegistry 查找构造，不可持久化
    public ShapeItem CrystalShape;
    public ShapeItem CurrentUnchangedShape;

    public void Sync(ISerializationVisitor visitor)
    {
        InputLaneState.Sync(visitor);
        ProcessingLaneState.Sync(visitor);
        OutputLaneState.Sync(visitor);
        ContainerState.Sync(visitor);
        visitor.Sync(ref ProcessingFluid);
        // 反序列化时清除渲染器引用。
        // CrystalShape 和 CurrentUnchangedShape 是 ShapeItem 对象，
        // 保存/加载后不再有效——它们需要通过 ShapeRegistry 从哈希重新查找。
        // 仿真在加载后重新运行时，ProcessItem 会重新设置这些引用。
        if (visitor.Reading)
        {
            CrystalShape = null;
            CurrentUnchangedShape = null;
        }
    }
}
