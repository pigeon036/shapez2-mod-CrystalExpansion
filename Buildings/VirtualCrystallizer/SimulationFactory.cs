using Core.Factory;

namespace CrystalExpansion.Buildings.VirtualCrystallizer;

internal class VirtualCrystallizerSimulationFactory : IFactory<Virtual2InSimulationState, VirtualCrystallizerSimulation>
{
    private readonly IItemOperation2In1Out Operation;
    public VirtualCrystallizerSimulationFactory(IItemOperation2In1Out operation) { Operation = operation; }
    public VirtualCrystallizerSimulation Produce(Virtual2InSimulationState state)
        => new VirtualCrystallizerSimulation(state, Operation);
}
