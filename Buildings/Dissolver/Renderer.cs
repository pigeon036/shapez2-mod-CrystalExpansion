using Game.Core.Coordinates;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalExpansion.Buildings.Dissolver;

// 溶解器动态渲染器——绘制物品下落动画和输入流体罐。
//
// 仅绘制 InputLane（上层传送带）和 ProcessingLane（下落中的物品）。
// EndLane 中的物品不渲染（停留时间极短，肉眼不可见）。
//
// 下落动画：ProcessingLane 物品的 Z 坐标由 FallHeight 曲线实时计算，
// 通过 LocalVector pos 直接传递给 DrawBeltItem。
[UsedImplicitly]
public class DissolverRenderer
    : StatelessBuildingSimulationRenderer<DissolverSimulation, IDissolverDrawData>
{
    public DissolverRenderer(
        IMapModel map,
        IBuildingSoundManager soundManager,
        IShapeRegistry shapeRegistry) : base(map) { }

    public override void OnDrawDynamic(in Entity entity, FrameDrawOptions options)
    {
        DissolverSimulation simulation = entity.Simulation;
        IDissolverDrawData drawData = entity.DrawData;

        DrawBeltItem(entity.Transform, options, simulation.InputLane, drawData.InputBeltLane);

        // 绘制 ProcessingLane 中的物品——Z 坐标由 FallHeight 动画曲线控制
        if (simulation.ProcessingLane.HasItem)
        {
            float t = simulation.ProcessingLane.Progress;
            float z = drawData.FallHeight.Evaluate(t);
            LocalVector pos = new(0f, 0f, z);
            DrawBeltItem(in entity.Transform, options, simulation.ProcessingLane.Item, in pos);
        }

        // 输入流体罐渲染——使用废弃的 FluidsContainer.Draw API
        if (simulation.InputTank.Fluid != null
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
                    new float3(1f, simulation.InputTank.Level, 1f)),
                simulation.InputTank.Fluid);
#pragma warning restore CS0618 // 类型或成员已过时
        }
    }
}
