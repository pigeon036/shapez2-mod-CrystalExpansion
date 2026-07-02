using System;
using System.Collections.Generic;
using MonoMod.RuntimeDetour;

namespace CrystalExpansion.Common;

// MonoMod Hook 生命周期管理器。批量收集所有 Detour Hook，
// Dispose 时统一卸载，消除分散的 _xxxHook?.Dispose() 模式。
// Hook.Dispose() 会从方法中移除 Detour 并恢复原始 IL，必须在模组卸载时调用。
internal sealed class HookCollection : IDisposable
{
    private readonly List<Hook> _hooks = new();

    public void Add(Hook hook)
    {
        _hooks.Add(hook);
    }

    public void Dispose()
    {
        foreach (Hook hook in _hooks)
            hook.Dispose();
        _hooks.Clear();
    }
}
