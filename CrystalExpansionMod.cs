using Core.Logging;
using CrystalExpansion.Buildings;
using CrystalExpansion.Buildings.Crystallizer;
using CrystalExpansion.Common;
using CrystalExpansion.Common.BuildingMirror;
using CrystalExpansion.Toolbar;
using JetBrains.Annotations;
using ShapezShifter.Kit;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion;

// 模组入口 = 组合根：只负责按阶段创建/装配各模块，并按相反顺序释放。
//
// 各模块职责：
// - GameDataHook        游戏数据阶段：安装 MonoMod 钩子，注入晶体变体 / Wiki / 流体补丁
// - BuildingRegistrations 建筑注册：集中列出全部建筑（物理建筑共享一个工具栏组）
// - ToolbarSetup        工具栏阶段：组构建器 + 原版条目接线器 + 执行顺序钩子
// - BuildingPredictionRegistry 会话级预测注册器
// - BuildingMirrorRegistry     镜像变体登记器
[UsedImplicitly]
public class CrystalExpansionMod : IMod
{
    private readonly BuildingPredictionRegistry predictionRegistry;
    private readonly ToolbarSetup toolbar;
    private readonly BuildingMirrorRegistry mirrorRegistry;

    public CrystalExpansionMod(ILogger logger)
    {
        // 日志规范（全项目统一）：
        //   游戏注入的 logger 已带 "<modId>[<version>]" 前缀（Game.Core.Modding.PrefixedLogger），
        //   这里再包一层模组名，玩家/support 才能按 "CrystalExpansion" 检索（Workshop 安装时只有数字 id）
        //   各子系统消息保持 "<Component>: ..." 形式（GameDataHook / ShapeTypedFluidPatches / ...）
        logger = new PrefixedLogger(logger, "[CrystalExpansion]");

        // 预测系统必须独立于 Atomic 链注册（避免预测关闭时建筑链无法重启）
        // 注册器由入口创建并持有，传给各建筑 Registration 自行注册预测工厂
        predictionRegistry = new BuildingPredictionRegistry(logger);

        GameDataHook.Initialize(logger);

        ModFolderLocator locator = ModDirectoryLocator.CreateLocator<CrystalExpansionMod>().SubLocator("Resources");

        // 工具栏组构建器由 ToolbarSetup 创建，物理建筑注册时共享同一个实例（同一个 GroupToolbarElementData 节点）
        toolbar = new ToolbarSetup(logger, locator);
        BuildingRegistrations.RegisterAll(logger, locator, toolbar.GroupBuilder, predictionRegistry);

        // 为晶化器补上游戏原生镜像能力（Y 轴翻转）。
        // 需复用与原建筑一致的模拟 / 预测工厂构建器（模拟/预测按 BuildingDefinitionId 精确匹配）
        mirrorRegistry = new BuildingMirrorRegistry(logger);
        mirrorRegistry.Mirror(new BuildingDefinitionId("Crystallizer"))
            .WithSimulation(new CrystallizerFactoryBuilder())
            .WithPrediction(new CrystallizerPredictionFactoryBuilder());
    }

    // Dispose 顺序与构造相反：工具栏（rewirer + 钩子）→ 镜像登记 → 预测注册器 → 游戏数据钩子
    // 工具栏必须最先释放：它的钩子参与工具栏重建，晚于其他模块释放会让重建走到已卸载的钩子
    public void Dispose()
    {
        toolbar.Dispose();
        mirrorRegistry.Dispose();
        predictionRegistry.Dispose();
        GameDataHook.Dispose();
    }
}
