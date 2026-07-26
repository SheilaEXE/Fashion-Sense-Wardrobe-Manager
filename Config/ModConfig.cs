using StardewModdingAPI;

namespace FashionSenseWardrobeManager;

internal sealed class ModConfig
{
    public SButton PreviewShortcut       { get; set; } = SButton.LeftControl;
    public SButton ExpandedMenuShortcut  { get; set; } = SButton.F3;
    public bool    OpenExpandedByDefault { get; set; } = false;
}
