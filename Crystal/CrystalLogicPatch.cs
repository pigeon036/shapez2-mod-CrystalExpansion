using CrystalExpansion.Common;
using CrystalExpansion.Mode;
using ShapezShifter.SharpDetour;
using Unity.Mathematics;

namespace CrystalExpansion.Crystal;

// 修改游戏核心的形状连通性规则，使新晶体部件之间能够相互"连接"和"融合"。
// 原版 ShapeLogic 仅识别原版晶体码（四分 'c' / 六分 'h'），新晶体码（r/s/w/f/g）会被当作独立部件。
// 这两个 Postfix Hook 在原有判断结果上 OR 新的晶体相邻规则。
internal static class CrystalLogicPatch
{
    private static readonly HookCollection _hooks = new();

    public static void Initialize()
    {
        // Logic_IsConnected Postfix: 在原版判断后，追加新晶体的连接规则——
        // 两个新晶体部件在相邻层且部件索引相同时也被视为"连接"，维持层间粘合（如垂直堆叠的晶体块）。
        _hooks.Add(DetourHelper.CreateStaticPostfixHook<ShapePartReference, ShapePartReference, int, bool>(
            (first, second, partCount) => ShapeLogic.Logic_IsConnected(first, second, partCount),
            (first, second, partCount, result) =>
                result || (IsCrystalPart(first) && IsCrystalPart(second)
                    && math.abs(first.LayerIndex - second.LayerIndex) == 1
                    && first.PartIndex == second.PartIndex)));

        // Logic_IsFused Postfix: 在原版判断后，追加新晶体的融合规则——
        // 两个新晶体部件在相同层且部件索引相邻（FastMath.SafeMod 环形包裹），
        // 或在相邻层且相同部件索引时，也被视为"融合"，模拟晶体超结构的侧向/垂直连接。
        _hooks.Add(DetourHelper.CreateStaticPostfixHook<ShapePartReference, ShapePartReference, int, bool>(
            (first, second, partCount) => ShapeLogic.Logic_IsFused(first, second, partCount),
            (first, second, partCount, result) =>
                result || (IsCrystalPart(first) && IsCrystalPart(second) && (
                    (first.LayerIndex == second.LayerIndex && (
                        FastMath.SafeMod(first.PartIndex + 1, partCount) == second.PartIndex ||
                        FastMath.SafeMod(first.PartIndex - 1, partCount) == second.PartIndex)) ||
                    (first.PartIndex == second.PartIndex && math.abs(first.LayerIndex - second.LayerIndex) == 1)))));
    }

    // 判断是否为任何晶体（包括原版晶体，因 CrystalCodes 在六分模式下也包含 'h'）。
    private static bool IsCrystalPart(ShapePartReference shapePartReference)
        => ModeConfig.CrystalCodes.Contains(shapePartReference.Part.Shape.Code);

    public static void Dispose()
    {
        _hooks.Dispose();
    }
}
