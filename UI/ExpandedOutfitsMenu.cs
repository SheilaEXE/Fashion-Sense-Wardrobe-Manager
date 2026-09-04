using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FashionSenseWardrobeManager;

/// <summary>
/// The main "expanded" outfit browser.
///
/// Layout (1100 × 640):
/// ┌────────────────────────────────────────────────────────────────────┐
/// │  [Title]                                               [X Close]  │
/// │  [All] [Spring] [Summer] [Pajamas] ...   [+ Create Category]      │
/// │  Search: [_________________________]                               │
/// ├──────────────────────────────┬────────────────────────────────────┤
/// │  Outfit grid (scrollable)    │  Player preview panel              │
/// │  □ OutfitA   □ OutfitB       │  ┌──────────────────┐             │
/// │  □ OutfitC   □ OutfitD       │  │   Farmer sprite  │             │
/// │  ...                         │  └──────────────────┘             │
/// │                              │  [← ] [→ ]  direction             │
/// │                              │  [       Equip      ]             │
/// └──────────────────────────────┴────────────────────────────────────┘
///
/// Category mode:
/// When the player opens I18n.ButtonCreateCategory a small modal appears where they
/// type the name, then the window enters "assign mode" where each outfit row
/// has a checkbox to include it in the new category.
/// </summary>
internal sealed class ExpandedOutfitsMenu : IClickableMenu
{
    // Layout constants (all in UI pixels at 4× scale)

    private const int WindowWidth     = 1200;
    private const int WindowHeight    = 700;
    private const int Padding         = 32;
    private const int GridPaddingX    = 16;   // inner padding so items don't touch border
    private const int CategoryBarH    = 44;
    private const int SearchBarH      = 38;
    private const int GridCellW       = 220;
    private const int GridCellH       = 44;
    private const int PreviewPanelW   = 320;
    private const int OutfitNameMaxW  = 170;
    private const int DropdownScrollbarW = 24;
    private const int DropdownScreenMargin = 16;

    // Preview sprite box inside the right panel
    private const int PortraitBoxW    = 240;
    private const int PortraitBoxH    = 310;

    // Arrow source rects from the vanilla cursor sheet
    private static readonly Rectangle LeftArrowSrc  = new(352, 495, 12, 11);
    private static readonly Rectangle RightArrowSrc = new(365, 495, 12, 11);

    // Services

    private readonly CategoryManager       _categoryManager;
    private readonly TagManager            _tagManager;
    private readonly GlobalOrganizationManager _organizationManager;
    private readonly SchedulePanel         _schedulePanel;
    private readonly OutfitPreviewRenderer _renderer;
    private readonly IClickableMenu?       _returnMenu;
    private readonly List<string>          _allOutfitNames;

    // State

    private List<OutfitCategory> _categories;
    private List<OutfitTag>      _tags;

    // Filter state
    private string   _selectedCategoryId = "all";
    private string?  _selectedTagId      = null;
    private string   _searchText         = string.Empty;
    private bool     _searchFocused      = false;
    private bool     _textInputHooked    = false;

    // Grid scroll
    private int _scrollOffset = 0;

    // Selected outfit (for preview)
    private string? _selectedOutfit;
    private int     _previewFacing = 2;

    // "Create" submenu (replaces old _createCategoryButton)
    private bool      _createSubmenuOpen = false;
    private Rectangle _saveCurrentStyleButton;
    private Rectangle _createButton;
    private Rectangle _createSubNewCategory;
    private Rectangle _createSubNewTag;
    private Rectangle _createSubNewColorTag;
    private Rectangle _createSubExportOrganization;
    private Rectangle _createSubImportOrganization;

    // "Create category / tag" modal
    private bool     _creatingCategory    = false;
    private bool     _creatingTag         = false;
    private bool     _savingCurrentOutfit = false;
    private bool     _renamingOutfit      = false;
    private bool     _renamingSchedule    = false;
    private string?  _renameOriginalOutfitName = null;
    private string?  _scheduleRenameRuleId = null;
    private TagKind  _creatingTagKind  = TagKind.General;
    private string   _newItemName      = string.Empty;
    private bool     _namingFocused    = false;
    private bool IsNamingModalOpen => _creatingCategory || _creatingTag || _savingCurrentOutfit || _renamingOutfit || _renamingSchedule;

    // Right-click outfit context menu
    private bool      _renameContextMenuOpen = false;
    private string?   _contextOutfitName = null;
    private Rectangle _renameContextMenuRect;
    private Rectangle _updateOutfitContextMenuRect;

    // "Assign mode" for categories
    private bool          _assignMode         = false;
    private string?       _pendingCategoryId  = null;

    // "Assign tag mode"
    private bool    _assignTagMode  = false;
    private string? _pendingTagId   = null;

    // Category / tag dropdowns
    private bool _dropdownOpen = false;
    private bool _tagDropdownOpen = false;
    private bool _colorDropdownOpen = false;
    private int _categoryDropdownScroll = 0;
    private int _tagDropdownScroll = 0;
    private int _colorDropdownScroll = 0;
    private DropdownKind _draggingDropdownScrollbar = DropdownKind.None;
    private int _dropdownScrollbarDragOffsetY = 0;

    private enum DropdownKind
    {
        None,
        Category,
        Tag,
        Color
    }

    private readonly struct DropdownLayout
    {
        public Rectangle Bounds { get; }
        public Rectangle ItemsBounds { get; }
        public Rectangle ScrollTrack { get; }
        public Rectangle ScrollThumb { get; }
        public int VisibleRows { get; }
        public int MaxScroll { get; }
        public bool HasScrollbar => MaxScroll > 0;

        public DropdownLayout(
            Rectangle bounds,
            Rectangle itemsBounds,
            Rectangle scrollTrack,
            Rectangle scrollThumb,
            int visibleRows,
            int maxScroll)
        {
            Bounds = bounds;
            ItemsBounds = itemsBounds;
            ScrollTrack = scrollTrack;
            ScrollThumb = scrollThumb;
            VisibleRows = visibleRows;
            MaxScroll = maxScroll;
        }
    }

    // "Select mode" — player picks outfits to remove from active category
    private bool              _selectMode         = false;
    private HashSet<string>   _selectedForRemoval = new();

    // Saved outfit deletion mode — player picks outfits to delete from Fashion Sense
    private bool            _deleteOutfitMode      = false;
    private HashSet<string> _selectedForDeletion   = new();

    // Delete confirmation modal
    private bool    _confirmDeleteOutfits       = false;
    private bool    _confirmDeleteSavedOutfits  = false;
    private bool    _confirmDeleteCategory      = false;
    private bool    _confirmDeleteTag           = false;
    private bool    _confirmUpdateOutfit        = false;
    private string? _pendingDeleteTagId         = null;
    private string? _pendingUpdateOutfitName    = null;

    // Advanced filter panel
    private readonly AdvancedFilterPanel _advancedPanel = new();
    private bool _scheduleOutfitSelectionMode;
    private string? _scheduleSelectionRuleId;
    private HashSet<string> _scheduleSelectedOutfits = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _scheduleSelectedTagIds = new(StringComparer.OrdinalIgnoreCase);

    // Computed regions (refreshed in UpdateLayout)

    private Rectangle _categoryBar;
    private Rectangle _searchBar;
    private Rectangle _gridArea;
    private Rectangle _previewPanel;
    private Rectangle _portraitBox;
    private Rectangle _leftArrow;
    private Rectangle _rightArrow;
    private Rectangle _equipButton;
    private Rectangle _createCategoryButton;   // kept as alias → _createButton

    // Extra buttons (only visible when a custom category is active)
    private Rectangle _selectModeButton;
    private Rectangle _deleteCategoryButton;
    private Rectangle _deleteOutfitsButton;
    private Rectangle _deleteTrashButton;
    private Rectangle _advancedButton;    // "Advanced" toggle in the category bar

    // Confirm modal buttons
    private Rectangle _confirmYesButton;
    private Rectangle _confirmNoButton;

    // Backspace hold-to-repeat support
    private double _backspaceHeldMs  = 0;
    private bool   _backspaceWasDown = false;
    private const double BackspaceInitialDelayMs = 400;
    private const double BackspaceRepeatMs       = 60;

    // Category tab rectangles (rebuilt when categories change)
    private List<(string Id, string Label, Rectangle Bounds)> _categoryTabs = new();

    // Visible outfits (after filter + search)
    private List<string> _visibleOutfits = new();

    // Modal buttons
    private Rectangle _modalConfirmButton;
    private Rectangle _modalCancelButton;
    private Rectangle _modalTextBox;
    private Rectangle _assignDoneButton;

    // Constructor

    public ExpandedOutfitsMenu(
        CategoryManager       categoryManager,
        TagManager            tagManager,
        GlobalOrganizationManager organizationManager,
        ScheduleManager       scheduleManager,
        ScheduleEvaluator     scheduleEvaluator,
        ScheduleConditionCatalog scheduleConditionCatalog,
        OutfitPreviewRenderer renderer,
        IReadOnlyList<string> allOutfitNames,
        IClickableMenu?       parentMenu = null)
        : base(
            x:      Game1.uiViewport.Width  / 2 - WindowWidth  / 2,
            y:      Game1.uiViewport.Height / 2 - WindowHeight / 2,
            width:  WindowWidth,
            height: WindowHeight,
            showUpperRightCloseButton: true)
    {
        _categoryManager = categoryManager;
        _tagManager      = tagManager;
        _organizationManager = organizationManager;
        _renderer        = renderer;
        _returnMenu      = parentMenu;
        _allOutfitNames  = allOutfitNames.Distinct().ToList();

        _categories = _categoryManager.LoadCategories();
        _tags       = _tagManager.LoadTags();
        _schedulePanel = new SchedulePanel(scheduleManager, scheduleEvaluator, scheduleConditionCatalog, _tags);

        _selectedOutfit = GetCurrentOutfitName();

        _advancedPanel.SetData(_categories, _tags);

        UpdateLayout();
        RebuildVisibleOutfits();

        // Hook the OS-level text input event so typed characters (accents, ç,
        // uppercase via Shift/Caps Lock, etc.) are produced exactly as the
        // player's keyboard layout intends — far more reliable than mapping
        // individual Keys values.
        Game1.game1.Window.TextInput += OnTextInput;
        _textInputHooked = true;

        Game1.playSound("bigSelect");
    }

    /// <summary>
    /// Handles OS-level text input (respects the player's keyboard layout,
    /// Shift, Caps Lock, AltGr/dead-key accents, ç, etc.).
    /// </summary>
    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        char ch = e.Character;

        // Ignore control characters (Backspace=8, Tab=9, Enter=13, Escape=27, Delete=127, ...)
        if (ch < 32 || ch == 127)
            return;

        if (_searchFocused)
        {
            _searchText += ch;
            _scrollOffset = 0;
            RebuildVisibleOutfits();
        }
        else if (_namingFocused && IsNamingModalOpen)
        {
            if (_newItemName.Length < 32)
                _newItemName += ch;
        }
    }

    // Public API

    /// <summary>True when any text field in the menu is actively focused for input.</summary>
    public bool IsTypingFocused => _searchFocused || (_namingFocused && IsNamingModalOpen);

    private OutfitTag? SelectedTag => _selectedTagId is null
        ? null
        : _tags.FirstOrDefault(t => t.Id == _selectedTagId);

    private bool IsMainView => _selectedCategoryId == "all" && _selectedTagId is null;
    private bool IsCustomCategorySelected => _selectedCategoryId != "all";
    private bool IsTagSelected => SelectedTag is not null;

    private void ClearTopSelection()
    {
        _selectedCategoryId = "all";
        _selectedTagId = null;
    }

    private void SelectCategoryFilter(string categoryId)
    {
        _selectedCategoryId = categoryId;
        _selectedTagId = null;
        _dropdownOpen = false;
        _tagDropdownOpen = false;
        _colorDropdownOpen = false;
        _scrollOffset = 0;
        _selectMode = false;
        _selectedForRemoval.Clear();
        _deleteOutfitMode = false;
        _selectedForDeletion.Clear();
        RebuildVisibleOutfits();
        RebuildCategoryTabs();
        Game1.playSound("smallSelect");
    }

    private void SelectTagFilter(string tagId)
    {
        _selectedCategoryId = "all";
        _selectedTagId = tagId;
        _dropdownOpen = false;
        _tagDropdownOpen = false;
        _colorDropdownOpen = false;
        _scrollOffset = 0;
        _selectMode = false;
        _selectedForRemoval.Clear();
        _deleteOutfitMode = false;
        _selectedForDeletion.Clear();
        RebuildVisibleOutfits();
        RebuildCategoryTabs();
        Game1.playSound("smallSelect");
    }

    // IClickableMenu overrides

    public override void draw(SpriteBatch b)
    {
        // Dim background
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.4f);

        // Main window box
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18),
            xPositionOnScreen, yPositionOnScreen, width, height,
            Color.White, 4f, drawShadow: true);

        // Title — offset down enough to clear the window border decoration
        string windowTitle = _schedulePanel.IsOpen && !_scheduleOutfitSelectionMode
            ? _schedulePanel.CurrentTitle
            : I18n.Title;
        SpriteText.drawStringWithScrollCenteredAt(b, windowTitle,
            xPositionOnScreen + width / 2,
            yPositionOnScreen + 28);

        // Native Stardew Valley close button.
        upperRightCloseButton?.draw(b);

        if (IsNamingModalOpen)
        {
            DrawCreationModal(b);
        }
        else if (_confirmDeleteOutfits || _confirmDeleteSavedOutfits || _confirmDeleteCategory || _confirmDeleteTag || _confirmUpdateOutfit)
        {
            DrawConfirmModal(b);
        }
        else if (_assignMode || _assignTagMode)
        {
            DrawAssignMode(b);
            DrawDropdownOverlay(b);
        }
        else
        {
            DrawAdvancedSideTab(b);

            if (_schedulePanel.IsOpen && !_scheduleOutfitSelectionMode)
            {
                _schedulePanel.Draw(b);
            }
            else
            {
                // Normal category controls are hidden while the advanced filter strip is open.
                if (!_advancedPanel.IsOpen)
                    DrawCategoryBar(b);

                if (!_advancedPanel.IsOpen)
                    DrawSearchBar(b);

                if (_advancedPanel.IsOpen)
                    _advancedPanel.Draw(b, _gridArea, _previewPanel);

                DrawGrid(b);
                DrawPreviewPanel(b);
                DrawDropdownOverlay(b);
                DrawCreateSubmenu(b);
                DrawRenameContextMenu(b);
            }

            _schedulePanel.DrawSideTabs(b);
        }

        drawMouse(b);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        // Close Create submenu if clicking anywhere outside it
        if (_createSubmenuOpen
            && !_createButton.Contains(x, y)
            && !_createSubNewCategory.Contains(x, y)
            && !_createSubNewTag.Contains(x, y)
            && !_createSubNewColorTag.Contains(x, y)
            && !_createSubExportOrganization.Contains(x, y)
            && !_createSubImportOrganization.Contains(x, y))
        {
            _createSubmenuOpen = false;
            // Don't return — still process the click below
        }

        // Close
        if (upperRightCloseButton?.containsPoint(x, y) == true)
        {
            CloseMenu();
            return;
        }

        if (IsNamingModalOpen)
        {
            HandleModalClick(x, y);
            return;
        }

        if (_confirmDeleteOutfits || _confirmDeleteSavedOutfits || _confirmDeleteCategory || _confirmDeleteTag || _confirmUpdateOutfit)
        {
            HandleConfirmModalClick(x, y);
            return;
        }

        if (_assignMode || _assignTagMode)
        {
            HandleAssignModeClick(x, y);
            return;
        }

        if (_renameContextMenuOpen)
        {
            if (_contextOutfitName is not null && _renameContextMenuRect.Contains(x, y))
            {
                StartRenamingOutfit(_contextOutfitName);
                return;
            }

            if (_contextOutfitName is not null && _updateOutfitContextMenuRect.Contains(x, y))
            {
                _pendingUpdateOutfitName = _contextOutfitName;
                _confirmUpdateOutfit = true;
                _renameContextMenuOpen = false;
                _contextOutfitName = null;
                Game1.playSound("smallSelect");
                return;
            }

            _renameContextMenuOpen = false;
            _contextOutfitName = null;
        }

        if (_schedulePanel.HandleSideTabClick(x, y))
        {
            _advancedPanel.IsOpen = false;
            CancelScheduleOutfitSelection();
            CloseTransientControls();
            UpdateLayout();
            return;
        }

        if (_schedulePanel.IsOpen && !_scheduleOutfitSelectionMode)
        {
            if (_schedulePanel.HandleClick(x, y))
            {
                if (_schedulePanel.TryBeginOutfitSelection(out OutfitScheduleRule? rule) && rule is not null)
                    BeginScheduleOutfitSelection(rule);
                else if (_schedulePanel.TryBeginNameEdit(out OutfitScheduleRule? nameRule) && nameRule is not null)
                    StartRenamingSchedule(nameRule);
                return;
            }
        }

        // Advanced side tab — toggle inline filter panel.
        // This stays active even when the normal category controls are hidden.
        if (_advancedButton.Contains(x, y))
        {
            if (_scheduleOutfitSelectionMode)
            {
                _advancedPanel.IsOpen = !_advancedPanel.IsOpen;
                CloseTransientControls();
                _scrollOffset = 0;
                UpdateLayout();
                RebuildVisibleOutfits();
                Game1.playSound("smallSelect");
                return;
            }

            bool openingFromSchedule = _schedulePanel.IsOpen;
            _schedulePanel.Close();
            CancelScheduleOutfitSelection();
            _advancedPanel.IsOpen = openingFromSchedule || !_advancedPanel.IsOpen;
            _dropdownOpen = false;
            _tagDropdownOpen = false;
            _colorDropdownOpen = false;
            _createSubmenuOpen = false;
            _selectMode = false;
            _selectedForRemoval.Clear();
            _deleteOutfitMode = false;
            _selectedForDeletion.Clear();
            _scrollOffset = 0;
            UpdateLayout();
            RebuildVisibleOutfits();
            Game1.playSound("smallSelect");
            return;
        }

        // Create submenu items must be handled before category buttons, because the submenu visually overlaps that row.
        if (!_advancedPanel.IsOpen && _createSubmenuOpen)
        {
            if (TryHandleCreateSubmenuClick(x, y))
                return;
        }

        // The normal category/create controls are intentionally disabled while advanced filters are open.
        if (!_advancedPanel.IsOpen)
        {
            if (HandleDropdownScrollbarPress(x, y))
                return;

            // If dropdown is open, check for item clicks first
            if (_dropdownOpen)
            {
                var dropTab = _categoryTabs.FirstOrDefault(t => t.Id == "__dropdown__");
                if (dropTab != default)
                {
                    // Clicking the dropdown button again = close (toggle)
                    if (dropTab.Bounds.Contains(x, y))
                    {
                        _dropdownOpen = false;
                        Game1.playSound("smallSelect");
                        return;
                    }

                    if (_categories.Count > 0)
                    {
                        int itemCount = _categories.Count + 1;
                        DropdownLayout layout = GetDropdownLayout(
                            dropTab.Bounds,
                            itemCount,
                            200,
                            _categoryDropdownScroll);

                        for (int row = 0; row < layout.VisibleRows; row++)
                        {
                            int itemIndex = _categoryDropdownScroll + row;
                            Rectangle itemRect = new(
                                layout.ItemsBounds.X,
                                layout.ItemsBounds.Y + row * CategoryBarH,
                                layout.ItemsBounds.Width,
                                CategoryBarH);
                            if (!itemRect.Contains(x, y))
                                continue;

                            SelectCategoryFilter(itemIndex == 0 ? "all" : _categories[itemIndex - 1].Id);
                            return;
                        }
                    }
                }
                // Click outside dropdown = close it
                _dropdownOpen = false;
            }

            if (_tagDropdownOpen || _colorDropdownOpen)
            {
                bool isColorDropdown = _colorDropdownOpen;
                string dropdownId = isColorDropdown ? "__color_dropdown__" : "__tag_dropdown__";
                var dropTab = _categoryTabs.FirstOrDefault(t => t.Id == dropdownId);
                if (dropTab != default)
                {
                    if (dropTab.Bounds.Contains(x, y))
                    {
                        _tagDropdownOpen = false;
                        _colorDropdownOpen = false;
                        Game1.playSound("smallSelect");
                        return;
                    }

                    List<OutfitTag> availableTags = _tags
                        .Where(t => t.Kind == (isColorDropdown ? TagKind.Color : TagKind.General))
                        .ToList();

                    if (availableTags.Count > 0)
                    {
                        DropdownKind kind = isColorDropdown ? DropdownKind.Color : DropdownKind.Tag;
                        int scroll = GetDropdownScroll(kind);
                        DropdownLayout layout = GetDropdownLayout(
                            dropTab.Bounds,
                            availableTags.Count,
                            _scheduleOutfitSelectionMode ? 360 : 230,
                            scroll);

                        for (int row = 0; row < layout.VisibleRows; row++)
                        {
                            int itemIndex = scroll + row;
                            Rectangle itemRect = new(
                                layout.ItemsBounds.X,
                                layout.ItemsBounds.Y + row * CategoryBarH,
                                layout.ItemsBounds.Width,
                                CategoryBarH);
                            OutfitTag tag = availableTags[itemIndex];
                            Rectangle actionRect = _scheduleOutfitSelectionMode
                                ? GetWholeTagSelectionBounds(itemRect)
                                : GetTagDeleteButtonBounds(itemRect);

                            if (actionRect.Contains(x, y))
                            {
                                if (_scheduleOutfitSelectionMode)
                                {
                                    if (!_scheduleSelectedTagIds.Add(tag.Id))
                                        _scheduleSelectedTagIds.Remove(tag.Id);
                                    Game1.playSound("smallSelect");
                                }
                                else
                                {
                                    DeleteTagAndRefresh(tag.Id);
                                }
                                return;
                            }

                            if (!itemRect.Contains(x, y))
                                continue;

                            SelectTagFilter(tag.Id);
                            return;
                        }
                    }
                }

                _tagDropdownOpen = false;
                _colorDropdownOpen = false;
            }

            // Category tabs
            foreach (var (id, _, bounds) in _categoryTabs)
            {
                if (!bounds.Contains(x, y))
                    continue;

                if (id == "__dropdown__")
                {
                    _dropdownOpen = !_dropdownOpen;
                    if (_dropdownOpen)
                        _categoryDropdownScroll = 0;
                    _tagDropdownOpen = false;
                    _colorDropdownOpen = false;
                    Game1.playSound("smallSelect");
                    return;
                }

                if (id == "__tag_dropdown__")
                {
                    _tagDropdownOpen = !_tagDropdownOpen;
                    if (_tagDropdownOpen)
                        _tagDropdownScroll = 0;
                    _dropdownOpen = false;
                    _colorDropdownOpen = false;
                    Game1.playSound("smallSelect");
                    return;
                }

                if (id == "__color_dropdown__")
                {
                    _colorDropdownOpen = !_colorDropdownOpen;
                    if (_colorDropdownOpen)
                        _colorDropdownScroll = 0;
                    _dropdownOpen = false;
                    _tagDropdownOpen = false;
                    Game1.playSound("smallSelect");
                    return;
                }

                SelectCategoryFilter(id);
                return;
            }

            // Save current style button — only on the main/all view
            if (!_scheduleOutfitSelectionMode && IsMainView && _saveCurrentStyleButton.Contains(x, y))
            {
                _deleteOutfitMode = false;
                _selectedForDeletion.Clear();
                StartSavingCurrentOutfit();
                return;
            }

            // Create button — only on the main/all view
            if (!_scheduleOutfitSelectionMode && IsMainView && _createCategoryButton.Contains(x, y))
            {
                _createSubmenuOpen = !_createSubmenuOpen;
                _searchFocused    = false;
                _deleteOutfitMode = false;
                _selectedForDeletion.Clear();
                Game1.playSound("smallSelect");
                return;
            }

            // Delete saved outfits button — only on the main/all view
            if (!_scheduleOutfitSelectionMode && IsMainView && _deleteOutfitsButton.Contains(x, y))
            {
                _deleteOutfitMode = !_deleteOutfitMode;
                _selectedForDeletion.Clear();
                _searchFocused = false;
                _createSubmenuOpen = false;
                Game1.playSound("smallSelect");
                return;
            }

            // Create submenu items
            if (_createSubmenuOpen)
            {
                if (TryHandleCreateSubmenuClick(x, y))
                    return;

                // Click outside submenu closes it
                _createSubmenuOpen = false;
            }

        }

        // Advanced panel click (when open, consumes clicks in grid area)
        if (_advancedPanel.IsOpen && _advancedPanel.HandleClick(x, y, _gridArea))
        {
            RebuildVisibleOutfits();
            return;
        }

        // Edit/delete buttons for the active category/tag/color selection.
        if (!_scheduleOutfitSelectionMode && !_advancedPanel.IsOpen && (IsCustomCategorySelected || IsTagSelected))
        {
            if (_selectModeButton.Contains(x, y))
            {
                if (IsCustomCategorySelected)
                    StartEditingCategory(_selectedCategoryId);
                else if (_selectedTagId is not null)
                    StartEditingTag(_selectedTagId);
                return;
            }

            if (_deleteCategoryButton.Contains(x, y))
            {
                if (IsCustomCategorySelected)
                {
                    _confirmDeleteCategory = true;
                    UpdateLayout();
                    Game1.playSound("smallSelect");
                }
                else if (_selectedTagId is not null)
                {
                    _confirmDeleteTag   = true;
                    _pendingDeleteTagId = _selectedTagId;
                    UpdateLayout();
                    Game1.playSound("smallSelect");
                }
                return;
            }
        }

        // In select mode, grid cells toggle removal selection instead of preview
        if (_scheduleOutfitSelectionMode)
        {
            int clickedScheduleIdx = HitTestGridRow(x, y);
            if (clickedScheduleIdx >= 0)
            {
                string outfitName = _visibleOutfits[clickedScheduleIdx];
                if (!_scheduleSelectedOutfits.Add(outfitName))
                    _scheduleSelectedOutfits.Remove(outfitName);
                SelectOutfit(outfitName);
                Game1.playSound("smallSelect");
                return;
            }
        }

        // In select mode, grid cells toggle removal selection instead of preview
        if (_selectMode)
        {
            int clickedIdxSel = HitTestGridRow(x, y);
            if (clickedIdxSel >= 0)
            {
                string selName = _visibleOutfits[clickedIdxSel];
                if (!_selectedForRemoval.Add(selName))
                    _selectedForRemoval.Remove(selName);
                Game1.playSound("smallSelect");
                return;
            }
        }

        // In saved-outfit delete mode, grid cells toggle deletion selection instead of preview
        if (_deleteOutfitMode)
        {
            int clickedDeleteIdx = HitTestGridRow(x, y);
            if (clickedDeleteIdx >= 0)
            {
                string deleteName = _visibleOutfits[clickedDeleteIdx];
                if (!_selectedForDeletion.Add(deleteName))
                    _selectedForDeletion.Remove(deleteName);
                Game1.playSound("smallSelect");
                return;
            }
        }

        // Search bar focus. The search box is hidden while the advanced filters are open.
        if (!_advancedPanel.IsOpen && _searchBar.Contains(x, y))
        {
            _searchFocused = true;
            return;
        }
        _searchFocused = false;

        // Scroll arrows
        if (ScrollUpArrowBounds().Contains(x, y))
        {
            ScrollBy(-1);
            return;
        }
        if (ScrollDownArrowBounds().Contains(x, y))
        {
            ScrollBy(1);
            return;
        }

        // Grid cells
        int clickedRow = HitTestGridRow(x, y);
        if (clickedRow >= 0)
        {
            SelectOutfit(_visibleOutfits[clickedRow]);
            return;
        }

        // Preview panel: direction arrows
        if (_leftArrow.Contains(x, y))
        {
            _previewFacing = TurnLeft(_previewFacing);
            Game1.playSound("shwip");
            return;
        }
        if (_rightArrow.Contains(x, y))
        {
            _previewFacing = TurnRight(_previewFacing);
            Game1.playSound("shwip");
            return;
        }

        // Equip / Confirm button
        if (_equipButton.Contains(x, y))
        {
            if (_scheduleOutfitSelectionMode)
            {
                ConfirmScheduleOutfitSelection();
                return;
            }

            if (_deleteOutfitMode)
            {
                if (_selectedForDeletion.Count == 0)
                {
                    Game1.addHUDMessage(new HUDMessage(I18n.ErrorSelectFirst, HUDMessage.error_type));
                    return;
                }

                _confirmDeleteSavedOutfits = true;
                UpdateLayout();
                Game1.playSound("smallSelect");
                return;
            }

            if (_selectedOutfit is not null)
            {
                EquipSelectedOutfit();
                return;
            }
        }
    }

    private void BeginScheduleOutfitSelection(OutfitScheduleRule rule)
    {
        _scheduleOutfitSelectionMode = true;
        _scheduleSelectionRuleId = rule.Id;
        _scheduleSelectedOutfits = new HashSet<string>(rule.OutfitNames, StringComparer.OrdinalIgnoreCase);
        _scheduleSelectedTagIds = new HashSet<string>(rule.TagIds, StringComparer.OrdinalIgnoreCase);
        _advancedPanel.IsOpen = false;
        _searchText = string.Empty;
        _searchFocused = false;
        _selectedCategoryId = "all";
        _selectedTagId = null;
        _scrollOffset = 0;
        CloseTransientControls();
        RebuildVisibleOutfits();
        UpdateLayout();
    }

    private void ConfirmScheduleOutfitSelection()
    {
        if (_scheduleSelectionRuleId is null
            || (_scheduleSelectedOutfits.Count == 0 && _scheduleSelectedTagIds.Count == 0))
        {
            Game1.addHUDMessage(new HUDMessage(I18n.ErrorSelectFirst, HUDMessage.error_type));
            return;
        }

        _schedulePanel.SetSelectedOutfits(
            _scheduleSelectionRuleId,
            _scheduleSelectedOutfits,
            _scheduleSelectedTagIds);
        CancelScheduleOutfitSelection();
        Game1.playSound("newArtifact");
    }

    private void CancelScheduleOutfitSelection()
    {
        if (_renderer.IsPreviewActive)
            _renderer.RestoreOriginal();

        _scheduleOutfitSelectionMode = false;
        _scheduleSelectionRuleId = null;
        _scheduleSelectedOutfits.Clear();
        _scheduleSelectedTagIds.Clear();
        _selectedOutfit = null;
        _scrollOffset = 0;
        RebuildVisibleOutfits();
        UpdateLayout();
    }

    private void CloseTransientControls()
    {
        _dropdownOpen = false;
        _tagDropdownOpen = false;
        _colorDropdownOpen = false;
        _createSubmenuOpen = false;
        _selectMode = false;
        _selectedForRemoval.Clear();
        _deleteOutfitMode = false;
        _selectedForDeletion.Clear();
    }

    private bool TryHandleCreateSubmenuClick(int x, int y)
    {
        if (_createSubNewCategory.Contains(x, y))
        {
            _createSubmenuOpen = false;
            _creatingCategory    = true;
            _creatingTag         = false;
            _savingCurrentOutfit = false;
            _newItemName      = string.Empty;
            _namingFocused    = true;
            _searchFocused    = false;
            Game1.playSound("smallSelect");
            return true;
        }

        if (_createSubNewTag.Contains(x, y))
        {
            _createSubmenuOpen = false;
            _creatingTag         = true;
            _creatingCategory    = false;
            _savingCurrentOutfit = false;
            _creatingTagKind     = TagKind.General;
            _newItemName      = string.Empty;
            _namingFocused    = true;
            _searchFocused    = false;
            Game1.playSound("smallSelect");
            return true;
        }

        if (_createSubNewColorTag.Contains(x, y))
        {
            _createSubmenuOpen = false;
            _creatingTag         = true;
            _creatingCategory    = false;
            _savingCurrentOutfit = false;
            _creatingTagKind     = TagKind.Color;
            _newItemName      = string.Empty;
            _namingFocused    = true;
            _searchFocused    = false;
            Game1.playSound("smallSelect");
            return true;
        }

        if (_createSubExportOrganization.Contains(x, y))
        {
            _createSubmenuOpen = false;
            ExportOrganization();
            return true;
        }

        if (_createSubImportOrganization.Contains(x, y))
        {
            _createSubmenuOpen = false;
            ImportOrganization();
            return true;
        }

        return false;
    }

    private void ExportOrganization()
    {
        _organizationManager.Export(_categories, _tags);
        Game1.addHUDMessage(new HUDMessage(I18n.MessageOrganizationExported, HUDMessage.newQuest_type));
        Game1.playSound("coin");
    }

    private void ImportOrganization()
    {
        if (!_organizationManager.TryImportInto(_categories, _tags))
        {
            Game1.addHUDMessage(new HUDMessage(I18n.ErrorOrganizationImportMissing, HUDMessage.error_type));
            Game1.playSound("cancel");
            return;
        }

        _categoryManager.SaveCategories(_categories);
        _tagManager.SaveTags(_tags);

        _categories = _categoryManager.LoadCategories();
        _tags = _tagManager.LoadTags();

        _selectedCategoryId = "all";
        _selectedTagId = null;
        _dropdownOpen = false;
        _tagDropdownOpen = false;
        _colorDropdownOpen = false;
        _advancedPanel.SetData(_categories, _tags);
        _advancedPanel.UpdateLayout(_gridArea);
        _scrollOffset = 0;
        RebuildCategoryTabs();
        RebuildVisibleOutfits();

        Game1.addHUDMessage(new HUDMessage(I18n.MessageOrganizationImported, HUDMessage.newQuest_type));
        Game1.playSound("coin");
    }

    private void StartEditingCategory(string categoryId)
    {
        if (categoryId == "all")
            return;

        _selectedCategoryId = categoryId;
        _selectedTagId = null;
        _assignMode = true;
        _assignTagMode = false;
        _pendingCategoryId = categoryId;
        _pendingTagId = null;
        _scrollOffset = 0;
        _searchText = string.Empty;
        _searchFocused = false;
        _selectMode = false;
        _selectedForRemoval.Clear();
        _dropdownOpen = false;
        _tagDropdownOpen = false;
        _colorDropdownOpen = false;
        _createSubmenuOpen = false;
        RebuildVisibleOutfits();
        Game1.playSound("smallSelect");
    }

    private void StartEditingTag(string tagId)
    {
        _selectedCategoryId = "all";
        _selectedTagId = tagId;
        _assignMode = false;
        _assignTagMode = true;
        _pendingCategoryId = null;
        _pendingTagId = tagId;
        _scrollOffset = 0;
        _searchText = string.Empty;
        _searchFocused = false;
        _selectMode = false;
        _selectedForRemoval.Clear();
        _dropdownOpen = false;
        _tagDropdownOpen = false;
        _colorDropdownOpen = false;
        _createSubmenuOpen = false;
        RebuildVisibleOutfits();
        Game1.playSound("smallSelect");
    }


    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (IsNamingModalOpen
            || _confirmDeleteOutfits || _confirmDeleteSavedOutfits || _confirmDeleteCategory || _confirmDeleteTag || _confirmUpdateOutfit
            || _assignMode || _assignTagMode || _advancedPanel.IsOpen)
            return;

        int clickedRow = HitTestGridRow(x, y);
        if (clickedRow < 0 || clickedRow >= _visibleOutfits.Count)
        {
            _renameContextMenuOpen = false;
            _contextOutfitName = null;
            return;
        }

        _contextOutfitName = _visibleOutfits[clickedRow];
        _renameContextMenuOpen = true;

        int menuW = Math.Max(230, (int)Math.Ceiling(Math.Max(
            Game1.smallFont.MeasureString(I18n.ButtonRename).X,
            Game1.smallFont.MeasureString(I18n.ButtonUpdateOutfit).X)) + 32);
        int menuH = CategoryBarH * 2 + 4;
        int menuX = Math.Clamp(x, xPositionOnScreen + 12, xPositionOnScreen + width - menuW - 12);
        int menuY = Math.Clamp(y, yPositionOnScreen + 12, yPositionOnScreen + height - menuH - 12);
        _renameContextMenuRect = new Rectangle(menuX, menuY, menuW, CategoryBarH);
        _updateOutfitContextMenuRect = new Rectangle(menuX, menuY + CategoryBarH + 4, menuW, CategoryBarH);

        Game1.playSound("smallSelect");
    }


    public override void receiveKeyPress(Keys key)
    {
        // While any text field is focused, eat the keystroke so the game
        // never sees it (prevents chat, journal, etc. from opening).
        if (_searchFocused || (_namingFocused && IsNamingModalOpen))
        {
            // Let Escape and Enter through to our handlers but nothing else
            // should reach the game's global shortcut system.
            if (_searchFocused)
                HandleSearchKeyPress(key);
            else
                HandleNamingKeyPress(key);
            return;
        }

        if (key == Keys.Escape)
        {
            if (_scheduleOutfitSelectionMode)
            {
                CancelScheduleOutfitSelection();
                Game1.playSound("smallSelect");
            }
            else if (_schedulePanel.TryCloseConditionPicker())
            {
                // The picker handles its own close sound.
            }
            else if (_schedulePanel.IsOpen)
            {
                _schedulePanel.Close();
                UpdateLayout();
                Game1.playSound("smallSelect");
            }
            else if (_assignMode)
            {
                _assignMode        = false;
                _pendingCategoryId = null;
                if (_renderer.IsPreviewActive) _renderer.RestoreOriginal();
                _selectedOutfit = null;
                RebuildCategoryTabs();
                Game1.playSound("smallSelect");
            }
            else if (_assignTagMode)
            {
                _assignTagMode = false;
                _pendingTagId  = null;
                if (_renderer.IsPreviewActive) _renderer.RestoreOriginal();
                _selectedOutfit = null;
                _advancedPanel.SetData(_categories, _tags);
                Game1.playSound("smallSelect");
            }
            else if (_deleteOutfitMode)
            {
                _deleteOutfitMode = false;
                _selectedForDeletion.Clear();
                Game1.playSound("smallSelect");
            }
            else if (_createSubmenuOpen || _dropdownOpen || _tagDropdownOpen || _colorDropdownOpen || _renameContextMenuOpen)
            {
                _createSubmenuOpen = false;
                _dropdownOpen = false;
                _tagDropdownOpen = false;
                _colorDropdownOpen = false;
                _renameContextMenuOpen = false;
                _contextOutfitName = null;
            }
            else
            {
                CloseMenu();
            }
        }
    }

    /// <summary>
    /// Returning true tells the game NOT to process keyboard shortcuts
    /// (chat, journal, etc.) while this menu is open and a text field is focused.
    /// </summary>
    public override bool overrideSnappyMenuCursorMovementBan() => true;

    public override void leftClickHeld(int x, int y)
    {
        if (_schedulePanel.IsOpen && !_scheduleOutfitSelectionMode
            && _schedulePanel.HandleScrollbarDrag(y))
        {
            return;
        }

        if (_draggingDropdownScrollbar != DropdownKind.None
            && TryGetDropdownLayout(_draggingDropdownScrollbar, out DropdownLayout layout))
        {
            SetDropdownScrollFromThumb(
                _draggingDropdownScrollbar,
                y - _dropdownScrollbarDragOffsetY,
                layout);
            return;
        }

        base.leftClickHeld(x, y);
    }

    public override void releaseLeftClick(int x, int y)
    {
        _schedulePanel.ReleaseTimeHold();
        _schedulePanel.ReleaseScrollbarDrag();
        _draggingDropdownScrollbar = DropdownKind.None;
        _dropdownScrollbarDragOffsetY = 0;
        base.releaseLeftClick(x, y);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        int mx = Game1.getMouseX(), my = Game1.getMouseY();

        if (_schedulePanel.IsOpen && !_scheduleOutfitSelectionMode
            && _schedulePanel.HandleScroll(direction, mx, my))
        {
            return;
        }

        if (TryGetOpenDropdownLayout(out DropdownKind dropdownKind, out DropdownLayout dropdownLayout)
            && dropdownLayout.Bounds.Contains(mx, my))
        {
            SetDropdownScroll(
                dropdownKind,
                GetDropdownScroll(dropdownKind) + (direction > 0 ? -1 : 1),
                dropdownLayout.MaxScroll);
            return;
        }

        if (_advancedPanel.IsOpen && !IsAssignmentModeActive && _advancedPanel.HandleScroll(direction, mx, my, _gridArea))
            return;

        Rectangle scrollArea = CurrentScrollFrameArea;

        if (scrollArea.Contains(mx, my))
            ScrollBy(direction > 0 ? -1 : 1);
    }

    public override void update(GameTime time)
    {
        // Safety net: if something replaced the active menu without calling
        // CloseMenu (e.g. the game force-closing menus), unhook our text input
        // listener so it doesn't keep firing for a hidden/orphaned instance.
        if (_textInputHooked && Game1.activeClickableMenu != this)
        {
            UnhookTextInput();
            return;
        }

        if (_schedulePanel.IsOpen && !_scheduleOutfitSelectionMode)
            _schedulePanel.UpdateHeldControls(time);
        else
            _schedulePanel.ReleaseTimeHold();

        bool backspaceDown = Keyboard.GetState().IsKeyDown(Keys.Back);

        if (backspaceDown && (_searchFocused || (_namingFocused && IsNamingModalOpen)))
        {
            if (!_backspaceWasDown)
            {
                // First frame down — delete once immediately
                DeleteLastChar();
                _backspaceHeldMs = 0;
            }
            else
            {
                _backspaceHeldMs += time.ElapsedGameTime.TotalMilliseconds;

                double threshold = _backspaceHeldMs < BackspaceInitialDelayMs
                    ? BackspaceInitialDelayMs
                    : BackspaceRepeatMs;

                if (_backspaceHeldMs >= threshold)
                {
                    DeleteLastChar();
                    // After the initial delay, reset to repeat interval
                    if (_backspaceHeldMs >= BackspaceInitialDelayMs)
                        _backspaceHeldMs = BackspaceInitialDelayMs - BackspaceRepeatMs;
                }
            }
        }
        else
        {
            _backspaceHeldMs = 0;
        }

        _backspaceWasDown = backspaceDown;
    }

    private void DeleteLastChar()
    {
        if (_searchFocused && _searchText.Length > 0)
        {
            _searchText = _searchText[..^1];
            _scrollOffset = 0;
            RebuildVisibleOutfits();
        }
        else if (_namingFocused && IsNamingModalOpen && _newItemName.Length > 0)
        {
            _newItemName = _newItemName[..^1];
        }
    }

    // Drawing helpers

    private void DrawCategoryBar(SpriteBatch b)
    {
        foreach (var (id, label, bounds) in _categoryTabs)
        {
            bool isDropdown = id == "__dropdown__";
            bool isTagDropdown = id == "__tag_dropdown__";
            bool isColorDropdown = id == "__color_dropdown__";
            OutfitTag? selectedTag = SelectedTag;
            bool active = (!isDropdown && !isTagDropdown && !isColorDropdown && id == _selectedCategoryId)
                       || (isDropdown && _selectedCategoryId != "all")
                       || (isTagDropdown && selectedTag?.Kind == TagKind.General)
                       || (isColorDropdown && selectedTag?.Kind == TagKind.Color);
            bool hovered    = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());

            Color tint = active      ? Color.SandyBrown
                       : hovered     ? Color.Wheat
                                     : Color.White;

            drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                bounds.X, bounds.Y, bounds.Width, bounds.Height,
                tint, 4f, drawShadow: false);

            Vector2 sz = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(bounds.Center.X - sz.X / 2f, bounds.Center.Y - sz.Y / 2f),
                active ? Color.White : Game1.textColor);
        }

        // "Save Current Style" and "Create" only appear on the main/all view.
        if (IsMainView && !_scheduleOutfitSelectionMode)
        {
            bool saveHovered = _saveCurrentStyleButton.Contains(Game1.getMouseX(), Game1.getMouseY());
            drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                _saveCurrentStyleButton.X, _saveCurrentStyleButton.Y,
                _saveCurrentStyleButton.Width, _saveCurrentStyleButton.Height,
                saveHovered ? Color.Wheat : Color.White, 4f, drawShadow: false);

            Vector2 saveSz = Game1.smallFont.MeasureString(I18n.ButtonSaveCurrentStyle);
            Utility.drawTextWithShadow(b, I18n.ButtonSaveCurrentStyle, Game1.smallFont,
                new Vector2(_saveCurrentStyleButton.Center.X - saveSz.X / 2f, _saveCurrentStyleButton.Center.Y - saveSz.Y / 2f),
                Game1.textColor);

            bool ccHovered = _createCategoryButton.Contains(Game1.getMouseX(), Game1.getMouseY());
            drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                _createCategoryButton.X, _createCategoryButton.Y,
                _createCategoryButton.Width, _createCategoryButton.Height,
                _createSubmenuOpen ? Color.SandyBrown : (ccHovered ? Color.Wheat : Color.White), 4f, drawShadow: false);

            Vector2 ccSz = Game1.smallFont.MeasureString(I18n.ButtonCreate);
            Utility.drawTextWithShadow(b, I18n.ButtonCreate, Game1.smallFont,
                new Vector2(_createCategoryButton.Center.X - ccSz.X / 2f, _createCategoryButton.Center.Y - ccSz.Y / 2f),
                Game1.textColor);

            bool deleteHovered = _deleteOutfitsButton.Contains(Game1.getMouseX(), Game1.getMouseY());
            drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                _deleteOutfitsButton.X, _deleteOutfitsButton.Y,
                _deleteOutfitsButton.Width, _deleteOutfitsButton.Height,
                _deleteOutfitMode ? Color.SandyBrown : (deleteHovered ? Color.Salmon : Color.White),
                4f, drawShadow: false);

            Vector2 deleteSz = Game1.smallFont.MeasureString(I18n.ButtonDeleteOutfits);
            Utility.drawTextWithShadow(b, I18n.ButtonDeleteOutfits, Game1.smallFont,
                new Vector2(_deleteOutfitsButton.Center.X - deleteSz.X / 2f, _deleteOutfitsButton.Center.Y - deleteSz.Y / 2f),
                _deleteOutfitMode ? Color.White : Game1.textColor);
        }

        // Edit/delete buttons for the active category, tag, or color tag.
        if (!_scheduleOutfitSelectionMode && (IsCustomCategorySelected || IsTagSelected))
        {
            bool editHovered = _selectModeButton.Contains(Game1.getMouseX(), Game1.getMouseY());
            drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                _selectModeButton.X, _selectModeButton.Y,
                _selectModeButton.Width, _selectModeButton.Height,
                editHovered ? Color.Wheat : Color.White, 4f, drawShadow: false);
            Vector2 editSz = Game1.smallFont.MeasureString(I18n.ButtonEdit);
            Utility.drawTextWithShadow(b, I18n.ButtonEdit, Game1.smallFont,
                new Vector2(_selectModeButton.Center.X - editSz.X / 2f, _selectModeButton.Center.Y - editSz.Y / 2f),
                Game1.textColor);

            string deleteLabel = IsCustomCategorySelected
                ? I18n.ButtonDeleteCategory
                : (SelectedTag?.Kind == TagKind.Color ? I18n.ButtonDeleteColor : I18n.ButtonDeleteTag);

            bool delHovered = _deleteCategoryButton.Contains(Game1.getMouseX(), Game1.getMouseY());
            drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                _deleteCategoryButton.X, _deleteCategoryButton.Y,
                _deleteCategoryButton.Width, _deleteCategoryButton.Height,
                delHovered ? Color.Salmon : Color.White, 4f, drawShadow: false);
            Vector2 delSz = Game1.smallFont.MeasureString(deleteLabel);
            Utility.drawTextWithShadow(b, deleteLabel, Game1.smallFont,
                new Vector2(_deleteCategoryButton.Center.X - delSz.X / 2f, _deleteCategoryButton.Center.Y - delSz.Y / 2f),
                Game1.textColor);
        }
    }   // end DrawCategoryBar

    private void DrawAdvancedSideTab(SpriteBatch b)
    {
        bool hovered = _advancedButton.Contains(Game1.getMouseX(), Game1.getMouseY());
        Color tint = _advancedPanel.IsOpen          ? Color.SandyBrown
                   : _advancedPanel.HasActiveFilter ? Color.PeachPuff
                   : hovered                        ? Color.Wheat
                                                    : Color.White;

        // Icon-only side tab, similar to the vanilla menu side tabs.
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            _advancedButton.X, _advancedButton.Y,
            _advancedButton.Width, _advancedButton.Height,
            tint, 4f, drawShadow: true);

        int iconSize = 28;
        b.Draw(Game1.mouseCursors,
            new Rectangle(_advancedButton.Center.X - iconSize / 2, _advancedButton.Center.Y - iconSize / 2, iconSize, iconSize),
            new Rectangle(211, 428, 7, 6),
            _advancedPanel.IsOpen ? Color.White : Color.Salmon);
    }


    private void DrawCreateSubmenu(SpriteBatch b)
    {
        if (!_createSubmenuOpen) return;

        int mx = Game1.getMouseX(), my = Game1.getMouseY();

        DrawCreateSubItem(b, _createSubNewCategory, I18n.ButtonNewCategory, mx, my);
        DrawCreateSubItem(b, _createSubNewTag,      I18n.ButtonNewTag,      mx, my);
        DrawCreateSubItem(b, _createSubNewColorTag, I18n.ButtonNewColorTag, mx, my);
        DrawCreateSubItem(b, _createSubExportOrganization, I18n.ButtonExportOrganization, mx, my);
        DrawCreateSubItem(b, _createSubImportOrganization, I18n.ButtonImportOrganization, mx, my);
    }

    private void DrawCreateSubItem(SpriteBatch b, Rectangle bounds, string label, int mx, int my)
    {
        bool hovered = bounds.Contains(mx, my);
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            hovered ? Color.Wheat : Color.White, 4f, drawShadow: true);
        Vector2 sz = Game1.smallFont.MeasureString(label);
        Utility.drawTextWithShadow(b, label, Game1.smallFont,
            new Vector2(bounds.Center.X - sz.X / 2f, bounds.Center.Y - sz.Y / 2f),
            Game1.textColor);
    }

    private void DrawRenameContextMenu(SpriteBatch b)
    {
        if (!_renameContextMenuOpen)
            return;

        bool hovered = _renameContextMenuRect.Contains(Game1.getMouseX(), Game1.getMouseY());
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            _renameContextMenuRect.X, _renameContextMenuRect.Y,
            _renameContextMenuRect.Width, _renameContextMenuRect.Height,
            hovered ? Color.Wheat : Color.White, 4f, drawShadow: true);

        Vector2 size = Game1.smallFont.MeasureString(I18n.ButtonRename);
        Utility.drawTextWithShadow(b, I18n.ButtonRename, Game1.smallFont,
            new Vector2(_renameContextMenuRect.Center.X - size.X / 2f,
                _renameContextMenuRect.Center.Y - size.Y / 2f),
            Game1.textColor);

        bool updateHovered = _updateOutfitContextMenuRect.Contains(Game1.getMouseX(), Game1.getMouseY());
        drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            _updateOutfitContextMenuRect.X, _updateOutfitContextMenuRect.Y,
            _updateOutfitContextMenuRect.Width, _updateOutfitContextMenuRect.Height,
            updateHovered ? Color.Wheat : Color.White, 4f, drawShadow: true);

        Vector2 updateSize = Game1.smallFont.MeasureString(I18n.ButtonUpdateOutfit);
        Utility.drawTextWithShadow(b, I18n.ButtonUpdateOutfit, Game1.smallFont,
            new Vector2(_updateOutfitContextMenuRect.Center.X - updateSize.X / 2f,
                _updateOutfitContextMenuRect.Center.Y - updateSize.Y / 2f),
            Game1.textColor);
    }

    private static string NoneCategoryLabel()
    {
        return I18n.ButtonNone;
    }

    private DropdownLayout GetDropdownLayout(
        Rectangle dropdownButton,
        int itemCount,
        int minimumWidth,
        int scrollOffset)
    {
        int listX = dropdownButton.X;
        int listY = dropdownButton.Bottom + 2;
        int listW = Math.Max(dropdownButton.Width, minimumWidth);
        int availableHeight = Math.Max(
            CategoryBarH + 8,
            Game1.uiViewport.Height - listY - DropdownScreenMargin);
        int visibleRows = Math.Clamp(
            (availableHeight - 8) / CategoryBarH,
            1,
            Math.Max(1, itemCount));
        int maxScroll = Math.Max(0, itemCount - visibleRows);
        int clampedScroll = Math.Clamp(scrollOffset, 0, maxScroll);
        int listH = visibleRows * CategoryBarH + 8;

        Rectangle bounds = new(listX, listY, listW, listH);
        int scrollbarSpace = maxScroll > 0 ? DropdownScrollbarW + 4 : 0;
        Rectangle itemsBounds = new(
            listX + 4,
            listY + 4,
            listW - 8 - scrollbarSpace,
            visibleRows * CategoryBarH);

        if (maxScroll == 0)
            return new DropdownLayout(bounds, itemsBounds, Rectangle.Empty, Rectangle.Empty, visibleRows, maxScroll);

        Rectangle track = new(
            bounds.Right - DropdownScrollbarW,
            bounds.Y + 8,
            DropdownScrollbarW - 6,
            bounds.Height - 16);
        int thumbHeight = Math.Max(24, (int)Math.Round(track.Height * (visibleRows / (double)itemCount)));
        thumbHeight = Math.Min(track.Height, thumbHeight);
        int thumbTravel = Math.Max(0, track.Height - thumbHeight);
        int thumbY = track.Y + (maxScroll == 0
            ? 0
            : (int)Math.Round(thumbTravel * (clampedScroll / (double)maxScroll)));
        Rectangle thumb = new(track.X, thumbY, track.Width, thumbHeight);

        return new DropdownLayout(bounds, itemsBounds, track, thumb, visibleRows, maxScroll);
    }

    private bool TryGetDropdownLayout(DropdownKind kind, out DropdownLayout layout)
    {
        string dropdownId;
        int itemCount;
        int minimumWidth;

        switch (kind)
        {
            case DropdownKind.Category:
                dropdownId = "__dropdown__";
                itemCount = _categories.Count + 1;
                minimumWidth = 200;
                break;
            case DropdownKind.Tag:
                dropdownId = "__tag_dropdown__";
                itemCount = _tags.Count(tag => tag.Kind == TagKind.General);
                minimumWidth = _scheduleOutfitSelectionMode ? 360 : 230;
                break;
            case DropdownKind.Color:
                dropdownId = "__color_dropdown__";
                itemCount = _tags.Count(tag => tag.Kind == TagKind.Color);
                minimumWidth = _scheduleOutfitSelectionMode ? 360 : 230;
                break;
            default:
                layout = default;
                return false;
        }

        var dropTab = _categoryTabs.FirstOrDefault(tab => tab.Id == dropdownId);
        if (dropTab == default || itemCount <= 0)
        {
            layout = default;
            return false;
        }

        layout = GetDropdownLayout(dropTab.Bounds, itemCount, minimumWidth, GetDropdownScroll(kind));
        SetDropdownScroll(kind, GetDropdownScroll(kind), layout.MaxScroll);
        return true;
    }

    private bool TryGetOpenDropdownLayout(out DropdownKind kind, out DropdownLayout layout)
    {
        kind = _dropdownOpen
            ? DropdownKind.Category
            : _tagDropdownOpen
                ? DropdownKind.Tag
                : _colorDropdownOpen
                    ? DropdownKind.Color
                    : DropdownKind.None;

        return TryGetDropdownLayout(kind, out layout);
    }

    private int GetDropdownScroll(DropdownKind kind)
    {
        return kind switch
        {
            DropdownKind.Category => _categoryDropdownScroll,
            DropdownKind.Tag => _tagDropdownScroll,
            DropdownKind.Color => _colorDropdownScroll,
            _ => 0
        };
    }

    private void SetDropdownScroll(DropdownKind kind, int value, int maxScroll)
    {
        int clamped = Math.Clamp(value, 0, Math.Max(0, maxScroll));
        switch (kind)
        {
            case DropdownKind.Category:
                _categoryDropdownScroll = clamped;
                break;
            case DropdownKind.Tag:
                _tagDropdownScroll = clamped;
                break;
            case DropdownKind.Color:
                _colorDropdownScroll = clamped;
                break;
        }
    }

    private bool HandleDropdownScrollbarPress(int x, int y)
    {
        if (!TryGetOpenDropdownLayout(out DropdownKind kind, out DropdownLayout layout)
            || !layout.HasScrollbar
            || !layout.ScrollTrack.Contains(x, y))
        {
            return false;
        }

        if (layout.ScrollThumb.Contains(x, y))
        {
            _draggingDropdownScrollbar = kind;
            _dropdownScrollbarDragOffsetY = y - layout.ScrollThumb.Y;
        }
        else
        {
            _draggingDropdownScrollbar = kind;
            _dropdownScrollbarDragOffsetY = layout.ScrollThumb.Height / 2;
            SetDropdownScrollFromThumb(kind, y - _dropdownScrollbarDragOffsetY, layout);
        }

        Game1.playSound("shiny4");
        return true;
    }

    private void SetDropdownScrollFromThumb(DropdownKind kind, int thumbTop, DropdownLayout layout)
    {
        if (!layout.HasScrollbar)
            return;

        int travel = layout.ScrollTrack.Height - layout.ScrollThumb.Height;
        if (travel <= 0)
        {
            SetDropdownScroll(kind, 0, layout.MaxScroll);
            return;
        }

        int clampedTop = Math.Clamp(
            thumbTop,
            layout.ScrollTrack.Y,
            layout.ScrollTrack.Bottom - layout.ScrollThumb.Height);
        double ratio = (clampedTop - layout.ScrollTrack.Y) / (double)travel;
        SetDropdownScroll(kind, (int)Math.Round(ratio * layout.MaxScroll), layout.MaxScroll);
    }

    private static void DrawDropdownScrollbar(SpriteBatch b, DropdownLayout layout)
    {
        if (!layout.HasScrollbar)
            return;

        b.Draw(Game1.staminaRect, layout.ScrollTrack, Color.SaddleBrown * 0.25f);
        drawTextureBox(
            b,
            Game1.mouseCursors,
            new Rectangle(432, 439, 9, 9),
            layout.ScrollThumb.X,
            layout.ScrollThumb.Y,
            layout.ScrollThumb.Width,
            layout.ScrollThumb.Height,
            layout.ScrollThumb.Contains(Game1.getMouseX(), Game1.getMouseY()) ? Color.Wheat : Color.White,
            2f,
            drawShadow: false);
    }

    /// <summary>
    /// Draw the category dropdown list on top of everything else so it overlaps the grid/searchbar.
    /// Must be called AFTER grid and search bar are drawn.
    /// </summary>
    private void DrawDropdownOverlay(SpriteBatch b)
    {
        if (_dropdownOpen)
            DrawCategoryDropdownOverlay(b);

        if (_tagDropdownOpen)
            DrawTagDropdownOverlay(b, TagKind.General, "__tag_dropdown__");

        if (_colorDropdownOpen)
            DrawTagDropdownOverlay(b, TagKind.Color, "__color_dropdown__");
    }

    private void DrawCategoryDropdownOverlay(SpriteBatch b)
    {
        if (_categories.Count == 0)
            return;

        var dropTab = _categoryTabs.FirstOrDefault(t => t.Id == "__dropdown__");
        if (dropTab == default)
            return;

        int itemCount = _categories.Count + 1; // + "Nenhum" / "None"
        DropdownLayout layout = GetDropdownLayout(
            dropTab.Bounds,
            itemCount,
            200,
            _categoryDropdownScroll);
        _categoryDropdownScroll = Math.Clamp(_categoryDropdownScroll, 0, layout.MaxScroll);

        b.Draw(Game1.staminaRect,
            layout.Bounds,
            Color.AntiqueWhite);

        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 396, 15, 15),
            layout.Bounds.X, layout.Bounds.Y, layout.Bounds.Width, layout.Bounds.Height,
            Color.White, 4f, drawShadow: true);

        for (int row = 0; row < layout.VisibleRows; row++)
        {
            int itemIndex = _categoryDropdownScroll + row;
            bool isNoneItem = itemIndex == 0;
            string itemId   = isNoneItem ? "all" : _categories[itemIndex - 1].Id;
            string itemName = isNoneItem ? NoneCategoryLabel() : _categories[itemIndex - 1].Name;

            Rectangle itemRect = new(
                layout.ItemsBounds.X,
                layout.ItemsBounds.Y + row * CategoryBarH,
                layout.ItemsBounds.Width,
                CategoryBarH);
            bool itemHovered    = itemRect.Contains(Game1.getMouseX(), Game1.getMouseY());
            bool itemActive     = itemId == _selectedCategoryId;

            if (itemActive)
                b.Draw(Game1.staminaRect, itemRect, Color.SandyBrown * 0.4f);
            else if (itemHovered)
                b.Draw(Game1.staminaRect, itemRect, Color.Wheat * 0.4f);

            Vector2 isz = Game1.smallFont.MeasureString(itemName);
            Utility.drawTextWithShadow(b, itemName, Game1.smallFont,
                new Vector2(itemRect.X + 10, itemRect.Center.Y - isz.Y / 2f),
                itemActive ? Color.SaddleBrown : Game1.textColor);
        }

        DrawDropdownScrollbar(b, layout);
    }

    private void DrawTagDropdownOverlay(SpriteBatch b, TagKind kind, string dropdownId)
    {
        var dropTab = _categoryTabs.FirstOrDefault(t => t.Id == dropdownId);
        if (dropTab == default)
            return;

        List<OutfitTag> availableTags = _tags.Where(t => t.Kind == kind).ToList();
        if (availableTags.Count == 0)
            return;

        DropdownKind dropdownKind = kind == TagKind.Color ? DropdownKind.Color : DropdownKind.Tag;
        int scroll = GetDropdownScroll(dropdownKind);
        DropdownLayout layout = GetDropdownLayout(
            dropTab.Bounds,
            availableTags.Count,
            _scheduleOutfitSelectionMode ? 360 : 230,
            scroll);
        SetDropdownScroll(dropdownKind, scroll, layout.MaxScroll);
        scroll = GetDropdownScroll(dropdownKind);

        b.Draw(Game1.staminaRect,
            layout.Bounds,
            Color.AntiqueWhite);

        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 396, 15, 15),
            layout.Bounds.X, layout.Bounds.Y, layout.Bounds.Width, layout.Bounds.Height,
            Color.White, 4f, drawShadow: true);

        for (int row = 0; row < layout.VisibleRows; row++)
        {
            int itemIndex = scroll + row;
            OutfitTag tag = availableTags[itemIndex];
            Rectangle itemRect = new(
                layout.ItemsBounds.X,
                layout.ItemsBounds.Y + row * CategoryBarH,
                layout.ItemsBounds.Width,
                CategoryBarH);
            bool itemHovered = itemRect.Contains(Game1.getMouseX(), Game1.getMouseY());

            if (itemHovered)
                b.Draw(Game1.staminaRect, itemRect, Color.Wheat * 0.4f);

            Rectangle actionRect = _scheduleOutfitSelectionMode
                ? GetWholeTagSelectionBounds(itemRect)
                : GetTagDeleteButtonBounds(itemRect);
            bool actionHovered = actionRect.Contains(Game1.getMouseX(), Game1.getMouseY());
            string tagLabel = TruncateString(
                tag.Name,
                Game1.smallFont,
                Math.Max(40, actionRect.X - itemRect.X - 18));
            Vector2 isz = Game1.smallFont.MeasureString(tagLabel);
            Utility.drawTextWithShadow(b, tagLabel, Game1.smallFont,
                new Vector2(itemRect.X + 10, itemRect.Center.Y - isz.Y / 2f),
                Game1.textColor);

            if (_scheduleOutfitSelectionMode)
            {
                if (actionHovered)
                    b.Draw(Game1.staminaRect, actionRect, Color.Wheat * 0.35f);

                Vector2 actionSize = Game1.smallFont.MeasureString(I18n.ScheduleWholeTag);
                Utility.drawTextWithShadow(b, I18n.ScheduleWholeTag, Game1.smallFont,
                    new Vector2(actionRect.X + 4, actionRect.Center.Y - actionSize.Y / 2f),
                    Game1.textColor);

                Rectangle checkbox = new(actionRect.Right - 22, actionRect.Center.Y - 8, 16, 16);
                b.Draw(Game1.mouseCursors, checkbox, new Rectangle(227, 425, 9, 9), Color.White, 0f,
                    Vector2.Zero, SpriteEffects.None, 0.88f);
                if (_scheduleSelectedTagIds.Contains(tag.Id))
                {
                    b.Draw(Game1.mouseCursors, checkbox, new Rectangle(236, 425, 9, 9), Color.White, 0f,
                        Vector2.Zero, SpriteEffects.None, 0.89f);
                }
            }
            else
            {
                drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                    actionRect.X, actionRect.Y, actionRect.Width, actionRect.Height,
                    actionHovered ? Color.Salmon : Color.White, 4f, drawShadow: false);
                b.Draw(Game1.mouseCursors,
                    new Rectangle(actionRect.Center.X - 8, actionRect.Center.Y - 8, 16, 16),
                    new Rectangle(323, 433, 9, 9),
                    Color.White);
            }
        }

        DrawDropdownScrollbar(b, layout);
    }

    private static Rectangle GetTagDeleteButtonBounds(Rectangle itemRect)
    {
        return new Rectangle(itemRect.Right - 42, itemRect.Y + 6, 32, itemRect.Height - 12);
    }

    private static Rectangle GetWholeTagSelectionBounds(Rectangle itemRect)
    {
        return new Rectangle(itemRect.Right - 142, itemRect.Y + 4, 132, itemRect.Height - 8);
    }

    private void DeleteTagAndRefresh(string tagId)
    {
        _schedulePanel.RemoveTag(tagId);
        _tagManager.DeleteTag(_tags, tagId);
        _tags = _tagManager.LoadTags();
        _schedulePanel.SetTags(_tags);
        _advancedPanel.SetData(_categories, _tags);
        _advancedPanel.UpdateLayout(_gridArea);
        _tagDropdownOpen = false;
        _colorDropdownOpen = false;
        _assignTagMode = false;
        if (_pendingTagId == tagId)
            _pendingTagId = null;
        if (_selectedTagId == tagId)
            _selectedTagId = null;
        RebuildCategoryTabs();
        RebuildVisibleOutfits();
        Game1.playSound("trashcan");
    }

    private void DrawSearchBar(SpriteBatch b)
    {
        Color borderColor = _searchFocused ? Color.SkyBlue : Color.White;

        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 396, 15, 15),
            _searchBar.X, _searchBar.Y, _searchBar.Width, _searchBar.Height,
            borderColor, 4f, drawShadow: false);

        string display = string.IsNullOrEmpty(_searchText) && !_searchFocused
            ? I18n.SearchPlaceholder
            : _searchText + (_searchFocused ? "|" : "");

        Color textColor = string.IsNullOrEmpty(_searchText) && !_searchFocused
            ? Color.Gray
            : Game1.textColor;

        Utility.drawTextWithShadow(b, display, Game1.smallFont,
            new Vector2(_searchBar.X + 12, _searchBar.Center.Y - Game1.smallFont.MeasureString("A").Y / 2f),
            textColor);
    }

    private void DrawGrid(SpriteBatch b)
    {
        Rectangle gridArea = OutfitGridArea;

        // Background
        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 396, 15, 15),
            gridArea.X, gridArea.Y, gridArea.Width, gridArea.Height,
            Color.White, 4f, drawShadow: false);

        Rectangle content = GridContentArea;
        int cols         = Math.Max(1, content.Width / GridCellW);
        int visibleRows  = content.Height / GridCellH;
        int mouseX       = Game1.getMouseX();
        int mouseY       = Game1.getMouseY();

        for (int row = 0; row < visibleRows; row++)
        {
            int outfitIdx = _scrollOffset * cols + row * cols;

            for (int col = 0; col < cols; col++, outfitIdx++)
            {
                if (outfitIdx >= _visibleOutfits.Count)
                    break;

                string name     = _visibleOutfits[outfitIdx];
                Rectangle cell  = GetGridCellRect(row, col);

                bool isSelected = name == _selectedOutfit;
                bool isHovered  = cell.Contains(mouseX, mouseY);

                if (isSelected)
                    b.Draw(Game1.staminaRect, cell, Color.SandyBrown * 0.35f);
                else if (isHovered)
                    b.Draw(Game1.staminaRect, cell, Color.Wheat * 0.25f);

                // In edit/delete modes: show checkbox instead of star icon
                if (_selectMode || _deleteOutfitMode || _scheduleOutfitSelectionMode)
                {
                    bool marked = _selectMode
                        ? _selectedForRemoval.Contains(name)
                        : _deleteOutfitMode
                            ? _selectedForDeletion.Contains(name)
                            : _scheduleSelectedOutfits.Contains(name);
                    Rectangle cb = new(cell.X + 6, cell.Center.Y - 8, 16, 16);
                    b.Draw(Game1.mouseCursors, cb, new Rectangle(227, 425, 9, 9), Color.White, 0f,
                        Vector2.Zero, SpriteEffects.None, 0.88f);
                    if (marked)
                        b.Draw(Game1.mouseCursors, cb, new Rectangle(236, 425, 9, 9), Color.White, 0f,
                            Vector2.Zero, SpriteEffects.None, 0.89f);
                }
                else
                {
                    // Cute pink star marker
                    b.Draw(Game1.mouseCursors,
                        new Rectangle(cell.X + 8, cell.Center.Y - 7, 14, 14),
                        new Rectangle(346, 392, 8, 8),
                        Color.White);
                }

                // Name (truncated)
                string displayName = TruncateString(name, Game1.smallFont, OutfitNameMaxW);
                Utility.drawTextWithShadow(b, displayName, Game1.smallFont,
                    new Vector2(cell.X + 32, cell.Center.Y - Game1.smallFont.MeasureString("A").Y / 2f),
                    isSelected ? Color.SaddleBrown : Game1.textColor);
            }
        }

        // Scroll indicators
        int totalRows2 = (int)Math.Ceiling(_visibleOutfits.Count / (double)cols);
        int maxScroll2 = Math.Max(0, totalRows2 - visibleRows);

        if (_scrollOffset > 0)
            DrawArrowButton(b, ScrollUpArrowBounds(),   LeftArrowSrc,  rotated: true);

        if (_scrollOffset < maxScroll2)
            DrawArrowButton(b, ScrollDownArrowBounds(), RightArrowSrc, rotated: true);

        // Empty state
        if (_visibleOutfits.Count == 0)
        {
            string msg = string.IsNullOrEmpty(_searchText)
                ? I18n.EmptyCategory
                : I18n.EmptySearch;

            Vector2 msz = Game1.smallFont.MeasureString(msg);
            Utility.drawTextWithShadow(b, msg, Game1.smallFont,
                new Vector2(gridArea.Center.X - msz.X / 2f, gridArea.Center.Y - msz.Y / 2f),
                Color.Gray);
        }

        // Trash icon at bottom-right of grid — only when select mode is active
        if (_selectMode && _selectedCategoryId != "all")
        {
            bool trashHovered = _deleteTrashButton.Contains(Game1.getMouseX(), Game1.getMouseY());
            Color trashTint   = trashHovered ? Color.Salmon : Color.White;
            drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                _deleteTrashButton.X, _deleteTrashButton.Y,
                _deleteTrashButton.Width, _deleteTrashButton.Height,
                trashTint, 4f, drawShadow: false);
            b.Draw(Game1.mouseCursors,
                new Rectangle(_deleteTrashButton.Center.X - 10, _deleteTrashButton.Center.Y - 10, 20, 20),
                new Rectangle(323, 433, 9, 9),
                Color.White);
        }
    }

    private void DrawArrowButton(SpriteBatch b, Rectangle bounds, Rectangle src, bool rotated)
    {
        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        Color color  = hovered ? Color.Wheat : Color.White;
        b.Draw(Game1.mouseCursors, bounds, src, color);
    }

    private void DrawPreviewPanel(SpriteBatch b, bool showEquipButton = true)
    {
        // Panel background
        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 373, 18, 18),
            _previewPanel.X, _previewPanel.Y, _previewPanel.Width, _previewPanel.Height,
            Color.White, 4f, drawShadow: true);

        // I18n.Preview label
        SpriteText.drawStringWithScrollCenteredAt(b, I18n.Preview,
            _previewPanel.Center.X,
            _previewPanel.Y + 28);

        // Portrait box background (game-style daybg)
        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 396, 15, 15),
            _portraitBox.X, _portraitBox.Y, _portraitBox.Width, _portraitBox.Height,
            Color.White, 4f, drawShadow: false);

        Rectangle inner = new(
            _portraitBox.X + 16, _portraitBox.Y + 16,
            _portraitBox.Width - 32, _portraitBox.Height - 32);

        b.Draw(Game1.daybg, inner, Color.White);

        // Draw farmer
        _renderer.DrawFarmer(b, inner, _previewFacing, yOffset: -32f);

        // Outfit name label — between portrait and arrows
        if (_selectedOutfit is not null)
        {
            string label = TruncateString(_selectedOutfit, Game1.smallFont, _previewPanel.Width - Padding * 2);
            Vector2 lsz  = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(_previewPanel.Center.X - lsz.X / 2f, _portraitBox.Bottom + 6),
                Game1.textColor);
        }

        // Direction arrows — sit between name label and equip button
        DrawArrowButton(b, _leftArrow,  LeftArrowSrc,  rotated: false);
        DrawArrowButton(b, _rightArrow, RightArrowSrc, rotated: false);

        if (!showEquipButton)
            return;

        // Equip / Confirm button
        bool equipHovered = _equipButton.Contains(Game1.getMouseX(), Game1.getMouseY());
        bool equipAvailable = _scheduleOutfitSelectionMode
            ? _scheduleSelectedOutfits.Count > 0 || _scheduleSelectedTagIds.Count > 0
            : _deleteOutfitMode
            ? _selectedForDeletion.Count > 0
            : _selectedOutfit is not null && _selectedOutfit != GetCurrentOutfitName();

        string buttonLabel = (_deleteOutfitMode || _scheduleOutfitSelectionMode) ? I18n.ButtonConfirm : I18n.ButtonEquip;

        Color equipTint = !equipAvailable ? Color.Gray
                        : equipHovered    ? (_deleteOutfitMode ? Color.Salmon : Color.Wheat)
                                          : Color.White;

        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(432, 439, 9, 9),
            _equipButton.X, _equipButton.Y, _equipButton.Width, _equipButton.Height,
            equipTint, 4f, drawShadow: false);

        Vector2 esz = Game1.smallFont.MeasureString(buttonLabel);
        Utility.drawTextWithShadow(b, buttonLabel, Game1.smallFont,
            new Vector2(_equipButton.Center.X - esz.X / 2f, _equipButton.Center.Y - esz.Y / 2f),
            equipAvailable ? Game1.textColor : Color.DarkGray);
    }

    private void DrawCreationModal(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);

        Rectangle modal = GetModalBounds();
        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 373, 18, 18),
            modal.X, modal.Y, modal.Width, modal.Height,
            Color.White, 4f, drawShadow: true);

        string title = _renamingSchedule ? I18n.ScheduleNameTitle
            : _renamingOutfit ? I18n.ModalRenameOutfit
            : _savingCurrentOutfit ? I18n.ModalSaveCurrentStyle
            : _creatingCategory ? I18n.ModalNewCategory
            : _creatingTagKind == TagKind.Color ? I18n.ModalNewColorTag
            : I18n.ModalNewTag;

        SpriteText.drawStringWithScrollCenteredAt(b, title, modal.Center.X, modal.Y + 40);

        Color borderColor = _namingFocused ? Color.SkyBlue : Color.White;
        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 396, 15, 15),
            _modalTextBox.X, _modalTextBox.Y, _modalTextBox.Width, _modalTextBox.Height,
            borderColor, 4f, drawShadow: false);

        string display = _newItemName + (_namingFocused ? "|" : "");
        Utility.drawTextWithShadow(b, display, Game1.smallFont,
            new Vector2(_modalTextBox.X + 12, _modalTextBox.Center.Y - Game1.smallFont.MeasureString("A").Y / 2f),
            Game1.textColor);

        DrawMenuButton(b, _modalCancelButton,  I18n.ButtonCancel);
        DrawMenuButton(b, _modalConfirmButton, I18n.ButtonConfirm);
    }

    private void DrawAssignMode(SpriteBatch b)
    {
        // Left side: only the tab bar (no Create Category) + search + grid

        // Category tabs row (without Create Category button)
        foreach (var (id, label, bounds) in _categoryTabs)
        {
            bool isDropdown = id == "__dropdown__";
            bool active     = (!isDropdown && id == _selectedCategoryId) || (isDropdown && _selectedCategoryId != "all");
            bool hovered    = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());

            Color tint = active  ? Color.SandyBrown
                       : hovered ? Color.Wheat
                                 : Color.White;

            drawTextureBox(b, Game1.mouseCursors,
                new Rectangle(432, 439, 9, 9),
                bounds.X, bounds.Y, bounds.Width, bounds.Height,
                tint, 4f, drawShadow: false);

            Vector2 sz = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(bounds.Center.X - sz.X / 2f, bounds.Center.Y - sz.Y / 2f),
                active ? Color.White : Game1.textColor);
        }

        DrawSearchBar(b);

        // Instruction banner
        Rectangle assignGrid = GetAssignGrid();
        Rectangle banner = new(assignGrid.X, assignGrid.Y, assignGrid.Width, 36);
        b.Draw(Game1.staminaRect, banner, Color.LightSteelBlue * 0.5f);

        OutfitCategory? pending    = _categories.FirstOrDefault(c => c.Id == _pendingCategoryId);
        OutfitTag?      pendingTag = _tags.FirstOrDefault(t => t.Id == _pendingTagId);

        string instruction;
        if (_assignTagMode && pendingTag is not null)
            instruction = I18n.AssignTagInstruction(pendingTag.Name);
        else
            instruction = I18n.AssignInstruction(pending?.Name ?? I18n.ModalDefaultCategory);

        Vector2 bsz = Game1.smallFont.MeasureString(instruction);
        Utility.drawTextWithShadow(b,
            instruction,
            Game1.smallFont,
            new Vector2(banner.Center.X - bsz.X / 2f, banner.Center.Y - bsz.Y / 2f),
            Game1.textColor);

        // Grid with checkboxes, using the same comfortable inner padding as the normal outfit list.
        Rectangle checkGrid = AssignGridContentArea;
        int cols        = Math.Max(1, checkGrid.Width / GridCellW);
        int visibleRows = checkGrid.Height / GridCellH;

        // Grid background
        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 396, 15, 15),
            _gridArea.X, _gridArea.Y, _gridArea.Width, _gridArea.Height,
            Color.White, 4f, drawShadow: false);

        for (int row = 0; row < visibleRows; row++)
        {
            int outfitIdx = _scrollOffset * cols + row * cols;

            for (int col = 0; col < cols; col++, outfitIdx++)
            {
                if (outfitIdx >= _visibleOutfits.Count)
                    break;

                string name    = _visibleOutfits[outfitIdx];
                Rectangle cell = new(
                    checkGrid.X + col * GridCellW,
                    checkGrid.Y + row * GridCellH,
                    GridCellW, GridCellH);

                bool assigned = _assignTagMode
                    ? (pendingTag?.OutfitNames.Contains(name) ?? false)
                    : (pending?.OutfitNames.Contains(name) ?? false);
                bool hovered  = cell.Contains(Game1.getMouseX(), Game1.getMouseY());

                if (assigned)
                    b.Draw(Game1.staminaRect, cell, Color.SandyBrown * 0.25f);
                else if (hovered)
                    b.Draw(Game1.staminaRect, cell, Color.Wheat * 0.2f);

                // Checkbox
                Rectangle cb = new(cell.X + 8, cell.Center.Y - 8, 16, 16);
                b.Draw(Game1.mouseCursors, cb, new Rectangle(227, 425, 9, 9), Color.White, 0f,
                    Vector2.Zero, SpriteEffects.None, 0.88f);

                if (assigned)
                    b.Draw(Game1.mouseCursors, cb, new Rectangle(236, 425, 9, 9), Color.White, 0f,
                        Vector2.Zero, SpriteEffects.None, 0.89f);

                string displayName = TruncateString(name, Game1.smallFont, OutfitNameMaxW);
                Utility.drawTextWithShadow(b, displayName, Game1.smallFont,
                    new Vector2(cell.X + 32, cell.Center.Y - Game1.smallFont.MeasureString("A").Y / 2f),
                    assigned ? Color.SaddleBrown : Game1.textColor);
            }
        }

        // Right side: preview panel (without Equip — Done is drawn separately)
        DrawPreviewPanel(b, showEquipButton: false);

        // I18n.ButtonDone in place of the Equip button
        DrawMenuButton(b, _equipButton, I18n.ButtonDone);
    }

    /// <summary>The assign-mode grid area (same as _gridArea for consistency).</summary>
    private Rectangle GetAssignGrid() => _gridArea;

    /// <summary>Usable cell area inside the assign-mode grid, leaving room for the banner and borders.</summary>
    private Rectangle AssignGridContentArea
    {
        get
        {
            Rectangle area = GetAssignGrid();
            const int bannerHeight = 40;

            return new Rectangle(
                area.X + GridPaddingX + 8,
                area.Y + bannerHeight + 14,
                area.Width - GridPaddingX * 2 - 8,
                area.Height - bannerHeight - 28
            );
        }
    }

    private void DrawMenuButton(SpriteBatch b, Rectangle bounds, string text, Color? hoverColor = null)
    {
        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        Color tint   = hovered ? (hoverColor ?? Color.Wheat) : Color.White;

        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(432, 439, 9, 9),
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            tint, 4f, drawShadow: false);

        Vector2 sz = Game1.smallFont.MeasureString(text);
        Utility.drawTextWithShadow(b, text, Game1.smallFont,
            new Vector2(bounds.Center.X - sz.X / 2f, bounds.Center.Y - sz.Y / 2f),
            Game1.textColor);
    }

    // Input handlers

    private void HandleModalClick(int x, int y)
    {
        _modalTextBox = GetModalTextBoxBounds();

        if (_modalTextBox.Contains(x, y))
        {
            _namingFocused = true;
            return;
        }

        if (_modalConfirmButton.Contains(x, y))
        {
            ConfirmCategoryCreation();
            return;
        }

        if (_modalCancelButton.Contains(x, y))
        {
            _creatingCategory    = false;
            _creatingTag         = false;
            _savingCurrentOutfit = false;
            _renamingOutfit      = false;
            _renamingSchedule    = false;
            _renameOriginalOutfitName = null;
            _scheduleRenameRuleId = null;
            _newItemName         = string.Empty;
            _namingFocused       = false;
            Game1.playSound("smallSelect");
        }
    }

    private void HandleConfirmModalClick(int x, int y)
    {
        if (_confirmYesButton.Contains(x, y))
        {
            if (_confirmDeleteCategory)
            {
                // Delete the whole category and reset to "all"
                _categoryManager.DeleteCategory(_categories, _selectedCategoryId);
                _selectedCategoryId = "all";
                _selectMode         = false;
                _selectedForRemoval.Clear();
                RebuildCategoryTabs();
                RebuildVisibleOutfits();
                Game1.playSound("trashcan");
            }
            else if (_confirmDeleteOutfits)
            {
                // Remove only the selected outfits from the active category
                OutfitCategory? cat = _categories.FirstOrDefault(c => c.Id == _selectedCategoryId);
                if (cat is not null)
                {
                    foreach (string outfitName in _selectedForRemoval)
                        cat.OutfitNames.Remove(outfitName);
                    _categoryManager.SaveCategories(_categories);
                }
                _selectedForRemoval.Clear();
                _selectMode = false;
                RebuildVisibleOutfits();
                Game1.playSound("trashcan");
            }
            else if (_confirmDeleteSavedOutfits)
            {
                DeleteSelectedSavedOutfits();
                Game1.playSound("trashcan");
            }
            else if (_confirmDeleteTag && _pendingDeleteTagId is not null)
            {
                DeleteTagAndRefresh(_pendingDeleteTagId);
                if (_selectedTagId == _pendingDeleteTagId)
                    _selectedTagId = null;
                _pendingDeleteTagId = null;
                RebuildCategoryTabs();
                RebuildVisibleOutfits();
                Game1.playSound("trashcan");
            }
            else if (_confirmUpdateOutfit && _pendingUpdateOutfitName is not null)
            {
                UpdateSavedOutfit(_pendingUpdateOutfitName);
            }

            _confirmDeleteCategory     = false;
            _confirmDeleteOutfits      = false;
            _confirmDeleteSavedOutfits = false;
            _confirmDeleteTag          = false;
            _confirmUpdateOutfit       = false;
            _pendingUpdateOutfitName   = null;
            return;
        }

        if (_confirmNoButton.Contains(x, y))
        {
            _confirmDeleteCategory     = false;
            _confirmDeleteOutfits      = false;
            _confirmDeleteSavedOutfits = false;
            _confirmDeleteTag          = false;
            _confirmUpdateOutfit       = false;
            _pendingDeleteTagId    = null;
            _pendingUpdateOutfitName = null;
            Game1.playSound("smallSelect");
        }
    }

    private void HandleAssignModeClick(int x, int y)
    {
        // Done button
        if (_equipButton.Contains(x, y))
        {
            if (_assignTagMode)
            {
                _assignTagMode = false;
                _pendingTagId  = null;
                // Reload from modData to guarantee consistency
                _tags = _tagManager.LoadTags();
                _advancedPanel.SetData(_categories, _tags);
                _advancedPanel.UpdateLayout(_gridArea);
                RebuildCategoryTabs();
            }
            else
            {
                _assignMode        = false;
                _pendingCategoryId = null;
                // Reload from modData to guarantee consistency
                _categories = _categoryManager.LoadCategories();
                _advancedPanel.SetData(_categories, _tags);
                RebuildCategoryTabs();
            }
            RebuildVisibleOutfits();
            Game1.playSound("bigSelect");
            return;
        }

        // Search bar focus
        if (_searchBar.Contains(x, y)) { _searchFocused = true; return; }

        // Direction arrows
        if (_leftArrow.Contains(x, y))  { _previewFacing = TurnLeft(_previewFacing);  Game1.playSound("shwip"); return; }
        if (_rightArrow.Contains(x, y)) { _previewFacing = TurnRight(_previewFacing); Game1.playSound("shwip"); return; }

        // Scroll
        if (ScrollUpArrowBounds().Contains(x, y))   { ScrollBy(-1); return; }
        if (ScrollDownArrowBounds().Contains(x, y))  { ScrollBy(1);  return; }

        // Toggle outfit in pending category or tag
        Rectangle checkGrid = AssignGridContentArea;
        int cols = Math.Max(1, checkGrid.Width / GridCellW);

        for (int row = 0; row < checkGrid.Height / GridCellH; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int outfitIdx = _scrollOffset * cols + row * cols + col;
                if (outfitIdx >= _visibleOutfits.Count) break;

                Rectangle cell = new(
                    checkGrid.X + col * GridCellW,
                    checkGrid.Y + row * GridCellH,
                    GridCellW, GridCellH);

                if (!cell.Contains(x, y)) continue;

                string outfitName = _visibleOutfits[outfitIdx];

                if (_assignTagMode && _pendingTagId is not null)
                    _tagManager.ToggleOutfitInTag(_tags, _pendingTagId, outfitName);
                else if (_pendingCategoryId is not null)
                    _categoryManager.ToggleOutfitInCategory(_categories, _pendingCategoryId, outfitName);

                // Apply preview
                if (_renderer.IsPreviewActive) _renderer.RestoreOriginal();
                _selectedOutfit = outfitName;
                _previewFacing  = 2;
                _renderer.ApplyOutfitForPreview(outfitName);

                Game1.playSound("smallSelect");
                return;
            }
        }
    }

    private void HandleSearchKeyPress(Keys key)
    {
        if (key == Keys.Escape || key == Keys.Enter)
        {
            _searchFocused = false;
            return;
        }

        // Character input is handled by OnTextInput (OS-level text input event).
        // Backspace is handled by update() for hold-to-repeat.
    }

    private void HandleNamingKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            _creatingCategory    = false;
            _creatingTag         = false;
            _savingCurrentOutfit = false;
            _renamingOutfit      = false;
            _renamingSchedule    = false;
            _renameOriginalOutfitName = null;
            _scheduleRenameRuleId = null;
            _newItemName         = string.Empty;
            _namingFocused       = false;
            return;
        }

        if (key == Keys.Enter)
        {
            ConfirmCreation();
            return;
        }

        // Character input is handled by OnTextInput (OS-level text input event).
        // Backspace is handled by update() for hold-to-repeat.
    }

    private void StartSavingCurrentOutfit()
    {
        if (_renderer.IsPreviewActive)
            _renderer.RestoreOriginal();

        _savingCurrentOutfit = true;
        _renamingOutfit = false;
        _renamingSchedule = false;
        _scheduleRenameRuleId = null;
        _renameOriginalOutfitName = null;
        _creatingCategory = false;
        _creatingTag = false;
        _createSubmenuOpen = false;
        _searchFocused = false;
        _namingFocused = true;
        _newItemName = string.Empty;
        Game1.playSound("smallSelect");
    }

    private void StartRenamingOutfit(string outfitName)
    {
        _renameContextMenuOpen = false;
        _contextOutfitName = null;
        _renamingOutfit = true;
        _renamingSchedule = false;
        _scheduleRenameRuleId = null;
        _renameOriginalOutfitName = outfitName;
        _savingCurrentOutfit = false;
        _creatingCategory = false;
        _creatingTag = false;
        _createSubmenuOpen = false;
        _searchFocused = false;
        _namingFocused = true;
        _newItemName = outfitName;
        Game1.playSound("smallSelect");
    }

    private void UpdateSavedOutfit(string outfitName)
    {
        // A grid preview temporarily changes the farmer's appearance. Restore the
        // real current style first so the preview itself is never saved by mistake.
        if (_renderer.IsPreviewActive)
            _renderer.RestoreOriginal();

        if (!_renderer.OverrideOutfitWithCurrentAppearance(outfitName))
        {
            Game1.addHUDMessage(new HUDMessage(I18n.ErrorUpdateOutfitFailed, HUDMessage.error_type));
            return;
        }

        _selectedOutfit = outfitName;
        RefreshParentMenu();
        RebuildVisibleOutfits();
        Game1.addHUDMessage(new HUDMessage(I18n.MessageOutfitUpdated));
        Game1.playSound("newArtifact");
    }

    private void StartRenamingSchedule(OutfitScheduleRule rule)
    {
        _renamingSchedule = true;
        _scheduleRenameRuleId = rule.Id;
        _renamingOutfit = false;
        _renameOriginalOutfitName = null;
        _savingCurrentOutfit = false;
        _creatingCategory = false;
        _creatingTag = false;
        _createSubmenuOpen = false;
        _searchFocused = false;
        _namingFocused = true;
        _newItemName = rule.Name;
        Game1.playSound("smallSelect");
    }

    private void SaveCurrentOutfitFromModal()
    {
        string outfitName = _newItemName.Trim();

        if (_allOutfitNames.Any(name => name.Equals(outfitName, StringComparison.OrdinalIgnoreCase)))
        {
            Game1.addHUDMessage(new HUDMessage(I18n.ErrorNameTaken, HUDMessage.error_type));
            return;
        }

        if (!_renderer.SaveCurrentOutfit(outfitName))
        {
            Game1.addHUDMessage(new HUDMessage(I18n.ErrorSaveOutfitFailed, HUDMessage.error_type));
            return;
        }

        _allOutfitNames.Add(outfitName);
        _selectedOutfit = outfitName;
        _previewFacing = 2;
        _savingCurrentOutfit = false;
        _newItemName = string.Empty;
        _namingFocused = false;
        _scrollOffset = 0;
        RefreshParentMenu();
        RebuildVisibleOutfits();
        SelectOutfit(outfitName);
        Game1.playSound("newArtifact");
    }

    private void RenameOutfitFromModal()
    {
        if (_renameOriginalOutfitName is null)
            return;

        string oldName = _renameOriginalOutfitName;
        string newName = _newItemName.Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            Game1.addHUDMessage(new HUDMessage(I18n.ErrorNameEmpty, HUDMessage.error_type));
            return;
        }

        if (!oldName.Equals(newName, StringComparison.OrdinalIgnoreCase)
            && _allOutfitNames.Any(name => name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            Game1.addHUDMessage(new HUDMessage(I18n.ErrorNameTaken, HUDMessage.error_type));
            return;
        }

        if (oldName.Equals(newName, StringComparison.Ordinal))
        {
            _renamingOutfit = false;
            _renameOriginalOutfitName = null;
            _newItemName = string.Empty;
            _namingFocused = false;
            Game1.playSound("smallSelect");
            return;
        }

        bool shouldReapplyPreview = (_selectedOutfit is not null
                && _selectedOutfit.Equals(oldName, StringComparison.OrdinalIgnoreCase))
            || (_renderer.IsPreviewActive
                && _renderer.ActiveOutfitName is not null
                && _renderer.ActiveOutfitName.Equals(oldName, StringComparison.OrdinalIgnoreCase));

        if (shouldReapplyPreview && _renderer.IsPreviewActive)
            _renderer.RestoreOriginal();

        if (!_renderer.RenameOutfit(oldName, newName))
        {
            Game1.addHUDMessage(new HUDMessage(I18n.ErrorRenameOutfitFailed, HUDMessage.error_type));
            return;
        }

        for (int i = 0; i < _allOutfitNames.Count; i++)
        {
            if (_allOutfitNames[i].Equals(oldName, StringComparison.OrdinalIgnoreCase))
                _allOutfitNames[i] = newName;
        }

        foreach (OutfitCategory category in _categories)
        {
            for (int i = 0; i < category.OutfitNames.Count; i++)
            {
                if (category.OutfitNames[i].Equals(oldName, StringComparison.OrdinalIgnoreCase))
                    category.OutfitNames[i] = newName;
            }
        }
        _categoryManager.SaveCategories(_categories);

        foreach (OutfitTag tag in _tags)
        {
            for (int i = 0; i < tag.OutfitNames.Count; i++)
            {
                if (tag.OutfitNames[i].Equals(oldName, StringComparison.OrdinalIgnoreCase))
                    tag.OutfitNames[i] = newName;
            }
        }
        _tagManager.SaveTags(_tags);
        _schedulePanel.RenameOutfit(oldName, newName);

        if (_selectedOutfit is not null && _selectedOutfit.Equals(oldName, StringComparison.OrdinalIgnoreCase))
            _selectedOutfit = newName;

        _renamingOutfit = false;
        _renameOriginalOutfitName = null;
        _newItemName = string.Empty;
        _namingFocused = false;
        _scrollOffset = 0;
        RefreshParentMenu();
        RebuildVisibleOutfits();

        // Fashion Sense can keep the old outfit object cached until the menu is reopened.
        // Reapply through the renderer after rebuilding so the renamed outfit previews
        // immediately, instead of only working after closing and opening the menu again.
        if (shouldReapplyPreview)
        {
            _selectedOutfit = null;
            SelectOutfit(newName);
        }

        Game1.playSound("newArtifact");
    }

    private void ConfirmCategoryCreation() => ConfirmCreation();

    private void ConfirmCreation()
    {
        if (_renamingSchedule)
        {
            if (_scheduleRenameRuleId is not null)
                _schedulePanel.SetRuleName(_scheduleRenameRuleId, _newItemName);

            _renamingSchedule = false;
            _scheduleRenameRuleId = null;
            _newItemName = string.Empty;
            _namingFocused = false;
            Game1.playSound("newArtifact");
            return;
        }

        if (string.IsNullOrWhiteSpace(_newItemName))
        {
            Game1.addHUDMessage(new HUDMessage(
                _creatingTag ? I18n.ErrorTagNameEmpty : I18n.ErrorNameEmpty,
                HUDMessage.error_type));
            return;
        }

        if (_renamingOutfit)
        {
            RenameOutfitFromModal();
            return;
        }

        if (_savingCurrentOutfit)
        {
            SaveCurrentOutfitFromModal();
            return;
        }

        if (_creatingCategory)
        {
            OutfitCategory? created = _categoryManager.AddCategory(_categories, _newItemName);

            if (created is null)
            {
                Game1.addHUDMessage(new HUDMessage(I18n.ErrorNameTaken, HUDMessage.error_type));
                return;
            }

            _creatingCategory  = false;
            _newItemName       = string.Empty;
            _assignMode        = true;
            _pendingCategoryId = created.Id;
            _scrollOffset      = 0;

            _categories = _categoryManager.LoadCategories();
            _advancedPanel.SetData(_categories, _tags);
            RebuildCategoryTabs();
            RebuildVisibleOutfits();
        }
        else if (_creatingTag)
        {
            OutfitTag? created = _tagManager.AddTag(_tags, _newItemName, _creatingTagKind);

            if (created is null)
            {
                Game1.addHUDMessage(new HUDMessage(I18n.ErrorTagNameTaken, HUDMessage.error_type));
                return;
            }

            _creatingTag   = false;
            _newItemName   = string.Empty;
            _assignTagMode = true;
            _pendingTagId  = created.Id;
            _scrollOffset  = 0;

            _tags = _tagManager.LoadTags();
            // Re-find the pending tag id in the freshly loaded list
            _pendingTagId = _tags.FirstOrDefault(t =>
                t.Name.Equals(created.Name, StringComparison.OrdinalIgnoreCase)
                && t.Kind == _creatingTagKind)?.Id ?? created.Id;

            _advancedPanel.SetData(_categories, _tags);
            _advancedPanel.UpdateLayout(_gridArea);
            RebuildCategoryTabs();
            RebuildVisibleOutfits();
        }

        Game1.playSound("newArtifact");
    }

    // Selection / equipping

    private void SelectOutfit(string outfitName)
    {
        if (_selectedOutfit == outfitName)
            return;

        // Restore before applying another preview
        if (_renderer.IsPreviewActive)
            _renderer.RestoreOriginal();

        _selectedOutfit = outfitName;
        _previewFacing  = 2;

        _renderer.ApplyOutfitForPreview(outfitName);
        Game1.playSound("smallSelect");
    }

    private void EquipSelectedOutfit()
    {
        if (_selectedOutfit is null)
            return;

        // Preview is already applied; commit by NOT restoring on close
        Game1.playSound("smallSelect");

        _renderer.CommitPreview();
        CloseMenu(restoreOriginal: false);
    }

    private void DeleteSelectedSavedOutfits()
    {
        if (_selectedForDeletion.Count == 0)
            return;

        List<string> toDelete = _selectedForDeletion
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();

        if (toDelete.Count == 0)
            return;

        if (_renderer.IsPreviewActive
            && _renderer.ActiveOutfitName is not null
            && toDelete.Contains(_renderer.ActiveOutfitName))
        {
            _renderer.RestoreOriginal();
        }

        var deletedNames = new List<string>();

        foreach (string outfitName in toDelete)
        {
            if (_renderer.DeleteOutfit(outfitName))
            {
                deletedNames.Add(outfitName);
                _allOutfitNames.RemoveAll(name => name.Equals(outfitName, StringComparison.Ordinal));
            }
        }

        if (deletedNames.Count == 0)
        {
            _selectedForDeletion.Clear();
            _deleteOutfitMode = false;
            _confirmDeleteSavedOutfits = false;
            RebuildVisibleOutfits();
            return;
        }

        foreach (OutfitCategory category in _categories)
        {
            category.OutfitNames.RemoveAll(name => deletedNames.Contains(name));
        }
        _categoryManager.SaveCategories(_categories);

        foreach (OutfitTag tag in _tags)
        {
            tag.OutfitNames.RemoveAll(name => deletedNames.Contains(name));
        }
        _tagManager.SaveTags(_tags);
        _schedulePanel.RemoveOutfits(deletedNames);

        if (_selectedOutfit is not null && deletedNames.Contains(_selectedOutfit))
            _selectedOutfit = null;

        _selectedForDeletion.Clear();
        _deleteOutfitMode = false;
        _confirmDeleteSavedOutfits = false;
        _scrollOffset = 0;
        _advancedPanel.SetData(_categories, _tags);
        RebuildCategoryTabs();
        RebuildVisibleOutfits();
        RefreshParentMenu();
    }

    // Layout & utilities

    private void UpdateLayout()
    {
        int ox = xPositionOnScreen;
        int oy = yPositionOnScreen;

        // Row 1: save current style + "Create" buttons — below title scroll
        int saveY = oy + 76;
        _saveCurrentStyleButton = new Rectangle(ox + Padding, saveY, 250, CategoryBarH);

        int createY = saveY + CategoryBarH + 6;
        _createCategoryButton = new Rectangle(ox + Padding, createY, 180, CategoryBarH);
        _createButton          = _createCategoryButton;

        // Submenu items drop over the content below. They are handled before underlying buttons.
        int subW = 260;
        int subY = createY + CategoryBarH + 10;
        _createSubNewCategory = new Rectangle(_createButton.X, subY, subW, CategoryBarH);
        _createSubNewTag      = new Rectangle(_createButton.X, subY + CategoryBarH + 4, subW, CategoryBarH);
        _createSubNewColorTag = new Rectangle(_createButton.X, subY + (CategoryBarH + 4) * 2, subW, CategoryBarH);
        _createSubExportOrganization = new Rectangle(_createButton.X, subY + (CategoryBarH + 4) * 3, subW, CategoryBarH);
        _createSubImportOrganization = new Rectangle(_createButton.X, subY + (CategoryBarH + 4) * 4, subW, CategoryBarH);

        // Row 2: category tabs + action buttons (Select / Delete Category)
        int categoryBarY = createY + CategoryBarH + 6;
        _categoryBar = new Rectangle(ox + Padding, categoryBarY, width - PreviewPanelW - Padding * 3, CategoryBarH);

        // Side tab for advanced filters, placed on the left side of the window like vanilla tabs.
        _advancedButton = new Rectangle(Math.Max(4, ox - 54), oy + 104, 52, 58);
        _schedulePanel.UpdateLayout(new Rectangle(ox, oy, width, height));

        // Main delete button and edit/delete buttons (placed at the far right of the category row).
        int actionRight = _categoryBar.Right;
        _deleteOutfitsButton  = new Rectangle(actionRight - 126, categoryBarY, 122, CategoryBarH);
        _deleteCategoryButton = new Rectangle(actionRight - 242, categoryBarY, 238, CategoryBarH);
        _selectModeButton     = new Rectangle(_deleteCategoryButton.X - 132, categoryBarY, 124, CategoryBarH);

        // When advanced filters are open, the search box is hidden and the filter panel starts higher.
        int searchY = categoryBarY + CategoryBarH + 8;
        _searchBar = _advancedPanel.IsOpen
            ? Rectangle.Empty
            : new Rectangle(ox + Padding, searchY, width - PreviewPanelW - Padding * 3, SearchBarH);

        // Grid — below search normally, or directly below the top controls when advanced filters are open.
        int gridY = _advancedPanel.IsOpen
            ? oy + 104
            : searchY + SearchBarH + 8;
        _gridArea = new Rectangle(
            ox + Padding,
            gridY,
            width - PreviewPanelW - Padding * 3,
            oy + height - gridY - Padding);

        // Trash icon lives at bottom-right of the grid area
        _deleteTrashButton = new Rectangle(
            _gridArea.Right - 48,
            _gridArea.Bottom - 48,
            40, 40);

        // Right preview panel
        _previewPanel = new Rectangle(
            ox + width - PreviewPanelW - Padding,
            oy + Padding + 52,
            PreviewPanelW,
            height - Padding * 2 - 52);

        // Equip button anchored near the bottom of the preview panel
        int equipY = _previewPanel.Bottom - 68;
        _equipButton = new Rectangle(
            _previewPanel.Center.X - 80,
            equipY,
            160, 52);

        // Direction arrows sit ABOVE the equip button (horizontally centred, below portrait name)
        int arrowY = equipY - 48;
        _leftArrow  = new Rectangle(_previewPanel.Center.X - 88, arrowY, 40, 36);
        _rightArrow = new Rectangle(_previewPanel.Center.X + 48, arrowY, 40, 36);

        // Portrait box: larger and pinned so its bottom clears the arrows
        // Layout from bottom: [equipButton 52px] [gap 8px] [arrows 36px] [gap 8px] [name 28px] [portrait]
        int nameH    = 28;
        int gapArrow = 8;
        int pbBottom = arrowY - gapArrow - nameH;
        int pbX = _previewPanel.Center.X - PortraitBoxW / 2;
        int pbY = pbBottom - PortraitBoxH;
        _portraitBox = new Rectangle(pbX, pbY, PortraitBoxW, PortraitBoxH);

        // Modal buttons: Cancel (left) — Confirm (right)
        Rectangle modal = GetModalBounds();
        _modalTextBox       = GetModalTextBoxBounds();
        _modalCancelButton  = new Rectangle(modal.Center.X - 130, modal.Bottom - 68, 120, 48);
        _modalConfirmButton = new Rectangle(modal.Center.X + 10,  modal.Bottom - 68, 120, 48);

        // Confirm modal buttons
        Rectangle confirmModal = GetConfirmModalBounds();
        _confirmYesButton = new Rectangle(confirmModal.Center.X - 120, confirmModal.Bottom - 72, 110, 48);
        _confirmNoButton  = new Rectangle(confirmModal.Center.X + 10,  confirmModal.Bottom - 72, 110, 48);

        _assignDoneButton = _equipButton;

        // Wire advanced filter panel layout (uses gridArea inline)
        _advancedPanel.SetData(_categories, _tags);
        _advancedPanel.UpdateLayout(_gridArea);
        _schedulePanel.SetTags(_tags);

        RebuildCategoryTabs();
    }

    private void RebuildCategoryTabs()
    {
        _categoryTabs.Clear();

        int tabX = _categoryBar.X;
        int tabY = _categoryBar.Y;

        // Fixed I18n.ButtonAll tab
        int todasW = Math.Max(80, (int)Game1.smallFont.MeasureString(I18n.ButtonAll).X + 24);
        _categoryTabs.Add(("all", I18n.ButtonAll, new Rectangle(tabX, tabY, todasW, CategoryBarH)));
        tabX += todasW + 6;

        OutfitTag? selectedTag = SelectedTag;

        // When a category/tag/color is active, show only that active selector plus Edit/Delete.
        // The other selectors stay hidden to avoid mixing editing modes.
        if (!_scheduleOutfitSelectionMode && IsCustomCategorySelected)
        {
            string dropLabel = _categories.FirstOrDefault(c => c.Id == _selectedCategoryId)?.Name ?? I18n.ButtonCategory;
            int dropW = Math.Min(220, Math.Max(132, (int)Game1.smallFont.MeasureString(dropLabel).X + 32));
            string shownDropLabel = TruncateString(dropLabel, Game1.smallFont, dropW - 24);
            _categoryTabs.Add(("__dropdown__", shownDropLabel, new Rectangle(tabX, tabY, dropW, CategoryBarH)));
            return;
        }

        if (!_scheduleOutfitSelectionMode && selectedTag is not null)
        {
            string dropdownId = selectedTag.Kind == TagKind.Color ? "__color_dropdown__" : "__tag_dropdown__";
            int minW = selectedTag.Kind == TagKind.Color ? 100 : 90;
            int tagW = Math.Min(220, Math.Max(minW, (int)Game1.smallFont.MeasureString(selectedTag.Name).X + 32));
            string shownTagLabel = TruncateString(selectedTag.Name, Game1.smallFont, tagW - 24);
            _categoryTabs.Add((dropdownId, shownTagLabel, new Rectangle(tabX, tabY, tagW, CategoryBarH)));
            return;
        }

        // Main/all view: show all selectors.
        if (_categories.Count > 0)
        {
            string dropLabel = _scheduleOutfitSelectionMode && IsCustomCategorySelected
                ? _categories.FirstOrDefault(c => c.Id == _selectedCategoryId)?.Name ?? I18n.ButtonCategory
                : I18n.ButtonCategory;
            int dropW = Math.Min(220, Math.Max(132, (int)Game1.smallFont.MeasureString(dropLabel).X + 32));
            string shownDropLabel = TruncateString(dropLabel, Game1.smallFont, dropW - 24);
            _categoryTabs.Add(("__dropdown__", shownDropLabel, new Rectangle(tabX, tabY, dropW, CategoryBarH)));
            tabX += dropW + 6;
        }

        if (_tags.Any(t => t.Kind == TagKind.General))
        {
            string tagLabel = _scheduleOutfitSelectionMode && selectedTag?.Kind == TagKind.General
                ? selectedTag.Name
                : I18n.ButtonTags;
            int tagW = Math.Min(220, Math.Max(90, (int)Game1.smallFont.MeasureString(tagLabel).X + 28));
            string shownTagLabel = TruncateString(tagLabel, Game1.smallFont, tagW - 20);
            _categoryTabs.Add(("__tag_dropdown__", shownTagLabel, new Rectangle(tabX, tabY, tagW, CategoryBarH)));
            tabX += tagW + 6;
        }

        if (_tags.Any(t => t.Kind == TagKind.Color))
        {
            string colorLabel = _scheduleOutfitSelectionMode && selectedTag?.Kind == TagKind.Color
                ? selectedTag.Name
                : I18n.ButtonColors;
            int colorW = Math.Min(220, Math.Max(100, (int)Game1.smallFont.MeasureString(colorLabel).X + 28));
            string shownColorLabel = TruncateString(colorLabel, Game1.smallFont, colorW - 20);
            _categoryTabs.Add(("__color_dropdown__", shownColorLabel, new Rectangle(tabX, tabY, colorW, CategoryBarH)));
        }
    }

    private void RebuildVisibleOutfits()
    {
        IEnumerable<string> outfits = _allOutfitNames;

        // During category/tag assignment, always show all outfits.
        // Otherwise, creating a second category/tag while a category or advanced filter is active
        // can make the assignment screen look empty.
        if (!(_assignMode || _assignTagMode))
        {
            // Category filter
            // When the advanced tab is open, it owns the category filter.
            // This makes the advanced view start from all outfits, then narrow down only by its own controls.
            string catId = _advancedPanel.IsOpen
                ? _advancedPanel.FilterCategoryId
                : _selectedCategoryId;

            if (catId != "all")
            {
                OutfitCategory? cat = _categories.FirstOrDefault(c => c.Id == catId);
                outfits = cat is not null
                    ? outfits.Where(o => cat.OutfitNames.Contains(o))
                    : Enumerable.Empty<string>();
            }

            // Top-level tag/color selection filter.
            // This is separate from the advanced filter panel.
            if (!_advancedPanel.IsOpen && _selectedTagId is not null)
            {
                OutfitTag? selectedTag = _tags.FirstOrDefault(t => t.Id == _selectedTagId);
                outfits = selectedTag is not null
                    ? outfits.Where(o => selectedTag.OutfitNames.Contains(o))
                    : Enumerable.Empty<string>();
            }
        }

        // Tag AND filter
        var requiredTagIds   = (!_assignMode && !_assignTagMode && _advancedPanel.IsOpen) ? _advancedPanel.FilterTagIds      : new HashSet<string>();
        var requiredColorIds = (!_assignMode && !_assignTagMode && _advancedPanel.IsOpen) ? _advancedPanel.FilterColorTagIds : new HashSet<string>();

        if (requiredTagIds.Count > 0)
        {
            outfits = outfits.Where(o =>
                requiredTagIds.All(tid =>
                    _tags.FirstOrDefault(t => t.Id == tid)?.OutfitNames.Contains(o) ?? false));
        }

        if (requiredColorIds.Count > 0)
        {
            outfits = outfits.Where(o =>
                requiredColorIds.All(tid =>
                    _tags.FirstOrDefault(t => t.Id == tid)?.OutfitNames.Contains(o) ?? false));
        }

        // Text search
        // While the advanced filter panel is open, the search bar is hidden — any
        // leftover text from before opening it should be ignored, not used to filter.
        if (!_advancedPanel.IsOpen && !string.IsNullOrWhiteSpace(_searchText))
            outfits = outfits.Where(o => o.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        _visibleOutfits = outfits.ToList();
    }

    private void ScrollBy(int delta)
    {
        Rectangle content = CurrentScrollContentArea;
        int cols        = Math.Max(1, content.Width / GridCellW);
        int totalRows   = (int)Math.Ceiling(_visibleOutfits.Count / (double)cols);
        int visibleRows = content.Height / GridCellH;
        int maxScroll   = Math.Max(0, totalRows - visibleRows);

        int oldOffset = _scrollOffset;
        _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, maxScroll);

        if (_scrollOffset != oldOffset)
            Game1.playSound("shiny4");
    }

    private int HitTestGridRow(int x, int y)
    {
        Rectangle content = GridContentArea;
        int cols        = Math.Max(1, content.Width / GridCellW);
        int visibleRows = content.Height / GridCellH;

        for (int row = 0; row < visibleRows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int outfitIdx = _scrollOffset * cols + row * cols + col;

                if (outfitIdx >= _visibleOutfits.Count)
                    return -1;

                if (GetGridCellRect(row, col).Contains(x, y))
                    return outfitIdx;
            }
        }

        return -1;
    }

    private bool IsAssignmentModeActive => _assignMode || _assignTagMode;

    private Rectangle CurrentScrollFrameArea => IsAssignmentModeActive
        ? GetAssignGrid()
        : OutfitGridArea;

    private Rectangle CurrentScrollContentArea => IsAssignmentModeActive
        ? AssignGridContentArea
        : GridContentArea;

    /// <summary>The outfit grid area. When advanced filters are open, the filter strip uses the top part.</summary>
    private Rectangle OutfitGridArea
    {
        get
        {
            if (!_advancedPanel.IsOpen)
                return _gridArea;

            int topOffset = AdvancedFilterPanel.PanelHeight + AdvancedFilterPanel.PanelGap;
            return new Rectangle(
                _gridArea.X,
                _gridArea.Y + topOffset,
                _gridArea.Width,
                Math.Max(0, _gridArea.Height - topOffset));
        }
    }

    /// <summary>The usable area inside the grid border where cells are drawn.</summary>
    private Rectangle GridContentArea
    {
        get
        {
            Rectangle area = OutfitGridArea;
            return new Rectangle(
                area.X + GridPaddingX + 8,    // extra right shift
                area.Y + 14,                  // extra downward shift
                area.Width  - GridPaddingX * 2 - 8,
                area.Height - 28);
        }
    }

    private Rectangle GetGridCellRect(int row, int col)
    {
        Rectangle content = GridContentArea;
        return new(content.X + col * GridCellW, content.Y + row * GridCellH, GridCellW, GridCellH);
    }

    private Rectangle ScrollUpArrowBounds()
    {
        Rectangle area = CurrentScrollFrameArea;
        return new Rectangle(area.Right - 28, area.Y + 4, 24, 24);
    }

    private Rectangle ScrollDownArrowBounds()
    {
        Rectangle area = CurrentScrollFrameArea;
        return new Rectangle(area.Right - 28, area.Bottom - 28, 24, 24);
    }

    private Rectangle GetConfirmModalBounds()
    {
        const int mw = 560, mh = 280;
        return new Rectangle(
            Game1.uiViewport.Width  / 2 - mw / 2,
            Game1.uiViewport.Height / 2 - mh / 2,
            mw, mh);
    }

    private void DrawConfirmModal(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);

        Rectangle modal = GetConfirmModalBounds();

        drawTextureBox(b, Game1.mouseCursors,
            new Rectangle(384, 373, 18, 18),
            modal.X, modal.Y, modal.Width, modal.Height,
            Color.White, 4f, drawShadow: true);

        string title;
        string msg;

        if (_confirmUpdateOutfit && _pendingUpdateOutfitName is not null)
        {
            title = I18n.ConfirmUpdateOutfitTitle;
            msg   = I18n.ConfirmUpdateOutfitMsg(_pendingUpdateOutfitName);
        }
        else if (_confirmDeleteCategory)
        {
            title = I18n.ConfirmDeleteCategoryTitle;
            msg   = I18n.ConfirmDeleteCategoryMsg;
        }
        else if (_confirmDeleteSavedOutfits)
        {
            title = I18n.ConfirmDeleteSavedOutfitsTitle;
            msg   = I18n.ConfirmDeleteSavedOutfitsMsg(_selectedForDeletion.Count);
        }
        else if (_confirmDeleteTag)
        {
            OutfitTag? pendingDeleteTag = _tags.FirstOrDefault(t => t.Id == _pendingDeleteTagId);
            bool isColor = pendingDeleteTag?.Kind == TagKind.Color;
            title = isColor ? I18n.ConfirmDeleteColorTitle : I18n.ConfirmDeleteTagTitle;
            msg   = isColor ? I18n.ConfirmDeleteColorMsg   : I18n.ConfirmDeleteTagMsg;
        }
        else
        {
            title = I18n.ConfirmRemoveOutfitsTitle;
            msg   = I18n.ConfirmRemoveOutfitsMsg(_selectedForRemoval.Count);
        }

        SpriteText.drawStringWithScrollCenteredAt(b, title, modal.Center.X, modal.Y + 36);

        Vector2 msz = Game1.smallFont.MeasureString(msg);
        Utility.drawTextWithShadow(b, msg, Game1.smallFont,
            new Vector2(modal.Center.X - msz.X / 2f, modal.Y + 128),
            Game1.textColor);

        DrawMenuButton(b, _confirmYesButton, I18n.ButtonYes);
        DrawMenuButton(b, _confirmNoButton,  I18n.ButtonNo);
    }

    private Rectangle GetModalBounds()
    {
        const int mw = 640, mh = 290;
        return new Rectangle(
            Game1.uiViewport.Width  / 2 - mw / 2,
            Game1.uiViewport.Height / 2 - mh / 2,
            mw, mh);
    }

    private Rectangle GetModalTextBoxBounds()
    {
        Rectangle modal = GetModalBounds();
        // No label above — textbox sits about 60% down the modal
        return new Rectangle(modal.X + 32, modal.Y + 148, modal.Width - 64, 44);
    }

    private void CloseMenu(bool restoreOriginal = true)
    {
        if (restoreOriginal && _renderer.IsPreviewActive)
            _renderer.RestoreOriginal();

        UnhookTextInput();
        RefreshParentMenu();

        Game1.playSound("smallSelect");

        IClickableMenu? returnMenu = GetFashionSenseMainMenuFromParent();

        if (returnMenu is not null)
        {
            Game1.activeClickableMenu = returnMenu;
        }
        else if (_returnMenu is not null)
        {
            // Fallback: give Fashion Sense's outfits menu its normal Escape press.
            // In Fashion Sense this should return to the Hand Mirror / main menu
            // instead of leaving the player on the saved outfits list.
            ModEntry.SuppressNextAutoOpen = true;
            Game1.activeClickableMenu = _returnMenu;
            TryAskParentMenuToReturnToMainMenu();
        }
        else
        {
            Game1.activeClickableMenu = null;
        }
    }

    private IClickableMenu? GetFashionSenseMainMenuFromParent()
    {
        if (_returnMenu is null)
            return null;

        // Fashion Sense's outfits screen is opened from the Hand Mirror / main
        // Fashion Sense menu. The exact field name is internal to Fashion Sense,
        // so we look for an IClickableMenu reference on the outfits menu and use
        // that as the return target when available.
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static bool IsValidReturnMenu(IClickableMenu? candidate, IClickableMenu parentMenu)
        {
            return candidate is not null
                && !ReferenceEquals(candidate, parentMenu)
                && candidate is not ExpandedOutfitsMenu;
        }

        string[] likelyNames =
        {
            "parentMenu", "_returnMenu", "ParentMenu",
            "previousMenu", "_previousMenu", "PreviousMenu",
            "sourceMenu", "_sourceMenu", "SourceMenu",
            "rootMenu", "_rootMenu", "RootMenu",
            "mainMenu", "_mainMenu", "MainMenu",
            "handMirrorMenu", "_handMirrorMenu", "HandMirrorMenu",
            "fashionSenseMenu", "_fashionSenseMenu", "FashionSenseMenu"
        };

        Type parentType = _returnMenu.GetType();

        foreach (string name in likelyNames)
        {
            FieldInfo? field = parentType.GetField(name, flags);
            if (field?.GetValue(_returnMenu) is IClickableMenu fieldMenu
                && IsValidReturnMenu(fieldMenu, _returnMenu))
            {
                return fieldMenu;
            }

            PropertyInfo? property = parentType.GetProperty(name, flags);
            if (property?.GetValue(_returnMenu) is IClickableMenu propertyMenu
                && IsValidReturnMenu(propertyMenu, _returnMenu))
            {
                return propertyMenu;
            }
        }

        foreach (FieldInfo field in parentType.GetFields(flags))
        {
            if (!typeof(IClickableMenu).IsAssignableFrom(field.FieldType))
                continue;

            if (field.GetValue(_returnMenu) is IClickableMenu fieldMenu
                && IsValidReturnMenu(fieldMenu, _returnMenu))
            {
                return fieldMenu;
            }
        }

        foreach (PropertyInfo property in parentType.GetProperties(flags))
        {
            if (!typeof(IClickableMenu).IsAssignableFrom(property.PropertyType))
                continue;

            if (property.GetIndexParameters().Length > 0)
                continue;

            try
            {
                if (property.GetValue(_returnMenu) is IClickableMenu propertyMenu
                    && IsValidReturnMenu(propertyMenu, _returnMenu))
                {
                    return propertyMenu;
                }
            }
            catch
            {
                // Some internal properties may throw when accessed. Ignore them.
            }
        }

        return null;
    }

    private void TryAskParentMenuToReturnToMainMenu()
    {
        if (_returnMenu is null)
            return;

        try
        {
            _returnMenu.receiveKeyPress(Keys.Escape);
        }
        catch
        {
            // Best-effort fallback only. If Fashion Sense changes internally,
            // the reflected main menu path above is still preferred.
        }
    }

    private void RefreshParentMenu()
    {
        if (_returnMenu is null)
            return;

        string[] methodNames =
        {
            "ResetOutfitsMenu",
            "RebuildOutfitsMenu",
            "RefreshOutfitsMenu",
            "PopulateOutfitButtons",
            "populateOutfitButtons",
            "PopulateButtons",
            "populateButtons",
            "SetUpPositions",
            "setUpPositions",
            "Repopulate",
            "repopulate"
        };

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (string methodName in methodNames)
        {
            MethodInfo? method = _returnMenu.GetType().GetMethod(methodName, flags, null, Type.EmptyTypes, null);

            if (method is null)
                continue;

            try
            {
                method.Invoke(_returnMenu, null);
                return;
            }
            catch
            {
                // Best-effort refresh only; the saved outfit is still stored by Fashion Sense.
            }
        }
    }

    private void UnhookTextInput()
    {
        if (!_textInputHooked)
            return;

        Game1.game1.Window.TextInput -= OnTextInput;
        _textInputHooked = false;
    }

    // Misc helpers

    private static int TurnLeft(int d) => d switch
    {
        2 => 3, 3 => 0, 0 => 1, 1 => 2, _ => 2
    };

    private static int TurnRight(int d) => d switch
    {
        2 => 1, 1 => 0, 0 => 3, 3 => 2, _ => 2
    };

    private static string TruncateString(string s, SpriteFont font, int maxWidth)
    {
        if (font.MeasureString(s).X <= maxWidth)
            return s;

        while (s.Length > 1 && font.MeasureString(s + "…").X > maxWidth)
            s = s[..^1];

        return s + "…";
    }

    private static string? GetCurrentOutfitName()
    {
        // Try to read the active outfit name from FS modData
        if (Game1.player.modData.TryGetValue("FashionSense.CurrentOutfit", out string? name))
            return name;

        return null;
    }
}
