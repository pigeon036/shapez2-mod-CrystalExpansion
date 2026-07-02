using Core.Collections.Scoped;
using Game.Content.Features;
using Game.Content.Features.Fluids;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation;
using Game.Core.Research;
using System.Collections.Generic;

namespace CrystalExpansion.Buildings.Dissolver;

// 溶解器 HUD 侧边栏模块——显示输入罐和输出罐的流体内容物。
// 与晶化器不同，溶解器无建筑效率模块（其处理通过双罐系统反映）。
public class DissolverBuildingModules : IBuildingModules
{
    private readonly ResearchSpeedId SpeedId;

    public DissolverBuildingModules(ResearchSpeedId speedId)
    {
        SpeedId = speedId;
    }

    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IBuildingDefinition definition)
    {
        using ScopedList<StructureStat> stats = ScopedList.Get<StructureStat>();
        stats.Add(new StructureStatBuildingsPerFullBelt(new ResearchSpeedId("BeltSpeed"), 3f, SpeedId));
        stats.Add(new StructureStatBuildingsPerFluidLauncher(3f));
        stats.Add(new StructureStatLitersPerItem(FluidUnit.FromLiters(20)));
        stats.Add(new StructureStatFluidConsumption(FluidRate.FromLitersPerMinute(400), SpeedId));
        yield return new HUDSidePanelModuleStats.Data(stats);
    }

    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IMapModel map, BuildingModel building)
    {
        GlobalTileCoordinate tile = building.Tile_G;
        if (map.Simulator.TryFindTileSimulation(in tile, out ILocalizedTileSimulation localized)
            && localized.Simulation is DissolverSimulation simulation)
        {
            // 显示两个罐的流体内容物——输入罐（消耗 ColorFluid）和输出罐（产出 ShapeTypedFluid）
            yield return new HUDSidePanelModuleFluidContainerContents.Data(simulation.InputTank);
            yield return new HUDSidePanelModuleFluidContainerContents.Data(simulation.OutputTank);
        }
    }
}
