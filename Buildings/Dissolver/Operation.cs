using System;
using System.Collections.Generic;
using CrystalExpansion.Fluid;
using CrystalExpansion.Mode;
using Game.Content.Features;

namespace CrystalExpansion.Buildings.Dissolver;

// 溶解操作——统计输入形状中的基础部件数量，选取出现次数最多的部件码，
// 生成对应的 ShapeTypedFluid。无匹配时返回 null（消耗形状，不产出流体）。
public class DissolverOperation : IItemOperation2In1Out
{
    public ShapeTypedFluid Execute(ShapeDefinition shape, ColorFluid color)
    {
        char dominantCode = PickDominantBaseCode(shape);
        return dominantCode == '\0' ? null : ShapeTypedFluid.Create(dominantCode, color);
    }

    bool IItemOperation2In1Out.TryExecute(IItem input1, IItem input2, out IItem output)
    {
        if (input1 is ShapeItem shape)
        {
            // ShapeTypedFluid 作为颜色来源时解包为 ColorFluid
            if (input2 is ShapeTypedFluid shapeTypedFluid)
                input2 = shapeTypedFluid.ColorFluid;

            if (input2 is ColorFluid color)
            {
                output = Execute(shape.Definition, color);
                return output != null;
            }
        }
        output = null;
        return false;
    }

    // 使用栈分配（零堆分配）的计数数组统计每个基础部件码的出现频率。
    // Span<int>[128] 以 ASCII 索引为基础，仅统计 ModeConfig.BaseCodes 中的码。
    // 时间复杂度 O(layers × partCount)，无堆分配。
    // 返回值 '\0' 是哨兵值，表示"未找到基础部件码"，调用方将其转换为 null。
    private static char PickDominantBaseCode(ShapeDefinition shape)
    {
        HashSet<char> baseCodes = ModeConfig.BaseCodes;
        if (baseCodes == null || baseCodes.Count == 0) return '\0';

        // stackalloc 将计数数组分配在栈上，避免 GC 压力
        Span<int> counts = stackalloc int[128];
        counts.Clear();

        foreach (ShapeLayer layer in shape.GenerateClonedLayers())
        {
            for (int j = 0; j < shape.PartCount; j++)
            {
                if (layer.Parts[j].IsEmpty) continue;
                char code = layer.Parts[j].Shape.Code;
                if (code < 128 && baseCodes.Contains(code))
                    counts[code]++;
            }
        }

        char best = '\0';
        foreach (char code in baseCodes)
        {
            if (counts[code] > counts[best])
                best = code;
        }
        return best;
    }
}
