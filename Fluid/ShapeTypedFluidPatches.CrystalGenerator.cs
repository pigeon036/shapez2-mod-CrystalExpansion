using System;
using System.Reflection;
using CrystalExpansion.Mode;
using MonoMod.RuntimeDetour;
using ShapezShifter.SharpDetour;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Fluid;

// CrystalGenerator 兼容补丁——使原版晶体生成器支持 ShapeTypedFluid。
//
// 补丁分为两层：
// 1. 仿真层：Replace Hook 替换 CrystalGeneratorSimulation.TryCreateCrystal，在 ShapeTypedFluid
//    到达时触发晶体生成（而非原版仅支持 ColorFluid 生成统一晶体）
// 2. 预测层：裸 Hook 挂接 ShapeOperationCrystallize.TryExecute，使预测系统
//    与仿真使用相同的 BuildCrystal 逻辑。预测在仿真锁外运行，因此必须独立安装 Hook。
//
// BuildCrystal 遍历所有层/部件，在空位或 Pin 部件（标记晶体生长位置）处设置晶体部件。
// BaseCodeToCrystalPart 字典提供 O(1) 查找；回退到 CrystalShapePart 用于未知形状码。
internal static partial class ShapeTypedFluidPatches
{
    // TryExecute 的自定义委托：ref IItem 参数无法通过 DetourHelper 泛型自动解析
    private delegate bool TryExecuteOriginal(ShapeOperationCrystallize self, IItem input1, IItem input2, ref IItem output);

    private delegate bool TryExecuteHookDelegate(TryExecuteOriginal original, ShapeOperationCrystallize self, IItem input1, IItem input2, ref IItem output);

    private static void PatchCrystalGenerator(ILogger logger)
    {
        // 完全替换 TryCreateCrystal：原方法仅检查 CurrentProcessingPaint is ColorFluid
        _hooks.Add(DetourHelper.Replace<CrystalGeneratorSimulation, BeltLane, bool>(
            (self, lane) => self.TryCreateCrystal(lane),
            (self, lane) =>
            {
                if (self.State.CurrentProcessingPaint is ShapeTypedFluid shapeTypedFluid)
                    return TryCreateCrystal(lane, shapeTypedFluid, self.State);
                return false;
            }));
        logger.Info?.Log("ShapeTypedFluidPatches: CrystalGenerator patched");
    }

    private static void PatchCrystalOperation(ILogger logger)
    {
        // 通过反射获取 TryExecute 方法（含 ref IItem 参数）
        MethodInfo method = typeof(ShapeOperationCrystallize).GetMethod(
            nameof(ShapeOperationCrystallize.TryExecute));
        _hooks.Add(new Hook(method,
            (TryExecuteHookDelegate)((TryExecuteOriginal original, ShapeOperationCrystallize self, IItem input1, IItem input2, ref IItem output) =>
            {
                if (input1 is ShapeItem shape)
                {
                    if (input2 is ShapeTypedFluid shapeTypedFluid)
                        output = BuildCrystal(shape, shapeTypedFluid);
                    else if (input2 is ColorFluid)
                        // 普通 ColorFluid → 输出原形状不变（晶体生成器用于标记"不可晶体化"）
                        output = input1;
                    else
                        return original(self, input1, input2, ref output);
                    return true;
                }
                return original(self, input1, input2, ref output);
            })));
        logger.Info?.Log("ShapeTypedFluidPatches: CrystalOperation TryExecute patched");
    }

    // 核心晶体构建逻辑：克隆层 → 遍历部件 → 空/Pin 位替换为晶体部件 → 计算哈希
    private static ShapeItem BuildCrystal(ShapeItem shapeItem, ShapeTypedFluid shapeTypedFluid)
    {
        ShapeLayer[] layers = shapeItem.Definition.GenerateClonedLayers();
        int partCount = shapeItem.Definition.PartCount;
        for (int i = 0; i < layers.Length; i++)
            for (int j = 0; j < partCount; j++)
            {
                ref ShapePart part = ref layers[i].Parts[j];
                if (part.IsEmpty || part.Shape.Code == 'P')
                {
                    // 使用 ModeConfig 字典查找对应的晶体部件；回退到游戏的默认 CrystalShapePart
                    // 回退：如果 ShapeCode 在字典中不存在，使用游戏的默认 CrystalShapePart
                    var dict = ModeConfig.BaseCodeToCrystalPart;
                    if (dict?.TryGetValue(shapeTypedFluid.ShapeCode, out IShapeSubPart CrystalPart) != true)
                        CrystalPart = _orch.Mode.ShapesConfiguration.CrystalShapePart;
                    part.Shape = CrystalPart;
                    part.Color = shapeTypedFluid.ColorFluid.Color;
                }
            }

        string hash = ShapeDefinition.ComputeHash(layers);
        ShapeId resultId = _orch.ShapeIdManager.Resolve(hash);
        return ((IShapeRegistry)_orch.ShapeRegistry).GetItem(resultId);
    }

    // 在仿真中生成晶体：
    // 1. 从 lane 获取源形状
    // 2. 调用 BuildCrystal 生成新晶体形状
    // 3. 设置 State.CurrentSourceShape 和 CurrentCrystalOnlyShape 供渲染器绘制生长动画
    // 4. 替换 lane 上的物品为晶体形状
    private static bool TryCreateCrystal(BeltLane lane, ShapeTypedFluid shapeTypedFluid, CrystalGeneratorSimulationState state)
    {
        try
        {
            ShapeItem sourceShape = (ShapeItem)lane.Item;
            ShapeItem resultItem = BuildCrystal(sourceShape, shapeTypedFluid);
            state.CurrentSourceShape = sourceShape;
            state.CurrentCrystalOnlyShape = resultItem;
            lane.ReplaceItem(resultItem);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error?.Log($"CrystalGen: error - {ex}");
            return false;
        }
    }
}
