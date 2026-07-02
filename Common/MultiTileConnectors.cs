using System.Collections.Generic;
using System.Linq;
using Game.Core.Coordinates;
using ShapezShifter.Flow.Atomic;

namespace CrystalExpansion.Common;

// 多格建筑连接器构建器——流畅式 API 用于声明建筑的瓦片布局和 IO 端口。
//
// Build 方法通过计算瓦片集合的 min/max 自动推导 TileBounds、TileDimensions 和中心点，
// 不需要手动指定建筑尺寸。
public static class MultiTileConnectors
{
    public static IMultiTileBuilder Start() => new MultiTileBuilder();
}

public interface IMultiTileBuilder
{
    IMultiTileBuilder AddTile(TileVector tile);
    IMultiTileBuilder AddShapeInput(TileVector tile, ShapeConnectorConfig config);
    IMultiTileBuilder AddShapeOutput(TileVector tile, ShapeConnectorConfig config);
    IMultiTileBuilder AddWireInput(TileVector tile, WireConnectorConfig config);
    IMultiTileBuilder AddWireOutput(TileVector tile, WireConnectorConfig config);
    IMultiTileBuilder AddFluidInput(TileVector tile, FluidConnectorConfig config);
    IMultiTileBuilder AddFluidOutput(TileVector tile, FluidConnectorConfig config);
    IBuildingConnectorData Build();
}

public class MultiTileBuilder : IMultiTileBuilder
{
    private readonly List<BuildingBaseIO> _connectors = new();
    private readonly HashSet<TileVector> _tiles = new();

    public IMultiTileBuilder AddTile(TileVector tile)
    {
        _tiles.Add(tile);
        return this;
    }

    public IMultiTileBuilder AddShapeInput(TileVector tile, ShapeConnectorConfig config)
    {
        _tiles.Add(tile);
        _connectors.Add(new BuildingItemInput
        {
            Position_L = tile,
            Direction_L = config.Direction.Value,
            StandType = config.StandType,
            IOType = config.CapsType,
            Seperators = config.Separators
        });
        return this;
    }

    public IMultiTileBuilder AddShapeOutput(TileVector tile, ShapeConnectorConfig config)
    {
        _tiles.Add(tile);
        _connectors.Add(new BuildingItemOutput
        {
            Position_L = tile,
            Direction_L = config.Direction.Value,
            StandType = config.StandType,
            IOType = config.CapsType,
            Seperators = config.Separators
        });
        return this;
    }

    public IMultiTileBuilder AddWireInput(TileVector tile, WireConnectorConfig config)
    {
        _tiles.Add(tile);
        _connectors.Add(new BuildingSignalInput
        {
            Position_L = tile,
            Direction_L = config.Direction.Value,
            _IOType = config.IoType,
            TileDirection = config.Direction
        });
        return this;
    }

    public IMultiTileBuilder AddWireOutput(TileVector tile, WireConnectorConfig config)
    {
        _tiles.Add(tile);
        _connectors.Add(new BuildingSignalOutput
        {
            Position_L = tile,
            Direction_L = config.Direction.Value,
            _IOType = config.IoType,
            TileDirection = config.Direction
        });
        return this;
    }

    public IMultiTileBuilder AddFluidInput(TileVector tile, FluidConnectorConfig config)
    {
        _tiles.Add(tile);
        _connectors.Add(new BuildingFluidInput
        {
            Position_L = tile,
            Direction_L = config.Direction.Value,
            _IOType = config.IoType,
            TileDirection = config.Direction
        });
        return this;
    }

    public IMultiTileBuilder AddFluidOutput(TileVector tile, FluidConnectorConfig config)
    {
        _tiles.Add(tile);
        _connectors.Add(new BuildingFluidOutput
        {
            Position_L = tile,
            Direction_L = config.Direction.Value,
            _IOType = config.IoType,
            TileDirection = config.Direction
        });
        return this;
    }

    // 通过收集到的瓦片集合自动推导建筑边界和中心点。
    // center = Lerp(min, max, 0.5) — 各维度的中点。
    public IBuildingConnectorData Build()
    {
        TileVector[] tilesArray = _tiles.ToArray();

        int minX = tilesArray.Min(t => t.x);
        int minY = tilesArray.Min(t => t.y);
        int minZ = tilesArray.Min(t => t.z);
        int maxX = tilesArray.Max(t => t.x);
        int maxY = tilesArray.Max(t => t.y);
        int maxZ = tilesArray.Max(t => t.z);

        TileVector min = new(minX, minY, (short)minZ);
        TileVector max = new(maxX, maxY, (short)maxZ);
        LocalTileBounds tileBounds = new(min, max);
        TileDimensions tileDimensions = tileBounds.Dimensions;
        LocalVector center = LocalVector.Lerp(
            a: (LocalVector)tileBounds.Min,
            b: (LocalVector)tileBounds.Max,
            t: 0.5f);

        return new BuildingConnectorData(
            allInputs: _connectors,
            tiles: tilesArray,
            tileBounds: tileBounds,
            tileBoundsCenter: center,
            tileDimensions: tileDimensions);
    }
}
