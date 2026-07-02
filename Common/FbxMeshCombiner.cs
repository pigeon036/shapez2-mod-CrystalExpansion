using Assimp;
using ShapezShifter.Kit;
using UnityMesh = UnityEngine.Mesh;
using UnityEngine;

namespace CrystalExpansion.Common;

// 运行时 FBX 加载工具。通过 Assimp 库导入 FBX 文件并转换为 Unity Mesh。
// 游戏使用 LODMesh 存储网格，FBX 是设计时格式 (Unity ModelImporter)，运行时无法直接加载。
// Assimp 以 TargetRealTimeMaximumQuality 后处理标志导入，然后通过 Shifter 的
// AssimpToUnityMeshConverter 转换为 Unity 网格。多子网格的 FBX 使用 CombineInstance 合并。
internal static class FbxMeshCombiner
{
    public static UnityMesh LoadAndCombine(string filePath)
    {
        using AssimpContext importer = new();
        Scene scene = importer.ImportFile(
            file: filePath,
            postProcessFlags: PostProcessPreset.TargetRealTimeMaximumQuality);

        if (scene.Meshes.Count == 0)
            throw new System.Exception($"No meshes found in {filePath}");

        UnityMesh result = new();

        if (scene.Meshes.Count == 1)
            result = AssimpToUnityMeshConverter.ConvertStaticMesh(scene.Meshes[0]);
        else
        {
            // 多子网格：分别转换后通过 CombineInstance 合并为单一网格。
            // mergeSubMeshes = true 将所有子网格合并为统一材质组。
            CombineInstance[] combine = new CombineInstance[scene.Meshes.Count];
            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                UnityMesh unityMesh = AssimpToUnityMeshConverter.ConvertStaticMesh(scene.Meshes[i]);
                combine[i].mesh = unityMesh;
                combine[i].transform = UnityEngine.Matrix4x4.identity;
            }
            result.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: true);
        }
        return result;
    }
}
