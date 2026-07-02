using CrystalExpansion.Fluid;
using Game.Content.Features;
using Game.Content.Features.Fluids;
using Game.Core.Simulation;

namespace CrystalExpansion.Buildings.Crystallizer;

// 晶化器仿真——三段传送带管道架构：
//   InputLane → ProcessingLane(DelayBeltLane, 2.5s) → OutputLane
//
// 输入：形状（传送带）+ ColorFluid 或 ShapeTypedFluid（管道，West 方向）
// 输出：晶体化形状（传送带）
//
// PreAcceptHook：仅在 ProcessingLane 空闲且容器有足够流体时接受输入
// AcceptHook：清除旧渲染引用，准备接收新物品
// ProcessItem：区分 ShapeTypedFluid（具体 ShapeCode）与普通 ColorFluid（ForAllParts 通配符）
public class CrystallizerSimulation : Simulation<CrystallizerSimulationState>, IItemSimulation, IFluidSimulation, IUpdatableSimulation
{
    public readonly BeltLane InputLane;
    public readonly DelayBeltLane ProcessingLane;
    public readonly BeltLane OutputLane;

    private readonly CrystallizerOperation Operation;
    private readonly IShapeRegistry ShapeRegistry;
    private readonly FluidUnit ConsumptionPerItem;

    public readonly ConsumingFluidContainer Container;

    public int NumFluidProviders => 0;
    public int NumFluidReceivers => 1;
    public int NumItemReceivers => 1;
    public int NumItemProviders => 1;

    public CrystallizerSimulation(
        CrystallizerSimulationState simulationState,
        ICrystallizerConfig config,
        CrystallizerOperation operation,
        IShapeRegistry shapeRegistry)
        : base(simulationState)
    {
        Operation = operation;
        ShapeRegistry = shapeRegistry;
        ConsumptionPerItem = config.ConsumptionPerItemInLiters;

        // 传送带链：OutputLane 是最终输出，ProcessingLane 的 NextLane 指向 OutputLane，
        // InputLane 指向 ProcessingLane。DelayBeltLane 在 progress=1.0 时自动传递物品到 NextLane。
        OutputLane = new BeltLane(config.BeltSpeed, simulationState.OutputLaneState);
        ProcessingLane = new DelayBeltLane(config.ProcessingDelay, simulationState.ProcessingLaneState, OutputLane);
        InputLane = new BeltLane(config.BeltSpeed, simulationState.InputLaneState, ProcessingLane);

        Container = new ConsumingFluidContainer(simulationState.ContainerState, config.ContainerConfig);

        // PreAcceptHook：物品进入 InputLane 前的门控条件。
        // ProcessingLane 空闲（无排队物品）+ 容器中流体充足
        InputLane.PreAcceptHook = _ => 
            !ProcessingLane.HasItem
            && Container.CanTake(ConsumptionPerItem);

        // AcceptHook：物品被接受后立即清除旧的渲染引用，为新操作准备视觉状态。
        InputLane.AcceptHook = delegate (IItemReceiver _, ref IBeltItem _, ref Ticks remainingTicks)
        {
            State.ProcessingFluid = null;
            State.CrystalShape = null;
            State.CurrentUnchangedShape = null;
        };
    }

    public IItemReceiver GetItemReceiver(int index) => InputLane;

    public IItemProvider GetItemProvider(int index) => OutputLane;

    public IFluidReceiver GetFluidReceiver(int index) => Container;

    public IFluidProvider GetFluidProvider(int index) => null;

    public void TraverseLanes<TTraverser>(TTraverser traverser) where TTraverser : IItemLaneTraverser
    {
        traverser.Traverse(InputLane);
        traverser.Traverse(ProcessingLane);
        traverser.Traverse(OutputLane);
    }

    public void ClearContent()
    {
        TraverseLanes(ClearItemsItemLaneTraverser.Default);
        Container.Flush();
        State.ProcessingFluid = null;
        State.CrystalShape = null;
        State.CurrentUnchangedShape = null;
    }

    // Update 顺序：先更新输出传送带再更新输入（数据流向后传播）。
    // ProcessingLane 到达进度 1.0 时自动将物品传递到 OutputLane，
    // 因此 ProcessItem 在 ProcessingLane.HasItem && ProcessingFluid 未设置时触发。
    public void Update(Ticks startTicks, Ticks deltaTicks)
    {
        OutputLane.Update(deltaTicks);
        ProcessingLane.Update(deltaTicks);
        InputLane.Update(deltaTicks);

        if (ProcessingLane.HasItem && State.ProcessingFluid == null && Container.CanTake(ConsumptionPerItem))
            ProcessItem(ProcessingLane);
    }

    private void ProcessItem(DelayBeltLane lane)
    {
        ShapeItem shape = (ShapeItem)lane.Item;
        CrystallizeVariantPayload payload;
        State.ProcessingFluid = Container.Fluid;
        // 区分 ShapeTypedFluid（使用具体 ShapeCode 定向替换）和
        // 普通 ColorFluid（使用 ForAllParts 通配符 '*' 替换所有非 Pin/晶体部件）
        if (State.ProcessingFluid is ShapeTypedFluid shapeTypedFluid)
            payload = new CrystallizeVariantPayload(shape.Definition, shapeTypedFluid);
        else
            payload = new CrystallizeVariantPayload(shape.Definition, ShapeTypedFluid.ForAllParts((ColorFluid)State.ProcessingFluid));
        CrystallizeVariantResult result = Operation.Execute(payload);
        Container.Take(ConsumptionPerItem);
        ShapeItem crystallized = ShapeRegistry.GetItem(result.Result);
        State.CrystalShape = crystallized;
        ShapeItem unchangedItem = null;
        if (result.Unchanged.IsValid)
            ShapeRegistry.TryGetItem(result.Unchanged, out unchangedItem);
        State.CurrentUnchangedShape = unchangedItem;
        lane.ReplaceItem(crystallized);
    }
}
