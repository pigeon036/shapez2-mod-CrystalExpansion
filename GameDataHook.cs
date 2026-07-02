using System;
using Core.Logging;
using CrystalExpansion.Common;
using CrystalExpansion.Crystal;
using CrystalExpansion.Fluid;
using CrystalExpansion.Mode;
using CrystalExpansion.Wiki;
using Game.Content.Features;
using Game.Orchestration;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;

namespace CrystalExpansion;

internal static class GameDataHook
{
    private static readonly HookCollection _hooks = new();
    private static Core.Logging.ILogger _logger;

    // 整个模组的引导入口。挂载在 GameSessionOrchestrator.Init_3_2_EssentialManagers 上：
    // 此时 ShapesConfiguration 已加载但 GameData 尚未被其他系统消费，是注入新晶体的最晚安全时机。
    // 更早的 Init 步骤可能尚未加载 Parts 列表；更晚的步骤则已开始使用原始部件数据。
    public static void Initialize(ILogger logger)
    {
        _logger = logger;

        try
        {
            // 独立的静态钩子（不依赖 orchestrator 引用），提前安装。
            CrystalLogicPatch.Initialize();
            // ShapeTypedFluid 的 10 个子补丁也在此安装，但其中需要 _orch 引用的补丁
            // 直到 StoreOrchestrator 被调用后才可用。
            ShapeTypedFluidPatches.Apply(logger);

            _logger.Info?.Log("GameDataHook: creating prefix hook on GameSessionOrchestrator.Init_3_2...");
            // Prefix Hook: 在原方法执行前注入我们的初始化逻辑。
            // 返回 (data, isNew) 保持不变，允许原始方法继续执行。
            _hooks.Add(DetourHelper.CreatePrefixHook<GameSessionOrchestrator, ResearchManager.SerializedData, bool>(
                (self, data, isNew) => self.Init_3_2_EssentialManagers(data, isNew),
                (self, data, isNew) =>
                {
                    _logger.Info?.Log("GameDataHook: prefix fired");
                    try
                    {
                        IShapesConfiguration config = self.Mode.ShapesConfiguration;
                        // PartCount == 6 表示六分模式。
                        // 必须将原版晶体码从 'c' 改为 'h'，因为六分模式下
                        // FlexibleShapeDefinitionFactory.ShapePartByCode['c'] 默认解析为四分晶体部件。
                        if (config.PartCount == 6)
                        {
                            var crystalMeta = (MetaShapeSubPart)config.CrystalShapePart;
                            crystalMeta._Code = 'h';
                            _logger.Info?.Log("GameDataHook: hex crystal code changed to 'h'");
                        }
                        // 初始化顺序至关重要：
                        // 1. ModeConfig — 先构建模式配置（BaseCodes/CrystalCodes/FbxPaths）
                        // 2. NewCrystalRegistry — 依赖 ModeConfig 的映射表来插入变体部件
                        // 3. StoreOrchestrator — 后续 Hook 委托中需要 _orch 引用
                        ModeConfig.Initialize(config);
                        NewCrystalRegistry.Inject(config, _logger);
                        CrystalWikiRegistration.Inject(self, _logger);
                        ShapeTypedFluidPatches.StoreOrchestrator(self);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error?.Log($"GameDataHook: {ex}");
                    }
                    return (data, isNew);
                }));
            _logger.Info?.Log("GameDataHook: prefix hook created");
        }
        catch (Exception ex)
        {
            _logger.Error?.Log($"GameDataHook: Initialize failed - {ex}");
        }
    }

    public static void Dispose()
    {
        CrystalLogicPatch.Dispose();
        ShapeTypedFluidPatches.Dispose();
        _hooks.Dispose();
    }
}
