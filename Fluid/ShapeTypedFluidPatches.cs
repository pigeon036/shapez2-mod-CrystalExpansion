using System;
using CrystalExpansion.Common;
using Game.Orchestration;
using MonoMod.RuntimeDetour;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Fluid;

// ShapeTypedFluid 系统的中心调度器。所有子补丁通过 partial class 分布在多个文件中，
// 每个文件负责一个游戏子系统（序列化、渲染、建筑、CrystalGenerator 等）。
// Apply 方法在 GameDataHook.Initialize 中调用，早于 Init_3_2 前缀钩子。
// 其中需要 _orch 引用的子补丁（ConstantSignal、CrystalGenerator 等）
// 直到 StoreOrchestrator 在 Init_3_2 前缀中调用后才可用。
internal static partial class ShapeTypedFluidPatches
{
    private static readonly HookCollection _hooks = new();
    private static ILogger _logger;
    // orchestrator 引用在 Init_3_2 前缀中设置，由多个子补丁共享。
    private static GameSessionOrchestrator _orch;

    public static void Apply(ILogger logger)
    {
        _logger = logger;
        try
        {
            PatchSerialize(logger);
            PatchDeserialize(logger);
            PatchGetData(logger);
            PatchCrystalGenerator(logger);
            PatchCrystalOperation(logger);
            PatchMixer(logger);
            PatchPainter(logger);
            PatchConstantSignalResolve(logger);
            PatchConstantSignalPrePopulate(logger);
            PatchShapeFactories(logger);
            logger.Info?.Log("ShapeTypedFluidPatches: all patches applied");
        }
        catch (Exception ex)
        {
            logger.Error?.Log($"ShapeTypedFluidPatches: Apply failed - {ex}");
        }
    }

    // 在 Init_3_2 前缀中调用，为后续子补丁提供 orchestrator 引用。
    public static void StoreOrchestrator(GameSessionOrchestrator orchestrator)
    {
        _orch = orchestrator;
    }

    public static void Dispose()
    {
        _hooks.Dispose();
    }
}
