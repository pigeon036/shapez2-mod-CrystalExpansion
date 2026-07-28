using CrystalExpansion.Buildings.VirtualCrystallizer;
using CrystalExpansion.Buildings.VirtualDissolver;
using CrystalExpansion.Buildings.Crystallizer;
using CrystalExpansion.Buildings.Dissolver;
using CrystalExpansion.Common.BuildingMirror;
using CrystalExpansion.Common;
using CrystalExpansion.Toolbar;
using JetBrains.Annotations;
using ShapezShifter.Hijack;
using ShapezShifter.Kit;
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion;

[UsedImplicitly]
public class CrystalExpansionMod : IMod
{
    private RewirerHandle dataHandle;
    private CrystalToolbarGrouper crystaltoolbarGrouper;
    private BuildingMirrorService mirror;

    // 构造时依次完成三阶段初始化：
    // 1. GameDataHook — 注入 MonoMod 钩子到游戏初始化流程（ModeConfig / 晶体变体注册）
    // 2. 建筑注册 — 通过 Shifter Atomic API 声明晶化器和溶解器
    // 3. 工具栏 — 创建分组构建器，注册晶体生成器替换逻辑并安装优先级钩子
    // 工具栏构建器在构造时创建并传给两个 Registration，保证建筑和组共享同一个 GroupToolbarElementData 节点。
    public CrystalExpansionMod(ILogger logger)
    {
        GameDataHook.Initialize(logger);

        ModFolderLocator locator = ModDirectoryLocator.CreateLocator<CrystalExpansionMod>().SubLocator("Resources");

        Sprite groupIcon = ShapezShifter.Textures.FileTextureLoader.LoadTextureAsSprite(
            locator.SubPath("ToolbarIcon/CrystalExToolbar_Icon.png"), out _);
        Sprite crystalGeneratorIcon = ShapezShifter.Textures.FileTextureLoader.LoadTextureAsSprite(
            locator.SubPath("ToolbarIcon/NewCrystalGenerator_Icon.png"), out _);

        ToolbarGroupBuilder toolbarGroup = new("building.crystal-group.title", groupIcon);
        CrystallizerRegistration.Register(logger, locator, toolbarGroup);
        DissolverRegistration.Register(logger, locator, toolbarGroup);

        VirtualCrystallizerRegistration.Register(logger, locator);
        VirtualDissolverRegistration.Register(logger, locator);

        crystaltoolbarGrouper = new CrystalToolbarGrouper(crystalGeneratorIcon, toolbarGroup);
        dataHandle = GameRewirers.AddRewirer<IToolbarDataRewirer>(crystaltoolbarGrouper);

        // 为晶化器补上游戏原生镜像能力（Y 轴翻转，流体喂料格北↔南）。
        // 需复用与原建筑一致的模拟 / 预测工厂构建器（模拟/预测按 BuildingDefinitionId 精确匹配）。
        mirror = new BuildingMirrorService(logger);
        mirror.Mirror(new BuildingDefinitionId("Crystallizer"))
            .WithSimulation(new CrystallizerFactoryBuilder())
            .WithPrediction(new CrystallizerPredictionFactoryBuilder());
    }

    // Dispose 顺序：先从 GameRewirers 中移除 rewirer（移除优先级钩子），
    // 再销毁 grouper（释放其内部的 priority Hook），最后释放 GameDataHook（卸载所有游戏逻辑挂钩）。
    // 顺序错误会导致工具栏重建时触发已卸载的钩子。
    public void Dispose()
    {
        GameRewirers.RemoveRewirer(dataHandle);
        crystaltoolbarGrouper.Dispose();
        mirror.Dispose();
        GameDataHook.Dispose();
    }
}
