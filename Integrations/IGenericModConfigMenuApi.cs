using StardewModdingAPI;
using System;

namespace FashionSenseWardrobeManager;

public interface IGenericModConfigMenuApi
{
    void Register(
        IManifest mod,
        Action reset,
        Action save,
        bool titleScreenOnly = false
    );

    void AddKeybind(
        IManifest mod,
        Func<SButton> getValue,
        Action<SButton> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null
    );

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null
    );
}
