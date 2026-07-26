using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FashionSenseWardrobeManager;

internal sealed class ScheduleEvaluator
{
    private readonly ScheduleManager _manager;
    private readonly TagManager _tagManager;
    private readonly OutfitPreviewRenderer _renderer;
    private readonly IMonitor _monitor;
    private readonly Random _random = new();

    public ScheduleEvaluator(ScheduleManager manager, TagManager tagManager, OutfitPreviewRenderer renderer, IMonitor monitor)
    {
        _manager = manager;
        _tagManager = tagManager;
        _renderer = renderer;
        _monitor = monitor;
    }

    public bool Evaluate(bool exactTimeTrigger = false)
    {
        if (!Context.IsWorldReady || Game1.eventUp || Game1.player?.currentLocation is null)
            return false;

        List<OutfitScheduleRule> rules = _manager.LoadRules();
        OutfitScheduleRule? selectedRule = FindWinningRule(rules, exactTimeTrigger);
        if (selectedRule is null)
            return true;

        HashSet<string> selectedOutfits = new(selectedRule.OutfitNames, StringComparer.OrdinalIgnoreCase);
        if (selectedRule.TagIds.Count > 0)
        {
            HashSet<string> selectedTagIds = new(selectedRule.TagIds, StringComparer.OrdinalIgnoreCase);
            foreach (OutfitTag tag in _tagManager.LoadTags().Where(tag => selectedTagIds.Contains(tag.Id)))
                selectedOutfits.UnionWith(tag.OutfitNames);
        }

        List<string> available = selectedOutfits
            .Where(_renderer.OutfitExists)
            .ToList();

        if (available.Count == 0)
            return false;

        string currentDayKey = GetCurrentDayKey();
        string? lockedOutfit = selectedRule.ChangeFrequency == ScheduleChangeFrequency.OncePerDay
            && selectedRule.LockedDayKey.Equals(currentDayKey, StringComparison.Ordinal)
            ? available.FirstOrDefault(name => name.Equals(selectedRule.LockedOutfitName, StringComparison.OrdinalIgnoreCase))
            : null;

        string outfitName;
        bool newDailySelection = lockedOutfit is null
            && selectedRule.ChangeFrequency == ScheduleChangeFrequency.OncePerDay;
        if (lockedOutfit is not null)
        {
            outfitName = lockedOutfit;
        }
        else
        {
            List<string> choices = available.Count > 1 && !string.IsNullOrWhiteSpace(selectedRule.LastOutfitName)
                ? available.Where(name => !name.Equals(selectedRule.LastOutfitName, StringComparison.OrdinalIgnoreCase)).ToList()
                : available;
            if (choices.Count == 0)
                choices = available;

            outfitName = choices[_random.Next(choices.Count)];
        }

        if (_renderer.EquipOutfitImmediately(outfitName))
        {
            bool selectionStateChanged = false;
            if (!selectedRule.LastOutfitName.Equals(outfitName, StringComparison.OrdinalIgnoreCase))
            {
                selectedRule.LastOutfitName = outfitName;
                selectionStateChanged = true;
            }

            if (newDailySelection)
            {
                selectedRule.LockedOutfitName = outfitName;
                selectedRule.LockedDayKey = currentDayKey;
                selectionStateChanged = true;
            }
            else if (selectedRule.ChangeFrequency == ScheduleChangeFrequency.EveryActivation
                && (!string.IsNullOrEmpty(selectedRule.LockedOutfitName)
                    || !string.IsNullOrEmpty(selectedRule.LockedDayKey)))
            {
                selectedRule.LockedOutfitName = string.Empty;
                selectedRule.LockedDayKey = string.Empty;
                selectionStateChanged = true;
            }

            if (selectionStateChanged)
                _manager.SaveRules(rules);
            _monitor.Log($"Schedule equipped Fashion Sense outfit '{outfitName}'.", LogLevel.Trace);
            return true;
        }

        return false;
    }

    private static string GetCurrentDayKey()
        => $"{Game1.year}:{Game1.currentSeason}:{Game1.dayOfMonth}";

    public string? GetWinningRuleId(IEnumerable<OutfitScheduleRule> rules)
    {
        if (!Context.IsWorldReady || Game1.eventUp || Game1.player?.currentLocation is null)
            return null;

        return FindWinningRule(rules, exactTimeTrigger: false)?.Id;
    }

    public bool IsRuleMatchingNow(OutfitScheduleRule rule)
    {
        if (!Context.IsWorldReady || Game1.eventUp || Game1.player?.currentLocation is null)
            return false;

        return Matches(rule, exactTimeTrigger: false);
    }

    private static OutfitScheduleRule? FindWinningRule(
        IEnumerable<OutfitScheduleRule> rules,
        bool exactTimeTrigger)
        => rules
            .Where(rule => Matches(rule, exactTimeTrigger))
            .OrderByDescending(GetPriority)
            .FirstOrDefault();

    private static bool Matches(OutfitScheduleRule rule, bool exactTimeTrigger)
    {
        if (!rule.Enabled || (rule.OutfitNames.Count == 0 && rule.TagIds.Count == 0))
            return false;

        bool festivalRule = rule.Section == ScheduleSection.Festivals;
        if (!MatchesSection(rule)
            || (!festivalRule && !MatchesDay(rule))
            || (!festivalRule && !MatchesWeather(rule.WeatherIds))
            || !MatchesPeriod(rule.Period)
            || (!festivalRule && !MatchesLocation(rule.LocationIds)))
        {
            return false;
        }

        if (rule.Times.Count == 0)
        {
            if (!exactTimeTrigger)
                return true;

            // A broad rule should only re-evaluate when its period begins, not every
            // ten in-game minutes (which would continuously reroll selected outfits).
            return rule.Period switch
            {
                SchedulePeriod.Morning => Game1.timeOfDay == 600,
                SchedulePeriod.Afternoon => Game1.timeOfDay == 1200,
                SchedulePeriod.Night => Game1.timeOfDay == 1800,
                _ => false
            };
        }

        if (exactTimeTrigger)
            return rule.Times.Contains(Game1.timeOfDay);

        // On a warp or save load, a scheduled time remains eligible after it has passed.
        return rule.Times.Any(time => time <= Game1.timeOfDay);
    }

    private static bool MatchesSection(OutfitScheduleRule rule)
    {
        return rule.Section switch
        {
            ScheduleSection.Spring => Game1.currentSeason == "spring",
            ScheduleSection.Summer => Game1.currentSeason == "summer",
            ScheduleSection.Fall => Game1.currentSeason == "fall",
            ScheduleSection.Winter => Game1.currentSeason == "winter",
            ScheduleSection.Festivals => MatchesFestival(rule.FestivalIds),
            ScheduleSection.Daily => true,
            _ => false
        };
    }

    private static bool MatchesFestival(IReadOnlyCollection<string> festivalIds)
    {
        if (festivalIds.Count > 0)
            return festivalIds.Any(IsFestivalActiveToday);

        if (Utility.isFestivalDay(Game1.dayOfMonth, Game1.season))
            return true;

        try
        {
            return DataLoader.PassiveFestivals(Game1.content).Keys.Any(Utility.IsPassiveFestivalDay);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFestivalActiveToday(string id)
    {
        if (id.StartsWith(ScheduleConditionIds.ActiveFestivalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string date = id[ScheduleConditionIds.ActiveFestivalPrefix.Length..];
            string today = $"{Game1.currentSeason}{Game1.dayOfMonth}";
            return date.Equals(today, StringComparison.OrdinalIgnoreCase)
                && Utility.isFestivalDay(Game1.dayOfMonth, Game1.season);
        }

        if (id.StartsWith(ScheduleConditionIds.PassiveFestivalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string festivalId = id[ScheduleConditionIds.PassiveFestivalPrefix.Length..];
            return Utility.IsPassiveFestivalDay(festivalId);
        }

        return false;
    }

    private static bool MatchesDay(OutfitScheduleRule rule)
    {
        return rule.DayMode switch
        {
            ScheduleDayMode.All => true,
            ScheduleDayMode.Single => rule.SingleDay == Game1.dayOfMonth,
            ScheduleDayMode.Multiple => rule.Days.Contains(Game1.dayOfMonth),
            _ => false
        };
    }

    private static bool MatchesWeather(IReadOnlyCollection<string> weatherIds)
    {
        if (weatherIds.Count == 0)
            return true;

        string current = Game1.player.currentLocation.GetWeather()?.Weather ?? "Sun";
        return weatherIds.Any(id => id switch
        {
            ScheduleConditionIds.Sunny => current.Equals("Sun", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Wind", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Debris", StringComparison.OrdinalIgnoreCase),
            "Wind" => current.Equals("Wind", StringComparison.OrdinalIgnoreCase)
                || current.Equals("Debris", StringComparison.OrdinalIgnoreCase),
            _ => current.Equals(id, StringComparison.OrdinalIgnoreCase)
        });
    }

    private static bool MatchesPeriod(SchedulePeriod period)
    {
        int time = Game1.timeOfDay;
        return period switch
        {
            SchedulePeriod.Any => true,
            SchedulePeriod.Morning => time >= 600 && time < 1200,
            SchedulePeriod.Afternoon => time >= 1200 && time < 1800,
            SchedulePeriod.Night => time >= 1800,
            _ => false
        };
    }

    private static bool MatchesLocation(IReadOnlyCollection<string> locationIds)
    {
        if (locationIds.Count == 0)
            return true;

        GameLocation current = Game1.player.currentLocation;
        return locationIds.Any(id => id switch
        {
            ScheduleConditionIds.FarmHouse => current is FarmHouse,
            ScheduleConditionIds.Indoors => !current.IsOutdoors,
            ScheduleConditionIds.Outdoors => current.IsOutdoors,
            ScheduleConditionIds.GingerIsland => current.InIslandContext(),
            ScheduleConditionIds.Mines => current is MineShaft
                || current.Name.Equals("Mine", StringComparison.OrdinalIgnoreCase),
            _ => current.NameOrUniqueName.Equals(id, StringComparison.OrdinalIgnoreCase)
                || current.Name.Equals(id, StringComparison.OrdinalIgnoreCase)
        });
    }

    private static int GetPriority(OutfitScheduleRule rule)
    {
        int section = rule.Section switch
        {
            ScheduleSection.Festivals => 400,
            ScheduleSection.Daily => 100,
            _ => 300
        };

        int day = rule.Section == ScheduleSection.Festivals ? 0 : rule.DayMode switch
        {
            ScheduleDayMode.Single => 30,
            ScheduleDayMode.Multiple => 20,
            _ => 0
        };

        int conditions = (rule.Section != ScheduleSection.Festivals && rule.WeatherIds.Count > 0 ? 4 : 0)
            + (rule.Period != SchedulePeriod.Any ? 4 : 0)
            + (rule.Section != ScheduleSection.Festivals && rule.LocationIds.Count > 0 ? 4 : 0)
            + (rule.Section == ScheduleSection.Festivals && rule.FestivalIds.Count > 0 ? 6 : 0)
            + (rule.Times.Count > 0 ? 8 : 0);

        return section + day + conditions;
    }
}
