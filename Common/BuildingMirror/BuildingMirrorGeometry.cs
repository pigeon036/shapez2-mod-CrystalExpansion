using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core.Coordinates;
using Game.Core.Rendering.MeshGeneration;

namespace CrystalExpansion.Common.BuildingMirror;

// 从一个已构建的 BuildingDefinition 克隆出其 Y 轴镜像孪生定义。
//
// 输入不是 meta 定义（Shifter Atomic API 已绕过 meta），而是运行时的 BuildingDefinition，
// 因此直接对已构建的连接器数据 / 绘制数据做镜像，其余 CustomData 组件原样复制。
// 前三项（连接器 / BuildingDrawData / 自定义绘制数据）需镜像替换，其余原样搬运。
internal static class BuildingDefinitionMirror
{
    public static BuildingDefinition Create(
        BuildingDefinition original,
        IMeshCache meshCache,
        BuildingDefinitionId mirrorId)
    {
        IBuildingConnectorData mirroredConnector = ConnectorDataMirror.Mirror(original.ConnectorData);
        var mirrored = new BuildingDefinition(mirrorId, mirroredConnector);

        foreach (object item in original.CustomData.All)
        {
            switch (item)
            {
                case IBuildingConnectorData:
                    mirrored.CustomData.Attach(mirroredConnector);
                    break;
                case BuildingDrawData drawData:
                    mirrored.CustomData.Attach(DrawDataMirror.MirrorDrawData(drawData, meshCache));
                    break;
                case IBuildingCustomDrawData customDrawData:
                    mirrored.CustomData.Attach(DrawDataMirror.MirrorCustom(customDrawData, meshCache));
                    break;
                case IBuildingMirroringDefinition:
                    // 跳过原有镜像标记，下面统一重建。
                    break;
                default:
                    mirrored.CustomData.Attach(item);
                    break;
            }
        }

        mirrored.CustomData.AttachOrReplace<IBuildingMirroringDefinition>(new MirroringDefinition(original, isMirrored: true));
        original.CustomData.AttachOrReplace<IBuildingMirroringDefinition>(new MirroringDefinition(mirrored, isMirrored: false));

        return mirrored;
    }
}

// 镜像一整个 IBuildingConnectorData（Y 轴，与游戏原生建筑镜像一致）。
//
// 两组并列数据都要翻转：
//   1. Tiles                 —— 建筑占用格子，每格 Y 取反
//   2. AllBuildingConnectors —— 各 IO，位置 Y 取反 + 方向 North↔South
//
// IO 采用 MemberwiseClone 逐个浅拷贝（IO 全部字段皆值类型 → 安全且与具体子类型无关，可兼容
// 现有/未来 IO 类型），只覆写需要镜像的 Position_L 与 TileDirection。
internal static class ConnectorDataMirror
{
    private static readonly TileAxis Axis = TileAxis.YAxis;

    private static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

    public static IBuildingConnectorData Mirror(IBuildingConnectorData source)
    {
        IBuildingIO[] sourceIOs = source.AllBuildingConnectors;
        var mirroredIOs = new IBuildingIO[sourceIOs.Length];
        for (int i = 0; i < sourceIOs.Length; i++)
            mirroredIOs[i] = MirrorIO(sourceIOs[i]);

        TileVector[] sourceTiles = source.Tiles;
        var mirroredTiles = new TileVector[sourceTiles.Length];
        for (int i = 0; i < sourceTiles.Length; i++)
            mirroredTiles[i] = sourceTiles[i].MirrorOver(Axis);

        LocalTileBounds bounds = ComputeBounds(mirroredTiles);
        TileDimensions dimensions = bounds.Dimensions;
        LocalVector center = LocalVector.Lerp((LocalVector)bounds.Min, (LocalVector)bounds.Max, 0.5f);

        return new BuildingConnectorData(mirroredIOs, mirroredTiles, bounds, center, dimensions);
    }

    private static IBuildingIO MirrorIO(IBuildingIO io)
    {
        var clone = (IBuildingIO)MemberwiseCloneMethod.Invoke(io, null);
        var baseClone = (BuildingBaseIO)clone;
        var baseSource = (BuildingBaseIO)io;

        baseClone.Position_L = baseSource.Position_L.MirrorOver(Axis);
        // TileDirection.Mirror(YAxis) 对 North/South 返回 Opposite，对 East/West/Up/Down 原样返回。
        baseClone.TileDirection = baseSource.TileDirection.Mirror(Axis);
        return clone;
    }

    private static LocalTileBounds ComputeBounds(IReadOnlyList<TileVector> tiles)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        short minZ = short.MaxValue, maxZ = short.MinValue;

        foreach (TileVector t in tiles)
        {
            if (t.x < minX) minX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.z < minZ) minZ = t.z;
            if (t.x > maxX) maxX = t.x;
            if (t.y > maxY) maxY = t.y;
            if (t.z > maxZ) maxZ = t.z;
        }

        return new LocalTileBounds(new TileVector(minX, minY, minZ), new TileVector(maxX, maxY, maxZ));
    }
}

// 镜像建筑绘制数据（Y 轴）。
//
// - 网格（各层主网格 / 玻璃 / 蓝图 / 预览 / 概览）：走 IMeshCache.GenerateMirroredMesh
// - 碰撞盒：CollisionBox(center, dim, mirrored:true)（内部对 Center_L 做 MirrorY）
// - 自定义绘制数据：实现 IBuildingMirrorableCustomDrawData 则调用 Mirror(meshCache)，否则原样。
//   完整动态镜像要求使用方 DrawData 实现该接口，否则动态渲染（动画/液面等）不会翻转。
internal static class DrawDataMirror
{
    public static IBuildingCustomDrawData MirrorCustom(IBuildingCustomDrawData custom, IMeshCache meshCache)
    {
        if (custom == null)
            return null;
        if (custom is IBuildingMirrorableCustomDrawData mirrorable)
            return mirrorable.Mirror(meshCache);
        return custom;
    }

    public static BuildingDrawData MirrorDrawData(BuildingDrawData source, IMeshCache meshCache)
    {
        ILODMesh[] mainMeshPerLayer = source.MainMeshPerLayer
            .Select(mesh => MirrorMesh(mesh, meshCache))
            .ToArray();

        CollisionBox[] colliders = source.Colliders
            .Select(box => new CollisionBox(box.Center_L, box.Dimensions_L, mirrored: true))
            .ToArray();

        IBuildingCustomDrawData mirroredCustom = MirrorCustom(source.CustomDrawData, meshCache);

        return new BuildingDrawData(
            renderVoidBelow: source.RenderVoidBelow,
            mainMeshPerLayer: mainMeshPerLayer,
            isolatedBlueprintMesh: MirrorMesh(source.IsolatedBlueprintMesh, meshCache),
            combinedBlueprintMesh: MirrorMesh(source.CombinedBlueprintMesh, meshCache),
            previewMesh: MirrorMesh(source.PreviewMesh, meshCache),
            glassMesh: MirrorMesh(source.GlassMesh, meshCache),
            colliders: colliders,
            customDrawData: mirroredCustom,
            hasCustomOverviewMesh: source.HasCustomOverviewMesh,
            customOverviewMesh: source.HasCustomOverviewMesh ? MirrorMesh(source.CustomOverviewMesh, meshCache) : source.CustomOverviewMesh,
            simulationRendererDrawsMainMesh: source.SimulationRendererDrawsMainMesh);
    }

    private static ILODMesh MirrorMesh(ILODMesh mesh, IMeshCache meshCache)
    {
        return mesh == null ? null : meshCache.GenerateMirroredMesh(mesh);
    }

    private static IMeshReference MirrorMesh(IMeshReference mesh, IMeshCache meshCache)
    {
        return mesh == null ? null : meshCache.GenerateMirroredMesh(mesh);
    }
}
