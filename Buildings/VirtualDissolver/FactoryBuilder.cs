using Core.Factory;
using CrystalExpansion.Buildings.Dissolver;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;

namespace CrystalExpansion.Buildings.VirtualDissolver;

internal class VirtualDissolverFactoryBuilder
    : IBuildingSimulationFactoryBuilder<VirtualDissolverSimulation, Virtual2InSimulationState, VirtualDissolverConfig>
{
    public IFactory<Virtual2InSimulationState, VirtualDissolverSimulation> BuildFactory(
        SimulationSystemsDependencies dependencies,
        out VirtualDissolverConfig config)
    {
        config = default;
        DissolverOperation operation = new();
        return new VirtualDissolverSimulationFactory(operation);
    }
}
