using Core.Factory;

namespace CrystalExpansion.Buildings.VirtualDissolver;

internal class VirtualDissolverSimulationFactory : IFactory<Virtual2InSimulationState, VirtualDissolverSimulation>
{
    private readonly IItemOperation2In1Out Operation;
    public VirtualDissolverSimulationFactory(IItemOperation2In1Out operation) { Operation = operation; }
    public VirtualDissolverSimulation Produce(Virtual2InSimulationState state)
        => new VirtualDissolverSimulation(state, Operation);
}
