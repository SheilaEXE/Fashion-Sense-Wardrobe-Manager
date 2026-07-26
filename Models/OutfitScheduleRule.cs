using System;
using System.Collections.Generic;

namespace FashionSenseWardrobeManager;

internal enum ScheduleSection
{
    Spring,
    Summer,
    Fall,
    Winter,
    Festivals,
    Daily
}

internal enum ScheduleDayMode
{
    All,
    Single,
    Multiple
}

internal enum ScheduleWeather
{
    Any,
    Sun,
    Rain,
    Storm,
    Snow,
    GreenRain
}

internal enum SchedulePeriod
{
    Any,
    Morning,
    Afternoon,
    Night
}

internal enum ScheduleLocation
{
    Any,
    FarmHouse,
    Indoors,
    Outdoors
}

internal enum ScheduleChangeFrequency
{
    OncePerDay,
    EveryActivation
}

internal sealed class OutfitScheduleRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public ScheduleSection Section { get; set; } = ScheduleSection.Spring;
    public bool Enabled { get; set; } = true;
    public ScheduleDayMode DayMode { get; set; } = ScheduleDayMode.All;
    public int SingleDay { get; set; } = 1;
    public List<int> Days { get; set; } = new();
    // Kept for migrating schedules created before dynamic weather selection.
    public ScheduleWeather Weather { get; set; } = ScheduleWeather.Any;
    public List<string> WeatherIds { get; set; } = new();
    public SchedulePeriod Period { get; set; } = SchedulePeriod.Any;
    public List<int> Times { get; set; } = new();
    // Kept for migrating schedules created before dynamic location selection.
    public ScheduleLocation Location { get; set; } = ScheduleLocation.Any;
    public List<string> LocationIds { get; set; } = new();
    public List<string> FestivalIds { get; set; } = new();
    public List<string> OutfitNames { get; set; } = new();
    public List<string> TagIds { get; set; } = new();
    public ScheduleChangeFrequency ChangeFrequency { get; set; } = ScheduleChangeFrequency.OncePerDay;
    public string LastOutfitName { get; set; } = string.Empty;
    public string LockedOutfitName { get; set; } = string.Empty;
    public string LockedDayKey { get; set; } = string.Empty;
}
