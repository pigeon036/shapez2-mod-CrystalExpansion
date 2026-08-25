using System;
using System.Collections.Generic;
using Game.Content.Features.Predictions;
using Game.Core.Map.Simulation;
using Game.Core.Simulation;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;
using ShapezShifter.Hijack.Predictions;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Common;

// 独立预测系统注册器（自包含，复制源码即可复用）。
//
// 为什么不把 WithPrediction 放进 AtomicBuildings 链：
// AtomicBuildingExtender 会把 BuildingPredictionExtender 纳入「全部执行一次后重启」
// 等待集。玩家关闭预测显示时预测系统从不创建，extender 不触发 → 链不重启 →
// 下一次会话建筑定义从 GameBuildings 消失。
//
// 本工具类在 Register 时直接把 BuildingPredictionExtender 注册为常驻 rewirer，
// 不依赖游戏会话钩子。SessionPredictionRewirer 记录上一次处理过的预测系统集合引用：
// 同一集合只注入一次；预测开关重新开启或新会话创建新集合时自动重新注入。
// 调用方只需：new → Register → Dispose，无需修改自己的初始化代码。
//
// Register<TSimulation> 是泛型方法，同一个 registry 实例可以容纳多种
// IItemPredictionSimulation 预测类型。
public sealed class BuildingPredictionRegistry : IDisposable
{
    private readonly ILogger _logger;
    private readonly List<RewirerHandle> _handles = new();

    public BuildingPredictionRegistry(ILogger logger)
    {
        _logger = logger;
    }

    public void Register<TSimulation>(
        BuildingDefinitionId buildingId,
        IBuildingPredictionFactoryBuilder<TSimulation> factoryBuilder)
        where TSimulation : IItemPredictionSimulation
    {
        var predictionExtender = new BuildingPredictionExtender<TSimulation>(
            buildingId,
            factoryBuilder,
            _logger);

        var rewirer = new SessionPredictionRewirer<TSimulation>(predictionExtender);
        _handles.Add(GameRewirers.AddRewirer<IPredictionSystemsRewirer>(rewirer));
    }

    public void Dispose()
    {
        foreach (RewirerHandle handle in _handles)
            GameRewirers.RemoveRewirer(handle);
        _handles.Clear();
    }
}

// 常驻预测 rewirer：对每个预测系统集合只注入一次。
// 游戏每次 SetupPredictions 都会创建新的 ICollection<ISimulationSystem>，
// 因此用引用比较即可覆盖「会话切换 / 预测开关重新打开」场景，且不会重复注入。
internal sealed class SessionPredictionRewirer<TSimulation> : IPredictionSystemsRewirer
    where TSimulation : IItemPredictionSimulation
{
    private readonly BuildingPredictionExtender<TSimulation> _predictionExtender;
    private ICollection<ISimulationSystem> _appliedCollection;

    public SessionPredictionRewirer(BuildingPredictionExtender<TSimulation> predictionExtender)
    {
        _predictionExtender = predictionExtender;
    }

    public void ModifyPredictionSystems(
        ICollection<ISimulationSystem> simulationSystems,
        PredictionSystemsDependencies dependencies)
    {
        if (ReferenceEquals(_appliedCollection, simulationSystems))
            return;

        _appliedCollection = simulationSystems;
        _predictionExtender.ModifyPredictionSystems(simulationSystems, dependencies);
    }
}
