using Core.Localization;
using Game.Core.Content.Buildings;
using Game.Core.Coordinates;
using Game.Core.Research;
using JetBrains.Annotations;
using ShapezShifter.Flow;
using ShapezShifter.Flow.Atomic;
using ShapezShifter.Flow.Research;
using ShapezShifter.Kit;
using CrystalExpansion.Common;
using ShapezShifter.Textures;
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Buildings.Dissolver;

using Renderer = DissolverRenderer;
using Simulation = DissolverSimulation;
using DrawData = IDissolverDrawData;

// 溶解器建筑注册——通过 Shifter Flow/Atomic API 声明建筑。
//
// 多格布局（2 格高度，z 轴垂直堆叠）：
//   Tile (0,0,0) — 下瓦片：流体输入（West）+ 流体输出（East）
//   Tile (0,0,1) — 上瓦片：形状输入（传送带）
//
// 里程碑选择器与晶化器相同：产业模式（场景 ID 含 "converter"）→ ConverterMilestoneTier8，
// 标准模式 → Milestone_Crystals。
//
// 碰撞盒手动计算：Center_L = (0, 0, 0.75) 覆盖 2 格高度（z=0 到 z=1.5，含管道间隙），
// Dimensions_L = (1, 1, 1.5) 覆盖东西 1 格 + 南北 1 格 + 高度 1.5 格。
[UsedImplicitly]
internal static class DissolverRegistration
{
    public static void Register(ILogger logger, ModFolderLocator locator, ToolbarGroupBuilder toolbarGroupBuilder)
    {
        BuildingDefinitionGroupId groupId = new("DissolverGroup");
        BuildingDefinitionId definitionId = new("Dissolver");

        string titleId = "building.dissolver.title";
        string descriptionId = "building.dissolver.description";

        Sprite icon = FileTextureLoader.LoadTextureAsSprite(locator.SubPath("ToolbarIcon/Dissolver_Icon.png"), out _);

        // sortOrder=2：在工具栏分组中，溶解器排在晶化器 (1) 后面
        toolbarGroupBuilder.Add(icon, 2);

        LOD6Mesh fluidMeshLod = MeshLod.Create()
            .AddLod0Mesh(FbxMeshCombiner.LoadAndCombine(locator.SubPath("Dissolver/Dissolver_Fluid.fbx")))
            .BuildLod6Mesh();

        IBuildingGroupBuilder group = BuildingGroup
            .Create(groupId)
            .WithTitle(titleId.T())
            .WithDescription(descriptionId.T())
            .WithIcon(icon)
            .AsNonTransportableBuilding()
            .WithPreferredPlacement(DefaultPreferredPlacementMode.LinePerpendicular)
            .WithDefaultStructureOverview();

        ((BuildingGroupBuilder)(object)group).LinkedEntryId = new WikiEntryId("WKFluids_Dissolver");

        IBuildingConnectorData connectorData = MultiTileConnectors
            .Start()
            .AddTile(new TileVector(0, 0, 0))
            .AddTile(new TileVector(0, 0, 1))
            // 形状输入位于上瓦片（z=1），通过传送带输入
            .AddShapeInput(new TileVector(0, 0, 1), ShapeConnectorConfig.DefaultInput())
            .AddFluidInput(new TileVector(0, 0, 0), FluidConnectorConfig.CustomInput(TileDirection.West))
            .AddFluidOutput(new TileVector(0, 0, 0), FluidConnectorConfig.CustomOutput(TileDirection.East))
            .Build();

        IBuildingBuilder buildingBuilder = Building
            .Create(definitionId)
            .WithConnectorData(connectorData)
            .DynamicallyRendering<Renderer, Simulation, DrawData>(new DissolverDrawData(fluidMeshLod))
            .WithStaticDrawData(CreateDrawData(locator, fluidMeshLod))
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
            .InToolbar(toolbarGroupBuilder.ToolbarLocation)
            .WithSimulation(new DissolverFactoryBuilder(), logger)
            .WithCustomModules(new DissolverBuildingModules(new ResearchSpeedId("CutterSpeed")))
            .WithPrediction(new DissolverPredictionFactoryBuilder(), logger)
            .Build();
    }

    private static BuildingDrawData CreateDrawData(ModFolderLocator locator, LOD6Mesh fluidMeshLod)
    {
        LOD6Mesh mainLod = MeshLod
            .Create()
            .AddLod0Mesh(FbxMeshCombiner.LoadAndCombine(locator.SubPath("Dissolver/Dissolver_Main.fbx")))
            .BuildLod6Mesh();

        LOD6Mesh glassLod = MeshLod
            .Create()
            .AddLod0Mesh(FbxMeshCombiner.LoadAndCombine(locator.SubPath("Dissolver/Dissolver_Glas.fbx")))
            .BuildLod6Mesh();

        // 手动计算碰撞盒：覆盖 2 格高度（z=0 到 z=1.5，含管道间隙）
        CollisionBox[] collisionBoxBuilder = new[] {
            new CollisionBox(
                new SerializedCollisionBox {
                    Center_L = new LocalVector(0f, 0f, 0.75f),
                    Dimensions_L = new LocalDimension(1f, 1f, 1.5f)
                }
            )
        };

        return new BuildingDrawData(
            renderVoidBelow: false,
            // 每层一个网格
            mainMeshPerLayer: new ILODMesh[] { mainLod, mainLod, mainLod },
            isolatedBlueprintMesh: mainLod,
            combinedBlueprintMesh: mainLod,
            previewMesh: mainLod.LODClose,
            glassMesh: glassLod,
            colliders: collisionBoxBuilder,
            customDrawData: new DissolverDrawData(fluidMeshLod),
            hasCustomOverviewMesh: false,
            customOverviewMesh: null,
            simulationRendererDrawsMainMesh: false);
    }
}
