using System;
using System.Collections.Generic;
using System.Linq;

namespace CrystalExpansion.Common;

// 插入位置
public enum InsertMode { Replace, After, Before }

// 工具栏树原语：定位、插入、写回 Children。不承载任何分组/业务策略
internal static class ToolbarTree
{
    // 写回子节点数组。RootToolbarElementData 与 ParentToolbarElementData 都有 Children 字段，
    // 但类层次上没有共享的可写基类/接口，只能按具体类型模式匹配
    public static void SetChildren(IParentToolbarElementData parent, IToolbarElementData[] children)
    {
        switch (parent)
        {
            case RootToolbarElementData root: root.Children = children; break;
            case ParentToolbarElementData pt: pt.Children = children; break;
        }
    }

    // 深度优先查找第一个满足 matcher 的条目，返回 (父节点, 下标)；找不到返回 (null, -1)。
    // 下标按原始 Children 计数（含 separator），与 SetChildren 的口径一致
    public static (IParentToolbarElementData parent, int index) Find(
        IParentToolbarElementData parent, Func<IToolbarElementData, bool> matcher)
    {
        int i = 0;
        // Children 可能为 null（别的 mod 手建的 GroupToolbarElementData 尚未填子项），不能因此炸整条链
        foreach (IToolbarElementData child in parent.Children ?? Array.Empty<IToolbarElementData>())
        {
            if (matcher(child))
                return (parent, i);

            if (child is IParentToolbarElementData sub)
            {
                (IParentToolbarElementData p, int idx) = Find(sub, matcher);
                if (p != null) return (p, idx);
            }
            i++;
        }
        return (null, -1);
    }

    // 按「建筑定义组 id」（IBuildingDefinitionGroup.Id）匹配建筑摆放条目。
    // 注意匹配的是 BuildingBasedPlacementToolbarElementData（原版建筑条目），
    // 不是 GroupToolbarElementData（工具栏里的折叠分组节点）——两种 "group" 不要混
    public static Func<IToolbarElementData, bool> ByBuildingDefinitionGroupId(string definitionGroupId) =>
        child => child is BuildingBasedPlacementToolbarElementData building
                 && building.BuildingDefinition.Id.Id == definitionGroupId;

    // 找到锚点后按 mode 插入；锚点不存在（被别的 mod 移除/改名）时返回 false，不抛异常，
    // 由调用方决定兜底
    public static bool TryInsert(
        IParentToolbarElementData root,
        Func<IToolbarElementData, bool> anchorMatcher,
        IToolbarElementData element,
        InsertMode mode)
    {
        (IParentToolbarElementData parent, int index) = Find(root, anchorMatcher);
        if (parent == null) return false;

        List<IToolbarElementData> siblings = parent.Children.ToList();
        switch (mode)
        {
            case InsertMode.Replace: siblings[index] = element; break;
            case InsertMode.After: siblings.Insert(index + 1, element); break;
            case InsertMode.Before: siblings.Insert(index, element); break;
        }
        SetChildren(parent, siblings.ToArray());
        return true;
    }

    // 追加到根节点末尾，等于新增一个顶层槽位（锚点缺失时的兜底）
    // 只接受 RootToolbarElementData：根节点由工具栏配置构造，Children 必为数组，这里不判空
    public static void Append(RootToolbarElementData root, IToolbarElementData element)
    {
        List<IToolbarElementData> siblings = root.Children.ToList();
        siblings.Add(element);
        SetChildren(root, siblings.ToArray());
    }
}
