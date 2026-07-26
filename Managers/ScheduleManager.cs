using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FashionSenseWardrobeManager;

internal sealed class ScheduleManager
{
    private const string ModDataKey = "FashionSenseWardrobeManager.Schedules";
    private const string LegacyModDataKey = "FashionSenseOutfitPreview.Schedules";

    public List<OutfitScheduleRule> LoadRules()
    {
        if (TryLoad(ModDataKey, out List<OutfitScheduleRule> rules))
            return rules;

        if (TryLoad(LegacyModDataKey, out rules))
        {
            SaveRules(rules);
            return rules;
        }

        return new();
    }

    public void SaveRules(List<OutfitScheduleRule> rules)
    {
        foreach (OutfitScheduleRule rule in rules)
            Normalize(rule);

        Game1.player.modData[ModDataKey] = JsonSerializer.Serialize(rules);
    }

    public void RenameOutfit(string oldName, string newName)
    {
        List<OutfitScheduleRule> rules = LoadRules();
        bool changed = false;

        foreach (OutfitScheduleRule rule in rules)
        {
            for (int i = 0; i < rule.OutfitNames.Count; i++)
            {
                if (!rule.OutfitNames[i].Equals(oldName, StringComparison.OrdinalIgnoreCase))
                    continue;

                rule.OutfitNames[i] = newName;
                changed = true;
            }

            if (rule.LastOutfitName.Equals(oldName, StringComparison.OrdinalIgnoreCase))
            {
                rule.LastOutfitName = newName;
                changed = true;
            }

            if (rule.LockedOutfitName.Equals(oldName, StringComparison.OrdinalIgnoreCase))
            {
                rule.LockedOutfitName = newName;
                changed = true;
            }
        }

        if (changed)
            SaveRules(rules);
    }

    public void RemoveOutfits(IEnumerable<string> outfitNames)
    {
        HashSet<string> removed = new(outfitNames, StringComparer.OrdinalIgnoreCase);
        List<OutfitScheduleRule> rules = LoadRules();
        bool changed = false;

        foreach (OutfitScheduleRule rule in rules)
        {
            changed |= rule.OutfitNames.RemoveAll(removed.Contains) > 0;
            if (removed.Contains(rule.LastOutfitName))
            {
                rule.LastOutfitName = string.Empty;
                changed = true;
            }
            if (removed.Contains(rule.LockedOutfitName))
            {
                rule.LockedOutfitName = string.Empty;
                rule.LockedDayKey = string.Empty;
                changed = true;
            }
        }

        if (changed)
            SaveRules(rules);
    }

    private static void Normalize(OutfitScheduleRule rule)
    {
        rule.Id = string.IsNullOrWhiteSpace(rule.Id) ? Guid.NewGuid().ToString("N") : rule.Id;
        rule.Name = (rule.Name ?? string.Empty).Trim();
        if (rule.Name.Length > 32)
            rule.Name = rule.Name[..32];
        rule.LastOutfitName = (rule.LastOutfitName ?? string.Empty).Trim();
        rule.LockedOutfitName = (rule.LockedOutfitName ?? string.Empty).Trim();
        rule.LockedDayKey = (rule.LockedDayKey ?? string.Empty).Trim();
        if (rule.ChangeFrequency is not ScheduleChangeFrequency.OncePerDay
            and not ScheduleChangeFrequency.EveryActivation)
        {
            rule.ChangeFrequency = ScheduleChangeFrequency.OncePerDay;
        }
        if (rule.ChangeFrequency == ScheduleChangeFrequency.EveryActivation)
        {
            rule.LockedOutfitName = string.Empty;
            rule.LockedDayKey = string.Empty;
        }
        rule.SingleDay = Math.Clamp(rule.SingleDay, 1, 28);
        rule.Days = rule.Days
            .Where(day => day is >= 1 and <= 28)
            .Distinct()
            .OrderBy(day => day)
            .ToList();
        rule.Times = rule.Times
            .Where(IsValidTime)
            .Distinct()
            .OrderBy(time => time)
            .ToList();
        rule.WeatherIds ??= new();
        if (rule.WeatherIds.Count == 0 && rule.Weather != ScheduleWeather.Any)
        {
            rule.WeatherIds.Add(rule.Weather switch
            {
                ScheduleWeather.Sun => ScheduleConditionIds.Sunny,
                ScheduleWeather.Rain => "Rain",
                ScheduleWeather.Storm => "Storm",
                ScheduleWeather.Snow => "Snow",
                ScheduleWeather.GreenRain => "GreenRain",
                _ => string.Empty
            });
        }
        rule.WeatherIds = NormalizeIds(rule.WeatherIds);
        rule.Weather = ScheduleWeather.Any;

        rule.LocationIds ??= new();
        if (rule.LocationIds.Count == 0 && rule.Location != ScheduleLocation.Any)
        {
            rule.LocationIds.Add(rule.Location switch
            {
                ScheduleLocation.FarmHouse => ScheduleConditionIds.FarmHouse,
                ScheduleLocation.Indoors => ScheduleConditionIds.Indoors,
                ScheduleLocation.Outdoors => ScheduleConditionIds.Outdoors,
                _ => string.Empty
            });
        }
        rule.LocationIds = NormalizeIds(rule.LocationIds);
        rule.Location = ScheduleLocation.Any;

        rule.FestivalIds ??= new();
        rule.FestivalIds = NormalizeIds(rule.FestivalIds);

        rule.OutfitNames = rule.OutfitNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        rule.TagIds ??= new();
        rule.TagIds = NormalizeIds(rule.TagIds);
    }

    private static List<string> NormalizeIds(IEnumerable<string> ids)
        => ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool TryLoad(string key, out List<OutfitScheduleRule> rules)
    {
        rules = new();
        if (!Game1.player.modData.TryGetValue(key, out string? json)
            || string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            rules = JsonSerializer.Deserialize<List<OutfitScheduleRule>>(json) ?? new();
            foreach (OutfitScheduleRule rule in rules)
                Normalize(rule);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidTime(int time)
    {
        int hour = time / 100;
        int minute = time % 100;
        return hour is >= 6 and <= 26 && minute is >= 0 and < 60 && minute % 10 == 0;
    }
}
