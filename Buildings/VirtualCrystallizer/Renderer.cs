namespace CrystalExpansion.Buildings.VirtualCrystallizer;

internal class VirtualCrystallizerRenderer
    : StatelessBuildingSimulationRenderer<VirtualCrystallizerSimulation, VirtualCrystallizerDrawData>
{
    public VirtualCrystallizerRenderer(IMapModel map) : base(map) { }
}
