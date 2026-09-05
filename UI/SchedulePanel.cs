using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FashionSenseWardrobeManager;

internal sealed class SchedulePanel
{
    private const int SideTabW = 52;
    private const int SideTabH = 58;
    private const int SideTabGap = 4;
    private static readonly Rectangle LeftArrowSource = new(352, 495, 12, 11);
    private static readonly Rectangle RightArrowSource = new(365, 495, 12, 11);

    private readonly ScheduleManager _manager;
    private readonly ScheduleEvaluator _evaluator;
    private readonly ScheduleConditionCatalog _conditionCatalog;
    private List<OutfitScheduleRule> _rules;
    private List<OutfitTag> _tags;
    private readonly Dictionary<ScheduleSection, Rectangle> _sideTabs = new();
    private readonly Dictionary<string, Rectangle> _ruleCards = new();
    private readonly Dictionary<int, Rectangle> _dayButtons = new();
    private readonly Dictionary<int, Rectangle> _timeChips = new();
    private readonly Dictionary<string, Rectangle> _conditionPickerRows = new(StringComparer.OrdinalIgnoreCase);

    private Rectangle _contentArea;
    private Rectangle _ruleListArea;
    private Rectangle _ruleListViewport;
    private Rectangle _ruleScrollTrack;
    private Rectangle _ruleScrollThumb;
    private Rectangle _editorArea;
    private Rectangle _editorViewport;
    private Rectangle _editorScrollTrack;
    private Rectangle _editorScrollThumb;
    private Rectangle _addButton;
    private Rectangle _enabledButton;
    private Rectangle _nameButton;
    private Rectangle _changeFrequencyButton;
    private Rectangle _dayModeButton;
    private Rectangle _singleDayDownButton;
    private Rectangle _singleDayUpButton;
    private Rectangle _weatherButton;
    private Rectangle _festivalButton;
    private Rectangle _periodButton;
    private Rectangle _locationButton;
    private Rectangle _timeDownButton;
    private Rectangle _timeUpButton;
    private Rectangle _addTimeButton;
    private Rectangle _selectOutfitsButton;
    private Rectangle _deleteButton;
    private Rectangle _conditionPickerBounds;
    private Rectangle _conditionPickerViewport;
    private Rectangle _conditionPickerScrollTrack;
    private Rectangle _conditionPickerScrollThumb;
    private Rectangle _conditionPickerClearButton;
    private Rectangle _conditionPickerDoneButton;
    private Rectangle _showCustomFestivalsButton;
    private int _ruleScroll;
    private int _ruleVisibleCount = 1;
    private int _editorScrollPixels;
    private int _editorMaxScroll;
    private ScrollbarDragTarget _draggingScrollbar;
    private int _scrollbarDragOffsetY;

    private enum ScrollbarDragTarget
    {
        None,
        Rules,
        Editor,
        ConditionPicker
    }
    private enum ConditionPickerKind
    {
        None,
        Weather,
        Location,
        Festival
    }
    private ConditionPickerKind _conditionPickerKind;
    private List<ScheduleConditionOption> _conditionPickerOptions = new();
    private int _conditionPickerScroll;
    private int _conditionPickerVisibleCount = 1;
    private bool _showCustomFestivals;
    private int _draftTime = 2200;
    private int _heldTimeDirection;
    private double _timeHoldElapsedMs;
    private double _timeHoldRepeatMs;
    private const double TimeHoldInitialDelayMs = 350;
    private const double TimeHoldRepeatMs = 70;
    private string? _selectedRuleId;
    private string? _outfitSelectionRuleId;
    private string? _nameEditRuleId;

    public ScheduleSection? ActiveSection { get; private set; }
    public bool IsOpen => ActiveSection.HasValue;
    public string CurrentTitle => ActiveSection is null
        ? I18n.Title
        : I18n.ScheduleTitle(SectionLabel(ActiveSection.Value));

    public SchedulePanel(
        ScheduleManager manager,
        ScheduleEvaluator evaluator,
        ScheduleConditionCatalog conditionCatalog,
        IEnumerable<OutfitTag> tags)
    {
        _manager = manager;
        _evaluator = evaluator;
        _conditionCatalog = conditionCatalog;
        _tags = tags.ToList();
        _rules = manager.LoadRules();
        bool normalizedRules = false;
        foreach (OutfitScheduleRule rule in _rules)
        {
            normalizedRules |= rule.LocationIds.RemoveAll(
                id => !_conditionCatalog.IsSupportedLocationId(id)) > 0;
            if (rule.Section == ScheduleSection.Festivals)
            {
                normalizedRules |= rule.DayMode != ScheduleDayMode.All
                    || rule.Days.Count > 0
                    || rule.WeatherIds.Count > 0
                    || rule.LocationIds.Count > 0;
                rule.DayMode = ScheduleDayMode.All;
                rule.Days.Clear();
                rule.WeatherIds.Clear();
                rule.LocationIds.Clear();
            }
        }
        if (normalizedRules)
            _manager.SaveRules(_rules);
    }

    public void UpdateLayout(Rectangle windowBounds)
    {
        int tabX = Math.Max(4, windowBounds.X - 54);
        int tabY = windowBounds.Y + 168;
        _sideTabs.Clear();

        foreach (ScheduleSection section in Enum.GetValues<ScheduleSection>())
        {
            _sideTabs[section] = new Rectangle(tabX, tabY, SideTabW, SideTabH);
            tabY += SideTabH + SideTabGap;
        }

        _contentArea = new Rectangle(
            windowBounds.X + 32,
            windowBounds.Y + 92,
            windowBounds.Width - 64,
            windowBounds.Height - 124);

        _ruleListArea = new Rectangle(_contentArea.X, _contentArea.Y, 330, _contentArea.Height);
        _editorArea = new Rectangle(
            _ruleListArea.Right + 16,
            _contentArea.Y,
            _contentArea.Right - _ruleListArea.Right - 16,
            _contentArea.Height);
        int addButtonWidth = Math.Min(240, _ruleListArea.Width - 24);
        _addButton = new Rectangle(
            _ruleListArea.Center.X - addButtonWidth / 2,
            _ruleListArea.Y + 16,
            addButtonWidth,
            44);
    }

    public void DrawSideTabs(SpriteBatch b)
    {
        int mouseX = Game1.getMouseX();
        int mouseY = Game1.getMouseY();

        foreach ((ScheduleSection section, Rectangle bounds) in _sideTabs)
        {
            bool active = ActiveSection == section;
            bool hovered = bounds.Contains(mouseX, mouseY);
            Color tint = active ? Color.SandyBrown : hovered ? Color.Wheat : Color.White;

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                bounds.X, bounds.Y, bounds.Width, bounds.Height, tint, 4f, drawShadow: true);

            const int iconSize = 32;
            Rectangle destination = new(
                bounds.Center.X - iconSize / 2,
                bounds.Center.Y - iconSize / 2,
                iconSize,
                iconSize);
            DrawSectionIcon(b, section, destination);

            if (hovered)
                IClickableMenu.drawHoverText(b, SectionLabel(section), Game1.smallFont);
        }
    }

    public bool HandleSideTabClick(int x, int y)
    {
        foreach ((ScheduleSection section, Rectangle bounds) in _sideTabs)
        {
            if (!bounds.Contains(x, y))
                continue;

            ActiveSection = section;
            CloseConditionPicker();
            OutfitScheduleRule? first = RulesForActiveSection().FirstOrDefault();
            _selectedRuleId = first?.Id;
            _ruleScroll = 0;
            Game1.playSound("smallSelect");
            return true;
        }

        return false;
    }

    public void Close()
    {
        ReleaseTimeHold();
        CloseConditionPicker();
        ActiveSection = null;
        _selectedRuleId = null;
        _outfitSelectionRuleId = null;
        _nameEditRuleId = null;
    }

    public void Draw(SpriteBatch b)
    {
        if (ActiveSection is null)
            return;

        DrawRuleList(b);
        DrawEditor(b);
        if (_conditionPickerKind != ConditionPickerKind.None)
            DrawConditionPicker(b);
    }

    public bool HandleClick(int x, int y)
    {
        if (ActiveSection is null)
            return false;

        if (_conditionPickerKind != ConditionPickerKind.None)
            return HandleConditionPickerClick(x, y);

        if (_addButton.Contains(x, y))
        {
            OutfitScheduleRule rule = new() { Section = ActiveSection.Value };
            _rules.Add(rule);
            _selectedRuleId = rule.Id;
            _ruleScroll = Math.Max(0, RulesForActiveSection().Count() - _ruleVisibleCount);
            Save();
            Game1.playSound("newArtifact");
            return true;
        }

        int ruleCount = RulesForActiveSection().Count();
        int maxRuleScroll = Math.Max(0, ruleCount - _ruleVisibleCount);
        if (maxRuleScroll > 0 && _ruleScrollThumb.Contains(x, y))
        {
            _draggingScrollbar = ScrollbarDragTarget.Rules;
            _scrollbarDragOffsetY = y - _ruleScrollThumb.Y;
            return true;
        }
        if (maxRuleScroll > 0 && _ruleScrollTrack.Contains(x, y))
        {
            float usable = Math.Max(1, _ruleScrollTrack.Height - _ruleScrollThumb.Height);
            float ratio = Math.Clamp((y - _ruleScrollTrack.Y - _ruleScrollThumb.Height / 2f) / usable, 0f, 1f);
            _ruleScroll = (int)Math.Round(ratio * maxRuleScroll);
            Game1.playSound("shwip");
            return true;
        }

        if (_editorMaxScroll > 0 && _editorScrollThumb.Contains(x, y))
        {
            _draggingScrollbar = ScrollbarDragTarget.Editor;
            _scrollbarDragOffsetY = y - _editorScrollThumb.Y;
            return true;
        }
        if (_editorMaxScroll > 0 && _editorScrollTrack.Contains(x, y))
        {
            float usable = Math.Max(1, _editorScrollTrack.Height - _editorScrollThumb.Height);
            float ratio = Math.Clamp((y - _editorScrollTrack.Y - _editorScrollThumb.Height / 2f) / usable, 0f, 1f);
            _editorScrollPixels = (int)Math.Round(ratio * _editorMaxScroll);
            Game1.playSound("shwip");
            return true;
        }

        foreach ((string id, Rectangle bounds) in _ruleCards)
        {
            if (!bounds.Contains(x, y))
                continue;

            _selectedRuleId = id;
            Game1.playSound("smallSelect");
            return true;
        }

        OutfitScheduleRule? selected = SelectedRule;
        if (selected is null)
            return _contentArea.Contains(x, y);

        if (!_editorViewport.Contains(x, y))
            return _contentArea.Contains(x, y);

        if (_enabledButton.Contains(x, y))
            selected.Enabled = !selected.Enabled;
        else if (_nameButton.Contains(x, y))
        {
            _nameEditRuleId = selected.Id;
            Game1.playSound("smallSelect");
            return true;
        }
        else if (_changeFrequencyButton.Contains(x, y))
        {
            selected.ChangeFrequency = Next(selected.ChangeFrequency);
            selected.LockedOutfitName = string.Empty;
            selected.LockedDayKey = string.Empty;
        }
        else if (_festivalButton.Contains(x, y))
        {
            OpenConditionPicker(ConditionPickerKind.Festival, selected);
            Game1.playSound("smallSelect");
            return true;
        }
        else if (_dayModeButton.Contains(x, y))
            selected.DayMode = Next(selected.DayMode);
        else if (_singleDayDownButton.Contains(x, y))
            selected.SingleDay = selected.SingleDay <= 1 ? 28 : selected.SingleDay - 1;
        else if (_singleDayUpButton.Contains(x, y))
            selected.SingleDay = selected.SingleDay >= 28 ? 1 : selected.SingleDay + 1;
        else if (_weatherButton.Contains(x, y))
        {
            OpenConditionPicker(ConditionPickerKind.Weather, selected);
            Game1.playSound("smallSelect");
            return true;
        }
        else if (_periodButton.Contains(x, y))
            selected.Period = Next(selected.Period);
        else if (_locationButton.Contains(x, y))
        {
            OpenConditionPicker(ConditionPickerKind.Location, selected);
            Game1.playSound("smallSelect");
            return true;
        }
        else if (_timeDownButton.Contains(x, y))
            StartTimeHold(-1);
        else if (_timeUpButton.Contains(x, y))
            StartTimeHold(1);
        else if (_addTimeButton.Contains(x, y))
        {
            if (!selected.Times.Contains(_draftTime))
                selected.Times.Add(_draftTime);
            selected.Times.Sort();
        }
        else if (_selectOutfitsButton.Contains(x, y))
        {
            _outfitSelectionRuleId = selected.Id;
            Game1.playSound("smallSelect");
            return true;
        }
        else if (_deleteButton.Contains(x, y))
        {
            _rules.Remove(selected);
            _selectedRuleId = RulesForActiveSection().FirstOrDefault()?.Id;
        }
        else
        {
            foreach ((int day, Rectangle bounds) in _dayButtons)
            {
                if (!bounds.Contains(x, y))
                    continue;

                if (!selected.Days.Remove(day))
                    selected.Days.Add(day);
                selected.Days.Sort();
                Save();
                Game1.playSound("smallSelect");
                return true;
            }

            foreach ((int time, Rectangle bounds) in _timeChips)
            {
                if (!bounds.Contains(x, y))
                    continue;

                selected.Times.Remove(time);
                Save();
                Game1.playSound("trashcan");
                return true;
            }

            return _contentArea.Contains(x, y);
        }

        Save();
        Game1.playSound("smallSelect");
        return true;
    }

    public bool HandleScroll(int direction, int x, int y)
    {
        if (!IsOpen)
            return false;

        if (_conditionPickerKind != ConditionPickerKind.None)
        {
            if (!_conditionPickerBounds.Contains(x, y))
                return true;

            int max = Math.Max(0, _conditionPickerOptions.Count - _conditionPickerVisibleCount);
            _conditionPickerScroll = Math.Clamp(
                _conditionPickerScroll + (direction > 0 ? -1 : 1),
                0,
                max);
            return true;
        }

        if (_ruleListArea.Contains(x, y))
        {
            int max = Math.Max(0, RulesForActiveSection().Count() - _ruleVisibleCount);
            _ruleScroll = Math.Clamp(_ruleScroll + (direction > 0 ? -1 : 1), 0, max);
            return true;
        }

        if (_editorArea.Contains(x, y) && _editorMaxScroll > 0)
        {
            _editorScrollPixels = Math.Clamp(
                _editorScrollPixels + (direction > 0 ? -48 : 48),
                0,
                _editorMaxScroll);
            return true;
        }

        return false;
    }

    public void UpdateHeldControls(GameTime time)
    {
        if (_heldTimeDirection == 0)
            return;

        if (Mouse.GetState().LeftButton != ButtonState.Pressed)
        {
            ReleaseTimeHold();
            return;
        }

        Rectangle activeArrow = _heldTimeDirection < 0 ? _timeDownButton : _timeUpButton;
        if (!activeArrow.Contains(Game1.getMouseX(), Game1.getMouseY()))
            return;

        double elapsedMs = time.ElapsedGameTime.TotalMilliseconds;
        _timeHoldElapsedMs += elapsedMs;
        if (_timeHoldElapsedMs < TimeHoldInitialDelayMs)
            return;

        _timeHoldRepeatMs += elapsedMs;
        while (_timeHoldRepeatMs >= TimeHoldRepeatMs)
        {
            _draftTime = AddMinutes(_draftTime, _heldTimeDirection * 10);
            _timeHoldRepeatMs -= TimeHoldRepeatMs;
        }
    }

    public void ReleaseTimeHold()
    {
        _heldTimeDirection = 0;
        _timeHoldElapsedMs = 0;
        _timeHoldRepeatMs = 0;
    }

    public bool HandleScrollbarDrag(int y)
    {
        if (_draggingScrollbar == ScrollbarDragTarget.ConditionPicker)
        {
            int max = Math.Max(0, _conditionPickerOptions.Count - _conditionPickerVisibleCount);
            _conditionPickerScroll = ScrollOffsetFromThumb(
                y,
                _scrollbarDragOffsetY,
                _conditionPickerScrollTrack,
                _conditionPickerScrollThumb,
                max);
            return true;
        }

        if (_draggingScrollbar == ScrollbarDragTarget.Rules)
        {
            int max = Math.Max(0, RulesForActiveSection().Count() - _ruleVisibleCount);
            _ruleScroll = ScrollOffsetFromThumb(y, _scrollbarDragOffsetY, _ruleScrollTrack, _ruleScrollThumb, max);
            return true;
        }

        if (_draggingScrollbar == ScrollbarDragTarget.Editor)
        {
            _editorScrollPixels = ScrollOffsetFromThumb(
                y,
                _scrollbarDragOffsetY,
                _editorScrollTrack,
                _editorScrollThumb,
                _editorMaxScroll);
            return true;
        }

        return false;
    }

    public void ReleaseScrollbarDrag()
    {
        _draggingScrollbar = ScrollbarDragTarget.None;
        _scrollbarDragOffsetY = 0;
    }

    public bool TryCloseConditionPicker()
    {
        if (_conditionPickerKind == ConditionPickerKind.None)
            return false;

        CloseConditionPicker();
        Game1.playSound("smallSelect");
        return true;
    }

    public bool TryBeginOutfitSelection(out OutfitScheduleRule? rule)
    {
        rule = null;
        if (_outfitSelectionRuleId is null)
            return false;

        rule = _rules.FirstOrDefault(candidate => candidate.Id == _outfitSelectionRuleId);
        _outfitSelectionRuleId = null;
        return rule is not null;
    }

    public bool TryBeginNameEdit(out OutfitScheduleRule? rule)
    {
        rule = null;
        if (_nameEditRuleId is null)
            return false;

        rule = _rules.FirstOrDefault(candidate => candidate.Id == _nameEditRuleId);
        _nameEditRuleId = null;
        return rule is not null;
    }

    public void SetRuleName(string ruleId, string name)
    {
        OutfitScheduleRule? rule = _rules.FirstOrDefault(candidate => candidate.Id == ruleId);
        if (rule is null)
            return;

        string normalized = name.Trim();
        rule.Name = normalized.Length > 32 ? normalized[..32] : normalized;
        Save();
    }

    public string GetRuleDisplayName(OutfitScheduleRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.Name))
            return rule.Name;

        int index = _rules.Where(candidate => candidate.Section == rule.Section).ToList().FindIndex(candidate => candidate.Id == rule.Id);
        return I18n.ScheduleRuleNumber(Math.Max(0, index) + 1);
    }

    public void SetSelectedOutfits(
        string ruleId,
        IEnumerable<string> outfitNames,
        IEnumerable<string> tagIds)
    {
        OutfitScheduleRule? rule = _rules.FirstOrDefault(candidate => candidate.Id == ruleId);
        if (rule is null)
            return;

        rule.OutfitNames = outfitNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        rule.TagIds = tagIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Save();
    }

    public void SetTags(IEnumerable<OutfitTag> tags)
    {
        _tags = tags.ToList();
    }

    public void RemoveTag(string tagId)
    {
        foreach (OutfitScheduleRule rule in _rules)
            rule.TagIds.RemoveAll(id => id.Equals(tagId, StringComparison.OrdinalIgnoreCase));

        Save();
    }

    public void RenameOutfit(string oldName, string newName)
    {
        foreach (OutfitScheduleRule rule in _rules)
        {
            for (int i = 0; i < rule.OutfitNames.Count; i++)
            {
                if (rule.OutfitNames[i].Equals(oldName, StringComparison.OrdinalIgnoreCase))
                    rule.OutfitNames[i] = newName;
            }

            if (rule.LastOutfitName.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                rule.LastOutfitName = newName;
            for (int i = 0; i < rule.UsedOutfitNames.Count; i++)
            {
                if (rule.UsedOutfitNames[i].Equals(oldName, StringComparison.OrdinalIgnoreCase))
                    rule.UsedOutfitNames[i] = newName;
            }
            if (rule.LockedOutfitName.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                rule.LockedOutfitName = newName;
        }

        Save();
    }

    public void RemoveOutfits(IEnumerable<string> outfitNames)
    {
        HashSet<string> removed = new(outfitNames, StringComparer.OrdinalIgnoreCase);
        foreach (OutfitScheduleRule rule in _rules)
        {
            rule.OutfitNames.RemoveAll(removed.Contains);
            rule.UsedOutfitNames.RemoveAll(removed.Contains);
            if (removed.Contains(rule.LastOutfitName))
                rule.LastOutfitName = string.Empty;
            if (removed.Contains(rule.LockedOutfitName))
            {
                rule.LockedOutfitName = string.Empty;
                rule.LockedDayKey = string.Empty;
            }
        }
        Save();
    }

    private OutfitScheduleRule? SelectedRule
        => _rules.FirstOrDefault(rule => rule.Id == _selectedRuleId);

    private IEnumerable<OutfitScheduleRule> RulesForActiveSection()
        => ActiveSection is null ? Enumerable.Empty<OutfitScheduleRule>() : _rules.Where(rule => rule.Section == ActiveSection);

    private void DrawRuleList(SpriteBatch b)
    {
        _ruleCards.Clear();
        Rectangle list = _ruleListArea;
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
            list.X, list.Y, list.Width, list.Height, Color.White, 4f, drawShadow: false);

        DrawButton(b, _addButton, I18n.ScheduleAddRule, Color.Wheat);

        _ruleListViewport = new Rectangle(list.X + 10, list.Y + 76, list.Width - 42, list.Height - 88);
        _ruleScrollTrack = new Rectangle(list.Right - 24, _ruleListViewport.Y, 12, _ruleListViewport.Height);
        _ruleVisibleCount = Math.Max(1, _ruleListViewport.Height / 72);
        int y = _ruleListViewport.Y;
        List<OutfitScheduleRule> sectionRules = RulesForActiveSection().ToList();
        int maxRuleScroll = Math.Max(0, sectionRules.Count - _ruleVisibleCount);
        _ruleScroll = Math.Clamp(_ruleScroll, 0, maxRuleScroll);
        int number = _ruleScroll + 1;
        foreach (OutfitScheduleRule rule in sectionRules.Skip(_ruleScroll).Take(_ruleVisibleCount))
        {
            int cardWidth = maxRuleScroll > 0
                ? _ruleListViewport.Width
                : list.Right - 10 - _ruleListViewport.X;
            Rectangle card = new(_ruleListViewport.X, y, cardWidth, 66);
            _ruleCards[rule.Id] = card;
            bool selected = rule.Id == _selectedRuleId;
            bool hovered = card.Contains(Game1.getMouseX(), Game1.getMouseY());
            b.Draw(Game1.staminaRect, card,
                selected ? Color.SandyBrown * 0.35f : hovered ? Color.Wheat * 0.25f : Color.White * 0.01f);

            string heading = string.IsNullOrWhiteSpace(rule.Name)
                ? I18n.ScheduleRuleNumber(number)
                : rule.Name;
            number++;
            Utility.drawTextWithShadow(b, Truncate(heading, card.Width - 20), Game1.smallFont,
                new Vector2(card.X + 10, card.Y + 7), rule.Enabled ? Game1.textColor : Color.Gray);

            string scheduleSummary = rule.Section == ScheduleSection.Festivals
                ? FestivalSummary(rule)
                : $"{DaySummary(rule)}  |  {WeatherSummary(rule)}";
            string sourceSummary = $"{rule.OutfitNames.Count} {I18n.ScheduleOutfitsShort}";
            if (rule.TagIds.Count > 0)
                sourceSummary += $"  |  {rule.TagIds.Count} {I18n.ScheduleTagsShort}";
            string summary = $"{scheduleSummary}  |  {sourceSummary}";
            Utility.drawTextWithShadow(b, Truncate(summary, card.Width - 20), Game1.smallFont,
                new Vector2(card.X + 10, card.Y + 34), Color.Gray);
            y += 72;
        }

        if (maxRuleScroll > 0)
        {
            _ruleScrollThumb = CalculateScrollThumb(
                _ruleScrollTrack,
                _ruleVisibleCount,
                sectionRules.Count,
                _ruleScroll,
                maxRuleScroll);
            DrawScrollbar(b, _ruleScrollTrack, _ruleScrollThumb);
        }
        else
        {
            _ruleScrollThumb = Rectangle.Empty;
        }

        if (!_ruleCards.Any())
        {
            Vector2 size = Game1.smallFont.MeasureString(I18n.ScheduleEmpty);
            Utility.drawTextWithShadow(b, I18n.ScheduleEmpty, Game1.smallFont,
                new Vector2(_ruleListViewport.Center.X - size.X / 2f, _ruleListViewport.Center.Y - size.Y / 2f), Color.Gray);
        }
    }

    private void DrawEditor(SpriteBatch b)
    {
        _dayButtons.Clear();
        _timeChips.Clear();
        OutfitScheduleRule? rule = SelectedRule;
        Rectangle editor = _editorArea;
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
            editor.X, editor.Y, editor.Width, editor.Height, Color.White, 4f, drawShadow: false);

        if (rule is null)
        {
            _editorMaxScroll = 0;
            _editorScrollThumb = Rectangle.Empty;
            Vector2 emptySize = Game1.smallFont.MeasureString(I18n.ScheduleEmpty);
            Utility.drawTextWithShadow(b, I18n.ScheduleEmpty, Game1.smallFont,
                new Vector2(editor.Center.X - emptySize.X / 2f, editor.Center.Y - emptySize.Y / 2f), Color.Gray);
            return;
        }

        _editorViewport = new Rectangle(editor.X + 12, editor.Y + 10, editor.Width - 44, editor.Height - 20);
        _editorScrollTrack = new Rectangle(editor.Right - 24, _editorViewport.Y, 12, _editorViewport.Height);

        List<string> selectedSources = new(rule.OutfitNames);
        selectedSources.AddRange(rule.TagIds.Select(TagDisplayName));
        string outfits = selectedSources.Count == 0
            ? I18n.ScheduleNoOutfits
            : string.Join(", ", selectedSources);
        List<string> outfitLines = WrapText(outfits, _editorViewport.Width - 16);
        int chipWidth = 128;
        int chipGap = 8;
        int secondColumnX = _editorViewport.X + _editorViewport.Width / 2 + 48;
        int chipAreaWidth = Math.Max(chipWidth, _editorViewport.Right - secondColumnX);
        int chipColumns = Math.Max(1, chipAreaWidth / (chipWidth + chipGap));
        int chipRows = Math.Max(1, (int)Math.Ceiling(rule.Times.Count / (double)chipColumns));
        int extraTimeRowsHeight = Math.Max(0, chipRows - 1) * 46;

        bool festivalRule = rule.Section == ScheduleSection.Festivals;
        int contentHeight = 18 + 58 + 76 + 76 + 88 + (!festivalRule && rule.DayMode == ScheduleDayMode.Multiple ? 76 : 0)
            + 88 + 90 + extraTimeRowsHeight + 54 + Math.Max(1, outfitLines.Count) * 26 + 16;
        _editorMaxScroll = Math.Max(0, contentHeight - _editorViewport.Height);
        _editorScrollPixels = Math.Clamp(_editorScrollPixels, 0, _editorMaxScroll);

        int x = _editorViewport.X + 8;
        int y = _editorViewport.Y + 18 - _editorScrollPixels;
        int fieldW = Math.Min(205, _editorViewport.Width / 2 - 24);
        int fieldH = 40;
        _festivalButton = Rectangle.Empty;
        _dayModeButton = Rectangle.Empty;
        _singleDayDownButton = Rectangle.Empty;
        _singleDayUpButton = Rectangle.Empty;
        _weatherButton = Rectangle.Empty;
        _locationButton = Rectangle.Empty;

        _enabledButton = new Rectangle(x, y, 170, fieldH);
        string statusLabel;
        Color statusColor;
        if (!rule.Enabled)
        {
            statusLabel = I18n.ScheduleDisabled;
            statusColor = Color.LightGray;
        }
        else if (rule.Id == _evaluator.GetWinningRuleId(_rules))
        {
            statusLabel = I18n.ScheduleActiveNow;
            statusColor = Color.PaleGreen;
        }
        else if (_evaluator.IsRuleMatchingNow(rule))
        {
            statusLabel = I18n.ScheduleWaiting;
            statusColor = Color.Khaki;
        }
        else
        {
            statusLabel = I18n.ScheduleEnabled;
            statusColor = Color.PaleGreen;
        }
        DrawEditorButton(b, _enabledButton, statusLabel, statusColor);
        _deleteButton = new Rectangle(_editorViewport.Right - 120, y, 120, fieldH);
        DrawEditorButton(b, _deleteButton, I18n.ScheduleDelete, Color.Salmon);
        y += 58;

        DrawEditorLabel(b, I18n.ScheduleNameOptional, x, y);
        _nameButton = new Rectangle(x, y + 28, Math.Min(420, _editorViewport.Width - 16), fieldH);
        DrawEditorButton(b, _nameButton, GetRuleDisplayName(rule), Color.Wheat);
        y += 76;

        DrawEditorLabel(b, I18n.ScheduleChangeFrequency, x, y);
        _changeFrequencyButton = new Rectangle(x, y + 28, Math.Min(420, _editorViewport.Width - 16), fieldH);
        DrawEditorButton(b, _changeFrequencyButton, ChangeFrequencyLabel(rule.ChangeFrequency), Color.LightBlue);
        y += 76;

        int col2 = secondColumnX;
        if (festivalRule)
        {
            DrawEditorLabel(b, I18n.ScheduleFestival, x, y);
            _festivalButton = new Rectangle(x, y + 32, Math.Min(420, _editorViewport.Width - 16), fieldH);
            DrawEditorButton(b, _festivalButton, FestivalSummary(rule), Color.LightBlue);
            y += 88;
        }
        else
        {
            DrawEditorLabel(b, I18n.ScheduleDays, x, y);
            _dayModeButton = new Rectangle(x, y + 32, fieldW, fieldH);
            DrawEditorButton(b, _dayModeButton, DayModeLabel(rule.DayMode), Color.Wheat);

            if (rule.DayMode == ScheduleDayMode.Single)
            {
                _singleDayDownButton = new Rectangle(x + fieldW + 12, y + 32, 42, fieldH);
                Rectangle dayValue = new(x + fieldW + 58, y + 32, 72, fieldH);
                _singleDayUpButton = new Rectangle(x + fieldW + 134, y + 32, 42, fieldH);
                DrawEditorArrow(b, _singleDayDownButton, LeftArrowSource);
                DrawEditorButton(b, dayValue, rule.SingleDay.ToString(), Color.White);
                DrawEditorArrow(b, _singleDayUpButton, RightArrowSource);
            }

            DrawEditorLabel(b, I18n.ScheduleWeather, col2, y);
            _weatherButton = new Rectangle(col2, y + 32, fieldW, fieldH);
            DrawEditorButton(b, _weatherButton, WeatherSummary(rule), Color.LightBlue);
            y += 88;

            if (rule.DayMode == ScheduleDayMode.Multiple)
            {
                int dayX = x;
                for (int day = 1; day <= 28; day++)
                {
                    int col = (day - 1) % 14;
                    int row = (day - 1) / 14;
                    Rectangle dayButton = new(dayX + col * 34, y + row * 34, 30, 30);
                    _dayButtons[day] = dayButton;
                    DrawEditorButton(b, dayButton, day.ToString(), rule.Days.Contains(day) ? Color.SandyBrown : Color.White, small: true);
                }
                y += 76;
            }
        }

        DrawEditorLabel(b, I18n.SchedulePeriod, x, y);
        _periodButton = new Rectangle(x, y + 32, fieldW, fieldH);
        DrawEditorButton(b, _periodButton, PeriodLabel(rule.Period), Color.PeachPuff);

        if (!festivalRule)
        {
            DrawEditorLabel(b, I18n.ScheduleLocation, col2, y);
            _locationButton = new Rectangle(col2, y + 32, fieldW, fieldH);
            DrawEditorButton(b, _locationButton, LocationSummary(rule), Color.PaleGreen);
        }
        y += 88;

        DrawEditorLabel(b, I18n.ScheduleExactTimes, x, y);
        _timeDownButton = new Rectangle(x, y + 32, 42, fieldH);
        Rectangle timeValue = new(x + 46, y + 32, 82, fieldH);
        _timeUpButton = new Rectangle(x + 132, y + 32, 42, fieldH);
        _addTimeButton = new Rectangle(x + 180, y + 32, 120, fieldH);
        DrawEditorArrow(b, _timeDownButton, LeftArrowSource);
        DrawEditorButton(b, timeValue, FormatTime(_draftTime), Color.White);
        DrawEditorArrow(b, _timeUpButton, RightArrowSource);
        DrawEditorButton(b, _addTimeButton, I18n.ScheduleAddTime, Color.Wheat);

        int chipIndex = 0;
        foreach (int time in rule.Times)
        {
            int chipCol = chipIndex % chipColumns;
            int chipRow = chipIndex / chipColumns;
            Rectangle chip = new(
                col2 + chipCol * (chipWidth + chipGap),
                y + 32 + chipRow * 46,
                chipWidth,
                fieldH);
            _timeChips[time] = chip;
            DrawTimeChip(b, chip, FormatTime(time));
            chipIndex++;
        }
        y += 90 + extraTimeRowsHeight;

        _selectOutfitsButton = new Rectangle(x, y, 240, 46);
        DrawEditorButton(b, _selectOutfitsButton, I18n.ScheduleSelectOutfits, Color.Plum);
        y += 54;

        Color outfitColor = selectedSources.Count == 0 ? Color.Gray : Game1.textColor;
        foreach (string line in outfitLines)
        {
            DrawEditorText(b, line, new Vector2(x, y), outfitColor);
            y += 26;
        }

        if (_editorMaxScroll > 0)
        {
            int visibleUnits = _editorViewport.Height;
            int totalUnits = _editorViewport.Height + _editorMaxScroll;
            _editorScrollThumb = CalculateScrollThumb(
                _editorScrollTrack,
                visibleUnits,
                totalUnits,
                _editorScrollPixels,
                _editorMaxScroll);
            DrawScrollbar(b, _editorScrollTrack, _editorScrollThumb);
        }
        else
        {
            _editorScrollThumb = Rectangle.Empty;
        }
    }

    private string TagDisplayName(string tagId)
    {
        OutfitTag? tag = _tags.FirstOrDefault(candidate =>
            candidate.Id.Equals(tagId, StringComparison.OrdinalIgnoreCase));
        return tag is null ? $"#{tagId}" : $"#{tag.Name}";
    }

    private void OpenConditionPicker(ConditionPickerKind kind, OutfitScheduleRule rule)
    {
        _conditionPickerKind = kind;
        _conditionPickerScroll = 0;
        _conditionPickerOptions = kind switch
        {
            ConditionPickerKind.Weather => _conditionCatalog.GetWeatherOptions(rule.WeatherIds),
            ConditionPickerKind.Location => _conditionCatalog.GetLocationOptions(rule.LocationIds),
            ConditionPickerKind.Festival => _conditionCatalog.GetFestivalOptions(_showCustomFestivals, rule.FestivalIds),
            _ => new()
        };
    }

    private void CloseConditionPicker()
    {
        _conditionPickerKind = ConditionPickerKind.None;
        _conditionPickerOptions.Clear();
        _conditionPickerRows.Clear();
        _conditionPickerScroll = 0;
        if (_draggingScrollbar == ScrollbarDragTarget.ConditionPicker)
            ReleaseScrollbarDrag();
    }

    private void DrawConditionPicker(SpriteBatch b)
    {
        OutfitScheduleRule? rule = SelectedRule;
        if (rule is null)
        {
            CloseConditionPicker();
            return;
        }

        b.Draw(Game1.fadeToBlackRect, _contentArea, Color.Black * 0.45f);

        int pickerWidth = Math.Min(660, _contentArea.Width - 80);
        int pickerHeight = Math.Min(570, _contentArea.Height - 48);
        _conditionPickerBounds = new Rectangle(
            _contentArea.Center.X - pickerWidth / 2,
            _contentArea.Center.Y - pickerHeight / 2,
            pickerWidth,
            pickerHeight);
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18),
            _conditionPickerBounds.X, _conditionPickerBounds.Y,
            _conditionPickerBounds.Width, _conditionPickerBounds.Height,
            Color.White, 4f, drawShadow: true);

        string title = _conditionPickerKind switch
        {
            ConditionPickerKind.Weather => I18n.ScheduleSelectWeather,
            ConditionPickerKind.Location => I18n.ScheduleSelectLocations,
            ConditionPickerKind.Festival => I18n.ScheduleSelectFestivals,
            _ => string.Empty
        };
        SpriteText.drawStringWithScrollCenteredAt(b, title,
            _conditionPickerBounds.Center.X,
            _conditionPickerBounds.Y + 24);

        bool festivalPicker = _conditionPickerKind == ConditionPickerKind.Festival;
        _showCustomFestivalsButton = Rectangle.Empty;
        if (festivalPicker)
        {
            _showCustomFestivalsButton = new Rectangle(
                _conditionPickerBounds.X + 24,
                _conditionPickerBounds.Y + 104,
                _conditionPickerBounds.Width - 48,
                42);
            DrawButton(b, _showCustomFestivalsButton,
                _showCustomFestivals ? I18n.ScheduleHideModFestivals : I18n.ScheduleShowModFestivals,
                _showCustomFestivals ? Color.PaleGreen : Color.Wheat);
        }

        int viewportTop = _conditionPickerBounds.Y + (festivalPicker ? 154 : 104);
        _conditionPickerViewport = new Rectangle(
            _conditionPickerBounds.X + 24,
            viewportTop,
            _conditionPickerBounds.Width - 64,
            _conditionPickerBounds.Bottom - 74 - viewportTop);
        _conditionPickerScrollTrack = new Rectangle(
            _conditionPickerBounds.Right - 28,
            _conditionPickerViewport.Y,
            12,
            _conditionPickerViewport.Height);
        _conditionPickerVisibleCount = Math.Max(1, _conditionPickerViewport.Height / 48);
        int maxScroll = Math.Max(0, _conditionPickerOptions.Count - _conditionPickerVisibleCount);
        _conditionPickerScroll = Math.Clamp(_conditionPickerScroll, 0, maxScroll);

        _conditionPickerRows.Clear();
        HashSet<string> selected = new(CurrentConditionIds(rule), StringComparer.OrdinalIgnoreCase);
        int rowY = _conditionPickerViewport.Y;
        foreach (ScheduleConditionOption option in _conditionPickerOptions
            .Skip(_conditionPickerScroll)
            .Take(_conditionPickerVisibleCount))
        {
            Rectangle row = new(_conditionPickerViewport.X, rowY, _conditionPickerViewport.Width, 42);
            _conditionPickerRows[option.Id] = row;
            bool isSelected = selected.Contains(option.Id);
            bool hovered = row.Contains(Game1.getMouseX(), Game1.getMouseY());
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                row.X, row.Y, row.Width, row.Height,
                isSelected ? Color.PaleGreen : hovered ? Color.Wheat : Color.White,
                4f, drawShadow: false);

            Rectangle checkbox = new(row.X + 8, row.Y + 6, 30, 30);
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                checkbox.X, checkbox.Y, checkbox.Width, checkbox.Height,
                isSelected ? Color.SandyBrown : Color.White,
                3f, drawShadow: false);
            if (isSelected)
                Utility.drawTextWithShadow(b, "X", Game1.smallFont,
                    new Vector2(checkbox.X + 8, checkbox.Y + 2), Game1.textColor);

            string label = Truncate(option.Label, row.Width - 58);
            Vector2 textSize = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(row.X + 48, row.Center.Y - textSize.Y / 2f), Game1.textColor);
            rowY += 48;
        }

        if (maxScroll > 0)
        {
            _conditionPickerScrollThumb = CalculateScrollThumb(
                _conditionPickerScrollTrack,
                _conditionPickerVisibleCount,
                _conditionPickerOptions.Count,
                _conditionPickerScroll,
                maxScroll);
            DrawScrollbar(b, _conditionPickerScrollTrack, _conditionPickerScrollThumb);
        }
        else
        {
            _conditionPickerScrollThumb = Rectangle.Empty;
        }

        int buttonY = _conditionPickerBounds.Bottom - 58;
        _conditionPickerClearButton = new Rectangle(_conditionPickerBounds.X + 24, buttonY, 180, 42);
        _conditionPickerDoneButton = new Rectangle(_conditionPickerBounds.Right - 204, buttonY, 180, 42);
        DrawButton(b, _conditionPickerClearButton, I18n.ScheduleAny, Color.LightGray);
        DrawButton(b, _conditionPickerDoneButton, I18n.ButtonDone, Color.PaleGreen);
    }

    private bool HandleConditionPickerClick(int x, int y)
    {
        OutfitScheduleRule? rule = SelectedRule;
        if (rule is null)
        {
            CloseConditionPicker();
            return true;
        }

        if (_conditionPickerKind == ConditionPickerKind.Festival
            && _showCustomFestivalsButton.Contains(x, y))
        {
            _showCustomFestivals = !_showCustomFestivals;
            _conditionPickerOptions = _conditionCatalog.GetFestivalOptions(
                _showCustomFestivals,
                rule.FestivalIds);
            _conditionPickerScroll = 0;
            Game1.playSound("smallSelect");
            return true;
        }

        int maxScroll = Math.Max(0, _conditionPickerOptions.Count - _conditionPickerVisibleCount);
        if (maxScroll > 0 && _conditionPickerScrollThumb.Contains(x, y))
        {
            _draggingScrollbar = ScrollbarDragTarget.ConditionPicker;
            _scrollbarDragOffsetY = y - _conditionPickerScrollThumb.Y;
            return true;
        }
        if (maxScroll > 0 && _conditionPickerScrollTrack.Contains(x, y))
        {
            float usable = Math.Max(1, _conditionPickerScrollTrack.Height - _conditionPickerScrollThumb.Height);
            float ratio = Math.Clamp(
                (y - _conditionPickerScrollTrack.Y - _conditionPickerScrollThumb.Height / 2f) / usable,
                0f,
                1f);
            _conditionPickerScroll = (int)Math.Round(ratio * maxScroll);
            Game1.playSound("shwip");
            return true;
        }

        if (_conditionPickerDoneButton.Contains(x, y))
        {
            CloseConditionPicker();
            Game1.playSound("smallSelect");
            return true;
        }

        List<string> selected = CurrentConditionIds(rule);
        if (_conditionPickerClearButton.Contains(x, y))
        {
            selected.Clear();
            Save();
            Game1.playSound("smallSelect");
            return true;
        }

        foreach ((string id, Rectangle row) in _conditionPickerRows)
        {
            if (!row.Contains(x, y))
                continue;

            int existing = selected.FindIndex(value => value.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                selected.RemoveAt(existing);
            else
                selected.Add(id);
            Save();
            Game1.playSound("smallSelect");
            return true;
        }

        return true;
    }

    private List<string> CurrentConditionIds(OutfitScheduleRule rule)
        => _conditionPickerKind switch
        {
            ConditionPickerKind.Weather => rule.WeatherIds,
            ConditionPickerKind.Location => rule.LocationIds,
            ConditionPickerKind.Festival => rule.FestivalIds,
            _ => new()
        };

    private string WeatherSummary(OutfitScheduleRule rule)
        => ConditionSummary(rule.WeatherIds, weather: true);

    private string LocationSummary(OutfitScheduleRule rule)
        => ConditionSummary(rule.LocationIds, weather: false);

    private string FestivalSummary(OutfitScheduleRule rule)
    {
        if (rule.FestivalIds.Count == 0)
            return I18n.ScheduleAny;
        if (rule.FestivalIds.Count > 1)
            return I18n.ScheduleSelectedCount(rule.FestivalIds.Count);
        return _conditionCatalog.GetFestivalLabel(rule.FestivalIds[0]);
    }

    private string ConditionSummary(IReadOnlyList<string> ids, bool weather)
    {
        if (ids.Count == 0)
            return I18n.ScheduleAny;
        if (ids.Count > 1)
            return I18n.ScheduleSelectedCount(ids.Count);

        return weather
            ? _conditionCatalog.GetWeatherLabel(ids[0])
            : _conditionCatalog.GetLocationLabel(ids[0]);
    }

    private void Save() => _manager.SaveRules(_rules);

    private static T Next<T>(T value) where T : struct, Enum
    {
        T[] values = Enum.GetValues<T>();
        int index = Array.IndexOf(values, value);
        return values[(index + 1) % values.Length];
    }

    private static int AddMinutes(int time, int delta)
    {
        int total = (time / 100) * 60 + time % 100 + delta;
        int min = 6 * 60;
        int max = 26 * 60;
        if (total < min) total = max;
        if (total > max) total = min;
        return (total / 60) * 100 + total % 60;
    }

    private void StartTimeHold(int direction)
    {
        _heldTimeDirection = direction;
        _timeHoldElapsedMs = 0;
        _timeHoldRepeatMs = 0;
        _draftTime = AddMinutes(_draftTime, direction * 10);
    }

    private static string FormatTime(int time)
    {
        int hour = time / 100;
        if (hour >= 24)
            hour -= 24;
        return $"{hour:00}:{time % 100:00}";
    }
    private static void DrawSectionIcon(SpriteBatch b, ScheduleSection section, Rectangle destination)
    {
        if (section == ScheduleSection.Daily)
        {
            var shirt = ItemRegistry.GetDataOrErrorItem("(S)1000");
            b.Draw(shirt.GetTexture(), destination, shirt.GetSourceRect(), Color.White);
            return;
        }

        int objectIndex = section switch
        {
            ScheduleSection.Spring => 18,  // Daffodil
            ScheduleSection.Summer => 402, // Sweet Pea
            ScheduleSection.Fall => 404,   // Common Mushroom
            ScheduleSection.Winter => 416, // Snow Yam
            ScheduleSection.Festivals => 434, // Stardrop
            _ => 18
        };

        Rectangle source = Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet, objectIndex, 16, 16);
        b.Draw(Game1.objectSpriteSheet, destination, source, Color.White);
    }

    private static string SectionLabel(ScheduleSection section) => section switch
    {
        ScheduleSection.Spring => I18n.ScheduleSpring,
        ScheduleSection.Summer => I18n.ScheduleSummer,
        ScheduleSection.Fall => I18n.ScheduleFall,
        ScheduleSection.Winter => I18n.ScheduleWinter,
        ScheduleSection.Festivals => I18n.ScheduleFestivals,
        ScheduleSection.Daily => I18n.ScheduleDaily,
        _ => string.Empty
    };

    private static string DayModeLabel(ScheduleDayMode mode) => mode switch
    {
        ScheduleDayMode.All => I18n.ScheduleAllDays,
        ScheduleDayMode.Single => I18n.ScheduleOneDay,
        ScheduleDayMode.Multiple => I18n.ScheduleMultipleDays,
        _ => string.Empty
    };

    private static string PeriodLabel(SchedulePeriod period) => period switch
    {
        SchedulePeriod.Any => I18n.ScheduleAny,
        SchedulePeriod.Morning => I18n.ScheduleMorning,
        SchedulePeriod.Afternoon => I18n.ScheduleAfternoon,
        SchedulePeriod.Night => I18n.ScheduleNight,
        _ => string.Empty
    };

    private static string ChangeFrequencyLabel(ScheduleChangeFrequency frequency) => frequency switch
    {
        ScheduleChangeFrequency.OncePerDay => I18n.ScheduleFixedForDay,
        ScheduleChangeFrequency.EveryActivation => I18n.ScheduleRandomDuringDay,
        _ => I18n.ScheduleFixedForDay
    };

    private static string DaySummary(OutfitScheduleRule rule) => rule.DayMode switch
    {
        ScheduleDayMode.All => I18n.ScheduleAllDays,
        ScheduleDayMode.Single => I18n.ScheduleDayNumber(rule.SingleDay),
        ScheduleDayMode.Multiple => string.Join(",", rule.Days),
        _ => string.Empty
    };

    private void DrawEditorButton(SpriteBatch b, Rectangle bounds, string text, Color tint, bool small = false)
    {
        if (_editorViewport.Contains(bounds))
            DrawButton(b, bounds, text, tint, small);
    }

    private void DrawEditorArrow(SpriteBatch b, Rectangle bounds, Rectangle source)
    {
        if (!_editorViewport.Contains(bounds))
            return;

        Color tint = bounds.Contains(Game1.getMouseX(), Game1.getMouseY()) ? Color.Wheat : Color.White;
        b.Draw(Game1.mouseCursors, bounds, source, tint);
    }

    private void DrawTimeChip(SpriteBatch b, Rectangle bounds, string time)
    {
        if (!_editorViewport.Contains(bounds))
            return;

        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            hovered ? Color.Wheat : Color.White, 4f, drawShadow: false);

        Rectangle trash = new(bounds.Right - 28, bounds.Center.Y - 10, 20, 20);
        Rectangle textArea = new(bounds.X + 8, bounds.Y, bounds.Width - 44, bounds.Height);
        Vector2 size = Game1.smallFont.MeasureString(time);
        Utility.drawTextWithShadow(b, time, Game1.smallFont,
            new Vector2(textArea.Center.X - size.X / 2f, textArea.Center.Y - size.Y / 2f),
            Game1.textColor);
        b.Draw(Game1.mouseCursors, trash, new Rectangle(323, 433, 9, 9),
            hovered ? Color.Salmon : Color.White);
    }

    private void DrawEditorLabel(SpriteBatch b, string label, int x, int y)
    {
        int height = (int)Math.Ceiling(Game1.smallFont.MeasureString(label).Y);
        Rectangle bounds = new(x, y, Math.Max(1, (int)Game1.smallFont.MeasureString(label).X), height);
        if (_editorViewport.Contains(bounds))
            DrawLabel(b, label, x, y);
    }

    private void DrawEditorText(SpriteBatch b, string text, Vector2 position, Color color)
    {
        Vector2 size = Game1.smallFont.MeasureString(text);
        Rectangle bounds = new((int)position.X, (int)position.Y, Math.Max(1, (int)size.X), Math.Max(1, (int)size.Y));
        if (_editorViewport.Contains(bounds))
            Utility.drawTextWithShadow(b, text, Game1.smallFont, position, color);
    }

    private static List<string> WrapText(string text, int maxWidth)
    {
        var lines = new List<string>();
        string current = string.Empty;

        foreach (string part in text.Split(", ", StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = string.IsNullOrEmpty(current) ? part : current + ", " + part;
            if (!string.IsNullOrEmpty(current) && Game1.smallFont.MeasureString(candidate).X > maxWidth)
            {
                lines.Add(current);
                current = part;
            }
            else
            {
                current = candidate;
            }
        }

        if (!string.IsNullOrEmpty(current))
            lines.Add(current);
        if (lines.Count == 0)
            lines.Add(text);
        return lines;
    }

    private static Rectangle CalculateScrollThumb(
        Rectangle track,
        int visibleUnits,
        int totalUnits,
        int offset,
        int maxOffset)
    {
        int thumbHeight = Math.Max(28, (int)Math.Round(track.Height * (visibleUnits / (double)Math.Max(1, totalUnits))));
        thumbHeight = Math.Min(track.Height, thumbHeight);
        int travel = Math.Max(0, track.Height - thumbHeight);
        int thumbY = track.Y + (maxOffset <= 0 ? 0 : (int)Math.Round(travel * (offset / (double)maxOffset)));
        return new Rectangle(track.X - 4, thumbY, track.Width + 8, thumbHeight);
    }

    private static int ScrollOffsetFromThumb(
        int mouseY,
        int dragOffsetY,
        Rectangle track,
        Rectangle thumb,
        int maxOffset)
    {
        int travel = Math.Max(1, track.Height - thumb.Height);
        int thumbTop = Math.Clamp(mouseY - dragOffsetY, track.Y, track.Bottom - thumb.Height);
        float ratio = (thumbTop - track.Y) / (float)travel;
        return (int)Math.Round(ratio * maxOffset);
    }

    private static void DrawScrollbar(SpriteBatch b, Rectangle track, Rectangle thumb)
    {
        b.Draw(Game1.staminaRect, track, Color.SaddleBrown * 0.25f);
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            thumb.X, thumb.Y, thumb.Width, thumb.Height,
            thumb.Contains(Game1.getMouseX(), Game1.getMouseY()) ? Color.Wheat : Color.White,
            4f,
            drawShadow: false);
    }

    private static void DrawLabel(SpriteBatch b, string label, int x, int y)
        => Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(x, y), Game1.textColor);

    private static void DrawButton(SpriteBatch b, Rectangle bounds, string text, Color tint, bool small = false)
    {
        bool hovered = bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            hovered ? Color.Wheat : tint, 4f, drawShadow: false);

        SpriteFont font = small ? Game1.tinyFont : Game1.smallFont;
        Vector2 size = font.MeasureString(text);
        Utility.drawTextWithShadow(b, text, font,
            new Vector2(bounds.Center.X - size.X / 2f, bounds.Center.Y - size.Y / 2f), Game1.textColor);
    }

    private static string Truncate(string text, int maxWidth)
    {
        if (Game1.smallFont.MeasureString(text).X <= maxWidth)
            return text;
        while (text.Length > 1 && Game1.smallFont.MeasureString(text + "...").X > maxWidth)
            text = text[..^1];
        return text + "...";
    }
}
