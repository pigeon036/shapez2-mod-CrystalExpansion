using Core.Collections.Scoped;
using Game.Core.Coordinates;
using Game.Core.Map.Simulation;
using System.Collections.Generic;

namespace CrystalExpansion.Buildings.Crystallizer;

// 晶化器 HUD 侧边栏模块——显示统计数据（处理时间、流体消耗、满速传送带数）和
// 运行时信息（建筑效率、流体罐内容物）。SpeedId 与 CutterSpeed 研究绑定。
public class CrystallizerBuildingModules : IBuildingModules
{
    private readonly ResearchSpeedId SpeedId;

    public CrystallizerBuildingModules(ResearchSpeedId speedId)
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
        stats.Add(new StructureStatProcessingTime(3f, SpeedId));
        yield return new HUDSidePanelModuleStats.Data(stats);
    }

    public IEnumerable<IHUDSidePanelModuleData> GetInfoModules(IMapModel map, BuildingModel building)
    {
        GlobalTileCoordinate tile = building.Tile_G;
        if (map.Simulator.TryFindTileSimulation(in tile, out ILocalizedTileSimulation localized)
            && localized.Simulation is CrystallizerSimulation simulation)
        {
            yield return new HUDSidePanelModuleBuildingEfficiency.Data(building, localized, simulation.InputLane, SpeedId, 3f);
            yield return new HUDSidePanelModuleFluidContainerContents.Data(simulation.Container);
        }
    }
}
