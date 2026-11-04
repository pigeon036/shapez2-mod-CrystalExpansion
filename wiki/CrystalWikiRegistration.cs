using System;
using CrystalExpansion.Common;
using Game.Core.Content.Buildings;
using ILogger = Core.Logging.ILogger;

namespace CrystalExpansion.Wiki;

internal static class CrystalWikiRegistration
{
    public static void Inject(GameSessionOrchestrator orchestrator, ILogger logger)
    {
        try
        {
            MetaWikiEntry crystallizerEntry = WikiEntryBuilder.Create("WKFluids_Crystallizer")
                .ShowInKnowledgePanel()
                .AddTextLocalized("wiki.WKFluids_Crystallizer.text")
                .AddBuildingPanel(new BuildingDefinitionGroupId("CrystallizerGroup"), "")
                .AddRelatedEntry("WKFluids_UnstableFluid")
                .Build();

            MetaWikiEntry dissolverEntry = WikiEntryBuilder.Create("WKFluids_Dissolver")
                .ShowInKnowledgePanel()
                .AddTextLocalized("wiki.WKFluids_Dissolver.text")
                .AddBuildingPanel(new BuildingDefinitionGroupId("DissolverGroup"), "")
                .AddRelatedEntry("WKFluids_UnstableFluid")
                .Build();

            MetaWikiEntry unstableFluidEntry = WikiEntryBuilder.Create("WKFluids_UnstableFluid")
                .ShowInKnowledgePanel()
                .AddTextLocalized("wiki.WKFluids_UnstableFluid.text")
                .AddRelatedEntry("WKShapes_Shapes")
                .AddRelatedEntry("WKFluids_Intro")
                .AddRelatedEntry("WKFluids_CrystalGenerator")
                .AddRelatedEntry("WKFluids_Crystallizer")
                .AddRelatedEntry("WKFluids_Dissolver")
                .Build();

            WikiEntryHelper.RegisterEntry(orchestrator, unstableFluidEntry, "Fluids", "WKFluids_FluidTank", "RUFluids");
            WikiEntryHelper.RegisterEntry(orchestrator, dissolverEntry, "Fluids", "WKFluids_UnstableFluid", "RUFluids");
            WikiEntryHelper.RegisterEntry(orchestrator, crystallizerEntry, "Fluids", "WKFluids_CrystalGenerator", "RUFluids");

            WikiEntryHelper.RebuildKnowledgePanel(orchestrator);

            logger.Info?.Log("Wiki: injected 3 wiki entries");
        }
        catch (Exception ex)
        {
            logger.Error?.Log($"Wiki: Inject failed - {ex}");
        }
    }
}
