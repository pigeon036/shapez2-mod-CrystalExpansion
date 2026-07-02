using Core.Factory;
using Game.Content.Features.Predictions.Processing;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack.Predictions;

namespace CrystalExpansion.Buildings.Crystallizer;

// 晶化器预测工厂——预测使用 Processing2In1OutPredictionSimulation，通过 IItemOperation2In1Out.TryExecute
// 推断输出。重用 CrystallizerOperation 确保预测结果与仿真一致。
internal class CrystallizerPredictionFactoryBuilder
    : IBuildingPredictionFactoryBuilder<Processing2In1OutPredictionSimulation>
{
    public IFactory<Processing2In1OutPredictionSimulation> BuildFactory(PredictionSystemsDependencies dependencies)
    {
        CrystallizerOperation operation = new(
            dependencies.Mode.ShapesConfiguration,
            dependencies.ShapeRegistry,
            dependencies.ShapeIdManager);
        return new Processing2In1OutPredictionSimulationFactory(operation);
    }
}
