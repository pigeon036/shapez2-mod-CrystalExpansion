using CrystalExpansion.Fluid;
using Game.Content.Features.Fluids;
using Game.Core.Simulation;

namespace CrystalExpansion.Buildings.Dissolver;

// 溶解器仿真——三段传送带管道架构：
//   InputLane（上层 z=1）→ ProcessingLane(DelayBeltLane, 1.5s) → EndLane（汇）
//
// 输入：形状（传送带上层）+ ColorFluid（管道，West 方向）
// 输出：ShapeTypedFluid（管道，East 方向）
//
// EndLane 为普通 BeltLane 且无 NextLane。DelayBeltLane 到期后会调用 NextLane.CanAcceptItem，
// 若 NextLane 为 null 则触发 NRE。因此 EndLane 仅用于防止崩溃，物品在到达 EndLane 时被消费。
//
// 液体罐最低阈值：40L（hardcoded），低于此值不允许转换。
public class DissolverSimulation : Simulation<DissolverSimulationState>, IItemSimulation, IFluidSimulation, IUpdatableSimulation
{
    public readonly BeltLane InputLane;
    public readonly DelayBeltLane ProcessingLane;
    public readonly BeltLane EndLane;

    private readonly DissolverOperation Operation;
    private readonly FluidUnit ConsumptionPerItem;
    private readonly FluidUnit ProductionPerItem;

    public readonly StoringFluidContainer InputTank;
    public readonly StoringFluidContainer OutputTank;

    public int NumFluidProviders => 1;
    public int NumFluidReceivers => 1;
    public int NumItemReceivers => 1;
    public int NumItemProviders => 0;

    public DissolverSimulation(
        DissolverSimulationState simulationState,
        IDissolverConfig config,
        DissolverOperation operation)
        : base(simulationState)
    {
        Operation = operation;
        ConsumptionPerItem = config.FluidConsumedPerItem;
        ProductionPerItem = config.FluidOutputPerItem;

        // EndLane 的 NextLane = null：仅作为 DelayBeltLane 的汇，防止 NRE
        EndLane = new BeltLane(config.BeltSpeed, simulationState.EndLaneState);
        ProcessingLane = new DelayBeltLane(config.ProcessingDelay, simulationState.ProcessingLaneState, EndLane);
        InputLane = new BeltLane(config.BeltSpeed, simulationState.InputLaneState, ProcessingLane);

        InputTank = new StoringFluidContainer(simulationState.InTankState, config.InputTankConfig);
        OutputTank = new StoringFluidContainer(simulationState.OutTankState, config.OutputTankConfig);

        // PreAcceptHook: 四个条件缺一不可
        // 1. ProcessingLane 空闲
        // 2. 输入罐有足够流体（≥ 20L）
        // 3. 流体类型必须是 ColorFluid（不确定形状码时不可溶解）
        // 4. 输出罐有足够剩余容量
        InputLane.PreAcceptHook = _ =>
            !ProcessingLane.HasItem
            && InputTank.CanTake(ConsumptionPerItem)
            && InputTank.Fluid is ColorFluid
            && OutputTank.RemainingCapacity >= ProductionPerItem;
    }

    public IItemReceiver GetItemReceiver(int index) => InputLane;
    public IItemProvider GetItemProvider(int index) => null;
    public IFluidReceiver GetFluidReceiver(int index) => InputTank;
    public IFluidProvider GetFluidProvider(int index) => OutputTank;

    public void TraverseLanes<TTraverser>(TTraverser visitor) where TTraverser : IItemLaneTraverser
    {
        visitor.Traverse(InputLane);
        visitor.Traverse(ProcessingLane);
        visitor.Traverse(EndLane);
    }

    public void ClearContent()
    {
        TraverseLanes(ClearItemsItemLaneTraverser.Default);
        InputTank.Flush();
        OutputTank.Flush();
    }

    public void Update(Ticks startTicks, Ticks deltaTicks)
    {
        // 更新顺序：先输出罐（排空已在管道中的流体），再传送带（从输出到输入反向传播）
        OutputTank.Update(deltaTicks);
        EndLane.Update(deltaTicks);
        ProcessingLane.Update(deltaTicks);
        InputLane.Update(deltaTicks);

        // EndLane 有物品且所有条件满足时才处理
        if (!EndLane.HasItem) return;
        if (!InputTank.CanTake(ConsumptionPerItem)) return;
        // 最低阈值：输入罐 < 40L 时不允许转换（防止微量流体浪费）
        if (InputTank.Value < FluidUnit.FromLiters(40)) return;
        if (InputTank.Fluid is not ColorFluid fluid) return;
        if (OutputTank.RemainingCapacity < ProductionPerItem) return;

        ProcessItem(EndLane, fluid);
    }

    private void ProcessItem(BeltLane belt, ColorFluid color)
    {
        ShapeItem shape = (ShapeItem)belt.Item;
        ShapeTypedFluid result = Operation.Execute(shape.Definition, color);
        // result == null 表示没有基础部件码匹配——静默消耗形状，不产出流体
        if (result != null)
        {
            InputTank.Take(ConsumptionPerItem);
            OutputTank.Add(ProductionPerItem, result);
        }
        belt.Clear();
    }
}
