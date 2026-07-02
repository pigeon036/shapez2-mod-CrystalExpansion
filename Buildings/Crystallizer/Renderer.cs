using Game.Core.Coordinates;
using Game.Core.Rendering;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalExpansion.Buildings.Crystallizer;

// 晶化器动态渲染器——多通道绘制晶体生长动画。
//
// 渲染层次（由底至顶）：
// 1. 不变底座（unchanged shape）：标准材质，显示未被晶体化的原始部件
// 2. 支撑网格（support mesh）：透明底座，提供深度排序参考
// 3. 晶体变体（crystal shape）：ShapeMaterialGrowCrystal 材质 + alpha 属性块，
//    实现从透明到不透明的生长效果
//
// 使用 RegularNonInstanced.DrawMesh 而非 DrawBeltItem 的原因：
// InstancedMeshManager 的新实例到可见之间有 1 帧延迟，动画会不同步。
[UsedImplicitly]
public class CrystallizerRenderer
    : StatelessBuildingSimulationRenderer<CrystallizerSimulation, ICrystallizerDrawData>
{
    public CrystallizerRenderer(
        IMapModel map,
        IBuildingSoundManager soundManager,
        IShapeRegistry shapeRegistry) : base(map) { }

    public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
    {
        CrystallizerSimulation simulation = entity.Simulation;
        ICrystallizerDrawData drawData = entity.DrawData;

        DrawBeltItem(entity.Transform, options, simulation.InputLane, drawData.InputBeltLane);
        DrawBeltItem(entity.Transform, options, simulation.OutputLane, drawData.OutputBeltLane);

        if (simulation.ProcessingLane.HasItem)
        {
            float t = simulation.ProcessingLane.Progress;

            ShapeItem unchanged = simulation.State.CurrentUnchangedShape;
            ShapeItem crystal = simulation.State.CrystalShape;

            float z = drawData.ProcessingHeight.Evaluate(t);
            LocalVector pos = new(0f, 0f, z);

            IBeltItemRenderer beltItems = options.Renderers.BeltItems;
            IShapeItemRenderer shapeRenderer = beltItems.ShapeRenderer;

            // 层 1：绘制不变底座（如果存在）
            if (unchanged != null)
            {
                DrawBeltItem(in entity.Transform, options, unchanged, in pos);
            }
            // 如果无不变底座但有晶体形状，绘制支撑网格作为深度参考
            else if (crystal != null)
            {
                MeshMaterialCombination supportMesh = shapeRenderer.GetSupportMeshForLOD(options.LOD.ShapeLOD);
                // 支撑网格位于传送带表面上方的 beltShapeHeight 偏移处
                WorldCoordinate supportPos = (pos + new LocalVector(0f, 0f, beltItems.BeltShapeHeight)) * entity.Transform;
                options.Renderers.RegularNonInstanced.DrawMesh(
                    supportMesh.Mesh,
                    supportMesh.Material,
                    FastMatrix.Translate(in supportPos),
                    RenderCategory.BuildingsDynamic);
            }

            // 层 3：绘制晶体生长形状
            // alpha 从 GrowProgress 曲线采样，< 0.01 时跳过（不可见）
            if (crystal != null)
            {
                float alpha = drawData.GrowProgress.Evaluate(t);
                if (alpha > 0.01f)
                {
                    // 晶体位置：支撑网格上方再叠加 supportMeshHeight 偏移
                    var worldPos = (pos + new LocalVector(0f, 0f, beltItems.BeltShapeHeight + shapeRenderer.SupportMeshHeight)) * entity.Transform;

                    MaterialPropertyBlock props = MaterialPropertyHelpers.CreateAlphaBlock(alpha);
                    options.Renderers.RegularNonInstanced.DrawMesh(
                        shapeRenderer.GetDrawData(crystal.Definition, options.LOD.ShapeLOD).Mesh,
                        shapeRenderer.ShapeMaterialGrowCrystal,
                        FastMatrix.Translate(in worldPos),
                        RenderCategory.BuildingsDynamic,
                        props);
                }
            }
        }

        // 流体罐渲染——使用废弃的 FluidsContainer.Draw API
        if (simulation.Container.Fluid != null
            && drawData.FluidFillMeshes.Count > 0
            && drawData.FluidFillMeshes[0].TryGet(options.LOD.BuildingLOD, out IMeshReference fluidHandle))
        {
            #pragma warning disable CS0618 // 类型或成员已过时
            options.Renderers.FluidsContainer.Draw(
                options,
                fluidHandle,
                Matrix4x4.TRS(
                    (Vector3)(new LocalVector(0f, 0f, 0.05f) * entity.Transform),
                    FastMatrix.RotateY(entity.Transform.Rotation),
                    new float3(1f, simulation.Container.Level, 1f)),
                simulation.Container.Fluid);
            #pragma warning restore CS0618 // 类型或成员已过时
        }
    }
}
