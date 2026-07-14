using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using CrystalExpansion.Mode;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Fluid;

// 六分模式哈希归一化补丁。
//
// 问题：FlexibleShapeDefinitionFactory.ShapePartByCode 是全局字典，默认将 'c' 解析为四分晶体部件。
// 在六分模式游戏中，形状哈希中的 'c' 字符（偶数位置 = 部件码）会被错误解析。
// 解决：在进入 ShapeDefinitionFactory 之前，将哈希中偶数位置的 'c' 替换为 OriginalCrystalCode（六分模式为 'h'）。
//
// 哈希格式：每个字符对为 (partCode, colorCode)。偶数索引（0, 2, 4...）为部件码，奇数索引为颜色码。
// 层之间用 ':' 分隔。
//
// ConcurrentDictionary 缓存：预测系统通过多线程调用 IShapeRegistry.TryGetShape，
// 绕过正常仿真循环的 lock 串行化——因此缓存必须线程安全。
internal static partial class ShapeTypedFluidPatches
{
    // TryCreateShapeDefinition 的自定义委托类型：ref 参数无法通过 DetourHelper 泛型自动解析。
    private delegate bool TryCreateShapeDefinitionOriginal(object self, string hash, ref ShapeDefinition def);
    private delegate bool TryCreateShapeDefinitionHook(TryCreateShapeDefinitionOriginal original, object self, string hash, ref ShapeDefinition def);

    private static readonly ConcurrentDictionary<string, string> _normalizedHashCache = new();

    private static string NormalizeCrystalHash(string hash)
    {
        // 四分模式无需归一化（OriginalCrystalCode 已经是 'c'）
        if (ModeConfig.OriginalCrystalCode == 'c')
            return hash;

        return _normalizedHashCache.GetOrAdd(hash, _ =>
        {
            string[] layers = hash.Split(':');
            StringBuilder sb = new(hash.Length + layers.Length);
            for (int i = 0; i < layers.Length; i++)
            {
                if (i > 0) sb.Append(':');
                string layer = layers[i];
                for (int j = 0; j < layer.Length; j++)
                {
                    // 仅对偶数索引（部件码位置）的 'c' 字符进行替换
                    // 颜色字节（奇数位置）保持原样
                    if (j % 2 == 0 && layer[j] == 'c')
                        sb.Append(ModeConfig.OriginalCrystalCode);
                    else
                        sb.Append(layer[j]);
                }
            }
            return sb.ToString();
        });
    }

    // 挂接到四种工厂入口点以覆盖所有形状定义创建的路径：
    // - FlexibleShapeDefinitionFactory.CreateShapeDefinition（Prefix）
    // - StrictShapeDefinitionFactory.CreateShapeDefinition（Prefix）
    // - IShapeDefinitionFactory.TryCreateShapeDefinition（裸 Hook，含 ref 参数）
    //   - 对 Flexible 和 Strict 工厂均安装此 Hook
    private static void PatchShapeFactories(ILogger logger)
    {
        _hooks.Add(DetourHelper.CreatePrefixHook<FlexibleShapeDefinitionFactory, string, ShapeDefinition>(
            (self, hash) => self.CreateShapeDefinition(hash),
            (self, hash) => NormalizeCrystalHash(hash)));

        _hooks.Add(DetourHelper.CreatePrefixHook<StrictShapeDefinitionFactory, string, ShapeDefinition>(
            (self, hash) => self.CreateShapeDefinition(hash),
            (self, hash) => NormalizeCrystalHash(hash)));

        PatchTryCreateHook(typeof(FlexibleShapeDefinitionFactory));
        PatchTryCreateHook(typeof(StrictShapeDefinitionFactory));

        logger.Info?.Log("ShapeTypedFluidPatches: ShapeFactories patched");
    }

    // 通过反射安装 TryCreateShapeDefinition 的 Hook。
    // ref ShapeDefinition 参数需要自定义委托类型，DetourHelper 泛型不支持 ref/out 参数解析。
    private static void PatchTryCreateHook(Type factoryType)
    {
        MethodInfo method = factoryType.GetMethod(
            nameof(IShapeDefinitionFactory.TryCreateShapeDefinition),
            BindingFlags.Public | BindingFlags.Instance);
        if (method == null) return;

        _hooks.Add(new Hook(method,
            (TryCreateShapeDefinitionHook)((TryCreateShapeDefinitionOriginal original, object self, string hash, ref ShapeDefinition def) =>
            {
                hash = NormalizeCrystalHash(hash);
                return original(self, hash, ref def);
            })));
    }
}