using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FashionSenseWardrobeManager;

/// <summary>
/// Manages outfit categories, persisting them in the player's modData so they survive across sessions.
/// Current key: "FashionSenseWardrobeManager.Categories". The previous key is retained for migration.
/// </summary>
internal sealed class CategoryManager
{
    private const string ModDataKey = "FashionSenseWardrobeManager.Categories";
    private const string LegacyModDataKey = "FashionSenseOutfitPreview.Categories";

    // Public API

    /// <summary>Load all categories from the current player's modData.</summary>
    public List<OutfitCategory> LoadCategories()
    {
        if (TryLoad(ModDataKey, out List<OutfitCategory> categories))
            return categories;

        if (TryLoad(LegacyModDataKey, out categories))
        {
            SaveCategories(categories);
            return categories;
        }

        return new List<OutfitCategory>();
    }

    /// <summary>Persist the given category list to the current player's modData.</summary>
    public void SaveCategories(List<OutfitCategory> categories)
    {
        string json = JsonSerializer.Serialize(categories);
        Game1.player.modData[ModDataKey] = json;
    }

    /// <summary>
    /// Add a new category with the given name (if a category with that name doesn't already exist).
    /// Returns the new category on success, or null if the name is already taken.
    /// </summary>
    public OutfitCategory? AddCategory(List<OutfitCategory> categories, string name)
    {
        string trimmed = name.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        if (categories.Any(c => c.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            return null;

        var category = new OutfitCategory
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = trimmed,
            OutfitNames = new List<string>()
        };

        categories.Add(category);
        SaveCategories(categories);

        return category;
    }

    /// <summary>Delete the category with the given id. Returns true if it was found and removed.</summary>
    public bool DeleteCategory(List<OutfitCategory> categories, string categoryId)
    {
        int removed = categories.RemoveAll(c => c.Id == categoryId);

        if (removed > 0)
            SaveCategories(categories);

        return removed > 0;
    }

    /// <summary>
    /// Toggle an outfit's membership in a category.
    /// If the outfit is already in the category it is removed; otherwise it is added.
    /// </summary>
    public void ToggleOutfitInCategory(List<OutfitCategory> categories, string categoryId, string outfitName)
    {
        OutfitCategory? category = categories.FirstOrDefault(c => c.Id == categoryId);

        if (category is null)
            return;

        if (category.OutfitNames.Contains(outfitName))
            category.OutfitNames.Remove(outfitName);
        else
            category.OutfitNames.Add(outfitName);

        SaveCategories(categories);
    }

    /// <summary>Return the display names of categories that contain the given outfit.</summary>
    public IEnumerable<string> GetCategoriesForOutfit(List<OutfitCategory> categories, string outfitName)
        => categories.Where(c => c.OutfitNames.Contains(outfitName)).Select(c => c.Name);

    private static bool TryLoad(string key, out List<OutfitCategory> categories)
    {
        categories = new List<OutfitCategory>();
        if (!Game1.player.modData.TryGetValue(key, out string? json) || string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            categories = JsonSerializer.Deserialize<List<OutfitCategory>>(json) ?? new List<OutfitCategory>();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

// Data model

/// <summary>A named collection of outfit names.</summary>
internal sealed class OutfitCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> OutfitNames { get; set; } = new();
}
