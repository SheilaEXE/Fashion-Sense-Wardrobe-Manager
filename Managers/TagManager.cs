using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FashionSenseWardrobeManager;

internal enum TagKind { General, Color }

/// <summary>A tag that can be assigned to one or more outfits.</summary>
internal sealed class OutfitTag
{
    public string  Id       { get; set; } = string.Empty;
    public string  Name     { get; set; } = string.Empty;
    public TagKind Kind     { get; set; } = TagKind.General;
    /// <summary>Outfit names that carry this tag.</summary>
    public List<string> OutfitNames { get; set; } = new();
}

/// <summary>
/// Manages outfit tags, persisting them in the player's modData.
/// Current key: "FashionSenseWardrobeManager.Tags". The previous key is retained for migration.
/// </summary>
internal sealed class TagManager
{
    private const string ModDataKey = "FashionSenseWardrobeManager.Tags";
    private const string LegacyModDataKey = "FashionSenseOutfitPreview.Tags";

    public List<OutfitTag> LoadTags()
    {
        if (TryLoad(ModDataKey, out List<OutfitTag> tags))
            return tags;

        if (TryLoad(LegacyModDataKey, out tags))
        {
            SaveTags(tags);
            return tags;
        }

        return new();
    }

    public void SaveTags(List<OutfitTag> tags)
    {
        Game1.player.modData[ModDataKey] = JsonSerializer.Serialize(tags);
    }

    /// <summary>
    /// Add a new tag. Returns the tag on success, or null if the name is already taken
    /// within the same kind.
    /// </summary>
    public OutfitTag? AddTag(List<OutfitTag> tags, string name, TagKind kind)
    {
        string trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        if (tags.Any(t => t.Kind == kind
                       && t.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            return null;

        var tag = new OutfitTag
        {
            Id          = Guid.NewGuid().ToString("N"),
            Name        = trimmed,
            Kind        = kind,
            OutfitNames = new()
        };

        tags.Add(tag);
        SaveTags(tags);
        return tag;
    }

    public bool DeleteTag(List<OutfitTag> tags, string tagId)
    {
        int removed = tags.RemoveAll(t => t.Id == tagId);
        if (removed > 0) SaveTags(tags);
        return removed > 0;
    }

    public void ToggleOutfitInTag(List<OutfitTag> tags, string tagId, string outfitName)
    {
        OutfitTag? tag = tags.FirstOrDefault(t => t.Id == tagId);
        if (tag is null) return;

        if (tag.OutfitNames.Contains(outfitName))
            tag.OutfitNames.Remove(outfitName);
        else
            tag.OutfitNames.Add(outfitName);

        SaveTags(tags);
    }

    /// <summary>Return all tags (of any kind) assigned to the given outfit.</summary>
    public IEnumerable<OutfitTag> GetTagsForOutfit(List<OutfitTag> tags, string outfitName)
        => tags.Where(t => t.OutfitNames.Contains(outfitName));

    private static bool TryLoad(string key, out List<OutfitTag> tags)
    {
        tags = new();
        if (!Game1.player.modData.TryGetValue(key, out string? json)
            || string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            tags = JsonSerializer.Deserialize<List<OutfitTag>>(json) ?? new();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
