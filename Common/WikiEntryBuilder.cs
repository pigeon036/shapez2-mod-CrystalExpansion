using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Game.Core.Content.Buildings;
using Game.Core.Research;
using UnityEngine;

namespace CrystalExpansion.Common;

internal class EmptyText : IText
{
    public void Build(System.Text.StringBuilder sb, ILocalizationResolver resolver, ITextStyleProvider textStyle) { }
    public IText Bind(string placeholder, IText value) => this;
}

// IWikiEntryContent → IWikiEntryContentData adapter
internal sealed class RuntimeWikiContentData : IWikiEntryContentData
{
    private readonly IWikiEntryContent _content;

    public RuntimeWikiContentData(IWikiEntryContent content)
    {
        _content = content;
    }

    public IWikiEntryContent Create()
    {
        return _content;
    }
}

internal sealed class WikiEntryBuilder
{
    private readonly MetaWikiEntry _entry;
    private readonly List<IWikiEntryContent> _contents;
    private readonly List<WikiEntryId> _relatedEntries;

    private WikiEntryBuilder(string id)
    {
        _entry = ScriptableObject.CreateInstance<MetaWikiEntry>();
        ((UnityEngine.Object)_entry).name = id;
        _contents = new List<IWikiEntryContent>();
        _relatedEntries = new List<WikiEntryId>();
    }

    public static WikiEntryBuilder Create(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("id must not be null or empty.", nameof(id));
        return new WikiEntryBuilder(id);
    }

    public WikiEntryBuilder ShowInKnowledgePanel(bool show = true)
    {
        _entry.ShowInKnowledgePanel = show;
        return this;
    }

    public WikiEntryBuilder AddHeadingLocalized(string translationKey)
    {
        if (string.IsNullOrEmpty(translationKey))
            throw new ArgumentException("translationKey must not be null or empty.", nameof(translationKey));
        _contents.Add(new WikiEntryContentHeading(new LazyLocalizedText(new TranslationId(translationKey))));
        return this;
    }

    public WikiEntryBuilder AddTextLocalized(string translationKey)
    {
        if (string.IsNullOrEmpty(translationKey))
            throw new ArgumentException("translationKey must not be null or empty.", nameof(translationKey));
        _contents.Add(new WikiEntryContentText(new LazyLocalizedText(new TranslationId(translationKey))));
        return this;
    }

    public WikiEntryBuilder AddBuildingPanel(BuildingDefinitionGroupId buildingId, string plainText)
    {
        _contents.Add(new WikiEntryContentBuildingPanel(buildingId, string.IsNullOrEmpty(plainText) ? (IText)new EmptyText() : new LazyLocalizedText(new TranslationId(plainText))));
        return this;
    }

    public WikiEntryBuilder AddBuildingPanelLocalized(BuildingDefinitionGroupId buildingId, string translationKey)
    {
        if (string.IsNullOrEmpty(translationKey))
            throw new ArgumentException("translationKey must not be null or empty.", nameof(translationKey));
        _contents.Add(new WikiEntryContentBuildingPanel(buildingId, new LazyLocalizedText(new TranslationId(translationKey))));
        return this;
    }

    public WikiEntryBuilder AddRelatedEntry(string entryId)
    {
        _relatedEntries.Add(new WikiEntryId(entryId));
        return this;
    }

    public MetaWikiEntry Build()
    {
        _entry.RelatedEntries = _relatedEntries.ToArray();
        _entry.Contents = _contents.Select(c => (IWikiEntryContentData)new RuntimeWikiContentData(c)).ToArray();
        return _entry;
    }
}
