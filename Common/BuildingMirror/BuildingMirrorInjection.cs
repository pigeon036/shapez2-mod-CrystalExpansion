using System;
using Game.Core.Rendering.MeshGeneration;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Common.BuildingMirror;

// 把镜像定义注入到目标建筑所在的组里。
//
// 时机：在 GameModeBuildingsFactory.FromMetadata 的后置钩里执行——此时所有建筑（含 Shifter
// Atomic 建筑）都已建好、组已填充。注入后组内定义数变为 2，游戏后续创建放置器时会自动选择
// FlippableSinglePlacer（含镜像键 / pipette），无需我们自建放置器。
//
// 幂等：FromMetadata 可能多次调用（每次生成全新的 gameBuildings/group）。仅对 count==1 的
// 目标组注入；注入后 count==2，重跑同一组时被守卫跳过。
internal static class MirrorInjector
{
    public static void TryInject(
        GameBuildings gameBuildings,
        IMeshCache meshCache,
        BuildingDefinitionId originalId,
        ILogger logger)
    {
        try
        {
            if (!gameBuildings.TryGetDefinition(originalId, out IBuildingDefinition originalDef))
                return;

            var original = (BuildingDefinition)originalDef;
            var group = (BuildingDefinitionGroup)original.CustomData.Get<IBuildingDefinitionGroup>();

            // 仅在单定义组上注入；已注入（count==2）或本身多定义则跳过（幂等）。
            if (group.Definitions.Count != 1)
                return;

            BuildingDefinitionId mirrorId = MirrorNaming.MirrorId(originalId);
            BuildingDefinition mirrored = BuildingDefinitionMirror.Create(original, meshCache, mirrorId);

            group.AddInternalVariant(mirrored);
            gameBuildings._DefinitionsById[mirrorId] = mirrored;

            logger.Info?.Log($"BuildingMirror: injected '{mirrorId.Name}' into group '{group.Id}' (definitions now {group.Definitions.Count})");
        }
        catch (Exception ex)
        {
            logger.Error?.Log($"BuildingMirror: injection failed for '{originalId.Name}': {ex}");
        }
    }
}

// IBuildingMirroringDefinition 的自建实现。
// 游戏内实现类（BuildingDefinitionFactory.BuildingMirroringDefinition）是 private，无法直接
// 实例化，故提供等价实现挂到定义的 CustomData 上——游戏只按接口读取。
// 原定义挂 (mirror, IsMirrored=false)，镜像定义挂 (original, IsMirrored=true)。
internal sealed class MirroringDefinition : IBuildingMirroringDefinition
{
    public IBuildingDefinition MirroredDefinition { get; }
    public bool IsMirrored { get; }

    public MirroringDefinition(IBuildingDefinition mirroredDefinition, bool isMirrored)
    {
        MirroredDefinition = mirroredDefinition;
        IsMirrored = isMirrored;
    }
}
