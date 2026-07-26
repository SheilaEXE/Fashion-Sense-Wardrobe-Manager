using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FashionSenseWardrobeManager;

/// <summary>
/// Entry point.
///
/// Responsibilities:
///  1. Register GMCM config.
///  2. Inject an "Expand" button into Fashion Sense's OutfitsMenu.
///  3. Maintain the quick-preview (Ctrl+Click) from the original mod.
///  4. Open ExpandedOutfitsMenu when the Expand button is clicked.
/// </summary>
internal sealed class ModEntry : Mod
{
    internal static bool SuppressNextAutoOpen { get; set; }

    // Constants

    private const string FashionSenseOutfitsMenuFqn = "FashionSense.Framework.UI.OutfitsMenu";
    private const string CreateOutfitInternalName   = "PeacefulEnd.Create.Outfit.Button";

    // State

    private ModConfig             _config          = new();
    private CategoryManager       _categoryManager = new();
    private TagManager            _tagManager      = new();
    private GlobalOrganizationManager _organizationManager = null!;
    private ScheduleManager       _scheduleManager = new();
    private ScheduleConditionCatalog _scheduleConditionCatalog = null!;
    private ScheduleEvaluator     _scheduleEvaluator = null!;
    private OutfitPreviewRenderer _renderer        = null!;
    private bool _scheduleEvaluationQueued;
    private int _scheduleEvaluationTicks;
    private int _scheduleEvaluationAttempts;

    // Quick-preview (legacy Ctrl+Click feature kept from the original mod)
    private bool    _quickPreviewOpen;
    private string? _quickPreviewOutfitName;
    private int     _quickPreviewFacing = 2;

    private Rectangle _quickPreviewBox;
    private Rectangle _quickPreviewPortraitBox;
    private Rectangle _quickLeftArrow;
    private Rectangle _quickRightArrow;
    private Rectangle _quickCloseButton;
    private Rectangle _quickCloseButtonHitbox;
    private Rectangle _quickLeftArrowHitbox;
    private Rectangle _quickRightArrowHitbox;

    private static readonly Rectangle LeftArrowSrc  = new(352, 495, 12, 11);
    private static readonly Rectangle RightArrowSrc = new(365, 495, 12, 11);

    // Expand button (injected over the FS menu)
    private Rectangle _expandButtonRect;

    // Entry

    public override void Entry(IModHelper helper)
    {
        _config   = helper.ReadConfig<ModConfig>();
        _organizationManager = new GlobalOrganizationManager(helper.Data, Monitor);
        _scheduleConditionCatalog = new ScheduleConditionCatalog(helper.ModRegistry);
        _renderer = new OutfitPreviewRenderer(Monitor);
        _scheduleEvaluator = new ScheduleEvaluator(_scheduleManager, _tagManager, _renderer, Monitor);
        I18n.Init(helper.Translation);

        helper.Events.GameLoop.GameLaunched    += OnGameLaunched;
        helper.Events.Input.ButtonPressed      += OnButtonPressed;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Display.MenuChanged      += OnMenuChanged;
        helper.Events.GameLoop.DayStarted      += OnDayStarted;
        helper.Events.GameLoop.SaveLoaded      += OnSaveLoaded;
        helper.Events.GameLoop.TimeChanged     += OnTimeChanged;
        helper.Events.Player.Warped            += OnWarped;
    }

    // GMCM

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        IGenericModConfigMenuApi? gmcm =
            Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

        if (gmcm is null)
            return;

        gmcm.Register(
            mod:   ModManifest,
            reset: () => _config = new ModConfig(),
            save:  () => Helper.WriteConfig(_config)
        );

        gmcm.AddKeybind(
            mod:      ModManifest,
            getValue: () => _config.PreviewShortcut,
            setValue: v => _config.PreviewShortcut = v,
            name:     () => I18n.ConfigShortcutName,
            tooltip:  () => I18n.ConfigShortcutTooltip
        );

        gmcm.AddKeybind(
            mod:      ModManifest,
            getValue: () => _config.ExpandedMenuShortcut,
            setValue: v => _config.ExpandedMenuShortcut = v,
            name:     () => I18n.ConfigExpandShortcutName,
            tooltip:  () => I18n.ConfigExpandShortcutTooltip
        );

        gmcm.AddBoolOption(
            mod:      ModManifest,
            getValue: () => _config.OpenExpandedByDefault,
            setValue: v => _config.OpenExpandedByDefault = v,
            name:     () => I18n.ConfigExpandDefaultName,
            tooltip:  () => I18n.ConfigExpandDefaultTooltip
        );
    }

    // Menu lifecycle

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        // If the FS outfits menu closed, tear down quick-preview
        if (_quickPreviewOpen && !IsFashionSenseOutfitsMenu(e.NewMenu))
            CloseQuickPreview(restoreOriginal: true);

        if (SuppressNextAutoOpen && IsFashionSenseOutfitsMenu(e.NewMenu))
        {
            SuppressNextAutoOpen = false;
            _pendingFsMenu = null;
            return;
        }

        // Auto-open expanded menu if the option is enabled and the FS menu just opened
        if (_config.OpenExpandedByDefault
            && IsFashionSenseOutfitsMenu(e.NewMenu)
            && e.NewMenu is not null)
        {
            // Defer one tick so the FS menu finishes initialising before we replace it
            Helper.Events.GameLoop.UpdateTicked += OpenExpandedOnNextTick;
            _pendingFsMenu = e.NewMenu;
        }
    }

    private IClickableMenu? _pendingFsMenu;

    private void OpenExpandedOnNextTick(object? sender, UpdateTickedEventArgs e)
    {
        Helper.Events.GameLoop.UpdateTicked -= OpenExpandedOnNextTick;

        // Only proceed if the FS menu is still the active one
        if (_pendingFsMenu is not null && Game1.activeClickableMenu == _pendingFsMenu)
            OpenExpandedMenu(_pendingFsMenu);

        _pendingFsMenu = null;
    }

    // Input

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        // When expanded menu has a text field focused, suppress game shortcuts
        // The game processes chat/journal/etc. before receiveKeyPress, so we need
        // to intercept at the SMAPI level. We suppress the button AND manually
        // forward it to receiveKeyPress so the character still gets typed.
        if (Game1.activeClickableMenu is ExpandedOutfitsMenu expandedMenu
            && expandedMenu.IsTypingFocused
            && e.Button.TryGetKeyboard(out Keys pressedKey))
        {
            // Always suppress so game shortcuts don't fire
            Helper.Input.Suppress(e.Button);
            // Forward to the menu's key handler so the character gets typed
            expandedMenu.receiveKeyPress(pressedKey);
            return;
        }

        // Quick-preview is open: route all input to it
        if (_quickPreviewOpen)
        {
            HandleQuickPreviewInput(e);
            return;
        }

        // Nothing if the active menu isn't the FS outfits menu
        IClickableMenu? menu = Game1.activeClickableMenu;

        if (!IsFashionSenseOutfitsMenu(menu))
            return;

        int x = Game1.getMouseX();
        int y = Game1.getMouseY();

        // Expanded menu hotkey
        if (IsExpandedMenuShortcut(e.Button))
        {
            Helper.Input.Suppress(e.Button);
            OpenExpandedMenu(menu!);
            return;
        }

        // "Expand" button
        if (e.Button == SButton.MouseLeft && _expandButtonRect.Contains(x, y))
        {
            Helper.Input.Suppress(e.Button);
            OpenExpandedMenu(menu!);
            return;
        }

        // Quick-preview: Ctrl + Left Click on an outfit
        if (e.Button != SButton.MouseLeft)
            return;

        if (!IsPreviewShortcutHeld())
            return;

        string? outfitName = TryGetClickedOutfitName(menu!, x, y);

        if (string.IsNullOrWhiteSpace(outfitName))
            return;

        Helper.Input.Suppress(e.Button);
        OpenQuickPreview(outfitName);
    }

    // Quick-preview (original Ctrl+Click behaviour)

    private void HandleQuickPreviewInput(ButtonPressedEventArgs e)
    {
        if (e.Button != SButton.MouseLeft && e.Button != SButton.Escape)
            return;

        Helper.Input.Suppress(e.Button);

        if (e.Button == SButton.Escape)
        {
            CloseQuickPreview(restoreOriginal: true);
            return;
        }

        UpdateQuickPreviewLayout();

        int x = Game1.getMouseX();
        int y = Game1.getMouseY();

        if (_quickLeftArrowHitbox.Contains(x, y))
        {
            _quickPreviewFacing = TurnLeft(_quickPreviewFacing);
            Game1.playSound("shwip");
            return;
        }

        if (_quickRightArrowHitbox.Contains(x, y))
        {
            _quickPreviewFacing = TurnRight(_quickPreviewFacing);
            Game1.playSound("shwip");
            return;
        }

        if (_quickCloseButtonHitbox.Contains(x, y) || !_quickPreviewBox.Contains(x, y))
            CloseQuickPreview(restoreOriginal: true);
    }

    private void OpenQuickPreview(string outfitName)
    {
        if (!_renderer.ApplyOutfitForPreview(outfitName))
            return;

        _quickPreviewOutfitName = outfitName;
        _quickPreviewFacing     = 2;
        _quickPreviewOpen       = true;

        Game1.playSound("bigSelect");
    }

    private void CloseQuickPreview(bool restoreOriginal)
    {
        if (restoreOriginal)
            _renderer.RestoreOriginal();

        _quickPreviewOpen       = false;
        _quickPreviewOutfitName = null;

        Game1.playSound("smallSelect");
    }

    // Expanded menu

    private void OpenExpandedMenu(IClickableMenu fashionSenseMenu)
    {
        List<string> outfitNames = GetAllOutfitNames(fashionSenseMenu);

        var expanded = new ExpandedOutfitsMenu(_categoryManager, _tagManager, _organizationManager,
            _scheduleManager, _scheduleEvaluator, _scheduleConditionCatalog, _renderer, outfitNames, fashionSenseMenu);
        Game1.activeClickableMenu = expanded;

        Game1.playSound("bigSelect");
    }

    // Outfit schedules

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        => QueueScheduleEvaluation();

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
        => QueueScheduleEvaluation();

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (e.IsLocalPlayer)
            QueueScheduleEvaluation();
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        => _scheduleEvaluator.Evaluate(exactTimeTrigger: true);

    private void QueueScheduleEvaluation()
    {
        if (_scheduleEvaluationQueued)
            return;

        _scheduleEvaluationQueued = true;
        _scheduleEvaluationTicks = 0;
        _scheduleEvaluationAttempts = 0;
        Helper.Events.GameLoop.UpdateTicked += EvaluateScheduleOnNextTick;
    }

    private void EvaluateScheduleOnNextTick(object? sender, UpdateTickedEventArgs e)
    {
        if (++_scheduleEvaluationTicks < 15)
            return;

        _scheduleEvaluationTicks = 0;
        _scheduleEvaluationAttempts++;

        bool finished = _scheduleEvaluator.Evaluate();
        if (!finished && _scheduleEvaluationAttempts < 20)
            return;

        Helper.Events.GameLoop.UpdateTicked -= EvaluateScheduleOnNextTick;
        _scheduleEvaluationQueued = false;
    }

    private List<string> GetAllOutfitNames(IClickableMenu menu)
    {
        var result = new List<string>();

        IList? pages = GetInstanceField(menu, "_pages") as IList;

        if (pages is null)
            return result;

        foreach (object? page in pages)
        {
            if (page is not IList outfits)
                continue;

            foreach (object? outfit in outfits)
            {
                string? name = GetMemberString(outfit, "Name");

                if (!string.IsNullOrWhiteSpace(name) && name != CreateOutfitInternalName)
                    result.Add(name);
            }
        }

        return result.Distinct().ToList();
    }

    // Rendering – Expand button + quick-preview overlay

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        IClickableMenu? menu = Game1.activeClickableMenu;

        // Draw the "Expand" button on top of the FS outfits menu
        if (IsFashionSenseOutfitsMenu(menu) && !_quickPreviewOpen)
        {
            UpdateExpandButtonLayout(menu!);
            DrawExpandButton(e.SpriteBatch);
        }

        // Draw quick-preview overlay
        if (_quickPreviewOpen && !string.IsNullOrWhiteSpace(_quickPreviewOutfitName))
            DrawQuickPreview(e.SpriteBatch);
    }

    private void UpdateExpandButtonLayout(IClickableMenu menu)
    {
        // Place the button below the bottom border of the FS menu, centred horizontally
        _expandButtonRect = new Rectangle(
            menu.xPositionOnScreen + menu.width / 2 - 70,
            menu.yPositionOnScreen + menu.height + 8,
            140, 44);
    }

    private void DrawExpandButton(SpriteBatch b)
    {
        bool hovered = _expandButtonRect.Contains(Game1.getMouseX(), Game1.getMouseY());

        IClickableMenu.drawTextureBox(
            b, Game1.mouseCursors,
            new Rectangle(432, 439, 9, 9),
            _expandButtonRect.X, _expandButtonRect.Y,
            _expandButtonRect.Width, _expandButtonRect.Height,
            hovered ? Color.Wheat : Color.White, 4f, drawShadow: true);

        Vector2 sz = Game1.smallFont.MeasureString(I18n.ButtonExpand);

        Utility.drawTextWithShadow(b, I18n.ButtonExpand, Game1.smallFont,
            new Vector2(
                _expandButtonRect.Center.X - sz.X / 2f,
                _expandButtonRect.Center.Y - sz.Y / 2f),
            Game1.textColor);
    }

    private void DrawQuickPreview(SpriteBatch b)
    {
        UpdateQuickPreviewLayout();

        b.Draw(Game1.fadeToBlackRect,
            Game1.graphics.GraphicsDevice.Viewport.Bounds,
            Color.Black * 0.35f);

        IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 373, 18, 18),
            _quickPreviewBox.X, _quickPreviewBox.Y,
            _quickPreviewBox.Width, _quickPreviewBox.Height,
            Color.White, 4f, drawShadow: true);

        SpriteText.drawStringWithScrollCenteredAt(b, I18n.Preview,
            _quickPreviewBox.Center.X,
            _quickPreviewBox.Y + 28);

        // Portrait box
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 396, 15, 15),
            _quickPreviewPortraitBox.X, _quickPreviewPortraitBox.Y,
            _quickPreviewPortraitBox.Width, _quickPreviewPortraitBox.Height,
            Color.White, 4f, drawShadow: false);

        Rectangle inner = new(
            _quickPreviewPortraitBox.X + 16, _quickPreviewPortraitBox.Y + 16,
            _quickPreviewPortraitBox.Width - 32, _quickPreviewPortraitBox.Height - 32);

        b.Draw(Game1.daybg, inner, Color.White);

        _renderer.DrawFarmerQuickPreview(b, inner, _quickPreviewFacing);

        DrawQuickArrow(b, _quickLeftArrow,  LeftArrowSrc);
        DrawQuickArrow(b, _quickRightArrow, RightArrowSrc);

        DrawMenuButton(b, _quickCloseButton, I18n.ButtonClose);

        Game1.mouseCursorTransparency = 1f;
        Game1.activeClickableMenu?.drawMouse(b);
    }

    private void DrawQuickArrow(SpriteBatch b, Rectangle bounds, Rectangle src)
    {
        Color color = ContainsWithPadding(bounds, Game1.getMouseX(), Game1.getMouseY(), 16)
            ? Color.Wheat : Color.White;
        b.Draw(Game1.mouseCursors, bounds, src, color);
    }

    private static void DrawMenuButton(SpriteBatch b, Rectangle bounds, string text)
    {
        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());

        IClickableMenu.drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(432, 439, 9, 9),
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            hovered ? Color.Wheat : Color.White, 4f, drawShadow: false);

        Vector2 sz = Game1.smallFont.MeasureString(text);
        Utility.drawTextWithShadow(b, text, Game1.smallFont,
            new Vector2(bounds.Center.X - sz.X / 2f, bounds.Center.Y - sz.Y / 2f),
            Game1.textColor);
    }

    private void UpdateQuickPreviewLayout()
    {
        const int w = 380, h = 430;

        _quickPreviewBox = new Rectangle(
            Game1.uiViewport.Width  / 2 - w / 2,
            Game1.uiViewport.Height / 2 - h / 2,
            w, h);

        _quickPreviewPortraitBox = new Rectangle(
            _quickPreviewBox.Center.X - 80,
            _quickPreviewBox.Y + 105,
            160, 220);

        _quickLeftArrow = new Rectangle(
            _quickPreviewPortraitBox.X - 64,
            _quickPreviewPortraitBox.Center.Y - 22,
            48, 44);

        _quickRightArrow = new Rectangle(
            _quickPreviewPortraitBox.Right + 16,
            _quickPreviewPortraitBox.Center.Y - 22,
            48, 44);

        _quickLeftArrowHitbox  = Inflate(_quickLeftArrow,  18, 26);
        _quickRightArrowHitbox = Inflate(_quickRightArrow, 18, 26);

        _quickCloseButton = new Rectangle(
            _quickPreviewBox.Center.X - 75,
            _quickPreviewBox.Bottom - 74,
            150, 56);

        _quickCloseButtonHitbox = Inflate(_quickCloseButton, 16, 18);
    }

    // Fashion Sense helpers (reflection)

    private string? TryGetClickedOutfitName(IClickableMenu menu, int x, int y)
    {
        object? isPresets = GetInstanceField(menu, "_isDisplayingPresets");

        if (isPresets is true)
        {
            Game1.addHUDMessage(new HUDMessage(I18n.ErrorPresetsUnsupported, HUDMessage.error_type));
            return null;
        }

        IList? outfitButtons = GetInstanceField(menu, "outfitButtons") as IList;

        if (outfitButtons is null)
            return null;

        int   currentPage = GetInstanceField(menu, "_currentPage") as int? ?? 0;
        IList? pages      = GetInstanceField(menu, "_pages") as IList;

        if (pages is null || currentPage < 0 || currentPage >= pages.Count)
            return null;

        IList? currentOutfits = pages[currentPage] as IList;

        if (currentOutfits is null)
            return null;

        for (int i = 0; i < outfitButtons.Count; i++)
        {
            if (i >= currentOutfits.Count)
                continue;

            if (outfitButtons[i] is not ClickableComponent btn)
                continue;

            if (!btn.containsPoint(x, y))
                continue;

            if (ClickedFunctionalIcon(menu, i, x, y))
                return null;

            string? name = GetMemberString(currentOutfits[i], "Name");

            if (string.IsNullOrWhiteSpace(name) || name == CreateOutfitInternalName)
                return null;

            return name;
        }

        return null;
    }

    private bool ClickedFunctionalIcon(IClickableMenu menu, int index, int x, int y)
        => PointInsideButtonList(menu, "shareButtons",  index, x, y)
        || PointInsideButtonList(menu, "exportButtons", index, x, y)
        || PointInsideButtonList(menu, "saveButtons",   index, x, y)
        || PointInsideButtonList(menu, "renameButtons", index, x, y)
        || PointInsideButtonList(menu, "deleteButtons", index, x, y);

    private static bool PointInsideButtonList(IClickableMenu menu, string field, int index, int x, int y)
    {
        if (GetInstanceField(menu, field) is not IList list || index >= list.Count)
            return false;

        return list[index] is ClickableComponent c && c.containsPoint(x, y);
    }

    // Generic reflection utilities

    private static object? GetInstanceField(object target, string name)
        => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(target);

    private static string? GetMemberString(object? target, string memberName)
    {
        if (target is null)
            return null;

        Type t = target.GetType();

        PropertyInfo? prop = t.GetProperty(memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (prop is not null)
            return prop.GetValue(target)?.ToString();

        FieldInfo? field = t.GetField(memberName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return field?.GetValue(target)?.ToString();
    }

    // Misc utilities

    private bool IsPreviewShortcutHeld()
        => _config.PreviewShortcut != SButton.None
        && Helper.Input.IsDown(_config.PreviewShortcut);

    private bool IsExpandedMenuShortcut(SButton button)
        => _config.ExpandedMenuShortcut != SButton.None
        && button == _config.ExpandedMenuShortcut;

    private static bool IsFashionSenseOutfitsMenu(IClickableMenu? menu)
        => menu?.GetType().FullName == FashionSenseOutfitsMenuFqn;

    private static bool ContainsWithPadding(Rectangle r, int x, int y, int pad)
        => new Rectangle(r.X - pad, r.Y - pad, r.Width + pad * 2, r.Height + pad * 2).Contains(x, y);

    private static Rectangle Inflate(Rectangle r, int padX, int padY)
        => new(r.X - padX, r.Y - padY, r.Width + padX * 2, r.Height + padY * 2);

    private static int TurnLeft(int d) => d switch { 2 => 3, 3 => 0, 0 => 1, 1 => 2, _ => 2 };
    private static int TurnRight(int d) => d switch { 2 => 1, 1 => 0, 0 => 3, 3 => 2, _ => 2 };
}
