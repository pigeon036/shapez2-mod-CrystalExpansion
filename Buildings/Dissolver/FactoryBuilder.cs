using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace CrystalExpansion.Buildings.Dissolver;

// 溶解器工厂构建器——与晶化器类似，但 DissolverOperation 不需要 ShapesConfiguration 依赖。
// 延迟为 OnePointFiveSeconds（1.5 秒/格），速度与 CutterSpeed 研究绑定。
internal class DissolverFactoryBuilder
    : IBuildingSimulationFactoryBuilder<DissolverSimulation, DissolverSimulationState, DissolverConfig>
{
    public IFactory<DissolverSimulationState, DissolverSimulation> BuildFactory(
        SimulationSystemsDependencies dependencies,
        out DissolverConfig config)
    {
        config = new DissolverConfig(
            BuffableBeltSpeed.DiscreteSpeed.OneSecondPerTile,
            BuffableBeltDelay.DiscreteDuration.OnePointFiveSeconds,
            new ResearchSpeedId("CutterSpeed"));
        DissolverOperation operation = new();
        return new DissolverSimulationFactory(config, operation);
    }
}
