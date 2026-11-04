using System;
using CrystalExpansion.Common;
using ShapezShifter.Hijack;
using ShapezShifter.Kit;
using ShapezShifter.Textures;
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Toolbar;

// 工具栏阶段的装配模块：组构建器 + 原版条目接线器
internal sealed class ToolbarSetup : IDisposable
{
    private readonly CrystalToolbarRewirer _rewirer;
    private readonly RewirerHandle _handle;

    // 物理建筑注册时共享同一个组构建器（作为它们的 IToolbarEntryInsertLocation）
    public ToolbarGroupBuilder GroupBuilder { get; }

    public ToolbarSetup(ILogger logger, ModFolderLocator locator)
    {
        Sprite groupIcon = FileTextureLoader.LoadTextureAsSprite(
            locator.SubPath("ToolbarIcon/CrystalExToolbar_Icon.png"), out _);
        Sprite crystalGeneratorIcon = FileTextureLoader.LoadTextureAsSprite(
            locator.SubPath("ToolbarIcon/NewCrystalGenerator_Icon.png"), out _);

        GroupBuilder = new ToolbarGroupBuilder("building.crystal-group.title", groupIcon, logger);

        _rewirer = new CrystalToolbarRewirer(crystalGeneratorIcon, GroupBuilder);
        _handle = GameRewirers.AddRewirer<IToolbarDataRewirer>(_rewirer);
    }

    public void Dispose()
    {
        GameRewirers.RemoveRewirer(_handle);
    }
}
