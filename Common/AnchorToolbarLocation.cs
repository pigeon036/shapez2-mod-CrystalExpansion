using System;
using Core.Localization;
using ShapezShifter.Flow.Toolbar;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Common;

// 按建筑组 id 锚定的工具栏插入位置
//
// Shifter 自带的 ToolbarElementLocator 只有绝对索引（Root().ChildAt(a).ChildAt(b)…），
// 一旦别的 mod 在同一分类里增删条目，索引就会指到别的分组上：
//   - 轻则把建筑插进错误的组；
//   - 重则 List.Insert 越界，主菜单建工具栏时抛 ArgumentOutOfRangeException，
//     整个游戏无法启动
//
// 这里改为按 BuildingDefinition.Id 查找锚点条目，其他 mod 的增删不再影响定位
// 树的查找/插入走 ToolbarTree 原语
internal sealed class AnchorToolbarLocation : IToolbarEntryInsertLocation
{
    private readonly string _anchorDefinitionGroupId;
    private readonly Func<IToolbarElementData, bool> _isAnchor;
    private readonly bool _insertAfter;
    private readonly ILogger _logger;

    private AnchorToolbarLocation(string anchorDefinitionGroupId, bool insertAfter, ILogger logger)
    {
        _anchorDefinitionGroupId = anchorDefinitionGroupId;
        _isAnchor = ToolbarTree.ByBuildingDefinitionGroupId(anchorDefinitionGroupId);
        _insertAfter = insertAfter;
        _logger = logger;
    }

    public static AnchorToolbarLocation After(string anchorDefinitionGroupId, ILogger logger) =>
        new(anchorDefinitionGroupId, insertAfter: true, logger);

    public static AnchorToolbarLocation Before(string anchorDefinitionGroupId, ILogger logger) =>
        new(anchorDefinitionGroupId, insertAfter: false, logger);

    // 锚点存在则插到它前/后；缺失（被别的 mod 移除/改名）时追加到工具栏根节点并打 warning，不抛异常
    public void AddEntry(ToolbarData toolbarData, IToolbarElementData elementData)
    {
        InsertMode mode = _insertAfter ? InsertMode.After : InsertMode.Before;
        if (ToolbarTree.TryInsert(toolbarData.RootToolbarElement, _isAnchor, elementData, mode))
        {
            return;
        }

        // 兜底：锚点被别的 mod 移除/改名时，把条目追加到根节点末尾（顶层槽位），保证建筑仍可放置
        ToolbarTree.Append(toolbarData.RootToolbarElement, elementData);

        _logger.Warning?.Log(
            $"Toolbar: anchor '{_anchorDefinitionGroupId}' not found while placing '{Describe(elementData)}'; " +
            "the entry was appended to the toolbar root (top level) instead");
    }

    // 日志用的条目标识：优先取标题的翻译键，没有则退回类型名
    private static string Describe(IToolbarElementData element) =>
        element is IPresentableToolbarElementData { Title: LazyLocalizedText title }
            ? title.Id.Id
            : element.GetType().Name;
}
