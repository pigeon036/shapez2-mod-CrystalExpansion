using System;
using System.Reflection;
using Game.Content.Features.Fluids;
using Game.Core.Serialization;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Fluid;

// 自定义二进制序列化协议 — ShapeTypedFluid 的存档持久化。
//
// 协议格式（tag 标识流体类型）：
//   byte 0  → null (无流体)
//   byte 1  + byte colorCode → ColorFluid（原版格式）
//   byte 2  + byte shapeCode + [递归调用 original 读取内部 ColorFluid] → ShapeTypedFluid
//
// 序列化使用 DetourHelper Prefix：拦截 FluidSerializer.Serialize，检测 ShapeTypedFluid
// 后写入 tag 2 + shapeCode，然后改写参数让原始方法序列化为 ColorFluid。
//
// 反序列化使用裸 Hook（完全替换）：因为原始 Deserialize 方法不理解 tag 2，
// 必须自行读取 tag 并在 case 2 时先消耗 shapeCode 再递归调用 original 序列化 inner。
// case 1 分支内联了颜色码反序列化逻辑，因为 original 调用会重复读取已被消耗的 tag 字节。
internal static partial class ShapeTypedFluidPatches
{
    private static void PatchSerialize(ILogger logger)
    {
        // Prefix Hook: 在原始 Serialize 执行前检查 fluid 类型。
        // 如果是 ShapeTypedFluid，写入 tag=2 和 shapeCode，将 ColorFluid 作为参数传给原始方法。
        _hooks.Add(DetourHelper.CreatePrefixHook<FluidSerializer, IFluid, ISerializationVisitor>(
            (self, fluid, visitor) => self.Serialize(fluid, visitor),
            (self, fluid, visitor) =>
            {
                if (fluid is ShapeTypedFluid shapeTypedFluid)
                {
                    visitor.WriteByte_1(2);
                    visitor.WriteByte_1((byte)shapeTypedFluid.ShapeCode);
                    return (shapeTypedFluid.ColorFluid, visitor);
                }
                return (fluid, visitor);
            }));
        logger.Info?.Log("ShapeTypedFluidPatches: Serialize patched");
    }

    private static void PatchDeserialize(ILogger logger)
    {
        // 通过反射获取原始的私有 Deserialize 方法以构造完整替换 Hook
        MethodInfo method = typeof(FluidSerializer).GetMethod(
            nameof(FluidSerializer.Deserialize),
            new[] { typeof(ISerializationVisitor) });
        // 使用 Func<Func<...>, ..., ...> 模式构造 Hook：外层 Func 接收 original 委托和参数，
        // 内层 Func 是原始方法签名。
        _hooks.Add(new Hook(method, new Func<Func<FluidSerializer, ISerializationVisitor, IFluid>, FluidSerializer, ISerializationVisitor, IFluid>(
            (original, self, visitor) =>
            {
                byte tag = visitor.ReadByte_1();
                switch (tag)
                {
                    case 0:
                        return null;
                    // case 1: 内联颜色流体反序列化，而非调用 original。
                    // 原因：tag 2 的处理过程中已消耗 shapeCode 字节，
                    // 之后通过 original(self, visitor) 递归时读取器期望的是 tag 字节，
                    // 而非已被消耗的 inner 颜色码。
                    case 1:
                        {
                            byte code = visitor.ReadByte_1();
                            IShapeColor color = self.ColorScheme.Colors.FirstOrDefault(color => color.Code == code);
                            if (color == null)
                                throw new Exception($"Unknown color code: {code}");
                            return ColorFluid.ForColor(color);
                        }
                    // case 2: 读取 shapeCode → 递归调用 original 解析 inner ColorFluid →
                    // 创建 ShapeTypedFluid 单例。
                    case 2:
                        {
                            char shapeCode = (char)visitor.ReadByte_1();
                            IFluid innerFluid = original(self, visitor);
                            return ShapeTypedFluid.Create(shapeCode, (ColorFluid)innerFluid);
                        }
                    default:
                        throw new Exception($"Invalid serialized fluid tag: {tag}");
                }
            })));
        logger.Info?.Log("ShapeTypedFluidPatches: Deserialize patched");
    }
}
