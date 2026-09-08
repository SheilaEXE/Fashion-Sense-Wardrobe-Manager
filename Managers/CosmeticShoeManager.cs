using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using System.Text.Json;

namespace FashionSenseWardrobeManager;

/// <summary>Stores and applies a visual-only pair of boots for each saved outfit.</summary>
internal sealed class CosmeticShoeManager
{
    private const string ModDataKey = "NatrollEXE.FashionSenseWardrobeManager.CosmeticShoes";

    private readonly IMonitor _monitor;
    private HashSet<string> _reportedInvalidIds = new(StringComparer.OrdinalIgnoreCase);
    private string? _cachedJson;
    private Dictionary<string, string> _cachedAssignments = new(StringComparer.OrdinalIgnoreCase);

    public CosmeticShoeManager(IMonitor monitor)
    {
        _monitor = monitor;
    }

    public string? GetShoeId(string outfitName)
    {
        Dictionary<string, string> assignments = Load();
        return assignments.TryGetValue(outfitName, out string? id) && !string.IsNullOrWhiteSpace(id)
            ? id
            : null;
    }

    public void SetShoe(string outfitName, string? qualifiedItemId)
    {
        Dictionary<string, string> assignments = Load();

        if (string.IsNullOrWhiteSpace(qualifiedItemId))
            assignments.Remove(outfitName);
        else
            assignments[outfitName] = qualifiedItemId;

        Save(assignments);
    }

    public void RenameOutfit(string oldName, string newName)
    {
        Dictionary<string, string> assignments = Load();
        if (!assignments.Remove(oldName, out string? shoeId))
            return;

        assignments[newName] = shoeId;
        Save(assignments);
    }

    public void RemoveOutfits(IEnumerable<string> outfitNames)
    {
        Dictionary<string, string> assignments = Load();
        bool changed = false;

        foreach (string outfitName in outfitNames)
            changed |= assignments.Remove(outfitName);

        if (changed)
            Save(assignments);
    }

    /// <summary>Apply only the shoe color/sprite associated with an outfit, without equipping its boots.</summary>
    public bool ApplyForOutfit(string outfitName)
    {
        string? qualifiedItemId = GetShoeId(outfitName);
        if (qualifiedItemId is null)
            return false;

        return ApplyShoe(qualifiedItemId, outfitName);
    }

    /// <summary>Preview a specific visual-only pair without persisting an outfit assignment.</summary>
    public bool ApplyShoe(string qualifiedItemId, string contextName = "preview")
    {

        try
        {
            if (ItemRegistry.Create(qualifiedItemId, allowNull: true) is not Boots boots)
                throw new InvalidOperationException("The registered item is unavailable or is not boots.");

            string color = boots.GetBootsColorString();
            if (!Game1.player.shoes.Value.Equals(color, StringComparison.Ordinal))
                Game1.player.changeShoeColor(color);
            return true;
        }
        catch (Exception ex)
        {
            if (_reportedInvalidIds.Add(qualifiedItemId))
                _monitor.Log($"Could not apply cosmetic shoes '{qualifiedItemId}' for '{contextName}': {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    public List<Boots> GetAvailableShoes()
    {
        var result = new List<Boots>();

        foreach (string itemId in DataLoader.Boots(Game1.content).Keys)
        {
            try
            {
                if (ItemRegistry.Create($"(B){itemId}", allowNull: true) is Boots boots)
                    result.Add(boots);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Skipping unavailable cosmetic shoes '(B){itemId}': {ex.Message}", LogLevel.Trace);
            }
        }

        return result
            .OrderBy(boots => boots.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(boots => boots.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void ApplyEquippedBootAppearance()
    {
        string color = Game1.player.boots.Value?.GetBootsColorString() ?? "12";
        if (!Game1.player.shoes.Value.Equals(color, StringComparison.Ordinal))
            Game1.player.changeShoeColor(color);
    }

    private Dictionary<string, string> Load()
    {
        string? json = null;
        if (Context.IsWorldReady)
            Game1.player.modData.TryGetValue(ModDataKey, out json);

        if (string.Equals(json, _cachedJson, StringComparison.Ordinal))
            return new Dictionary<string, string>(_cachedAssignments, StringComparer.OrdinalIgnoreCase);

        _cachedJson = json;
        _cachedAssignments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(_cachedAssignments, StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            Dictionary<string, string>? stored = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            _cachedAssignments = stored is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(stored, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _cachedAssignments.Clear();
        }

        return new Dictionary<string, string>(_cachedAssignments, StringComparer.OrdinalIgnoreCase);
    }

    private void Save(Dictionary<string, string> assignments)
    {
        if (!Context.IsWorldReady)
            return;

        if (assignments.Count == 0)
        {
            Game1.player.modData.Remove(ModDataKey);
            _cachedJson = null;
            _cachedAssignments.Clear();
        }
        else
        {
            string json = JsonSerializer.Serialize(assignments);
            Game1.player.modData[ModDataKey] = json;
            _cachedJson = json;
            _cachedAssignments = new Dictionary<string, string>(assignments, StringComparer.OrdinalIgnoreCase);
        }
    }
}
