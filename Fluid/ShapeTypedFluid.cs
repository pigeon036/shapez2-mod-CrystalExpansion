using System.Collections.Concurrent;
using Game.Content.Features.Fluids;

namespace CrystalExpansion.Fluid;

// ShapeTypedFluid — 携带形状码的自定义 IFluid 实现。
// 可在管道网络中与 ColorFluid 互通（通过 ShapetypedFluidPatches 中的兼容层）。
// 每个 (ShapeCode, ColorFluid) 组合全局唯一单例，由 ConcurrentDictionary 缓存保证线程安全。
public class ShapeTypedFluid : IFluid
{
    // 哨兵值：当晶化器接收普通 ColorFluid（非特定形状码）时使用此通配符。
    // CrystallizerOperation 中 willReplace 条件会匹配所有非 Pin/非晶体部件。
    public const char AllPartsCode = '*';

    public char ShapeCode { get; }
    public ColorFluid ColorFluid { get; }

    private static readonly ConcurrentDictionary<(char, ColorFluid), ShapeTypedFluid> Cache = new();
    private static FluidRegistry _registry;

    private ShapeTypedFluid(char shapeCode, ColorFluid colorFluid)
    {
        ShapeCode = shapeCode;
        ColorFluid = colorFluid;
    }

    // 工厂方法——从缓存获取或创建单例。
    // 首次调用时延迟注册到 FluidRegistry：
    // _registry ??= 使用 null 条件运算符处理 Init 阶段 G 为 null 的情况；
    // 进入仿真循环后 G 已赋值，后续 Create 调用正常注册。
    public static ShapeTypedFluid Create(char shapeCode, ColorFluid colorFluid)
    {
        return Cache.GetOrAdd((shapeCode, colorFluid), key =>
        {
            ShapeTypedFluid shapeTypedFluid = new(key.Item1, key.Item2);
            _registry ??= StaticGameCoreAccessor.G?.FluidRegistry;
            _registry?.RegisterFluid(shapeTypedFluid);
            return shapeTypedFluid;
        });
    }

    // 便捷方法——创建可匹配所有基础形状码的通配流体。
    public static ShapeTypedFluid ForAllParts(ColorFluid colorFluid)
    {
        return Create(AllPartsCode, colorFluid);
    }
}