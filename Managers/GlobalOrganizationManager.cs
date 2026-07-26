using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FashionSenseWardrobeManager;

internal sealed class GlobalOrganizationManager
{
    private const string GlobalDataKey = "GlobalOutfitOrganization";
    private const string LegacyUniqueId = "NatrollEXE.FashionSenseOutfitPreview";

    private readonly IDataHelper _data;
    private readonly IMonitor _monitor;

    public GlobalOrganizationManager(IDataHelper data, IMonitor monitor)
    {
        _data = data;
        _monitor = monitor;
        MigrateLegacyGlobalData();
    }

    private void MigrateLegacyGlobalData()
    {
        if (_data.ReadGlobalData<GlobalOutfitOrganization>(GlobalDataKey) is not null)
            return;

        string legacyPath = Path.Combine(
            Constants.DataPath,
            ".smapi",
            "mod-data",
            LegacyUniqueId.ToLowerInvariant(),
            $"{GlobalDataKey.ToLowerInvariant()}.json");

        if (!File.Exists(legacyPath))
            return;

        try
        {
            GlobalOutfitOrganization? legacyData = JsonSerializer.Deserialize<GlobalOutfitOrganization>(
                File.ReadAllText(legacyPath));
            if (legacyData is not null)
                _data.WriteGlobalData(GlobalDataKey, legacyData);
        }
        catch (Exception ex)
        {
            _monitor.Log($"Could not migrate legacy global wardrobe organization: {ex.Message}", LogLevel.Warn);
        }
    }

    public void Export(List<OutfitCategory> categories, List<OutfitTag> tags)
    {
        GlobalOutfitOrganization data = new()
        {
            Categories = categories
                .Select(category => new GlobalOutfitCategory
                {
                    Name = category.Name,
                    OutfitNames = CleanOutfitNames(category.OutfitNames)
                })
                .Where(category => !string.IsNullOrWhiteSpace(category.Name))
                .ToList(),

            Tags = tags
                .Select(tag => new GlobalOutfitTag
                {
                    Name = tag.Name,
                    Kind = tag.Kind,
                    OutfitNames = CleanOutfitNames(tag.OutfitNames)
                })
                .Where(tag => !string.IsNullOrWhiteSpace(tag.Name))
                .ToList()
        };

        _data.WriteGlobalData(GlobalDataKey, data);
    }

    public bool TryImportInto(List<OutfitCategory> categories, List<OutfitTag> tags)
    {
        GlobalOutfitOrganization? data = _data.ReadGlobalData<GlobalOutfitOrganization>(GlobalDataKey);

        if (data is null)
            return false;

        foreach (GlobalOutfitCategory importedCategory in data.Categories)
        {
            string name = importedCategory.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            OutfitCategory? category = categories.FirstOrDefault(existing =>
                existing.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (category is null)
            {
                category = new OutfitCategory
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = name,
                    OutfitNames = new List<string>()
                };
                categories.Add(category);
            }

            MergeOutfitNames(category.OutfitNames, importedCategory.OutfitNames);
        }

        foreach (GlobalOutfitTag importedTag in data.Tags)
        {
            string name = importedTag.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            OutfitTag? tag = tags.FirstOrDefault(existing =>
                existing.Kind == importedTag.Kind
                && existing.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (tag is null)
            {
                tag = new OutfitTag
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = name,
                    Kind = importedTag.Kind,
                    OutfitNames = new List<string>()
                };
                tags.Add(tag);
            }

            MergeOutfitNames(tag.OutfitNames, importedTag.OutfitNames);
        }

        return true;
    }

    private static List<string> CleanOutfitNames(IEnumerable<string> outfitNames)
    {
        return outfitNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void MergeOutfitNames(List<string> target, IEnumerable<string> source)
    {
        HashSet<string> existing = new(target, StringComparer.OrdinalIgnoreCase);

        foreach (string outfitName in CleanOutfitNames(source))
        {
            if (existing.Add(outfitName))
                target.Add(outfitName);
        }
    }
}

internal sealed class GlobalOutfitOrganization
{
    public int Version { get; set; } = 1;
    public List<GlobalOutfitCategory> Categories { get; set; } = new();
    public List<GlobalOutfitTag> Tags { get; set; } = new();
}

internal sealed class GlobalOutfitCategory
{
    public string Name { get; set; } = string.Empty;
    public List<string> OutfitNames { get; set; } = new();
}

internal sealed class GlobalOutfitTag
{
    public string Name { get; set; } = string.Empty;
    public TagKind Kind { get; set; } = TagKind.General;
    public List<string> OutfitNames { get; set; } = new();
}
