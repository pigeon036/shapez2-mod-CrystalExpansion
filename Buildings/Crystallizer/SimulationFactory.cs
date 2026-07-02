using Core.Factory;

namespace CrystalExpansion.Buildings.Crystallizer;

// 晶化器仿真工厂——每个建筑实例独立调用 Produce 创建专属仿真实例。
internal class CrystallizerSimulationFactory : IFactory<CrystallizerSimulationState, CrystallizerSimulation>
{
    private readonly ICrystallizerConfig Config;
    private readonly CrystallizerOperation Operation;
    private readonly IShapeRegistry ShapeRegistry;

    public CrystallizerSimulationFactory(
        ICrystallizerConfig config,
        CrystallizerOperation operation,
        IShapeRegistry shapeRegistry)
    {
        Config = config;
        Operation = operation;
        ShapeRegistry = shapeRegistry;
    }

    public CrystallizerSimulation Produce(CrystallizerSimulationState state)
    {
        return new CrystallizerSimulation(state, Config, Operation, ShapeRegistry);
    }
}
