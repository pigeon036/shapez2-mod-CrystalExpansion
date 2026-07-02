using CrystalExpansion.Common;
using CrystalExpansion.Mode;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Crystal;

internal static class NewCrystalRegistry
{
    // 向游戏的 ShapesConfiguration 中注入晶体部件（更新原版 FBX + 创建新变体 ScriptableObject）。
    // 两遍遍历：
    // 1. 更新已有的 MetaShapeSubPart 网格（仅六分模式，原版占位部件已存在但需要 FBX 模型）
    // 2. 在原版晶体部件之后插入新变体 ScriptableObject（四分 r/s/w，六分 f/g）
    // 插入位置：crystalIdx + 1 + variantOffset，确保变体紧随原版晶体之后。
    // 使用 IList 强制转换而非泛型 List<T>，因为游戏的 Parts 属性可能返回非 List 的 IList 实现。
    public static void Inject(IShapesConfiguration config, ILogger logger)
    {
        logger.Info?.Log("CrystalVariant: Inject start");

        IList parts = config.Parts as IList;
        IList allParts = config.MapGenerationAllParts as IList;

        logger.Info?.Log($"CrystalVariant: parts={parts.Count} targets=[{string.Join(",", ModeConfig.BaseToCrystal.Keys)}]");

        int crystalIdxParts = parts.IndexOf(config.CrystalShapePart);
        int crystalIdxAll = allParts.IndexOf(config.CrystalShapePart);
        if (crystalIdxParts < 0) crystalIdxParts = parts.Count - 1;
        if (crystalIdxAll < 0) crystalIdxAll = allParts.Count - 1;
        int variantOffset = 0;

        // 第一遍：更新已有部件的网格（仅限六分模式已有小写变体占位符的场景）
        foreach (char code in ModeConfig.BaseToCrystal.Values)
        {
            MetaShapeSubPart existing = parts.Cast<IShapeSubPart>().FirstOrDefault(p => p.Code == code) as MetaShapeSubPart;
            if (existing == null) continue;
            if (!ModeConfig.CrystalFbxPaths.TryGetValue(char.ToUpperInvariant(code), out var relPath)) continue;
            string fbxPath = Path.Combine(ModeConfig.ResourcesDir, relPath);
            if (File.Exists(fbxPath))
            {
                UpdateMeshFromFbx(existing, fbxPath);
                logger.Info?.Log($"CrystalVariant: updated '{code}' mesh from FBX");
            }
        }

        MetaShapeSubPart crystalMeta = (MetaShapeSubPart)config.CrystalShapePart;

        // 第二遍：为 BaseToCrystal 中的每个映射创建新变体 ScriptableObject
        // 仅对尚不存在小写变体的基础部件创建（!parts.Any(p => p.Code == lower)）
        foreach (IShapeSubPart part in parts.Cast<IShapeSubPart>().ToList())
        {
            if (!ModeConfig.BaseToCrystal.ContainsKey(part.Code)) continue;
            char lower = char.ToLowerInvariant(part.Code);
            if (parts.Cast<IShapeSubPart>().Any(p => p.Code == lower)) continue;

            MetaShapeSubPart meta = (MetaShapeSubPart)part;
            MetaShapeSubPart crystalVariant = ScriptableObject.CreateInstance<MetaShapeSubPart>();
            crystalVariant._Code = lower;
            crystalVariant._AllowColor = true;
            crystalVariant._AllowChangingColor = true;
            crystalVariant._DestroyOnFallDown = crystalMeta._DestroyOnFallDown;
            // 变体继承基础形状的高清网格（用作 LOD0），同时设置覆盖为 Crystal 材质
            crystalVariant.HighDetailMesh = meta.HighDetailMesh;
            crystalVariant.OverrideMaterial = true;
            crystalVariant.Material = ShapeShaderMaterialType.Crystal;
            crystalVariant.name = $"Crystal_{meta._Code}_Variant";

            // 如果存在自定义 FBX 模型，则用 FBX 替换默认网格和 Mesh 字段
            crystalVariant.Mesh = meta.Mesh;
            if (ModeConfig.CrystalFbxPaths.TryGetValue(meta._Code, out var relPath))
            {
                string fbxPath = Path.Combine(ModeConfig.ResourcesDir, relPath);
                if (File.Exists(fbxPath))
                {
                    UpdateMeshFromFbx(crystalVariant, fbxPath);
                    logger.Info?.Log($"CrystalVariant: '{lower}' uses custom FBX ({fbxPath})");
                }
            }

            // ScriptableObject 未保存到场景，DontDestroyOnLoad 确保在场景切换后仍存活
            Object.DontDestroyOnLoad(crystalVariant);

            parts.Insert(crystalIdxParts + 1 + variantOffset, crystalVariant);
            allParts.Insert(crystalIdxAll + 1 + variantOffset, crystalVariant);
            variantOffset++;

            logger.Info?.Log($"CrystalVariant: added '{lower}' ({meta._Code})");
        }

        // 构建 BaseCodeToCrystalPart 字典：基础码 → 对应晶体部件的 IShapeSubPart（含原版晶体）
        // CrystallizerOperation 和 CrystalGenerator 通过此字典做 O(1) 查找
        Dictionary<char, IShapeSubPart> dict = new();
        foreach (var kv in ModeConfig.BaseToCrystal)
            foreach (IShapeSubPart p in config.Parts)
                if (p.Code == kv.Value) { dict[kv.Key] = p; break; }
        dict[char.ToUpperInvariant(config.CrystalShapePart.Code)] = config.CrystalShapePart;
        ModeConfig.BaseCodeToCrystalPart = dict;

        logger.Info?.Log("CrystalVariant: Inject done");
    }

    // 通过 Assimp 加载 FBX 并更新目标 MetaShapeSubPart 的网格引用
    private static void UpdateMeshFromFbx(MetaShapeSubPart target, string fbxPath)
    {
        Mesh mesh = FbxMeshCombiner.LoadAndCombine(fbxPath);
        target.Mesh = CreateLodAsset(mesh);
        UnityMeshReference hdRef = new() { _Mesh = mesh };
        target.HighDetailMesh = hdRef;
    }

    // 将所有 LOD 等级绑定到同一个网格，因为 FBX 模型是静态装饰件，无需分级细化
    private static LODMeshAsset CreateLodAsset(UnityEngine.Mesh mesh)
    {
        LODMeshAsset lodAsset = ScriptableObject.CreateInstance<LODMeshAsset>();
        UnityMeshReference meshRef = new() { _Mesh = mesh };

        lodAsset.Meshes.LODClose = meshRef;
        lodAsset.Meshes.LODNormal = meshRef;
        lodAsset.Meshes.LODFar = meshRef;
        lodAsset.Meshes.LODReduced = meshRef;
        lodAsset.Meshes.LODMinimal = meshRef;
        lodAsset.Meshes.LODOverview = meshRef;

        return lodAsset;
    }
}