using Core.Collections.Scoped;
using CrystalExpansion.Fluid;
using CrystalExpansion.Mode;
using Game.Content.Features;

namespace CrystalExpansion.Buildings.Crystallizer;

// 晶化操作负载——定义一次晶体转化所需的输入参数。
public readonly struct CrystallizeVariantPayload : IShapeOperationInput
{
    public readonly ShapeDefinition Shape;
    public readonly IShapeColor Color;
    public readonly char TargetShapeCode;

    // UniqueOperationId 用于游戏内部的次形状缓存去重。
    // 位布局：bit 0-31 = shape UID（形状去重）
    //         bit 32-39 = ColorCode（不同颜色需不同结果）
    //         bit 40-47 = TargetShapeCode（不同目标部件需不同结果）
    // 这确保 (shape, color, targetCode) 三元组独立缓存，避免不同颜色的晶体结果混淆。
    public ulong UniqueOperationId => (uint)Shape.Id.Uid | ((ulong)Color.Code << 32) | ((ulong)TargetShapeCode << 40);

    public CrystallizeVariantPayload(ShapeDefinition shape, ShapeTypedFluid fluid)
    {
        Shape = shape;
        Color = fluid.ColorFluid.Color;
        TargetShapeCode = fluid.ShapeCode;
    }
}

// 晶化操作结果——包含双重输出：
// Result：完全晶体化后的形状哈希（所有匹配部件替换为晶体变体）
// Unchanged：仅包含未被替换的部件的折叠形状（用于渲染底座）
public readonly struct CrystallizeVariantResult
{
    public readonly ShapeId Result;
    public readonly ShapeId Unchanged;

    public CrystallizeVariantResult(ShapeId result, ShapeId unchanged)
    {
        Result = result;
        Unchanged = unchanged;
    }
}

public class CrystallizerOperation : ShapeOperation<CrystallizeVariantPayload, CrystallizeVariantResult>, IItemOperation2In1Out
{
    private readonly IShapesConfiguration ShapesConfiguration;

    public CrystallizerOperation(
        IShapesConfiguration shapesConfiguration,
        IShapeRegistry shapeRegistry,
        IShapeIdManager shapeIdManager)
        : base(shapeRegistry, shapeIdManager)
    {
        ShapesConfiguration = shapesConfiguration;
    }

    // IItemOperation2In1Out 入口——同时支持 ShapeTypedFluid 和普通 ColorFluid。
    // ShapeTypedFluid：使用其携带的具体 ShapeCode 进行定向替换
    // ColorFluid：使用 ForAllParts 通配符 '*'，晶体化所有非 Pin/晶体部件
    public bool TryExecute(IItem input1, IItem input2, out IItem output)
    {
        if (input1 is ShapeItem shapeItem1 && input2 is ShapeTypedFluid typedFluid)
        {
            CrystallizeVariantResult result = Execute(new CrystallizeVariantPayload(shapeItem1.Definition, typedFluid));
            output = ShapeRegistry.GetItem(result.Result);
            return true;
        }
        if (input1 is ShapeItem shapeItem2 && input2 is ColorFluid colorFluid)
        {
            ShapeTypedFluid shapeTypedFluid = ShapeTypedFluid.ForAllParts(colorFluid);
            CrystallizeVariantResult result = Execute(new CrystallizeVariantPayload(shapeItem2.Definition, shapeTypedFluid));
            output = ShapeRegistry.GetItem(result.Result);
            return true;
        }
        output = null;
        return false;
    }

    public override CrystallizeVariantResult ExecuteInternal(CrystallizeVariantPayload input)
    {
        ShapeLayer[] array = input.Shape.GenerateClonedLayers();
        using ScopedList<ShapePartReference> unchangedShapePart = ScopedList<ShapePartReference>.Get();

        for (int i = 0; i < array.Length; i++)
        {
            ShapeLayer shapeLayer = array[i];
            ShapeLayer originalLayer = input.Shape.Layers[i];
            for (int j = 0; j < input.Shape.PartCount; j++)
            {
                ref ShapePart reference = ref shapeLayer.Parts[j];
                ShapePart originalPart = originalLayer.Parts[j];

                // willReplace: 四个条件决定是否用晶体部件替换此部件。
                // 1. !IsEmpty：空槽不应生长晶体（由 CrystalGenerator 处理空位）
                // 2. != PinCode：引脚部件永不晶体化
                // 3. != CrystalCode：已有晶体部件不重复处理（避免二次覆盖）
                // 4. 通配符 '*' 或精确匹配 TargetShapeCode：控制哪些形状码被替换
                bool willReplace = !originalPart.IsEmpty
                    && originalPart.Shape.Code != ShapesConfiguration.PinShapePart.Code
                    && originalPart.Shape.Code != ShapesConfiguration.CrystalShapePart.Code
                    && (input.TargetShapeCode == ShapeTypedFluid.AllPartsCode || input.TargetShapeCode == originalPart.Shape.Code);

                if (willReplace)
                {
                    var dict = ModeConfig.BaseCodeToCrystalPart;
                    // 从字典查找对应晶体部件；回退到游戏默认 CrystalShapePart
                    if (dict?.TryGetValue(reference.Shape.Code, out var crystalShapePart) != true)
                        crystalShapePart = ShapesConfiguration.CrystalShapePart;
                    reference.Shape = crystalShapePart;
                    reference.Color = input.Color;
                }
                else if (!originalPart.IsEmpty)
                    // 未被替换的非空部件：收集起来用于构建"不变底座"
                    unchangedShapePart.Add(new ShapePartReference(i, j, originalPart));
            }
        }

        string hash = ShapeDefinition.ComputeHash(array);
        ShapeId result = ShapeIdManager.Resolve(hash);

        // 通过 ShapeLogic.Fold 将未替换部件折叠为独立形状。
        // 此形状用作渲染的底座层——非晶体部件显示为普通形状，晶体变体在底座之上以 alpha 生长。
        // 如果 Fold 返回 null（无未替换部件），则 Unchanged = Invalid。
        ShapeId unchanged = ShapeId.Invalid;
        ShapeLayer[] folded = ShapeLogic.Fold(unchangedShapePart, input.Shape.PartCount);
        if (folded != null && folded.Length != 0)
        {
            string hash2 = ShapeDefinition.ComputeHash(folded);
            unchanged = ShapeIdManager.Resolve(hash2);
        }

        return new CrystallizeVariantResult(result, unchanged);
    }
}
