using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FashionSenseWardrobeManager;

internal sealed record ScheduleConditionOption(string Id, string Label);

internal static class ScheduleConditionIds
{
    public const string ActiveFestivalPrefix = "$festival:active:";
    public const string PassiveFestivalPrefix = "$festival:passive:";
    public const string Sunny = "$weather:Sunny";
    public const string FarmHouse = "$location:FarmHouse";
    public const string Indoors = "$location:Indoors";
    public const string Outdoors = "$location:Outdoors";
    public const string GingerIsland = "$location:GingerIsland";
    public const string Mines = "$location:Mines";

    public static string ActiveFestival(string dateId) => ActiveFestivalPrefix + dateId;
    public static string PassiveFestival(string festivalId) => PassiveFestivalPrefix + festivalId;
}

internal sealed class ScheduleConditionCatalog
{
    private readonly IModRegistry _modRegistry;

    public ScheduleConditionCatalog(IModRegistry modRegistry)
    {
        _modRegistry = modRegistry;
    }

    public List<ScheduleConditionOption> GetWeatherOptions(IEnumerable<string>? selectedIds = null)
    {
        List<ScheduleConditionOption> options = new()
        {
            new(ScheduleConditionIds.Sunny, I18n.ScheduleSun),
            new("Rain", I18n.ScheduleRain),
            new("Storm", I18n.ScheduleStorm),
            new("Snow", I18n.ScheduleSnow),
            new("GreenRain", I18n.ScheduleGreenRain),
            new("Wind", I18n.ScheduleWind)
        };

        if (IsWeatherWondersLoaded())
        {
            options.AddRange(new[]
            {
                new ScheduleConditionOption("Kana.WeatherWonders_AcidRain", I18n.ScheduleWeatherAcidRain),
                new ScheduleConditionOption("Kana.WeatherWonders_Blizzard", I18n.ScheduleWeatherBlizzard),
                new ScheduleConditionOption("Kana.WeatherWonders_Cloudy", I18n.ScheduleWeatherCloudy),
                new ScheduleConditionOption("Kana.WeatherWonders_Deluge", I18n.ScheduleWeatherDeluge),
                new ScheduleConditionOption("Kana.WeatherWonders_Drizzle", I18n.ScheduleWeatherDrizzle),
                new ScheduleConditionOption("Kana.WeatherWonders_DryLightning", I18n.ScheduleWeatherDryLightning),
                new ScheduleConditionOption("Kana.WeatherWonders_Hailstorm", I18n.ScheduleWeatherHailstorm),
                new ScheduleConditionOption("Kana.WeatherWonders_Heatwave", I18n.ScheduleWeatherHeatwave),
                new ScheduleConditionOption("Kana.WeatherWonders_Mist", I18n.ScheduleWeatherMist),
                new ScheduleConditionOption("Kana.WeatherWonders_MuddyRain", I18n.ScheduleWeatherMuddyRain),
                new ScheduleConditionOption("Kana.WeatherWonders_RainSnowMix", I18n.ScheduleWeatherRainSnowMix),
                new ScheduleConditionOption("Kana.WeatherWonders_Sandstorm", I18n.ScheduleWeatherSandstorm)
            });
        }

        string? currentWeather = Game1.player?.currentLocation?.GetWeather()?.Weather;
        AddUnknownOption(options, currentWeather);
        foreach (string selected in selectedIds ?? Enumerable.Empty<string>())
            AddUnknownOption(options, selected);

        return options;
    }

    public List<ScheduleConditionOption> GetLocationOptions(IEnumerable<string>? selectedIds = null)
    {
        return new List<ScheduleConditionOption>
        {
            new(ScheduleConditionIds.FarmHouse, I18n.ScheduleFarmHouse),
            new(ScheduleConditionIds.Indoors, I18n.ScheduleIndoors),
            new(ScheduleConditionIds.Outdoors, I18n.ScheduleOutdoors),
            new("Farm", I18n.ScheduleLocationFarm),
            new("Town", I18n.ScheduleLocationTown),
            new("Beach", I18n.ScheduleLocationBeach),
            new("Forest", I18n.ScheduleLocationForest),
            new("Mountain", I18n.ScheduleLocationMountainQuarry),
            new("BusStop", I18n.ScheduleLocationBusStop),
            new("Desert", I18n.ScheduleLocationDesert),
            new(ScheduleConditionIds.GingerIsland, I18n.ScheduleLocationGingerIsland),
            new("Woods", I18n.ScheduleLocationSecretWoods),
            new("Railroad", I18n.ScheduleLocationRailroad),
            new(ScheduleConditionIds.Mines, I18n.ScheduleLocationMines),
            new("Sewer", I18n.ScheduleLocationSewer)
        };
    }

    public List<ScheduleConditionOption> GetFestivalOptions(
        bool includeCustom,
        IEnumerable<string>? selectedIds = null)
    {
        List<ScheduleConditionOption> options = new()
        {
            new(ScheduleConditionIds.ActiveFestival("spring13"), I18n.ScheduleFestivalEgg),
            new(ScheduleConditionIds.PassiveFestival("DesertFestival"), I18n.ScheduleFestivalDesert),
            new(ScheduleConditionIds.ActiveFestival("spring24"), I18n.ScheduleFestivalFlowerDance),
            new(ScheduleConditionIds.ActiveFestival("summer11"), I18n.ScheduleFestivalLuau),
            new(ScheduleConditionIds.PassiveFestival("TroutDerby"), I18n.ScheduleFestivalTroutDerby),
            new(ScheduleConditionIds.ActiveFestival("summer28"), I18n.ScheduleFestivalMoonlightJellies),
            new(ScheduleConditionIds.ActiveFestival("fall16"), I18n.ScheduleFestivalFair),
            new(ScheduleConditionIds.ActiveFestival("fall27"), I18n.ScheduleFestivalSpiritsEve),
            new(ScheduleConditionIds.ActiveFestival("winter8"), I18n.ScheduleFestivalIce),
            new(ScheduleConditionIds.PassiveFestival("SquidFest"), I18n.ScheduleFestivalSquidFest),
            new(ScheduleConditionIds.PassiveFestival("NightMarket"), I18n.ScheduleFestivalNightMarket),
            new(ScheduleConditionIds.ActiveFestival("winter25"), I18n.ScheduleFestivalWinterStar)
        };

        if (includeCustom)
        {
            AddCustomActiveFestivals(options);
            AddCustomPassiveFestivals(options);
        }

        foreach (string selected in selectedIds ?? Enumerable.Empty<string>())
        {
            if (!options.Any(option => option.Id.Equals(selected, StringComparison.OrdinalIgnoreCase)))
                options.Add(new ScheduleConditionOption(selected, HumanizeFestivalId(selected)));
        }

        return options;
    }

    public string GetWeatherLabel(string id)
        => GetWeatherOptions(new[] { id }).FirstOrDefault(option => option.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Label
            ?? Humanize(id);

    public string GetLocationLabel(string id)
        => GetLocationOptions(new[] { id }).FirstOrDefault(option => option.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Label
            ?? Humanize(id);

    public bool IsSupportedLocationId(string id)
        => GetLocationOptions().Any(option => option.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public string GetFestivalLabel(string id)
        => GetFestivalOptions(includeCustom: true, new[] { id })
            .FirstOrDefault(option => option.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Label
            ?? HumanizeFestivalId(id);

    private static void AddCustomActiveFestivals(List<ScheduleConditionOption> options)
    {
        try
        {
            Dictionary<string, string> dates = Game1.content.Load<Dictionary<string, string>>(
                "Data/Festivals/FestivalDates");
            foreach ((string key, string value) in dates)
            {
                string? date = LooksLikeFestivalDate(key) ? key
                    : LooksLikeFestivalDate(value) ? value
                    : null;
                if (date is null)
                    continue;

                string id = ScheduleConditionIds.ActiveFestival(date);
                if (options.Any(option => option.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                string label = date.Equals(key, StringComparison.OrdinalIgnoreCase) ? value : key;
                options.Add(new ScheduleConditionOption(id,
                    string.IsNullOrWhiteSpace(label) ? Humanize(date) : label));
            }
        }
        catch
        {
            // A malformed custom festival shouldn't prevent the vanilla list opening.
        }
    }

    private static void AddCustomPassiveFestivals(List<ScheduleConditionOption> options)
    {
        try
        {
            foreach (string festivalId in DataLoader.PassiveFestivals(Game1.content).Keys)
            {
                string id = ScheduleConditionIds.PassiveFestival(festivalId);
                if (!options.Any(option => option.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                    options.Add(new ScheduleConditionOption(id, Humanize(festivalId)));
            }
        }
        catch
        {
            // A malformed custom passive festival shouldn't hide the vanilla list.
        }
    }

    private static bool LooksLikeFestivalDate(string value)
    {
        string lower = value.ToLowerInvariant();
        string? season = new[] { "spring", "summer", "fall", "winter" }
            .FirstOrDefault(lower.StartsWith);
        return season is not null
            && int.TryParse(lower[season.Length..], out int day)
            && day is >= 1 and <= 28;
    }

    private static string HumanizeFestivalId(string id)
    {
        if (id.StartsWith(ScheduleConditionIds.ActiveFestivalPrefix, StringComparison.OrdinalIgnoreCase))
            id = id[ScheduleConditionIds.ActiveFestivalPrefix.Length..];
        else if (id.StartsWith(ScheduleConditionIds.PassiveFestivalPrefix, StringComparison.OrdinalIgnoreCase))
            id = id[ScheduleConditionIds.PassiveFestivalPrefix.Length..];
        return Humanize(id);
    }

    private bool IsWeatherWondersLoaded()
        => _modRegistry.GetAll().Any(mod =>
            mod.Manifest.UniqueID.Contains("WeatherWonders", StringComparison.OrdinalIgnoreCase)
            || mod.Manifest.Name.Contains("Weather Wonders", StringComparison.OrdinalIgnoreCase));

    private static void AddUnknownOption(List<ScheduleConditionOption> options, string? id)
    {
        if (string.IsNullOrWhiteSpace(id)
            || id.Equals("Debris", StringComparison.OrdinalIgnoreCase)
            || options.Any(option => option.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        options.Add(new ScheduleConditionOption(id, Humanize(id)));
    }

    internal static string Humanize(string id)
    {
        string value = id.Contains('.') ? id[(id.LastIndexOf('.') + 1)..] : id;
        value = value.Replace('_', ' ').Replace('-', ' ');

        StringBuilder result = new();
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (i > 0 && char.IsUpper(current) && char.IsLower(value[i - 1]))
                result.Append(' ');
            result.Append(current);
        }

        return result.ToString().Trim();
    }
}
