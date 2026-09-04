# Fashion Sense - Wardrobe Manager

Preview, organize, filter, and automatically schedule your saved Fashion Sense outfits entirely in-game.

[![Nexus Mods](https://img.shields.io/badge/Nexus%20Mods-Wardrobe%20Manager-orange)](https://www.nexusmods.com/stardewvalley/mods/45911)
[![Version](https://img.shields.io/badge/version-1.0.8-blue)](https://www.nexusmods.com/stardewvalley/mods/45911)

## About

Fashion Sense - Wardrobe Manager is a complete wardrobe companion for players with large Fashion Sense outfit collections. It expands the saved-outfit menu with live previews, organization tools, advanced filters, and a visual scheduler that can equip outfits automatically as your farmer goes through the day and year.

The mod was originally released as **Fashion Sense Outfit Preview**. Its name and technical UniqueID changed as it grew beyond previews into a full wardrobe-management tool. Existing categories, tags, and schedules are migrated automatically from the previous save-data keys.

## Main Features

### Preview and manage outfits

- Browse saved outfits in an expanded window with a live farmer preview.
- Quickly preview an outfit with the configured shortcut plus left-click.
- Search saved outfits by name.
- Save, rename, and delete Fashion Sense outfits from the expanded interface.

### Organize your wardrobe

- Create custom outfit categories.
- Add general tags and color tags to saved outfits.
- Combine categories, tags, colors, and text search through advanced filters.
- Keep even very large outfit collections easy to browse.

### Automatic outfit schedules

- Create schedules for spring, summer, fall, winter, festivals, and everyday use.
- Choose all days, one specific day, or multiple days from 1 to 28.
- Filter schedules by weather, including weather types added by compatible mods.
- Select a time period or add multiple exact in-game times.
- Use general and vanilla location conditions, such as the farmhouse, interiors, outdoors, the farm, town, beach, mines, and more.
- Select individual outfits, entire dynamic tags, or combine both in one schedule.
- Choose whether a randomly selected outfit remains fixed for the day or may change whenever the schedule is activated again.
- Give schedules optional custom names.
- Use automatic priority: festival schedules override seasonal schedules, and seasonal schedules override everyday schedules.
- See whether a schedule is active now, waiting for a higher-priority rule, enabled, or manually disabled.

## Installation

1. Install [SMAPI](https://smapi.io/).
2. Install [Fashion Sense](https://www.nexusmods.com/stardewvalley/mods/9969).
3. Download Fashion Sense - Wardrobe Manager.
4. Extract the mod folder into your Stardew Valley `Mods` folder.
5. Launch the game through SMAPI.

### Updating from 1.0.6 or earlier

Delete the entire old **Fashion Sense Outfit Preview** folder before installing this version. Then extract the new **Fashion Sense Wardrobe Manager** folder into `Mods`. Do not merge the folders or keep both copies: the old and new releases have different UniqueIDs and SMAPI may load both at the same time.

## How to Use

- Open the saved outfits menu in Fashion Sense.
- Use the preview shortcut plus left-click for a quick preview.
- Click **Expand** to open the complete wardrobe manager.
- Use the controls above the outfit list to browse categories, tags, colors, and advanced filters.
- Use the seasonal and schedule tabs on the left side of the window to create automatic outfit rules without editing JSON files.

> The configured quick-preview shortcut is always used together with the left mouse button.

## Requirements

- [SMAPI](https://smapi.io/)
- [Fashion Sense](https://www.nexusmods.com/stardewvalley/mods/9969)
- [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional)

## Compatibility Notes

- Weather conditions added by other mods can be used by the scheduler when exposed through Stardew Valley's weather data.
- Festivals added by other mods can optionally be displayed in the festival selector.
- The technical UniqueID is `NatrollEXE.FashionSenseWardrobeManager`. Categories, tags, and schedules created under the previous `NatrollEXE.FashionSenseOutfitPreview` identity are migrated automatically when each save is loaded.

## Links

- [Nexus Mods page](https://www.nexusmods.com/stardewvalley/mods/45911)
- [Fashion Sense](https://www.nexusmods.com/stardewvalley/mods/9969)
- [Source code](https://github.com/SheilaEXE/Fashion-Sense-Wardrobe-Manager)
