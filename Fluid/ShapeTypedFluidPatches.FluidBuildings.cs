using System.Reflection;
using Game.Content.Features.Fluids;
using Game.Core.Simulation;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Fluid;

// ShapeTypedFluid → ColorFluid 安全转换补丁，使 ShapeTypedFluid 在原版流体建筑中可用。
//
// Mixer（混合器）：Prefix Hook 在 Mix 调用前将两个输入流体中的 ShapeTypedFluid 解包为 ColorFluid。
// 混合器内部通过颜色混合运算，ShapeTypedFluid 的 ShapeCode 无关紧要——仅颜色参与混合。
//
// Painter（上色器）：Postfix Hook 在 Update 执行后检查 State.CurrentProcessingPaint，
// 如果管道网络交付了 ShapeTypedFluid，则还原为 ColorFluid。
// 上色器的内部状态依赖 ColorFluid 进行着色计算，ShapeTypedFluid 会破坏其颜色解析逻辑。
//
// VirtualPainter（模拟上色器）：Prefix Hook 在 TryExecute 调用前将
// ShapeTypedFluid 解包为 ColorFluid。虚拟上色器无 CurrentProcessingPaint 缓冲，
// 信号到达后同帧调用 TryExecute，必须用 PREFIX 在 is ColorFluid 检查前转换。
internal static partial class ShapeTypedFluidPatches
{
    private delegate bool TryExecutePaintTopmostOrig(ShapeOperationPaintTopmost self, IItem input1, IItem input2, ref IItem output);
    private delegate bool TryExecutePaintTopmostHook(TryExecutePaintTopmostOrig original, ShapeOperationPaintTopmost self, IItem input1, IItem input2, ref IItem output);

    private static void PatchMixer(ILogger logger)
    {
        _hooks.Add(DetourHelper.CreatePrefixHook<MixerSimulation, IFluid, IFluid, IFluid>(
            (self, f0, f1) => self.Mix(f0, f1),
            (self, f0, f1) => (
                f0 is ShapeTypedFluid s0 ? s0.ColorFluid : f0,
                f1 is ShapeTypedFluid s1 ? s1.ColorFluid : f1)));
        logger.Info?.Log("ShapeTypedFluidPatches: Mixer patched");
    }

    private static void PatchPainter(ILogger logger)
    {
        _hooks.Add(DetourHelper.CreatePostfixHook<PainterSimulation, Ticks, Ticks>(
            (self, start, delta) => self.Update(start, delta),
            (self, start, delta) =>
            {
                if (self.State.CurrentProcessingPaint is ShapeTypedFluid shapeTypedFluid)
                    self.State.CurrentProcessingPaint = shapeTypedFluid.ColorFluid;
            }));
        logger.Info?.Log("ShapeTypedFluidPatches: Painter patched");
    }

    private static void PatchVirtualPainter(ILogger logger)
    {
        MethodInfo method = typeof(ShapeOperationPaintTopmost).GetMethod(
            nameof(ShapeOperationPaintTopmost.TryExecute));
        _hooks.Add(new Hook(method,
            (TryExecutePaintTopmostHook)((TryExecutePaintTopmostOrig original, ShapeOperationPaintTopmost self, IItem input1, IItem input2, ref IItem output) =>
            {
                if (input2 is ShapeTypedFluid stf)
                    input2 = stf.ColorFluid;
                return original(self, input1, input2, ref output);
            })));
        logger.Info?.Log("ShapeTypedFluidPatches: VirtualPainter patched");
    }
}
