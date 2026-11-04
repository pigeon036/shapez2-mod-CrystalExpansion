using System;
using System.Linq;
using Core.Localization;
using CrystalExpansion.Common;
using ShapezShifter.Hijack;
using UnityEngine;

namespace CrystalExpansion.Toolbar;

// 晶体工具栏接线器：
// 1. 把原版晶体生成器条目换成自定义图标的 PlacementToolbarElementData，交给 ToolbarGroupBuilder
//    （原版条目的图标来自只读的 BuildingDefinition.Icon，想换图标只能新建 PlacementToolbarElementData）
// 2. 调用 ToolbarGroupBuilder.InsertAt，用「晶体工具」组替换原版条目；条目缺失时由 builder 兜底
//
// 与执行顺序无关：本类先跑或后跑都成立——组节点先插入树，之后 Shifter 的 ToolbarRewirer 收集到条目时，
// builder 会直接刷新同一组节点的 Children（见 ToolbarGroupBuilder.BeginBuild / RefreshIfInserted）
internal sealed class CrystalToolbarRewirer : IToolbarDataRewirer
{
    private const string CrystalGeneratorDefinitionGroupId = "CrystalGeneratorDefaultVariant";

    private static readonly Func<IToolbarElementData, bool> _isCrystalGenerator =
        ToolbarTree.ByBuildingDefinitionGroupId(CrystalGeneratorDefinitionGroupId);

    private readonly Sprite _crystalGeneratorOverrideIcon;
    private readonly ToolbarGroupBuilder _builder;

    public CrystalToolbarRewirer(Sprite crystalGenOverrideIcon, ToolbarGroupBuilder builder)
    {
        _crystalGeneratorOverrideIcon = crystalGenOverrideIcon;
        _builder = builder;
    }

    // 定位原版晶体生成器条目 → 换成自定义图标的 PlacementToolbarElementData 交给 builder
    // → 由 builder 构建并插入「晶体工具」组
    //
    // 之所以新建 PlacementToolbarElementData：原版 BuildingBasedPlacementToolbarElementData 的图标
    // 取自只读的 BuildingDefinition.Icon，只有 PlacementToolbarElementData 能带自定义 Icon；
    // Title/Description/PlacementInitiatorId 从原版条目复制，保证放置行为不变
    ToolbarData IToolbarDataRewirer.ModifyToolbarData(ToolbarData toolbarData)
    {
        // 每次工具栏重建都会拿到新的 ToolbarData 实例；builder 据此清空上一轮收集的条目
        _builder.BeginBuild(toolbarData);

        (IParentToolbarElementData parent, int index) = ToolbarTree.Find(
            toolbarData.RootToolbarElement, _isCrystalGenerator);

        if (parent != null)
        {
            BuildingBasedPlacementToolbarElementData original =
                (BuildingBasedPlacementToolbarElementData)parent.Children.ToList()[index];

            PlacementToolbarElementData replacement = new(
                (LazyLocalizedText)original.BuildingDefinition.Title,
                (LazyLocalizedText)original.BuildingDefinition.Description,
                original.PlacementInitiatorId,
                _crystalGeneratorOverrideIcon)
            { SectionIndex = 0, EntityType = EntityType.Building };
            _builder.Add(replacement, 0);
        }

        // 地标存在 → Replace 原条目；缺失 → InsertAt 内部兜底（追加到根节点 + warning）
        _builder.InsertAt(toolbarData.RootToolbarElement, _isCrystalGenerator);
        return toolbarData;
    }
}
