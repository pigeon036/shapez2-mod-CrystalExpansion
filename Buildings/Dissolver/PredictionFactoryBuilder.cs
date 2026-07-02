using Core.Factory;
using Game.Content.Features.Predictions.Processing;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack.Predictions;

namespace CrystalExpansion.Buildings.Dissolver;

// 溶解器预测工厂——重用 DissolverOperation 确保预测的 PickDominantBaseCode 与仿真一致。
internal class DissolverPredictionFactoryBuilder
    : IBuildingPredictionFactoryBuilder<Processing2In1OutPredictionSimulation>
{
    public IFactory<Processing2In1OutPredictionSimulation> BuildFactory(PredictionSystemsDependencies dependencies)
    {
        DissolverOperation operation = new();
        return new Processing2In1OutPredictionSimulationFactory(operation);
    }
}
