using Game.Content.Features.Fluids;
using Game.Core.Shape.Colors;
using ShapezShifter.SharpDetour;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Fluid;

// 渲染回退补丁。ShapeColorVisualizationScheme.GetData(IFluid) 用于查找流体的颜色渲染数据。
// 原始方法仅识别 ColorFluid 子类型，ShapeTypedFluid 会因类型不匹配而抛出异常。
// 此 Prefix Hook 在进入 GetData 前将 ShapeTypedFluid 解包为其内部的 ColorFluid。
internal static partial class ShapeTypedFluidPatches
{
    private static void PatchGetData(ILogger logger)
    {
        _hooks.Add(DetourHelper.CreatePrefixHook<ShapeColorVisualizationScheme, IFluid, ColorRenderData>(
            (self, fluid) => self.GetData(fluid),
            (self, fluid) => fluid is ShapeTypedFluid shapeTypedFluid ? shapeTypedFluid.ColorFluid : fluid));
        logger.Info?.Log("ShapeTypedFluidPatches: GetData patched");
    }
}
