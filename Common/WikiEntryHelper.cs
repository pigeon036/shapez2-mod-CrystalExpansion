using System;
using System.Linq;
using Game.Core.Research;
using Game.Orchestration;

namespace CrystalExpansion.Common;

internal static class WikiEntryHelper
{
    internal static void RegisterEntry(
        GameSessionOrchestrator orchestrator,
        MetaWikiEntry metaEntry,
        string categoryId,
        string insertAfterEntryId,
        params string[] requiredMechanicIds)
    {
        var serialized = new SerializedUnlockableWikiReference
        {
            EntryId = metaEntry.Id.Id,
            CategoryId = categoryId
        };

        var mechanics = requiredMechanicIds.Select(id => new ResearchMechanicId(id));
        var reference = new UnlockableWikiReference(serialized, mechanics, Array.Empty<ResearchUpgradeId>());

        var wikiRefs = orchestrator.Mode.ResearchLayout.WikiConfiguration.WikiReferences;
        int idx = wikiRefs.FindIndex(r => r.EntryId.Id == insertAfterEntryId);
        wikiRefs.Insert(idx + 1, reference);

        WikiDatabase wikiDb = (WikiDatabase)orchestrator.Mode.WikiDatabase;
        WikiEntry wikiEntry = wikiDb.Convert(metaEntry, reference);
        wikiDb.Entries.Add(wikiEntry.Id, wikiEntry);
        if (wikiEntry.ShowInKnowledgePanel)
            wikiDb._KnowledgePanelEntries.Add(wikiEntry.Id, wikiEntry);
    }

    // Dictionary 不保证迭代顺序，按 WikiReferences 重建确保 UI 导航顺序正确
    internal static void RebuildKnowledgePanel(GameSessionOrchestrator orchestrator)
    {
        var wikiConfig = orchestrator.Mode.ResearchLayout.WikiConfiguration;
        WikiDatabase wikiDb = (WikiDatabase)orchestrator.Mode.WikiDatabase;
        var panelEntries = wikiDb._KnowledgePanelEntries.Values.ToList();
        wikiDb._KnowledgePanelEntries.Clear();
        foreach (var wikiRef in wikiConfig.WikiReferences)
        {
            var match = panelEntries.FirstOrDefault(e => e.Id == wikiRef.EntryId);
            if (match != null)
                wikiDb._KnowledgePanelEntries.Add(match.Id, match);
        }
    }
}
