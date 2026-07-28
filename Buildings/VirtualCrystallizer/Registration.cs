using Core.Localization;
using CrystalExpansion.Common;
using Game.Core.Content.Buildings;
using Game.Core.Coordinates;
using Game.Core.Research;
using JetBrains.Annotations;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Flow.Toolbar;
using ShapezShifter.Kit;
using ShapezShifter.Textures;
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Buildings.VirtualCrystallizer;

using DrawData = VirtualCrystallizerDrawData;
using Renderer = VirtualCrystallizerRenderer;
using Simulation = VirtualCrystallizerSimulation;

[UsedImplicitly]
internal static class VirtualCrystallizerRegistration
{
    public static void Register(ILogger logger, ModFolderLocator locator)
    {
        BuildingDefinitionGroupId groupId = new("VirtualCrystallizerGroup");
        BuildingDefinitionId definitionId = new("VirtualCrystallizer");

        string titleId = "building.virtual-crystallizer.title";
        string descriptionId = "building.virtual-crystallizer.description";

        Sprite icon = FileTextureLoader.LoadTextureAsSprite(
            locator.SubPath("ToolbarIcon/VirtualCrystallizer_Icon.png"), out _);

        IBuildingGroupBuilder group = BuildingGroup
            .Create(groupId)
            .WithTitle(titleId.T())
            .WithDescription(descriptionId.T())
            .WithIcon(icon)
            .AsNonTransportableBuilding()
            .WithPreferredPlacement(DefaultPreferredPlacementMode.Single)
            
;

        IBuildingConnectorData connectorData = BuildingConnectors.SingleTile()
            .AddWireInput(WireConnectorConfig.CustomInput(TileDirection.West, BuildingSignalIOType.Building))
            .AddWireInput(WireConnectorConfig.CustomInput(TileDirection.North, BuildingSignalIOType.Building))
            .AddWireOutput(WireConnectorConfig.CustomOutput(TileDirection.East, BuildingSignalIOType.Building))
            .Build();

        IBuildingBuilder buildingBuilder = Building
            .Create(definitionId)
            .WithConnectorData(connectorData)
            .DynamicallyRendering<Renderer, Simulation, DrawData>(new VirtualCrystallizerDrawData())
            .WithStaticDrawData(CreateDrawData(locator))
            .WithoutSound()
            .WithoutSimulationConfiguration()
            .WithEfficiencyData(new BuildingEfficiencyData(2.0f, 1));

        ByIdPerScenarioMilestoneSelector milestoneUnlockedBuilder = new(
            scenarioId => scenarioId.Id.Contains("converter")
            ? new ResearchUpgradeId("ConverterMilestoneTier8")
            : new ResearchUpgradeId("Milestone_Crystals"));

        AtomicBuildings.Extend()
            .AllScenarios()
            .WithBuilding(buildingBuilder, group)
            .UnlockedAtMilestone(milestoneUnlockedBuilder)
            .WithDefaultPlacement()
            .InToolbar(
                ToolbarElementLocator.Root().ChildAt(2).ChildAt(7).ChildAt(6).InsertAfter())
            .WithSimulation(new VirtualCrystallizerFactoryBuilder(), logger)
            .WithCustomModules(new Virtual2In1OutBuildingModuleDataProvider("Shape", "Fluid"))
            .WithoutPrediction()
            .Build();
    }

    private static BuildingDrawData CreateDrawData(ModFolderLocator locator)
    {
        LOD6Mesh mesh = MeshLod.Create()
            .AddLod0Mesh(FbxMeshCombiner.LoadAndCombine(
                locator.SubPath("VirtualCrystallizer/VirtualCrystallizer.fbx")))
            .BuildLod6Mesh();

        return new BuildingDrawData(
            renderVoidBelow: false,
            mainMeshPerLayer: new ILODMesh[] { mesh, mesh, mesh },
            isolatedBlueprintMesh: mesh,
            combinedBlueprintMesh: mesh,
            previewMesh: mesh.LODClose,
            glassMesh: new LODEmptyMesh(),
            colliders: new[]
            {
                new CollisionBox(new SerializedCollisionBox
                {
                    Center_L = new LocalVector(0f, 0f, 0.5f),
                    Dimensions_L = new LocalDimension(1f, 1f, 1f)
                })
            },
            customDrawData: null,
            hasCustomOverviewMesh: false,
            customOverviewMesh: null,
            simulationRendererDrawsMainMesh: false);
    }
}
