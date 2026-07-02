using Game.Content.Features.Fluids;
using Game.Core.Research;
using Game.Core.Simulation;

namespace CrystalExpansion.Buildings.Dissolver;

public interface IDissolverConfig
{
    FluidUnit FluidConsumedPerItem { get; }
    FluidUnit FluidOutputPerItem { get; }
    IStoringFluidContainerConfiguration InputTankConfig { get; }
    IStoringFluidContainerConfiguration OutputTankConfig { get; }
    BeltSpeed BeltSpeed { get; }
    BeltDelay ProcessingDelay { get; }
}

// 溶解器配置——双罐配置（输入罐为消费型，输出罐为提供型），均与 CutterSpeed 研究绑定。
// 输入罐和输出罐各自独立，但液体消耗/产出量相同（20L/次），IO 速率均为 400 L/min。
internal class DissolverConfig : IDissolverConfig
{
    public FluidUnit FluidConsumedPerItem { get; }
    public FluidUnit FluidOutputPerItem { get; }
    public IStoringFluidContainerConfiguration InputTankConfig { get; }
    public IStoringFluidContainerConfiguration OutputTankConfig { get; }
    public BeltSpeed BeltSpeed => _Speed;
    private readonly BuffableBeltSpeed _Speed;
    public BeltDelay ProcessingDelay => _Delay;
    private readonly BuffableBeltDelay _Delay;

    public DissolverConfig(
        BuffableBeltSpeed.DiscreteSpeed baseSpeed,
        BuffableBeltDelay.DiscreteDuration baseDuration,
        ResearchSpeedId SpeedId)
    {
        FluidConsumedPerItem = FluidUnit.FromLiters(20);
        FluidOutputPerItem = FluidUnit.FromLiters(20);

        InputTankConfig = new StoringFluidContainerConfiguration
        {
            CapacityLiters = 40,
            Capacity = FluidUnit.FromLiters(40),
            ConsumingLitersPerMinute = 400,
            ConsumingRate = FluidRate.FromLitersPerMinute(400),
            ConsumingPriority = FluidPriority.Primary,
            ConsumingFlowScaleWithSpeedId = SpeedId,
            CapacityScaleWithSpeedId = SpeedId,
        };

        OutputTankConfig = new StoringFluidContainerConfiguration
        {
            CapacityLiters = 40,
            Capacity = FluidUnit.FromLiters(40),
            ProvidingLitersPerMinute = 400,
            ProvidingRate = FluidRate.FromLitersPerMinute(400),
            ProvidingPriority = FluidPriority.Primary,
            ProvidingFlowScaleWithSpeedId = SpeedId,
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
