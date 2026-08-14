using System;
using System.Collections.Generic;
using Game.Content.Features.Predictions;
using Game.Core.Content.Buildings;
using Game.Core.Rendering.MeshGeneration;
using Game.Core.Simulation;
using Global.Core;
using MonoMod.RuntimeDetour;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Hijack;
using ShapezShifter.Hijack.Predictions;
using ShapezShifter.SharpDetour;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Common.BuildingMirror;

// 通用建筑镜像服务（独立可移植）。
//
// 为通过 Shifter Atomic API 注册的建筑补上游戏原生镜像能力：Atomic API 每组只放 1 个定义
// → SinglePlacer（无镜像）。本服务在建筑创建后向目标组注入一个 Y 轴镜像孪生定义，使组内定义
// 数变为 2，游戏随后自动创建 FlippableSinglePlacer（含镜像键 building-placement.mirror）。
//
// 用法：
//   _mirror = new BuildingMirrorService(logger);
//   _mirror.Mirror(new BuildingDefinitionId("Dissolver"))
//          .WithSimulation(new DissolverFactoryBuilder())
//          .WithPrediction(new DissolverPredictionFactoryBuilder());
//   ...
//   _mirror.Dispose();
//
// 注意：完整动态镜像要求使用方的动态 DrawData 实现 IBuildingMirrorableCustomDrawData。
public sealed class BuildingMirrorService : IDisposable
{
    private readonly ILogger _logger;
    private readonly List<BuildingDefinitionId> _targets = new();
    private readonly List<RewirerHandle> _systemHandles = new();
    private readonly Hook _factoryHook;

    // 构造即安装工厂后置钩。钩在 Shifter 的 BuildingsInterceptor 之后加入，MonoMod 使其处于
    // 最外层，后置逻辑在 Shifter 填充完组之后运行，从而看到完整的组定义。
    public BuildingMirrorService(ILogger logger)
    {
        _logger = logger;
        _factoryHook = DetourHelper
            .CreateStaticPostfixHook<IBuildingCatalogPair, AuthoringBuildings, IMeshCache, VisualThemeBaseResources, GameBuildings>(
                original: (catalog, meta, meshCache, resources) =>
                    GameModeBuildingsFactory.FromMetadata(catalog, meta, meshCache, resources),
                postfix: Postfix);
    }

    private GameBuildings Postfix(
        IBuildingCatalogPair catalog,
        AuthoringBuildings meta,
        IMeshCache meshCache,
        VisualThemeBaseResources theme,
        GameBuildings gameBuildings)
    {
        foreach (BuildingDefinitionId target in _targets)
            MirrorInjector.TryInject(gameBuildings, meshCache, target, _logger);

        return gameBuildings;
    }

    // 注册一个待镜像的建筑（按原始定义 Id）。返回目标句柄用于附加模拟 / 预测系统。
    public MirrorTarget Mirror(BuildingDefinitionId originalId)
    {
        _targets.Add(originalId);
        return new MirrorTarget(this, originalId, _logger);
    }

    internal void TrackHandle(RewirerHandle handle)
    {
        _systemHandles.Add(handle);
    }

    public void Dispose()
    {
        foreach (RewirerHandle handle in _systemHandles)
            GameRewirers.RemoveRewirer(handle);
        _systemHandles.Clear();

        _factoryHook.Dispose();
    }
}

// 单个镜像目标的可选配置：为镜像定义补注册模拟 / 预测系统。
//
// 必要性：动态渲染按 TSimulation 类型匹配（复制 CustomData 即可），但模拟 / 预测系统按
// BuildingDefinitionId 精确匹配。镜像定义是新 Id，必须为其单独注册一套模拟 / 预测系统，
// 复用与原建筑相同的工厂构建器。游戏原生（如 Painter）也是逐 definition.Id 各注册一套。
//
// 提供与 Shifter 两种工厂形态对应的重载：无状态 / 有状态模拟，无配置 / 带配置预测。
public sealed class MirrorTarget
{
    private readonly BuildingMirrorService _service;
    private readonly BuildingDefinitionId _mirrorId;
    private readonly ILogger _logger;

    internal MirrorTarget(BuildingMirrorService service, BuildingDefinitionId originalId, ILogger logger)
    {
        _service = service;
        _mirrorId = MirrorNaming.MirrorId(originalId);
        _logger = logger;
    }

    // 无状态模拟。
    public MirrorTarget WithSimulation<TSimulation>(
        IBuildingSimulationFactoryBuilder<TSimulation> factoryBuilder)
        where TSimulation : ISimulation
        => Register<ISimulationSystemsRewirer>(
            new BuildingSimulationExtender<TSimulation>(_mirrorId, factoryBuilder, _logger));

    // 有状态模拟（晶化器 / 溶解器使用此形态）。
    public MirrorTarget WithSimulation<TSimulation, TState, TConfig>(
        IBuildingSimulationFactoryBuilder<TSimulation, TState, TConfig> factoryBuilder)
        where TSimulation : ISimulation
        where TState : class, ISimulationState, new()
        => Register<ISimulationSystemsRewirer>(
            new BuildingSimulationExtender<TSimulation, TState, TConfig>(_mirrorId, factoryBuilder, _logger));

    // 预测（无附加配置形态；晶化器 / 溶解器使用此形态）。
    public MirrorTarget WithPrediction<TSimulation>(
        IBuildingPredictionFactoryBuilder<TSimulation> factoryBuilder)
        where TSimulation : IItemPredictionSimulation
        => Register<IPredictionSystemsRewirer>(
            new BuildingPredictionExtender<TSimulation>(_mirrorId, factoryBuilder, _logger));

    // 预测（带配置形态）。
    public MirrorTarget WithPrediction<TSimulation, TConfig>(
        IBuildingPredictionFactoryBuilder<TSimulation, TConfig> factoryBuilder)
        where TSimulation : IItemPredictionSimulation
        => Register<IPredictionSystemsRewirer>(
            new BuildingPredictionExtender<TSimulation, TConfig>(_mirrorId, factoryBuilder, _logger));

    private MirrorTarget Register<TRewirer>(TRewirer rewirer) where TRewirer : IRewirer
    {
        _service.TrackHandle(GameRewirers.AddRewirer(rewirer));
        return this;
    }
}

// 镜像定义命名规则。镜像 BuildingDefinition 的 Id 由原 Id 追加固定后缀构成，
// 与游戏原生 BuildingDefinitionFactory 一致（"Mirrored" 后缀）。该后缀必须在
// 「注入镜像定义」与「注册镜像模拟/预测系统」两处保持一致，否则模拟系统按 Id
// 精确匹配时找不到镜像实体。
internal static class MirrorNaming
{
    public const string MirrorSuffix = "Mirrored";

    public static BuildingDefinitionId MirrorId(BuildingDefinitionId originalId)
    {
        return new BuildingDefinitionId(originalId.Name + MirrorSuffix);
    }
}
