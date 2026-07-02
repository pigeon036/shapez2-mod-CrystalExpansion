using System;
using System.Collections.Generic;

namespace CrystalExpansion.Common;

// 工具栏树操作工具。
//
// SetChildren 中的 switch：RootToolbarElementData 和 ParentToolbarElementData
// 都拥有 Children 属性，但在 C# 类层次中无共享可写基类/接口，
// 因此必须通过具体类型模式匹配来设置。
//
// FindInTree：深度优先递归遍历工具栏树，返回 (parent, childIndex) 元组。
// null 返回值表示未找到匹配的子树。
internal static class ToolbarGroupHelper
{
    public static void SetChildren(IParentToolbarElementData parent, IToolbarElementData[] children)
    {
        switch (parent)
        {
            case RootToolbarElementData root: root.Children = children; break;
            case ParentToolbarElementData pt:  pt.Children = children;  break;
        }
    }

    public static (IParentToolbarElementData parent, int index) FindInTree(
        IParentToolbarElementData parent, Func<IToolbarElementData, bool> matcher)
    {
        int i = 0;
        foreach (IToolbarElementData child in parent.Children)
        {
            if (matcher(child))
                return (parent, i);

            if (child is IParentToolbarElementData sub)
            {
                (IParentToolbarElementData p, int idx) = FindInTree(sub, matcher);
                if (p != null) return (p, idx);
            }
            i++;
        }
        return (null, -1);
    }
}
