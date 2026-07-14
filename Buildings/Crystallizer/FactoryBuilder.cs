using Core.Factory;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace CrystalExpansion.Buildings.Crystallizer;

// 晶化器工厂构建器——在游戏仿真系统初始化时调用，创建 Config + Operation + Factory。
// 延迟为 OnePointFiveSeconds（1.5 秒/格），速度与 CutterSpeed 研究绑定。
internal class CrystallizerFactoryBuilder
    : IBuildingSimulationFactoryBuilder<CrystallizerSimulation, CrystallizerSimulationState, CrystallizerConfig>
{
    public IFactory<CrystallizerSimulationState, CrystallizerSimulation> BuildFactory(
        SimulationSystemsDependencies dependencies,
        out CrystallizerConfig config)
    {
        config = new CrystallizerConfig(
            BuffableBeltSpeed.DiscreteSpeed.OneSecondPerTile,
            BuffableBeltDelay.DiscreteDuration.OnePointFiveSeconds,
            new ResearchSpeedId("CutterSpeed"));
        CrystallizerOperation operation = new(
            dependencies.Mode.ShapesConfiguration,
            dependencies.ShapeRegistry,
            dependencies.ShapeIdManager);
        return new CrystallizerSimulationFactory(config, operation, dependencies.ShapeRegistry);
    }
}
