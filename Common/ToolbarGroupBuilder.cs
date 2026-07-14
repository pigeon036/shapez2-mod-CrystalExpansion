using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using ShapezShifter.Flow.Toolbar;
using UnityEngine;

namespace CrystalExpansion.Common;

// 工具栏分组构建器——三步收集 + 排序 + 插入。
//
// 流程：
// 1. 声明阶段：Add(Sprite, sortOrder) 注册图标到排序映射
// 2. 收集阶段：Shifter 的 AtomicBuildings 流程通过 IToolbarEntryInsertLocation.AddEntry
//    推送实际的 PlacementToolbarElementData 对象；builder 通过图标 GetInstanceID() 匹配
// 3. 构建阶段：BuildGroupNode 合并收集到的条目和声明的排序 → 排序 → 创建 GroupToolbarElementData
// 4. 插入阶段：InsertAt 在工具栏树中定位地标节点 → 按模式替换/前插/后插
//
// _entries.Clear() 和 _manualItems.Clear()：ModifyToolbarData 在工具栏重建期间被多次调用，
// 必须每次清空以避免跨重建周期的条目累积。
public enum InsertMode { Replace, After, Before }

public class ToolbarGroupBuilder : IToolbarEntryInsertLocation
{
    private readonly Dictionary<int, int> _sortOrders = new();
    private readonly List<PlacementToolbarElementData> _entries = new();
    private readonly List<(int sortOrder, ToolbarGroupBuilder subGroup)> _subGroups = new();
    private readonly List<(int sortOrder, IToolbarElementData item)> _manualItems = new();
    private readonly GroupToolbarElementData _groupNode;

    public ToolbarGroupBuilder(string groupTitleKey, Sprite groupIcon)
    {
        _groupNode = new GroupToolbarElementData
        {
            Title = (LazyLocalizedText)groupTitleKey.T(),
            Icon = groupIcon,
            RememberPreferredChild = true,
        };
    }

    public IToolbarEntryInsertLocation ToolbarLocation => this;

    // 注册图标 → 排序映射。sortOrder 决定子条目在组内的排列顺序。
    public void Add(Sprite icon, int sortOrder)
    {
        _sortOrders[icon.GetInstanceID()] = sortOrder;
    }

    // 注册子分组（嵌套组），sortOrder 控制子组在父组内的位置。
    public void Add(ToolbarGroupBuilder subGroup, int sortOrder)
    {
        _subGroups.Add((sortOrder, subGroup));
    }

    // 注册手动条目（非 Shifter 自动生成的条目），如替换后的晶体生成器。
    public void Add(IToolbarElementData item, int sortOrder)
    {
        _manualItems.Add((sortOrder, item));
    }

    // Shifter 的 AtomicBuildings 回调——将 PlacementToolbarElementData 收集到内部列表。
    void IToolbarEntryInsertLocation.AddEntry(ToolbarData toolbarData, IToolbarElementData element)
    {
        if (element is PlacementToolbarElementData pt)
            _entries.Add(pt);
    }

    // 在工具栏树中定位地标节点，按指定模式插入构建好的分组节点。
    public void InsertAt(IParentToolbarElementData root,
        Func<IToolbarElementData, bool> landmarkMatcher,
        InsertMode mode = InsertMode.Replace)
    {
        GroupToolbarElementData node = BuildGroupNode();

        (IParentToolbarElementData parent, int index) =
            ToolbarGroupHelper.FindInTree(root, landmarkMatcher);
        if (parent == null) return;

        List<IToolbarElementData> siblings = parent.Children.ToList();
        switch (mode)
        {
            case InsertMode.Replace:
                siblings[index] = node;
                break;
            case InsertMode.After:
                siblings.Insert(index + 1, node);
                break;
            case InsertMode.Before:
                siblings.Insert(index, node);
                break;
        }
        ToolbarGroupHelper.SetChildren(parent, siblings.ToArray());
    }

    // 将收集到的条目与声明的排序合并 → 排序 → 构建 GroupToolbarElementData。
    // SectionIndex 从第一个 PlacementToolbarElementData 继承，用于定位组在工具栏中的位置。
    private GroupToolbarElementData BuildGroupNode()
    {
        List<(int sortOrder, IToolbarElementData item)> allItems = new();

        // 通过图标 GetInstanceID 匹配收集到的条目与声明的 sortOrder
        foreach (PlacementToolbarElementData pt in _entries)
        {
            int key = pt.Icon.GetInstanceID();
            int order = _sortOrders.TryGetValue(key, out int o) ? o : int.MaxValue;
            allItems.Add((order, pt));
        }
        _entries.Clear();

        foreach ((int order, ToolbarGroupBuilder subGroup) in _subGroups)
            allItems.Add((order, subGroup.BuildGroupNode()));

        foreach ((int order, IToolbarElementData item) in _manualItems)
            allItems.Add((order, item));
        _manualItems.Clear();

        allItems.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        List<IToolbarElementData> items = allItems.Select(x => x.item).ToList();

        // 组节点的 SectionIndex 继承自第一个放置子元素
        _groupNode.SectionIndex = items.OfType<PlacementToolbarElementData>()
            .FirstOrDefault()?.SectionIndex ?? 0;
        _groupNode.Children = items.ToArray();

        return _groupNode;
    }
}
