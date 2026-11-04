using System;
using System.Reflection;
using CrystalExpansion.Mode;
using Game.Content.Features.Signals;
using MonoMod.RuntimeDetour;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Fluid;

// 常量信号支持补丁——允许玩家通过信号塔的常量信号输入框创建 ShapeTypedFluid 信号。
//
// 自定义文本协议格式：fluid-{ShapeCode}-{ColorCode}
//   示例：fluid-R-r = 红色 ShapeTypedR 信号，fluid-F-g = 绿色 ShapeTypedF 信号
//
// 两个 Hook 分别处理：
// - ResolveValue：文本框内容 → ISignal 对象（正向解析）
// - GetPrePopulatedText：已有 ISignal → 文本框回显文本（反向格式化）
//
// 由于 ConstantSignalBuildingModuleDataProvider 通过 TextBox UI 控件暴露内部方法，
// 无公开可重写钩子，因此必须通过反射 Hook 非公开方法。
internal static partial class ShapeTypedFluidPatches
{
    private static readonly Type _providerType = typeof(ConstantSignalBuildingModuleDataProvider);

    // Hook 非公开的 ResolveValue 方法。
    // 当文本框中的内容以 "fluid-" 开头时，解析协议并构造 FluidSignal(ShapeTypedFluid)。
    // ShapeCode 必须存在于 ModeConfig.BaseCodes（即不是 Pin 或晶体码），
    // ColorCode 从游戏的颜色方案中查找（如果找不到则返回 NullSignal.Instance）。
    private static void PatchConstantSignalResolve(ILogger logger)
    {
        MethodInfo method = _providerType.GetMethod("ResolveValue", BindingFlags.NonPublic | BindingFlags.Instance);
        _hooks.Add(new Hook(method,
            new Func<Func<object, string, ISignal>, object, string, ISignal>(
                (original, self, text) =>
                {
                    string lower = text?.Trim().ToLowerInvariant() ?? "";
                    if (lower.StartsWith("fluid-"))
                    {
                        string[] parts = lower.Substring("fluid-".Length).Split('-');
                        if (parts.Length == 2 && parts[0].Length == 1 && parts[1].Length == 1)
                        {
                            char shapeCode = char.ToUpper(parts[0][0]);
                            if (ModeConfig.BaseCodes.Contains(shapeCode))
                            {
                                byte colorByte = (byte)parts[1][0];
                                IShapeColor color = _orch.Mode.ShapeColorScheme.Colors
                                    .FirstOrDefault(color => color.Code == colorByte);
                                if (color != null)
                                    return FluidSignal.From(
                                        ShapeTypedFluid.Create(shapeCode, ColorFluid.ForColor(color)));
                            }
                        }
                        // 协议格式正确但形状码/颜色码无效 → 返回空信号而非回退到原方法
                        return NullSignal.Instance;
                    }
                    return original(self, text);
                })));
        logger.Info?.Log("ShapeTypedFluidPatches: ConstantSignal ResolveValue patched");
    }

    // Hook 非公开的 GetPrePopulatedText 方法。
    // 当已有信号值为 ShapeTypedFluid 时，反向格式化为 "fluid-{ShapeCode}-{ColorCode}" 字符串，
    // 确保 UI 文本框正确回显（而非显示默认文本或错误字符串）。
    private static void PatchConstantSignalPrePopulate(ILogger logger)
    {
        MethodInfo method = _providerType.GetMethod("GetPrePopulatedText", BindingFlags.NonPublic | BindingFlags.Instance);
        _hooks.Add(new Hook(method,
            new Func<Func<object, ISignal, string>, object, ISignal, string>(
                (original, self, signal) =>
                {
                    if (signal is FluidSignal fluidSignal && fluidSignal.Value is ShapeTypedFluid shapeTypedFluid)
                        return $"fluid-{shapeTypedFluid.ShapeCode}-{(char)shapeTypedFluid.ColorFluid.Color.Code}";
                    return original(self, signal);
                })));
        logger.Info?.Log("ShapeTypedFluidPatches: ConstantSignal GetPrePopulatedText patched");
    }
}