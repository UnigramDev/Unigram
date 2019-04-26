using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Services.Settings;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;

namespace Unigram.Services
{
    public interface IThemeService
    {
        Dictionary<string, string[]> GetMapping(TelegramTheme flags);

        Task<IList<ThemeInfoBase>> GetThemesAsync();

        Task SerializeAsync(StorageFile file, ThemeCustomInfo theme);
        Task<ThemeCustomInfo> DeserializeAsync(StorageFile file);

        Task InstallThemeAsync(StorageFile file);
        Task SetThemeAsync(ThemeInfoBase info);
    }

    public class ThemeService : IThemeService
    {
        private readonly ISettingsService _settingsService;

        public ThemeService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public Dictionary<string, string[]> GetMapping(TelegramTheme flags)
        {
            if (flags.HasFlag(TelegramTheme.Dark))
            {
                return _mappingDark;
            }

            return _mapping;
        }

        public async Task<IList<ThemeInfoBase>> GetThemesAsync()
        {
            var result = new List<ThemeInfoBase>
            {
                new ThemeBundledInfo { Name = "Light", Parent = TelegramTheme.Light | TelegramTheme.Brand },
                new ThemeBundledInfo { Name = "Dark", Parent = TelegramTheme.Dark | TelegramTheme.Brand },
                new ThemeBundledInfo { Name = "Windows 10 Light", Parent = TelegramTheme.Light },
                new ThemeBundledInfo { Name = "Windows 10 Dark", Parent = TelegramTheme.Dark },
            };

            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("themes", CreationCollisionOption.OpenIfExists);
            var files = await folder.GetFilesAsync();

            foreach (var file in files)
            {
                result.Add(await DeserializeAsync(file));
            }

            return result;
        }

        public async Task SerializeAsync(StorageFile file, ThemeCustomInfo theme)
        {
            var lines = new StringBuilder();
            lines.AppendLine("!");
            lines.AppendLine($"name: {theme.Name}");
            lines.AppendLine($"parent: {(int)theme.Parent}");

            var lastbrush = false;

            foreach (var item in theme.Values)
            {
                if (item.Value is Color color)
                {
                    if (!lastbrush)
                    {
                        lines.AppendLine("#");
                    }

                    var hexValue = (color.A << 24) + (color.R << 16) + (color.G << 8) + (color.B & 0xff);

                    lastbrush = true;
                    lines.AppendLine(string.Format("{0}: #{1:X8}", item.Key, hexValue));
                }
            }

            await FileIO.WriteTextAsync(file, lines.ToString());
        }

        public async Task<ThemeCustomInfo> DeserializeAsync(StorageFile file)
        {
            var lines = await FileIO.ReadLinesAsync(file);
            var theme = new ThemeCustomInfo();
            theme.Path = file.Path;

            foreach (var line in lines)
            {
                if (line.StartsWith("name: "))
                {
                    theme.Name = line.Substring("name: ".Length);
                }
                else if (line.StartsWith("parent: "))
                {
                    theme.Parent = (TelegramTheme)int.Parse(line.Substring("parent: ".Length));
                }
                else if (line.Equals("!") || line.Equals("#"))
                {
                    continue;
                }
                else
                {
                    var split = line.Split(':');
                    var key = split[0].Trim();
                    var value = split[1].Trim();

                    if (value.StartsWith("#") && int.TryParse(value.Substring(1), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexValue))
                    {
                        byte a = (byte)((hexValue & 0xff000000) >> 24);
                        byte r = (byte)((hexValue & 0x00ff0000) >> 16);
                        byte g = (byte)((hexValue & 0x0000ff00) >> 8);
                        byte b = (byte)(hexValue & 0x000000ff);

                        theme.Values[key] = Color.FromArgb(a, r, g, b);
                    }
                }
            }

            return theme;
        }



        public async Task InstallThemeAsync(StorageFile file)
        {
            var info = await DeserializeAsync(file);
            if (info == null)
            {
                return;
            }

            var installed = await GetThemesAsync();

            var equals = installed.FirstOrDefault(x => x is ThemeCustomInfo custom && ThemeCustomInfo.Equals(custom, info));
            if (equals != null)
            {
                await SetThemeAsync(equals);
                return;
            }

            var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync("themes");
            var result = await file.CopyAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName);

            var theme = await DeserializeAsync(result);
            if (theme != null)
            {
                await SetThemeAsync(theme);
            }
        }

        public async Task SetThemeAsync(ThemeInfoBase info)
        {
            _settingsService.Appearance.RequestedTheme = info.Parent;

            if (info is ThemeCustomInfo custom)
            {
                _settingsService.Appearance.RequestedThemePath = custom.Path;
            }
            else
            {
                _settingsService.Appearance.RequestedThemePath = null;
            }

            var flags = GetElementTheme();

            foreach (TLWindowContext window in WindowContext.ActiveWrappers)
            {
                await window.Dispatcher.DispatchAsync(() =>
                {
                    Theme.Current.Update(info as ThemeCustomInfo);

                    window.UpdateTitleBar();

                    if (window.Content is FrameworkElement element)
                    {
                        if (flags == element.RequestedTheme)
                        {
                            element.RequestedTheme = flags == ElementTheme.Dark
                                ? ElementTheme.Light
                                : ElementTheme.Dark;
                        }

                        element.RequestedTheme = flags;
                    }
                });
            }
        }

        public ElementTheme GetElementTheme()
        {
            var theme = _settingsService.Appearance.RequestedTheme;
            return theme.HasFlag(TelegramTheme.Default)
                ? ElementTheme.Default
                : theme.HasFlag(TelegramTheme.Dark)
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }



        private readonly Dictionary<string, string[]> _mapping = new Dictionary<string, string[]>
        {
            { "SystemControlPageTextBaseMediumBrush", new[] { "SystemControlDescriptionTextForegroundBrush", "HyperlinkButtonForegroundPointerOver", "TextControlPlaceholderForeground", "TextControlPlaceholderForegroundPointerOver" } },
            { "SystemControlTransparentBrush", new[] { "SliderContainerBackground", "SliderContainerBackgroundPointerOver", "SliderContainerBackgroundPressed", "SliderContainerBackgroundDisabled", "RadioButtonBackground", "RadioButtonBackgroundPointerOver", "RadioButtonBackgroundPressed", "RadioButtonBackgroundDisabled", "RadioButtonBorderBrush", "RadioButtonBorderBrushPointerOver", "RadioButtonBorderBrushPressed", "RadioButtonBorderBrushDisabled", "RadioButtonOuterEllipseFill", "RadioButtonOuterEllipseFillPointerOver", "RadioButtonOuterEllipseFillPressed", "RadioButtonOuterEllipseFillDisabled", "RadioButtonOuterEllipseCheckedFillDisabled", "RadioButtonCheckGlyphStroke", "RadioButtonCheckGlyphStrokePointerOver", "RadioButtonCheckGlyphStrokePressed", "RadioButtonCheckGlyphStrokeDisabled", "CheckBoxBackgroundUnchecked", "CheckBoxBackgroundUncheckedPointerOver", "CheckBoxBackgroundUncheckedPressed", "CheckBoxBackgroundUncheckedDisabled", "CheckBoxBackgroundChecked", "CheckBoxBackgroundCheckedPointerOver", "CheckBoxBackgroundCheckedPressed", "CheckBoxBackgroundCheckedDisabled", "CheckBoxBackgroundIndeterminate", "CheckBoxBackgroundIndeterminatePointerOver", "CheckBoxBackgroundIndeterminatePressed", "CheckBoxBackgroundIndeterminateDisabled", "CheckBoxBorderBrushUnchecked", "CheckBoxBorderBrushUncheckedPointerOver", "CheckBoxBorderBrushUncheckedPressed", "CheckBoxBorderBrushUncheckedDisabled", "CheckBoxBorderBrushChecked", "CheckBoxBorderBrushCheckedPointerOver", "CheckBoxBorderBrushCheckedPressed", "CheckBoxBorderBrushCheckedDisabled", "CheckBoxBorderBrushIndeterminate", "CheckBoxBorderBrushIndeterminatePointerOver", "CheckBoxBorderBrushIndeterminatePressed", "CheckBoxBorderBrushIndeterminateDisabled", "CheckBoxCheckBackgroundFillUnchecked", "CheckBoxCheckBackgroundFillUncheckedPointerOver", "CheckBoxCheckBackgroundFillUncheckedDisabled", "CheckBoxCheckBackgroundFillCheckedDisabled", "CheckBoxCheckBackgroundFillIndeterminateDisabled", "HyperlinkButtonBorderBrush", "HyperlinkButtonBorderBrushPointerOver", "HyperlinkButtonBorderBrushPressed", "HyperlinkButtonBorderBrushDisabled", "ToggleSwitchContainerBackground", "ToggleSwitchContainerBackgroundPointerOver", "ToggleSwitchContainerBackgroundPressed", "ToggleSwitchContainerBackgroundDisabled", "ToggleSwitchFillOff", "ToggleSwitchFillOffPointerOver", "ToggleSwitchFillOffDisabled", "ToggleButtonBorderBrushCheckedPressed", "ScrollBarBackground", "ScrollBarBackgroundPointerOver", "ScrollBarBackgroundDisabled", "ScrollBarForeground", "ScrollBarBorderBrush", "ScrollBarBorderBrushPointerOver", "ScrollBarBorderBrushDisabled", "ScrollBarButtonBackground", "ScrollBarButtonBackgroundDisabled", "ScrollBarButtonBorderBrush", "ScrollBarButtonBorderBrushPointerOver", "ScrollBarButtonBorderBrushPressed", "ScrollBarButtonBorderBrushDisabled", "ListViewHeaderItemBackground", "ComboBoxItemBackground", "ComboBoxItemBackgroundDisabled", "ComboBoxItemBackgroundSelectedDisabled", "ComboBoxItemBorderBrush", "ComboBoxItemBorderBrushPressed", "ComboBoxItemBorderBrushPointerOver", "ComboBoxItemBorderBrushDisabled", "ComboBoxItemBorderBrushSelected", "ComboBoxItemBorderBrushSelectedUnfocused", "ComboBoxItemBorderBrushSelectedPressed", "ComboBoxItemBorderBrushSelectedPointerOver", "ComboBoxItemBorderBrushSelectedDisabled", "AppBarEllipsisButtonBackground", "AppBarEllipsisButtonBackgroundDisabled", "AppBarEllipsisButtonBorderBrush", "AppBarEllipsisButtonBorderBrushPointerOver", "AppBarEllipsisButtonBorderBrushPressed", "AppBarEllipsisButtonBorderBrushDisabled", "CalendarViewNavigationButtonBackground", "CalendarViewNavigationButtonBorderBrush", "FlipViewItemBackground", "DateTimePickerFlyoutButtonBackground", "TextControlButtonBackground", "TextControlButtonBackgroundPointerOver", "TextControlButtonBorderBrush", "TextControlButtonBorderBrushPointerOver", "TextControlButtonBorderBrushPressed", "ToggleMenuFlyoutItemBackground", "ToggleMenuFlyoutItemBackgroundDisabled", "PivotBackground", "PivotHeaderBackground", "PivotItemBackground", "PivotHeaderItemBackgroundUnselected", "PivotHeaderItemBackgroundDisabled", "GridViewHeaderItemBackground", "GridViewItemBackground", "GridViewItemDragBackground", "MenuFlyoutItemBackground", "MenuFlyoutItemBackgroundDisabled", "MenuFlyoutSubItemBackground", "MenuFlyoutSubItemBackgroundDisabled", "NavigationViewItemBorderBrushDisabled", "NavigationViewItemBorderBrushCheckedDisabled", "NavigationViewItemBorderBrushSelectedDisabled", "TopNavigationViewItemBackgroundPointerOver", "TopNavigationViewItemBackgroundPressed", "TopNavigationViewItemBackgroundSelected", "NavigationViewBackButtonBackground", "MenuBarBackground", "MenuBarItemBackground", "AppBarButtonBackground", "AppBarButtonBackgroundDisabled", "AppBarButtonBorderBrush", "AppBarButtonBorderBrushPointerOver", "AppBarButtonBorderBrushPressed", "AppBarButtonBorderBrushDisabled", "AppBarToggleButtonBackground", "AppBarToggleButtonBackgroundDisabled", "AppBarToggleButtonBackgroundHighLightOverlay", "AppBarToggleButtonBorderBrush", "AppBarToggleButtonBorderBrushPointerOver", "AppBarToggleButtonBorderBrushPressed", "AppBarToggleButtonBorderBrushDisabled", "AppBarToggleButtonBorderBrushChecked", "AppBarToggleButtonBorderBrushCheckedPointerOver", "AppBarToggleButtonBorderBrushCheckedPressed", "AppBarToggleButtonBorderBrushCheckedDisabled", "ListViewItemBackground", "ListViewItemDragBackground", "TreeViewItemBackgroundDisabled", "TreeViewItemBorderBrush", "TreeViewItemBorderBrushDisabled", "TreeViewItemBorderBrushSelected", "TreeViewItemBorderBrushSelectedDisabled", "TreeViewItemCheckBoxBackgroundSelected", "CommandBarFlyoutButtonBackground", "AppBarButtonBorderBrushSubMenuOpened" } },
            { "SystemControlForegroundAccentBrush", new[] { "SliderThumbBackground", "CheckBoxCheckBackgroundStrokeIndeterminate", "AccentButtonBackground", "AccentButtonBackgroundPointerOver", "RatingControlSelectedForeground", "RatingControlPointerOverSelectedForeground", "NavigationViewSelectionIndicatorForeground" } },
            { "SystemControlHighlightChromeAltLowBrush", new[] { "SliderThumbBackgroundPointerOver", "ColorPickerSliderThumbBackgroundPointerOver" } },
            { "SystemControlHighlightChromeHighBrush", new[] { "SliderThumbBackgroundPressed" } },
            { "SystemControlDisabledChromeDisabledHighBrush", new[] { "SliderThumbBackgroundDisabled", "SliderTrackFillDisabled", "SliderTrackValueFillDisabled", "GridViewItemPlaceholderBackground", "ColorPickerSliderThumbBackgroundDisabled", "ListViewItemPlaceholderBackground" } },
            { "SystemControlForegroundBaseMediumLowBrush", new[] { "SliderTrackFill", "SliderTrackFillPressed", "SliderTickBarFill", "ComboBoxBorderBrush", "AppBarSeparatorForeground", "CalendarDatePickerBorderBrush", "DatePickerButtonBorderBrush", "TimePickerButtonBorderBrush", "TextControlBorderBrush", "MenuBarItemBorderBrush" } },
            { "SystemControlForegroundBaseMediumBrush", new[] { "SliderTrackFillPointerOver", "CheckBoxCheckGlyphForegroundIndeterminatePressed", "CalendarDatePickerTextForeground", "CalendarViewNavigationButtonForegroundPressed", "ToggleMenuFlyoutItemKeyboardAcceleratorTextForeground", "PivotHeaderItemForegroundUnselected", "RatingControlPointerOverPlaceholderForeground", "RatingControlPointerOverUnselectedForeground", "RatingControlCaptionForeground", "TopNavigationViewItemForeground", "MenuFlyoutItemKeyboardAcceleratorTextForeground", "AppBarButtonKeyboardAcceleratorTextForeground", "AppBarToggleButtonKeyboardAcceleratorTextForeground", "AppBarToggleButtonKeyboardAcceleratorTextForegroundChecked" } },
            { "SystemControlHighlightAccentBrush", new[] { "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed", "RadioButtonOuterEllipseCheckedStroke", "RadioButtonOuterEllipseCheckedStrokePointerOver", "CheckBoxCheckBackgroundStrokeIndeterminatePointerOver", "CheckBoxCheckBackgroundFillChecked", "ToggleSwitchFillOn", "ToggleButtonBackgroundChecked", "ToggleButtonBackgroundCheckedPointerOver", "CalendarViewSelectedBorderBrush", "TextControlBorderBrushFocused", "TextControlSelectionHighlightColor", "TextControlButtonBackgroundPressed", "TextControlButtonForegroundPointerOver", "GridViewItemBackgroundSelected", "AppBarToggleButtonBackgroundChecked", "AppBarToggleButtonBackgroundCheckedPointerOver", "AppBarToggleButtonBackgroundCheckedPressed", "SplitButtonBackgroundChecked", "SplitButtonBackgroundCheckedPointerOver" } },
            { "SystemControlForegroundBaseHighBrush", new[] { "SliderHeaderForeground", "ButtonForeground", "RadioButtonForeground", "RadioButtonForegroundPointerOver", "RadioButtonForegroundPressed", "CheckBoxForegroundUnchecked", "CheckBoxForegroundUncheckedPointerOver", "CheckBoxForegroundUncheckedPressed", "CheckBoxForegroundChecked", "CheckBoxForegroundCheckedPointerOver", "CheckBoxForegroundCheckedPressed", "CheckBoxForegroundIndeterminate", "CheckBoxForegroundIndeterminatePointerOver", "CheckBoxForegroundIndeterminatePressed", "CheckBoxCheckGlyphForegroundIndeterminatePointerOver", "RepeatButtonForeground", "ToggleSwitchContentForeground", "ToggleSwitchHeaderForeground", "ToggleButtonForeground", "ToggleButtonForegroundIndeterminate", "ScrollBarButtonArrowForeground", "ScrollBarButtonArrowForegroundPointerOver", "ComboBoxItemForeground", "ComboBoxForeground", "ComboBoxDropDownForeground", "AppBarEllipsisButtonForeground", "AppBarEllipsisButtonForegroundPressed", "AppBarForeground", "ToolTipForeground", "CalendarDatePickerForeground", "CalendarDatePickerTextForegroundSelected", "CalendarViewFocusBorderBrush", "CalendarViewCalendarItemForeground", "CalendarViewNavigationButtonForegroundPointerOver", "DatePickerHeaderForeground", "DatePickerButtonForeground", "TimePickerHeaderForeground", "TimePickerButtonForeground", "LoopingSelectorItemForeground", "TextControlForeground", "TextControlForegroundPointerOver", "TextControlHeaderForeground", "TextControlHighlighterForeground", "ToggleMenuFlyoutItemForeground", "GridViewItemForeground", "GridViewItemForegroundPointerOver", "GridViewItemForegroundSelected", "GridViewItemFocusSecondaryBorderBrush", "MenuFlyoutItemForeground", "MenuFlyoutSubItemForeground", "RatingControlPlaceholderForeground", "NavigationViewItemForeground", "TopNavigationViewItemForegroundSelected", "ColorPickerSliderThumbBackground", "AppBarButtonForeground", "AppBarToggleButtonForeground", "AppBarToggleButtonCheckGlyphForeground", "AppBarToggleButtonCheckGlyphForegroundChecked", "CommandBarForeground", "ListViewItemForeground", "ListViewItemFocusSecondaryBorderBrush", "TreeViewItemForeground", "SwipeItemForeground", "SplitButtonForeground" } },
            { "SystemControlDisabledBaseMediumLowBrush", new[] { "SliderHeaderForegroundDisabled", "SliderTickBarFillDisabled", "ButtonForegroundDisabled", "RadioButtonForegroundDisabled", "RadioButtonOuterEllipseStrokeDisabled", "RadioButtonOuterEllipseCheckedStrokeDisabled", "RadioButtonCheckGlyphFillDisabled", "CheckBoxForegroundUncheckedDisabled", "CheckBoxForegroundCheckedDisabled", "CheckBoxForegroundIndeterminateDisabled", "CheckBoxCheckBackgroundStrokeUncheckedDisabled", "CheckBoxCheckBackgroundStrokeCheckedDisabled", "CheckBoxCheckBackgroundStrokeIndeterminateDisabled", "CheckBoxCheckGlyphForegroundCheckedDisabled", "CheckBoxCheckGlyphForegroundIndeterminateDisabled", "HyperlinkButtonForegroundDisabled", "RepeatButtonForegroundDisabled", "ToggleSwitchContentForegroundDisabled", "ToggleSwitchHeaderForegroundDisabled", "ToggleSwitchStrokeOffDisabled", "ToggleSwitchStrokeOnDisabled", "ToggleSwitchKnobFillOffDisabled", "ToggleButtonForegroundDisabled", "ToggleButtonForegroundCheckedDisabled", "ToggleButtonForegroundIndeterminateDisabled", "ComboBoxItemForegroundDisabled", "ComboBoxItemForegroundSelectedDisabled", "ComboBoxForegroundDisabled", "ComboBoxDropDownGlyphForegroundDisabled", "AppBarEllipsisButtonForegroundDisabled", "AccentButtonForegroundDisabled", "CalendarDatePickerForegroundDisabled", "CalendarDatePickerCalendarGlyphForegroundDisabled", "CalendarDatePickerTextForegroundDisabled", "CalendarDatePickerHeaderForegroundDisabled", "CalendarViewBlackoutForeground", "CalendarViewWeekDayForegroundDisabled", "CalendarViewNavigationButtonForegroundDisabled", "HubSectionHeaderButtonForegroundDisabled", "DatePickerHeaderForegroundDisabled", "DatePickerButtonForegroundDisabled", "TimePickerHeaderForegroundDisabled", "TimePickerButtonForegroundDisabled", "TextControlHeaderForegroundDisabled", "ToggleMenuFlyoutItemForegroundDisabled", "ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled", "ToggleMenuFlyoutItemCheckGlyphForegroundDisabled", "PivotHeaderItemForegroundDisabled", "JumpListDefaultDisabledForeground", "MenuFlyoutItemForegroundDisabled", "MenuFlyoutSubItemForegroundDisabled", "MenuFlyoutSubItemChevronDisabled", "NavigationViewItemForegroundDisabled", "NavigationViewItemForegroundCheckedDisabled", "NavigationViewItemForegroundSelectedDisabled", "TopNavigationViewItemForegroundDisabled", "AppBarButtonForegroundDisabled", "AppBarToggleButtonForegroundDisabled", "AppBarToggleButtonCheckGlyphForegroundDisabled", "AppBarToggleButtonCheckGlyphForegroundCheckedDisabled", "AppBarToggleButtonOverflowLabelForegroundDisabled", "AppBarToggleButtonOverflowLabelForegroundCheckedDisabled", "CommandBarEllipsisIconForegroundDisabled", "TreeViewItemBackgroundSelectedDisabled", "TreeViewItemForegroundDisabled", "TreeViewItemForegroundSelectedDisabled", "SplitButtonForegroundDisabled", "SplitButtonForegroundCheckedDisabled", "MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled", "AppBarButtonKeyboardAcceleratorTextForegroundDisabled", "AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabled", "AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabled", "AppBarButtonSubItemChevronForegroundDisabled" } },
            { "SystemControlBackgroundAltHighBrush", new[] { "SliderInlineTickBarFill", "CalendarViewCalendarItemBackground", "CalendarViewBackground" } },
            { "SystemControlBackgroundBaseLowBrush", new[] { "ButtonBackground", "ButtonBackgroundPointerOver", "ButtonBackgroundDisabled", "RepeatButtonBackground", "RepeatButtonBackgroundPointerOver", "RepeatButtonBackgroundDisabled", "ThumbBackground", "ToggleButtonBackground", "ToggleButtonBackgroundPointerOver", "ToggleButtonBackgroundDisabled", "ToggleButtonBackgroundCheckedDisabled", "ToggleButtonBackgroundIndeterminate", "ToggleButtonBackgroundIndeterminatePointerOver", "ToggleButtonBackgroundIndeterminateDisabled", "ComboBoxBackgroundDisabled", "ContentDialogBorderBrush", "AccentButtonBackgroundDisabled", "CalendarDatePickerBackgroundPressed", "CalendarDatePickerBackgroundDisabled", "DatePickerButtonBackgroundPressed", "DatePickerButtonBackgroundDisabled", "TimePickerButtonBackgroundPressed", "TimePickerButtonBackgroundDisabled", "TextControlBackgroundDisabled", "JumpListDefaultDisabledBackground", "RatingControlUnselectedForeground", "SwipeItemBackground", "SwipeItemPreThresholdExecuteBackground", "SplitButtonBackground", "SplitButtonBackgroundPointerOver", "SplitButtonBackgroundDisabled", "SplitButtonBackgroundCheckedDisabled" } },
            { "SystemControlBackgroundBaseMediumLowBrush", new[] { "ButtonBackgroundPressed", "RepeatButtonBackgroundPressed", "ToggleButtonBackgroundPressed", "ToggleButtonBackgroundIndeterminatePressed", "ScrollBarThumbFillPointerOver", "AccentButtonBackgroundPressed", "FlipViewNextPreviousButtonBackground", "PivotNextButtonBackground", "PivotPreviousButtonBackground", "AppBarToggleButtonForegroundCheckedDisabled", "SwipeItemBackgroundPressed", "SplitButtonBackgroundPressed" } },
            { "SystemControlHighlightBaseHighBrush", new[] { "ButtonForegroundPointerOver", "ButtonForegroundPressed", "RadioButtonOuterEllipseStrokePointerOver", "CheckBoxCheckBackgroundStrokeUncheckedPointerOver", "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "RepeatButtonForegroundPointerOver", "RepeatButtonForegroundPressed", "ToggleSwitchStrokeOffPointerOver", "ToggleSwitchStrokeOn", "ToggleSwitchKnobFillOffPointerOver", "ToggleButtonForegroundPointerOver", "ToggleButtonForegroundPressed", "ToggleButtonForegroundIndeterminatePointerOver", "ToggleButtonForegroundIndeterminatePressed", "AccentButtonForegroundPressed", "CalendarViewSelectedForeground", "CalendarViewPressedForeground", "DatePickerButtonForegroundPointerOver", "DatePickerButtonForegroundPressed", "TimePickerButtonForegroundPointerOver", "TimePickerButtonForegroundPressed", "SplitButtonForegroundPointerOver", "SplitButtonForegroundPressed" } },
            { "SystemControlForegroundTransparentBrush", new[] { "ButtonBorderBrush", "RepeatButtonBorderBrush", "ToggleButtonBorderBrush", "ToggleButtonBorderBrushIndeterminate", "ScrollBarTrackStroke", "ScrollBarTrackStrokePointerOver", "AppBarHighContrastBorder", "AccentButtonBorderBrush", "FlipViewNextPreviousButtonBorderBrush", "FlipViewNextPreviousButtonBorderBrushPointerOver", "FlipViewNextPreviousButtonBorderBrushPressed", "DateTimePickerFlyoutButtonBorderBrush", "PivotNextButtonBorderBrush", "PivotNextButtonBorderBrushPointerOver", "PivotNextButtonBorderBrushPressed", "PivotPreviousButtonBorderBrush", "PivotPreviousButtonBorderBrushPointerOver", "PivotPreviousButtonBorderBrushPressed", "KeyTipBorderBrush", "CommandBarHighContrastBorder", "SplitButtonBorderBrush" } },
            { "SystemControlHighlightBaseMediumLowBrush", new[] { "ButtonBorderBrushPointerOver", "HyperlinkButtonForegroundPressed", "RepeatButtonBorderBrushPointerOver", "ThumbBackgroundPointerOver", "ToggleButtonBackgroundCheckedPressed", "ToggleButtonBorderBrushPointerOver", "ToggleButtonBorderBrushCheckedPointerOver", "ToggleButtonBorderBrushIndeterminatePointerOver", "ComboBoxBackgroundBorderBrushUnfocused", "ComboBoxBorderBrushPressed", "AccentButtonBorderBrushPointerOver", "CalendarDatePickerBorderBrushPressed", "CalendarViewHoverBorderBrush", "HubSectionHeaderButtonForegroundPressed", "DatePickerButtonBorderBrushPressed", "TimePickerButtonBorderBrushPressed", "MenuBarItemBorderBrushPressed", "MenuBarItemBorderBrushSelected", "SplitButtonBackgroundCheckedPressed", "SplitButtonBorderBrushPointerOver", "SplitButtonBorderBrushCheckedPointerOver" } },
            { "SystemControlHighlightTransparentBrush", new[] { "ButtonBorderBrushPressed", "RadioButtonOuterEllipseCheckedFillPointerOver", "RadioButtonOuterEllipseCheckedFillPressed", "CheckBoxCheckBackgroundStrokeUncheckedPressed", "CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundStrokeCheckedPressed", "CheckBoxCheckBackgroundFillIndeterminate", "CheckBoxCheckBackgroundFillIndeterminatePointerOver", "CheckBoxCheckBackgroundFillIndeterminatePressed", "RepeatButtonBorderBrushPressed", "ThumbBorderBrush", "ThumbBorderBrushPointerOver", "ThumbBorderBrushPressed", "ToggleButtonBorderBrushPressed", "ToggleButtonBorderBrushIndeterminatePressed", "ComboBoxBackgroundBorderBrushFocused", "AccentButtonBorderBrushPressed", "CalendarViewNavigationButtonBorderBrushPointerOver", "DateTimePickerFlyoutButtonBorderBrushPointerOver", "DateTimePickerFlyoutButtonBorderBrushPressed", "PivotHeaderItemBackgroundUnselectedPointerOver", "PivotHeaderItemBackgroundUnselectedPressed", "PivotHeaderItemBackgroundSelected", "PivotHeaderItemBackgroundSelectedPointerOver", "PivotHeaderItemBackgroundSelectedPressed", "SplitButtonBorderBrushPressed" } },
            { "SystemControlDisabledTransparentBrush", new[] { "ButtonBorderBrushDisabled", "RepeatButtonBorderBrushDisabled", "ToggleButtonBorderBrushDisabled", "ToggleButtonBorderBrushCheckedDisabled", "ToggleButtonBorderBrushIndeterminateDisabled", "ScrollBarThumbFillDisabled", "ScrollBarTrackFillDisabled", "ScrollBarTrackStrokeDisabled", "AccentButtonBorderBrushDisabled", "SplitButtonBorderBrushDisabled", "SplitButtonBorderBrushCheckedDisabled" } },
            { "SystemControlForegroundBaseMediumHighBrush", new[] { "RadioButtonOuterEllipseStroke", "CheckBoxCheckBackgroundStrokeUnchecked", "CheckBoxCheckGlyphForegroundIndeterminate", "ToggleSwitchStrokeOff", "ToggleSwitchStrokeOffPressed", "ToggleSwitchKnobFillOff", "ComboBoxDropDownGlyphForeground", "ComboBoxEditableDropDownGlyphForeground", "CalendarDatePickerCalendarGlyphForeground", "ToggleMenuFlyoutItemCheckGlyphForeground", "GridViewItemCheckBrush", "MenuFlyoutSubItemChevron", "TopNavigationViewItemForegroundPointerOver", "TopNavigationViewItemForegroundPressed", "ListViewItemCheckBrush", "ListViewItemCheckBoxBrush", "TreeViewItemCheckBoxBorderSelected", "TreeViewItemCheckGlyphSelected", "AppBarButtonSubItemChevronForeground" } },
            { "SystemControlHighlightBaseMediumBrush", new[] { "RadioButtonOuterEllipseStrokePressed", "RadioButtonOuterEllipseCheckedStrokePressed", "CheckBoxCheckBackgroundStrokeIndeterminatePressed", "CheckBoxCheckBackgroundFillCheckedPressed", "ToggleSwitchFillOffPressed", "ToggleSwitchFillOnPressed", "ToggleSwitchStrokeOnPressed", "ThumbBackgroundPressed", "ComboBoxBorderBrushPointerOver", "CalendarDatePickerBorderBrushPointerOver", "CalendarViewPressedBorderBrush", "FlipViewNextPreviousButtonBackgroundPointerOver", "DatePickerButtonBorderBrushPointerOver", "TimePickerButtonBorderBrushPointerOver", "TextControlBorderBrushPointerOver", "PivotNextButtonBackgroundPointerOver", "PivotPreviousButtonBackgroundPointerOver", "MenuBarItemBorderBrushPointerOver" } },
            { "SystemControlHighlightAltTransparentBrush", new[] { "RadioButtonOuterEllipseCheckedFill", "ToggleButtonBorderBrushChecked", "SplitButtonBorderBrushChecked", "SplitButtonBorderBrushCheckedPressed" } },
            { "SystemControlHighlightBaseMediumHighBrush", new[] { "RadioButtonCheckGlyphFill", "FlipViewNextPreviousButtonBackgroundPressed", "PivotNextButtonBackgroundPressed", "PivotPreviousButtonBackgroundPressed" } },
            { "SystemControlHighlightAltBaseHighBrush", new[] { "RadioButtonCheckGlyphFillPointerOver", "ComboBoxItemForegroundPressed", "ComboBoxItemForegroundPointerOver", "ComboBoxItemForegroundSelected", "ComboBoxItemForegroundSelectedUnfocused", "ComboBoxItemForegroundSelectedPressed", "ComboBoxItemForegroundSelectedPointerOver", "ComboBoxForegroundFocused", "ComboBoxForegroundFocusedPressed", "ComboBoxPlaceHolderForegroundFocusedPressed", "AppBarEllipsisButtonForegroundPointerOver", "DateTimePickerFlyoutButtonForegroundPointerOver", "DateTimePickerFlyoutButtonForegroundPressed", "DatePickerButtonForegroundFocused", "TimePickerButtonForegroundFocused", "LoopingSelectorItemForegroundSelected", "LoopingSelectorItemForegroundPointerOver", "LoopingSelectorItemForegroundPressed", "ToggleMenuFlyoutItemForegroundPointerOver", "ToggleMenuFlyoutItemForegroundPressed", "ToggleMenuFlyoutItemCheckGlyphForegroundPointerOver", "ToggleMenuFlyoutItemCheckGlyphForegroundPressed", "PivotHeaderItemForegroundSelected", "MenuFlyoutItemForegroundPointerOver", "MenuFlyoutItemForegroundPressed", "MenuFlyoutSubItemForegroundPointerOver", "MenuFlyoutSubItemForegroundPressed", "MenuFlyoutSubItemForegroundSubMenuOpened", "MenuFlyoutSubItemChevronPointerOver", "MenuFlyoutSubItemChevronPressed", "MenuFlyoutSubItemChevronSubMenuOpened", "NavigationViewItemForegroundPointerOver", "NavigationViewItemForegroundPressed", "NavigationViewItemForegroundChecked", "NavigationViewItemForegroundCheckedPointerOver", "NavigationViewItemForegroundCheckedPressed", "NavigationViewItemForegroundSelected", "NavigationViewItemForegroundSelectedPointerOver", "NavigationViewItemForegroundSelectedPressed", "AppBarButtonForegroundPointerOver", "AppBarButtonForegroundPressed", "AppBarToggleButtonForegroundPointerOver", "AppBarToggleButtonForegroundPressed", "AppBarToggleButtonForegroundChecked", "AppBarToggleButtonForegroundCheckedPointerOver", "AppBarToggleButtonForegroundCheckedPressed", "AppBarToggleButtonCheckGlyphForegroundPointerOver", "AppBarToggleButtonCheckGlyphForegroundPressed", "AppBarToggleButtonCheckGlyphForegroundCheckedPointerOver", "AppBarToggleButtonCheckGlyphForegroundCheckedPressed", "AppBarToggleButtonOverflowLabelForegroundPointerOver", "AppBarToggleButtonOverflowLabelForegroundPressed", "AppBarToggleButtonOverflowLabelForegroundCheckedPointerOver", "AppBarToggleButtonOverflowLabelForegroundCheckedPressed", "ListViewItemForegroundPointerOver", "ListViewItemForegroundSelected", "TreeViewItemForegroundPointerOver", "TreeViewItemForegroundPressed", "TreeViewItemForegroundSelected", "TreeViewItemForegroundSelectedPointerOver", "TreeViewItemForegroundSelectedPressed", "AppBarButtonForegroundSubMenuOpened", "AppBarButtonSubItemChevronForegroundPointerOver", "AppBarButtonSubItemChevronForegroundPressed", "AppBarButtonSubItemChevronForegroundSubMenuOpened" } },
            { "SystemControlHighlightAltBaseMediumBrush", new[] { "RadioButtonCheckGlyphFillPressed", "ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver", "ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", "MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver", "MenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", "AppBarButtonKeyboardAcceleratorTextForegroundPointerOver", "AppBarButtonKeyboardAcceleratorTextForegroundPressed", "AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOver", "AppBarToggleButtonKeyboardAcceleratorTextForegroundPressed", "AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOver", "AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressed", "AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpened" } },
            { "SystemControlBackgroundBaseMediumBrush", new[] { "CheckBoxCheckBackgroundFillUncheckedPressed", "ScrollBarButtonBackgroundPressed", "ScrollBarThumbFillPressed", "SwipeItemPreThresholdExecuteForeground" } },
            { "SystemControlBackgroundAccentBrush", new[] { "CheckBoxCheckBackgroundFillCheckedPointerOver", "JumpListDefaultEnabledBackground", "SwipeItemPostThresholdExecuteBackground" } },
            { "SystemControlHighlightAltChromeWhiteBrush", new[] { "CheckBoxCheckGlyphForegroundUnchecked", "CheckBoxCheckGlyphForegroundUncheckedPointerOver", "CheckBoxCheckGlyphForegroundUncheckedPressed", "CheckBoxCheckGlyphForegroundUncheckedDisabled", "CheckBoxCheckGlyphForegroundChecked", "ToggleSwitchKnobFillOffPressed", "ToggleSwitchKnobFillOn", "ToggleSwitchKnobFillOnPressed", "ToggleButtonForegroundChecked", "ToggleButtonForegroundCheckedPointerOver", "ToggleButtonForegroundCheckedPressed", "CalendarViewTodayForeground", "TextControlButtonForegroundPressed", "GridViewItemDragForeground", "ListViewItemDragForeground", "SplitButtonForegroundChecked", "SplitButtonForegroundCheckedPointerOver", "SplitButtonForegroundCheckedPressed" } },
            { "SystemControlForegroundChromeWhiteBrush", new[] { "CheckBoxCheckGlyphForegroundCheckedPointerOver", "CheckBoxCheckGlyphForegroundCheckedPressed", "JumpListDefaultEnabledForeground", "SwipeItemPostThresholdExecuteForeground" } },
            { "SystemControlHyperlinkTextBrush", new[] { "HyperlinkButtonForeground", "HubSectionHeaderButtonForeground", "ContentLinkForegroundColor" } },
            { "SystemControlPageBackgroundTransparentBrush", new[] { "HyperlinkButtonBackground", "HyperlinkButtonBackgroundPointerOver", "HyperlinkButtonBackgroundPressed", "HyperlinkButtonBackgroundDisabled" } },
            { "SystemControlHighlightAltListAccentHighBrush", new[] { "ToggleSwitchFillOnPointerOver" } },
            { "SystemControlDisabledBaseLowBrush", new[] { "ToggleSwitchFillOnDisabled", "ComboBoxBorderBrushDisabled", "CalendarDatePickerBorderBrushDisabled", "DatePickerSpacerFillDisabled", "DatePickerButtonBorderBrushDisabled", "TimePickerSpacerFillDisabled", "TimePickerButtonBorderBrushDisabled", "TextControlBorderBrushDisabled", "ColorPickerSliderTrackFillDisabled" } },
            { "SystemControlHighlightListAccentHighBrush", new[] { "ToggleSwitchStrokeOnPointerOver", "ComboBoxItemBackgroundSelectedPressed", "CalendarViewSelectedPressedBorderBrush", "GridViewItemBackgroundSelectedPressed", "MenuFlyoutSubItemBackgroundPressed", "ListViewItemBackgroundSelectedPressed" } },
            { "SystemControlHighlightChromeWhiteBrush", new[] { "ToggleSwitchKnobFillOnPointerOver" } },
            { "SystemControlPageBackgroundBaseLowBrush", new[] { "ToggleSwitchKnobFillOnDisabled" } },
            { "SystemControlBackgroundListLowBrush", new[] { "ScrollBarButtonBackgroundPointerOver", "ComboBoxDropDownBackgroundPointerOver", "MenuBarItemBackgroundPointerOver" } },
            { "SystemControlForegroundAltHighBrush", new[] { "ScrollBarButtonArrowForegroundPressed", "GridViewItemFocusBorderBrush", "ListViewItemFocusBorderBrush" } },
            { "SystemControlForegroundBaseLowBrush", new[] { "ScrollBarButtonArrowForegroundDisabled", "ListViewHeaderItemDividerStroke", "DatePickerSpacerFill", "DatePickerFlyoutPresenterSpacerFill", "TimePickerSpacerFill", "TimePickerFlyoutPresenterSpacerFill", "GridViewHeaderItemDividerStroke" } },
            { "SystemControlForegroundChromeDisabledLowBrush", new[] { "ScrollBarThumbFill" } },
            { "SystemControlDisabledChromeHighBrush", new[] { "ScrollBarPanningThumbBackgroundDisabled" } },
            { "SystemBaseLowColor", new[] { "ScrollBarThumbBackgroundColor" } },
            { "SystemChromeDisabledLowColor", new[] { "ScrollBarPanningThumbBackgroundColor" } },
            { "SystemControlHighlightListMediumBrush", new[] { "ComboBoxItemBackgroundPressed", "AppBarEllipsisButtonBackgroundPressed", "DateTimePickerFlyoutButtonBackgroundPressed", "LoopingSelectorItemBackgroundPressed", "ToggleMenuFlyoutItemBackgroundPressed", "GridViewItemBackgroundPressed", "MenuFlyoutItemBackgroundPressed", "AppBarButtonBackgroundPressed", "AppBarToggleButtonBackgroundHighLightOverlayPressed", "AppBarToggleButtonBackgroundHighLightOverlayCheckedPressed", "ListViewItemBackgroundPressed" } },
            { "SystemControlHighlightListLowBrush", new[] { "ComboBoxItemBackgroundPointerOver", "AppBarEllipsisButtonBackgroundPointerOver", "DateTimePickerFlyoutButtonBackgroundPointerOver", "LoopingSelectorItemBackgroundPointerOver", "ToggleMenuFlyoutItemBackgroundPointerOver", "GridViewItemBackgroundPointerOver", "MenuFlyoutItemBackgroundPointerOver", "MenuFlyoutSubItemBackgroundPointerOver", "AppBarButtonBackgroundPointerOver", "AppBarToggleButtonBackgroundHighLightOverlayPointerOver", "AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOver", "ListViewItemBackgroundPointerOver" } },
            { "SystemControlHighlightListAccentLowBrush", new[] { "ComboBoxItemBackgroundSelected", "ComboBoxItemBackgroundSelectedUnfocused", "ComboBoxBackgroundUnfocused", "CalendarDatePickerBackgroundFocused", "DatePickerButtonBackgroundFocused", "DatePickerFlyoutPresenterHighlightFill", "TimePickerButtonBackgroundFocused", "TimePickerFlyoutPresenterHighlightFill", "MenuFlyoutSubItemBackgroundSubMenuOpened", "ListViewItemBackgroundSelected", "AppBarButtonBackgroundSubMenuOpened" } },
            { "SystemControlHighlightListAccentMediumBrush", new[] { "ComboBoxItemBackgroundSelectedPointerOver", "CalendarViewSelectedHoverBorderBrush", "GridViewItemBackgroundSelectedPointerOver", "ListViewItemBackgroundSelectedPointerOver" } },
            { "SystemControlBackgroundAltMediumLowBrush", new[] { "ComboBoxBackground", "CalendarDatePickerBackground", "DatePickerButtonBackground", "TimePickerButtonBackground", "TextControlBackground" } },
            { "SystemControlPageBackgroundAltMediumBrush", new[] { "ComboBoxBackgroundPointerOver", "CalendarDatePickerBackgroundPointerOver", "DatePickerButtonBackgroundPointerOver", "TimePickerButtonBackgroundPointerOver", "MediaTransportControlsPanelBackground" } },
            { "SystemControlBackgroundListMediumBrush", new[] { "ComboBoxBackgroundPressed", "ComboBoxDropDownBackgroundPointerPressed", "MenuBarItemBackgroundPressed", "MenuBarItemBackgroundSelected" } },
            { "SystemControlPageTextBaseHighBrush", new[] { "ComboBoxPlaceHolderForeground", "ContentDialogForeground", "HubForeground", "HubSectionHeaderForeground" } },
            { "SystemControlBackgroundChromeBlackLowBrush", new[] { "ComboBoxFocusedDropDownBackgroundPointerOver" } },
            { "SystemControlBackgroundChromeBlackMediumLowBrush", new[] { "ComboBoxFocusedDropDownBackgroundPointerPressed" } },
            { "SystemControlHighlightAltBaseMediumHighBrush", new[] { "ComboBoxDropDownGlyphForegroundFocused", "ComboBoxDropDownGlyphForegroundFocusedPressed", "PivotHeaderItemForegroundUnselectedPointerOver", "PivotHeaderItemForegroundUnselectedPressed", "PivotHeaderItemForegroundSelectedPointerOver", "PivotHeaderItemForegroundSelectedPressed" } },
            { "SystemControlTransientBackgroundBrush", new[] { "ComboBoxDropDownBackground", "DatePickerFlyoutPresenterBackground", "TimePickerFlyoutPresenterBackground", "FlyoutPresenterBackground", "MediaTransportControlsFlyoutBackground", "MenuFlyoutPresenterBackground", "CommandBarOverflowPresenterBackground", "AutoSuggestBoxSuggestionsListBackground" } },
            { "SystemControlTransientBorderBrush", new[] { "ComboBoxDropDownBorderBrush", "ToolTipBorderBrush", "DatePickerFlyoutPresenterBorderBrush", "TimePickerFlyoutPresenterBorderBrush", "FlyoutBorderThemeBrush", "MenuFlyoutPresenterBorderBrush", "CommandBarOverflowPresenterBorderBrush", "AutoSuggestBoxSuggestionsListBorderBrush" } },
            { "SystemControlBackgroundChromeMediumBrush", new[] { "AppBarBackground", "LoopingSelectorButtonBackground", "GridViewItemCheckBoxBrush", "CommandBarBackground" } },
            { "SystemControlPageBackgroundAltHighBrush", new[] { "ContentDialogBackground" } },
            { "SystemControlBackgroundChromeWhiteBrush", new[] { "AccentButtonForeground", "AccentButtonForegroundPointerOver", "TextControlBackgroundFocused", "KeyTipForeground" } },
            { "SystemControlBackgroundChromeMediumLowBrush", new[] { "ToolTipBackground" } },
            { "SystemControlHyperlinkBaseHighBrush", new[] { "CalendarViewOutOfScopeForeground" } },
            { "SystemControlDisabledChromeMediumLowBrush", new[] { "CalendarViewOutOfScopeBackground" } },
            { "SystemControlHyperlinkBaseMediumHighBrush", new[] { "CalendarViewForeground" } },
            { "SystemControlForegroundChromeMediumBrush", new[] { "CalendarViewBorderBrush" } },
            { "SystemControlHyperlinkBaseMediumBrush", new[] { "HubSectionHeaderButtonForegroundPointerOver" } },
            { "SystemControlPageBackgroundListLowBrush", new[] { "FlipViewBackground" } },
            { "SystemControlForegroundAltMediumHighBrush", new[] { "FlipViewNextPreviousArrowForeground", "PivotNextButtonForeground", "PivotPreviousButtonForeground" } },
            { "SystemControlHighlightAltAltMediumHighBrush", new[] { "FlipViewNextPreviousArrowForegroundPointerOver", "FlipViewNextPreviousArrowForegroundPressed", "PivotNextButtonForegroundPointerOver", "PivotNextButtonForegroundPressed", "PivotPreviousButtonForegroundPointerOver", "PivotPreviousButtonForegroundPressed" } },
            { "SystemControlForegroundChromeBlackHighBrush", new[] { "TextControlForegroundFocused" } },
            { "SystemControlDisabledChromeDisabledLowBrush", new[] { "TextControlForegroundDisabled", "TextControlPlaceholderForegroundDisabled" } },
            { "SystemControlBackgroundAltMediumBrush", new[] { "TextControlBackgroundPointerOver" } },
            { "SystemControlPageTextChromeBlackMediumLowBrush", new[] { "TextControlPlaceholderForegroundFocused" } },
            { "SystemControlForegroundChromeBlackMediumBrush", new[] { "TextControlButtonForeground" } },
            { "SystemControlPageBackgroundChromeLowBrush", new[] { "ContentLinkBackgroundColor" } },
            { "SystemControlHighlightAltAccentBrush", new[] { "PivotHeaderItemFocusPipeFill", "PivotHeaderItemSelectedPipeFill" } },
            { "SystemControlFocusVisualPrimaryBrush", new[] { "GridViewItemFocusVisualPrimaryBrush", "ListViewItemFocusVisualPrimaryBrush" } },
            { "SystemControlFocusVisualSecondaryBrush", new[] { "GridViewItemFocusVisualSecondaryBrush", "ListViewItemFocusVisualSecondaryBrush" } },
            { "SystemControlPageBackgroundMediumAltMediumBrush", new[] { "AppBarLightDismissOverlayBackground", "CalendarDatePickerLightDismissOverlayBackground", "ComboBoxLightDismissOverlayBackground", "DatePickerLightDismissOverlayBackground", "FlyoutLightDismissOverlayBackground", "PopupLightDismissOverlayBackground", "SplitViewLightDismissOverlayBackground", "TimePickerLightDismissOverlayBackground", "MenuFlyoutLightDismissOverlayBackground", "CommandBarLightDismissOverlayBackground", "AutoSuggestBoxLightDismissOverlayBackground" } },
            { "SystemControlForegroundChromeGrayBrush", new[] { "KeyTipBackground" } },
            { "SystemBaseMediumLowColor", new[] { "RatingControlDisabledSelectedForeground" } },
            { "SystemControlChromeMediumLowAcrylicElementMediumBrush", new[] { "NavigationViewDefaultPaneBackground", "NavigationViewTopPaneBackground" } },
            { "SystemControlForegroundChromeHighBrush", new[] { "ColorPickerSliderThumbBackgroundPressed" } },
            { "SystemControlDisabledAccentBrush", new[] { "AppBarToggleButtonBackgroundCheckedDisabled" } },
        };

        private readonly Dictionary<string, string[]> _mappingDark = new Dictionary<string, string[]>
        {
            { "SystemControlPageTextBaseMediumBrush", new[] { "SystemControlDescriptionTextForegroundBrush", "HyperlinkButtonForegroundPointerOver", "TextControlPlaceholderForeground", "TextControlPlaceholderForegroundPointerOver" } },
            { "SystemControlTransparentBrush", new[] { "SliderContainerBackground", "SliderContainerBackgroundPointerOver", "SliderContainerBackgroundPressed", "SliderContainerBackgroundDisabled", "RadioButtonBackground", "RadioButtonBackgroundPointerOver", "RadioButtonBackgroundPressed", "RadioButtonBackgroundDisabled", "RadioButtonBorderBrush", "RadioButtonBorderBrushPointerOver", "RadioButtonBorderBrushPressed", "RadioButtonBorderBrushDisabled", "RadioButtonOuterEllipseFill", "RadioButtonOuterEllipseFillPointerOver", "RadioButtonOuterEllipseFillPressed", "RadioButtonOuterEllipseFillDisabled", "RadioButtonOuterEllipseCheckedFillDisabled", "RadioButtonCheckGlyphStroke", "RadioButtonCheckGlyphStrokePointerOver", "RadioButtonCheckGlyphStrokePressed", "RadioButtonCheckGlyphStrokeDisabled", "CheckBoxBackgroundUnchecked", "CheckBoxBackgroundUncheckedPointerOver", "CheckBoxBackgroundUncheckedPressed", "CheckBoxBackgroundUncheckedDisabled", "CheckBoxBackgroundChecked", "CheckBoxBackgroundCheckedPointerOver", "CheckBoxBackgroundCheckedPressed", "CheckBoxBackgroundCheckedDisabled", "CheckBoxBackgroundIndeterminate", "CheckBoxBackgroundIndeterminatePointerOver", "CheckBoxBackgroundIndeterminatePressed", "CheckBoxBackgroundIndeterminateDisabled", "CheckBoxBorderBrushUnchecked", "CheckBoxBorderBrushUncheckedPointerOver", "CheckBoxBorderBrushUncheckedPressed", "CheckBoxBorderBrushUncheckedDisabled", "CheckBoxBorderBrushChecked", "CheckBoxBorderBrushCheckedPointerOver", "CheckBoxBorderBrushCheckedPressed", "CheckBoxBorderBrushCheckedDisabled", "CheckBoxBorderBrushIndeterminate", "CheckBoxBorderBrushIndeterminatePointerOver", "CheckBoxBorderBrushIndeterminatePressed", "CheckBoxBorderBrushIndeterminateDisabled", "CheckBoxCheckBackgroundFillUnchecked", "CheckBoxCheckBackgroundFillUncheckedPointerOver", "CheckBoxCheckBackgroundFillUncheckedDisabled", "CheckBoxCheckBackgroundFillCheckedDisabled", "CheckBoxCheckBackgroundFillIndeterminateDisabled", "HyperlinkButtonBorderBrush", "HyperlinkButtonBorderBrushPointerOver", "HyperlinkButtonBorderBrushPressed", "HyperlinkButtonBorderBrushDisabled", "ToggleSwitchContainerBackground", "ToggleSwitchContainerBackgroundPointerOver", "ToggleSwitchContainerBackgroundPressed", "ToggleSwitchContainerBackgroundDisabled", "ToggleSwitchFillOff", "ToggleSwitchFillOffPointerOver", "ToggleSwitchFillOffDisabled", "ToggleButtonBorderBrushCheckedPressed", "ScrollBarBackground", "ScrollBarBackgroundPointerOver", "ScrollBarBackgroundDisabled", "ScrollBarForeground", "ScrollBarBorderBrush", "ScrollBarBorderBrushPointerOver", "ScrollBarBorderBrushDisabled", "ScrollBarButtonBackground", "ScrollBarButtonBackgroundDisabled", "ScrollBarButtonBorderBrush", "ScrollBarButtonBorderBrushPointerOver", "ScrollBarButtonBorderBrushPressed", "ScrollBarButtonBorderBrushDisabled", "ListViewHeaderItemBackground", "ComboBoxItemBackground", "ComboBoxItemBackgroundDisabled", "ComboBoxItemBackgroundSelectedDisabled", "ComboBoxItemBorderBrush", "ComboBoxItemBorderBrushPressed", "ComboBoxItemBorderBrushPointerOver", "ComboBoxItemBorderBrushDisabled", "ComboBoxItemBorderBrushSelected", "ComboBoxItemBorderBrushSelectedUnfocused", "ComboBoxItemBorderBrushSelectedPressed", "ComboBoxItemBorderBrushSelectedPointerOver", "ComboBoxItemBorderBrushSelectedDisabled", "AppBarEllipsisButtonBackground", "AppBarEllipsisButtonBackgroundDisabled", "AppBarEllipsisButtonBorderBrush", "AppBarEllipsisButtonBorderBrushPointerOver", "AppBarEllipsisButtonBorderBrushPressed", "AppBarEllipsisButtonBorderBrushDisabled", "CalendarViewNavigationButtonBackground", "CalendarViewNavigationButtonBorderBrush", "FlipViewItemBackground", "DateTimePickerFlyoutButtonBackground", "TextControlButtonBackground", "TextControlButtonBackgroundPointerOver", "TextControlButtonBorderBrush", "TextControlButtonBorderBrushPointerOver", "TextControlButtonBorderBrushPressed", "ToggleMenuFlyoutItemBackground", "ToggleMenuFlyoutItemBackgroundDisabled", "PivotBackground", "PivotHeaderBackground", "PivotItemBackground", "PivotHeaderItemBackgroundUnselected", "PivotHeaderItemBackgroundDisabled", "GridViewHeaderItemBackground", "GridViewItemBackground", "GridViewItemDragBackground", "MenuFlyoutItemBackground", "MenuFlyoutItemBackgroundDisabled", "MenuFlyoutSubItemBackground", "MenuFlyoutSubItemBackgroundDisabled", "NavigationViewItemBorderBrushDisabled", "NavigationViewItemBorderBrushCheckedDisabled", "NavigationViewItemBorderBrushSelectedDisabled", "TopNavigationViewItemBackgroundPointerOver", "TopNavigationViewItemBackgroundPressed", "TopNavigationViewItemBackgroundSelected", "NavigationViewBackButtonBackground", "MenuBarBackground", "MenuBarItemBackground", "AppBarButtonBackground", "AppBarButtonBackgroundDisabled", "AppBarButtonBorderBrush", "AppBarButtonBorderBrushPointerOver", "AppBarButtonBorderBrushPressed", "AppBarButtonBorderBrushDisabled", "AppBarToggleButtonBackground", "AppBarToggleButtonBackgroundDisabled", "AppBarToggleButtonBackgroundHighLightOverlay", "AppBarToggleButtonBorderBrush", "AppBarToggleButtonBorderBrushPointerOver", "AppBarToggleButtonBorderBrushPressed", "AppBarToggleButtonBorderBrushDisabled", "AppBarToggleButtonBorderBrushChecked", "AppBarToggleButtonBorderBrushCheckedPointerOver", "AppBarToggleButtonBorderBrushCheckedPressed", "AppBarToggleButtonBorderBrushCheckedDisabled", "ListViewItemBackground", "ListViewItemDragBackground", "TreeViewItemBackgroundDisabled", "TreeViewItemBorderBrush", "TreeViewItemBorderBrushDisabled", "TreeViewItemBorderBrushSelected", "TreeViewItemBorderBrushSelectedDisabled", "TreeViewItemCheckBoxBackgroundSelected", "CommandBarFlyoutButtonBackground", "AppBarButtonBorderBrushSubMenuOpened" } },
            { "SystemControlForegroundAccentBrush", new[] { "SliderThumbBackground", "CheckBoxCheckBackgroundStrokeIndeterminate", "AccentButtonBackground", "AccentButtonBackgroundPointerOver", "RatingControlSelectedForeground", "RatingControlPointerOverSelectedForeground", "NavigationViewSelectionIndicatorForeground" } },
            { "SystemControlHighlightChromeAltLowBrush", new[] { "SliderThumbBackgroundPointerOver", "ContentLinkBackgroundColor", "ColorPickerSliderThumbBackgroundPointerOver" } },
            { "SystemControlHighlightChromeHighBrush", new[] { "SliderThumbBackgroundPressed" } },
            { "SystemControlDisabledChromeDisabledHighBrush", new[] { "SliderThumbBackgroundDisabled", "SliderTrackFillDisabled", "SliderTrackValueFillDisabled", "GridViewItemPlaceholderBackground", "ColorPickerSliderThumbBackgroundDisabled", "ListViewItemPlaceholderBackground" } },
            { "SystemControlForegroundBaseMediumLowBrush", new[] { "SliderTrackFill", "SliderTrackFillPressed", "SliderTickBarFill", "ComboBoxBorderBrush", "AppBarSeparatorForeground", "CalendarDatePickerBorderBrush", "DatePickerButtonBorderBrush", "TimePickerButtonBorderBrush", "TextControlBorderBrush", "MenuBarItemBorderBrush" } },
            { "SystemControlForegroundBaseMediumBrush", new[] { "SliderTrackFillPointerOver", "CheckBoxCheckGlyphForegroundIndeterminatePressed", "CalendarDatePickerTextForeground", "CalendarViewNavigationButtonForegroundPressed", "ToggleMenuFlyoutItemKeyboardAcceleratorTextForeground", "PivotHeaderItemForegroundUnselected", "RatingControlPointerOverPlaceholderForeground", "RatingControlPointerOverUnselectedForeground", "RatingControlCaptionForeground", "TopNavigationViewItemForeground", "MenuFlyoutItemKeyboardAcceleratorTextForeground", "AppBarButtonKeyboardAcceleratorTextForeground", "AppBarToggleButtonKeyboardAcceleratorTextForeground", "AppBarToggleButtonKeyboardAcceleratorTextForegroundChecked" } },
            { "SystemControlHighlightAccentBrush", new[] { "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed", "RadioButtonOuterEllipseCheckedStroke", "RadioButtonOuterEllipseCheckedStrokePointerOver", "CheckBoxCheckBackgroundStrokeIndeterminatePointerOver", "CheckBoxCheckBackgroundFillChecked", "ToggleSwitchFillOn", "ToggleButtonBackgroundChecked", "ToggleButtonBackgroundCheckedPointerOver", "CalendarViewSelectedBorderBrush", "TextControlBorderBrushFocused", "TextControlSelectionHighlightColor", "TextControlButtonBackgroundPressed", "TextControlButtonForegroundPointerOver", "GridViewItemBackgroundSelected", "AppBarToggleButtonBackgroundChecked", "AppBarToggleButtonBackgroundCheckedPointerOver", "AppBarToggleButtonBackgroundCheckedPressed", "SplitButtonBackgroundChecked", "SplitButtonBackgroundCheckedPointerOver" } },
            { "SystemControlForegroundBaseHighBrush", new[] { "SliderHeaderForeground", "ButtonForeground", "RadioButtonForeground", "RadioButtonForegroundPointerOver", "RadioButtonForegroundPressed", "CheckBoxForegroundUnchecked", "CheckBoxForegroundUncheckedPointerOver", "CheckBoxForegroundUncheckedPressed", "CheckBoxForegroundChecked", "CheckBoxForegroundCheckedPointerOver", "CheckBoxForegroundCheckedPressed", "CheckBoxForegroundIndeterminate", "CheckBoxForegroundIndeterminatePointerOver", "CheckBoxForegroundIndeterminatePressed", "CheckBoxCheckGlyphForegroundIndeterminatePointerOver", "RepeatButtonForeground", "ToggleSwitchContentForeground", "ToggleSwitchHeaderForeground", "ToggleButtonForeground", "ToggleButtonForegroundIndeterminate", "ScrollBarButtonArrowForeground", "ScrollBarButtonArrowForegroundPointerOver", "ComboBoxItemForeground", "ComboBoxForeground", "ComboBoxDropDownForeground", "AppBarEllipsisButtonForeground", "AppBarEllipsisButtonForegroundPressed", "AppBarForeground", "ToolTipForeground", "CalendarDatePickerForeground", "CalendarDatePickerTextForegroundSelected", "CalendarViewFocusBorderBrush", "CalendarViewCalendarItemForeground", "CalendarViewNavigationButtonForegroundPointerOver", "DatePickerHeaderForeground", "DatePickerButtonForeground", "TimePickerHeaderForeground", "TimePickerButtonForeground", "LoopingSelectorItemForeground", "TextControlForeground", "TextControlForegroundPointerOver", "TextControlHeaderForeground", "ToggleMenuFlyoutItemForeground", "GridViewItemForeground", "GridViewItemForegroundPointerOver", "GridViewItemForegroundSelected", "GridViewItemFocusSecondaryBorderBrush", "MenuFlyoutItemForeground", "MenuFlyoutSubItemForeground", "RatingControlPlaceholderForeground", "NavigationViewItemForeground", "TopNavigationViewItemForegroundSelected", "ColorPickerSliderThumbBackground", "AppBarButtonForeground", "AppBarToggleButtonForeground", "AppBarToggleButtonCheckGlyphForeground", "AppBarToggleButtonCheckGlyphForegroundChecked", "CommandBarForeground", "ListViewItemForeground", "ListViewItemFocusSecondaryBorderBrush", "TreeViewItemForeground", "SwipeItemForeground", "SplitButtonForeground" } },
            { "SystemControlDisabledBaseMediumLowBrush", new[] { "SliderHeaderForegroundDisabled", "SliderTickBarFillDisabled", "ButtonForegroundDisabled", "RadioButtonForegroundDisabled", "RadioButtonOuterEllipseStrokeDisabled", "RadioButtonOuterEllipseCheckedStrokeDisabled", "RadioButtonCheckGlyphFillDisabled", "CheckBoxForegroundUncheckedDisabled", "CheckBoxForegroundCheckedDisabled", "CheckBoxForegroundIndeterminateDisabled", "CheckBoxCheckBackgroundStrokeUncheckedDisabled", "CheckBoxCheckBackgroundStrokeCheckedDisabled", "CheckBoxCheckBackgroundStrokeIndeterminateDisabled", "CheckBoxCheckGlyphForegroundCheckedDisabled", "CheckBoxCheckGlyphForegroundIndeterminateDisabled", "HyperlinkButtonForegroundDisabled", "RepeatButtonForegroundDisabled", "ToggleSwitchContentForegroundDisabled", "ToggleSwitchHeaderForegroundDisabled", "ToggleSwitchStrokeOffDisabled", "ToggleSwitchStrokeOnDisabled", "ToggleSwitchKnobFillOffDisabled", "ToggleButtonForegroundDisabled", "ToggleButtonForegroundCheckedDisabled", "ToggleButtonForegroundIndeterminateDisabled", "ComboBoxItemForegroundDisabled", "ComboBoxItemForegroundSelectedDisabled", "ComboBoxForegroundDisabled", "ComboBoxDropDownGlyphForegroundDisabled", "AppBarEllipsisButtonForegroundDisabled", "AccentButtonForegroundDisabled", "CalendarDatePickerForegroundDisabled", "CalendarDatePickerCalendarGlyphForegroundDisabled", "CalendarDatePickerTextForegroundDisabled", "CalendarDatePickerHeaderForegroundDisabled", "CalendarViewBlackoutForeground", "CalendarViewWeekDayForegroundDisabled", "CalendarViewNavigationButtonForegroundDisabled", "HubSectionHeaderButtonForegroundDisabled", "DatePickerHeaderForegroundDisabled", "DatePickerButtonForegroundDisabled", "TimePickerHeaderForegroundDisabled", "TimePickerButtonForegroundDisabled", "TextControlHeaderForegroundDisabled", "ToggleMenuFlyoutItemForegroundDisabled", "ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled", "ToggleMenuFlyoutItemCheckGlyphForegroundDisabled", "PivotHeaderItemForegroundDisabled", "JumpListDefaultDisabledForeground", "MenuFlyoutItemForegroundDisabled", "MenuFlyoutSubItemForegroundDisabled", "MenuFlyoutSubItemChevronDisabled", "NavigationViewItemForegroundDisabled", "NavigationViewItemForegroundCheckedDisabled", "NavigationViewItemForegroundSelectedDisabled", "TopNavigationViewItemForegroundDisabled", "AppBarButtonForegroundDisabled", "AppBarToggleButtonForegroundDisabled", "AppBarToggleButtonCheckGlyphForegroundDisabled", "AppBarToggleButtonCheckGlyphForegroundCheckedDisabled", "AppBarToggleButtonOverflowLabelForegroundDisabled", "AppBarToggleButtonOverflowLabelForegroundCheckedDisabled", "CommandBarEllipsisIconForegroundDisabled", "TreeViewItemBackgroundSelectedDisabled", "TreeViewItemForegroundDisabled", "TreeViewItemForegroundSelectedDisabled", "SplitButtonForegroundDisabled", "SplitButtonForegroundCheckedDisabled", "MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled", "AppBarButtonKeyboardAcceleratorTextForegroundDisabled", "AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabled", "AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabled", "AppBarButtonSubItemChevronForegroundDisabled" } },
            { "SystemControlBackgroundAltHighBrush", new[] { "SliderInlineTickBarFill", "CalendarViewCalendarItemBackground", "CalendarViewBackground" } },
            { "SystemControlBackgroundBaseLowBrush", new[] { "ButtonBackground", "ButtonBackgroundPointerOver", "ButtonBackgroundDisabled", "RepeatButtonBackground", "RepeatButtonBackgroundPointerOver", "RepeatButtonBackgroundDisabled", "ThumbBackground", "ToggleButtonBackground", "ToggleButtonBackgroundPointerOver", "ToggleButtonBackgroundDisabled", "ToggleButtonBackgroundCheckedDisabled", "ToggleButtonBackgroundIndeterminate", "ToggleButtonBackgroundIndeterminatePointerOver", "ToggleButtonBackgroundIndeterminateDisabled", "ComboBoxBackgroundDisabled", "ContentDialogBorderBrush", "AccentButtonBackgroundDisabled", "CalendarDatePickerBackgroundPressed", "CalendarDatePickerBackgroundDisabled", "DatePickerButtonBackgroundPressed", "DatePickerButtonBackgroundDisabled", "TimePickerButtonBackgroundPressed", "TimePickerButtonBackgroundDisabled", "TextControlBackgroundDisabled", "JumpListDefaultDisabledBackground", "RatingControlUnselectedForeground", "SwipeItemBackground", "SwipeItemPreThresholdExecuteBackground", "SplitButtonBackground", "SplitButtonBackgroundPointerOver", "SplitButtonBackgroundDisabled", "SplitButtonBackgroundCheckedDisabled" } },
            { "SystemControlBackgroundBaseMediumLowBrush", new[] { "ButtonBackgroundPressed", "RepeatButtonBackgroundPressed", "ToggleButtonBackgroundPressed", "ToggleButtonBackgroundIndeterminatePressed", "ScrollBarThumbFillPointerOver", "AccentButtonBackgroundPressed", "FlipViewNextPreviousButtonBackground", "PivotNextButtonBackground", "PivotPreviousButtonBackground", "AppBarToggleButtonForegroundCheckedDisabled", "SwipeItemBackgroundPressed", "SplitButtonBackgroundPressed" } },
            { "SystemControlHighlightBaseHighBrush", new[] { "ButtonForegroundPointerOver", "ButtonForegroundPressed", "RadioButtonOuterEllipseStrokePointerOver", "CheckBoxCheckBackgroundStrokeUncheckedPointerOver", "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "RepeatButtonForegroundPointerOver", "RepeatButtonForegroundPressed", "ToggleSwitchStrokeOffPointerOver", "ToggleSwitchStrokeOn", "ToggleSwitchKnobFillOffPointerOver", "ToggleButtonForegroundPointerOver", "ToggleButtonForegroundPressed", "ToggleButtonForegroundIndeterminatePointerOver", "ToggleButtonForegroundIndeterminatePressed", "AccentButtonForegroundPressed", "CalendarViewSelectedForeground", "CalendarViewPressedForeground", "DatePickerButtonForegroundPointerOver", "DatePickerButtonForegroundPressed", "TimePickerButtonForegroundPointerOver", "TimePickerButtonForegroundPressed", "SplitButtonForegroundPointerOver", "SplitButtonForegroundPressed" } },
            { "SystemControlForegroundTransparentBrush", new[] { "ButtonBorderBrush", "RepeatButtonBorderBrush", "ToggleButtonBorderBrush", "ToggleButtonBorderBrushIndeterminate", "ScrollBarTrackStroke", "ScrollBarTrackStrokePointerOver", "AppBarHighContrastBorder", "AccentButtonBorderBrush", "FlipViewNextPreviousButtonBorderBrush", "FlipViewNextPreviousButtonBorderBrushPointerOver", "FlipViewNextPreviousButtonBorderBrushPressed", "DateTimePickerFlyoutButtonBorderBrush", "PivotNextButtonBorderBrush", "PivotNextButtonBorderBrushPointerOver", "PivotNextButtonBorderBrushPressed", "PivotPreviousButtonBorderBrush", "PivotPreviousButtonBorderBrushPointerOver", "PivotPreviousButtonBorderBrushPressed", "KeyTipBorderBrush", "CommandBarHighContrastBorder", "SplitButtonBorderBrush" } },
            { "SystemControlHighlightBaseMediumLowBrush", new[] { "ButtonBorderBrushPointerOver", "HyperlinkButtonForegroundPressed", "RepeatButtonBorderBrushPointerOver", "ThumbBackgroundPointerOver", "ToggleButtonBackgroundCheckedPressed", "ToggleButtonBorderBrushPointerOver", "ToggleButtonBorderBrushCheckedPointerOver", "ToggleButtonBorderBrushIndeterminatePointerOver", "ComboBoxBackgroundBorderBrushUnfocused", "ComboBoxBorderBrushPressed", "AccentButtonBorderBrushPointerOver", "CalendarDatePickerBorderBrushPressed", "CalendarViewHoverBorderBrush", "HubSectionHeaderButtonForegroundPressed", "DatePickerButtonBorderBrushPressed", "TimePickerButtonBorderBrushPressed", "MenuBarItemBorderBrushPressed", "MenuBarItemBorderBrushSelected", "SplitButtonBackgroundCheckedPressed", "SplitButtonBorderBrushPointerOver", "SplitButtonBorderBrushCheckedPointerOver" } },
            { "SystemControlHighlightTransparentBrush", new[] { "ButtonBorderBrushPressed", "RadioButtonOuterEllipseCheckedFillPointerOver", "RadioButtonOuterEllipseCheckedFillPressed", "CheckBoxCheckBackgroundStrokeUncheckedPressed", "CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundStrokeCheckedPressed", "CheckBoxCheckBackgroundFillIndeterminate", "CheckBoxCheckBackgroundFillIndeterminatePointerOver", "CheckBoxCheckBackgroundFillIndeterminatePressed", "RepeatButtonBorderBrushPressed", "ThumbBorderBrush", "ThumbBorderBrushPointerOver", "ThumbBorderBrushPressed", "ToggleButtonBorderBrushPressed", "ToggleButtonBorderBrushIndeterminatePressed", "ComboBoxBackgroundBorderBrushFocused", "AccentButtonBorderBrushPressed", "CalendarViewNavigationButtonBorderBrushPointerOver", "DateTimePickerFlyoutButtonBorderBrushPointerOver", "DateTimePickerFlyoutButtonBorderBrushPressed", "PivotHeaderItemBackgroundUnselectedPointerOver", "PivotHeaderItemBackgroundUnselectedPressed", "PivotHeaderItemBackgroundSelected", "PivotHeaderItemBackgroundSelectedPointerOver", "PivotHeaderItemBackgroundSelectedPressed", "SplitButtonBorderBrushPressed" } },
            { "SystemControlDisabledTransparentBrush", new[] { "ButtonBorderBrushDisabled", "RepeatButtonBorderBrushDisabled", "ToggleButtonBorderBrushDisabled", "ToggleButtonBorderBrushCheckedDisabled", "ToggleButtonBorderBrushIndeterminateDisabled", "ScrollBarThumbFillDisabled", "ScrollBarTrackFillDisabled", "ScrollBarTrackStrokeDisabled", "AccentButtonBorderBrushDisabled", "SplitButtonBorderBrushDisabled", "SplitButtonBorderBrushCheckedDisabled" } },
            { "SystemControlForegroundBaseMediumHighBrush", new[] { "RadioButtonOuterEllipseStroke", "CheckBoxCheckBackgroundStrokeUnchecked", "CheckBoxCheckGlyphForegroundIndeterminate", "ToggleSwitchStrokeOff", "ToggleSwitchStrokeOffPressed", "ToggleSwitchKnobFillOff", "ComboBoxDropDownGlyphForeground", "CalendarDatePickerCalendarGlyphForeground", "ToggleMenuFlyoutItemCheckGlyphForeground", "GridViewItemCheckBrush", "MenuFlyoutSubItemChevron", "TopNavigationViewItemForegroundPointerOver", "TopNavigationViewItemForegroundPressed", "ListViewItemCheckBrush", "ListViewItemCheckBoxBrush", "TreeViewItemCheckBoxBorderSelected", "TreeViewItemCheckGlyphSelected", "AppBarButtonSubItemChevronForeground" } },
            { "SystemControlHighlightBaseMediumBrush", new[] { "RadioButtonOuterEllipseStrokePressed", "RadioButtonOuterEllipseCheckedStrokePressed", "CheckBoxCheckBackgroundStrokeIndeterminatePressed", "CheckBoxCheckBackgroundFillCheckedPressed", "ToggleSwitchFillOffPressed", "ToggleSwitchFillOnPressed", "ToggleSwitchStrokeOnPressed", "ThumbBackgroundPressed", "ComboBoxBorderBrushPointerOver", "CalendarDatePickerBorderBrushPointerOver", "CalendarViewPressedBorderBrush", "FlipViewNextPreviousButtonBackgroundPointerOver", "DatePickerButtonBorderBrushPointerOver", "TimePickerButtonBorderBrushPointerOver", "TextControlBorderBrushPointerOver", "PivotNextButtonBackgroundPointerOver", "PivotPreviousButtonBackgroundPointerOver", "MenuBarItemBorderBrushPointerOver" } },
            { "SystemControlHighlightAltTransparentBrush", new[] { "RadioButtonOuterEllipseCheckedFill", "ToggleButtonBorderBrushChecked", "SplitButtonBorderBrushChecked", "SplitButtonBorderBrushCheckedPressed" } },
            { "SystemControlHighlightBaseMediumHighBrush", new[] { "RadioButtonCheckGlyphFill", "FlipViewNextPreviousButtonBackgroundPressed", "PivotNextButtonBackgroundPressed", "PivotPreviousButtonBackgroundPressed" } },
            { "SystemControlHighlightAltBaseHighBrush", new[] { "RadioButtonCheckGlyphFillPointerOver", "ComboBoxItemForegroundPressed", "ComboBoxItemForegroundPointerOver", "ComboBoxItemForegroundSelected", "ComboBoxItemForegroundSelectedUnfocused", "ComboBoxItemForegroundSelectedPressed", "ComboBoxItemForegroundSelectedPointerOver", "ComboBoxForegroundFocused", "ComboBoxForegroundFocusedPressed", "ComboBoxPlaceHolderForegroundFocusedPressed", "AppBarEllipsisButtonForegroundPointerOver", "DateTimePickerFlyoutButtonForegroundPointerOver", "DateTimePickerFlyoutButtonForegroundPressed", "DatePickerButtonForegroundFocused", "TimePickerButtonForegroundFocused", "LoopingSelectorItemForegroundSelected", "LoopingSelectorItemForegroundPointerOver", "LoopingSelectorItemForegroundPressed", "ToggleMenuFlyoutItemForegroundPointerOver", "ToggleMenuFlyoutItemForegroundPressed", "ToggleMenuFlyoutItemCheckGlyphForegroundPointerOver", "ToggleMenuFlyoutItemCheckGlyphForegroundPressed", "PivotHeaderItemForegroundSelected", "MenuFlyoutItemForegroundPointerOver", "MenuFlyoutItemForegroundPressed", "MenuFlyoutSubItemForegroundPointerOver", "MenuFlyoutSubItemForegroundPressed", "MenuFlyoutSubItemForegroundSubMenuOpened", "MenuFlyoutSubItemChevronPointerOver", "MenuFlyoutSubItemChevronPressed", "MenuFlyoutSubItemChevronSubMenuOpened", "NavigationViewItemForegroundPointerOver", "NavigationViewItemForegroundPressed", "NavigationViewItemForegroundChecked", "NavigationViewItemForegroundCheckedPointerOver", "NavigationViewItemForegroundCheckedPressed", "NavigationViewItemForegroundSelected", "NavigationViewItemForegroundSelectedPointerOver", "NavigationViewItemForegroundSelectedPressed", "AppBarButtonForegroundPointerOver", "AppBarButtonForegroundPressed", "AppBarToggleButtonForegroundPointerOver", "AppBarToggleButtonForegroundPressed", "AppBarToggleButtonForegroundChecked", "AppBarToggleButtonForegroundCheckedPointerOver", "AppBarToggleButtonForegroundCheckedPressed", "AppBarToggleButtonCheckGlyphForegroundPointerOver", "AppBarToggleButtonCheckGlyphForegroundPressed", "AppBarToggleButtonCheckGlyphForegroundCheckedPointerOver", "AppBarToggleButtonCheckGlyphForegroundCheckedPressed", "AppBarToggleButtonOverflowLabelForegroundPointerOver", "AppBarToggleButtonOverflowLabelForegroundPressed", "AppBarToggleButtonOverflowLabelForegroundCheckedPointerOver", "AppBarToggleButtonOverflowLabelForegroundCheckedPressed", "ListViewItemForegroundPointerOver", "ListViewItemForegroundSelected", "TreeViewItemForegroundPointerOver", "TreeViewItemForegroundPressed", "TreeViewItemForegroundSelected", "TreeViewItemForegroundSelectedPointerOver", "TreeViewItemForegroundSelectedPressed", "AppBarButtonForegroundSubMenuOpened", "AppBarButtonSubItemChevronForegroundPointerOver", "AppBarButtonSubItemChevronForegroundPressed", "AppBarButtonSubItemChevronForegroundSubMenuOpened" } },
            { "SystemControlHighlightAltBaseMediumBrush", new[] { "RadioButtonCheckGlyphFillPressed", "ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver", "ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", "MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver", "MenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", "AppBarButtonKeyboardAcceleratorTextForegroundPointerOver", "AppBarButtonKeyboardAcceleratorTextForegroundPressed", "AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOver", "AppBarToggleButtonKeyboardAcceleratorTextForegroundPressed", "AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOver", "AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressed", "AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpened" } },
            { "SystemControlBackgroundBaseMediumBrush", new[] { "CheckBoxCheckBackgroundFillUncheckedPressed", "ScrollBarButtonBackgroundPressed", "ScrollBarThumbFillPressed", "SwipeItemPreThresholdExecuteForeground" } },
            { "SystemControlBackgroundAccentBrush", new[] { "CheckBoxCheckBackgroundFillCheckedPointerOver", "JumpListDefaultEnabledBackground", "SwipeItemPostThresholdExecuteBackground" } },
            { "SystemControlHighlightAltChromeWhiteBrush", new[] { "CheckBoxCheckGlyphForegroundUnchecked", "CheckBoxCheckGlyphForegroundUncheckedPointerOver", "CheckBoxCheckGlyphForegroundUncheckedPressed", "CheckBoxCheckGlyphForegroundUncheckedDisabled", "CheckBoxCheckGlyphForegroundChecked", "ToggleSwitchKnobFillOffPressed", "ToggleSwitchKnobFillOn", "ToggleSwitchKnobFillOnPressed", "ToggleButtonForegroundChecked", "ToggleButtonForegroundCheckedPointerOver", "ToggleButtonForegroundCheckedPressed", "CalendarViewTodayForeground", "TextControlButtonForegroundPressed", "GridViewItemDragForeground", "ListViewItemDragForeground", "SplitButtonForegroundChecked", "SplitButtonForegroundCheckedPointerOver", "SplitButtonForegroundCheckedPressed" } },
            { "SystemControlForegroundChromeWhiteBrush", new[] { "CheckBoxCheckGlyphForegroundCheckedPointerOver", "CheckBoxCheckGlyphForegroundCheckedPressed", "JumpListDefaultEnabledForeground", "SwipeItemPostThresholdExecuteForeground" } },
            { "SystemControlHyperlinkTextBrush", new[] { "HyperlinkButtonForeground", "HubSectionHeaderButtonForeground", "ContentLinkForegroundColor" } },
            { "SystemControlPageBackgroundTransparentBrush", new[] { "HyperlinkButtonBackground", "HyperlinkButtonBackgroundPointerOver", "HyperlinkButtonBackgroundPressed", "HyperlinkButtonBackgroundDisabled" } },
            { "SystemControlHighlightAltListAccentHighBrush", new[] { "ToggleSwitchFillOnPointerOver" } },
            { "SystemControlDisabledBaseLowBrush", new[] { "ToggleSwitchFillOnDisabled", "ComboBoxBorderBrushDisabled", "CalendarDatePickerBorderBrushDisabled", "DatePickerSpacerFillDisabled", "DatePickerButtonBorderBrushDisabled", "TimePickerSpacerFillDisabled", "TimePickerButtonBorderBrushDisabled", "TextControlBorderBrushDisabled", "ColorPickerSliderTrackFillDisabled" } },
            { "SystemControlHighlightListAccentHighBrush", new[] { "ToggleSwitchStrokeOnPointerOver", "ComboBoxItemBackgroundSelectedPressed", "CalendarViewSelectedPressedBorderBrush", "GridViewItemBackgroundSelectedPressed", "MenuFlyoutSubItemBackgroundPressed", "ListViewItemBackgroundSelectedPressed" } },
            { "SystemControlHighlightChromeWhiteBrush", new[] { "ToggleSwitchKnobFillOnPointerOver" } },
            { "SystemControlPageBackgroundBaseLowBrush", new[] { "ToggleSwitchKnobFillOnDisabled" } },
            { "SystemControlBackgroundListLowBrush", new[] { "ScrollBarButtonBackgroundPointerOver", "ComboBoxDropDownBackgroundPointerOver", "MenuBarItemBackgroundPointerOver" } },
            { "SystemControlForegroundAltHighBrush", new[] { "ScrollBarButtonArrowForegroundPressed", "TextControlHighlighterForeground", "GridViewItemFocusBorderBrush", "ListViewItemFocusBorderBrush" } },
            { "SystemControlForegroundBaseLowBrush", new[] { "ScrollBarButtonArrowForegroundDisabled", "ListViewHeaderItemDividerStroke", "DatePickerSpacerFill", "DatePickerFlyoutPresenterSpacerFill", "TimePickerSpacerFill", "TimePickerFlyoutPresenterSpacerFill", "GridViewHeaderItemDividerStroke" } },
            { "SystemControlForegroundChromeDisabledLowBrush", new[] { "ScrollBarThumbFill" } },
            { "SystemControlDisabledChromeHighBrush", new[] { "ScrollBarPanningThumbBackgroundDisabled" } },
            { "SystemBaseLowColor", new[] { "ScrollBarThumbBackgroundColor" } },
            { "SystemChromeDisabledLowColor", new[] { "ScrollBarPanningThumbBackgroundColor" } },
            { "SystemControlHighlightListMediumBrush", new[] { "ComboBoxItemBackgroundPressed", "AppBarEllipsisButtonBackgroundPressed", "DateTimePickerFlyoutButtonBackgroundPressed", "LoopingSelectorItemBackgroundPressed", "ToggleMenuFlyoutItemBackgroundPressed", "GridViewItemBackgroundPressed", "MenuFlyoutItemBackgroundPressed", "AppBarButtonBackgroundPressed", "AppBarToggleButtonBackgroundHighLightOverlayPressed", "AppBarToggleButtonBackgroundHighLightOverlayCheckedPressed", "ListViewItemBackgroundPressed" } },
            { "SystemControlHighlightListLowBrush", new[] { "ComboBoxItemBackgroundPointerOver", "AppBarEllipsisButtonBackgroundPointerOver", "DateTimePickerFlyoutButtonBackgroundPointerOver", "LoopingSelectorItemBackgroundPointerOver", "ToggleMenuFlyoutItemBackgroundPointerOver", "GridViewItemBackgroundPointerOver", "MenuFlyoutItemBackgroundPointerOver", "MenuFlyoutSubItemBackgroundPointerOver", "AppBarButtonBackgroundPointerOver", "AppBarToggleButtonBackgroundHighLightOverlayPointerOver", "AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOver", "ListViewItemBackgroundPointerOver" } },
            { "SystemControlHighlightListAccentLowBrush", new[] { "ComboBoxItemBackgroundSelected", "ComboBoxItemBackgroundSelectedUnfocused", "ComboBoxBackgroundUnfocused", "CalendarDatePickerBackgroundFocused", "DatePickerButtonBackgroundFocused", "DatePickerFlyoutPresenterHighlightFill", "TimePickerButtonBackgroundFocused", "TimePickerFlyoutPresenterHighlightFill", "MenuFlyoutSubItemBackgroundSubMenuOpened", "ListViewItemBackgroundSelected", "AppBarButtonBackgroundSubMenuOpened" } },
            { "SystemControlHighlightListAccentMediumBrush", new[] { "ComboBoxItemBackgroundSelectedPointerOver", "CalendarViewSelectedHoverBorderBrush", "GridViewItemBackgroundSelectedPointerOver", "ListViewItemBackgroundSelectedPointerOver" } },
            { "SystemControlBackgroundAltMediumLowBrush", new[] { "ComboBoxBackground", "CalendarDatePickerBackground", "DatePickerButtonBackground", "TimePickerButtonBackground", "TextControlBackground" } },
            { "SystemControlPageBackgroundAltMediumBrush", new[] { "ComboBoxBackgroundPointerOver", "CalendarDatePickerBackgroundPointerOver", "DatePickerButtonBackgroundPointerOver", "TimePickerButtonBackgroundPointerOver", "MediaTransportControlsPanelBackground" } },
            { "SystemControlBackgroundListMediumBrush", new[] { "ComboBoxBackgroundPressed", "ComboBoxDropDownBackgroundPointerPressed", "MenuBarItemBackgroundPressed", "MenuBarItemBackgroundSelected" } },
            { "SystemControlPageTextBaseHighBrush", new[] { "ComboBoxPlaceHolderForeground", "ContentDialogForeground", "HubForeground", "HubSectionHeaderForeground" } },
            { "SystemControlBackgroundChromeBlackLowBrush", new[] { "ComboBoxFocusedDropDownBackgroundPointerOver" } },
            { "SystemControlBackgroundChromeBlackMediumLowBrush", new[] { "ComboBoxFocusedDropDownBackgroundPointerPressed" } },
            { "SystemControlForegroundAltMediumHighBrush", new[] { "ComboBoxEditableDropDownGlyphForeground", "FlipViewNextPreviousArrowForeground", "PivotNextButtonForeground", "PivotPreviousButtonForeground" } },
            { "SystemControlHighlightAltBaseMediumHighBrush", new[] { "ComboBoxDropDownGlyphForegroundFocused", "ComboBoxDropDownGlyphForegroundFocusedPressed", "PivotHeaderItemForegroundUnselectedPointerOver", "PivotHeaderItemForegroundUnselectedPressed", "PivotHeaderItemForegroundSelectedPointerOver", "PivotHeaderItemForegroundSelectedPressed" } },
            { "SystemControlTransientBackgroundBrush", new[] { "ComboBoxDropDownBackground", "DatePickerFlyoutPresenterBackground", "TimePickerFlyoutPresenterBackground", "FlyoutPresenterBackground", "MediaTransportControlsFlyoutBackground", "MenuFlyoutPresenterBackground", "CommandBarOverflowPresenterBackground", "AutoSuggestBoxSuggestionsListBackground" } },
            { "SystemControlTransientBorderBrush", new[] { "ComboBoxDropDownBorderBrush", "ToolTipBorderBrush", "DatePickerFlyoutPresenterBorderBrush", "TimePickerFlyoutPresenterBorderBrush", "FlyoutBorderThemeBrush", "MenuFlyoutPresenterBorderBrush", "CommandBarOverflowPresenterBorderBrush", "AutoSuggestBoxSuggestionsListBorderBrush" } },
            { "SystemControlBackgroundChromeMediumBrush", new[] { "AppBarBackground", "LoopingSelectorButtonBackground", "GridViewItemCheckBoxBrush", "CommandBarBackground" } },
            { "SystemControlPageBackgroundAltHighBrush", new[] { "ContentDialogBackground" } },
            { "SystemControlBackgroundChromeWhiteBrush", new[] { "AccentButtonForeground", "AccentButtonForegroundPointerOver", "TextControlBackgroundFocused", "KeyTipForeground" } },
            { "SystemControlBackgroundChromeMediumLowBrush", new[] { "ToolTipBackground" } },
            { "SystemControlHyperlinkBaseHighBrush", new[] { "CalendarViewOutOfScopeForeground" } },
            { "SystemControlDisabledChromeMediumLowBrush", new[] { "CalendarViewOutOfScopeBackground" } },
            { "SystemControlHyperlinkBaseMediumHighBrush", new[] { "CalendarViewForeground" } },
            { "SystemControlForegroundChromeMediumBrush", new[] { "CalendarViewBorderBrush" } },
            { "SystemControlHyperlinkBaseMediumBrush", new[] { "HubSectionHeaderButtonForegroundPointerOver" } },
            { "SystemControlPageBackgroundListLowBrush", new[] { "FlipViewBackground" } },
            { "SystemControlHighlightAltAltMediumHighBrush", new[] { "FlipViewNextPreviousArrowForegroundPointerOver", "FlipViewNextPreviousArrowForegroundPressed", "PivotNextButtonForegroundPointerOver", "PivotNextButtonForegroundPressed", "PivotPreviousButtonForegroundPointerOver", "PivotPreviousButtonForegroundPressed" } },
            { "SystemControlForegroundChromeBlackHighBrush", new[] { "TextControlForegroundFocused" } },
            { "SystemControlDisabledChromeDisabledLowBrush", new[] { "TextControlForegroundDisabled", "TextControlPlaceholderForegroundDisabled" } },
            { "SystemControlBackgroundAltMediumBrush", new[] { "TextControlBackgroundPointerOver" } },
            { "SystemControlPageTextChromeBlackMediumLowBrush", new[] { "TextControlPlaceholderForegroundFocused" } },
            { "SystemControlForegroundChromeBlackMediumBrush", new[] { "TextControlButtonForeground" } },
            { "SystemControlHighlightAltAccentBrush", new[] { "PivotHeaderItemFocusPipeFill", "PivotHeaderItemSelectedPipeFill" } },
            { "SystemControlFocusVisualPrimaryBrush", new[] { "GridViewItemFocusVisualPrimaryBrush", "ListViewItemFocusVisualPrimaryBrush" } },
            { "SystemControlFocusVisualSecondaryBrush", new[] { "GridViewItemFocusVisualSecondaryBrush", "ListViewItemFocusVisualSecondaryBrush" } },
            { "SystemControlPageBackgroundMediumAltMediumBrush", new[] { "AppBarLightDismissOverlayBackground", "CalendarDatePickerLightDismissOverlayBackground", "ComboBoxLightDismissOverlayBackground", "DatePickerLightDismissOverlayBackground", "FlyoutLightDismissOverlayBackground", "PopupLightDismissOverlayBackground", "SplitViewLightDismissOverlayBackground", "TimePickerLightDismissOverlayBackground", "MenuFlyoutLightDismissOverlayBackground", "CommandBarLightDismissOverlayBackground", "AutoSuggestBoxLightDismissOverlayBackground" } },
            { "SystemControlForegroundChromeGrayBrush", new[] { "KeyTipBackground" } },
            { "SystemBaseMediumLowColor", new[] { "RatingControlDisabledSelectedForeground" } },
            { "SystemControlForegroundChromeHighBrush", new[] { "ColorPickerSliderThumbBackgroundPressed" } },
            { "SystemControlDisabledAccentBrush", new[] { "AppBarToggleButtonBackgroundCheckedDisabled" } },
        };
    }

    public class ThemeCustomInfo : ThemeInfoBase
    {
        public ThemeCustomInfo()
        {
            Values = new Dictionary<string, object>();
        }

        public Dictionary<string, object> Values { get; private set; }

        public string Path { get; set; }



        public static bool Equals(ThemeCustomInfo x, ThemeCustomInfo y)
        {
            if (x.Parent != y.Parent)
            {
                return false;
            }

            bool equal = false;
            if (x.Values.Count == y.Values.Count) // Require equal count.
            {
                equal = true;
                foreach (var pair in x.Values)
                {
                    if (y.Values.TryGetValue(pair.Key, out object value))
                    {
                        // Require value be equal.
                        if (!Equals(value, pair.Value))
                        {
                            equal = false;
                            break;
                        }
                    }
                    else
                    {
                        // Require key be present.
                        equal = false;
                        break;
                    }
                }
            }

            return equal;
        }
    }

    public class ThemeBundledInfo : ThemeInfoBase
    {

    }

    public abstract class ThemeInfoBase
    {
        public string Name { get; set; }
        public TelegramTheme Parent { get; set; }
    }
}
