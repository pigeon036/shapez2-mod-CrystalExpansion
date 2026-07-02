using System.Collections.Generic;
using Game.Core.Coordinates;
using Game.Core.Rendering;
using UnityEngine;

namespace CrystalExpansion.Buildings.Crystallizer;

public interface ICrystallizerDrawData : IFluidRendererDrawData
{
    IBeltLaneRendererDefinition InputBeltLane { get; }
    IBeltLaneRendererDefinition ProcessingBeltLane { get; }
    IBeltLaneRendererDefinition OutputBeltLane { get; }
    // 晶体生长高度曲线——0→0.25→0.2±0.015 振荡→0
    AnimationCurve ProcessingHeight { get; }
    // 晶体透明度曲线——从 0（不可见）线性增长至 1（完全不透明）
    AnimationCurve GrowProgress { get; }
}

internal class CrystallizerDrawData : ICrystallizerDrawData
{
    public IBeltLaneRendererDefinition InputBeltLane { get; }
    public IBeltLaneRendererDefinition ProcessingBeltLane { get; }
    public IBeltLaneRendererDefinition OutputBeltLane { get; }
    public AnimationCurve ProcessingHeight { get; }
    public AnimationCurve GrowProgress { get; }
    public IReadOnlyList<ILODMesh> FluidFillMeshes { get; }

    public CrystallizerDrawData(ILODMesh fluidFillMesh)
    {
        // InputLane: 物品从 x=-0.5（建筑左侧边缘）移动到 x=0（中心处理位置）
        InputBeltLane = new CrystallizerLaneDef(
            new LocalVector(-0.5f, 0f, 0f),
            new LocalVector(0f, 0f, 0f));
        // Processing: 物品在中心位置垂直动画（由 ProcessingHeight 曲线控制 Z 轴）
        ProcessingBeltLane = new CrystallizerLaneDef(
            new LocalVector(0f, 0f, 0f),
            new LocalVector(0f, 0f, 0f));
        // OutputLane: 物品从 x=0 移动到 x=+0.5（建筑右侧边缘）
        OutputBeltLane = new CrystallizerLaneDef(
            new LocalVector(0f, 0f, 0f),
            new LocalVector(0.5f, 0f, 0f));

        // 生长高度曲线：t=0→0 基底，t=0.2→0.2 弹出，t=0.35~0.85 在 0.2±0.015 振荡（脉动效果），t=1→0 收缩
        ProcessingHeight = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.05f, 0f),
            new Keyframe(0.2f, 0.2f),
            new Keyframe(0.35f, 0.215f),
            new Keyframe(0.50f, 0.185f),
            new Keyframe(0.65f, 0.21f),
            new Keyframe(0.85f, 0.2f),
            new Keyframe(1f, 0f));

        GrowProgress = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f));

        FluidFillMeshes = new ILODMesh[] { fluidFillMesh };
    }
}

internal class CrystallizerLaneDef : IBeltLaneRendererDefinition
{
    public LocalVector ItemStartPos_L { get; }
    public LocalVector ItemEndPos_L { get; }

    public CrystallizerLaneDef(LocalVector itemStartPos_L, LocalVector itemEndPos_L)
    {
        ItemStartPos_L = itemStartPos_L;
        ItemEndPos_L = itemEndPos_L;
    }
}