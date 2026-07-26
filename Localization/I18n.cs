using StardewModdingAPI;

namespace FashionSenseWardrobeManager;

/// <summary>
/// Thin wrapper around SMAPI's ITranslationHelper.
/// Call I18n.Init(helper.Translation) once in ModEntry.Entry(), then use the
/// static properties/methods anywhere in the mod.
/// </summary>
internal static class I18n
{
    private static ITranslationHelper _t = null!;

    public static void Init(ITranslationHelper translation) => _t = translation;

    private static string Get(string key) => _t.Get(key);
    private static string Get(string key, object tokens) => _t.Get(key, tokens);

    // UI
    public static string Title                => Get("ui.title");
    public static string Preview              => Get("ui.preview");
    public static string SearchPlaceholder    => Get("ui.search.placeholder");

    // Buttons
    public static string ButtonExpand         => Get("ui.button.expand");
    public static string ButtonClose          => Get("ui.button.close");
    public static string ButtonEquip          => Get("ui.button.equip");
    public static string ButtonAll            => Get("ui.button.all");
    public static string ButtonCategory       => Get("ui.button.category");
    public static string ButtonTags           => Get("ui.button.tags");
    public static string ButtonColors         => Get("ui.button.colors");
    public static string ButtonCreateCategory => Get("ui.button.create_category");
    public static string ButtonSelect         => Get("ui.button.select");
    public static string ButtonDeleteCategory => Get("ui.button.delete_category");
    public static string ButtonConfirm        => Get("ui.button.confirm");
    public static string ButtonCancel         => Get("ui.button.cancel");
    public static string ButtonDone           => Get("ui.button.done");
    public static string ButtonYes            => Get("ui.button.yes");
    public static string ButtonNo             => Get("ui.button.no");
    public static string ButtonRename         => Get("ui.button.rename");
    public static string ButtonEdit           => Get("ui.button.edit");
    public static string ButtonDeleteTag      => Get("ui.button.delete_tag");
    public static string ButtonDeleteColor    => Get("ui.button.delete_color");
    public static string ButtonDeleteOutfits  => Get("ui.button.delete_outfits");
    public static string ButtonNone           => Get("ui.button.none");

    // Modal
    public static string ModalNewCategory     => Get("ui.modal.new_category");
    public static string ModalDefaultCategory => Get("ui.modal.default_category");

    // Assign mode
    public static string AssignInstruction(string categoryName)
        => Get("ui.assign.instruction", new { categoryName });

    // Confirm modals
    public static string ConfirmDeleteCategoryTitle => Get("ui.confirm.delete_category.title");
    public static string ConfirmDeleteCategoryMsg   => Get("ui.confirm.delete_category.msg");
    public static string ConfirmRemoveOutfitsTitle  => Get("ui.confirm.remove_outfits.title");
    public static string ConfirmRemoveOutfitsMsg(int count)
        => Get("ui.confirm.remove_outfits.msg", new { count });
    public static string ConfirmDeleteSavedOutfitsTitle => Get("ui.confirm.delete_saved_outfits.title");
    public static string ConfirmDeleteSavedOutfitsMsg(int count)
        => Get("ui.confirm.delete_saved_outfits.msg", new { count });
    public static string ConfirmDeleteTagTitle      => Get("ui.confirm.delete_tag.title");
    public static string ConfirmDeleteTagMsg        => Get("ui.confirm.delete_tag.msg");
    public static string ConfirmDeleteColorTitle    => Get("ui.confirm.delete_color.title");
    public static string ConfirmDeleteColorMsg      => Get("ui.confirm.delete_color.msg");

    // Empty states
    public static string EmptyCategory        => Get("ui.empty.category");
    public static string EmptySearch          => Get("ui.empty.search");

    // Errors
    public static string ErrorSelectFirst     => Get("ui.error.select_first");
    public static string ErrorNameEmpty       => Get("ui.error.name_empty");
    public static string ErrorNameTaken       => Get("ui.error.name_taken");
    public static string ErrorPresetsUnsupported => Get("ui.error.presets_unsupported");

    // Config
    public static string ConfigShortcutName        => Get("config.shortcut.name");
    public static string ConfigShortcutTooltip     => Get("config.shortcut.tooltip");
    public static string ConfigExpandShortcutName  => Get("config.expand_shortcut.name");
    public static string ConfigExpandShortcutTooltip => Get("config.expand_shortcut.tooltip");
    public static string ConfigExpandDefaultName    => Get("config.expand_default.name");
    public static string ConfigExpandDefaultTooltip => Get("config.expand_default.tooltip");

    // Tags
    public static string ButtonCreate         => Get("ui.button.create");
    public static string ButtonSaveCurrentStyle => Get("ui.button.save_current_style");
    public static string ButtonNewCategory    => Get("ui.button.new_category");
    public static string ButtonNewTag         => Get("ui.button.new_tag");
    public static string ButtonNewColorTag    => Get("ui.button.new_color_tag");
    public static string ButtonExportOrganization => Get("ui.button.export_organization");
    public static string ButtonImportOrganization => Get("ui.button.import_organization");
    public static string ModalNewTag          => Get("ui.modal.new_tag");
    public static string ModalNewColorTag     => Get("ui.modal.new_color_tag");
    public static string ModalSaveCurrentStyle => Get("ui.modal.save_current_style");
    public static string ModalRenameOutfit    => Get("ui.modal.rename_outfit");
    public static string ErrorTagNameEmpty    => Get("ui.error.tag_name_empty");
    public static string ErrorTagNameTaken    => Get("ui.error.tag_name_taken");
    public static string ErrorSaveOutfitFailed => Get("ui.error.save_outfit_failed");
    public static string ErrorRenameOutfitFailed => Get("ui.error.rename_outfit_failed");
    public static string ErrorOrganizationImportMissing => Get("ui.error.organization_import_missing");
    public static string MessageOrganizationExported => Get("ui.message.organization_exported");
    public static string MessageOrganizationImported => Get("ui.message.organization_imported");
    public static string AssignTagInstruction(string tagName)
        => Get("ui.assign.tag_instruction", new { tagName });

    // Advanced filter panel
    public static string ButtonAdvanced       => Get("ui.button.advanced");
    public static string ButtonClearFilters   => Get("ui.button.clear_filters");
    public static string SectionCategory      => Get("ui.section.category");
    public static string SectionTags          => Get("ui.section.tags");
    public static string SectionColors        => Get("ui.section.colors");

    // Outfit schedules
    public static string ScheduleTitle(string section) => Get("ui.schedule.title", new { section });
    public static string ScheduleRuleNumber(int number) => Get("ui.schedule.rule_number", new { number });
    public static string ScheduleDayNumber(int day) => Get("ui.schedule.day_number", new { day });
    public static string ScheduleSpring       => Get("ui.schedule.spring");
    public static string ScheduleSummer       => Get("ui.schedule.summer");
    public static string ScheduleFall         => Get("ui.schedule.fall");
    public static string ScheduleWinter       => Get("ui.schedule.winter");
    public static string ScheduleFestivals    => Get("ui.schedule.festivals");
    public static string ScheduleDaily        => Get("ui.schedule.daily");
    public static string ScheduleAddRule      => Get("ui.schedule.add_rule");
    public static string ScheduleEmpty        => Get("ui.schedule.empty");
    public static string ScheduleEnabled      => Get("ui.schedule.enabled");
    public static string ScheduleDisabled     => Get("ui.schedule.disabled");
    public static string ScheduleDelete       => Get("ui.schedule.delete");
    public static string ScheduleDays         => Get("ui.schedule.days");
    public static string ScheduleAllDays      => Get("ui.schedule.all_days");
    public static string ScheduleOneDay       => Get("ui.schedule.one_day");
    public static string ScheduleMultipleDays => Get("ui.schedule.multiple_days");
    public static string ScheduleWeather      => Get("ui.schedule.weather");
    public static string ScheduleAny          => Get("ui.schedule.any");
    public static string ScheduleSun          => Get("ui.schedule.sun");
    public static string ScheduleRain         => Get("ui.schedule.rain");
    public static string ScheduleStorm        => Get("ui.schedule.storm");
    public static string ScheduleSnow         => Get("ui.schedule.snow");
    public static string ScheduleGreenRain    => Get("ui.schedule.green_rain");
    public static string ScheduleWind         => Get("ui.schedule.wind");
    public static string ScheduleSelectWeather => Get("ui.schedule.select_weather");
    public static string ScheduleSelectLocations => Get("ui.schedule.select_locations");
    public static string ScheduleSelectedCount(int count) => Get("ui.schedule.selected_count", new { count });
    public static string ScheduleWeatherAcidRain => Get("ui.schedule.weather.acid_rain");
    public static string ScheduleWeatherBlizzard => Get("ui.schedule.weather.blizzard");
    public static string ScheduleWeatherCloudy => Get("ui.schedule.weather.cloudy");
    public static string ScheduleWeatherDeluge => Get("ui.schedule.weather.deluge");
    public static string ScheduleWeatherDrizzle => Get("ui.schedule.weather.drizzle");
    public static string ScheduleWeatherDryLightning => Get("ui.schedule.weather.dry_lightning");
    public static string ScheduleWeatherHailstorm => Get("ui.schedule.weather.hailstorm");
    public static string ScheduleWeatherHeatwave => Get("ui.schedule.weather.heatwave");
    public static string ScheduleWeatherMist => Get("ui.schedule.weather.mist");
    public static string ScheduleWeatherMuddyRain => Get("ui.schedule.weather.muddy_rain");
    public static string ScheduleWeatherRainSnowMix => Get("ui.schedule.weather.rain_snow_mix");
    public static string ScheduleWeatherSandstorm => Get("ui.schedule.weather.sandstorm");
    public static string SchedulePeriod       => Get("ui.schedule.period");
    public static string ScheduleMorning      => Get("ui.schedule.morning");
    public static string ScheduleAfternoon    => Get("ui.schedule.afternoon");
    public static string ScheduleNight        => Get("ui.schedule.night");
    public static string ScheduleLocation     => Get("ui.schedule.location");
    public static string ScheduleFarmHouse    => Get("ui.schedule.farmhouse");
    public static string ScheduleIndoors      => Get("ui.schedule.indoors");
    public static string ScheduleOutdoors     => Get("ui.schedule.outdoors");
    public static string ScheduleLocationFarm => Get("ui.schedule.location.farm");
    public static string ScheduleLocationTown => Get("ui.schedule.location.town");
    public static string ScheduleLocationBeach => Get("ui.schedule.location.beach");
    public static string ScheduleLocationForest => Get("ui.schedule.location.forest");
    public static string ScheduleLocationMountainQuarry => Get("ui.schedule.location.mountain_quarry");
    public static string ScheduleLocationBusStop => Get("ui.schedule.location.bus_stop");
    public static string ScheduleLocationDesert => Get("ui.schedule.location.desert");
    public static string ScheduleLocationGingerIsland => Get("ui.schedule.location.ginger_island");
    public static string ScheduleLocationSecretWoods => Get("ui.schedule.location.secret_woods");
    public static string ScheduleLocationRailroad => Get("ui.schedule.location.railroad");
    public static string ScheduleLocationMines => Get("ui.schedule.location.mines");
    public static string ScheduleLocationSewer => Get("ui.schedule.location.sewer");
    public static string ScheduleFestival      => Get("ui.schedule.festival");
    public static string ScheduleSelectFestivals => Get("ui.schedule.select_festivals");
    public static string ScheduleShowModFestivals => Get("ui.schedule.show_mod_festivals");
    public static string ScheduleHideModFestivals => Get("ui.schedule.hide_mod_festivals");
    public static string ScheduleFestivalEgg => Get("ui.schedule.festival.egg");
    public static string ScheduleFestivalDesert => Get("ui.schedule.festival.desert");
    public static string ScheduleFestivalFlowerDance => Get("ui.schedule.festival.flower_dance");
    public static string ScheduleFestivalLuau => Get("ui.schedule.festival.luau");
    public static string ScheduleFestivalTroutDerby => Get("ui.schedule.festival.trout_derby");
    public static string ScheduleFestivalMoonlightJellies => Get("ui.schedule.festival.moonlight_jellies");
    public static string ScheduleFestivalFair => Get("ui.schedule.festival.fair");
    public static string ScheduleFestivalSpiritsEve => Get("ui.schedule.festival.spirits_eve");
    public static string ScheduleFestivalIce => Get("ui.schedule.festival.ice");
    public static string ScheduleFestivalSquidFest => Get("ui.schedule.festival.squid_fest");
    public static string ScheduleFestivalNightMarket => Get("ui.schedule.festival.night_market");
    public static string ScheduleFestivalWinterStar => Get("ui.schedule.festival.winter_star");
    public static string ScheduleExactTimes   => Get("ui.schedule.exact_times");
    public static string ScheduleAddTime      => Get("ui.schedule.add_time");
    public static string ScheduleSelectOutfits => Get("ui.schedule.select_outfits");
    public static string ScheduleNoOutfits    => Get("ui.schedule.no_outfits");
    public static string ScheduleOutfitsShort => Get("ui.schedule.outfits_short");
    public static string ScheduleTagsShort    => Get("ui.schedule.tags_short");
    public static string ScheduleWholeTag     => Get("ui.schedule.whole_tag");
    public static string ScheduleNameOptional => Get("ui.schedule.name_optional");
    public static string ScheduleNameTitle    => Get("ui.schedule.name_title");
    public static string ScheduleActiveNow    => Get("ui.schedule.active_now");
    public static string ScheduleWaiting      => Get("ui.schedule.waiting");
    public static string ScheduleChangeFrequency => Get("ui.schedule.change_frequency");
    public static string ScheduleFixedForDay => Get("ui.schedule.fixed_for_day");
    public static string ScheduleRandomDuringDay => Get("ui.schedule.random_during_day");
}
