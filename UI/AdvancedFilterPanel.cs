using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using System.Collections.Generic;
using System.Linq;

namespace FashionSenseWardrobeManager;

/// <summary>
/// Manages the advanced filter strip shown above the outfit grid.
/// The strip controls category, normal tags, and color tags while the outfit grid
/// remains visible underneath.
/// </summary>
internal sealed class AdvancedFilterPanel
{
    public const int PanelHeight = 220;
    public const int PanelGap    = 8;

    private const int ItemH      = 34;
    private const int SectionGap = 6;

    // Filter state (public for menu to query)
    public bool   IsOpen           { get; set; } = false;
    public string FilterCategoryId { get; set; } = "all";
    public HashSet<string> FilterTagIds      { get; } = new();
    public HashSet<string> FilterColorTagIds { get; } = new();

    public bool HasActiveFilter
        => FilterCategoryId != "all"
        || FilterTagIds.Count > 0
        || FilterColorTagIds.Count > 0;

    public void ClearFilters()
    {
        FilterCategoryId = "all";
        FilterTagIds.Clear();
        FilterColorTagIds.Clear();
    }

    // Data
    private List<OutfitCategory> _categories = new();
    private List<OutfitTag>      _tags       = new();

    public void SetData(List<OutfitCategory> categories, List<OutfitTag> tags)
    {
        _categories = categories;
        _tags       = tags;
        RebuildItems();
    }

    // Items
    private record FilterItem(string Id, string Label, FilterItemKind Kind, int RelativeY);
    private enum FilterItemKind { Category, Tag, ColorTag, SectionHeader }
    private List<FilterItem> _items = new();

    private int _scrollOffset = 0;
    private Rectangle _panelBounds;
    private Rectangle _scrollUpBtn;
    private Rectangle _scrollDownBtn;
    private Rectangle _clearButton;

    private void RebuildItems()
    {
        _items.Clear();
        int y = 0;

        // Category section
        _items.Add(new("__cat_header__", I18n.SectionCategory, FilterItemKind.SectionHeader, y)); y += ItemH;
        _items.Add(new("all", I18n.ButtonAll, FilterItemKind.Category, y)); y += ItemH;
        foreach (var cat in _categories)
        {
            _items.Add(new(cat.Id, cat.Name, FilterItemKind.Category, y));
            y += ItemH;
        }
        y += SectionGap;

        // Tags section
        var generalTags = _tags.Where(t => t.Kind == TagKind.General).ToList();
        _items.Add(new("__tag_header__", I18n.SectionTags, FilterItemKind.SectionHeader, y)); y += ItemH;
        foreach (var tag in generalTags)
        {
            _items.Add(new(tag.Id, tag.Name, FilterItemKind.Tag, y));
            y += ItemH;
        }
        y += SectionGap;

        // Color tags section
        var colorTags = _tags.Where(t => t.Kind == TagKind.Color).ToList();
        _items.Add(new("__color_header__", I18n.SectionColors, FilterItemKind.SectionHeader, y)); y += ItemH;
        foreach (var tag in colorTags)
        {
            _items.Add(new(tag.Id, tag.Name, FilterItemKind.ColorTag, y));
            y += ItemH;
        }
    }

    /// <summary>
    /// Maximum scroll offset (in item rows) needed so the last item can be
    /// fully scrolled into view. Uses ceiling division — floor division would
    /// leave the bottom item permanently cut off by a few pixels whenever the
    /// total content height isn't an exact multiple of <see cref="ItemH"/>
    /// (which happens often due to the small section gaps).
    /// </summary>
    private int GetMaxScroll(int clipHeight)
    {
        int totalH   = _items.Count > 0 ? _items[^1].RelativeY + ItemH : 0;
        int overflow = totalH - clipHeight;

        if (overflow <= 0)
            return 0;

        return (overflow + ItemH - 1) / ItemH;   // ceiling division
    }

    // Layout
    public void UpdateLayout(Rectangle gridArea)
    {
        _panelBounds = GetPanelBounds(gridArea);
        _scrollUpBtn   = new Rectangle(_panelBounds.Right - 30, _panelBounds.Y + 8,      24, 24);
        _scrollDownBtn = new Rectangle(_panelBounds.Right - 30, _panelBounds.Bottom - 32, 24, 24);

        // "Limpar Filtros" sits above-left of the panel frame, in the space normally
        // occupied by the category bar (which is hidden while the panel is open).
        _clearButton = new Rectangle(_panelBounds.X, _panelBounds.Y - 44, 170, 38);
    }

    public static Rectangle GetPanelBounds(Rectangle gridArea)
        => new(gridArea.X, gridArea.Y, gridArea.Width, PanelHeight);

    // Drawing (called by ExpandedOutfitsMenu above DrawGrid)
    public void Draw(SpriteBatch b, Rectangle gridArea, Rectangle previewPanel)
    {
        UpdateLayout(gridArea);

        IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 396, 15, 15),
            _panelBounds.X, _panelBounds.Y, _panelBounds.Width, _panelBounds.Height,
            Color.White, 4f, drawShadow: false);

        int contentX   = _panelBounds.X + 16;
        int contentW   = _panelBounds.Width - 56;
        int contentTop = _panelBounds.Y + 8;
        int offsetY    = contentTop - _scrollOffset * ItemH;
        int mouseX     = Game1.getMouseX();
        int mouseY     = Game1.getMouseY();

        // Leave a little room for the clear button when visible.
        Rectangle clip = new(_panelBounds.X + 8, _panelBounds.Y + 4, _panelBounds.Width - 16, _panelBounds.Height - 8);

        foreach (var item in _items)
        {
            Rectangle drawn = new(contentX, offsetY + item.RelativeY, contentW, ItemH);

            // Only draw fully visible rows. This prevents text/icons from bleeding
            // outside the filter panel when scrolling.
            if (drawn.Y < clip.Y || drawn.Bottom > clip.Bottom)
                continue;

            if (item.Kind == FilterItemKind.SectionHeader)
            {
                DrawSectionHeader(b, item, drawn);
                continue;
            }

            bool selected = item.Kind switch
            {
                FilterItemKind.Category => item.Id == FilterCategoryId,
                FilterItemKind.Tag      => FilterTagIds.Contains(item.Id),
                FilterItemKind.ColorTag => FilterColorTagIds.Contains(item.Id),
                _                       => false
            };
            bool hovered = clip.Contains(mouseX, mouseY) && drawn.Contains(mouseX, mouseY);

            if (selected)      b.Draw(Game1.staminaRect, drawn, Color.SandyBrown * 0.35f);
            else if (hovered)  b.Draw(Game1.staminaRect, drawn, Color.Wheat * 0.25f);

            // Checkbox
            Rectangle cb = new(drawn.X + 4, drawn.Center.Y - 8, 16, 16);
            b.Draw(Game1.mouseCursors, cb, new Rectangle(227, 425, 9, 9), Color.White, 0f,
                Vector2.Zero, SpriteEffects.None, 0.88f);
            if (selected)
                b.Draw(Game1.mouseCursors, cb, new Rectangle(236, 425, 9, 9), Color.White, 0f,
                    Vector2.Zero, SpriteEffects.None, 0.89f);

            string label = TruncateString(item.Label, Game1.smallFont, contentW - 40);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(drawn.X + 30, drawn.Center.Y - Game1.smallFont.MeasureString("A").Y / 2f),
                selected ? Color.SaddleBrown : Game1.textColor);
        }

        int maxScroll = GetMaxScroll(clip.Height);
        if (_scrollOffset > 0)
        {
            bool hu = _scrollUpBtn.Contains(mouseX, mouseY);
            b.Draw(Game1.mouseCursors, _scrollUpBtn,
                new Rectangle(352, 495, 12, 11), hu ? Color.Wheat : Color.White);
        }
        if (_scrollOffset < maxScroll)
        {
            bool hd = _scrollDownBtn.Contains(mouseX, mouseY);
            b.Draw(Game1.mouseCursors, _scrollDownBtn,
                new Rectangle(365, 495, 12, 11), hd ? Color.Wheat : Color.White);
        }

        if (HasActiveFilter)
        {
            bool hc = _clearButton.Contains(mouseX, mouseY);
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                _clearButton.X, _clearButton.Y, _clearButton.Width, _clearButton.Height,
                hc ? Color.Salmon : Color.White, 4f, drawShadow: true);
            Vector2 csz = Game1.smallFont.MeasureString(I18n.ButtonClearFilters);
            Utility.drawTextWithShadow(b, I18n.ButtonClearFilters, Game1.smallFont,
                new Vector2(_clearButton.Center.X - csz.X / 2f, _clearButton.Center.Y - csz.Y / 2f),
                Game1.textColor);
        }
    }

    // Input

    /// <summary>Returns true if click was consumed by the panel.</summary>
    public bool HandleClick(int x, int y, Rectangle gridArea)
    {
        if (!IsOpen) return false;

        UpdateLayout(gridArea);

        // The clear button now floats above the panel frame, so check it first.
        if (HasActiveFilter && _clearButton.Contains(x, y))
        {
            ClearFilters();
            Game1.playSound("smallSelect");
            return true;
        }

        if (!_panelBounds.Contains(x, y))
            return false;

        Rectangle clip = new(_panelBounds.X + 8, _panelBounds.Y + 4, _panelBounds.Width - 16, _panelBounds.Height - 8);
        int maxScroll = GetMaxScroll(clip.Height);

        if (_scrollUpBtn.Contains(x, y))   { _scrollOffset = System.Math.Max(0, _scrollOffset - 1); return true; }
        if (_scrollDownBtn.Contains(x, y)) { _scrollOffset = System.Math.Min(maxScroll, _scrollOffset + 1); return true; }

        int contentX = _panelBounds.X + 16;
        int contentW = _panelBounds.Width - 56;
        int offsetY  = _panelBounds.Y + 8 - _scrollOffset * ItemH;

        foreach (var item in _items)
        {
            if (item.Kind == FilterItemKind.SectionHeader) continue;
            Rectangle drawn = new(contentX, offsetY + item.RelativeY, contentW, ItemH);
            if (!clip.Contains(x, y) || !drawn.Contains(x, y)) continue;

            switch (item.Kind)
            {
                case FilterItemKind.Category:
                    FilterCategoryId = item.Id;
                    break;
                case FilterItemKind.Tag:
                    if (!FilterTagIds.Remove(item.Id)) FilterTagIds.Add(item.Id);
                    break;
                case FilterItemKind.ColorTag:
                    if (!FilterColorTagIds.Remove(item.Id)) FilterColorTagIds.Add(item.Id);
                    break;
            }

            Game1.playSound("smallSelect");
            return true;
        }

        return true;
    }

    public bool HandleScroll(int direction, int mouseX, int mouseY, Rectangle gridArea)
    {
        if (!IsOpen) return false;

        UpdateLayout(gridArea);
        if (!_panelBounds.Contains(mouseX, mouseY))
            return false;

        Rectangle clip = new(_panelBounds.X + 8, _panelBounds.Y + 4, _panelBounds.Width - 16, _panelBounds.Height - 8);
        int maxScroll = GetMaxScroll(clip.Height);

        _scrollOffset = System.Math.Clamp(_scrollOffset + (direction > 0 ? -1 : 1), 0, maxScroll);
        return true;
    }

    private static void DrawSectionHeader(SpriteBatch b, FilterItem item, Rectangle bounds)
    {
        b.Draw(Game1.staminaRect, bounds, Color.LightSteelBlue * 0.45f);

        string label = CleanSectionLabel(item.Label);
        Vector2 textSize = Game1.smallFont.MeasureString(label);
        const int iconSize = 28;
        const int gap = 8;

        float totalWidth = iconSize + gap + textSize.X + gap + iconSize;
        float startX = bounds.Center.X - totalWidth / 2f;
        int iconY = bounds.Center.Y - iconSize / 2;

        Rectangle leftIcon = new((int)startX, iconY, iconSize, iconSize);
        Rectangle rightIcon = new((int)(startX + iconSize + gap + textSize.X + gap), iconY, iconSize, iconSize);

        DrawSectionIcon(b, item.Id, leftIcon);
        DrawSectionIcon(b, item.Id, rightIcon);

        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(startX + iconSize + gap, bounds.Center.Y - textSize.Y / 2f),
            Game1.textColor);
    }

    private static void DrawSectionIcon(SpriteBatch b, string id, Rectangle destination)
    {
        switch (id)
        {
            case "__color_header__":
                DrawObjectIcon(b, 74, destination, Color.White); // Prismatic Shard
                return;

            case "__tag_header__":
                DrawQualityStar(b, new Rectangle(346, 392, 8, 8), destination); // Iridium-quality star
                return;

            case "__cat_header__":
            default:
                DrawQualityStar(b, new Rectangle(346, 400, 8, 8), destination); // Gold-quality star
                return;
        }
    }

    private static void DrawQualityStar(SpriteBatch b, Rectangle source, Rectangle destination)
    {
        b.Draw(Game1.mouseCursors, destination, source, Color.White);
    }

    private static void DrawObjectIcon(SpriteBatch b, int objectIndex, Rectangle destination, Color tint)
    {
        Rectangle source = Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, objectIndex, 16, 16);
        b.Draw(Game1.objectSpriteSheet, destination, source, tint);
    }

    private static string CleanSectionLabel(string label)
    {
        char[] decorative = { '✿', '❀', '✦', '✧', '★', '☆', '*', '•', '◆', '◇', '❖', '❦', '¤', '※', '✤', '✥' };
        return new string(label.Where(c => !decorative.Contains(c)).ToArray()).Trim();
    }

    // Helpers
    private static string TruncateString(string s, Microsoft.Xna.Framework.Graphics.SpriteFont font, int maxW)
    {
        if (font.MeasureString(s).X <= maxW) return s;
        while (s.Length > 1 && font.MeasureString(s + "…").X > maxW) s = s[..^1];
        return s + "…";
    }
}
