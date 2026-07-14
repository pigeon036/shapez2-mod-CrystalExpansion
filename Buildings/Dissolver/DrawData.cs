using System.Collections.Generic;
using Game.Core.Coordinates;
using UnityEngine;

namespace CrystalExpansion.Buildings.Dissolver;

public interface IDissolverDrawData : IFluidRendererDrawData
{
    IBeltLaneRendererDefinition InputBeltLane { get; }
    // 下落高度曲线——物品从顶层 z=1.0 降至罐底 z=0.05
    AnimationCurve FallHeight { get; }
}

internal class DissolverDrawData : IDissolverDrawData
{
    public IReadOnlyList<ILODMesh> FluidFillMeshes { get; }
    public IBeltLaneRendererDefinition InputBeltLane { get; }
    public AnimationCurve FallHeight { get; }

    public DissolverDrawData(LOD6Mesh fluidFillMesh)
    {
        // InputLane: 物品从建筑左侧边缘 x=-0.5（上层 z=1）移动到中心 x=0
        InputBeltLane = new DissolverLaneDef(
            new LocalVector(-0.5f, 0f, 1f),
            new LocalVector(0f, 0f, 1f));

        // 下落曲线：
        // t=0: z=1.0，outTangent=0（零初速度，物品在上层边缘静止）
        // t=0.15: z=0.05，inTangent=-12.667（加速下落到达罐底）
        // t=0.15→1.0: z=0.05 保持（等待 delay 到期）
        FallHeight = new AnimationCurve(
            new Keyframe(0f, 1.0f, 0f, 0f),
            new Keyframe(0.15f, 0.05f, -12.667f, 0f),
            new Keyframe(1f, 0.05f, 0f, 0f));

        FluidFillMeshes = new ILODMesh[] { fluidFillMesh };
    }
}

internal class DissolverLaneDef : IBeltLaneRendererDefinition
{
    public LocalVector ItemStartPos_L { get; }
    public LocalVector ItemEndPos_L { get; }

    public DissolverLaneDef(LocalVector start, LocalVector end)
    {
        ItemStartPos_L = start;
        ItemEndPos_L = end;
    }
}