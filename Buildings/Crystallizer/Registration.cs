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

namespace CrystalExpansion.Buildings.Crystallizer;

using Renderer = CrystallizerRenderer;
using Simulation = CrystallizerSimulation;
using DrawData = ICrystallizerDrawData;

// 晶化器建筑注册——通过 Shifter Flow/Atomic API 声明建筑。
//
// 多格布局（1×2 前后排列，y 轴南北方向）：
//   Tile (0,0,0) — 北瓦片：形状输入 + 形状输出
//   Tile (0,1,0) — 南瓦片：流体输入（West 方向）
//
// 里程碑选择器：产业模式（场景 ID 含 "converter"）→ ConverterMilestoneTier8，
// 标准模式 → Milestone_Crystals。使用 ByIdPerScenarioMilestoneSelector 而非
// ByIdMilestoneSelector，因为后者在产业模式下因缺少 Milestone_Crystals 而崩溃。
//
// 碰撞盒手动计算：Center_L = (0, 0.5, 0.25) 覆盖前后 2 格（y=-0.5 到 y=1.5），高度 z=0 到 z=0.5。
// 原因是 BoundingBoxHelper.CreateBasicCollider 基于 mesh bounds 生成，多格建筑
// 默认碰撞盒不覆盖所有瓦片，必须手动构造 CollisionBox。
[UsedImplicitly]
internal static class CrystallizerRegistration
{
    public static void Register(ILogger logger, ModFolderLocator locator, ToolbarGroupBuilder toolbarGroupBuilder)
    {
        BuildingDefinitionGroupId groupId = new("CrystallizerGroup");
        BuildingDefinitionId definitionId = new("Crystallizer");

        string titleId = "building.crystallizer.title";
        string descriptionId = "building.crystallizer.description";

        Sprite icon = FileTextureLoader.LoadTextureAsSprite(locator.SubPath("ToolbarIcon/Crystallizer_Icon.png"), out _);

        // sortOrder=1：在工具栏分组中，晶化器排在溶解器 (2) 前面
        toolbarGroupBuilder.Add(icon, 1);

        LOD6Mesh fluidMeshLod = MeshLod.Create()
            .AddLod0Mesh(FbxMeshCombiner.LoadAndCombine(locator.SubPath("Crystallizer/Crystallizer_Fluid.fbx")))
            .BuildLod6Mesh();

        IBuildingGroupBuilder group = BuildingGroup
            .Create(groupId)
            .WithTitle(titleId.T())
            .WithDescription(descriptionId.T())
            .WithIcon(icon)
            .AsNonTransportableBuilding()
            .WithPreferredPlacement(DefaultPreferredPlacementMode.LinePerpendicular)
            .WithDefaultStructureOverview();

        ((BuildingGroupBuilder)(object)group).LinkedEntryId = new WikiEntryId("WKFluids_Crystallizer");

        IBuildingConnectorData connectorData = BuildingConnectors
            .MultiTile()
            .AddTile(new TileVector(0, 0, 0))
            .AddTile(new TileVector(0, 1, 0))
            .AddShapeInput(new TileVector(0, 0, 0), ShapeConnectorConfig.DefaultInput())
            .AddShapeOutput(new TileVector(0, 0, 0), ShapeConnectorConfig.DefaultOutput())
            .AddFluidInput(new TileVector(0, 1, 0), FluidConnectorConfig.CustomInput(TileDirection.West))
            .Build();

        IBuildingBuilder buildingBuilder = Building
            .Create(definitionId)
            .WithConnectorData(connectorData)
            .DynamicallyRendering<Renderer, Simulation, DrawData>(new CrystallizerDrawData(fluidMeshLod))
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
            .WithSimulation(new CrystallizerFactoryBuilder(), logger)
            .WithCustomModules(new CrystallizerBuildingModules(new ResearchSpeedId("CutterSpeed")))
            .WithPrediction(new CrystallizerPredictionFactoryBuilder(), logger)
            .Build();
    }

    private static BuildingDrawData CreateDrawData(ModFolderLocator locator, LOD6Mesh fluidMeshLod)
    {
        LOD6Mesh mainLod = MeshLod
            .Create()
            .AddLod0Mesh(FbxMeshCombiner.LoadAndCombine(locator.SubPath("Crystallizer/Crystallizer_Main.fbx")))
            .BuildLod6Mesh();

        LOD6Mesh glassLod = MeshLod
            .Create()
            .AddLod0Mesh(FbxMeshCombiner.LoadAndCombine(locator.SubPath("Crystallizer/Crystallizer_Glas.fbx")))
            .BuildLod6Mesh();

        // 手动计算碰撞盒：覆盖前后 2 格（y=-0.5 到 y=1.5）+ 高度 z=0 到 z=0.5
        CollisionBox[] collisionBoxBuilder = new[] {
            new CollisionBox(
                new SerializedCollisionBox {
                    Center_L = new LocalVector(0f, 0.5f, 0.25f),
                    Dimensions_L = new LocalDimension(1f, 2f, 0.5f)
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
            customDrawData: new CrystallizerDrawData(fluidMeshLod),
            hasCustomOverviewMesh: false,
            customOverviewMesh: null,
            simulationRendererDrawsMainMesh: false);
    }
}
