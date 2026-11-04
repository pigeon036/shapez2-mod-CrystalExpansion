using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using ShapezShifter.Flow.Toolbar;
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Common;

// 工具栏分组构建器——收集 + 排序 + 插入，并支持插入后就地刷新
//
// 流程：
// 1. 声明阶段：Add(Sprite, sortOrder) 注册图标 → 排序映射；Add(subGroup/item, sortOrder) 登记子组与手动条目
// 2. 收集阶段：Shifter 的 AtomicBuildings 流程通过 IToolbarEntryInsertLocation.AddEntry
//    推送 PlacementToolbarElementData；builder 用图标 GetInstanceID() 匹配排序
// 3. 插入阶段：InsertAt 在树中定位地标 → Replace / After / Before 放入组节点
// 4. 刷新阶段：组节点插入后，晚到的条目（AddEntry / Add(manual)）直接改同一个 GroupToolbarElementData
//    对象的 Children，不需要依赖 rewirer 的执行顺序
//
// 跨构建周期清理：工具栏每次重建都会重新反序列化一份新的 ToolbarData（工具条 JSON → ToolbarData），
// 因此用引用比较识别“新一次构建”，在第一次收集/插入时清空上一次的条目，避免重复
public class ToolbarGroupBuilder : IToolbarEntryInsertLocation
{
    private readonly Dictionary<int, int> _sortOrders = new();
    private readonly List<PlacementToolbarElementData> _entries = new();
    private readonly List<(int sortOrder, ToolbarGroupBuilder subGroup)> _subGroups = new();
    private readonly List<(int sortOrder, IToolbarElementData item)> _manualItems = new();
    private readonly GroupToolbarElementData _groupNode;
    private readonly string _titleId;
    private readonly ILogger _logger;

    private ToolbarData _buildData;
    private bool _inserted;

    public ToolbarGroupBuilder(string groupTitleKey, Sprite groupIcon, ILogger logger)
    {
        _groupNode = new GroupToolbarElementData
        {
            Title = (LazyLocalizedText)groupTitleKey.T(),
            Icon = groupIcon,
            RememberPreferredChild = true,
        };
        _titleId = _groupNode.Title.Id.Id;
        _logger = logger;
    }

    public IToolbarEntryInsertLocation ToolbarLocation => this;

    // 注册图标 → 排序映射。sortOrder 决定子条目在组内的排列顺序
    public void Add(Sprite icon, int sortOrder)
    {
        _sortOrders[icon.GetInstanceID()] = sortOrder;
    }

    // 注册子分组（嵌套组），sortOrder 控制子组在父组内的位置
    public void Add(ToolbarGroupBuilder subGroup, int sortOrder)
    {
        _subGroups.Add((sortOrder, subGroup));
    }

    // 注册手动条目（非 Shifter 自动生成的条目），如替换后的晶体生成器
    public void Add(IToolbarElementData item, int sortOrder)
    {
        _manualItems.Add((sortOrder, item));
        RefreshIfInserted();
    }

    // Shifter 的 AtomicBuildings 回调——收集 PlacementToolbarElementData；组已插入时就地刷新
    void IToolbarEntryInsertLocation.AddEntry(ToolbarData toolbarData, IToolbarElementData element)
    {
        BeginBuild(toolbarData);

        if (element is not PlacementToolbarElementData pt) return;

        _entries.Add(pt);
        RefreshIfInserted();
    }

    // 识别新一次工具栏构建：每次重建都会拿到新的 ToolbarData 实例。
    // 调用方（rewirer 与 AddEntry）谁先跑到就用谁的引用初始化，后续同一实例不会重复清空。
    public void BeginBuild(ToolbarData toolbarData)
    {
        if (ReferenceEquals(_buildData, toolbarData)) return;

        _buildData = toolbarData;
        _entries.Clear();
        _manualItems.Clear();
        _inserted = false;
    }

    // 在工具栏树中定位地标节点，按指定模式插入构建好的分组节点
    public void InsertAt(RootToolbarElementData root,
        Func<IToolbarElementData, bool> landmarkMatcher,
        InsertMode mode = InsertMode.Replace)
    {
        GroupToolbarElementData node = BuildGroupNode();

        if (!ToolbarTree.TryInsert(root, landmarkMatcher, node, mode))
        {
            // 兜底：地标被别的 mod 移除/改名时，把组追加到根节点末尾并打 warning，
            // 至少让组内建筑仍可出现，而不是整组消失
            ToolbarTree.Append(root, node);
            _logger.Warning?.Log(
                $"Toolbar: landmark for group '{_titleId}' not found; the group was appended to the toolbar root (top level) instead");
        }

        // 树里持有的是这个组节点对象，之后新增条目直接改它的 Children 即可
        _inserted = true;
    }

    private void RefreshIfInserted()
    {
        if (_inserted) BuildGroupNode();
    }

    // 把收集到的条目 + 子组 + 手动条目按 sortOrder 合并，写入组节点。
    // 不清空收集列表：插入后晚到的条目需要同一份数据重新排序
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

        foreach ((int order, ToolbarGroupBuilder subGroup) in _subGroups)
            allItems.Add((order, subGroup.BuildGroupNode()));

        foreach ((int order, IToolbarElementData item) in _manualItems)
            allItems.Add((order, item));

        allItems.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        List<IToolbarElementData> items = allItems.Select(x => x.item).ToList();

        // 组节点的 SectionIndex 继承自第一个放置子元素
        _groupNode.SectionIndex = items.OfType<PlacementToolbarElementData>()
            .FirstOrDefault()?.SectionIndex ?? 0;
        _groupNode.Children = items.ToArray();

        return _groupNode;
    }
}
