using System;
using System.Collections.Generic;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.Views.Popups;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsThemePage : HostedPage
    {
        private ThemeGroup _group;
        private ThemeCustomInfo _theme;

        public SettingsThemePage()
        {
            InitializeComponent();

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedRight"))
            {
                MenuFlyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
            }

#if !DEBUG
            Microsoft.AppCenter.Analytics.Analytics.TrackEvent("SettingsThemePage");
#endif
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var path = e.Parameter as string;
            var file = await StorageFile.GetFileFromPathAsync(path);

            var theme = _theme = await TLContainer.Current.Resolve<IThemeService>().DeserializeAsync(file);

            Load(theme);
        }

        public void Load(ThemeCustomInfo theme)
        {
            TitleLabel.Text = theme.Name;

            _theme = theme;

            ResourceDictionary baseTheme;
            // This isn't working as orignal theme base resources are going to be picked
            //if (theme.Parent.HasFlag(TelegramTheme.Brand))
            //{
            //    baseTheme = new ResourceDictionary { Source = new Uri("ms-appx:///Themes/ThemeGreen.xaml") };
            //    baseTheme = baseTheme.ThemeDictionaries[theme.Parent.HasFlag(TelegramTheme.Light) ? "Light" : "Dark"] as ResourceDictionary;
            //}
            //else
            //{
            //    baseTheme = new ResourceDictionary { Source = new Uri("ms-appx:///Themes/ThemeSystem.xaml") };
            //    baseTheme = baseTheme.ThemeDictionaries[theme.Parent.HasFlag(TelegramTheme.Light) ? "Light" : "Dark"] as ResourceDictionary;
            //}
            baseTheme = Theme.Current;
            if (theme.Parent == TelegramTheme.Light)
            {
                baseTheme = baseTheme.MergedDictionaries[0].ThemeDictionaries["Light"] as ResourceDictionary;
            }
            else
            {
                baseTheme = baseTheme.MergedDictionaries[0].ThemeDictionaries["Dark"] as ResourceDictionary;
            }

            var service = TLContainer.Current.Resolve<IThemeService>();

            var group = _group = new ThemeGroup("All", service, theme, baseTheme);
            var groups = new List<ThemeGroup>();

            var items = new ThemeGroup("Header", service, theme, baseTheme);
            items.Add(group.AddBrush("PageHeaderHighlightBrush"));
            items.Add(group.AddBrush("PageHeaderDisabledBrush"));
            items.Add(group.AddBrush("PageHeaderForegroundBrush"));
            items.Add(group.AddBrush("PageHeaderBackgroundBrush"));
            items.Add(group.AddBrush("PageSubHeaderBackgroundBrush"));
            groups.Add(items);

            items = new ThemeGroup("Accent", service, theme, baseTheme);
            items.Add(group.AddColor("SystemAccentColor"));
            items.Add(group.AddBrush("SystemControlBackgroundAccentBrush", resourceKey: "SystemAccentColor"));
            items.Add(group.AddBrush("SystemControlDisabledAccentBrush", resourceKey: "SystemAccentColor"));
            items.Add(group.AddBrush("SystemControlForegroundAccentBrush", resourceKey: "SystemAccentColor"));
            items.Add(group.AddBrush("SystemControlHighlightAccentBrush", resourceKey: "SystemAccentColor"));
            items.Add(group.AddBrush("SystemControlHighlightAltAccentBrush", resourceKey: "SystemAccentColor"));
            groups.Add(items);

            items = new ThemeGroup("Content", service, theme, baseTheme);
            items.Add(group.AddBrush("ApplicationPageBackgroundThemeBrush"));
            items.Add(group.AddBrush("TelegramSeparatorMediumBrush"));
            //items.Add(group.AddBrush("SystemControlPageTextBaseHighBrush"));;
            items.Add(group.AddBrush("SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("SystemControlDisabledChromeDisabledLowBrush"));
            groups.Add(items);

            items = new ThemeGroup("Chats", service, theme, baseTheme);
            items.Add(group.AddBrush("ChatPageBackgroundBrush", resourceKey: "ApplicationPageBackgroundThemeBrush"));
            items.Add(group.AddBrush("ChatOnlineBadgeBrush"));
            items.Add(group.AddBrush("ChatVerifiedBadgeBrush"));
            items.Add(group.AddBrush("ChatLastMessageStateBrush"));
            items.Add(group.AddBrush("ChatFromLabelBrush"));
            items.Add(group.AddBrush("ChatDraftLabelBrush"));
            items.Add(group.AddBrush("ChatUnreadBadgeBrush"));
            items.Add(group.AddBrush("ChatUnreadLabelBrush", "ChatUnreadBadgeBrush"));
            items.Add(group.AddBrush("ChatUnreadBadgeMutedBrush"));
            items.Add(group.AddBrush("ChatUnreadLabelMutedBrush", "ChatUnreadBadgeMutedBrush"));
            items.Add(group.AddBrush("ChatFailedBadgeBrush"));
            items.Add(group.AddBrush("ChatFailedLabelBrush", "ChatFailedBadgeBrush"));
            groups.Add(items);

            var messageThickness = new ThemeBooleanPart { Key = "MessageBorderThickness" };
            group.Add("MessageBorderThickness", messageThickness);

            items = new ThemeGroup("Incoming messages", service, theme, baseTheme);
            items.Add(group.AddColor("MessageForegroundColor"));
            items.Add(group.AddColor("MessageForegroundLinkColor"));
            items.Add(group.AddColor("MessageBackgroundColor"));
            //items.Add(group.AddColor("MessageBorderColor"));
            //items.Add("MessageBorderThickness", messageThickness);
            items.Add(group.AddColor("MessageSubtleLabelColor"));
            items.Add(group.AddColor("MessageSubtleGlyphColor"));
            items.Add(group.AddColor("MessageSubtleForegroundColor"));
            items.Add(group.AddColor("MessageHeaderForegroundColor"));
            items.Add(group.AddColor("MessageHeaderBorderColor"));
            items.Add(group.AddColor("MessageMediaForegroundColor"));
            items.Add(group.AddColor("MessageMediaBackgroundColor"));
            items.Add(group.AddColor("MessageOverlayBackgroundColor"));
            items.Add(group.AddColor("MessageCallForegroundColor"));
            items.Add(group.AddColor("MessageCallMissedForegroundColor"));
            groups.Add(items);

            items = new ThemeGroup("Outgoing messages", service, theme, baseTheme);
            items.Add(group.AddColor("MessageForegroundOutColor"));
            items.Add(group.AddColor("MessageForegroundLinkOutColor"));
            items.Add(group.AddColor("MessageBackgroundOutColor"));
            //items.Add(group.AddColor("MessageBorderOutColor"));
            //items.Add("MessageBorderThickness", messageThickness);
            items.Add(group.AddColor("MessageSubtleLabelOutColor"));
            items.Add(group.AddColor("MessageSubtleGlyphOutColor"));
            items.Add(group.AddColor("MessageSubtleForegroundOutColor"));
            items.Add(group.AddColor("MessageHeaderForegroundOutColor"));
            items.Add(group.AddColor("MessageHeaderBorderOutColor"));
            items.Add(group.AddColor("MessageMediaForegroundOutColor"));
            items.Add(group.AddColor("MessageMediaBackgroundOutColor"));
            items.Add(group.AddColor("MessageOverlayBackgroundOutColor"));
            items.Add(group.AddColor("MessageCallForegroundOutColor"));
            items.Add(group.AddColor("MessageCallMissedForegroundOutColor"));
            groups.Add(items);



            items = new ThemeGroup("ToggleSwitch", service, theme, baseTheme);
            items.Add(group.AddBrush("ToggleSwitchContentForeground"));
            items.Add(group.AddBrush("ToggleSwitchContentForegroundDisabled"));
            items.Add(group.AddBrush("ToggleSwitchHeaderForeground"));
            items.Add(group.AddBrush("ToggleSwitchHeaderForegroundDisabled"));
            items.Add(group.AddBrush("ToggleSwitchStrokeOff"));
            items.Add(group.AddBrush("ToggleSwitchStrokeOffPointerOver"));
            items.Add(group.AddBrush("ToggleSwitchStrokeOffPressed"));
            items.Add(group.AddBrush("ToggleSwitchStrokeOffDisabled"));
            items.Add(group.AddBrush("ToggleSwitchFillOn"));
            items.Add(group.AddBrush("ToggleSwitchFillOnPointerOver"));
            items.Add(group.AddBrush("ToggleSwitchFillOnPressed"));
            items.Add(group.AddBrush("ToggleSwitchFillOnDisabled"));
            items.Add(group.AddBrush("ToggleSwitchKnobFillOff"));
            items.Add(group.AddBrush("ToggleSwitchKnobFillOffPointerOver"));
            items.Add(group.AddBrush("ToggleSwitchKnobFillOffPressed"));
            items.Add(group.AddBrush("ToggleSwitchKnobFillOffDisabled"));
            items.Add(group.AddBrush("ToggleSwitchKnobFillOn"));
            items.Add(group.AddBrush("ToggleSwitchKnobFillOnPointerOver"));
            items.Add(group.AddBrush("ToggleSwitchKnobFillOnPressed"));
            items.Add(group.AddBrush("ToggleSwitchKnobFillOnDisabled"));
            groups.Add(items);

            items = new ThemeGroup("RadioButton", service, theme, baseTheme);
            items.Add(group.AddBrush("RadioButtonForeground", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("RadioButtonForegroundPointerOver", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("RadioButtonForegroundPressed", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("RadioButtonForegroundDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            items.Add(group.AddBrush("RadioButtonOuterEllipseStroke", resourceKey: "SystemControlForegroundBaseMediumHighBrush"));
            items.Add(group.AddBrush("RadioButtonOuterEllipseStrokePointerOver", resourceKey: "SystemControlHighlightBaseHighBrush"));
            items.Add(group.AddBrush("RadioButtonOuterEllipseStrokePressed", resourceKey: "SystemControlHighlightBaseMediumBrush"));
            items.Add(group.AddBrush("RadioButtonOuterEllipseStrokeDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            items.Add(group.AddBrush("RadioButtonOuterEllipseCheckedStroke", resourceKey: "SystemControlHighlightAccentBrush"));
            items.Add(group.AddBrush("RadioButtonOuterEllipseCheckedStrokePointerOver", resourceKey: "SystemControlHighlightAccentBrush"));
            items.Add(group.AddBrush("RadioButtonOuterEllipseCheckedStrokePressed", resourceKey: "SystemControlHighlightBaseMediumBrush"));
            items.Add(group.AddBrush("RadioButtonOuterEllipseCheckedStrokeDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            items.Add(group.AddBrush("RadioButtonCheckGlyphFill", resourceKey: "SystemControlHighlightBaseMediumHighBrush"));
            items.Add(group.AddBrush("RadioButtonCheckGlyphFillPointerOver", resourceKey: "SystemControlHighlightAltBaseHighBrush"));
            items.Add(group.AddBrush("RadioButtonCheckGlyphFillPressed", resourceKey: "SystemControlHighlightAltBaseMediumBrush"));
            items.Add(group.AddBrush("RadioButtonCheckGlyphFillDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            groups.Add(items);

            items = new ThemeGroup("CheckBox", service, theme, baseTheme);
            items.Add(group.AddBrush("CheckBoxForegroundUnchecked", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundUncheckedPointerOver", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundUncheckedPressed", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundUncheckedDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundChecked", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundCheckedPointerOver", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundCheckedPressed", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundCheckedDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundIndeterminate", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundIndeterminatePointerOver", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundIndeterminatePressed", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxForegroundIndeterminateDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundStrokeUnchecked", resourceKey: "SystemControlForegroundBaseMediumHighBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundStrokeUncheckedPointerOver", resourceKey: "SystemControlHighlightBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundStrokeUncheckedDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundStrokeCheckedPointerOver", resourceKey: "SystemControlHighlightBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundStrokeCheckedDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundStrokeIndeterminate", resourceKey: "SystemControlForegroundAccentBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundStrokeIndeterminatePointerOver", resourceKey: "SystemControlHighlightAccentBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundStrokeIndeterminatePressed", resourceKey: "SystemControlHighlightBaseMediumBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundStrokeIndeterminateDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundFillUncheckedPressed", resourceKey: "SystemControlBackgroundBaseMediumBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundFillChecked", resourceKey: "SystemControlHighlightAccentBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundFillCheckedPointerOver", resourceKey: "SystemControlBackgroundAccentBrush"));
            items.Add(group.AddBrush("CheckBoxCheckBackgroundFillCheckedPressed", resourceKey: "SystemControlHighlightBaseMediumBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundUnchecked", resourceKey: "SystemControlHighlightAltChromeWhiteBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundUncheckedPointerOver", resourceKey: "SystemControlHighlightAltChromeWhiteBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundUncheckedPressed", resourceKey: "SystemControlHighlightAltChromeWhiteBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundUncheckedDisabled", resourceKey: "SystemControlHighlightAltChromeWhiteBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundChecked", resourceKey: "SystemControlHighlightAltChromeWhiteBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundCheckedPointerOver", resourceKey: "SystemControlForegroundChromeWhiteBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundCheckedPressed", resourceKey: "SystemControlForegroundChromeWhiteBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundCheckedDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundIndeterminate", resourceKey: "SystemControlForegroundBaseMediumHighBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundIndeterminatePointerOver", resourceKey: "SystemControlForegroundBaseHighBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundIndeterminatePressed", resourceKey: "SystemControlForegroundBaseMediumBrush"));
            items.Add(group.AddBrush("CheckBoxCheckGlyphForegroundIndeterminateDisabled", resourceKey: "SystemControlDisabledBaseMediumLowBrush"));
            groups.Add(items);

            var collection = new CollectionViewSource();
            collection.Source = groups;
            collection.IsSourceGrouped = true;
            collection.ItemsPath = new PropertyPath("Values");

            List.ItemsSource = collection.View;
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputPopup();
            dialog.Title = Strings.Resources.EditName;
            dialog.Text = _theme.Name;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            _theme.Name = dialog.Text;

            TitleLabel.Text = _theme.Name;

            var file = await StorageFile.GetFileFromPathAsync(_theme.Path);
            await TLContainer.Current.Resolve<IThemeService>().SerializeAsync(file, _theme);
        }

        private async void Done_Click(object sender, RoutedEventArgs e)
        {
            if (Window.Current.Content is Host.RootPage root)
            {
                root.HideEditor();
            }

            //_theme.Values.Clear();

            //foreach (var item in _group.Values)
            //{
            //    if (item is ThemeBrushPart brush && !brush.IsDefault)
            //    {
            //        _theme.Values[brush.Key] = brush.Value;
            //    }
            //    else if (item is ThemeColorPart color && !color.IsDefault)
            //    {
            //        _theme.Values[color.Key] = color.Value;
            //    }
            //}

            //var file = await StorageFile.GetFileFromPathAsync(_theme.Path);
            //await TLContainer.Current.Resolve<IThemeService>().SerializeAsync(file, _theme);
        }

        private void List_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is ThemeColorPart color)
            {
                var root = args.ItemContainer.ContentTemplateRoot as Grid;
                if (root == null)
                {
                    return;
                }

                var presenter = root.FindName("Presenter") as ContentControl;
                if (presenter == null)
                {
                    return;
                }

                if (Resources.TryGetValue($"{color.Key}Template", out object value) && value is DataTemplate template)
                {
                    presenter.ContentTemplate = template;
                }
                else if (color.DependsUpon == null)
                {
                    presenter.ContentTemplate = Resources["ThemeBrushPartTemplate"] as DataTemplate;
                }
                else
                {
                    presenter.ContentTemplate = new DataTemplate();
                }
            }
        }

        private async void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ThemeColorPart part)
            {
                var picker = new Microsoft.UI.Xaml.Controls.ColorPicker();
                picker.IsAlphaEnabled = true;
                picker.Color = part.Value;
                picker.PreviousColor = part.Value;
                picker.Margin = new Thickness(12, 12, 12, 0);

                var dialog = new ContentPopup();
                dialog.PrimaryButtonText = Strings.Resources.Save;
                dialog.SecondaryButtonText = part.IsDefault ? string.Empty : Strings.Resources.Default;
                dialog.CloseButtonText = Strings.Resources.Cancel;
                dialog.Content = picker;
                dialog.TitleTemplate = null;
                dialog.DefaultButton = ContentDialogButton.Primary;

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    part.Value = picker.Color;
                }
                else if (confirm == ContentDialogResult.Secondary)
                {
                    part.Reset();
                }
                else
                {
                    return;
                }

                Update();
            }
        }

        private void Boolean_Click(object sender, RoutedEventArgs e)
        {
            var check = sender as CheckBox;
            if (check != null && check.Tag is ThemeBooleanPart)
            {
                Update();
            }
        }

        private async void Update()
        {
            var value = SettingsService.Current.Appearance.GetActualTheme();
            var mapping = TLContainer.Current.Resolve<IThemeService>().GetMapping(_theme.Parent);

            _theme.Values.Clear();

            foreach (TLWindowContext window in WindowContext.ActiveWrappers)
            {
                await window.Dispatcher.DispatchAsync(() =>
                {
                    var dict = new ResourceDictionary();
                    foreach (var item in _group.Values)
                    {
                        if (item is ThemeBrushPart brush && !brush.IsDefault)
                        {
                            _theme.Values[brush.Key] = brush.Value;
                            dict[brush.Key] = new SolidColorBrush(brush.Value);

                            if (mapping.TryGetValue(brush.Key, out string[] additional))
                            {
                                foreach (var key in additional)
                                {
                                    dict[key] = new SolidColorBrush(brush.Value);
                                }
                            }
                        }
                        else if (item is ThemeColorPart color && !color.IsDefault)
                        {
                            _theme.Values[color.Key] = color.Value;
                            dict[color.Key] = color.Value;
                        }
                        else if (item is ThemeBooleanPart boolean && boolean.Value)
                        {
                            dict["MessageBorderThickness"] = new Thickness(1);
                            dict["MessageBorderNegativeThickness"] = new Thickness(-1);
                        }
                    }

                    Theme.Current.MergedDictionaries[0].MergedDictionaries.Clear();
                    Theme.Current.MergedDictionaries[0].MergedDictionaries.Add(dict);

                    window.UpdateTitleBar();

                    if (window.Content is FrameworkElement element)
                    {
                        if (value == element.RequestedTheme)
                        {
                            element.RequestedTheme = value == ElementTheme.Dark
                                ? ElementTheme.Light
                                : ElementTheme.Dark;
                        }

                        element.RequestedTheme = value;
                    }
                });
            }

            var file = await StorageFile.GetFileFromPathAsync(_theme.Path);
            await TLContainer.Current.Resolve<IThemeService>().SerializeAsync(file, _theme);
        }
    }

    public class ThemeGroup : Dictionary<string, ThemePartBase>
    {
        private readonly IThemeService _service;
        private readonly ThemeCustomInfo _model;
        private readonly ResourceDictionary _super;

        public ThemeGroup(string key, IThemeService service, ThemeCustomInfo model, ResourceDictionary super)
        {
            _service = service;
            _super = super;
            _model = model;

            Key = key;
        }

        public string Key { get; set; }

        public ThemeBrushPart AddBrush(string key, string dependsUpon = null, string resourceKey = null)
        {
            ThemeBrushPart item;
            Add(key, item = new ThemeBrushPart(key, dependsUpon, resourceKey, _service, this, _model, _super));
            return item;
        }

        public ThemeColorPart AddColor(string key, string dependsUpon = null, string resourceKey = null)
        {
            ThemeColorPart item;
            Add(key, item = new ThemeColorPart(key, dependsUpon, resourceKey, _service, this, _model, _super));
            return item;
        }

        public void Add(ThemeColorPart part)
        {
            Add(part.Key, part);
        }

        public bool TryGetColor(string key, out ThemeColorPart value)
        {
            if (TryGetValue(key, out var result) && result is ThemeColorPart)
            {
                value = result as ThemeColorPart;
                return true;
            }

            value = null;
            return false;
        }
    }

    public abstract class ThemePartBase : BindableBase
    {
        public string Key { get; set; }
        public string Description { get; set; }
    }

    public abstract class ThemePropertyPart : ThemePartBase
    {
        public bool IsResource { get; }

        public abstract object GetValue();
    }

    public class ThemeNumberPart : ThemePropertyPart
    {
        private int _value;
        public int Value
        {
            get { return _value; }
            set { Set(ref _value, value); }
        }

        public override object GetValue()
        {
            return Value;
        }
    }

    public class ThemeThicknessPart : ThemeNumberPart
    {
        public override object GetValue()
        {
            return new Thickness(Value);
        }
    }

    public class ThemeBooleanPart : ThemePropertyPart
    {
        private bool _value;
        public bool Value
        {
            get { return _value; }
            set { Set(ref _value, value); }
        }

        public override object GetValue()
        {
            return Value;
        }
    }

    public class ThemeBrushPart : ThemeColorPart
    {
        public ThemeBrushPart(string key, string dependsUpon, string resourceKey, IThemeService service, ThemeGroup group, ThemeCustomInfo model, ResourceDictionary super)
            : base(key, dependsUpon, resourceKey, group)
        {
            _super = service.GetDefaultColor(model.Parent, key);

            if (model.Values.TryGetValue(key, out Color mcolor))
            {
                _value = mcolor;
            }
            else if (resourceKey != null)
            {
                //_super = GetDefaultColor(resourceKey, service, group, model);
                _super = default;
                _value = default;
            }
            else if (super.TryGet(key, out SolidColorBrush brush))
            {
                _value = brush.Color;
            }
        }
    }

    public class ThemeColorPart : ThemePartBase
    {
        protected readonly ThemeGroup _group;
        protected Color _super;

        protected ThemeColorPart(string key, string dependsUpon, string resourceKey, ThemeGroup group)
        {
            Key = key;
            DependsUpon = dependsUpon;
            ResourceKey = resourceKey;

            //if (resourceKey != null)
            //{
            //    Description = string.Format("When empty, **{0}** is used.", resourceKey);
            //}

            _group = group;
        }

        public ThemeColorPart(string key, string dependsUpon, string resourceKey, IThemeService service, ThemeGroup group, ThemeCustomInfo model, ResourceDictionary super)
            : this(key, dependsUpon, resourceKey, group)
        {
            _super = service.GetDefaultColor(model.Parent, key);

            if (model.Values.TryGetValue(key, out Color mcolor))
            {
                _value = mcolor;
            }
            else if (resourceKey != null)
            {
                //_super = GetDefaultColor(resourceKey, service, group, model);
                _super = default;
                _value = default;
            }
            else if (super.TryGet(key, out Color color))
            {
                _value = color;
            }
        }

        public string DependsUpon { get; private set; }
        public string ResourceKey { get; private set; }

        protected Color _value;
        public Color Value
        {
            get { return _value; }
            set { SetValue(value); }
        }

        private void SetValue(Color value)
        {
            //foreach (ThemeColorPart part in _group.Values)
            //{
            //    if (string.Equals(part.ResourceKey, Key))
            //    {

            //    }
            //}

            Set(ref _value, value);
            RaisePropertyChanged(nameof(IsDefault));
            RaisePropertyChanged(nameof(HexValue));
        }

        private void SetSuper(Color super)
        {
            _super = super;
            RaisePropertyChanged(nameof(IsDefault));
        }

        public bool IsDefault => _value == _super || _value == default;



        public ThemeGroup Group => _group;



        public string HexValue
        {
            get
            {
                if (ResourceKey != null && IsDefault)
                {
                    return $"**{ResourceKey}**";
                }

                if (_value.A < 255)
                {
                    return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", _value.A, _value.R, _value.G, _value.B);
                }

                return string.Format("#{0:X2}{1:X2}{2:X2}", _value.R, _value.G, _value.B);
            }
        }

        public void Reset()
        {
            if (ResourceKey != null)
            {
                Value = default(Color);
            }
            else
            {
                Value = _super;
            }
        }



        protected static Color GetDefaultColor(string key, IThemeService service, ThemeGroup group, ThemeCustomInfo model)
        {
            while (group.TryGetColor(key, out ThemeColorPart parent))
            {
                if (parent.ResourceKey == null)
                {
                    return parent.Value;
                }
                else
                {
                    key = parent.ResourceKey;
                }
            }

            return default(Color);
        }
    }

    public class ThemePartTemplateSelector : DataTemplateSelector
    {
        public DataTemplate BrushTemplate { get; set; }
        public DataTemplate ColorTemplate { get; set; }
        public DataTemplate BooleanTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is ThemeColorPart)
            {
                return BrushTemplate;
            }
            else if (item is ThemeBooleanPart)
            {
                return BooleanTemplate;
            }
            //else if (item is ThemeColorPart)
            //{
            //    return ColorTemplate;
            //}

            return base.SelectTemplateCore(item);
        }
    }

    public class ResourcesMapper : Control
    {
        public object PageHeaderDisabledBrush
        {
            get { return GetValue(PageHeaderDisabledBrushProperty); }
            set { SetValue(PageHeaderDisabledBrushProperty, value); }
        }

        public static readonly DependencyProperty PageHeaderDisabledBrushProperty =
            DependencyProperty.Register("PageHeaderDisabledBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PageHeaderBackgroundBrush
        {
            get { return GetValue(PageHeaderBackgroundBrushProperty); }
            set { SetValue(PageHeaderBackgroundBrushProperty, value); }
        }

        public static readonly DependencyProperty PageHeaderBackgroundBrushProperty =
            DependencyProperty.Register("PageHeaderBackgroundBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PageSubHeaderBackgroundBrush
        {
            get { return GetValue(PageSubHeaderBackgroundBrushProperty); }
            set { SetValue(PageSubHeaderBackgroundBrushProperty, value); }
        }

        public static readonly DependencyProperty PageSubHeaderBackgroundBrushProperty =
            DependencyProperty.Register("PageSubHeaderBackgroundBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TelegramSeparatorMediumBrush
        {
            get { return GetValue(TelegramSeparatorMediumBrushProperty); }
            set { SetValue(TelegramSeparatorMediumBrushProperty, value); }
        }

        public static readonly DependencyProperty TelegramSeparatorMediumBrushProperty =
            DependencyProperty.Register("TelegramSeparatorMediumBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageBackgroundOutColor
        {
            get { return GetValue(MessageBackgroundOutColorProperty); }
            set { SetValue(MessageBackgroundOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageBackgroundOutColorProperty =
            DependencyProperty.Register("MessageBackgroundOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageSubtleLabelOutColor
        {
            get { return GetValue(MessageSubtleLabelOutColorProperty); }
            set { SetValue(MessageSubtleLabelOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageSubtleLabelOutColorProperty =
            DependencyProperty.Register("MessageSubtleLabelOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageSubtleGlyphOutColor
        {
            get { return GetValue(MessageSubtleGlyphOutColorProperty); }
            set { SetValue(MessageSubtleGlyphOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageSubtleGlyphOutColorProperty =
            DependencyProperty.Register("MessageSubtleGlyphOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageSubtleForegroundColor
        {
            get { return GetValue(MessageSubtleForegroundColorProperty); }
            set { SetValue(MessageSubtleForegroundColorProperty, value); }
        }

        public static readonly DependencyProperty MessageSubtleForegroundColorProperty =
            DependencyProperty.Register("MessageSubtleForegroundColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageSubtleForegroundOutColor
        {
            get { return GetValue(MessageSubtleForegroundOutColorProperty); }
            set { SetValue(MessageSubtleForegroundOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageSubtleForegroundOutColorProperty =
            DependencyProperty.Register("MessageSubtleForegroundOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageHeaderForegroundOutColor
        {
            get { return GetValue(MessageHeaderForegroundOutColorProperty); }
            set { SetValue(MessageHeaderForegroundOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageHeaderForegroundOutColorProperty =
            DependencyProperty.Register("MessageHeaderForegroundOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageHeaderBorderOutColor
        {
            get { return GetValue(MessageHeaderBorderOutColorProperty); }
            set { SetValue(MessageHeaderBorderOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageHeaderBorderOutColorProperty =
            DependencyProperty.Register("MessageHeaderBorderOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageMediaForegroundOutColor
        {
            get { return GetValue(MessageMediaForegroundOutColorProperty); }
            set { SetValue(MessageMediaForegroundOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageMediaForegroundOutColorProperty =
            DependencyProperty.Register("MessageMediaForegroundOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageMediaBackgroundOutColor
        {
            get { return GetValue(MessageMediaBackgroundOutColorProperty); }
            set { SetValue(MessageMediaBackgroundOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageMediaBackgroundOutColorProperty =
            DependencyProperty.Register("MessageMediaBackgroundOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SystemControlDescriptionTextForegroundBrush
        {
            get { return GetValue(SystemControlDescriptionTextForegroundBrushProperty); }
            set { SetValue(SystemControlDescriptionTextForegroundBrushProperty, value); }
        }

        public static readonly DependencyProperty SystemControlDescriptionTextForegroundBrushProperty =
            DependencyProperty.Register("SystemControlDescriptionTextForegroundBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderThumbBackground
        {
            get { return GetValue(SliderThumbBackgroundProperty); }
            set { SetValue(SliderThumbBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SliderThumbBackgroundProperty =
            DependencyProperty.Register("SliderThumbBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderThumbBackgroundPointerOver
        {
            get { return GetValue(SliderThumbBackgroundPointerOverProperty); }
            set { SetValue(SliderThumbBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SliderThumbBackgroundPointerOverProperty =
            DependencyProperty.Register("SliderThumbBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderThumbBackgroundPressed
        {
            get { return GetValue(SliderThumbBackgroundPressedProperty); }
            set { SetValue(SliderThumbBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty SliderThumbBackgroundPressedProperty =
            DependencyProperty.Register("SliderThumbBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderThumbBackgroundDisabled
        {
            get { return GetValue(SliderThumbBackgroundDisabledProperty); }
            set { SetValue(SliderThumbBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty SliderThumbBackgroundDisabledProperty =
            DependencyProperty.Register("SliderThumbBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackFill
        {
            get { return GetValue(SliderTrackFillProperty); }
            set { SetValue(SliderTrackFillProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackFillProperty =
            DependencyProperty.Register("SliderTrackFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackFillPointerOver
        {
            get { return GetValue(SliderTrackFillPointerOverProperty); }
            set { SetValue(SliderTrackFillPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackFillPointerOverProperty =
            DependencyProperty.Register("SliderTrackFillPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackFillPressed
        {
            get { return GetValue(SliderTrackFillPressedProperty); }
            set { SetValue(SliderTrackFillPressedProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackFillPressedProperty =
            DependencyProperty.Register("SliderTrackFillPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackFillDisabled
        {
            get { return GetValue(SliderTrackFillDisabledProperty); }
            set { SetValue(SliderTrackFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackFillDisabledProperty =
            DependencyProperty.Register("SliderTrackFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackValueFill
        {
            get { return GetValue(SliderTrackValueFillProperty); }
            set { SetValue(SliderTrackValueFillProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackValueFillProperty =
            DependencyProperty.Register("SliderTrackValueFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackValueFillPointerOver
        {
            get { return GetValue(SliderTrackValueFillPointerOverProperty); }
            set { SetValue(SliderTrackValueFillPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackValueFillPointerOverProperty =
            DependencyProperty.Register("SliderTrackValueFillPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackValueFillPressed
        {
            get { return GetValue(SliderTrackValueFillPressedProperty); }
            set { SetValue(SliderTrackValueFillPressedProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackValueFillPressedProperty =
            DependencyProperty.Register("SliderTrackValueFillPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackValueFillDisabled
        {
            get { return GetValue(SliderTrackValueFillDisabledProperty); }
            set { SetValue(SliderTrackValueFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackValueFillDisabledProperty =
            DependencyProperty.Register("SliderTrackValueFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderHeaderForeground
        {
            get { return GetValue(SliderHeaderForegroundProperty); }
            set { SetValue(SliderHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty SliderHeaderForegroundProperty =
            DependencyProperty.Register("SliderHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderHeaderForegroundDisabled
        {
            get { return GetValue(SliderHeaderForegroundDisabledProperty); }
            set { SetValue(SliderHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty SliderHeaderForegroundDisabledProperty =
            DependencyProperty.Register("SliderHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTickBarFill
        {
            get { return GetValue(SliderTickBarFillProperty); }
            set { SetValue(SliderTickBarFillProperty, value); }
        }

        public static readonly DependencyProperty SliderTickBarFillProperty =
            DependencyProperty.Register("SliderTickBarFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTickBarFillDisabled
        {
            get { return GetValue(SliderTickBarFillDisabledProperty); }
            set { SetValue(SliderTickBarFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty SliderTickBarFillDisabledProperty =
            DependencyProperty.Register("SliderTickBarFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderInlineTickBarFill
        {
            get { return GetValue(SliderInlineTickBarFillProperty); }
            set { SetValue(SliderInlineTickBarFillProperty, value); }
        }

        public static readonly DependencyProperty SliderInlineTickBarFillProperty =
            DependencyProperty.Register("SliderInlineTickBarFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBackground
        {
            get { return GetValue(ButtonBackgroundProperty); }
            set { SetValue(ButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ButtonBackgroundProperty =
            DependencyProperty.Register("ButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBackgroundPointerOver
        {
            get { return GetValue(ButtonBackgroundPointerOverProperty); }
            set { SetValue(ButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("ButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBackgroundPressed
        {
            get { return GetValue(ButtonBackgroundPressedProperty); }
            set { SetValue(ButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ButtonBackgroundPressedProperty =
            DependencyProperty.Register("ButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBackgroundDisabled
        {
            get { return GetValue(ButtonBackgroundDisabledProperty); }
            set { SetValue(ButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ButtonBackgroundDisabledProperty =
            DependencyProperty.Register("ButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonForeground
        {
            get { return GetValue(ButtonForegroundProperty); }
            set { SetValue(ButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty ButtonForegroundProperty =
            DependencyProperty.Register("ButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonForegroundPointerOver
        {
            get { return GetValue(ButtonForegroundPointerOverProperty); }
            set { SetValue(ButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ButtonForegroundPointerOverProperty =
            DependencyProperty.Register("ButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonForegroundPressed
        {
            get { return GetValue(ButtonForegroundPressedProperty); }
            set { SetValue(ButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ButtonForegroundPressedProperty =
            DependencyProperty.Register("ButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonForegroundDisabled
        {
            get { return GetValue(ButtonForegroundDisabledProperty); }
            set { SetValue(ButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ButtonForegroundDisabledProperty =
            DependencyProperty.Register("ButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBorderBrush
        {
            get { return GetValue(ButtonBorderBrushProperty); }
            set { SetValue(ButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ButtonBorderBrushProperty =
            DependencyProperty.Register("ButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBorderBrushPointerOver
        {
            get { return GetValue(ButtonBorderBrushPointerOverProperty); }
            set { SetValue(ButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("ButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBorderBrushPressed
        {
            get { return GetValue(ButtonBorderBrushPressedProperty); }
            set { SetValue(ButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty ButtonBorderBrushPressedProperty =
            DependencyProperty.Register("ButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBorderBrushDisabled
        {
            get { return GetValue(ButtonBorderBrushDisabledProperty); }
            set { SetValue(ButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty ButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("ButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonForeground
        {
            get { return GetValue(RadioButtonForegroundProperty); }
            set { SetValue(RadioButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonForegroundProperty =
            DependencyProperty.Register("RadioButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonForegroundPointerOver
        {
            get { return GetValue(RadioButtonForegroundPointerOverProperty); }
            set { SetValue(RadioButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonForegroundPointerOverProperty =
            DependencyProperty.Register("RadioButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonForegroundPressed
        {
            get { return GetValue(RadioButtonForegroundPressedProperty); }
            set { SetValue(RadioButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonForegroundPressedProperty =
            DependencyProperty.Register("RadioButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonForegroundDisabled
        {
            get { return GetValue(RadioButtonForegroundDisabledProperty); }
            set { SetValue(RadioButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonForegroundDisabledProperty =
            DependencyProperty.Register("RadioButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseStroke
        {
            get { return GetValue(RadioButtonOuterEllipseStrokeProperty); }
            set { SetValue(RadioButtonOuterEllipseStrokeProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseStrokeProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseStroke", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseStrokePointerOver
        {
            get { return GetValue(RadioButtonOuterEllipseStrokePointerOverProperty); }
            set { SetValue(RadioButtonOuterEllipseStrokePointerOverProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseStrokePointerOverProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseStrokePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseStrokePressed
        {
            get { return GetValue(RadioButtonOuterEllipseStrokePressedProperty); }
            set { SetValue(RadioButtonOuterEllipseStrokePressedProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseStrokePressedProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseStrokePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseStrokeDisabled
        {
            get { return GetValue(RadioButtonOuterEllipseStrokeDisabledProperty); }
            set { SetValue(RadioButtonOuterEllipseStrokeDisabledProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseStrokeDisabledProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseStrokeDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedStroke
        {
            get { return GetValue(RadioButtonOuterEllipseCheckedStrokeProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedStrokeProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedStrokeProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedStroke", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedStrokePointerOver
        {
            get { return GetValue(RadioButtonOuterEllipseCheckedStrokePointerOverProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedStrokePointerOverProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedStrokePointerOverProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedStrokePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedStrokePressed
        {
            get { return GetValue(RadioButtonOuterEllipseCheckedStrokePressedProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedStrokePressedProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedStrokePressedProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedStrokePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedStrokeDisabled
        {
            get { return GetValue(RadioButtonOuterEllipseCheckedStrokeDisabledProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedStrokeDisabledProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedStrokeDisabledProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedStrokeDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedFill
        {
            get { return GetValue(RadioButtonOuterEllipseCheckedFillProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedFillProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedFillProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedFillPointerOver
        {
            get { return GetValue(RadioButtonOuterEllipseCheckedFillPointerOverProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedFillPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedFillPointerOverProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedFillPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedFillPressed
        {
            get { return GetValue(RadioButtonOuterEllipseCheckedFillPressedProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedFillPressedProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedFillPressedProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedFillPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonCheckGlyphFill
        {
            get { return GetValue(RadioButtonCheckGlyphFillProperty); }
            set { SetValue(RadioButtonCheckGlyphFillProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonCheckGlyphFillProperty =
            DependencyProperty.Register("RadioButtonCheckGlyphFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonCheckGlyphFillPointerOver
        {
            get { return GetValue(RadioButtonCheckGlyphFillPointerOverProperty); }
            set { SetValue(RadioButtonCheckGlyphFillPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonCheckGlyphFillPointerOverProperty =
            DependencyProperty.Register("RadioButtonCheckGlyphFillPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonCheckGlyphFillPressed
        {
            get { return GetValue(RadioButtonCheckGlyphFillPressedProperty); }
            set { SetValue(RadioButtonCheckGlyphFillPressedProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonCheckGlyphFillPressedProperty =
            DependencyProperty.Register("RadioButtonCheckGlyphFillPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonCheckGlyphFillDisabled
        {
            get { return GetValue(RadioButtonCheckGlyphFillDisabledProperty); }
            set { SetValue(RadioButtonCheckGlyphFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonCheckGlyphFillDisabledProperty =
            DependencyProperty.Register("RadioButtonCheckGlyphFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundUnchecked
        {
            get { return GetValue(CheckBoxForegroundUncheckedProperty); }
            set { SetValue(CheckBoxForegroundUncheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundUncheckedProperty =
            DependencyProperty.Register("CheckBoxForegroundUnchecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundUncheckedPointerOver
        {
            get { return GetValue(CheckBoxForegroundUncheckedPointerOverProperty); }
            set { SetValue(CheckBoxForegroundUncheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundUncheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxForegroundUncheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundUncheckedPressed
        {
            get { return GetValue(CheckBoxForegroundUncheckedPressedProperty); }
            set { SetValue(CheckBoxForegroundUncheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundUncheckedPressedProperty =
            DependencyProperty.Register("CheckBoxForegroundUncheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundUncheckedDisabled
        {
            get { return GetValue(CheckBoxForegroundUncheckedDisabledProperty); }
            set { SetValue(CheckBoxForegroundUncheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundUncheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxForegroundUncheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundChecked
        {
            get { return GetValue(CheckBoxForegroundCheckedProperty); }
            set { SetValue(CheckBoxForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundCheckedProperty =
            DependencyProperty.Register("CheckBoxForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundCheckedPointerOver
        {
            get { return GetValue(CheckBoxForegroundCheckedPointerOverProperty); }
            set { SetValue(CheckBoxForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundCheckedPressed
        {
            get { return GetValue(CheckBoxForegroundCheckedPressedProperty); }
            set { SetValue(CheckBoxForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundCheckedPressedProperty =
            DependencyProperty.Register("CheckBoxForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundCheckedDisabled
        {
            get { return GetValue(CheckBoxForegroundCheckedDisabledProperty); }
            set { SetValue(CheckBoxForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundCheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundIndeterminate
        {
            get { return GetValue(CheckBoxForegroundIndeterminateProperty); }
            set { SetValue(CheckBoxForegroundIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundIndeterminateProperty =
            DependencyProperty.Register("CheckBoxForegroundIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundIndeterminatePointerOver
        {
            get { return GetValue(CheckBoxForegroundIndeterminatePointerOverProperty); }
            set { SetValue(CheckBoxForegroundIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundIndeterminatePointerOverProperty =
            DependencyProperty.Register("CheckBoxForegroundIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundIndeterminatePressed
        {
            get { return GetValue(CheckBoxForegroundIndeterminatePressedProperty); }
            set { SetValue(CheckBoxForegroundIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundIndeterminatePressedProperty =
            DependencyProperty.Register("CheckBoxForegroundIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundIndeterminateDisabled
        {
            get { return GetValue(CheckBoxForegroundIndeterminateDisabledProperty); }
            set { SetValue(CheckBoxForegroundIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundIndeterminateDisabledProperty =
            DependencyProperty.Register("CheckBoxForegroundIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeUnchecked
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeUncheckedProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeUncheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeUncheckedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeUnchecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeUncheckedPointerOver
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeUncheckedPointerOverProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeUncheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeUncheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeUncheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeUncheckedPressed
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeUncheckedPressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeUncheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeUncheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeUncheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeUncheckedDisabled
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeUncheckedDisabledProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeUncheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeUncheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeUncheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeChecked
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeCheckedProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeCheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeCheckedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeCheckedPointerOver
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeCheckedPointerOverProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeCheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeCheckedPressed
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeCheckedPressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeCheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeCheckedDisabled
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeCheckedDisabledProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeCheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeIndeterminate
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeIndeterminateProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeIndeterminateProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeIndeterminatePointerOver
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeIndeterminatePointerOverProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeIndeterminatePointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeIndeterminatePressed
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeIndeterminatePressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeIndeterminatePressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeIndeterminateDisabled
        {
            get { return GetValue(CheckBoxCheckBackgroundStrokeIndeterminateDisabledProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeIndeterminateDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillUncheckedPressed
        {
            get { return GetValue(CheckBoxCheckBackgroundFillUncheckedPressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillUncheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillUncheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillUncheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillChecked
        {
            get { return GetValue(CheckBoxCheckBackgroundFillCheckedProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillCheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillCheckedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillCheckedPointerOver
        {
            get { return GetValue(CheckBoxCheckBackgroundFillCheckedPointerOverProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillCheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillCheckedPressed
        {
            get { return GetValue(CheckBoxCheckBackgroundFillCheckedPressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillCheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillIndeterminate
        {
            get { return GetValue(CheckBoxCheckBackgroundFillIndeterminateProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillIndeterminateProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillIndeterminatePointerOver
        {
            get { return GetValue(CheckBoxCheckBackgroundFillIndeterminatePointerOverProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillIndeterminatePointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillIndeterminatePressed
        {
            get { return GetValue(CheckBoxCheckBackgroundFillIndeterminatePressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillIndeterminatePressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundUnchecked
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundUncheckedProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundUncheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundUncheckedProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundUnchecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundUncheckedPointerOver
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundUncheckedPointerOverProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundUncheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundUncheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundUncheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundUncheckedPressed
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundUncheckedPressedProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundUncheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundUncheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundUncheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundUncheckedDisabled
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundUncheckedDisabledProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundUncheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundUncheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundUncheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundChecked
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundCheckedProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundCheckedProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundCheckedPointerOver
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundCheckedPointerOverProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundCheckedPressed
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundCheckedPressedProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundCheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundCheckedDisabled
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundCheckedDisabledProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundCheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundIndeterminate
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundIndeterminateProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundIndeterminateProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundIndeterminatePointerOver
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundIndeterminatePointerOverProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundIndeterminatePointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundIndeterminatePressed
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundIndeterminatePressedProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundIndeterminatePressedProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundIndeterminateDisabled
        {
            get { return GetValue(CheckBoxCheckGlyphForegroundIndeterminateDisabledProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundIndeterminateDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonForeground
        {
            get { return GetValue(HyperlinkButtonForegroundProperty); }
            set { SetValue(HyperlinkButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonForegroundProperty =
            DependencyProperty.Register("HyperlinkButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonForegroundPointerOver
        {
            get { return GetValue(HyperlinkButtonForegroundPointerOverProperty); }
            set { SetValue(HyperlinkButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonForegroundPointerOverProperty =
            DependencyProperty.Register("HyperlinkButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonForegroundPressed
        {
            get { return GetValue(HyperlinkButtonForegroundPressedProperty); }
            set { SetValue(HyperlinkButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonForegroundPressedProperty =
            DependencyProperty.Register("HyperlinkButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonForegroundDisabled
        {
            get { return GetValue(HyperlinkButtonForegroundDisabledProperty); }
            set { SetValue(HyperlinkButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonForegroundDisabledProperty =
            DependencyProperty.Register("HyperlinkButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonBackground
        {
            get { return GetValue(HyperlinkButtonBackgroundProperty); }
            set { SetValue(HyperlinkButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonBackgroundProperty =
            DependencyProperty.Register("HyperlinkButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonBackgroundPointerOver
        {
            get { return GetValue(HyperlinkButtonBackgroundPointerOverProperty); }
            set { SetValue(HyperlinkButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("HyperlinkButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonBackgroundPressed
        {
            get { return GetValue(HyperlinkButtonBackgroundPressedProperty); }
            set { SetValue(HyperlinkButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonBackgroundPressedProperty =
            DependencyProperty.Register("HyperlinkButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonBackgroundDisabled
        {
            get { return GetValue(HyperlinkButtonBackgroundDisabledProperty); }
            set { SetValue(HyperlinkButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonBackgroundDisabledProperty =
            DependencyProperty.Register("HyperlinkButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBackground
        {
            get { return GetValue(RepeatButtonBackgroundProperty); }
            set { SetValue(RepeatButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBackgroundProperty =
            DependencyProperty.Register("RepeatButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBackgroundPointerOver
        {
            get { return GetValue(RepeatButtonBackgroundPointerOverProperty); }
            set { SetValue(RepeatButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("RepeatButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBackgroundPressed
        {
            get { return GetValue(RepeatButtonBackgroundPressedProperty); }
            set { SetValue(RepeatButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBackgroundPressedProperty =
            DependencyProperty.Register("RepeatButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBackgroundDisabled
        {
            get { return GetValue(RepeatButtonBackgroundDisabledProperty); }
            set { SetValue(RepeatButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBackgroundDisabledProperty =
            DependencyProperty.Register("RepeatButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonForeground
        {
            get { return GetValue(RepeatButtonForegroundProperty); }
            set { SetValue(RepeatButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonForegroundProperty =
            DependencyProperty.Register("RepeatButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonForegroundPointerOver
        {
            get { return GetValue(RepeatButtonForegroundPointerOverProperty); }
            set { SetValue(RepeatButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonForegroundPointerOverProperty =
            DependencyProperty.Register("RepeatButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonForegroundPressed
        {
            get { return GetValue(RepeatButtonForegroundPressedProperty); }
            set { SetValue(RepeatButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonForegroundPressedProperty =
            DependencyProperty.Register("RepeatButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonForegroundDisabled
        {
            get { return GetValue(RepeatButtonForegroundDisabledProperty); }
            set { SetValue(RepeatButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonForegroundDisabledProperty =
            DependencyProperty.Register("RepeatButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBorderBrush
        {
            get { return GetValue(RepeatButtonBorderBrushProperty); }
            set { SetValue(RepeatButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBorderBrushProperty =
            DependencyProperty.Register("RepeatButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBorderBrushPointerOver
        {
            get { return GetValue(RepeatButtonBorderBrushPointerOverProperty); }
            set { SetValue(RepeatButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("RepeatButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBorderBrushPressed
        {
            get { return GetValue(RepeatButtonBorderBrushPressedProperty); }
            set { SetValue(RepeatButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBorderBrushPressedProperty =
            DependencyProperty.Register("RepeatButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBorderBrushDisabled
        {
            get { return GetValue(RepeatButtonBorderBrushDisabledProperty); }
            set { SetValue(RepeatButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("RepeatButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchContentForeground
        {
            get { return GetValue(ToggleSwitchContentForegroundProperty); }
            set { SetValue(ToggleSwitchContentForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchContentForegroundProperty =
            DependencyProperty.Register("ToggleSwitchContentForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchContentForegroundDisabled
        {
            get { return GetValue(ToggleSwitchContentForegroundDisabledProperty); }
            set { SetValue(ToggleSwitchContentForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchContentForegroundDisabledProperty =
            DependencyProperty.Register("ToggleSwitchContentForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchHeaderForeground
        {
            get { return GetValue(ToggleSwitchHeaderForegroundProperty); }
            set { SetValue(ToggleSwitchHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchHeaderForegroundProperty =
            DependencyProperty.Register("ToggleSwitchHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchHeaderForegroundDisabled
        {
            get { return GetValue(ToggleSwitchHeaderForegroundDisabledProperty); }
            set { SetValue(ToggleSwitchHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchHeaderForegroundDisabledProperty =
            DependencyProperty.Register("ToggleSwitchHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchFillOffPressed
        {
            get { return GetValue(ToggleSwitchFillOffPressedProperty); }
            set { SetValue(ToggleSwitchFillOffPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchFillOffPressedProperty =
            DependencyProperty.Register("ToggleSwitchFillOffPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOff
        {
            get { return GetValue(ToggleSwitchStrokeOffProperty); }
            set { SetValue(ToggleSwitchStrokeOffProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOffProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOff", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOffPointerOver
        {
            get { return GetValue(ToggleSwitchStrokeOffPointerOverProperty); }
            set { SetValue(ToggleSwitchStrokeOffPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOffPointerOverProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOffPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOffPressed
        {
            get { return GetValue(ToggleSwitchStrokeOffPressedProperty); }
            set { SetValue(ToggleSwitchStrokeOffPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOffPressedProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOffPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOffDisabled
        {
            get { return GetValue(ToggleSwitchStrokeOffDisabledProperty); }
            set { SetValue(ToggleSwitchStrokeOffDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOffDisabledProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOffDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchFillOn
        {
            get { return GetValue(ToggleSwitchFillOnProperty); }
            set { SetValue(ToggleSwitchFillOnProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchFillOnProperty =
            DependencyProperty.Register("ToggleSwitchFillOn", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchFillOnPointerOver
        {
            get { return GetValue(ToggleSwitchFillOnPointerOverProperty); }
            set { SetValue(ToggleSwitchFillOnPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchFillOnPointerOverProperty =
            DependencyProperty.Register("ToggleSwitchFillOnPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchFillOnPressed
        {
            get { return GetValue(ToggleSwitchFillOnPressedProperty); }
            set { SetValue(ToggleSwitchFillOnPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchFillOnPressedProperty =
            DependencyProperty.Register("ToggleSwitchFillOnPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchFillOnDisabled
        {
            get { return GetValue(ToggleSwitchFillOnDisabledProperty); }
            set { SetValue(ToggleSwitchFillOnDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchFillOnDisabledProperty =
            DependencyProperty.Register("ToggleSwitchFillOnDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOn
        {
            get { return GetValue(ToggleSwitchStrokeOnProperty); }
            set { SetValue(ToggleSwitchStrokeOnProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOnProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOn", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOnPointerOver
        {
            get { return GetValue(ToggleSwitchStrokeOnPointerOverProperty); }
            set { SetValue(ToggleSwitchStrokeOnPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOnPointerOverProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOnPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOnPressed
        {
            get { return GetValue(ToggleSwitchStrokeOnPressedProperty); }
            set { SetValue(ToggleSwitchStrokeOnPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOnPressedProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOnPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOnDisabled
        {
            get { return GetValue(ToggleSwitchStrokeOnDisabledProperty); }
            set { SetValue(ToggleSwitchStrokeOnDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOnDisabledProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOnDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOff
        {
            get { return GetValue(ToggleSwitchKnobFillOffProperty); }
            set { SetValue(ToggleSwitchKnobFillOffProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOffProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOff", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOffPointerOver
        {
            get { return GetValue(ToggleSwitchKnobFillOffPointerOverProperty); }
            set { SetValue(ToggleSwitchKnobFillOffPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOffPointerOverProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOffPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOffPressed
        {
            get { return GetValue(ToggleSwitchKnobFillOffPressedProperty); }
            set { SetValue(ToggleSwitchKnobFillOffPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOffPressedProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOffPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOffDisabled
        {
            get { return GetValue(ToggleSwitchKnobFillOffDisabledProperty); }
            set { SetValue(ToggleSwitchKnobFillOffDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOffDisabledProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOffDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOn
        {
            get { return GetValue(ToggleSwitchKnobFillOnProperty); }
            set { SetValue(ToggleSwitchKnobFillOnProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOnProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOn", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOnPointerOver
        {
            get { return GetValue(ToggleSwitchKnobFillOnPointerOverProperty); }
            set { SetValue(ToggleSwitchKnobFillOnPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOnPointerOverProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOnPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOnPressed
        {
            get { return GetValue(ToggleSwitchKnobFillOnPressedProperty); }
            set { SetValue(ToggleSwitchKnobFillOnPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOnPressedProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOnPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOnDisabled
        {
            get { return GetValue(ToggleSwitchKnobFillOnDisabledProperty); }
            set { SetValue(ToggleSwitchKnobFillOnDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOnDisabledProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOnDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBackground
        {
            get { return GetValue(ThumbBackgroundProperty); }
            set { SetValue(ThumbBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ThumbBackgroundProperty =
            DependencyProperty.Register("ThumbBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBackgroundPointerOver
        {
            get { return GetValue(ThumbBackgroundPointerOverProperty); }
            set { SetValue(ThumbBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ThumbBackgroundPointerOverProperty =
            DependencyProperty.Register("ThumbBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBackgroundPressed
        {
            get { return GetValue(ThumbBackgroundPressedProperty); }
            set { SetValue(ThumbBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ThumbBackgroundPressedProperty =
            DependencyProperty.Register("ThumbBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBorderBrush
        {
            get { return GetValue(ThumbBorderBrushProperty); }
            set { SetValue(ThumbBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ThumbBorderBrushProperty =
            DependencyProperty.Register("ThumbBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBorderBrushPointerOver
        {
            get { return GetValue(ThumbBorderBrushPointerOverProperty); }
            set { SetValue(ThumbBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ThumbBorderBrushPointerOverProperty =
            DependencyProperty.Register("ThumbBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBorderBrushPressed
        {
            get { return GetValue(ThumbBorderBrushPressedProperty); }
            set { SetValue(ThumbBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty ThumbBorderBrushPressedProperty =
            DependencyProperty.Register("ThumbBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackground
        {
            get { return GetValue(ToggleButtonBackgroundProperty); }
            set { SetValue(ToggleButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundProperty =
            DependencyProperty.Register("ToggleButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundPointerOver
        {
            get { return GetValue(ToggleButtonBackgroundPointerOverProperty); }
            set { SetValue(ToggleButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("ToggleButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundPressed
        {
            get { return GetValue(ToggleButtonBackgroundPressedProperty); }
            set { SetValue(ToggleButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundPressedProperty =
            DependencyProperty.Register("ToggleButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundDisabled
        {
            get { return GetValue(ToggleButtonBackgroundDisabledProperty); }
            set { SetValue(ToggleButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundDisabledProperty =
            DependencyProperty.Register("ToggleButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundChecked
        {
            get { return GetValue(ToggleButtonBackgroundCheckedProperty); }
            set { SetValue(ToggleButtonBackgroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundCheckedProperty =
            DependencyProperty.Register("ToggleButtonBackgroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundCheckedPointerOver
        {
            get { return GetValue(ToggleButtonBackgroundCheckedPointerOverProperty); }
            set { SetValue(ToggleButtonBackgroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundCheckedPointerOverProperty =
            DependencyProperty.Register("ToggleButtonBackgroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundCheckedPressed
        {
            get { return GetValue(ToggleButtonBackgroundCheckedPressedProperty); }
            set { SetValue(ToggleButtonBackgroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundCheckedPressedProperty =
            DependencyProperty.Register("ToggleButtonBackgroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundCheckedDisabled
        {
            get { return GetValue(ToggleButtonBackgroundCheckedDisabledProperty); }
            set { SetValue(ToggleButtonBackgroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundCheckedDisabledProperty =
            DependencyProperty.Register("ToggleButtonBackgroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundIndeterminate
        {
            get { return GetValue(ToggleButtonBackgroundIndeterminateProperty); }
            set { SetValue(ToggleButtonBackgroundIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundIndeterminateProperty =
            DependencyProperty.Register("ToggleButtonBackgroundIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundIndeterminatePointerOver
        {
            get { return GetValue(ToggleButtonBackgroundIndeterminatePointerOverProperty); }
            set { SetValue(ToggleButtonBackgroundIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundIndeterminatePointerOverProperty =
            DependencyProperty.Register("ToggleButtonBackgroundIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundIndeterminatePressed
        {
            get { return GetValue(ToggleButtonBackgroundIndeterminatePressedProperty); }
            set { SetValue(ToggleButtonBackgroundIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundIndeterminatePressedProperty =
            DependencyProperty.Register("ToggleButtonBackgroundIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundIndeterminateDisabled
        {
            get { return GetValue(ToggleButtonBackgroundIndeterminateDisabledProperty); }
            set { SetValue(ToggleButtonBackgroundIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundIndeterminateDisabledProperty =
            DependencyProperty.Register("ToggleButtonBackgroundIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForeground
        {
            get { return GetValue(ToggleButtonForegroundProperty); }
            set { SetValue(ToggleButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundProperty =
            DependencyProperty.Register("ToggleButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundPointerOver
        {
            get { return GetValue(ToggleButtonForegroundPointerOverProperty); }
            set { SetValue(ToggleButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundPointerOverProperty =
            DependencyProperty.Register("ToggleButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundPressed
        {
            get { return GetValue(ToggleButtonForegroundPressedProperty); }
            set { SetValue(ToggleButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundPressedProperty =
            DependencyProperty.Register("ToggleButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundDisabled
        {
            get { return GetValue(ToggleButtonForegroundDisabledProperty); }
            set { SetValue(ToggleButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundDisabledProperty =
            DependencyProperty.Register("ToggleButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundChecked
        {
            get { return GetValue(ToggleButtonForegroundCheckedProperty); }
            set { SetValue(ToggleButtonForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundCheckedProperty =
            DependencyProperty.Register("ToggleButtonForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundCheckedPointerOver
        {
            get { return GetValue(ToggleButtonForegroundCheckedPointerOverProperty); }
            set { SetValue(ToggleButtonForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("ToggleButtonForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundCheckedPressed
        {
            get { return GetValue(ToggleButtonForegroundCheckedPressedProperty); }
            set { SetValue(ToggleButtonForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundCheckedPressedProperty =
            DependencyProperty.Register("ToggleButtonForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundCheckedDisabled
        {
            get { return GetValue(ToggleButtonForegroundCheckedDisabledProperty); }
            set { SetValue(ToggleButtonForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundCheckedDisabledProperty =
            DependencyProperty.Register("ToggleButtonForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundIndeterminate
        {
            get { return GetValue(ToggleButtonForegroundIndeterminateProperty); }
            set { SetValue(ToggleButtonForegroundIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundIndeterminateProperty =
            DependencyProperty.Register("ToggleButtonForegroundIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundIndeterminatePointerOver
        {
            get { return GetValue(ToggleButtonForegroundIndeterminatePointerOverProperty); }
            set { SetValue(ToggleButtonForegroundIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundIndeterminatePointerOverProperty =
            DependencyProperty.Register("ToggleButtonForegroundIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundIndeterminatePressed
        {
            get { return GetValue(ToggleButtonForegroundIndeterminatePressedProperty); }
            set { SetValue(ToggleButtonForegroundIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundIndeterminatePressedProperty =
            DependencyProperty.Register("ToggleButtonForegroundIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundIndeterminateDisabled
        {
            get { return GetValue(ToggleButtonForegroundIndeterminateDisabledProperty); }
            set { SetValue(ToggleButtonForegroundIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundIndeterminateDisabledProperty =
            DependencyProperty.Register("ToggleButtonForegroundIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrush
        {
            get { return GetValue(ToggleButtonBorderBrushProperty); }
            set { SetValue(ToggleButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushProperty =
            DependencyProperty.Register("ToggleButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushPointerOver
        {
            get { return GetValue(ToggleButtonBorderBrushPointerOverProperty); }
            set { SetValue(ToggleButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushPressed
        {
            get { return GetValue(ToggleButtonBorderBrushPressedProperty); }
            set { SetValue(ToggleButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushPressedProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushDisabled
        {
            get { return GetValue(ToggleButtonBorderBrushDisabledProperty); }
            set { SetValue(ToggleButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushChecked
        {
            get { return GetValue(ToggleButtonBorderBrushCheckedProperty); }
            set { SetValue(ToggleButtonBorderBrushCheckedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushCheckedProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushCheckedPointerOver
        {
            get { return GetValue(ToggleButtonBorderBrushCheckedPointerOverProperty); }
            set { SetValue(ToggleButtonBorderBrushCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushCheckedPointerOverProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushCheckedDisabled
        {
            get { return GetValue(ToggleButtonBorderBrushCheckedDisabledProperty); }
            set { SetValue(ToggleButtonBorderBrushCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushCheckedDisabledProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushIndeterminate
        {
            get { return GetValue(ToggleButtonBorderBrushIndeterminateProperty); }
            set { SetValue(ToggleButtonBorderBrushIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushIndeterminateProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushIndeterminatePointerOver
        {
            get { return GetValue(ToggleButtonBorderBrushIndeterminatePointerOverProperty); }
            set { SetValue(ToggleButtonBorderBrushIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushIndeterminatePointerOverProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushIndeterminatePressed
        {
            get { return GetValue(ToggleButtonBorderBrushIndeterminatePressedProperty); }
            set { SetValue(ToggleButtonBorderBrushIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushIndeterminatePressedProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushIndeterminateDisabled
        {
            get { return GetValue(ToggleButtonBorderBrushIndeterminateDisabledProperty); }
            set { SetValue(ToggleButtonBorderBrushIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushIndeterminateDisabledProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonBackgroundPointerOver
        {
            get { return GetValue(ScrollBarButtonBackgroundPointerOverProperty); }
            set { SetValue(ScrollBarButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("ScrollBarButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonBackgroundPressed
        {
            get { return GetValue(ScrollBarButtonBackgroundPressedProperty); }
            set { SetValue(ScrollBarButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonBackgroundPressedProperty =
            DependencyProperty.Register("ScrollBarButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonArrowForeground
        {
            get { return GetValue(ScrollBarButtonArrowForegroundProperty); }
            set { SetValue(ScrollBarButtonArrowForegroundProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonArrowForegroundProperty =
            DependencyProperty.Register("ScrollBarButtonArrowForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonArrowForegroundPointerOver
        {
            get { return GetValue(ScrollBarButtonArrowForegroundPointerOverProperty); }
            set { SetValue(ScrollBarButtonArrowForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonArrowForegroundPointerOverProperty =
            DependencyProperty.Register("ScrollBarButtonArrowForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonArrowForegroundPressed
        {
            get { return GetValue(ScrollBarButtonArrowForegroundPressedProperty); }
            set { SetValue(ScrollBarButtonArrowForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonArrowForegroundPressedProperty =
            DependencyProperty.Register("ScrollBarButtonArrowForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonArrowForegroundDisabled
        {
            get { return GetValue(ScrollBarButtonArrowForegroundDisabledProperty); }
            set { SetValue(ScrollBarButtonArrowForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonArrowForegroundDisabledProperty =
            DependencyProperty.Register("ScrollBarButtonArrowForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarThumbFill
        {
            get { return GetValue(ScrollBarThumbFillProperty); }
            set { SetValue(ScrollBarThumbFillProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarThumbFillProperty =
            DependencyProperty.Register("ScrollBarThumbFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarThumbFillPointerOver
        {
            get { return GetValue(ScrollBarThumbFillPointerOverProperty); }
            set { SetValue(ScrollBarThumbFillPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarThumbFillPointerOverProperty =
            DependencyProperty.Register("ScrollBarThumbFillPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarThumbFillPressed
        {
            get { return GetValue(ScrollBarThumbFillPressedProperty); }
            set { SetValue(ScrollBarThumbFillPressedProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarThumbFillPressedProperty =
            DependencyProperty.Register("ScrollBarThumbFillPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarThumbFillDisabled
        {
            get { return GetValue(ScrollBarThumbFillDisabledProperty); }
            set { SetValue(ScrollBarThumbFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarThumbFillDisabledProperty =
            DependencyProperty.Register("ScrollBarThumbFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarTrackFillDisabled
        {
            get { return GetValue(ScrollBarTrackFillDisabledProperty); }
            set { SetValue(ScrollBarTrackFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarTrackFillDisabledProperty =
            DependencyProperty.Register("ScrollBarTrackFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarTrackStroke
        {
            get { return GetValue(ScrollBarTrackStrokeProperty); }
            set { SetValue(ScrollBarTrackStrokeProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarTrackStrokeProperty =
            DependencyProperty.Register("ScrollBarTrackStroke", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarTrackStrokePointerOver
        {
            get { return GetValue(ScrollBarTrackStrokePointerOverProperty); }
            set { SetValue(ScrollBarTrackStrokePointerOverProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarTrackStrokePointerOverProperty =
            DependencyProperty.Register("ScrollBarTrackStrokePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarTrackStrokeDisabled
        {
            get { return GetValue(ScrollBarTrackStrokeDisabledProperty); }
            set { SetValue(ScrollBarTrackStrokeDisabledProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarTrackStrokeDisabledProperty =
            DependencyProperty.Register("ScrollBarTrackStrokeDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarPanningThumbBackgroundDisabled
        {
            get { return GetValue(ScrollBarPanningThumbBackgroundDisabledProperty); }
            set { SetValue(ScrollBarPanningThumbBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarPanningThumbBackgroundDisabledProperty =
            DependencyProperty.Register("ScrollBarPanningThumbBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarThumbBackgroundColor
        {
            get { return GetValue(ScrollBarThumbBackgroundColorProperty); }
            set { SetValue(ScrollBarThumbBackgroundColorProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarThumbBackgroundColorProperty =
            DependencyProperty.Register("ScrollBarThumbBackgroundColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarPanningThumbBackgroundColor
        {
            get { return GetValue(ScrollBarPanningThumbBackgroundColorProperty); }
            set { SetValue(ScrollBarPanningThumbBackgroundColorProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarPanningThumbBackgroundColorProperty =
            DependencyProperty.Register("ScrollBarPanningThumbBackgroundColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewHeaderItemDividerStroke
        {
            get { return GetValue(ListViewHeaderItemDividerStrokeProperty); }
            set { SetValue(ListViewHeaderItemDividerStrokeProperty, value); }
        }

        public static readonly DependencyProperty ListViewHeaderItemDividerStrokeProperty =
            DependencyProperty.Register("ListViewHeaderItemDividerStroke", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForeground
        {
            get { return GetValue(ComboBoxItemForegroundProperty); }
            set { SetValue(ComboBoxItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundProperty =
            DependencyProperty.Register("ComboBoxItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundPressed
        {
            get { return GetValue(ComboBoxItemForegroundPressedProperty); }
            set { SetValue(ComboBoxItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundPressedProperty =
            DependencyProperty.Register("ComboBoxItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundPointerOver
        {
            get { return GetValue(ComboBoxItemForegroundPointerOverProperty); }
            set { SetValue(ComboBoxItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundPointerOverProperty =
            DependencyProperty.Register("ComboBoxItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundDisabled
        {
            get { return GetValue(ComboBoxItemForegroundDisabledProperty); }
            set { SetValue(ComboBoxItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundDisabledProperty =
            DependencyProperty.Register("ComboBoxItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundSelected
        {
            get { return GetValue(ComboBoxItemForegroundSelectedProperty); }
            set { SetValue(ComboBoxItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundSelectedProperty =
            DependencyProperty.Register("ComboBoxItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundSelectedUnfocused
        {
            get { return GetValue(ComboBoxItemForegroundSelectedUnfocusedProperty); }
            set { SetValue(ComboBoxItemForegroundSelectedUnfocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundSelectedUnfocusedProperty =
            DependencyProperty.Register("ComboBoxItemForegroundSelectedUnfocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundSelectedPressed
        {
            get { return GetValue(ComboBoxItemForegroundSelectedPressedProperty); }
            set { SetValue(ComboBoxItemForegroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundSelectedPressedProperty =
            DependencyProperty.Register("ComboBoxItemForegroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundSelectedPointerOver
        {
            get { return GetValue(ComboBoxItemForegroundSelectedPointerOverProperty); }
            set { SetValue(ComboBoxItemForegroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundSelectedPointerOverProperty =
            DependencyProperty.Register("ComboBoxItemForegroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundSelectedDisabled
        {
            get { return GetValue(ComboBoxItemForegroundSelectedDisabledProperty); }
            set { SetValue(ComboBoxItemForegroundSelectedDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundSelectedDisabledProperty =
            DependencyProperty.Register("ComboBoxItemForegroundSelectedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundPressed
        {
            get { return GetValue(ComboBoxItemBackgroundPressedProperty); }
            set { SetValue(ComboBoxItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundPressedProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundPointerOver
        {
            get { return GetValue(ComboBoxItemBackgroundPointerOverProperty); }
            set { SetValue(ComboBoxItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundPointerOverProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundSelected
        {
            get { return GetValue(ComboBoxItemBackgroundSelectedProperty); }
            set { SetValue(ComboBoxItemBackgroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundSelectedProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundSelectedUnfocused
        {
            get { return GetValue(ComboBoxItemBackgroundSelectedUnfocusedProperty); }
            set { SetValue(ComboBoxItemBackgroundSelectedUnfocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundSelectedUnfocusedProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundSelectedUnfocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundSelectedPressed
        {
            get { return GetValue(ComboBoxItemBackgroundSelectedPressedProperty); }
            set { SetValue(ComboBoxItemBackgroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundSelectedPressedProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundSelectedPointerOver
        {
            get { return GetValue(ComboBoxItemBackgroundSelectedPointerOverProperty); }
            set { SetValue(ComboBoxItemBackgroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundSelectedPointerOverProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackground
        {
            get { return GetValue(ComboBoxBackgroundProperty); }
            set { SetValue(ComboBoxBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundProperty =
            DependencyProperty.Register("ComboBoxBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundPointerOver
        {
            get { return GetValue(ComboBoxBackgroundPointerOverProperty); }
            set { SetValue(ComboBoxBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundPointerOverProperty =
            DependencyProperty.Register("ComboBoxBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundPressed
        {
            get { return GetValue(ComboBoxBackgroundPressedProperty); }
            set { SetValue(ComboBoxBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundPressedProperty =
            DependencyProperty.Register("ComboBoxBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundDisabled
        {
            get { return GetValue(ComboBoxBackgroundDisabledProperty); }
            set { SetValue(ComboBoxBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundDisabledProperty =
            DependencyProperty.Register("ComboBoxBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundUnfocused
        {
            get { return GetValue(ComboBoxBackgroundUnfocusedProperty); }
            set { SetValue(ComboBoxBackgroundUnfocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundUnfocusedProperty =
            DependencyProperty.Register("ComboBoxBackgroundUnfocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundBorderBrushFocused
        {
            get { return GetValue(ComboBoxBackgroundBorderBrushFocusedProperty); }
            set { SetValue(ComboBoxBackgroundBorderBrushFocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundBorderBrushFocusedProperty =
            DependencyProperty.Register("ComboBoxBackgroundBorderBrushFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundBorderBrushUnfocused
        {
            get { return GetValue(ComboBoxBackgroundBorderBrushUnfocusedProperty); }
            set { SetValue(ComboBoxBackgroundBorderBrushUnfocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundBorderBrushUnfocusedProperty =
            DependencyProperty.Register("ComboBoxBackgroundBorderBrushUnfocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxForeground
        {
            get { return GetValue(ComboBoxForegroundProperty); }
            set { SetValue(ComboBoxForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxForegroundProperty =
            DependencyProperty.Register("ComboBoxForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxForegroundDisabled
        {
            get { return GetValue(ComboBoxForegroundDisabledProperty); }
            set { SetValue(ComboBoxForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxForegroundDisabledProperty =
            DependencyProperty.Register("ComboBoxForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxForegroundFocused
        {
            get { return GetValue(ComboBoxForegroundFocusedProperty); }
            set { SetValue(ComboBoxForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxForegroundFocusedProperty =
            DependencyProperty.Register("ComboBoxForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxForegroundFocusedPressed
        {
            get { return GetValue(ComboBoxForegroundFocusedPressedProperty); }
            set { SetValue(ComboBoxForegroundFocusedPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxForegroundFocusedPressedProperty =
            DependencyProperty.Register("ComboBoxForegroundFocusedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxPlaceHolderForeground
        {
            get { return GetValue(ComboBoxPlaceHolderForegroundProperty); }
            set { SetValue(ComboBoxPlaceHolderForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxPlaceHolderForegroundProperty =
            DependencyProperty.Register("ComboBoxPlaceHolderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxPlaceHolderForegroundFocusedPressed
        {
            get { return GetValue(ComboBoxPlaceHolderForegroundFocusedPressedProperty); }
            set { SetValue(ComboBoxPlaceHolderForegroundFocusedPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxPlaceHolderForegroundFocusedPressedProperty =
            DependencyProperty.Register("ComboBoxPlaceHolderForegroundFocusedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBorderBrush
        {
            get { return GetValue(ComboBoxBorderBrushProperty); }
            set { SetValue(ComboBoxBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBorderBrushProperty =
            DependencyProperty.Register("ComboBoxBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBorderBrushPointerOver
        {
            get { return GetValue(ComboBoxBorderBrushPointerOverProperty); }
            set { SetValue(ComboBoxBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBorderBrushPointerOverProperty =
            DependencyProperty.Register("ComboBoxBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBorderBrushPressed
        {
            get { return GetValue(ComboBoxBorderBrushPressedProperty); }
            set { SetValue(ComboBoxBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBorderBrushPressedProperty =
            DependencyProperty.Register("ComboBoxBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBorderBrushDisabled
        {
            get { return GetValue(ComboBoxBorderBrushDisabledProperty); }
            set { SetValue(ComboBoxBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBorderBrushDisabledProperty =
            DependencyProperty.Register("ComboBoxBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownBackgroundPointerOver
        {
            get { return GetValue(ComboBoxDropDownBackgroundPointerOverProperty); }
            set { SetValue(ComboBoxDropDownBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownBackgroundPointerOverProperty =
            DependencyProperty.Register("ComboBoxDropDownBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownBackgroundPointerPressed
        {
            get { return GetValue(ComboBoxDropDownBackgroundPointerPressedProperty); }
            set { SetValue(ComboBoxDropDownBackgroundPointerPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownBackgroundPointerPressedProperty =
            DependencyProperty.Register("ComboBoxDropDownBackgroundPointerPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxFocusedDropDownBackgroundPointerOver
        {
            get { return GetValue(ComboBoxFocusedDropDownBackgroundPointerOverProperty); }
            set { SetValue(ComboBoxFocusedDropDownBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxFocusedDropDownBackgroundPointerOverProperty =
            DependencyProperty.Register("ComboBoxFocusedDropDownBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxFocusedDropDownBackgroundPointerPressed
        {
            get { return GetValue(ComboBoxFocusedDropDownBackgroundPointerPressedProperty); }
            set { SetValue(ComboBoxFocusedDropDownBackgroundPointerPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxFocusedDropDownBackgroundPointerPressedProperty =
            DependencyProperty.Register("ComboBoxFocusedDropDownBackgroundPointerPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownGlyphForeground
        {
            get { return GetValue(ComboBoxDropDownGlyphForegroundProperty); }
            set { SetValue(ComboBoxDropDownGlyphForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownGlyphForegroundProperty =
            DependencyProperty.Register("ComboBoxDropDownGlyphForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxEditableDropDownGlyphForeground
        {
            get { return GetValue(ComboBoxEditableDropDownGlyphForegroundProperty); }
            set { SetValue(ComboBoxEditableDropDownGlyphForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxEditableDropDownGlyphForegroundProperty =
            DependencyProperty.Register("ComboBoxEditableDropDownGlyphForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownGlyphForegroundDisabled
        {
            get { return GetValue(ComboBoxDropDownGlyphForegroundDisabledProperty); }
            set { SetValue(ComboBoxDropDownGlyphForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownGlyphForegroundDisabledProperty =
            DependencyProperty.Register("ComboBoxDropDownGlyphForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownGlyphForegroundFocused
        {
            get { return GetValue(ComboBoxDropDownGlyphForegroundFocusedProperty); }
            set { SetValue(ComboBoxDropDownGlyphForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownGlyphForegroundFocusedProperty =
            DependencyProperty.Register("ComboBoxDropDownGlyphForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownGlyphForegroundFocusedPressed
        {
            get { return GetValue(ComboBoxDropDownGlyphForegroundFocusedPressedProperty); }
            set { SetValue(ComboBoxDropDownGlyphForegroundFocusedPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownGlyphForegroundFocusedPressedProperty =
            DependencyProperty.Register("ComboBoxDropDownGlyphForegroundFocusedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownBackground
        {
            get { return GetValue(ComboBoxDropDownBackgroundProperty); }
            set { SetValue(ComboBoxDropDownBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownBackgroundProperty =
            DependencyProperty.Register("ComboBoxDropDownBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownForeground
        {
            get { return GetValue(ComboBoxDropDownForegroundProperty); }
            set { SetValue(ComboBoxDropDownForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownForegroundProperty =
            DependencyProperty.Register("ComboBoxDropDownForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownBorderBrush
        {
            get { return GetValue(ComboBoxDropDownBorderBrushProperty); }
            set { SetValue(ComboBoxDropDownBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownBorderBrushProperty =
            DependencyProperty.Register("ComboBoxDropDownBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarSeparatorForeground
        {
            get { return GetValue(AppBarSeparatorForegroundProperty); }
            set { SetValue(AppBarSeparatorForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarSeparatorForegroundProperty =
            DependencyProperty.Register("AppBarSeparatorForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonBackgroundPointerOver
        {
            get { return GetValue(AppBarEllipsisButtonBackgroundPointerOverProperty); }
            set { SetValue(AppBarEllipsisButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("AppBarEllipsisButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonBackgroundPressed
        {
            get { return GetValue(AppBarEllipsisButtonBackgroundPressedProperty); }
            set { SetValue(AppBarEllipsisButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonBackgroundPressedProperty =
            DependencyProperty.Register("AppBarEllipsisButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonForeground
        {
            get { return GetValue(AppBarEllipsisButtonForegroundProperty); }
            set { SetValue(AppBarEllipsisButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonForegroundProperty =
            DependencyProperty.Register("AppBarEllipsisButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonForegroundPointerOver
        {
            get { return GetValue(AppBarEllipsisButtonForegroundPointerOverProperty); }
            set { SetValue(AppBarEllipsisButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarEllipsisButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonForegroundPressed
        {
            get { return GetValue(AppBarEllipsisButtonForegroundPressedProperty); }
            set { SetValue(AppBarEllipsisButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonForegroundPressedProperty =
            DependencyProperty.Register("AppBarEllipsisButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonForegroundDisabled
        {
            get { return GetValue(AppBarEllipsisButtonForegroundDisabledProperty); }
            set { SetValue(AppBarEllipsisButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonForegroundDisabledProperty =
            DependencyProperty.Register("AppBarEllipsisButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarBackground
        {
            get { return GetValue(AppBarBackgroundProperty); }
            set { SetValue(AppBarBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarBackgroundProperty =
            DependencyProperty.Register("AppBarBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarForeground
        {
            get { return GetValue(AppBarForegroundProperty); }
            set { SetValue(AppBarForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarForegroundProperty =
            DependencyProperty.Register("AppBarForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarHighContrastBorder
        {
            get { return GetValue(AppBarHighContrastBorderProperty); }
            set { SetValue(AppBarHighContrastBorderProperty, value); }
        }

        public static readonly DependencyProperty AppBarHighContrastBorderProperty =
            DependencyProperty.Register("AppBarHighContrastBorder", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ContentDialogForeground
        {
            get { return GetValue(ContentDialogForegroundProperty); }
            set { SetValue(ContentDialogForegroundProperty, value); }
        }

        public static readonly DependencyProperty ContentDialogForegroundProperty =
            DependencyProperty.Register("ContentDialogForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ContentDialogBackground
        {
            get { return GetValue(ContentDialogBackgroundProperty); }
            set { SetValue(ContentDialogBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ContentDialogBackgroundProperty =
            DependencyProperty.Register("ContentDialogBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ContentDialogBorderBrush
        {
            get { return GetValue(ContentDialogBorderBrushProperty); }
            set { SetValue(ContentDialogBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ContentDialogBorderBrushProperty =
            DependencyProperty.Register("ContentDialogBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBackground
        {
            get { return GetValue(AccentButtonBackgroundProperty); }
            set { SetValue(AccentButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBackgroundProperty =
            DependencyProperty.Register("AccentButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBackgroundPointerOver
        {
            get { return GetValue(AccentButtonBackgroundPointerOverProperty); }
            set { SetValue(AccentButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("AccentButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBackgroundPressed
        {
            get { return GetValue(AccentButtonBackgroundPressedProperty); }
            set { SetValue(AccentButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBackgroundPressedProperty =
            DependencyProperty.Register("AccentButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBackgroundDisabled
        {
            get { return GetValue(AccentButtonBackgroundDisabledProperty); }
            set { SetValue(AccentButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBackgroundDisabledProperty =
            DependencyProperty.Register("AccentButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonForeground
        {
            get { return GetValue(AccentButtonForegroundProperty); }
            set { SetValue(AccentButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonForegroundProperty =
            DependencyProperty.Register("AccentButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonForegroundPointerOver
        {
            get { return GetValue(AccentButtonForegroundPointerOverProperty); }
            set { SetValue(AccentButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonForegroundPointerOverProperty =
            DependencyProperty.Register("AccentButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonForegroundPressed
        {
            get { return GetValue(AccentButtonForegroundPressedProperty); }
            set { SetValue(AccentButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonForegroundPressedProperty =
            DependencyProperty.Register("AccentButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonForegroundDisabled
        {
            get { return GetValue(AccentButtonForegroundDisabledProperty); }
            set { SetValue(AccentButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonForegroundDisabledProperty =
            DependencyProperty.Register("AccentButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBorderBrush
        {
            get { return GetValue(AccentButtonBorderBrushProperty); }
            set { SetValue(AccentButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBorderBrushProperty =
            DependencyProperty.Register("AccentButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBorderBrushPointerOver
        {
            get { return GetValue(AccentButtonBorderBrushPointerOverProperty); }
            set { SetValue(AccentButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("AccentButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBorderBrushPressed
        {
            get { return GetValue(AccentButtonBorderBrushPressedProperty); }
            set { SetValue(AccentButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBorderBrushPressedProperty =
            DependencyProperty.Register("AccentButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBorderBrushDisabled
        {
            get { return GetValue(AccentButtonBorderBrushDisabledProperty); }
            set { SetValue(AccentButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("AccentButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToolTipForeground
        {
            get { return GetValue(ToolTipForegroundProperty); }
            set { SetValue(ToolTipForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToolTipForegroundProperty =
            DependencyProperty.Register("ToolTipForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToolTipBackground
        {
            get { return GetValue(ToolTipBackgroundProperty); }
            set { SetValue(ToolTipBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ToolTipBackgroundProperty =
            DependencyProperty.Register("ToolTipBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToolTipBorderBrush
        {
            get { return GetValue(ToolTipBorderBrushProperty); }
            set { SetValue(ToolTipBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ToolTipBorderBrushProperty =
            DependencyProperty.Register("ToolTipBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerForeground
        {
            get { return GetValue(CalendarDatePickerForegroundProperty); }
            set { SetValue(CalendarDatePickerForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerForegroundProperty =
            DependencyProperty.Register("CalendarDatePickerForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerForegroundDisabled
        {
            get { return GetValue(CalendarDatePickerForegroundDisabledProperty); }
            set { SetValue(CalendarDatePickerForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerForegroundDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerCalendarGlyphForeground
        {
            get { return GetValue(CalendarDatePickerCalendarGlyphForegroundProperty); }
            set { SetValue(CalendarDatePickerCalendarGlyphForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerCalendarGlyphForegroundProperty =
            DependencyProperty.Register("CalendarDatePickerCalendarGlyphForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerCalendarGlyphForegroundDisabled
        {
            get { return GetValue(CalendarDatePickerCalendarGlyphForegroundDisabledProperty); }
            set { SetValue(CalendarDatePickerCalendarGlyphForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerCalendarGlyphForegroundDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerCalendarGlyphForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerTextForeground
        {
            get { return GetValue(CalendarDatePickerTextForegroundProperty); }
            set { SetValue(CalendarDatePickerTextForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerTextForegroundProperty =
            DependencyProperty.Register("CalendarDatePickerTextForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerTextForegroundDisabled
        {
            get { return GetValue(CalendarDatePickerTextForegroundDisabledProperty); }
            set { SetValue(CalendarDatePickerTextForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerTextForegroundDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerTextForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerTextForegroundSelected
        {
            get { return GetValue(CalendarDatePickerTextForegroundSelectedProperty); }
            set { SetValue(CalendarDatePickerTextForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerTextForegroundSelectedProperty =
            DependencyProperty.Register("CalendarDatePickerTextForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerHeaderForegroundDisabled
        {
            get { return GetValue(CalendarDatePickerHeaderForegroundDisabledProperty); }
            set { SetValue(CalendarDatePickerHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerHeaderForegroundDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBackground
        {
            get { return GetValue(CalendarDatePickerBackgroundProperty); }
            set { SetValue(CalendarDatePickerBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBackgroundProperty =
            DependencyProperty.Register("CalendarDatePickerBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBackgroundPointerOver
        {
            get { return GetValue(CalendarDatePickerBackgroundPointerOverProperty); }
            set { SetValue(CalendarDatePickerBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBackgroundPointerOverProperty =
            DependencyProperty.Register("CalendarDatePickerBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBackgroundPressed
        {
            get { return GetValue(CalendarDatePickerBackgroundPressedProperty); }
            set { SetValue(CalendarDatePickerBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBackgroundPressedProperty =
            DependencyProperty.Register("CalendarDatePickerBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBackgroundDisabled
        {
            get { return GetValue(CalendarDatePickerBackgroundDisabledProperty); }
            set { SetValue(CalendarDatePickerBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBackgroundDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBackgroundFocused
        {
            get { return GetValue(CalendarDatePickerBackgroundFocusedProperty); }
            set { SetValue(CalendarDatePickerBackgroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBackgroundFocusedProperty =
            DependencyProperty.Register("CalendarDatePickerBackgroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBorderBrush
        {
            get { return GetValue(CalendarDatePickerBorderBrushProperty); }
            set { SetValue(CalendarDatePickerBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBorderBrushProperty =
            DependencyProperty.Register("CalendarDatePickerBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBorderBrushPointerOver
        {
            get { return GetValue(CalendarDatePickerBorderBrushPointerOverProperty); }
            set { SetValue(CalendarDatePickerBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBorderBrushPointerOverProperty =
            DependencyProperty.Register("CalendarDatePickerBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBorderBrushPressed
        {
            get { return GetValue(CalendarDatePickerBorderBrushPressedProperty); }
            set { SetValue(CalendarDatePickerBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBorderBrushPressedProperty =
            DependencyProperty.Register("CalendarDatePickerBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBorderBrushDisabled
        {
            get { return GetValue(CalendarDatePickerBorderBrushDisabledProperty); }
            set { SetValue(CalendarDatePickerBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBorderBrushDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewFocusBorderBrush
        {
            get { return GetValue(CalendarViewFocusBorderBrushProperty); }
            set { SetValue(CalendarViewFocusBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewFocusBorderBrushProperty =
            DependencyProperty.Register("CalendarViewFocusBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewSelectedHoverBorderBrush
        {
            get { return GetValue(CalendarViewSelectedHoverBorderBrushProperty); }
            set { SetValue(CalendarViewSelectedHoverBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewSelectedHoverBorderBrushProperty =
            DependencyProperty.Register("CalendarViewSelectedHoverBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewSelectedPressedBorderBrush
        {
            get { return GetValue(CalendarViewSelectedPressedBorderBrushProperty); }
            set { SetValue(CalendarViewSelectedPressedBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewSelectedPressedBorderBrushProperty =
            DependencyProperty.Register("CalendarViewSelectedPressedBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewSelectedBorderBrush
        {
            get { return GetValue(CalendarViewSelectedBorderBrushProperty); }
            set { SetValue(CalendarViewSelectedBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewSelectedBorderBrushProperty =
            DependencyProperty.Register("CalendarViewSelectedBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewHoverBorderBrush
        {
            get { return GetValue(CalendarViewHoverBorderBrushProperty); }
            set { SetValue(CalendarViewHoverBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewHoverBorderBrushProperty =
            DependencyProperty.Register("CalendarViewHoverBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewPressedBorderBrush
        {
            get { return GetValue(CalendarViewPressedBorderBrushProperty); }
            set { SetValue(CalendarViewPressedBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewPressedBorderBrushProperty =
            DependencyProperty.Register("CalendarViewPressedBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewTodayForeground
        {
            get { return GetValue(CalendarViewTodayForegroundProperty); }
            set { SetValue(CalendarViewTodayForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewTodayForegroundProperty =
            DependencyProperty.Register("CalendarViewTodayForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewBlackoutForeground
        {
            get { return GetValue(CalendarViewBlackoutForegroundProperty); }
            set { SetValue(CalendarViewBlackoutForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewBlackoutForegroundProperty =
            DependencyProperty.Register("CalendarViewBlackoutForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewSelectedForeground
        {
            get { return GetValue(CalendarViewSelectedForegroundProperty); }
            set { SetValue(CalendarViewSelectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewSelectedForegroundProperty =
            DependencyProperty.Register("CalendarViewSelectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewPressedForeground
        {
            get { return GetValue(CalendarViewPressedForegroundProperty); }
            set { SetValue(CalendarViewPressedForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewPressedForegroundProperty =
            DependencyProperty.Register("CalendarViewPressedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewOutOfScopeForeground
        {
            get { return GetValue(CalendarViewOutOfScopeForegroundProperty); }
            set { SetValue(CalendarViewOutOfScopeForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewOutOfScopeForegroundProperty =
            DependencyProperty.Register("CalendarViewOutOfScopeForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewCalendarItemForeground
        {
            get { return GetValue(CalendarViewCalendarItemForegroundProperty); }
            set { SetValue(CalendarViewCalendarItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewCalendarItemForegroundProperty =
            DependencyProperty.Register("CalendarViewCalendarItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewOutOfScopeBackground
        {
            get { return GetValue(CalendarViewOutOfScopeBackgroundProperty); }
            set { SetValue(CalendarViewOutOfScopeBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewOutOfScopeBackgroundProperty =
            DependencyProperty.Register("CalendarViewOutOfScopeBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewCalendarItemBackground
        {
            get { return GetValue(CalendarViewCalendarItemBackgroundProperty); }
            set { SetValue(CalendarViewCalendarItemBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewCalendarItemBackgroundProperty =
            DependencyProperty.Register("CalendarViewCalendarItemBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewForeground
        {
            get { return GetValue(CalendarViewForegroundProperty); }
            set { SetValue(CalendarViewForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewForegroundProperty =
            DependencyProperty.Register("CalendarViewForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewBackground
        {
            get { return GetValue(CalendarViewBackgroundProperty); }
            set { SetValue(CalendarViewBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewBackgroundProperty =
            DependencyProperty.Register("CalendarViewBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewBorderBrush
        {
            get { return GetValue(CalendarViewBorderBrushProperty); }
            set { SetValue(CalendarViewBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewBorderBrushProperty =
            DependencyProperty.Register("CalendarViewBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewWeekDayForegroundDisabled
        {
            get { return GetValue(CalendarViewWeekDayForegroundDisabledProperty); }
            set { SetValue(CalendarViewWeekDayForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewWeekDayForegroundDisabledProperty =
            DependencyProperty.Register("CalendarViewWeekDayForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewNavigationButtonForegroundPointerOver
        {
            get { return GetValue(CalendarViewNavigationButtonForegroundPointerOverProperty); }
            set { SetValue(CalendarViewNavigationButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewNavigationButtonForegroundPointerOverProperty =
            DependencyProperty.Register("CalendarViewNavigationButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewNavigationButtonForegroundPressed
        {
            get { return GetValue(CalendarViewNavigationButtonForegroundPressedProperty); }
            set { SetValue(CalendarViewNavigationButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewNavigationButtonForegroundPressedProperty =
            DependencyProperty.Register("CalendarViewNavigationButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewNavigationButtonForegroundDisabled
        {
            get { return GetValue(CalendarViewNavigationButtonForegroundDisabledProperty); }
            set { SetValue(CalendarViewNavigationButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewNavigationButtonForegroundDisabledProperty =
            DependencyProperty.Register("CalendarViewNavigationButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewNavigationButtonBorderBrushPointerOver
        {
            get { return GetValue(CalendarViewNavigationButtonBorderBrushPointerOverProperty); }
            set { SetValue(CalendarViewNavigationButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewNavigationButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("CalendarViewNavigationButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubForeground
        {
            get { return GetValue(HubForegroundProperty); }
            set { SetValue(HubForegroundProperty, value); }
        }

        public static readonly DependencyProperty HubForegroundProperty =
            DependencyProperty.Register("HubForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubSectionHeaderButtonForeground
        {
            get { return GetValue(HubSectionHeaderButtonForegroundProperty); }
            set { SetValue(HubSectionHeaderButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty HubSectionHeaderButtonForegroundProperty =
            DependencyProperty.Register("HubSectionHeaderButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubSectionHeaderButtonForegroundPointerOver
        {
            get { return GetValue(HubSectionHeaderButtonForegroundPointerOverProperty); }
            set { SetValue(HubSectionHeaderButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty HubSectionHeaderButtonForegroundPointerOverProperty =
            DependencyProperty.Register("HubSectionHeaderButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubSectionHeaderButtonForegroundPressed
        {
            get { return GetValue(HubSectionHeaderButtonForegroundPressedProperty); }
            set { SetValue(HubSectionHeaderButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty HubSectionHeaderButtonForegroundPressedProperty =
            DependencyProperty.Register("HubSectionHeaderButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubSectionHeaderButtonForegroundDisabled
        {
            get { return GetValue(HubSectionHeaderButtonForegroundDisabledProperty); }
            set { SetValue(HubSectionHeaderButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty HubSectionHeaderButtonForegroundDisabledProperty =
            DependencyProperty.Register("HubSectionHeaderButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubSectionHeaderForeground
        {
            get { return GetValue(HubSectionHeaderForegroundProperty); }
            set { SetValue(HubSectionHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty HubSectionHeaderForegroundProperty =
            DependencyProperty.Register("HubSectionHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewBackground
        {
            get { return GetValue(FlipViewBackgroundProperty); }
            set { SetValue(FlipViewBackgroundProperty, value); }
        }

        public static readonly DependencyProperty FlipViewBackgroundProperty =
            DependencyProperty.Register("FlipViewBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBackground
        {
            get { return GetValue(FlipViewNextPreviousButtonBackgroundProperty); }
            set { SetValue(FlipViewNextPreviousButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBackgroundProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBackgroundPointerOver
        {
            get { return GetValue(FlipViewNextPreviousButtonBackgroundPointerOverProperty); }
            set { SetValue(FlipViewNextPreviousButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBackgroundPressed
        {
            get { return GetValue(FlipViewNextPreviousButtonBackgroundPressedProperty); }
            set { SetValue(FlipViewNextPreviousButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBackgroundPressedProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousArrowForeground
        {
            get { return GetValue(FlipViewNextPreviousArrowForegroundProperty); }
            set { SetValue(FlipViewNextPreviousArrowForegroundProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousArrowForegroundProperty =
            DependencyProperty.Register("FlipViewNextPreviousArrowForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousArrowForegroundPointerOver
        {
            get { return GetValue(FlipViewNextPreviousArrowForegroundPointerOverProperty); }
            set { SetValue(FlipViewNextPreviousArrowForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousArrowForegroundPointerOverProperty =
            DependencyProperty.Register("FlipViewNextPreviousArrowForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousArrowForegroundPressed
        {
            get { return GetValue(FlipViewNextPreviousArrowForegroundPressedProperty); }
            set { SetValue(FlipViewNextPreviousArrowForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousArrowForegroundPressedProperty =
            DependencyProperty.Register("FlipViewNextPreviousArrowForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBorderBrush
        {
            get { return GetValue(FlipViewNextPreviousButtonBorderBrushProperty); }
            set { SetValue(FlipViewNextPreviousButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBorderBrushProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBorderBrushPointerOver
        {
            get { return GetValue(FlipViewNextPreviousButtonBorderBrushPointerOverProperty); }
            set { SetValue(FlipViewNextPreviousButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBorderBrushPressed
        {
            get { return GetValue(FlipViewNextPreviousButtonBorderBrushPressedProperty); }
            set { SetValue(FlipViewNextPreviousButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBorderBrushPressedProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonBackgroundPointerOver
        {
            get { return GetValue(DateTimePickerFlyoutButtonBackgroundPointerOverProperty); }
            set { SetValue(DateTimePickerFlyoutButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonBackgroundPressed
        {
            get { return GetValue(DateTimePickerFlyoutButtonBackgroundPressedProperty); }
            set { SetValue(DateTimePickerFlyoutButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonBackgroundPressedProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonBorderBrush
        {
            get { return GetValue(DateTimePickerFlyoutButtonBorderBrushProperty); }
            set { SetValue(DateTimePickerFlyoutButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonBorderBrushProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonBorderBrushPointerOver
        {
            get { return GetValue(DateTimePickerFlyoutButtonBorderBrushPointerOverProperty); }
            set { SetValue(DateTimePickerFlyoutButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonBorderBrushPressed
        {
            get { return GetValue(DateTimePickerFlyoutButtonBorderBrushPressedProperty); }
            set { SetValue(DateTimePickerFlyoutButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonBorderBrushPressedProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonForegroundPointerOver
        {
            get { return GetValue(DateTimePickerFlyoutButtonForegroundPointerOverProperty); }
            set { SetValue(DateTimePickerFlyoutButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonForegroundPointerOverProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonForegroundPressed
        {
            get { return GetValue(DateTimePickerFlyoutButtonForegroundPressedProperty); }
            set { SetValue(DateTimePickerFlyoutButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonForegroundPressedProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerSpacerFill
        {
            get { return GetValue(DatePickerSpacerFillProperty); }
            set { SetValue(DatePickerSpacerFillProperty, value); }
        }

        public static readonly DependencyProperty DatePickerSpacerFillProperty =
            DependencyProperty.Register("DatePickerSpacerFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerSpacerFillDisabled
        {
            get { return GetValue(DatePickerSpacerFillDisabledProperty); }
            set { SetValue(DatePickerSpacerFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty DatePickerSpacerFillDisabledProperty =
            DependencyProperty.Register("DatePickerSpacerFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerHeaderForeground
        {
            get { return GetValue(DatePickerHeaderForegroundProperty); }
            set { SetValue(DatePickerHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty DatePickerHeaderForegroundProperty =
            DependencyProperty.Register("DatePickerHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerHeaderForegroundDisabled
        {
            get { return GetValue(DatePickerHeaderForegroundDisabledProperty); }
            set { SetValue(DatePickerHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty DatePickerHeaderForegroundDisabledProperty =
            DependencyProperty.Register("DatePickerHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBorderBrush
        {
            get { return GetValue(DatePickerButtonBorderBrushProperty); }
            set { SetValue(DatePickerButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBorderBrushProperty =
            DependencyProperty.Register("DatePickerButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBorderBrushPointerOver
        {
            get { return GetValue(DatePickerButtonBorderBrushPointerOverProperty); }
            set { SetValue(DatePickerButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("DatePickerButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBorderBrushPressed
        {
            get { return GetValue(DatePickerButtonBorderBrushPressedProperty); }
            set { SetValue(DatePickerButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBorderBrushPressedProperty =
            DependencyProperty.Register("DatePickerButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBorderBrushDisabled
        {
            get { return GetValue(DatePickerButtonBorderBrushDisabledProperty); }
            set { SetValue(DatePickerButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("DatePickerButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBackground
        {
            get { return GetValue(DatePickerButtonBackgroundProperty); }
            set { SetValue(DatePickerButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBackgroundProperty =
            DependencyProperty.Register("DatePickerButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBackgroundPointerOver
        {
            get { return GetValue(DatePickerButtonBackgroundPointerOverProperty); }
            set { SetValue(DatePickerButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("DatePickerButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBackgroundPressed
        {
            get { return GetValue(DatePickerButtonBackgroundPressedProperty); }
            set { SetValue(DatePickerButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBackgroundPressedProperty =
            DependencyProperty.Register("DatePickerButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBackgroundDisabled
        {
            get { return GetValue(DatePickerButtonBackgroundDisabledProperty); }
            set { SetValue(DatePickerButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBackgroundDisabledProperty =
            DependencyProperty.Register("DatePickerButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBackgroundFocused
        {
            get { return GetValue(DatePickerButtonBackgroundFocusedProperty); }
            set { SetValue(DatePickerButtonBackgroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBackgroundFocusedProperty =
            DependencyProperty.Register("DatePickerButtonBackgroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonForeground
        {
            get { return GetValue(DatePickerButtonForegroundProperty); }
            set { SetValue(DatePickerButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonForegroundProperty =
            DependencyProperty.Register("DatePickerButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonForegroundPointerOver
        {
            get { return GetValue(DatePickerButtonForegroundPointerOverProperty); }
            set { SetValue(DatePickerButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonForegroundPointerOverProperty =
            DependencyProperty.Register("DatePickerButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonForegroundPressed
        {
            get { return GetValue(DatePickerButtonForegroundPressedProperty); }
            set { SetValue(DatePickerButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonForegroundPressedProperty =
            DependencyProperty.Register("DatePickerButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonForegroundDisabled
        {
            get { return GetValue(DatePickerButtonForegroundDisabledProperty); }
            set { SetValue(DatePickerButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonForegroundDisabledProperty =
            DependencyProperty.Register("DatePickerButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonForegroundFocused
        {
            get { return GetValue(DatePickerButtonForegroundFocusedProperty); }
            set { SetValue(DatePickerButtonForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonForegroundFocusedProperty =
            DependencyProperty.Register("DatePickerButtonForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerFlyoutPresenterBackground
        {
            get { return GetValue(DatePickerFlyoutPresenterBackgroundProperty); }
            set { SetValue(DatePickerFlyoutPresenterBackgroundProperty, value); }
        }

        public static readonly DependencyProperty DatePickerFlyoutPresenterBackgroundProperty =
            DependencyProperty.Register("DatePickerFlyoutPresenterBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerFlyoutPresenterBorderBrush
        {
            get { return GetValue(DatePickerFlyoutPresenterBorderBrushProperty); }
            set { SetValue(DatePickerFlyoutPresenterBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty DatePickerFlyoutPresenterBorderBrushProperty =
            DependencyProperty.Register("DatePickerFlyoutPresenterBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerFlyoutPresenterSpacerFill
        {
            get { return GetValue(DatePickerFlyoutPresenterSpacerFillProperty); }
            set { SetValue(DatePickerFlyoutPresenterSpacerFillProperty, value); }
        }

        public static readonly DependencyProperty DatePickerFlyoutPresenterSpacerFillProperty =
            DependencyProperty.Register("DatePickerFlyoutPresenterSpacerFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerFlyoutPresenterHighlightFill
        {
            get { return GetValue(DatePickerFlyoutPresenterHighlightFillProperty); }
            set { SetValue(DatePickerFlyoutPresenterHighlightFillProperty, value); }
        }

        public static readonly DependencyProperty DatePickerFlyoutPresenterHighlightFillProperty =
            DependencyProperty.Register("DatePickerFlyoutPresenterHighlightFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerSpacerFill
        {
            get { return GetValue(TimePickerSpacerFillProperty); }
            set { SetValue(TimePickerSpacerFillProperty, value); }
        }

        public static readonly DependencyProperty TimePickerSpacerFillProperty =
            DependencyProperty.Register("TimePickerSpacerFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerSpacerFillDisabled
        {
            get { return GetValue(TimePickerSpacerFillDisabledProperty); }
            set { SetValue(TimePickerSpacerFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty TimePickerSpacerFillDisabledProperty =
            DependencyProperty.Register("TimePickerSpacerFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerHeaderForeground
        {
            get { return GetValue(TimePickerHeaderForegroundProperty); }
            set { SetValue(TimePickerHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty TimePickerHeaderForegroundProperty =
            DependencyProperty.Register("TimePickerHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerHeaderForegroundDisabled
        {
            get { return GetValue(TimePickerHeaderForegroundDisabledProperty); }
            set { SetValue(TimePickerHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TimePickerHeaderForegroundDisabledProperty =
            DependencyProperty.Register("TimePickerHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBorderBrush
        {
            get { return GetValue(TimePickerButtonBorderBrushProperty); }
            set { SetValue(TimePickerButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBorderBrushProperty =
            DependencyProperty.Register("TimePickerButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBorderBrushPointerOver
        {
            get { return GetValue(TimePickerButtonBorderBrushPointerOverProperty); }
            set { SetValue(TimePickerButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("TimePickerButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBorderBrushPressed
        {
            get { return GetValue(TimePickerButtonBorderBrushPressedProperty); }
            set { SetValue(TimePickerButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBorderBrushPressedProperty =
            DependencyProperty.Register("TimePickerButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBorderBrushDisabled
        {
            get { return GetValue(TimePickerButtonBorderBrushDisabledProperty); }
            set { SetValue(TimePickerButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("TimePickerButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBackground
        {
            get { return GetValue(TimePickerButtonBackgroundProperty); }
            set { SetValue(TimePickerButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBackgroundProperty =
            DependencyProperty.Register("TimePickerButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBackgroundPointerOver
        {
            get { return GetValue(TimePickerButtonBackgroundPointerOverProperty); }
            set { SetValue(TimePickerButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("TimePickerButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBackgroundPressed
        {
            get { return GetValue(TimePickerButtonBackgroundPressedProperty); }
            set { SetValue(TimePickerButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBackgroundPressedProperty =
            DependencyProperty.Register("TimePickerButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBackgroundDisabled
        {
            get { return GetValue(TimePickerButtonBackgroundDisabledProperty); }
            set { SetValue(TimePickerButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBackgroundDisabledProperty =
            DependencyProperty.Register("TimePickerButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBackgroundFocused
        {
            get { return GetValue(TimePickerButtonBackgroundFocusedProperty); }
            set { SetValue(TimePickerButtonBackgroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBackgroundFocusedProperty =
            DependencyProperty.Register("TimePickerButtonBackgroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonForeground
        {
            get { return GetValue(TimePickerButtonForegroundProperty); }
            set { SetValue(TimePickerButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonForegroundProperty =
            DependencyProperty.Register("TimePickerButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonForegroundPointerOver
        {
            get { return GetValue(TimePickerButtonForegroundPointerOverProperty); }
            set { SetValue(TimePickerButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonForegroundPointerOverProperty =
            DependencyProperty.Register("TimePickerButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonForegroundPressed
        {
            get { return GetValue(TimePickerButtonForegroundPressedProperty); }
            set { SetValue(TimePickerButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonForegroundPressedProperty =
            DependencyProperty.Register("TimePickerButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonForegroundDisabled
        {
            get { return GetValue(TimePickerButtonForegroundDisabledProperty); }
            set { SetValue(TimePickerButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonForegroundDisabledProperty =
            DependencyProperty.Register("TimePickerButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonForegroundFocused
        {
            get { return GetValue(TimePickerButtonForegroundFocusedProperty); }
            set { SetValue(TimePickerButtonForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonForegroundFocusedProperty =
            DependencyProperty.Register("TimePickerButtonForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerFlyoutPresenterBackground
        {
            get { return GetValue(TimePickerFlyoutPresenterBackgroundProperty); }
            set { SetValue(TimePickerFlyoutPresenterBackgroundProperty, value); }
        }

        public static readonly DependencyProperty TimePickerFlyoutPresenterBackgroundProperty =
            DependencyProperty.Register("TimePickerFlyoutPresenterBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerFlyoutPresenterBorderBrush
        {
            get { return GetValue(TimePickerFlyoutPresenterBorderBrushProperty); }
            set { SetValue(TimePickerFlyoutPresenterBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty TimePickerFlyoutPresenterBorderBrushProperty =
            DependencyProperty.Register("TimePickerFlyoutPresenterBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerFlyoutPresenterSpacerFill
        {
            get { return GetValue(TimePickerFlyoutPresenterSpacerFillProperty); }
            set { SetValue(TimePickerFlyoutPresenterSpacerFillProperty, value); }
        }

        public static readonly DependencyProperty TimePickerFlyoutPresenterSpacerFillProperty =
            DependencyProperty.Register("TimePickerFlyoutPresenterSpacerFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerFlyoutPresenterHighlightFill
        {
            get { return GetValue(TimePickerFlyoutPresenterHighlightFillProperty); }
            set { SetValue(TimePickerFlyoutPresenterHighlightFillProperty, value); }
        }

        public static readonly DependencyProperty TimePickerFlyoutPresenterHighlightFillProperty =
            DependencyProperty.Register("TimePickerFlyoutPresenterHighlightFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorButtonBackground
        {
            get { return GetValue(LoopingSelectorButtonBackgroundProperty); }
            set { SetValue(LoopingSelectorButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorButtonBackgroundProperty =
            DependencyProperty.Register("LoopingSelectorButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemForeground
        {
            get { return GetValue(LoopingSelectorItemForegroundProperty); }
            set { SetValue(LoopingSelectorItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemForegroundProperty =
            DependencyProperty.Register("LoopingSelectorItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemForegroundSelected
        {
            get { return GetValue(LoopingSelectorItemForegroundSelectedProperty); }
            set { SetValue(LoopingSelectorItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemForegroundSelectedProperty =
            DependencyProperty.Register("LoopingSelectorItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemForegroundPointerOver
        {
            get { return GetValue(LoopingSelectorItemForegroundPointerOverProperty); }
            set { SetValue(LoopingSelectorItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemForegroundPointerOverProperty =
            DependencyProperty.Register("LoopingSelectorItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemForegroundPressed
        {
            get { return GetValue(LoopingSelectorItemForegroundPressedProperty); }
            set { SetValue(LoopingSelectorItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemForegroundPressedProperty =
            DependencyProperty.Register("LoopingSelectorItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemBackgroundPointerOver
        {
            get { return GetValue(LoopingSelectorItemBackgroundPointerOverProperty); }
            set { SetValue(LoopingSelectorItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemBackgroundPointerOverProperty =
            DependencyProperty.Register("LoopingSelectorItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemBackgroundPressed
        {
            get { return GetValue(LoopingSelectorItemBackgroundPressedProperty); }
            set { SetValue(LoopingSelectorItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemBackgroundPressedProperty =
            DependencyProperty.Register("LoopingSelectorItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlForeground
        {
            get { return GetValue(TextControlForegroundProperty); }
            set { SetValue(TextControlForegroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlForegroundProperty =
            DependencyProperty.Register("TextControlForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlForegroundPointerOver
        {
            get { return GetValue(TextControlForegroundPointerOverProperty); }
            set { SetValue(TextControlForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TextControlForegroundPointerOverProperty =
            DependencyProperty.Register("TextControlForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlForegroundFocused
        {
            get { return GetValue(TextControlForegroundFocusedProperty); }
            set { SetValue(TextControlForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty TextControlForegroundFocusedProperty =
            DependencyProperty.Register("TextControlForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlForegroundDisabled
        {
            get { return GetValue(TextControlForegroundDisabledProperty); }
            set { SetValue(TextControlForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TextControlForegroundDisabledProperty =
            DependencyProperty.Register("TextControlForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBackground
        {
            get { return GetValue(TextControlBackgroundProperty); }
            set { SetValue(TextControlBackgroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlBackgroundProperty =
            DependencyProperty.Register("TextControlBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBackgroundPointerOver
        {
            get { return GetValue(TextControlBackgroundPointerOverProperty); }
            set { SetValue(TextControlBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TextControlBackgroundPointerOverProperty =
            DependencyProperty.Register("TextControlBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBackgroundFocused
        {
            get { return GetValue(TextControlBackgroundFocusedProperty); }
            set { SetValue(TextControlBackgroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty TextControlBackgroundFocusedProperty =
            DependencyProperty.Register("TextControlBackgroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBackgroundDisabled
        {
            get { return GetValue(TextControlBackgroundDisabledProperty); }
            set { SetValue(TextControlBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TextControlBackgroundDisabledProperty =
            DependencyProperty.Register("TextControlBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBorderBrush
        {
            get { return GetValue(TextControlBorderBrushProperty); }
            set { SetValue(TextControlBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty TextControlBorderBrushProperty =
            DependencyProperty.Register("TextControlBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBorderBrushPointerOver
        {
            get { return GetValue(TextControlBorderBrushPointerOverProperty); }
            set { SetValue(TextControlBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TextControlBorderBrushPointerOverProperty =
            DependencyProperty.Register("TextControlBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBorderBrushFocused
        {
            get { return GetValue(TextControlBorderBrushFocusedProperty); }
            set { SetValue(TextControlBorderBrushFocusedProperty, value); }
        }

        public static readonly DependencyProperty TextControlBorderBrushFocusedProperty =
            DependencyProperty.Register("TextControlBorderBrushFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBorderBrushDisabled
        {
            get { return GetValue(TextControlBorderBrushDisabledProperty); }
            set { SetValue(TextControlBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty TextControlBorderBrushDisabledProperty =
            DependencyProperty.Register("TextControlBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlPlaceholderForeground
        {
            get { return GetValue(TextControlPlaceholderForegroundProperty); }
            set { SetValue(TextControlPlaceholderForegroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlPlaceholderForegroundProperty =
            DependencyProperty.Register("TextControlPlaceholderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlPlaceholderForegroundPointerOver
        {
            get { return GetValue(TextControlPlaceholderForegroundPointerOverProperty); }
            set { SetValue(TextControlPlaceholderForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TextControlPlaceholderForegroundPointerOverProperty =
            DependencyProperty.Register("TextControlPlaceholderForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlPlaceholderForegroundFocused
        {
            get { return GetValue(TextControlPlaceholderForegroundFocusedProperty); }
            set { SetValue(TextControlPlaceholderForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty TextControlPlaceholderForegroundFocusedProperty =
            DependencyProperty.Register("TextControlPlaceholderForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlPlaceholderForegroundDisabled
        {
            get { return GetValue(TextControlPlaceholderForegroundDisabledProperty); }
            set { SetValue(TextControlPlaceholderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TextControlPlaceholderForegroundDisabledProperty =
            DependencyProperty.Register("TextControlPlaceholderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlHeaderForeground
        {
            get { return GetValue(TextControlHeaderForegroundProperty); }
            set { SetValue(TextControlHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlHeaderForegroundProperty =
            DependencyProperty.Register("TextControlHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlHeaderForegroundDisabled
        {
            get { return GetValue(TextControlHeaderForegroundDisabledProperty); }
            set { SetValue(TextControlHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TextControlHeaderForegroundDisabledProperty =
            DependencyProperty.Register("TextControlHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlSelectionHighlightColor
        {
            get { return GetValue(TextControlSelectionHighlightColorProperty); }
            set { SetValue(TextControlSelectionHighlightColorProperty, value); }
        }

        public static readonly DependencyProperty TextControlSelectionHighlightColorProperty =
            DependencyProperty.Register("TextControlSelectionHighlightColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlButtonBackgroundPressed
        {
            get { return GetValue(TextControlButtonBackgroundPressedProperty); }
            set { SetValue(TextControlButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TextControlButtonBackgroundPressedProperty =
            DependencyProperty.Register("TextControlButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlButtonForeground
        {
            get { return GetValue(TextControlButtonForegroundProperty); }
            set { SetValue(TextControlButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlButtonForegroundProperty =
            DependencyProperty.Register("TextControlButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlButtonForegroundPointerOver
        {
            get { return GetValue(TextControlButtonForegroundPointerOverProperty); }
            set { SetValue(TextControlButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TextControlButtonForegroundPointerOverProperty =
            DependencyProperty.Register("TextControlButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlButtonForegroundPressed
        {
            get { return GetValue(TextControlButtonForegroundPressedProperty); }
            set { SetValue(TextControlButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TextControlButtonForegroundPressedProperty =
            DependencyProperty.Register("TextControlButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ContentLinkForegroundColor
        {
            get { return GetValue(ContentLinkForegroundColorProperty); }
            set { SetValue(ContentLinkForegroundColorProperty, value); }
        }

        public static readonly DependencyProperty ContentLinkForegroundColorProperty =
            DependencyProperty.Register("ContentLinkForegroundColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ContentLinkBackgroundColor
        {
            get { return GetValue(ContentLinkBackgroundColorProperty); }
            set { SetValue(ContentLinkBackgroundColorProperty, value); }
        }

        public static readonly DependencyProperty ContentLinkBackgroundColorProperty =
            DependencyProperty.Register("ContentLinkBackgroundColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlHighlighterForeground
        {
            get { return GetValue(TextControlHighlighterForegroundProperty); }
            set { SetValue(TextControlHighlighterForegroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlHighlighterForegroundProperty =
            DependencyProperty.Register("TextControlHighlighterForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlyoutPresenterBackground
        {
            get { return GetValue(FlyoutPresenterBackgroundProperty); }
            set { SetValue(FlyoutPresenterBackgroundProperty, value); }
        }

        public static readonly DependencyProperty FlyoutPresenterBackgroundProperty =
            DependencyProperty.Register("FlyoutPresenterBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlyoutBorderThemeBrush
        {
            get { return GetValue(FlyoutBorderThemeBrushProperty); }
            set { SetValue(FlyoutBorderThemeBrushProperty, value); }
        }

        public static readonly DependencyProperty FlyoutBorderThemeBrushProperty =
            DependencyProperty.Register("FlyoutBorderThemeBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemBackgroundPointerOver
        {
            get { return GetValue(ToggleMenuFlyoutItemBackgroundPointerOverProperty); }
            set { SetValue(ToggleMenuFlyoutItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemBackgroundPointerOverProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemBackgroundPressed
        {
            get { return GetValue(ToggleMenuFlyoutItemBackgroundPressedProperty); }
            set { SetValue(ToggleMenuFlyoutItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemBackgroundPressedProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemForeground
        {
            get { return GetValue(ToggleMenuFlyoutItemForegroundProperty); }
            set { SetValue(ToggleMenuFlyoutItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemForegroundProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemForegroundPointerOver
        {
            get { return GetValue(ToggleMenuFlyoutItemForegroundPointerOverProperty); }
            set { SetValue(ToggleMenuFlyoutItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemForegroundPointerOverProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemForegroundPressed
        {
            get { return GetValue(ToggleMenuFlyoutItemForegroundPressedProperty); }
            set { SetValue(ToggleMenuFlyoutItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemForegroundPressedProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemForegroundDisabled
        {
            get { return GetValue(ToggleMenuFlyoutItemForegroundDisabledProperty); }
            set { SetValue(ToggleMenuFlyoutItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemForegroundDisabledProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemKeyboardAcceleratorTextForeground
        {
            get { return GetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundProperty); }
            set { SetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemKeyboardAcceleratorTextForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver
        {
            get { return GetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty); }
            set { SetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressed
        {
            get { return GetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty); }
            set { SetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled
        {
            get { return GetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty); }
            set { SetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemCheckGlyphForeground
        {
            get { return GetValue(ToggleMenuFlyoutItemCheckGlyphForegroundProperty); }
            set { SetValue(ToggleMenuFlyoutItemCheckGlyphForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemCheckGlyphForegroundProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemCheckGlyphForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemCheckGlyphForegroundPointerOver
        {
            get { return GetValue(ToggleMenuFlyoutItemCheckGlyphForegroundPointerOverProperty); }
            set { SetValue(ToggleMenuFlyoutItemCheckGlyphForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemCheckGlyphForegroundPointerOverProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemCheckGlyphForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemCheckGlyphForegroundPressed
        {
            get { return GetValue(ToggleMenuFlyoutItemCheckGlyphForegroundPressedProperty); }
            set { SetValue(ToggleMenuFlyoutItemCheckGlyphForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemCheckGlyphForegroundPressedProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemCheckGlyphForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemCheckGlyphForegroundDisabled
        {
            get { return GetValue(ToggleMenuFlyoutItemCheckGlyphForegroundDisabledProperty); }
            set { SetValue(ToggleMenuFlyoutItemCheckGlyphForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemCheckGlyphForegroundDisabledProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemCheckGlyphForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBackground
        {
            get { return GetValue(PivotNextButtonBackgroundProperty); }
            set { SetValue(PivotNextButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBackgroundProperty =
            DependencyProperty.Register("PivotNextButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBackgroundPointerOver
        {
            get { return GetValue(PivotNextButtonBackgroundPointerOverProperty); }
            set { SetValue(PivotNextButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("PivotNextButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBackgroundPressed
        {
            get { return GetValue(PivotNextButtonBackgroundPressedProperty); }
            set { SetValue(PivotNextButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBackgroundPressedProperty =
            DependencyProperty.Register("PivotNextButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBorderBrush
        {
            get { return GetValue(PivotNextButtonBorderBrushProperty); }
            set { SetValue(PivotNextButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBorderBrushProperty =
            DependencyProperty.Register("PivotNextButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBorderBrushPointerOver
        {
            get { return GetValue(PivotNextButtonBorderBrushPointerOverProperty); }
            set { SetValue(PivotNextButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("PivotNextButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBorderBrushPressed
        {
            get { return GetValue(PivotNextButtonBorderBrushPressedProperty); }
            set { SetValue(PivotNextButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBorderBrushPressedProperty =
            DependencyProperty.Register("PivotNextButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonForeground
        {
            get { return GetValue(PivotNextButtonForegroundProperty); }
            set { SetValue(PivotNextButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonForegroundProperty =
            DependencyProperty.Register("PivotNextButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonForegroundPointerOver
        {
            get { return GetValue(PivotNextButtonForegroundPointerOverProperty); }
            set { SetValue(PivotNextButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonForegroundPointerOverProperty =
            DependencyProperty.Register("PivotNextButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonForegroundPressed
        {
            get { return GetValue(PivotNextButtonForegroundPressedProperty); }
            set { SetValue(PivotNextButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonForegroundPressedProperty =
            DependencyProperty.Register("PivotNextButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBackground
        {
            get { return GetValue(PivotPreviousButtonBackgroundProperty); }
            set { SetValue(PivotPreviousButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBackgroundProperty =
            DependencyProperty.Register("PivotPreviousButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBackgroundPointerOver
        {
            get { return GetValue(PivotPreviousButtonBackgroundPointerOverProperty); }
            set { SetValue(PivotPreviousButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("PivotPreviousButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBackgroundPressed
        {
            get { return GetValue(PivotPreviousButtonBackgroundPressedProperty); }
            set { SetValue(PivotPreviousButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBackgroundPressedProperty =
            DependencyProperty.Register("PivotPreviousButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBorderBrush
        {
            get { return GetValue(PivotPreviousButtonBorderBrushProperty); }
            set { SetValue(PivotPreviousButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBorderBrushProperty =
            DependencyProperty.Register("PivotPreviousButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBorderBrushPointerOver
        {
            get { return GetValue(PivotPreviousButtonBorderBrushPointerOverProperty); }
            set { SetValue(PivotPreviousButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("PivotPreviousButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBorderBrushPressed
        {
            get { return GetValue(PivotPreviousButtonBorderBrushPressedProperty); }
            set { SetValue(PivotPreviousButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBorderBrushPressedProperty =
            DependencyProperty.Register("PivotPreviousButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonForeground
        {
            get { return GetValue(PivotPreviousButtonForegroundProperty); }
            set { SetValue(PivotPreviousButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonForegroundProperty =
            DependencyProperty.Register("PivotPreviousButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonForegroundPointerOver
        {
            get { return GetValue(PivotPreviousButtonForegroundPointerOverProperty); }
            set { SetValue(PivotPreviousButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonForegroundPointerOverProperty =
            DependencyProperty.Register("PivotPreviousButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonForegroundPressed
        {
            get { return GetValue(PivotPreviousButtonForegroundPressedProperty); }
            set { SetValue(PivotPreviousButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonForegroundPressedProperty =
            DependencyProperty.Register("PivotPreviousButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemBackgroundUnselectedPointerOver
        {
            get { return GetValue(PivotHeaderItemBackgroundUnselectedPointerOverProperty); }
            set { SetValue(PivotHeaderItemBackgroundUnselectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemBackgroundUnselectedPointerOverProperty =
            DependencyProperty.Register("PivotHeaderItemBackgroundUnselectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemBackgroundUnselectedPressed
        {
            get { return GetValue(PivotHeaderItemBackgroundUnselectedPressedProperty); }
            set { SetValue(PivotHeaderItemBackgroundUnselectedPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemBackgroundUnselectedPressedProperty =
            DependencyProperty.Register("PivotHeaderItemBackgroundUnselectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemBackgroundSelected
        {
            get { return GetValue(PivotHeaderItemBackgroundSelectedProperty); }
            set { SetValue(PivotHeaderItemBackgroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemBackgroundSelectedProperty =
            DependencyProperty.Register("PivotHeaderItemBackgroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemBackgroundSelectedPointerOver
        {
            get { return GetValue(PivotHeaderItemBackgroundSelectedPointerOverProperty); }
            set { SetValue(PivotHeaderItemBackgroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemBackgroundSelectedPointerOverProperty =
            DependencyProperty.Register("PivotHeaderItemBackgroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemBackgroundSelectedPressed
        {
            get { return GetValue(PivotHeaderItemBackgroundSelectedPressedProperty); }
            set { SetValue(PivotHeaderItemBackgroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemBackgroundSelectedPressedProperty =
            DependencyProperty.Register("PivotHeaderItemBackgroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundUnselected
        {
            get { return GetValue(PivotHeaderItemForegroundUnselectedProperty); }
            set { SetValue(PivotHeaderItemForegroundUnselectedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundUnselectedProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundUnselected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundUnselectedPointerOver
        {
            get { return GetValue(PivotHeaderItemForegroundUnselectedPointerOverProperty); }
            set { SetValue(PivotHeaderItemForegroundUnselectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundUnselectedPointerOverProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundUnselectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundUnselectedPressed
        {
            get { return GetValue(PivotHeaderItemForegroundUnselectedPressedProperty); }
            set { SetValue(PivotHeaderItemForegroundUnselectedPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundUnselectedPressedProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundUnselectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundSelected
        {
            get { return GetValue(PivotHeaderItemForegroundSelectedProperty); }
            set { SetValue(PivotHeaderItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundSelectedProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundSelectedPointerOver
        {
            get { return GetValue(PivotHeaderItemForegroundSelectedPointerOverProperty); }
            set { SetValue(PivotHeaderItemForegroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundSelectedPointerOverProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundSelectedPressed
        {
            get { return GetValue(PivotHeaderItemForegroundSelectedPressedProperty); }
            set { SetValue(PivotHeaderItemForegroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundSelectedPressedProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundDisabled
        {
            get { return GetValue(PivotHeaderItemForegroundDisabledProperty); }
            set { SetValue(PivotHeaderItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundDisabledProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemFocusPipeFill
        {
            get { return GetValue(PivotHeaderItemFocusPipeFillProperty); }
            set { SetValue(PivotHeaderItemFocusPipeFillProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemFocusPipeFillProperty =
            DependencyProperty.Register("PivotHeaderItemFocusPipeFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemSelectedPipeFill
        {
            get { return GetValue(PivotHeaderItemSelectedPipeFillProperty); }
            set { SetValue(PivotHeaderItemSelectedPipeFillProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemSelectedPipeFillProperty =
            DependencyProperty.Register("PivotHeaderItemSelectedPipeFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewHeaderItemDividerStroke
        {
            get { return GetValue(GridViewHeaderItemDividerStrokeProperty); }
            set { SetValue(GridViewHeaderItemDividerStrokeProperty, value); }
        }

        public static readonly DependencyProperty GridViewHeaderItemDividerStrokeProperty =
            DependencyProperty.Register("GridViewHeaderItemDividerStroke", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemBackgroundPointerOver
        {
            get { return GetValue(GridViewItemBackgroundPointerOverProperty); }
            set { SetValue(GridViewItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemBackgroundPointerOverProperty =
            DependencyProperty.Register("GridViewItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemBackgroundPressed
        {
            get { return GetValue(GridViewItemBackgroundPressedProperty); }
            set { SetValue(GridViewItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemBackgroundPressedProperty =
            DependencyProperty.Register("GridViewItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemBackgroundSelected
        {
            get { return GetValue(GridViewItemBackgroundSelectedProperty); }
            set { SetValue(GridViewItemBackgroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemBackgroundSelectedProperty =
            DependencyProperty.Register("GridViewItemBackgroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemBackgroundSelectedPointerOver
        {
            get { return GetValue(GridViewItemBackgroundSelectedPointerOverProperty); }
            set { SetValue(GridViewItemBackgroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemBackgroundSelectedPointerOverProperty =
            DependencyProperty.Register("GridViewItemBackgroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemBackgroundSelectedPressed
        {
            get { return GetValue(GridViewItemBackgroundSelectedPressedProperty); }
            set { SetValue(GridViewItemBackgroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemBackgroundSelectedPressedProperty =
            DependencyProperty.Register("GridViewItemBackgroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemForeground
        {
            get { return GetValue(GridViewItemForegroundProperty); }
            set { SetValue(GridViewItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemForegroundProperty =
            DependencyProperty.Register("GridViewItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemForegroundPointerOver
        {
            get { return GetValue(GridViewItemForegroundPointerOverProperty); }
            set { SetValue(GridViewItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemForegroundPointerOverProperty =
            DependencyProperty.Register("GridViewItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemForegroundSelected
        {
            get { return GetValue(GridViewItemForegroundSelectedProperty); }
            set { SetValue(GridViewItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemForegroundSelectedProperty =
            DependencyProperty.Register("GridViewItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemFocusVisualPrimaryBrush
        {
            get { return GetValue(GridViewItemFocusVisualPrimaryBrushProperty); }
            set { SetValue(GridViewItemFocusVisualPrimaryBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemFocusVisualPrimaryBrushProperty =
            DependencyProperty.Register("GridViewItemFocusVisualPrimaryBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemFocusVisualSecondaryBrush
        {
            get { return GetValue(GridViewItemFocusVisualSecondaryBrushProperty); }
            set { SetValue(GridViewItemFocusVisualSecondaryBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemFocusVisualSecondaryBrushProperty =
            DependencyProperty.Register("GridViewItemFocusVisualSecondaryBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemFocusBorderBrush
        {
            get { return GetValue(GridViewItemFocusBorderBrushProperty); }
            set { SetValue(GridViewItemFocusBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemFocusBorderBrushProperty =
            DependencyProperty.Register("GridViewItemFocusBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemFocusSecondaryBorderBrush
        {
            get { return GetValue(GridViewItemFocusSecondaryBorderBrushProperty); }
            set { SetValue(GridViewItemFocusSecondaryBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemFocusSecondaryBorderBrushProperty =
            DependencyProperty.Register("GridViewItemFocusSecondaryBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemCheckBrush
        {
            get { return GetValue(GridViewItemCheckBrushProperty); }
            set { SetValue(GridViewItemCheckBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemCheckBrushProperty =
            DependencyProperty.Register("GridViewItemCheckBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemCheckBoxBrush
        {
            get { return GetValue(GridViewItemCheckBoxBrushProperty); }
            set { SetValue(GridViewItemCheckBoxBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemCheckBoxBrushProperty =
            DependencyProperty.Register("GridViewItemCheckBoxBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemDragForeground
        {
            get { return GetValue(GridViewItemDragForegroundProperty); }
            set { SetValue(GridViewItemDragForegroundProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemDragForegroundProperty =
            DependencyProperty.Register("GridViewItemDragForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemPlaceholderBackground
        {
            get { return GetValue(GridViewItemPlaceholderBackgroundProperty); }
            set { SetValue(GridViewItemPlaceholderBackgroundProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemPlaceholderBackgroundProperty =
            DependencyProperty.Register("GridViewItemPlaceholderBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MediaTransportControlsPanelBackground
        {
            get { return GetValue(MediaTransportControlsPanelBackgroundProperty); }
            set { SetValue(MediaTransportControlsPanelBackgroundProperty, value); }
        }

        public static readonly DependencyProperty MediaTransportControlsPanelBackgroundProperty =
            DependencyProperty.Register("MediaTransportControlsPanelBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MediaTransportControlsFlyoutBackground
        {
            get { return GetValue(MediaTransportControlsFlyoutBackgroundProperty); }
            set { SetValue(MediaTransportControlsFlyoutBackgroundProperty, value); }
        }

        public static readonly DependencyProperty MediaTransportControlsFlyoutBackgroundProperty =
            DependencyProperty.Register("MediaTransportControlsFlyoutBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarLightDismissOverlayBackground
        {
            get { return GetValue(AppBarLightDismissOverlayBackgroundProperty); }
            set { SetValue(AppBarLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("AppBarLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerLightDismissOverlayBackground
        {
            get { return GetValue(CalendarDatePickerLightDismissOverlayBackgroundProperty); }
            set { SetValue(CalendarDatePickerLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("CalendarDatePickerLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxLightDismissOverlayBackground
        {
            get { return GetValue(ComboBoxLightDismissOverlayBackgroundProperty); }
            set { SetValue(ComboBoxLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("ComboBoxLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerLightDismissOverlayBackground
        {
            get { return GetValue(DatePickerLightDismissOverlayBackgroundProperty); }
            set { SetValue(DatePickerLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty DatePickerLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("DatePickerLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlyoutLightDismissOverlayBackground
        {
            get { return GetValue(FlyoutLightDismissOverlayBackgroundProperty); }
            set { SetValue(FlyoutLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty FlyoutLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("FlyoutLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PopupLightDismissOverlayBackground
        {
            get { return GetValue(PopupLightDismissOverlayBackgroundProperty); }
            set { SetValue(PopupLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty PopupLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("PopupLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitViewLightDismissOverlayBackground
        {
            get { return GetValue(SplitViewLightDismissOverlayBackgroundProperty); }
            set { SetValue(SplitViewLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SplitViewLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("SplitViewLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerLightDismissOverlayBackground
        {
            get { return GetValue(TimePickerLightDismissOverlayBackgroundProperty); }
            set { SetValue(TimePickerLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty TimePickerLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("TimePickerLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object JumpListDefaultEnabledBackground
        {
            get { return GetValue(JumpListDefaultEnabledBackgroundProperty); }
            set { SetValue(JumpListDefaultEnabledBackgroundProperty, value); }
        }

        public static readonly DependencyProperty JumpListDefaultEnabledBackgroundProperty =
            DependencyProperty.Register("JumpListDefaultEnabledBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object JumpListDefaultEnabledForeground
        {
            get { return GetValue(JumpListDefaultEnabledForegroundProperty); }
            set { SetValue(JumpListDefaultEnabledForegroundProperty, value); }
        }

        public static readonly DependencyProperty JumpListDefaultEnabledForegroundProperty =
            DependencyProperty.Register("JumpListDefaultEnabledForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object JumpListDefaultDisabledBackground
        {
            get { return GetValue(JumpListDefaultDisabledBackgroundProperty); }
            set { SetValue(JumpListDefaultDisabledBackgroundProperty, value); }
        }

        public static readonly DependencyProperty JumpListDefaultDisabledBackgroundProperty =
            DependencyProperty.Register("JumpListDefaultDisabledBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object JumpListDefaultDisabledForeground
        {
            get { return GetValue(JumpListDefaultDisabledForegroundProperty); }
            set { SetValue(JumpListDefaultDisabledForegroundProperty, value); }
        }

        public static readonly DependencyProperty JumpListDefaultDisabledForegroundProperty =
            DependencyProperty.Register("JumpListDefaultDisabledForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object KeyTipForeground
        {
            get { return GetValue(KeyTipForegroundProperty); }
            set { SetValue(KeyTipForegroundProperty, value); }
        }

        public static readonly DependencyProperty KeyTipForegroundProperty =
            DependencyProperty.Register("KeyTipForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object KeyTipBackground
        {
            get { return GetValue(KeyTipBackgroundProperty); }
            set { SetValue(KeyTipBackgroundProperty, value); }
        }

        public static readonly DependencyProperty KeyTipBackgroundProperty =
            DependencyProperty.Register("KeyTipBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object KeyTipBorderBrush
        {
            get { return GetValue(KeyTipBorderBrushProperty); }
            set { SetValue(KeyTipBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty KeyTipBorderBrushProperty =
            DependencyProperty.Register("KeyTipBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutPresenterBackground
        {
            get { return GetValue(MenuFlyoutPresenterBackgroundProperty); }
            set { SetValue(MenuFlyoutPresenterBackgroundProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutPresenterBackgroundProperty =
            DependencyProperty.Register("MenuFlyoutPresenterBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutPresenterBorderBrush
        {
            get { return GetValue(MenuFlyoutPresenterBorderBrushProperty); }
            set { SetValue(MenuFlyoutPresenterBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutPresenterBorderBrushProperty =
            DependencyProperty.Register("MenuFlyoutPresenterBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemBackgroundPointerOver
        {
            get { return GetValue(MenuFlyoutItemBackgroundPointerOverProperty); }
            set { SetValue(MenuFlyoutItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemBackgroundPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemBackgroundPressed
        {
            get { return GetValue(MenuFlyoutItemBackgroundPressedProperty); }
            set { SetValue(MenuFlyoutItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemBackgroundPressedProperty =
            DependencyProperty.Register("MenuFlyoutItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemForeground
        {
            get { return GetValue(MenuFlyoutItemForegroundProperty); }
            set { SetValue(MenuFlyoutItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemForegroundProperty =
            DependencyProperty.Register("MenuFlyoutItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemForegroundPointerOver
        {
            get { return GetValue(MenuFlyoutItemForegroundPointerOverProperty); }
            set { SetValue(MenuFlyoutItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemForegroundPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemForegroundPressed
        {
            get { return GetValue(MenuFlyoutItemForegroundPressedProperty); }
            set { SetValue(MenuFlyoutItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemForegroundPressedProperty =
            DependencyProperty.Register("MenuFlyoutItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemForegroundDisabled
        {
            get { return GetValue(MenuFlyoutItemForegroundDisabledProperty); }
            set { SetValue(MenuFlyoutItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemForegroundDisabledProperty =
            DependencyProperty.Register("MenuFlyoutItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemBackgroundPointerOver
        {
            get { return GetValue(MenuFlyoutSubItemBackgroundPointerOverProperty); }
            set { SetValue(MenuFlyoutSubItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemBackgroundPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutSubItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemBackgroundPressed
        {
            get { return GetValue(MenuFlyoutSubItemBackgroundPressedProperty); }
            set { SetValue(MenuFlyoutSubItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemBackgroundPressedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemBackgroundSubMenuOpened
        {
            get { return GetValue(MenuFlyoutSubItemBackgroundSubMenuOpenedProperty); }
            set { SetValue(MenuFlyoutSubItemBackgroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemBackgroundSubMenuOpenedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemBackgroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemForeground
        {
            get { return GetValue(MenuFlyoutSubItemForegroundProperty); }
            set { SetValue(MenuFlyoutSubItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemForegroundProperty =
            DependencyProperty.Register("MenuFlyoutSubItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemForegroundPointerOver
        {
            get { return GetValue(MenuFlyoutSubItemForegroundPointerOverProperty); }
            set { SetValue(MenuFlyoutSubItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemForegroundPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutSubItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemForegroundPressed
        {
            get { return GetValue(MenuFlyoutSubItemForegroundPressedProperty); }
            set { SetValue(MenuFlyoutSubItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemForegroundPressedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemForegroundSubMenuOpened
        {
            get { return GetValue(MenuFlyoutSubItemForegroundSubMenuOpenedProperty); }
            set { SetValue(MenuFlyoutSubItemForegroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemForegroundSubMenuOpenedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemForegroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemForegroundDisabled
        {
            get { return GetValue(MenuFlyoutSubItemForegroundDisabledProperty); }
            set { SetValue(MenuFlyoutSubItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemForegroundDisabledProperty =
            DependencyProperty.Register("MenuFlyoutSubItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemChevron
        {
            get { return GetValue(MenuFlyoutSubItemChevronProperty); }
            set { SetValue(MenuFlyoutSubItemChevronProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemChevronProperty =
            DependencyProperty.Register("MenuFlyoutSubItemChevron", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemChevronPointerOver
        {
            get { return GetValue(MenuFlyoutSubItemChevronPointerOverProperty); }
            set { SetValue(MenuFlyoutSubItemChevronPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemChevronPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutSubItemChevronPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemChevronPressed
        {
            get { return GetValue(MenuFlyoutSubItemChevronPressedProperty); }
            set { SetValue(MenuFlyoutSubItemChevronPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemChevronPressedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemChevronPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemChevronSubMenuOpened
        {
            get { return GetValue(MenuFlyoutSubItemChevronSubMenuOpenedProperty); }
            set { SetValue(MenuFlyoutSubItemChevronSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemChevronSubMenuOpenedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemChevronSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemChevronDisabled
        {
            get { return GetValue(MenuFlyoutSubItemChevronDisabledProperty); }
            set { SetValue(MenuFlyoutSubItemChevronDisabledProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemChevronDisabledProperty =
            DependencyProperty.Register("MenuFlyoutSubItemChevronDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutLightDismissOverlayBackground
        {
            get { return GetValue(MenuFlyoutLightDismissOverlayBackgroundProperty); }
            set { SetValue(MenuFlyoutLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("MenuFlyoutLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlUnselectedForeground
        {
            get { return GetValue(RatingControlUnselectedForegroundProperty); }
            set { SetValue(RatingControlUnselectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlUnselectedForegroundProperty =
            DependencyProperty.Register("RatingControlUnselectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlSelectedForeground
        {
            get { return GetValue(RatingControlSelectedForegroundProperty); }
            set { SetValue(RatingControlSelectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlSelectedForegroundProperty =
            DependencyProperty.Register("RatingControlSelectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlPlaceholderForeground
        {
            get { return GetValue(RatingControlPlaceholderForegroundProperty); }
            set { SetValue(RatingControlPlaceholderForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlPlaceholderForegroundProperty =
            DependencyProperty.Register("RatingControlPlaceholderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlPointerOverPlaceholderForeground
        {
            get { return GetValue(RatingControlPointerOverPlaceholderForegroundProperty); }
            set { SetValue(RatingControlPointerOverPlaceholderForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlPointerOverPlaceholderForegroundProperty =
            DependencyProperty.Register("RatingControlPointerOverPlaceholderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlPointerOverUnselectedForeground
        {
            get { return GetValue(RatingControlPointerOverUnselectedForegroundProperty); }
            set { SetValue(RatingControlPointerOverUnselectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlPointerOverUnselectedForegroundProperty =
            DependencyProperty.Register("RatingControlPointerOverUnselectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlPointerOverSelectedForeground
        {
            get { return GetValue(RatingControlPointerOverSelectedForegroundProperty); }
            set { SetValue(RatingControlPointerOverSelectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlPointerOverSelectedForegroundProperty =
            DependencyProperty.Register("RatingControlPointerOverSelectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlDisabledSelectedForeground
        {
            get { return GetValue(RatingControlDisabledSelectedForegroundProperty); }
            set { SetValue(RatingControlDisabledSelectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlDisabledSelectedForegroundProperty =
            DependencyProperty.Register("RatingControlDisabledSelectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlCaptionForeground
        {
            get { return GetValue(RatingControlCaptionForegroundProperty); }
            set { SetValue(RatingControlCaptionForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlCaptionForegroundProperty =
            DependencyProperty.Register("RatingControlCaptionForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForeground
        {
            get { return GetValue(NavigationViewItemForegroundProperty); }
            set { SetValue(NavigationViewItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundProperty =
            DependencyProperty.Register("NavigationViewItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundPointerOver
        {
            get { return GetValue(NavigationViewItemForegroundPointerOverProperty); }
            set { SetValue(NavigationViewItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundPointerOverProperty =
            DependencyProperty.Register("NavigationViewItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundPressed
        {
            get { return GetValue(NavigationViewItemForegroundPressedProperty); }
            set { SetValue(NavigationViewItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundPressedProperty =
            DependencyProperty.Register("NavigationViewItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundDisabled
        {
            get { return GetValue(NavigationViewItemForegroundDisabledProperty); }
            set { SetValue(NavigationViewItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundDisabledProperty =
            DependencyProperty.Register("NavigationViewItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundChecked
        {
            get { return GetValue(NavigationViewItemForegroundCheckedProperty); }
            set { SetValue(NavigationViewItemForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundCheckedProperty =
            DependencyProperty.Register("NavigationViewItemForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundCheckedPointerOver
        {
            get { return GetValue(NavigationViewItemForegroundCheckedPointerOverProperty); }
            set { SetValue(NavigationViewItemForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("NavigationViewItemForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundCheckedPressed
        {
            get { return GetValue(NavigationViewItemForegroundCheckedPressedProperty); }
            set { SetValue(NavigationViewItemForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundCheckedPressedProperty =
            DependencyProperty.Register("NavigationViewItemForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundCheckedDisabled
        {
            get { return GetValue(NavigationViewItemForegroundCheckedDisabledProperty); }
            set { SetValue(NavigationViewItemForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundCheckedDisabledProperty =
            DependencyProperty.Register("NavigationViewItemForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundSelected
        {
            get { return GetValue(NavigationViewItemForegroundSelectedProperty); }
            set { SetValue(NavigationViewItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundSelectedProperty =
            DependencyProperty.Register("NavigationViewItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundSelectedPointerOver
        {
            get { return GetValue(NavigationViewItemForegroundSelectedPointerOverProperty); }
            set { SetValue(NavigationViewItemForegroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundSelectedPointerOverProperty =
            DependencyProperty.Register("NavigationViewItemForegroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundSelectedPressed
        {
            get { return GetValue(NavigationViewItemForegroundSelectedPressedProperty); }
            set { SetValue(NavigationViewItemForegroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundSelectedPressedProperty =
            DependencyProperty.Register("NavigationViewItemForegroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundSelectedDisabled
        {
            get { return GetValue(NavigationViewItemForegroundSelectedDisabledProperty); }
            set { SetValue(NavigationViewItemForegroundSelectedDisabledProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundSelectedDisabledProperty =
            DependencyProperty.Register("NavigationViewItemForegroundSelectedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewSelectionIndicatorForeground
        {
            get { return GetValue(NavigationViewSelectionIndicatorForegroundProperty); }
            set { SetValue(NavigationViewSelectionIndicatorForegroundProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewSelectionIndicatorForegroundProperty =
            DependencyProperty.Register("NavigationViewSelectionIndicatorForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TopNavigationViewItemForeground
        {
            get { return GetValue(TopNavigationViewItemForegroundProperty); }
            set { SetValue(TopNavigationViewItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty TopNavigationViewItemForegroundProperty =
            DependencyProperty.Register("TopNavigationViewItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TopNavigationViewItemForegroundPointerOver
        {
            get { return GetValue(TopNavigationViewItemForegroundPointerOverProperty); }
            set { SetValue(TopNavigationViewItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TopNavigationViewItemForegroundPointerOverProperty =
            DependencyProperty.Register("TopNavigationViewItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TopNavigationViewItemForegroundPressed
        {
            get { return GetValue(TopNavigationViewItemForegroundPressedProperty); }
            set { SetValue(TopNavigationViewItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TopNavigationViewItemForegroundPressedProperty =
            DependencyProperty.Register("TopNavigationViewItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TopNavigationViewItemForegroundSelected
        {
            get { return GetValue(TopNavigationViewItemForegroundSelectedProperty); }
            set { SetValue(TopNavigationViewItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty TopNavigationViewItemForegroundSelectedProperty =
            DependencyProperty.Register("TopNavigationViewItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TopNavigationViewItemForegroundDisabled
        {
            get { return GetValue(TopNavigationViewItemForegroundDisabledProperty); }
            set { SetValue(TopNavigationViewItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TopNavigationViewItemForegroundDisabledProperty =
            DependencyProperty.Register("TopNavigationViewItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ColorPickerSliderThumbBackground
        {
            get { return GetValue(ColorPickerSliderThumbBackgroundProperty); }
            set { SetValue(ColorPickerSliderThumbBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ColorPickerSliderThumbBackgroundProperty =
            DependencyProperty.Register("ColorPickerSliderThumbBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ColorPickerSliderThumbBackgroundPointerOver
        {
            get { return GetValue(ColorPickerSliderThumbBackgroundPointerOverProperty); }
            set { SetValue(ColorPickerSliderThumbBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ColorPickerSliderThumbBackgroundPointerOverProperty =
            DependencyProperty.Register("ColorPickerSliderThumbBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ColorPickerSliderThumbBackgroundPressed
        {
            get { return GetValue(ColorPickerSliderThumbBackgroundPressedProperty); }
            set { SetValue(ColorPickerSliderThumbBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ColorPickerSliderThumbBackgroundPressedProperty =
            DependencyProperty.Register("ColorPickerSliderThumbBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ColorPickerSliderThumbBackgroundDisabled
        {
            get { return GetValue(ColorPickerSliderThumbBackgroundDisabledProperty); }
            set { SetValue(ColorPickerSliderThumbBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ColorPickerSliderThumbBackgroundDisabledProperty =
            DependencyProperty.Register("ColorPickerSliderThumbBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ColorPickerSliderTrackFillDisabled
        {
            get { return GetValue(ColorPickerSliderTrackFillDisabledProperty); }
            set { SetValue(ColorPickerSliderTrackFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty ColorPickerSliderTrackFillDisabledProperty =
            DependencyProperty.Register("ColorPickerSliderTrackFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBackgroundPointerOver
        {
            get { return GetValue(MenuBarItemBackgroundPointerOverProperty); }
            set { SetValue(MenuBarItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBackgroundPointerOverProperty =
            DependencyProperty.Register("MenuBarItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBackgroundPressed
        {
            get { return GetValue(MenuBarItemBackgroundPressedProperty); }
            set { SetValue(MenuBarItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBackgroundPressedProperty =
            DependencyProperty.Register("MenuBarItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBackgroundSelected
        {
            get { return GetValue(MenuBarItemBackgroundSelectedProperty); }
            set { SetValue(MenuBarItemBackgroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBackgroundSelectedProperty =
            DependencyProperty.Register("MenuBarItemBackgroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBorderBrush
        {
            get { return GetValue(MenuBarItemBorderBrushProperty); }
            set { SetValue(MenuBarItemBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBorderBrushProperty =
            DependencyProperty.Register("MenuBarItemBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBorderBrushPointerOver
        {
            get { return GetValue(MenuBarItemBorderBrushPointerOverProperty); }
            set { SetValue(MenuBarItemBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBorderBrushPointerOverProperty =
            DependencyProperty.Register("MenuBarItemBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBorderBrushPressed
        {
            get { return GetValue(MenuBarItemBorderBrushPressedProperty); }
            set { SetValue(MenuBarItemBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBorderBrushPressedProperty =
            DependencyProperty.Register("MenuBarItemBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBorderBrushSelected
        {
            get { return GetValue(MenuBarItemBorderBrushSelectedProperty); }
            set { SetValue(MenuBarItemBorderBrushSelectedProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBorderBrushSelectedProperty =
            DependencyProperty.Register("MenuBarItemBorderBrushSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonBackgroundPointerOver
        {
            get { return GetValue(AppBarButtonBackgroundPointerOverProperty); }
            set { SetValue(AppBarButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("AppBarButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonBackgroundPressed
        {
            get { return GetValue(AppBarButtonBackgroundPressedProperty); }
            set { SetValue(AppBarButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonBackgroundPressedProperty =
            DependencyProperty.Register("AppBarButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonForeground
        {
            get { return GetValue(AppBarButtonForegroundProperty); }
            set { SetValue(AppBarButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonForegroundProperty =
            DependencyProperty.Register("AppBarButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonForegroundPointerOver
        {
            get { return GetValue(AppBarButtonForegroundPointerOverProperty); }
            set { SetValue(AppBarButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonForegroundPressed
        {
            get { return GetValue(AppBarButtonForegroundPressedProperty); }
            set { SetValue(AppBarButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonForegroundPressedProperty =
            DependencyProperty.Register("AppBarButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonForegroundDisabled
        {
            get { return GetValue(AppBarButtonForegroundDisabledProperty); }
            set { SetValue(AppBarButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonForegroundDisabledProperty =
            DependencyProperty.Register("AppBarButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundChecked
        {
            get { return GetValue(AppBarToggleButtonBackgroundCheckedProperty); }
            set { SetValue(AppBarToggleButtonBackgroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundCheckedProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundCheckedPointerOver
        {
            get { return GetValue(AppBarToggleButtonBackgroundCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonBackgroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundCheckedPressed
        {
            get { return GetValue(AppBarToggleButtonBackgroundCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonBackgroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundCheckedDisabled
        {
            get { return GetValue(AppBarToggleButtonBackgroundCheckedDisabledProperty); }
            set { SetValue(AppBarToggleButtonBackgroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundCheckedDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundHighLightOverlayPointerOver
        {
            get { return GetValue(AppBarToggleButtonBackgroundHighLightOverlayPointerOverProperty); }
            set { SetValue(AppBarToggleButtonBackgroundHighLightOverlayPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundHighLightOverlayPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundHighLightOverlayPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundHighLightOverlayPressed
        {
            get { return GetValue(AppBarToggleButtonBackgroundHighLightOverlayPressedProperty); }
            set { SetValue(AppBarToggleButtonBackgroundHighLightOverlayPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundHighLightOverlayPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundHighLightOverlayPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOver
        {
            get { return GetValue(AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundHighLightOverlayCheckedPressed
        {
            get { return GetValue(AppBarToggleButtonBackgroundHighLightOverlayCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonBackgroundHighLightOverlayCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundHighLightOverlayCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundHighLightOverlayCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForeground
        {
            get { return GetValue(AppBarToggleButtonForegroundProperty); }
            set { SetValue(AppBarToggleButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundProperty =
            DependencyProperty.Register("AppBarToggleButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundPointerOver
        {
            get { return GetValue(AppBarToggleButtonForegroundPointerOverProperty); }
            set { SetValue(AppBarToggleButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundPressed
        {
            get { return GetValue(AppBarToggleButtonForegroundPressedProperty); }
            set { SetValue(AppBarToggleButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundDisabled
        {
            get { return GetValue(AppBarToggleButtonForegroundDisabledProperty); }
            set { SetValue(AppBarToggleButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundChecked
        {
            get { return GetValue(AppBarToggleButtonForegroundCheckedProperty); }
            set { SetValue(AppBarToggleButtonForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundCheckedProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundCheckedPointerOver
        {
            get { return GetValue(AppBarToggleButtonForegroundCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundCheckedPressed
        {
            get { return GetValue(AppBarToggleButtonForegroundCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundCheckedDisabled
        {
            get { return GetValue(AppBarToggleButtonForegroundCheckedDisabledProperty); }
            set { SetValue(AppBarToggleButtonForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundCheckedDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForeground
        {
            get { return GetValue(AppBarToggleButtonCheckGlyphForegroundProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundPointerOver
        {
            get { return GetValue(AppBarToggleButtonCheckGlyphForegroundPointerOverProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundPressed
        {
            get { return GetValue(AppBarToggleButtonCheckGlyphForegroundPressedProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundDisabled
        {
            get { return GetValue(AppBarToggleButtonCheckGlyphForegroundDisabledProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundChecked
        {
            get { return GetValue(AppBarToggleButtonCheckGlyphForegroundCheckedProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundCheckedProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundCheckedPointerOver
        {
            get { return GetValue(AppBarToggleButtonCheckGlyphForegroundCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundCheckedPressed
        {
            get { return GetValue(AppBarToggleButtonCheckGlyphForegroundCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundCheckedDisabled
        {
            get { return GetValue(AppBarToggleButtonCheckGlyphForegroundCheckedDisabledProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundCheckedDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundPointerOver
        {
            get { return GetValue(AppBarToggleButtonOverflowLabelForegroundPointerOverProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundPressed
        {
            get { return GetValue(AppBarToggleButtonOverflowLabelForegroundPressedProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundDisabled
        {
            get { return GetValue(AppBarToggleButtonOverflowLabelForegroundDisabledProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundCheckedPointerOver
        {
            get { return GetValue(AppBarToggleButtonOverflowLabelForegroundCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundCheckedPressed
        {
            get { return GetValue(AppBarToggleButtonOverflowLabelForegroundCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundCheckedDisabled
        {
            get { return GetValue(AppBarToggleButtonOverflowLabelForegroundCheckedDisabledProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundCheckedDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarBackground
        {
            get { return GetValue(CommandBarBackgroundProperty); }
            set { SetValue(CommandBarBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CommandBarBackgroundProperty =
            DependencyProperty.Register("CommandBarBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarForeground
        {
            get { return GetValue(CommandBarForegroundProperty); }
            set { SetValue(CommandBarForegroundProperty, value); }
        }

        public static readonly DependencyProperty CommandBarForegroundProperty =
            DependencyProperty.Register("CommandBarForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarHighContrastBorder
        {
            get { return GetValue(CommandBarHighContrastBorderProperty); }
            set { SetValue(CommandBarHighContrastBorderProperty, value); }
        }

        public static readonly DependencyProperty CommandBarHighContrastBorderProperty =
            DependencyProperty.Register("CommandBarHighContrastBorder", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarEllipsisIconForegroundDisabled
        {
            get { return GetValue(CommandBarEllipsisIconForegroundDisabledProperty); }
            set { SetValue(CommandBarEllipsisIconForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CommandBarEllipsisIconForegroundDisabledProperty =
            DependencyProperty.Register("CommandBarEllipsisIconForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarOverflowPresenterBackground
        {
            get { return GetValue(CommandBarOverflowPresenterBackgroundProperty); }
            set { SetValue(CommandBarOverflowPresenterBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CommandBarOverflowPresenterBackgroundProperty =
            DependencyProperty.Register("CommandBarOverflowPresenterBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarOverflowPresenterBorderBrush
        {
            get { return GetValue(CommandBarOverflowPresenterBorderBrushProperty); }
            set { SetValue(CommandBarOverflowPresenterBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CommandBarOverflowPresenterBorderBrushProperty =
            DependencyProperty.Register("CommandBarOverflowPresenterBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarLightDismissOverlayBackground
        {
            get { return GetValue(CommandBarLightDismissOverlayBackgroundProperty); }
            set { SetValue(CommandBarLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CommandBarLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("CommandBarLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemBackgroundPointerOver
        {
            get { return GetValue(ListViewItemBackgroundPointerOverProperty); }
            set { SetValue(ListViewItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemBackgroundPointerOverProperty =
            DependencyProperty.Register("ListViewItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemBackgroundPressed
        {
            get { return GetValue(ListViewItemBackgroundPressedProperty); }
            set { SetValue(ListViewItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemBackgroundPressedProperty =
            DependencyProperty.Register("ListViewItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemBackgroundSelected
        {
            get { return GetValue(ListViewItemBackgroundSelectedProperty); }
            set { SetValue(ListViewItemBackgroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemBackgroundSelectedProperty =
            DependencyProperty.Register("ListViewItemBackgroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemBackgroundSelectedPointerOver
        {
            get { return GetValue(ListViewItemBackgroundSelectedPointerOverProperty); }
            set { SetValue(ListViewItemBackgroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemBackgroundSelectedPointerOverProperty =
            DependencyProperty.Register("ListViewItemBackgroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemBackgroundSelectedPressed
        {
            get { return GetValue(ListViewItemBackgroundSelectedPressedProperty); }
            set { SetValue(ListViewItemBackgroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemBackgroundSelectedPressedProperty =
            DependencyProperty.Register("ListViewItemBackgroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemForeground
        {
            get { return GetValue(ListViewItemForegroundProperty); }
            set { SetValue(ListViewItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemForegroundProperty =
            DependencyProperty.Register("ListViewItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemForegroundPointerOver
        {
            get { return GetValue(ListViewItemForegroundPointerOverProperty); }
            set { SetValue(ListViewItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemForegroundPointerOverProperty =
            DependencyProperty.Register("ListViewItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemForegroundSelected
        {
            get { return GetValue(ListViewItemForegroundSelectedProperty); }
            set { SetValue(ListViewItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemForegroundSelectedProperty =
            DependencyProperty.Register("ListViewItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemFocusVisualPrimaryBrush
        {
            get { return GetValue(ListViewItemFocusVisualPrimaryBrushProperty); }
            set { SetValue(ListViewItemFocusVisualPrimaryBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemFocusVisualPrimaryBrushProperty =
            DependencyProperty.Register("ListViewItemFocusVisualPrimaryBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemFocusVisualSecondaryBrush
        {
            get { return GetValue(ListViewItemFocusVisualSecondaryBrushProperty); }
            set { SetValue(ListViewItemFocusVisualSecondaryBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemFocusVisualSecondaryBrushProperty =
            DependencyProperty.Register("ListViewItemFocusVisualSecondaryBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemFocusBorderBrush
        {
            get { return GetValue(ListViewItemFocusBorderBrushProperty); }
            set { SetValue(ListViewItemFocusBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemFocusBorderBrushProperty =
            DependencyProperty.Register("ListViewItemFocusBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemFocusSecondaryBorderBrush
        {
            get { return GetValue(ListViewItemFocusSecondaryBorderBrushProperty); }
            set { SetValue(ListViewItemFocusSecondaryBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemFocusSecondaryBorderBrushProperty =
            DependencyProperty.Register("ListViewItemFocusSecondaryBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemCheckBrush
        {
            get { return GetValue(ListViewItemCheckBrushProperty); }
            set { SetValue(ListViewItemCheckBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemCheckBrushProperty =
            DependencyProperty.Register("ListViewItemCheckBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemCheckBoxBrush
        {
            get { return GetValue(ListViewItemCheckBoxBrushProperty); }
            set { SetValue(ListViewItemCheckBoxBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemCheckBoxBrushProperty =
            DependencyProperty.Register("ListViewItemCheckBoxBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemDragForeground
        {
            get { return GetValue(ListViewItemDragForegroundProperty); }
            set { SetValue(ListViewItemDragForegroundProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemDragForegroundProperty =
            DependencyProperty.Register("ListViewItemDragForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemPlaceholderBackground
        {
            get { return GetValue(ListViewItemPlaceholderBackgroundProperty); }
            set { SetValue(ListViewItemPlaceholderBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemPlaceholderBackgroundProperty =
            DependencyProperty.Register("ListViewItemPlaceholderBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AutoSuggestBoxSuggestionsListBackground
        {
            get { return GetValue(AutoSuggestBoxSuggestionsListBackgroundProperty); }
            set { SetValue(AutoSuggestBoxSuggestionsListBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AutoSuggestBoxSuggestionsListBackgroundProperty =
            DependencyProperty.Register("AutoSuggestBoxSuggestionsListBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AutoSuggestBoxSuggestionsListBorderBrush
        {
            get { return GetValue(AutoSuggestBoxSuggestionsListBorderBrushProperty); }
            set { SetValue(AutoSuggestBoxSuggestionsListBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty AutoSuggestBoxSuggestionsListBorderBrushProperty =
            DependencyProperty.Register("AutoSuggestBoxSuggestionsListBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AutoSuggestBoxLightDismissOverlayBackground
        {
            get { return GetValue(AutoSuggestBoxLightDismissOverlayBackgroundProperty); }
            set { SetValue(AutoSuggestBoxLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AutoSuggestBoxLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("AutoSuggestBoxLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemBackgroundSelectedDisabled
        {
            get { return GetValue(TreeViewItemBackgroundSelectedDisabledProperty); }
            set { SetValue(TreeViewItemBackgroundSelectedDisabledProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemBackgroundSelectedDisabledProperty =
            DependencyProperty.Register("TreeViewItemBackgroundSelectedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForeground
        {
            get { return GetValue(TreeViewItemForegroundProperty); }
            set { SetValue(TreeViewItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundProperty =
            DependencyProperty.Register("TreeViewItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundPointerOver
        {
            get { return GetValue(TreeViewItemForegroundPointerOverProperty); }
            set { SetValue(TreeViewItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundPointerOverProperty =
            DependencyProperty.Register("TreeViewItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundPressed
        {
            get { return GetValue(TreeViewItemForegroundPressedProperty); }
            set { SetValue(TreeViewItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundPressedProperty =
            DependencyProperty.Register("TreeViewItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundDisabled
        {
            get { return GetValue(TreeViewItemForegroundDisabledProperty); }
            set { SetValue(TreeViewItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundDisabledProperty =
            DependencyProperty.Register("TreeViewItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundSelected
        {
            get { return GetValue(TreeViewItemForegroundSelectedProperty); }
            set { SetValue(TreeViewItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundSelectedProperty =
            DependencyProperty.Register("TreeViewItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundSelectedPointerOver
        {
            get { return GetValue(TreeViewItemForegroundSelectedPointerOverProperty); }
            set { SetValue(TreeViewItemForegroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundSelectedPointerOverProperty =
            DependencyProperty.Register("TreeViewItemForegroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundSelectedPressed
        {
            get { return GetValue(TreeViewItemForegroundSelectedPressedProperty); }
            set { SetValue(TreeViewItemForegroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundSelectedPressedProperty =
            DependencyProperty.Register("TreeViewItemForegroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundSelectedDisabled
        {
            get { return GetValue(TreeViewItemForegroundSelectedDisabledProperty); }
            set { SetValue(TreeViewItemForegroundSelectedDisabledProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundSelectedDisabledProperty =
            DependencyProperty.Register("TreeViewItemForegroundSelectedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemCheckBoxBorderSelected
        {
            get { return GetValue(TreeViewItemCheckBoxBorderSelectedProperty); }
            set { SetValue(TreeViewItemCheckBoxBorderSelectedProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemCheckBoxBorderSelectedProperty =
            DependencyProperty.Register("TreeViewItemCheckBoxBorderSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemCheckGlyphSelected
        {
            get { return GetValue(TreeViewItemCheckGlyphSelectedProperty); }
            set { SetValue(TreeViewItemCheckGlyphSelectedProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemCheckGlyphSelectedProperty =
            DependencyProperty.Register("TreeViewItemCheckGlyphSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemBackground
        {
            get { return GetValue(SwipeItemBackgroundProperty); }
            set { SetValue(SwipeItemBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemBackgroundProperty =
            DependencyProperty.Register("SwipeItemBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemForeground
        {
            get { return GetValue(SwipeItemForegroundProperty); }
            set { SetValue(SwipeItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemForegroundProperty =
            DependencyProperty.Register("SwipeItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemBackgroundPressed
        {
            get { return GetValue(SwipeItemBackgroundPressedProperty); }
            set { SetValue(SwipeItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemBackgroundPressedProperty =
            DependencyProperty.Register("SwipeItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemPreThresholdExecuteForeground
        {
            get { return GetValue(SwipeItemPreThresholdExecuteForegroundProperty); }
            set { SetValue(SwipeItemPreThresholdExecuteForegroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemPreThresholdExecuteForegroundProperty =
            DependencyProperty.Register("SwipeItemPreThresholdExecuteForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemPreThresholdExecuteBackground
        {
            get { return GetValue(SwipeItemPreThresholdExecuteBackgroundProperty); }
            set { SetValue(SwipeItemPreThresholdExecuteBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemPreThresholdExecuteBackgroundProperty =
            DependencyProperty.Register("SwipeItemPreThresholdExecuteBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemPostThresholdExecuteForeground
        {
            get { return GetValue(SwipeItemPostThresholdExecuteForegroundProperty); }
            set { SetValue(SwipeItemPostThresholdExecuteForegroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemPostThresholdExecuteForegroundProperty =
            DependencyProperty.Register("SwipeItemPostThresholdExecuteForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemPostThresholdExecuteBackground
        {
            get { return GetValue(SwipeItemPostThresholdExecuteBackgroundProperty); }
            set { SetValue(SwipeItemPostThresholdExecuteBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemPostThresholdExecuteBackgroundProperty =
            DependencyProperty.Register("SwipeItemPostThresholdExecuteBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackground
        {
            get { return GetValue(SplitButtonBackgroundProperty); }
            set { SetValue(SplitButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundProperty =
            DependencyProperty.Register("SplitButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundPointerOver
        {
            get { return GetValue(SplitButtonBackgroundPointerOverProperty); }
            set { SetValue(SplitButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("SplitButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundPressed
        {
            get { return GetValue(SplitButtonBackgroundPressedProperty); }
            set { SetValue(SplitButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundPressedProperty =
            DependencyProperty.Register("SplitButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundDisabled
        {
            get { return GetValue(SplitButtonBackgroundDisabledProperty); }
            set { SetValue(SplitButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundDisabledProperty =
            DependencyProperty.Register("SplitButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundChecked
        {
            get { return GetValue(SplitButtonBackgroundCheckedProperty); }
            set { SetValue(SplitButtonBackgroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundCheckedProperty =
            DependencyProperty.Register("SplitButtonBackgroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundCheckedPointerOver
        {
            get { return GetValue(SplitButtonBackgroundCheckedPointerOverProperty); }
            set { SetValue(SplitButtonBackgroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundCheckedPointerOverProperty =
            DependencyProperty.Register("SplitButtonBackgroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundCheckedPressed
        {
            get { return GetValue(SplitButtonBackgroundCheckedPressedProperty); }
            set { SetValue(SplitButtonBackgroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundCheckedPressedProperty =
            DependencyProperty.Register("SplitButtonBackgroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundCheckedDisabled
        {
            get { return GetValue(SplitButtonBackgroundCheckedDisabledProperty); }
            set { SetValue(SplitButtonBackgroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundCheckedDisabledProperty =
            DependencyProperty.Register("SplitButtonBackgroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForeground
        {
            get { return GetValue(SplitButtonForegroundProperty); }
            set { SetValue(SplitButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundProperty =
            DependencyProperty.Register("SplitButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundPointerOver
        {
            get { return GetValue(SplitButtonForegroundPointerOverProperty); }
            set { SetValue(SplitButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundPointerOverProperty =
            DependencyProperty.Register("SplitButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundPressed
        {
            get { return GetValue(SplitButtonForegroundPressedProperty); }
            set { SetValue(SplitButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundPressedProperty =
            DependencyProperty.Register("SplitButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundDisabled
        {
            get { return GetValue(SplitButtonForegroundDisabledProperty); }
            set { SetValue(SplitButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundDisabledProperty =
            DependencyProperty.Register("SplitButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundChecked
        {
            get { return GetValue(SplitButtonForegroundCheckedProperty); }
            set { SetValue(SplitButtonForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundCheckedProperty =
            DependencyProperty.Register("SplitButtonForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundCheckedPointerOver
        {
            get { return GetValue(SplitButtonForegroundCheckedPointerOverProperty); }
            set { SetValue(SplitButtonForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("SplitButtonForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundCheckedPressed
        {
            get { return GetValue(SplitButtonForegroundCheckedPressedProperty); }
            set { SetValue(SplitButtonForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundCheckedPressedProperty =
            DependencyProperty.Register("SplitButtonForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundCheckedDisabled
        {
            get { return GetValue(SplitButtonForegroundCheckedDisabledProperty); }
            set { SetValue(SplitButtonForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundCheckedDisabledProperty =
            DependencyProperty.Register("SplitButtonForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrush
        {
            get { return GetValue(SplitButtonBorderBrushProperty); }
            set { SetValue(SplitButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushProperty =
            DependencyProperty.Register("SplitButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushPointerOver
        {
            get { return GetValue(SplitButtonBorderBrushPointerOverProperty); }
            set { SetValue(SplitButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("SplitButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushPressed
        {
            get { return GetValue(SplitButtonBorderBrushPressedProperty); }
            set { SetValue(SplitButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushPressedProperty =
            DependencyProperty.Register("SplitButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushDisabled
        {
            get { return GetValue(SplitButtonBorderBrushDisabledProperty); }
            set { SetValue(SplitButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("SplitButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushChecked
        {
            get { return GetValue(SplitButtonBorderBrushCheckedProperty); }
            set { SetValue(SplitButtonBorderBrushCheckedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushCheckedProperty =
            DependencyProperty.Register("SplitButtonBorderBrushChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushCheckedPointerOver
        {
            get { return GetValue(SplitButtonBorderBrushCheckedPointerOverProperty); }
            set { SetValue(SplitButtonBorderBrushCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushCheckedPointerOverProperty =
            DependencyProperty.Register("SplitButtonBorderBrushCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushCheckedPressed
        {
            get { return GetValue(SplitButtonBorderBrushCheckedPressedProperty); }
            set { SetValue(SplitButtonBorderBrushCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushCheckedPressedProperty =
            DependencyProperty.Register("SplitButtonBorderBrushCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushCheckedDisabled
        {
            get { return GetValue(SplitButtonBorderBrushCheckedDisabledProperty); }
            set { SetValue(SplitButtonBorderBrushCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushCheckedDisabledProperty =
            DependencyProperty.Register("SplitButtonBorderBrushCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemKeyboardAcceleratorTextForeground
        {
            get { return GetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundProperty); }
            set { SetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemKeyboardAcceleratorTextForegroundProperty =
            DependencyProperty.Register("MenuFlyoutItemKeyboardAcceleratorTextForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver
        {
            get { return GetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty); }
            set { SetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemKeyboardAcceleratorTextForegroundPressed
        {
            get { return GetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty); }
            set { SetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty =
            DependencyProperty.Register("MenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled
        {
            get { return GetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty); }
            set { SetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty =
            DependencyProperty.Register("MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonKeyboardAcceleratorTextForeground
        {
            get { return GetValue(AppBarButtonKeyboardAcceleratorTextForegroundProperty); }
            set { SetValue(AppBarButtonKeyboardAcceleratorTextForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonKeyboardAcceleratorTextForegroundProperty =
            DependencyProperty.Register("AppBarButtonKeyboardAcceleratorTextForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonKeyboardAcceleratorTextForegroundPointerOver
        {
            get { return GetValue(AppBarButtonKeyboardAcceleratorTextForegroundPointerOverProperty); }
            set { SetValue(AppBarButtonKeyboardAcceleratorTextForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonKeyboardAcceleratorTextForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarButtonKeyboardAcceleratorTextForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonKeyboardAcceleratorTextForegroundPressed
        {
            get { return GetValue(AppBarButtonKeyboardAcceleratorTextForegroundPressedProperty); }
            set { SetValue(AppBarButtonKeyboardAcceleratorTextForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonKeyboardAcceleratorTextForegroundPressedProperty =
            DependencyProperty.Register("AppBarButtonKeyboardAcceleratorTextForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonKeyboardAcceleratorTextForegroundDisabled
        {
            get { return GetValue(AppBarButtonKeyboardAcceleratorTextForegroundDisabledProperty); }
            set { SetValue(AppBarButtonKeyboardAcceleratorTextForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonKeyboardAcceleratorTextForegroundDisabledProperty =
            DependencyProperty.Register("AppBarButtonKeyboardAcceleratorTextForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForeground
        {
            get { return GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOver
        {
            get { return GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOverProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundPressed
        {
            get { return GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundPressedProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabled
        {
            get { return GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabledProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundChecked
        {
            get { return GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOver
        {
            get { return GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressed
        {
            get { return GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabled
        {
            get { return GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabledProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonBackgroundSubMenuOpened
        {
            get { return GetValue(AppBarButtonBackgroundSubMenuOpenedProperty); }
            set { SetValue(AppBarButtonBackgroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonBackgroundSubMenuOpenedProperty =
            DependencyProperty.Register("AppBarButtonBackgroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonForegroundSubMenuOpened
        {
            get { return GetValue(AppBarButtonForegroundSubMenuOpenedProperty); }
            set { SetValue(AppBarButtonForegroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonForegroundSubMenuOpenedProperty =
            DependencyProperty.Register("AppBarButtonForegroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpened
        {
            get { return GetValue(AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpenedProperty); }
            set { SetValue(AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpenedProperty =
            DependencyProperty.Register("AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonSubItemChevronForeground
        {
            get { return GetValue(AppBarButtonSubItemChevronForegroundProperty); }
            set { SetValue(AppBarButtonSubItemChevronForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonSubItemChevronForegroundProperty =
            DependencyProperty.Register("AppBarButtonSubItemChevronForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonSubItemChevronForegroundPointerOver
        {
            get { return GetValue(AppBarButtonSubItemChevronForegroundPointerOverProperty); }
            set { SetValue(AppBarButtonSubItemChevronForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonSubItemChevronForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarButtonSubItemChevronForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonSubItemChevronForegroundPressed
        {
            get { return GetValue(AppBarButtonSubItemChevronForegroundPressedProperty); }
            set { SetValue(AppBarButtonSubItemChevronForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonSubItemChevronForegroundPressedProperty =
            DependencyProperty.Register("AppBarButtonSubItemChevronForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonSubItemChevronForegroundSubMenuOpened
        {
            get { return GetValue(AppBarButtonSubItemChevronForegroundSubMenuOpenedProperty); }
            set { SetValue(AppBarButtonSubItemChevronForegroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonSubItemChevronForegroundSubMenuOpenedProperty =
            DependencyProperty.Register("AppBarButtonSubItemChevronForegroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonSubItemChevronForegroundDisabled
        {
            get { return GetValue(AppBarButtonSubItemChevronForegroundDisabledProperty); }
            set { SetValue(AppBarButtonSubItemChevronForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonSubItemChevronForegroundDisabledProperty =
            DependencyProperty.Register("AppBarButtonSubItemChevronForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));

    }
}
