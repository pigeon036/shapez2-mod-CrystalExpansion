using System.Collections.Generic;
using System.IO;
using Game.Content.Features;

namespace CrystalExpansion.Mode;

internal static class ModeConfig
{
    // 所有变体 FBX 文件路径，键为大写基础部件码，值为相对 Resources 目录的路径
    public static IReadOnlyDictionary<char, string> CrystalFbxPaths { get; private set; }
    // 基础部件码 → 小写晶体变体码的映射（如 R→r, S→s）
    public static IReadOnlyDictionary<char, char> BaseToCrystal { get; private set; }
    // 当前模式下的基础形状码集合（排除 Pin、晶体、已有小写码）
    public static HashSet<char> BaseCodes { get; private set; }
    // 游戏原始晶体码（四分模式 'c'，六分模式 'h'）
    public static char OriginalCrystalCode { get; private set; }
    // 所有晶体码（小写变体码 + 原版晶体码，若原码不是 'c'）
    // CrystalLogicPatch.IsCrystalPart 使用此集合判断部件是否为晶体，必须包含原版码
    public static HashSet<char> CrystalCodes { get; private set; }
    public static string ResourcesDir { get; private set; }
    // 基础部件码 → IShapeSubPart 的 O(1) 查找字典，
    // CrystallizerOperation 和 CrystalGeneratorPatch 共享
    public static IReadOnlyDictionary<char, IShapeSubPart> BaseCodeToCrystalPart { get; internal set; }

    // 必须在 GameDataHook 前缀中最早调用，为后续 NewCrystalRegistry 和 ShapeTypedFluidPatches 提供配置。
    // 通过 config.PartCount 判断模式：4（四分）/ 6（六分）。
    public static void Initialize(IShapesConfiguration config)
    {
        ResourcesDir = Path.Combine(
            Path.GetDirectoryName(typeof(ModeConfig).Assembly.Location),
            "Resources");

        Dictionary<char, string> crystalFbxPaths;
        if (config.PartCount == 6)
        {
            crystalFbxPaths = new Dictionary<char, string>
            {
                { 'H', "CrystalHex/CrystalHex_H.fbx" },
                { 'F', "CrystalHex/CrystalHex_F.fbx" },
                { 'G', "CrystalHex/CrystalHex_G.fbx" },
            };
        }
        else
        {
            crystalFbxPaths = new Dictionary<char, string>
            {
                { 'C', "CrystalQuad/CrystalQuad_C.fbx" },
                { 'R', "CrystalQuad/CrystalQuad_R.fbx" },
                { 'S', "CrystalQuad/CrystalQuad_S.fbx" },
                { 'W', "CrystalQuad/CrystalQuad_W.fbx" },
            };
        }
        CrystalFbxPaths = crystalFbxPaths;

        // 晶体变体使用小写码，以区别于大写基础部件（Pin 等占位部件）。
        Dictionary<char, char> baseToCrystal = new();
        foreach (char code in crystalFbxPaths.Keys)
            baseToCrystal[code] = char.ToLowerInvariant(code);
        BaseToCrystal = baseToCrystal;

        char pinCode = config.PinShapePart.Code;
        char originalCrystalCode = config.CrystalShapePart.Code;
        OriginalCrystalCode = originalCrystalCode;

        // BaseCodes: 收集所有可作为"目标形状"的基础部件码。
        // 排除 Pin（永不晶体化）、已有晶体（避免自引用）、已有小写码（已是变体）。
        HashSet<char> baseCodes = new();
        foreach (IShapeSubPart part in config.Parts)
        {
            char code = part.Code;
            if (code == pinCode || code == originalCrystalCode || char.IsLower(code))
                continue;
            baseCodes.Add(code);
        }
        BaseCodes = baseCodes;

    // CrystalCodes 包含所有晶体变体码 + 原版晶体码（若原码不是 'c'，即六分模式的 'h'）。
    // CrystalLogicPatch.IsCrystalPart 依赖此集合识别晶体部件以判定相邻性。
        HashSet<char> crystalCodes = new(baseToCrystal.Values);
        if (OriginalCrystalCode != 'c')
            crystalCodes.Add(OriginalCrystalCode);
        CrystalCodes = crystalCodes;
    }
}
