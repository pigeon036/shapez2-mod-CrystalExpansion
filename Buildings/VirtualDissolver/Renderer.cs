namespace CrystalExpansion.Buildings.VirtualDissolver;

internal class VirtualDissolverRenderer
    : StatelessBuildingSimulationRenderer<VirtualDissolverSimulation, VirtualDissolverDrawData>
{
    public VirtualDissolverRenderer(IMapModel map) : base(map) { }
}
