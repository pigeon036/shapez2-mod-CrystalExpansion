using Core.Factory;

namespace CrystalExpansion.Buildings.Dissolver;

// 溶解器仿真工厂——与晶化器相同，每个建筑实例独立调用 Produce 创建专属仿真实例。
internal class DissolverSimulationFactory : IFactory<DissolverSimulationState, DissolverSimulation>
{
    private readonly IDissolverConfig Config;
    private readonly DissolverOperation Operation;

    public DissolverSimulationFactory(IDissolverConfig config, DissolverOperation operation)
    {
        Config = config;
        Operation = operation;
    }

    public DissolverSimulation Produce(DissolverSimulationState state)
    {
        return new DissolverSimulation(state, Config, Operation);
    }
}
