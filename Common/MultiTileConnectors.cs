using System;
using System.Collections.Generic;
using Game.Core.Coordinates;
using ShapezShifter.Flow.Atomic;

namespace CrystalExpansion.Common;

public static class MultiTileConnectors
{
    public static IMultiTileBuilder Start() => new MultiTileBuilder();
}

public interface IMultiTileBuilder
{
    IMultiTileBuilder AddTile(TileVector tile);
    IMultiTileBuilder AddShapeInput(TileVector tile, ShapeConnectorConfig shapeConnectorConfig);
    IMultiTileBuilder AddShapeOutput(TileVector tile, ShapeConnectorConfig shapeConnectorConfig);
    IMultiTileBuilder AddWireInput(TileVector tile, WireConnectorConfig wireConnectorConfig);
    IMultiTileBuilder AddWireOutput(TileVector tile, WireConnectorConfig wireConnectorConfig);
    IMultiTileBuilder AddFluidInput(TileVector tile, FluidConnectorConfig fluidConnectorConfig);
    IMultiTileBuilder AddFluidOutput(TileVector tile, FluidConnectorConfig fluidConnectorConfig);
    IBuildingConnectorData Build();
}

public class MultiTileBuilder : IMultiTileBuilder
{
    private readonly List<BuildingBaseIO> BuildingConnectors = new();
    private readonly HashSet<TileVector> BuildingTiles = new();

    public IMultiTileBuilder AddTile(TileVector tile)
    {
        BuildingTiles.Add(tile);
        return this;
    }

    public IMultiTileBuilder AddShapeInput(TileVector tile, ShapeConnectorConfig shapeConnectorConfig)
    {
        BuildingTiles.Add(tile);
        BuildingConnectors.Add(new BuildingItemInput
        {
            Position_L = tile,
            Direction_L = shapeConnectorConfig.Direction.Value,
            StandType = shapeConnectorConfig.StandType,
            IOType = shapeConnectorConfig.CapsType,
            Seperators = shapeConnectorConfig.Separators
        });
        return this;
    }

    public IMultiTileBuilder AddShapeOutput(TileVector tile, ShapeConnectorConfig shapeConnectorConfig)
    {
        BuildingTiles.Add(tile);
        BuildingConnectors.Add(new BuildingItemOutput
        {
            Position_L = tile,
            Direction_L = shapeConnectorConfig.Direction.Value,
            StandType = shapeConnectorConfig.StandType,
            IOType = shapeConnectorConfig.CapsType,
            Seperators = shapeConnectorConfig.Separators
        });
        return this;
    }

    public IMultiTileBuilder AddWireInput(TileVector tile, WireConnectorConfig wireConnectorConfig)
    {
        BuildingTiles.Add(tile);
        BuildingConnectors.Add(new BuildingSignalInput
        {
            Position_L = tile,
            Direction_L = wireConnectorConfig.Direction.Value,
            _IOType = wireConnectorConfig.IoType,
            TileDirection = wireConnectorConfig.Direction
        });
        return this;
    }

    public IMultiTileBuilder AddWireOutput(TileVector tile, WireConnectorConfig wireConnectorConfig)
    {
        BuildingTiles.Add(tile);
        BuildingConnectors.Add(new BuildingSignalOutput
        {
            Position_L = tile,
            Direction_L = wireConnectorConfig.Direction.Value,
            _IOType = wireConnectorConfig.IoType,
            TileDirection = wireConnectorConfig.Direction
        });
        return this;
    }

    public IMultiTileBuilder AddFluidInput(TileVector tile, FluidConnectorConfig fluidConnectorConfig)
    {
        BuildingTiles.Add(tile);
        BuildingConnectors.Add(new BuildingFluidInput
        {
            Position_L = tile,
            Direction_L = fluidConnectorConfig.Direction.Value,
            _IOType = fluidConnectorConfig.IoType,
            TileDirection = fluidConnectorConfig.Direction
        });
        return this;
    }

    public IMultiTileBuilder AddFluidOutput(TileVector tile, FluidConnectorConfig fluidConnectorConfig)
    {
        BuildingTiles.Add(tile);
        BuildingConnectors.Add(new BuildingFluidOutput
        {
            Position_L = tile,
            Direction_L = fluidConnectorConfig.Direction.Value,
            _IOType = fluidConnectorConfig.IoType,
            TileDirection = fluidConnectorConfig.Direction
        });
        return this;
    }

    public IBuildingConnectorData Build()
    {
        TileVector[] tilesArray = new TileVector[BuildingTiles.Count];
        BuildingTiles.CopyTo(tilesArray);

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        short minZ = short.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        short maxZ = short.MinValue;
        foreach (TileVector tile in BuildingTiles)
        {
            if (tile.x < minX) minX = tile.x;
            if (tile.y < minY) minY = tile.y;
            if (tile.z < minZ) minZ = tile.z;
            if (tile.x > maxX) maxX = tile.x;
            if (tile.y > maxY) maxY = tile.y;
            if (tile.z > maxZ) maxZ = tile.z;
        }

        TileVector min = new(minX, minY, minZ);
        TileVector max = new(maxX, maxY, maxZ);
        LocalTileBounds tileBounds = new(min, max);
        TileDimensions tileDimensions = tileBounds.Dimensions;
        LocalVector center = LocalVector.Lerp(
            a: (LocalVector)tileBounds.Min,
            b: (LocalVector)tileBounds.Max,
            t: 0.5f);

        return new BuildingConnectorData(
            allInputs: BuildingConnectors,
            tiles: tilesArray,
            tileBounds: tileBounds,
            tileBoundsCenter: center,
            tileDimensions: tileDimensions);
    }
}
