using Core.Factory;
using CrystalExpansion.Buildings.Crystallizer;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace CrystalExpansion.Buildings.VirtualCrystallizer;

internal class VirtualCrystallizerFactoryBuilder
    : IBuildingSimulationFactoryBuilder<VirtualCrystallizerSimulation, Virtual2InSimulationState, VirtualCrystallizerConfig>
{
    public IFactory<Virtual2InSimulationState, VirtualCrystallizerSimulation> BuildFactory(
        SimulationSystemsDependencies dependencies,
        out VirtualCrystallizerConfig config)
    {
        config = default;
        CrystallizerOperation operation = new(
            dependencies.Mode.ShapesConfiguration,
            dependencies.ShapeRegistry,
            dependencies.ShapeIdManager);
        return new VirtualCrystallizerSimulationFactory(operation);
    }
}
