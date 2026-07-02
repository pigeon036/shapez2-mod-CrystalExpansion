using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Localization;
using CrystalExpansion.Common;
using Game.Core.Content.Buildings;
using MonoMod.RuntimeDetour;
using ShapezShifter.Hijack;
using UnityEngine;

namespace CrystalExpansion.Toolbar;

// 工具栏编排器——将原版晶体生成器替换为自定义分组，并安装优先级钩子。
//
// 问题：Shifter 的 ToolbarRewirer 通过图标匹配 PlacementToolbarElementData（而非组 ID），
// 且 IToolbarDataRewirer 执行顺序取决于 GameRewirers.RewirersMap 字典的枚举顺序。
// GroupingRewirer 必须在所有其他 rewirer 填充完条目后最后执行。
//
// 解决：InstallPriorityHook 钩住 GameRewirers.Rewirers 属性 getter，
// 按 RewirerHandle 整数 ID 降序重排——高 Handle ID 先执行，分组 rewirer 最后运行。
internal sealed class CrystalToolbarGrouper : IToolbarDataRewirer
{
    private const string CrystalGeneratorGroupId = "CrystalGeneratorDefaultVariant";

    private readonly Sprite _crystalGeneratorOverrideIcon;
    private readonly ToolbarGroupBuilder _builder;
    private readonly HookCollection _hooks = new();

    public CrystalToolbarGrouper(Sprite crystalGenOverrideIcon, ToolbarGroupBuilder builder)
    {
        _crystalGeneratorOverrideIcon = crystalGenOverrideIcon;
        _builder = builder;
        _hooks.Add(InstallPriorityHook());
    }

    public void Dispose()
    {
        _hooks.Dispose();
    }

    // ModifyToolbarData：定位原版晶体生成器工具栏条目 → 替换为自定义图标
    // 的 PlacementToolbarElementData → 注册到分组构建器 → 在树中插入分组节点。
    //
    // 使用 PlacementToolbarElementData（而非 BuildingBasedPlacementToolbarElementData）
    // 因为 Shifter 的 ToolbarRewirer 通过图标 GetInstanceID 匹配，而非组 ID。
    ToolbarData IToolbarDataRewirer.ModifyToolbarData(ToolbarData toolbarData)
    {
        (IParentToolbarElementData parent, int index) = ToolbarGroupHelper.FindInTree(
            toolbarData.RootToolbarElement, IsCrystalGenerator);
        if (parent == null) return toolbarData;

        BuildingBasedPlacementToolbarElementData original =
            (BuildingBasedPlacementToolbarElementData)parent.Children.ToList()[index];

        // 用更简单的 PlacementToolbarElementData 替换，以便 Shifter 通过图标匹配
        PlacementToolbarElementData replacement = new(
            (LazyLocalizedText)original.BuildingDefinition.Title,
            (LazyLocalizedText)original.BuildingDefinition.Description,
            original.PlacementInitiatorId,
            _crystalGeneratorOverrideIcon)
        { SectionIndex = 0, EntityType = EntityType.Building };
        _builder.Add(replacement, 0);

        // 在晶体生成器条目的位置插入分组节点（默认 Replace 模式）
        _builder.InsertAt(toolbarData.RootToolbarElement, IsCrystalGenerator);
        return toolbarData;
    }

    private static bool IsCrystalGenerator(IToolbarElementData child) =>
        child is BuildingBasedPlacementToolbarElementData bpt
        && bpt.BuildingDefinition.Id.Id == CrystalGeneratorGroupId;

    // 钩住 GameRewirers.Rewirers 的内部属性 getter，按 RewirerHandle 优先级重排枚举。
    // 内部 RewirersMap 字段以 RewirerHandle 为键（按插入顺序枚举），
    // 通过 OrderByDescending 按 Handle ID 整数排序，确保高 ID（分组 rewirer）最后执行。
    private static Hook InstallPriorityHook()
    {
        PropertyInfo property = typeof(GameRewirers).GetProperty(
            "Rewirers", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("GameRewirers.Rewirers property not found");

        MethodInfo method = typeof(CrystalToolbarGrouper).GetMethod(
            nameof(PatchRewirersOrder), BindingFlags.Static | BindingFlags.NonPublic);

        return new Hook((MethodBase)property.GetMethod, method);
    }

    private static IEnumerable<IRewirer> PatchRewirersOrder(
        Func<IEnumerable<IRewirer>> original)
    {
        FieldInfo mapField = typeof(GameRewirers).GetField(
            "RewirersMap", BindingFlags.Static | BindingFlags.NonPublic);
        // 若 RewirersMap 不可用或不是 IEnumerable，回退到原始枚举
        if (mapField?.GetValue(null) is not IEnumerable source)
            return original();

        // 按 RewirerHandle 整数 ID 降序排序：高 ID → 先执行，低 ID → 后执行
        // 使 CrystalToolbarGrouper（Handle 3）在 ToolbarRewirer（Handle 16+）之后运行
        return source
            .Cast<KeyValuePair<RewirerHandle, IRewirer>>()
            .OrderByDescending(kv => int.TryParse(kv.Key.ToString(), out var p) ? p : int.MaxValue)
            .Select(kv => kv.Value)
            .ToArray();
    }
}
