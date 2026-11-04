using CrystalExpansion.Buildings.Crystallizer;
using CrystalExpansion.Buildings.Dissolver;
using CrystalExpansion.Buildings.VirtualCrystallizer;
using CrystalExpansion.Buildings.VirtualDissolver;
using CrystalExpansion.Common;
using ShapezShifter.Kit;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Buildings;

// 集中列出本模组注册的全部建筑：
// 物理建筑（晶化器 / 溶解器）共享一个工具栏组，模拟建筑自己锚定原版条目。
internal static class BuildingRegistrations
{
    public static void RegisterAll(
        ILogger logger,
        ModFolderLocator locator,
        ToolbarGroupBuilder toolbarGroup,
        BuildingPredictionRegistry predictionRegistry)
    {
        CrystallizerRegistration.Register(logger, locator, toolbarGroup, predictionRegistry);
        DissolverRegistration.Register(logger, locator, toolbarGroup, predictionRegistry);
        VirtualCrystallizerRegistration.Register(logger, locator);
        VirtualDissolverRegistration.Register(logger, locator);
    }
}
