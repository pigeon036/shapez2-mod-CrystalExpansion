using Game.Content.Features.Fluids;
using Game.Core.Research;
using Game.Core.Simulation;

namespace CrystalExpansion.Buildings.Crystallizer;

public interface ICrystallizerConfig
{
    FluidUnit ConsumptionPerItemInLiters { get; }
    IConsumingFluidContainerConfiguration ContainerConfig { get; }
    public BeltSpeed BeltSpeed { get; }
    public BeltDelay ProcessingDelay { get; }
}

// 晶化器配置——流体消耗、容器容量和传送带速度均与 CutterSpeed 研究绑定。
// OnAfterDeserialize 调用确保 BuffableBeltSpeed/BuffableBeltDelay 在加载后正确初始化可缓冲值。
internal class CrystallizerConfig : ICrystallizerConfig
{
    public FluidUnit ConsumptionPerItemInLiters { get; }
    public IConsumingFluidContainerConfiguration ContainerConfig { get; }
    public BeltSpeed BeltSpeed => _Speed;
    private readonly BuffableBeltSpeed _Speed;
    public BeltDelay ProcessingDelay => _Delay;
    private readonly BuffableBeltDelay _Delay;

    public CrystallizerConfig(
        BuffableBeltSpeed.DiscreteSpeed baseSpeed,
        BuffableBeltDelay.DiscreteDuration baseDuration,
        ResearchSpeedId SpeedId)
    {
        ConsumptionPerItemInLiters = FluidUnit.FromLiters(20);

        ContainerConfig = new ConsumingFluidContainerConfiguration
        {
            Capacity = FluidUnit.FromLiters(40),
            CapacityLiters = 40,
            ConsumingLitersPerMinute = 400,
            ConsumingRate = FluidRate.FromLitersPerMinute(400),
            ConsumingPriority = FluidPriority.Primary,
            ConsumingFlowScaleWithSpeedId = SpeedId,
            CapacityScaleWithSpeedId = SpeedId,
        };

        _Speed = new BuffableBeltSpeed
        {
            BaseSpeed = baseSpeed,
            ResearchId = SpeedId
        };
        _Delay = new BuffableBeltDelay
        {
            BaseDuration = baseDuration,
            Research = SpeedId
        };
        _Speed.OnAfterDeserialize();
        _Delay.OnAfterDeserialize();
    }
}
