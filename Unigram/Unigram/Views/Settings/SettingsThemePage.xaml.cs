using System;
using System.Collections.Generic;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Services.Settings;
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
            this.InitializeComponent();

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedRight"))
            {
                MenuFlyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
            }
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
            if (theme.Parent.HasFlag(TelegramTheme.Light))
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
            var dialog = new InputDialog();
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
            if (check != null && check.Tag is ThemeBooleanPart boolean)
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
            RaisePropertyChanged(() => IsDefault);
            RaisePropertyChanged(() => HexValue);
        }

        private void SetSuper(Color super)
        {
            _super = super;
            RaisePropertyChanged(() => IsDefault);
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
            get { return (object)GetValue(PageHeaderDisabledBrushProperty); }
            set { SetValue(PageHeaderDisabledBrushProperty, value); }
        }

        public static readonly DependencyProperty PageHeaderDisabledBrushProperty =
            DependencyProperty.Register("PageHeaderDisabledBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PageHeaderBackgroundBrush
        {
            get { return (object)GetValue(PageHeaderBackgroundBrushProperty); }
            set { SetValue(PageHeaderBackgroundBrushProperty, value); }
        }

        public static readonly DependencyProperty PageHeaderBackgroundBrushProperty =
            DependencyProperty.Register("PageHeaderBackgroundBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PageSubHeaderBackgroundBrush
        {
            get { return (object)GetValue(PageSubHeaderBackgroundBrushProperty); }
            set { SetValue(PageSubHeaderBackgroundBrushProperty, value); }
        }

        public static readonly DependencyProperty PageSubHeaderBackgroundBrushProperty =
            DependencyProperty.Register("PageSubHeaderBackgroundBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TelegramSeparatorMediumBrush
        {
            get { return (object)GetValue(TelegramSeparatorMediumBrushProperty); }
            set { SetValue(TelegramSeparatorMediumBrushProperty, value); }
        }

        public static readonly DependencyProperty TelegramSeparatorMediumBrushProperty =
            DependencyProperty.Register("TelegramSeparatorMediumBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageBackgroundOutColor
        {
            get { return (object)GetValue(MessageBackgroundOutColorProperty); }
            set { SetValue(MessageBackgroundOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageBackgroundOutColorProperty =
            DependencyProperty.Register("MessageBackgroundOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageSubtleLabelOutColor
        {
            get { return (object)GetValue(MessageSubtleLabelOutColorProperty); }
            set { SetValue(MessageSubtleLabelOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageSubtleLabelOutColorProperty =
            DependencyProperty.Register("MessageSubtleLabelOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageSubtleGlyphOutColor
        {
            get { return (object)GetValue(MessageSubtleGlyphOutColorProperty); }
            set { SetValue(MessageSubtleGlyphOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageSubtleGlyphOutColorProperty =
            DependencyProperty.Register("MessageSubtleGlyphOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageSubtleForegroundColor
        {
            get { return (object)GetValue(MessageSubtleForegroundColorProperty); }
            set { SetValue(MessageSubtleForegroundColorProperty, value); }
        }

        public static readonly DependencyProperty MessageSubtleForegroundColorProperty =
            DependencyProperty.Register("MessageSubtleForegroundColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageSubtleForegroundOutColor
        {
            get { return (object)GetValue(MessageSubtleForegroundOutColorProperty); }
            set { SetValue(MessageSubtleForegroundOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageSubtleForegroundOutColorProperty =
            DependencyProperty.Register("MessageSubtleForegroundOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageHeaderForegroundOutColor
        {
            get { return (object)GetValue(MessageHeaderForegroundOutColorProperty); }
            set { SetValue(MessageHeaderForegroundOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageHeaderForegroundOutColorProperty =
            DependencyProperty.Register("MessageHeaderForegroundOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageHeaderBorderOutColor
        {
            get { return (object)GetValue(MessageHeaderBorderOutColorProperty); }
            set { SetValue(MessageHeaderBorderOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageHeaderBorderOutColorProperty =
            DependencyProperty.Register("MessageHeaderBorderOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageMediaForegroundOutColor
        {
            get { return (object)GetValue(MessageMediaForegroundOutColorProperty); }
            set { SetValue(MessageMediaForegroundOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageMediaForegroundOutColorProperty =
            DependencyProperty.Register("MessageMediaForegroundOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MessageMediaBackgroundOutColor
        {
            get { return (object)GetValue(MessageMediaBackgroundOutColorProperty); }
            set { SetValue(MessageMediaBackgroundOutColorProperty, value); }
        }

        public static readonly DependencyProperty MessageMediaBackgroundOutColorProperty =
            DependencyProperty.Register("MessageMediaBackgroundOutColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SystemControlDescriptionTextForegroundBrush
        {
            get { return (object)GetValue(SystemControlDescriptionTextForegroundBrushProperty); }
            set { SetValue(SystemControlDescriptionTextForegroundBrushProperty, value); }
        }

        public static readonly DependencyProperty SystemControlDescriptionTextForegroundBrushProperty =
            DependencyProperty.Register("SystemControlDescriptionTextForegroundBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderThumbBackground
        {
            get { return (object)GetValue(SliderThumbBackgroundProperty); }
            set { SetValue(SliderThumbBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SliderThumbBackgroundProperty =
            DependencyProperty.Register("SliderThumbBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderThumbBackgroundPointerOver
        {
            get { return (object)GetValue(SliderThumbBackgroundPointerOverProperty); }
            set { SetValue(SliderThumbBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SliderThumbBackgroundPointerOverProperty =
            DependencyProperty.Register("SliderThumbBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderThumbBackgroundPressed
        {
            get { return (object)GetValue(SliderThumbBackgroundPressedProperty); }
            set { SetValue(SliderThumbBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty SliderThumbBackgroundPressedProperty =
            DependencyProperty.Register("SliderThumbBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderThumbBackgroundDisabled
        {
            get { return (object)GetValue(SliderThumbBackgroundDisabledProperty); }
            set { SetValue(SliderThumbBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty SliderThumbBackgroundDisabledProperty =
            DependencyProperty.Register("SliderThumbBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackFill
        {
            get { return (object)GetValue(SliderTrackFillProperty); }
            set { SetValue(SliderTrackFillProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackFillProperty =
            DependencyProperty.Register("SliderTrackFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackFillPointerOver
        {
            get { return (object)GetValue(SliderTrackFillPointerOverProperty); }
            set { SetValue(SliderTrackFillPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackFillPointerOverProperty =
            DependencyProperty.Register("SliderTrackFillPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackFillPressed
        {
            get { return (object)GetValue(SliderTrackFillPressedProperty); }
            set { SetValue(SliderTrackFillPressedProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackFillPressedProperty =
            DependencyProperty.Register("SliderTrackFillPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackFillDisabled
        {
            get { return (object)GetValue(SliderTrackFillDisabledProperty); }
            set { SetValue(SliderTrackFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackFillDisabledProperty =
            DependencyProperty.Register("SliderTrackFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackValueFill
        {
            get { return (object)GetValue(SliderTrackValueFillProperty); }
            set { SetValue(SliderTrackValueFillProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackValueFillProperty =
            DependencyProperty.Register("SliderTrackValueFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackValueFillPointerOver
        {
            get { return (object)GetValue(SliderTrackValueFillPointerOverProperty); }
            set { SetValue(SliderTrackValueFillPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackValueFillPointerOverProperty =
            DependencyProperty.Register("SliderTrackValueFillPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackValueFillPressed
        {
            get { return (object)GetValue(SliderTrackValueFillPressedProperty); }
            set { SetValue(SliderTrackValueFillPressedProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackValueFillPressedProperty =
            DependencyProperty.Register("SliderTrackValueFillPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTrackValueFillDisabled
        {
            get { return (object)GetValue(SliderTrackValueFillDisabledProperty); }
            set { SetValue(SliderTrackValueFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty SliderTrackValueFillDisabledProperty =
            DependencyProperty.Register("SliderTrackValueFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderHeaderForeground
        {
            get { return (object)GetValue(SliderHeaderForegroundProperty); }
            set { SetValue(SliderHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty SliderHeaderForegroundProperty =
            DependencyProperty.Register("SliderHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderHeaderForegroundDisabled
        {
            get { return (object)GetValue(SliderHeaderForegroundDisabledProperty); }
            set { SetValue(SliderHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty SliderHeaderForegroundDisabledProperty =
            DependencyProperty.Register("SliderHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTickBarFill
        {
            get { return (object)GetValue(SliderTickBarFillProperty); }
            set { SetValue(SliderTickBarFillProperty, value); }
        }

        public static readonly DependencyProperty SliderTickBarFillProperty =
            DependencyProperty.Register("SliderTickBarFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderTickBarFillDisabled
        {
            get { return (object)GetValue(SliderTickBarFillDisabledProperty); }
            set { SetValue(SliderTickBarFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty SliderTickBarFillDisabledProperty =
            DependencyProperty.Register("SliderTickBarFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SliderInlineTickBarFill
        {
            get { return (object)GetValue(SliderInlineTickBarFillProperty); }
            set { SetValue(SliderInlineTickBarFillProperty, value); }
        }

        public static readonly DependencyProperty SliderInlineTickBarFillProperty =
            DependencyProperty.Register("SliderInlineTickBarFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBackground
        {
            get { return (object)GetValue(ButtonBackgroundProperty); }
            set { SetValue(ButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ButtonBackgroundProperty =
            DependencyProperty.Register("ButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBackgroundPointerOver
        {
            get { return (object)GetValue(ButtonBackgroundPointerOverProperty); }
            set { SetValue(ButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("ButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBackgroundPressed
        {
            get { return (object)GetValue(ButtonBackgroundPressedProperty); }
            set { SetValue(ButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ButtonBackgroundPressedProperty =
            DependencyProperty.Register("ButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBackgroundDisabled
        {
            get { return (object)GetValue(ButtonBackgroundDisabledProperty); }
            set { SetValue(ButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ButtonBackgroundDisabledProperty =
            DependencyProperty.Register("ButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonForeground
        {
            get { return (object)GetValue(ButtonForegroundProperty); }
            set { SetValue(ButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty ButtonForegroundProperty =
            DependencyProperty.Register("ButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonForegroundPointerOver
        {
            get { return (object)GetValue(ButtonForegroundPointerOverProperty); }
            set { SetValue(ButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ButtonForegroundPointerOverProperty =
            DependencyProperty.Register("ButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonForegroundPressed
        {
            get { return (object)GetValue(ButtonForegroundPressedProperty); }
            set { SetValue(ButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ButtonForegroundPressedProperty =
            DependencyProperty.Register("ButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonForegroundDisabled
        {
            get { return (object)GetValue(ButtonForegroundDisabledProperty); }
            set { SetValue(ButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ButtonForegroundDisabledProperty =
            DependencyProperty.Register("ButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBorderBrush
        {
            get { return (object)GetValue(ButtonBorderBrushProperty); }
            set { SetValue(ButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ButtonBorderBrushProperty =
            DependencyProperty.Register("ButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(ButtonBorderBrushPointerOverProperty); }
            set { SetValue(ButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("ButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBorderBrushPressed
        {
            get { return (object)GetValue(ButtonBorderBrushPressedProperty); }
            set { SetValue(ButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty ButtonBorderBrushPressedProperty =
            DependencyProperty.Register("ButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ButtonBorderBrushDisabled
        {
            get { return (object)GetValue(ButtonBorderBrushDisabledProperty); }
            set { SetValue(ButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty ButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("ButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonForeground
        {
            get { return (object)GetValue(RadioButtonForegroundProperty); }
            set { SetValue(RadioButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonForegroundProperty =
            DependencyProperty.Register("RadioButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonForegroundPointerOver
        {
            get { return (object)GetValue(RadioButtonForegroundPointerOverProperty); }
            set { SetValue(RadioButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonForegroundPointerOverProperty =
            DependencyProperty.Register("RadioButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonForegroundPressed
        {
            get { return (object)GetValue(RadioButtonForegroundPressedProperty); }
            set { SetValue(RadioButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonForegroundPressedProperty =
            DependencyProperty.Register("RadioButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonForegroundDisabled
        {
            get { return (object)GetValue(RadioButtonForegroundDisabledProperty); }
            set { SetValue(RadioButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonForegroundDisabledProperty =
            DependencyProperty.Register("RadioButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseStroke
        {
            get { return (object)GetValue(RadioButtonOuterEllipseStrokeProperty); }
            set { SetValue(RadioButtonOuterEllipseStrokeProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseStrokeProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseStroke", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseStrokePointerOver
        {
            get { return (object)GetValue(RadioButtonOuterEllipseStrokePointerOverProperty); }
            set { SetValue(RadioButtonOuterEllipseStrokePointerOverProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseStrokePointerOverProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseStrokePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseStrokePressed
        {
            get { return (object)GetValue(RadioButtonOuterEllipseStrokePressedProperty); }
            set { SetValue(RadioButtonOuterEllipseStrokePressedProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseStrokePressedProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseStrokePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseStrokeDisabled
        {
            get { return (object)GetValue(RadioButtonOuterEllipseStrokeDisabledProperty); }
            set { SetValue(RadioButtonOuterEllipseStrokeDisabledProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseStrokeDisabledProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseStrokeDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedStroke
        {
            get { return (object)GetValue(RadioButtonOuterEllipseCheckedStrokeProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedStrokeProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedStrokeProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedStroke", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedStrokePointerOver
        {
            get { return (object)GetValue(RadioButtonOuterEllipseCheckedStrokePointerOverProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedStrokePointerOverProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedStrokePointerOverProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedStrokePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedStrokePressed
        {
            get { return (object)GetValue(RadioButtonOuterEllipseCheckedStrokePressedProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedStrokePressedProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedStrokePressedProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedStrokePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedStrokeDisabled
        {
            get { return (object)GetValue(RadioButtonOuterEllipseCheckedStrokeDisabledProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedStrokeDisabledProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedStrokeDisabledProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedStrokeDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedFill
        {
            get { return (object)GetValue(RadioButtonOuterEllipseCheckedFillProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedFillProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedFillProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedFillPointerOver
        {
            get { return (object)GetValue(RadioButtonOuterEllipseCheckedFillPointerOverProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedFillPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedFillPointerOverProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedFillPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonOuterEllipseCheckedFillPressed
        {
            get { return (object)GetValue(RadioButtonOuterEllipseCheckedFillPressedProperty); }
            set { SetValue(RadioButtonOuterEllipseCheckedFillPressedProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonOuterEllipseCheckedFillPressedProperty =
            DependencyProperty.Register("RadioButtonOuterEllipseCheckedFillPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonCheckGlyphFill
        {
            get { return (object)GetValue(RadioButtonCheckGlyphFillProperty); }
            set { SetValue(RadioButtonCheckGlyphFillProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonCheckGlyphFillProperty =
            DependencyProperty.Register("RadioButtonCheckGlyphFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonCheckGlyphFillPointerOver
        {
            get { return (object)GetValue(RadioButtonCheckGlyphFillPointerOverProperty); }
            set { SetValue(RadioButtonCheckGlyphFillPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonCheckGlyphFillPointerOverProperty =
            DependencyProperty.Register("RadioButtonCheckGlyphFillPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonCheckGlyphFillPressed
        {
            get { return (object)GetValue(RadioButtonCheckGlyphFillPressedProperty); }
            set { SetValue(RadioButtonCheckGlyphFillPressedProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonCheckGlyphFillPressedProperty =
            DependencyProperty.Register("RadioButtonCheckGlyphFillPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RadioButtonCheckGlyphFillDisabled
        {
            get { return (object)GetValue(RadioButtonCheckGlyphFillDisabledProperty); }
            set { SetValue(RadioButtonCheckGlyphFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty RadioButtonCheckGlyphFillDisabledProperty =
            DependencyProperty.Register("RadioButtonCheckGlyphFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundUnchecked
        {
            get { return (object)GetValue(CheckBoxForegroundUncheckedProperty); }
            set { SetValue(CheckBoxForegroundUncheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundUncheckedProperty =
            DependencyProperty.Register("CheckBoxForegroundUnchecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundUncheckedPointerOver
        {
            get { return (object)GetValue(CheckBoxForegroundUncheckedPointerOverProperty); }
            set { SetValue(CheckBoxForegroundUncheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundUncheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxForegroundUncheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundUncheckedPressed
        {
            get { return (object)GetValue(CheckBoxForegroundUncheckedPressedProperty); }
            set { SetValue(CheckBoxForegroundUncheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundUncheckedPressedProperty =
            DependencyProperty.Register("CheckBoxForegroundUncheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundUncheckedDisabled
        {
            get { return (object)GetValue(CheckBoxForegroundUncheckedDisabledProperty); }
            set { SetValue(CheckBoxForegroundUncheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundUncheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxForegroundUncheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundChecked
        {
            get { return (object)GetValue(CheckBoxForegroundCheckedProperty); }
            set { SetValue(CheckBoxForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundCheckedProperty =
            DependencyProperty.Register("CheckBoxForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundCheckedPointerOver
        {
            get { return (object)GetValue(CheckBoxForegroundCheckedPointerOverProperty); }
            set { SetValue(CheckBoxForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundCheckedPressed
        {
            get { return (object)GetValue(CheckBoxForegroundCheckedPressedProperty); }
            set { SetValue(CheckBoxForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundCheckedPressedProperty =
            DependencyProperty.Register("CheckBoxForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundCheckedDisabled
        {
            get { return (object)GetValue(CheckBoxForegroundCheckedDisabledProperty); }
            set { SetValue(CheckBoxForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundCheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundIndeterminate
        {
            get { return (object)GetValue(CheckBoxForegroundIndeterminateProperty); }
            set { SetValue(CheckBoxForegroundIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundIndeterminateProperty =
            DependencyProperty.Register("CheckBoxForegroundIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundIndeterminatePointerOver
        {
            get { return (object)GetValue(CheckBoxForegroundIndeterminatePointerOverProperty); }
            set { SetValue(CheckBoxForegroundIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundIndeterminatePointerOverProperty =
            DependencyProperty.Register("CheckBoxForegroundIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundIndeterminatePressed
        {
            get { return (object)GetValue(CheckBoxForegroundIndeterminatePressedProperty); }
            set { SetValue(CheckBoxForegroundIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundIndeterminatePressedProperty =
            DependencyProperty.Register("CheckBoxForegroundIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxForegroundIndeterminateDisabled
        {
            get { return (object)GetValue(CheckBoxForegroundIndeterminateDisabledProperty); }
            set { SetValue(CheckBoxForegroundIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxForegroundIndeterminateDisabledProperty =
            DependencyProperty.Register("CheckBoxForegroundIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeUnchecked
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeUncheckedProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeUncheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeUncheckedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeUnchecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeUncheckedPointerOver
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeUncheckedPointerOverProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeUncheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeUncheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeUncheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeUncheckedPressed
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeUncheckedPressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeUncheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeUncheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeUncheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeUncheckedDisabled
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeUncheckedDisabledProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeUncheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeUncheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeUncheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeChecked
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeCheckedProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeCheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeCheckedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeCheckedPointerOver
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeCheckedPointerOverProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeCheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeCheckedPressed
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeCheckedPressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeCheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeCheckedDisabled
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeCheckedDisabledProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeCheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeIndeterminate
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeIndeterminateProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeIndeterminateProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeIndeterminatePointerOver
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeIndeterminatePointerOverProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeIndeterminatePointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeIndeterminatePressed
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeIndeterminatePressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeIndeterminatePressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundStrokeIndeterminateDisabled
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundStrokeIndeterminateDisabledProperty); }
            set { SetValue(CheckBoxCheckBackgroundStrokeIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundStrokeIndeterminateDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundStrokeIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillUncheckedPressed
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundFillUncheckedPressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillUncheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillUncheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillUncheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillChecked
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundFillCheckedProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillCheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillCheckedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillCheckedPointerOver
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundFillCheckedPointerOverProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillCheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillCheckedPressed
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundFillCheckedPressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillCheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillIndeterminate
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundFillIndeterminateProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillIndeterminateProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillIndeterminatePointerOver
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundFillIndeterminatePointerOverProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillIndeterminatePointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckBackgroundFillIndeterminatePressed
        {
            get { return (object)GetValue(CheckBoxCheckBackgroundFillIndeterminatePressedProperty); }
            set { SetValue(CheckBoxCheckBackgroundFillIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckBackgroundFillIndeterminatePressedProperty =
            DependencyProperty.Register("CheckBoxCheckBackgroundFillIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundUnchecked
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundUncheckedProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundUncheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundUncheckedProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundUnchecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundUncheckedPointerOver
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundUncheckedPointerOverProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundUncheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundUncheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundUncheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundUncheckedPressed
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundUncheckedPressedProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundUncheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundUncheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundUncheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundUncheckedDisabled
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundUncheckedDisabledProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundUncheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundUncheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundUncheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundChecked
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundCheckedProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundCheckedProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundCheckedPointerOver
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundCheckedPointerOverProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundCheckedPressed
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundCheckedPressedProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundCheckedPressedProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundCheckedDisabled
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundCheckedDisabledProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundCheckedDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundIndeterminate
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundIndeterminateProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundIndeterminateProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundIndeterminatePointerOver
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundIndeterminatePointerOverProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundIndeterminatePointerOverProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundIndeterminatePressed
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundIndeterminatePressedProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundIndeterminatePressedProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CheckBoxCheckGlyphForegroundIndeterminateDisabled
        {
            get { return (object)GetValue(CheckBoxCheckGlyphForegroundIndeterminateDisabledProperty); }
            set { SetValue(CheckBoxCheckGlyphForegroundIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty CheckBoxCheckGlyphForegroundIndeterminateDisabledProperty =
            DependencyProperty.Register("CheckBoxCheckGlyphForegroundIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonForeground
        {
            get { return (object)GetValue(HyperlinkButtonForegroundProperty); }
            set { SetValue(HyperlinkButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonForegroundProperty =
            DependencyProperty.Register("HyperlinkButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonForegroundPointerOver
        {
            get { return (object)GetValue(HyperlinkButtonForegroundPointerOverProperty); }
            set { SetValue(HyperlinkButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonForegroundPointerOverProperty =
            DependencyProperty.Register("HyperlinkButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonForegroundPressed
        {
            get { return (object)GetValue(HyperlinkButtonForegroundPressedProperty); }
            set { SetValue(HyperlinkButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonForegroundPressedProperty =
            DependencyProperty.Register("HyperlinkButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonForegroundDisabled
        {
            get { return (object)GetValue(HyperlinkButtonForegroundDisabledProperty); }
            set { SetValue(HyperlinkButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonForegroundDisabledProperty =
            DependencyProperty.Register("HyperlinkButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonBackground
        {
            get { return (object)GetValue(HyperlinkButtonBackgroundProperty); }
            set { SetValue(HyperlinkButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonBackgroundProperty =
            DependencyProperty.Register("HyperlinkButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonBackgroundPointerOver
        {
            get { return (object)GetValue(HyperlinkButtonBackgroundPointerOverProperty); }
            set { SetValue(HyperlinkButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("HyperlinkButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonBackgroundPressed
        {
            get { return (object)GetValue(HyperlinkButtonBackgroundPressedProperty); }
            set { SetValue(HyperlinkButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonBackgroundPressedProperty =
            DependencyProperty.Register("HyperlinkButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HyperlinkButtonBackgroundDisabled
        {
            get { return (object)GetValue(HyperlinkButtonBackgroundDisabledProperty); }
            set { SetValue(HyperlinkButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty HyperlinkButtonBackgroundDisabledProperty =
            DependencyProperty.Register("HyperlinkButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBackground
        {
            get { return (object)GetValue(RepeatButtonBackgroundProperty); }
            set { SetValue(RepeatButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBackgroundProperty =
            DependencyProperty.Register("RepeatButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBackgroundPointerOver
        {
            get { return (object)GetValue(RepeatButtonBackgroundPointerOverProperty); }
            set { SetValue(RepeatButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("RepeatButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBackgroundPressed
        {
            get { return (object)GetValue(RepeatButtonBackgroundPressedProperty); }
            set { SetValue(RepeatButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBackgroundPressedProperty =
            DependencyProperty.Register("RepeatButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBackgroundDisabled
        {
            get { return (object)GetValue(RepeatButtonBackgroundDisabledProperty); }
            set { SetValue(RepeatButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBackgroundDisabledProperty =
            DependencyProperty.Register("RepeatButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonForeground
        {
            get { return (object)GetValue(RepeatButtonForegroundProperty); }
            set { SetValue(RepeatButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonForegroundProperty =
            DependencyProperty.Register("RepeatButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonForegroundPointerOver
        {
            get { return (object)GetValue(RepeatButtonForegroundPointerOverProperty); }
            set { SetValue(RepeatButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonForegroundPointerOverProperty =
            DependencyProperty.Register("RepeatButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonForegroundPressed
        {
            get { return (object)GetValue(RepeatButtonForegroundPressedProperty); }
            set { SetValue(RepeatButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonForegroundPressedProperty =
            DependencyProperty.Register("RepeatButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonForegroundDisabled
        {
            get { return (object)GetValue(RepeatButtonForegroundDisabledProperty); }
            set { SetValue(RepeatButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonForegroundDisabledProperty =
            DependencyProperty.Register("RepeatButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBorderBrush
        {
            get { return (object)GetValue(RepeatButtonBorderBrushProperty); }
            set { SetValue(RepeatButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBorderBrushProperty =
            DependencyProperty.Register("RepeatButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(RepeatButtonBorderBrushPointerOverProperty); }
            set { SetValue(RepeatButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("RepeatButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBorderBrushPressed
        {
            get { return (object)GetValue(RepeatButtonBorderBrushPressedProperty); }
            set { SetValue(RepeatButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBorderBrushPressedProperty =
            DependencyProperty.Register("RepeatButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RepeatButtonBorderBrushDisabled
        {
            get { return (object)GetValue(RepeatButtonBorderBrushDisabledProperty); }
            set { SetValue(RepeatButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty RepeatButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("RepeatButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchContentForeground
        {
            get { return (object)GetValue(ToggleSwitchContentForegroundProperty); }
            set { SetValue(ToggleSwitchContentForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchContentForegroundProperty =
            DependencyProperty.Register("ToggleSwitchContentForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchContentForegroundDisabled
        {
            get { return (object)GetValue(ToggleSwitchContentForegroundDisabledProperty); }
            set { SetValue(ToggleSwitchContentForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchContentForegroundDisabledProperty =
            DependencyProperty.Register("ToggleSwitchContentForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchHeaderForeground
        {
            get { return (object)GetValue(ToggleSwitchHeaderForegroundProperty); }
            set { SetValue(ToggleSwitchHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchHeaderForegroundProperty =
            DependencyProperty.Register("ToggleSwitchHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchHeaderForegroundDisabled
        {
            get { return (object)GetValue(ToggleSwitchHeaderForegroundDisabledProperty); }
            set { SetValue(ToggleSwitchHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchHeaderForegroundDisabledProperty =
            DependencyProperty.Register("ToggleSwitchHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchFillOffPressed
        {
            get { return (object)GetValue(ToggleSwitchFillOffPressedProperty); }
            set { SetValue(ToggleSwitchFillOffPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchFillOffPressedProperty =
            DependencyProperty.Register("ToggleSwitchFillOffPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOff
        {
            get { return (object)GetValue(ToggleSwitchStrokeOffProperty); }
            set { SetValue(ToggleSwitchStrokeOffProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOffProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOff", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOffPointerOver
        {
            get { return (object)GetValue(ToggleSwitchStrokeOffPointerOverProperty); }
            set { SetValue(ToggleSwitchStrokeOffPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOffPointerOverProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOffPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOffPressed
        {
            get { return (object)GetValue(ToggleSwitchStrokeOffPressedProperty); }
            set { SetValue(ToggleSwitchStrokeOffPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOffPressedProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOffPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOffDisabled
        {
            get { return (object)GetValue(ToggleSwitchStrokeOffDisabledProperty); }
            set { SetValue(ToggleSwitchStrokeOffDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOffDisabledProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOffDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchFillOn
        {
            get { return (object)GetValue(ToggleSwitchFillOnProperty); }
            set { SetValue(ToggleSwitchFillOnProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchFillOnProperty =
            DependencyProperty.Register("ToggleSwitchFillOn", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchFillOnPointerOver
        {
            get { return (object)GetValue(ToggleSwitchFillOnPointerOverProperty); }
            set { SetValue(ToggleSwitchFillOnPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchFillOnPointerOverProperty =
            DependencyProperty.Register("ToggleSwitchFillOnPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchFillOnPressed
        {
            get { return (object)GetValue(ToggleSwitchFillOnPressedProperty); }
            set { SetValue(ToggleSwitchFillOnPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchFillOnPressedProperty =
            DependencyProperty.Register("ToggleSwitchFillOnPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchFillOnDisabled
        {
            get { return (object)GetValue(ToggleSwitchFillOnDisabledProperty); }
            set { SetValue(ToggleSwitchFillOnDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchFillOnDisabledProperty =
            DependencyProperty.Register("ToggleSwitchFillOnDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOn
        {
            get { return (object)GetValue(ToggleSwitchStrokeOnProperty); }
            set { SetValue(ToggleSwitchStrokeOnProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOnProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOn", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOnPointerOver
        {
            get { return (object)GetValue(ToggleSwitchStrokeOnPointerOverProperty); }
            set { SetValue(ToggleSwitchStrokeOnPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOnPointerOverProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOnPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOnPressed
        {
            get { return (object)GetValue(ToggleSwitchStrokeOnPressedProperty); }
            set { SetValue(ToggleSwitchStrokeOnPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOnPressedProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOnPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchStrokeOnDisabled
        {
            get { return (object)GetValue(ToggleSwitchStrokeOnDisabledProperty); }
            set { SetValue(ToggleSwitchStrokeOnDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchStrokeOnDisabledProperty =
            DependencyProperty.Register("ToggleSwitchStrokeOnDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOff
        {
            get { return (object)GetValue(ToggleSwitchKnobFillOffProperty); }
            set { SetValue(ToggleSwitchKnobFillOffProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOffProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOff", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOffPointerOver
        {
            get { return (object)GetValue(ToggleSwitchKnobFillOffPointerOverProperty); }
            set { SetValue(ToggleSwitchKnobFillOffPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOffPointerOverProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOffPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOffPressed
        {
            get { return (object)GetValue(ToggleSwitchKnobFillOffPressedProperty); }
            set { SetValue(ToggleSwitchKnobFillOffPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOffPressedProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOffPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOffDisabled
        {
            get { return (object)GetValue(ToggleSwitchKnobFillOffDisabledProperty); }
            set { SetValue(ToggleSwitchKnobFillOffDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOffDisabledProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOffDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOn
        {
            get { return (object)GetValue(ToggleSwitchKnobFillOnProperty); }
            set { SetValue(ToggleSwitchKnobFillOnProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOnProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOn", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOnPointerOver
        {
            get { return (object)GetValue(ToggleSwitchKnobFillOnPointerOverProperty); }
            set { SetValue(ToggleSwitchKnobFillOnPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOnPointerOverProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOnPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOnPressed
        {
            get { return (object)GetValue(ToggleSwitchKnobFillOnPressedProperty); }
            set { SetValue(ToggleSwitchKnobFillOnPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOnPressedProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOnPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleSwitchKnobFillOnDisabled
        {
            get { return (object)GetValue(ToggleSwitchKnobFillOnDisabledProperty); }
            set { SetValue(ToggleSwitchKnobFillOnDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleSwitchKnobFillOnDisabledProperty =
            DependencyProperty.Register("ToggleSwitchKnobFillOnDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBackground
        {
            get { return (object)GetValue(ThumbBackgroundProperty); }
            set { SetValue(ThumbBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ThumbBackgroundProperty =
            DependencyProperty.Register("ThumbBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBackgroundPointerOver
        {
            get { return (object)GetValue(ThumbBackgroundPointerOverProperty); }
            set { SetValue(ThumbBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ThumbBackgroundPointerOverProperty =
            DependencyProperty.Register("ThumbBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBackgroundPressed
        {
            get { return (object)GetValue(ThumbBackgroundPressedProperty); }
            set { SetValue(ThumbBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ThumbBackgroundPressedProperty =
            DependencyProperty.Register("ThumbBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBorderBrush
        {
            get { return (object)GetValue(ThumbBorderBrushProperty); }
            set { SetValue(ThumbBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ThumbBorderBrushProperty =
            DependencyProperty.Register("ThumbBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBorderBrushPointerOver
        {
            get { return (object)GetValue(ThumbBorderBrushPointerOverProperty); }
            set { SetValue(ThumbBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ThumbBorderBrushPointerOverProperty =
            DependencyProperty.Register("ThumbBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ThumbBorderBrushPressed
        {
            get { return (object)GetValue(ThumbBorderBrushPressedProperty); }
            set { SetValue(ThumbBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty ThumbBorderBrushPressedProperty =
            DependencyProperty.Register("ThumbBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackground
        {
            get { return (object)GetValue(ToggleButtonBackgroundProperty); }
            set { SetValue(ToggleButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundProperty =
            DependencyProperty.Register("ToggleButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundPointerOver
        {
            get { return (object)GetValue(ToggleButtonBackgroundPointerOverProperty); }
            set { SetValue(ToggleButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("ToggleButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundPressed
        {
            get { return (object)GetValue(ToggleButtonBackgroundPressedProperty); }
            set { SetValue(ToggleButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundPressedProperty =
            DependencyProperty.Register("ToggleButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundDisabled
        {
            get { return (object)GetValue(ToggleButtonBackgroundDisabledProperty); }
            set { SetValue(ToggleButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundDisabledProperty =
            DependencyProperty.Register("ToggleButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundChecked
        {
            get { return (object)GetValue(ToggleButtonBackgroundCheckedProperty); }
            set { SetValue(ToggleButtonBackgroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundCheckedProperty =
            DependencyProperty.Register("ToggleButtonBackgroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundCheckedPointerOver
        {
            get { return (object)GetValue(ToggleButtonBackgroundCheckedPointerOverProperty); }
            set { SetValue(ToggleButtonBackgroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundCheckedPointerOverProperty =
            DependencyProperty.Register("ToggleButtonBackgroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundCheckedPressed
        {
            get { return (object)GetValue(ToggleButtonBackgroundCheckedPressedProperty); }
            set { SetValue(ToggleButtonBackgroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundCheckedPressedProperty =
            DependencyProperty.Register("ToggleButtonBackgroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundCheckedDisabled
        {
            get { return (object)GetValue(ToggleButtonBackgroundCheckedDisabledProperty); }
            set { SetValue(ToggleButtonBackgroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundCheckedDisabledProperty =
            DependencyProperty.Register("ToggleButtonBackgroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundIndeterminate
        {
            get { return (object)GetValue(ToggleButtonBackgroundIndeterminateProperty); }
            set { SetValue(ToggleButtonBackgroundIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundIndeterminateProperty =
            DependencyProperty.Register("ToggleButtonBackgroundIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundIndeterminatePointerOver
        {
            get { return (object)GetValue(ToggleButtonBackgroundIndeterminatePointerOverProperty); }
            set { SetValue(ToggleButtonBackgroundIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundIndeterminatePointerOverProperty =
            DependencyProperty.Register("ToggleButtonBackgroundIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundIndeterminatePressed
        {
            get { return (object)GetValue(ToggleButtonBackgroundIndeterminatePressedProperty); }
            set { SetValue(ToggleButtonBackgroundIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundIndeterminatePressedProperty =
            DependencyProperty.Register("ToggleButtonBackgroundIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBackgroundIndeterminateDisabled
        {
            get { return (object)GetValue(ToggleButtonBackgroundIndeterminateDisabledProperty); }
            set { SetValue(ToggleButtonBackgroundIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBackgroundIndeterminateDisabledProperty =
            DependencyProperty.Register("ToggleButtonBackgroundIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForeground
        {
            get { return (object)GetValue(ToggleButtonForegroundProperty); }
            set { SetValue(ToggleButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundProperty =
            DependencyProperty.Register("ToggleButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundPointerOver
        {
            get { return (object)GetValue(ToggleButtonForegroundPointerOverProperty); }
            set { SetValue(ToggleButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundPointerOverProperty =
            DependencyProperty.Register("ToggleButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundPressed
        {
            get { return (object)GetValue(ToggleButtonForegroundPressedProperty); }
            set { SetValue(ToggleButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundPressedProperty =
            DependencyProperty.Register("ToggleButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundDisabled
        {
            get { return (object)GetValue(ToggleButtonForegroundDisabledProperty); }
            set { SetValue(ToggleButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundDisabledProperty =
            DependencyProperty.Register("ToggleButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundChecked
        {
            get { return (object)GetValue(ToggleButtonForegroundCheckedProperty); }
            set { SetValue(ToggleButtonForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundCheckedProperty =
            DependencyProperty.Register("ToggleButtonForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundCheckedPointerOver
        {
            get { return (object)GetValue(ToggleButtonForegroundCheckedPointerOverProperty); }
            set { SetValue(ToggleButtonForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("ToggleButtonForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundCheckedPressed
        {
            get { return (object)GetValue(ToggleButtonForegroundCheckedPressedProperty); }
            set { SetValue(ToggleButtonForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundCheckedPressedProperty =
            DependencyProperty.Register("ToggleButtonForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundCheckedDisabled
        {
            get { return (object)GetValue(ToggleButtonForegroundCheckedDisabledProperty); }
            set { SetValue(ToggleButtonForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundCheckedDisabledProperty =
            DependencyProperty.Register("ToggleButtonForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundIndeterminate
        {
            get { return (object)GetValue(ToggleButtonForegroundIndeterminateProperty); }
            set { SetValue(ToggleButtonForegroundIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundIndeterminateProperty =
            DependencyProperty.Register("ToggleButtonForegroundIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundIndeterminatePointerOver
        {
            get { return (object)GetValue(ToggleButtonForegroundIndeterminatePointerOverProperty); }
            set { SetValue(ToggleButtonForegroundIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundIndeterminatePointerOverProperty =
            DependencyProperty.Register("ToggleButtonForegroundIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundIndeterminatePressed
        {
            get { return (object)GetValue(ToggleButtonForegroundIndeterminatePressedProperty); }
            set { SetValue(ToggleButtonForegroundIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundIndeterminatePressedProperty =
            DependencyProperty.Register("ToggleButtonForegroundIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonForegroundIndeterminateDisabled
        {
            get { return (object)GetValue(ToggleButtonForegroundIndeterminateDisabledProperty); }
            set { SetValue(ToggleButtonForegroundIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonForegroundIndeterminateDisabledProperty =
            DependencyProperty.Register("ToggleButtonForegroundIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrush
        {
            get { return (object)GetValue(ToggleButtonBorderBrushProperty); }
            set { SetValue(ToggleButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushProperty =
            DependencyProperty.Register("ToggleButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(ToggleButtonBorderBrushPointerOverProperty); }
            set { SetValue(ToggleButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushPressed
        {
            get { return (object)GetValue(ToggleButtonBorderBrushPressedProperty); }
            set { SetValue(ToggleButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushPressedProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushDisabled
        {
            get { return (object)GetValue(ToggleButtonBorderBrushDisabledProperty); }
            set { SetValue(ToggleButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushChecked
        {
            get { return (object)GetValue(ToggleButtonBorderBrushCheckedProperty); }
            set { SetValue(ToggleButtonBorderBrushCheckedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushCheckedProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushCheckedPointerOver
        {
            get { return (object)GetValue(ToggleButtonBorderBrushCheckedPointerOverProperty); }
            set { SetValue(ToggleButtonBorderBrushCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushCheckedPointerOverProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushCheckedDisabled
        {
            get { return (object)GetValue(ToggleButtonBorderBrushCheckedDisabledProperty); }
            set { SetValue(ToggleButtonBorderBrushCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushCheckedDisabledProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushIndeterminate
        {
            get { return (object)GetValue(ToggleButtonBorderBrushIndeterminateProperty); }
            set { SetValue(ToggleButtonBorderBrushIndeterminateProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushIndeterminateProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushIndeterminate", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushIndeterminatePointerOver
        {
            get { return (object)GetValue(ToggleButtonBorderBrushIndeterminatePointerOverProperty); }
            set { SetValue(ToggleButtonBorderBrushIndeterminatePointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushIndeterminatePointerOverProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushIndeterminatePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushIndeterminatePressed
        {
            get { return (object)GetValue(ToggleButtonBorderBrushIndeterminatePressedProperty); }
            set { SetValue(ToggleButtonBorderBrushIndeterminatePressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushIndeterminatePressedProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushIndeterminatePressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleButtonBorderBrushIndeterminateDisabled
        {
            get { return (object)GetValue(ToggleButtonBorderBrushIndeterminateDisabledProperty); }
            set { SetValue(ToggleButtonBorderBrushIndeterminateDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleButtonBorderBrushIndeterminateDisabledProperty =
            DependencyProperty.Register("ToggleButtonBorderBrushIndeterminateDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonBackgroundPointerOver
        {
            get { return (object)GetValue(ScrollBarButtonBackgroundPointerOverProperty); }
            set { SetValue(ScrollBarButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("ScrollBarButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonBackgroundPressed
        {
            get { return (object)GetValue(ScrollBarButtonBackgroundPressedProperty); }
            set { SetValue(ScrollBarButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonBackgroundPressedProperty =
            DependencyProperty.Register("ScrollBarButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonArrowForeground
        {
            get { return (object)GetValue(ScrollBarButtonArrowForegroundProperty); }
            set { SetValue(ScrollBarButtonArrowForegroundProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonArrowForegroundProperty =
            DependencyProperty.Register("ScrollBarButtonArrowForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonArrowForegroundPointerOver
        {
            get { return (object)GetValue(ScrollBarButtonArrowForegroundPointerOverProperty); }
            set { SetValue(ScrollBarButtonArrowForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonArrowForegroundPointerOverProperty =
            DependencyProperty.Register("ScrollBarButtonArrowForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonArrowForegroundPressed
        {
            get { return (object)GetValue(ScrollBarButtonArrowForegroundPressedProperty); }
            set { SetValue(ScrollBarButtonArrowForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonArrowForegroundPressedProperty =
            DependencyProperty.Register("ScrollBarButtonArrowForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarButtonArrowForegroundDisabled
        {
            get { return (object)GetValue(ScrollBarButtonArrowForegroundDisabledProperty); }
            set { SetValue(ScrollBarButtonArrowForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarButtonArrowForegroundDisabledProperty =
            DependencyProperty.Register("ScrollBarButtonArrowForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarThumbFill
        {
            get { return (object)GetValue(ScrollBarThumbFillProperty); }
            set { SetValue(ScrollBarThumbFillProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarThumbFillProperty =
            DependencyProperty.Register("ScrollBarThumbFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarThumbFillPointerOver
        {
            get { return (object)GetValue(ScrollBarThumbFillPointerOverProperty); }
            set { SetValue(ScrollBarThumbFillPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarThumbFillPointerOverProperty =
            DependencyProperty.Register("ScrollBarThumbFillPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarThumbFillPressed
        {
            get { return (object)GetValue(ScrollBarThumbFillPressedProperty); }
            set { SetValue(ScrollBarThumbFillPressedProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarThumbFillPressedProperty =
            DependencyProperty.Register("ScrollBarThumbFillPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarThumbFillDisabled
        {
            get { return (object)GetValue(ScrollBarThumbFillDisabledProperty); }
            set { SetValue(ScrollBarThumbFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarThumbFillDisabledProperty =
            DependencyProperty.Register("ScrollBarThumbFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarTrackFillDisabled
        {
            get { return (object)GetValue(ScrollBarTrackFillDisabledProperty); }
            set { SetValue(ScrollBarTrackFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarTrackFillDisabledProperty =
            DependencyProperty.Register("ScrollBarTrackFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarTrackStroke
        {
            get { return (object)GetValue(ScrollBarTrackStrokeProperty); }
            set { SetValue(ScrollBarTrackStrokeProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarTrackStrokeProperty =
            DependencyProperty.Register("ScrollBarTrackStroke", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarTrackStrokePointerOver
        {
            get { return (object)GetValue(ScrollBarTrackStrokePointerOverProperty); }
            set { SetValue(ScrollBarTrackStrokePointerOverProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarTrackStrokePointerOverProperty =
            DependencyProperty.Register("ScrollBarTrackStrokePointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarTrackStrokeDisabled
        {
            get { return (object)GetValue(ScrollBarTrackStrokeDisabledProperty); }
            set { SetValue(ScrollBarTrackStrokeDisabledProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarTrackStrokeDisabledProperty =
            DependencyProperty.Register("ScrollBarTrackStrokeDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarPanningThumbBackgroundDisabled
        {
            get { return (object)GetValue(ScrollBarPanningThumbBackgroundDisabledProperty); }
            set { SetValue(ScrollBarPanningThumbBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarPanningThumbBackgroundDisabledProperty =
            DependencyProperty.Register("ScrollBarPanningThumbBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarThumbBackgroundColor
        {
            get { return (object)GetValue(ScrollBarThumbBackgroundColorProperty); }
            set { SetValue(ScrollBarThumbBackgroundColorProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarThumbBackgroundColorProperty =
            DependencyProperty.Register("ScrollBarThumbBackgroundColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ScrollBarPanningThumbBackgroundColor
        {
            get { return (object)GetValue(ScrollBarPanningThumbBackgroundColorProperty); }
            set { SetValue(ScrollBarPanningThumbBackgroundColorProperty, value); }
        }

        public static readonly DependencyProperty ScrollBarPanningThumbBackgroundColorProperty =
            DependencyProperty.Register("ScrollBarPanningThumbBackgroundColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewHeaderItemDividerStroke
        {
            get { return (object)GetValue(ListViewHeaderItemDividerStrokeProperty); }
            set { SetValue(ListViewHeaderItemDividerStrokeProperty, value); }
        }

        public static readonly DependencyProperty ListViewHeaderItemDividerStrokeProperty =
            DependencyProperty.Register("ListViewHeaderItemDividerStroke", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForeground
        {
            get { return (object)GetValue(ComboBoxItemForegroundProperty); }
            set { SetValue(ComboBoxItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundProperty =
            DependencyProperty.Register("ComboBoxItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundPressed
        {
            get { return (object)GetValue(ComboBoxItemForegroundPressedProperty); }
            set { SetValue(ComboBoxItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundPressedProperty =
            DependencyProperty.Register("ComboBoxItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundPointerOver
        {
            get { return (object)GetValue(ComboBoxItemForegroundPointerOverProperty); }
            set { SetValue(ComboBoxItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundPointerOverProperty =
            DependencyProperty.Register("ComboBoxItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundDisabled
        {
            get { return (object)GetValue(ComboBoxItemForegroundDisabledProperty); }
            set { SetValue(ComboBoxItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundDisabledProperty =
            DependencyProperty.Register("ComboBoxItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundSelected
        {
            get { return (object)GetValue(ComboBoxItemForegroundSelectedProperty); }
            set { SetValue(ComboBoxItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundSelectedProperty =
            DependencyProperty.Register("ComboBoxItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundSelectedUnfocused
        {
            get { return (object)GetValue(ComboBoxItemForegroundSelectedUnfocusedProperty); }
            set { SetValue(ComboBoxItemForegroundSelectedUnfocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundSelectedUnfocusedProperty =
            DependencyProperty.Register("ComboBoxItemForegroundSelectedUnfocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundSelectedPressed
        {
            get { return (object)GetValue(ComboBoxItemForegroundSelectedPressedProperty); }
            set { SetValue(ComboBoxItemForegroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundSelectedPressedProperty =
            DependencyProperty.Register("ComboBoxItemForegroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundSelectedPointerOver
        {
            get { return (object)GetValue(ComboBoxItemForegroundSelectedPointerOverProperty); }
            set { SetValue(ComboBoxItemForegroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundSelectedPointerOverProperty =
            DependencyProperty.Register("ComboBoxItemForegroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemForegroundSelectedDisabled
        {
            get { return (object)GetValue(ComboBoxItemForegroundSelectedDisabledProperty); }
            set { SetValue(ComboBoxItemForegroundSelectedDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemForegroundSelectedDisabledProperty =
            DependencyProperty.Register("ComboBoxItemForegroundSelectedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundPressed
        {
            get { return (object)GetValue(ComboBoxItemBackgroundPressedProperty); }
            set { SetValue(ComboBoxItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundPressedProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundPointerOver
        {
            get { return (object)GetValue(ComboBoxItemBackgroundPointerOverProperty); }
            set { SetValue(ComboBoxItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundPointerOverProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundSelected
        {
            get { return (object)GetValue(ComboBoxItemBackgroundSelectedProperty); }
            set { SetValue(ComboBoxItemBackgroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundSelectedProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundSelectedUnfocused
        {
            get { return (object)GetValue(ComboBoxItemBackgroundSelectedUnfocusedProperty); }
            set { SetValue(ComboBoxItemBackgroundSelectedUnfocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundSelectedUnfocusedProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundSelectedUnfocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundSelectedPressed
        {
            get { return (object)GetValue(ComboBoxItemBackgroundSelectedPressedProperty); }
            set { SetValue(ComboBoxItemBackgroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundSelectedPressedProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxItemBackgroundSelectedPointerOver
        {
            get { return (object)GetValue(ComboBoxItemBackgroundSelectedPointerOverProperty); }
            set { SetValue(ComboBoxItemBackgroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxItemBackgroundSelectedPointerOverProperty =
            DependencyProperty.Register("ComboBoxItemBackgroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackground
        {
            get { return (object)GetValue(ComboBoxBackgroundProperty); }
            set { SetValue(ComboBoxBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundProperty =
            DependencyProperty.Register("ComboBoxBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundPointerOver
        {
            get { return (object)GetValue(ComboBoxBackgroundPointerOverProperty); }
            set { SetValue(ComboBoxBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundPointerOverProperty =
            DependencyProperty.Register("ComboBoxBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundPressed
        {
            get { return (object)GetValue(ComboBoxBackgroundPressedProperty); }
            set { SetValue(ComboBoxBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundPressedProperty =
            DependencyProperty.Register("ComboBoxBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundDisabled
        {
            get { return (object)GetValue(ComboBoxBackgroundDisabledProperty); }
            set { SetValue(ComboBoxBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundDisabledProperty =
            DependencyProperty.Register("ComboBoxBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundUnfocused
        {
            get { return (object)GetValue(ComboBoxBackgroundUnfocusedProperty); }
            set { SetValue(ComboBoxBackgroundUnfocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundUnfocusedProperty =
            DependencyProperty.Register("ComboBoxBackgroundUnfocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundBorderBrushFocused
        {
            get { return (object)GetValue(ComboBoxBackgroundBorderBrushFocusedProperty); }
            set { SetValue(ComboBoxBackgroundBorderBrushFocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundBorderBrushFocusedProperty =
            DependencyProperty.Register("ComboBoxBackgroundBorderBrushFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBackgroundBorderBrushUnfocused
        {
            get { return (object)GetValue(ComboBoxBackgroundBorderBrushUnfocusedProperty); }
            set { SetValue(ComboBoxBackgroundBorderBrushUnfocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBackgroundBorderBrushUnfocusedProperty =
            DependencyProperty.Register("ComboBoxBackgroundBorderBrushUnfocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxForeground
        {
            get { return (object)GetValue(ComboBoxForegroundProperty); }
            set { SetValue(ComboBoxForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxForegroundProperty =
            DependencyProperty.Register("ComboBoxForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxForegroundDisabled
        {
            get { return (object)GetValue(ComboBoxForegroundDisabledProperty); }
            set { SetValue(ComboBoxForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxForegroundDisabledProperty =
            DependencyProperty.Register("ComboBoxForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxForegroundFocused
        {
            get { return (object)GetValue(ComboBoxForegroundFocusedProperty); }
            set { SetValue(ComboBoxForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxForegroundFocusedProperty =
            DependencyProperty.Register("ComboBoxForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxForegroundFocusedPressed
        {
            get { return (object)GetValue(ComboBoxForegroundFocusedPressedProperty); }
            set { SetValue(ComboBoxForegroundFocusedPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxForegroundFocusedPressedProperty =
            DependencyProperty.Register("ComboBoxForegroundFocusedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxPlaceHolderForeground
        {
            get { return (object)GetValue(ComboBoxPlaceHolderForegroundProperty); }
            set { SetValue(ComboBoxPlaceHolderForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxPlaceHolderForegroundProperty =
            DependencyProperty.Register("ComboBoxPlaceHolderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxPlaceHolderForegroundFocusedPressed
        {
            get { return (object)GetValue(ComboBoxPlaceHolderForegroundFocusedPressedProperty); }
            set { SetValue(ComboBoxPlaceHolderForegroundFocusedPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxPlaceHolderForegroundFocusedPressedProperty =
            DependencyProperty.Register("ComboBoxPlaceHolderForegroundFocusedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBorderBrush
        {
            get { return (object)GetValue(ComboBoxBorderBrushProperty); }
            set { SetValue(ComboBoxBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBorderBrushProperty =
            DependencyProperty.Register("ComboBoxBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBorderBrushPointerOver
        {
            get { return (object)GetValue(ComboBoxBorderBrushPointerOverProperty); }
            set { SetValue(ComboBoxBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBorderBrushPointerOverProperty =
            DependencyProperty.Register("ComboBoxBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBorderBrushPressed
        {
            get { return (object)GetValue(ComboBoxBorderBrushPressedProperty); }
            set { SetValue(ComboBoxBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBorderBrushPressedProperty =
            DependencyProperty.Register("ComboBoxBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxBorderBrushDisabled
        {
            get { return (object)GetValue(ComboBoxBorderBrushDisabledProperty); }
            set { SetValue(ComboBoxBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxBorderBrushDisabledProperty =
            DependencyProperty.Register("ComboBoxBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownBackgroundPointerOver
        {
            get { return (object)GetValue(ComboBoxDropDownBackgroundPointerOverProperty); }
            set { SetValue(ComboBoxDropDownBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownBackgroundPointerOverProperty =
            DependencyProperty.Register("ComboBoxDropDownBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownBackgroundPointerPressed
        {
            get { return (object)GetValue(ComboBoxDropDownBackgroundPointerPressedProperty); }
            set { SetValue(ComboBoxDropDownBackgroundPointerPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownBackgroundPointerPressedProperty =
            DependencyProperty.Register("ComboBoxDropDownBackgroundPointerPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxFocusedDropDownBackgroundPointerOver
        {
            get { return (object)GetValue(ComboBoxFocusedDropDownBackgroundPointerOverProperty); }
            set { SetValue(ComboBoxFocusedDropDownBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxFocusedDropDownBackgroundPointerOverProperty =
            DependencyProperty.Register("ComboBoxFocusedDropDownBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxFocusedDropDownBackgroundPointerPressed
        {
            get { return (object)GetValue(ComboBoxFocusedDropDownBackgroundPointerPressedProperty); }
            set { SetValue(ComboBoxFocusedDropDownBackgroundPointerPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxFocusedDropDownBackgroundPointerPressedProperty =
            DependencyProperty.Register("ComboBoxFocusedDropDownBackgroundPointerPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownGlyphForeground
        {
            get { return (object)GetValue(ComboBoxDropDownGlyphForegroundProperty); }
            set { SetValue(ComboBoxDropDownGlyphForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownGlyphForegroundProperty =
            DependencyProperty.Register("ComboBoxDropDownGlyphForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxEditableDropDownGlyphForeground
        {
            get { return (object)GetValue(ComboBoxEditableDropDownGlyphForegroundProperty); }
            set { SetValue(ComboBoxEditableDropDownGlyphForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxEditableDropDownGlyphForegroundProperty =
            DependencyProperty.Register("ComboBoxEditableDropDownGlyphForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownGlyphForegroundDisabled
        {
            get { return (object)GetValue(ComboBoxDropDownGlyphForegroundDisabledProperty); }
            set { SetValue(ComboBoxDropDownGlyphForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownGlyphForegroundDisabledProperty =
            DependencyProperty.Register("ComboBoxDropDownGlyphForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownGlyphForegroundFocused
        {
            get { return (object)GetValue(ComboBoxDropDownGlyphForegroundFocusedProperty); }
            set { SetValue(ComboBoxDropDownGlyphForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownGlyphForegroundFocusedProperty =
            DependencyProperty.Register("ComboBoxDropDownGlyphForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownGlyphForegroundFocusedPressed
        {
            get { return (object)GetValue(ComboBoxDropDownGlyphForegroundFocusedPressedProperty); }
            set { SetValue(ComboBoxDropDownGlyphForegroundFocusedPressedProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownGlyphForegroundFocusedPressedProperty =
            DependencyProperty.Register("ComboBoxDropDownGlyphForegroundFocusedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownBackground
        {
            get { return (object)GetValue(ComboBoxDropDownBackgroundProperty); }
            set { SetValue(ComboBoxDropDownBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownBackgroundProperty =
            DependencyProperty.Register("ComboBoxDropDownBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownForeground
        {
            get { return (object)GetValue(ComboBoxDropDownForegroundProperty); }
            set { SetValue(ComboBoxDropDownForegroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownForegroundProperty =
            DependencyProperty.Register("ComboBoxDropDownForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxDropDownBorderBrush
        {
            get { return (object)GetValue(ComboBoxDropDownBorderBrushProperty); }
            set { SetValue(ComboBoxDropDownBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxDropDownBorderBrushProperty =
            DependencyProperty.Register("ComboBoxDropDownBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarSeparatorForeground
        {
            get { return (object)GetValue(AppBarSeparatorForegroundProperty); }
            set { SetValue(AppBarSeparatorForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarSeparatorForegroundProperty =
            DependencyProperty.Register("AppBarSeparatorForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonBackgroundPointerOver
        {
            get { return (object)GetValue(AppBarEllipsisButtonBackgroundPointerOverProperty); }
            set { SetValue(AppBarEllipsisButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("AppBarEllipsisButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonBackgroundPressed
        {
            get { return (object)GetValue(AppBarEllipsisButtonBackgroundPressedProperty); }
            set { SetValue(AppBarEllipsisButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonBackgroundPressedProperty =
            DependencyProperty.Register("AppBarEllipsisButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonForeground
        {
            get { return (object)GetValue(AppBarEllipsisButtonForegroundProperty); }
            set { SetValue(AppBarEllipsisButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonForegroundProperty =
            DependencyProperty.Register("AppBarEllipsisButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonForegroundPointerOver
        {
            get { return (object)GetValue(AppBarEllipsisButtonForegroundPointerOverProperty); }
            set { SetValue(AppBarEllipsisButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarEllipsisButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonForegroundPressed
        {
            get { return (object)GetValue(AppBarEllipsisButtonForegroundPressedProperty); }
            set { SetValue(AppBarEllipsisButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonForegroundPressedProperty =
            DependencyProperty.Register("AppBarEllipsisButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarEllipsisButtonForegroundDisabled
        {
            get { return (object)GetValue(AppBarEllipsisButtonForegroundDisabledProperty); }
            set { SetValue(AppBarEllipsisButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarEllipsisButtonForegroundDisabledProperty =
            DependencyProperty.Register("AppBarEllipsisButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarBackground
        {
            get { return (object)GetValue(AppBarBackgroundProperty); }
            set { SetValue(AppBarBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarBackgroundProperty =
            DependencyProperty.Register("AppBarBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarForeground
        {
            get { return (object)GetValue(AppBarForegroundProperty); }
            set { SetValue(AppBarForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarForegroundProperty =
            DependencyProperty.Register("AppBarForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarHighContrastBorder
        {
            get { return (object)GetValue(AppBarHighContrastBorderProperty); }
            set { SetValue(AppBarHighContrastBorderProperty, value); }
        }

        public static readonly DependencyProperty AppBarHighContrastBorderProperty =
            DependencyProperty.Register("AppBarHighContrastBorder", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ContentDialogForeground
        {
            get { return (object)GetValue(ContentDialogForegroundProperty); }
            set { SetValue(ContentDialogForegroundProperty, value); }
        }

        public static readonly DependencyProperty ContentDialogForegroundProperty =
            DependencyProperty.Register("ContentDialogForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ContentDialogBackground
        {
            get { return (object)GetValue(ContentDialogBackgroundProperty); }
            set { SetValue(ContentDialogBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ContentDialogBackgroundProperty =
            DependencyProperty.Register("ContentDialogBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ContentDialogBorderBrush
        {
            get { return (object)GetValue(ContentDialogBorderBrushProperty); }
            set { SetValue(ContentDialogBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ContentDialogBorderBrushProperty =
            DependencyProperty.Register("ContentDialogBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBackground
        {
            get { return (object)GetValue(AccentButtonBackgroundProperty); }
            set { SetValue(AccentButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBackgroundProperty =
            DependencyProperty.Register("AccentButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBackgroundPointerOver
        {
            get { return (object)GetValue(AccentButtonBackgroundPointerOverProperty); }
            set { SetValue(AccentButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("AccentButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBackgroundPressed
        {
            get { return (object)GetValue(AccentButtonBackgroundPressedProperty); }
            set { SetValue(AccentButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBackgroundPressedProperty =
            DependencyProperty.Register("AccentButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBackgroundDisabled
        {
            get { return (object)GetValue(AccentButtonBackgroundDisabledProperty); }
            set { SetValue(AccentButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBackgroundDisabledProperty =
            DependencyProperty.Register("AccentButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonForeground
        {
            get { return (object)GetValue(AccentButtonForegroundProperty); }
            set { SetValue(AccentButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonForegroundProperty =
            DependencyProperty.Register("AccentButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonForegroundPointerOver
        {
            get { return (object)GetValue(AccentButtonForegroundPointerOverProperty); }
            set { SetValue(AccentButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonForegroundPointerOverProperty =
            DependencyProperty.Register("AccentButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonForegroundPressed
        {
            get { return (object)GetValue(AccentButtonForegroundPressedProperty); }
            set { SetValue(AccentButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonForegroundPressedProperty =
            DependencyProperty.Register("AccentButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonForegroundDisabled
        {
            get { return (object)GetValue(AccentButtonForegroundDisabledProperty); }
            set { SetValue(AccentButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonForegroundDisabledProperty =
            DependencyProperty.Register("AccentButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBorderBrush
        {
            get { return (object)GetValue(AccentButtonBorderBrushProperty); }
            set { SetValue(AccentButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBorderBrushProperty =
            DependencyProperty.Register("AccentButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(AccentButtonBorderBrushPointerOverProperty); }
            set { SetValue(AccentButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("AccentButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBorderBrushPressed
        {
            get { return (object)GetValue(AccentButtonBorderBrushPressedProperty); }
            set { SetValue(AccentButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBorderBrushPressedProperty =
            DependencyProperty.Register("AccentButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AccentButtonBorderBrushDisabled
        {
            get { return (object)GetValue(AccentButtonBorderBrushDisabledProperty); }
            set { SetValue(AccentButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty AccentButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("AccentButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToolTipForeground
        {
            get { return (object)GetValue(ToolTipForegroundProperty); }
            set { SetValue(ToolTipForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToolTipForegroundProperty =
            DependencyProperty.Register("ToolTipForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToolTipBackground
        {
            get { return (object)GetValue(ToolTipBackgroundProperty); }
            set { SetValue(ToolTipBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ToolTipBackgroundProperty =
            DependencyProperty.Register("ToolTipBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToolTipBorderBrush
        {
            get { return (object)GetValue(ToolTipBorderBrushProperty); }
            set { SetValue(ToolTipBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ToolTipBorderBrushProperty =
            DependencyProperty.Register("ToolTipBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerForeground
        {
            get { return (object)GetValue(CalendarDatePickerForegroundProperty); }
            set { SetValue(CalendarDatePickerForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerForegroundProperty =
            DependencyProperty.Register("CalendarDatePickerForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerForegroundDisabled
        {
            get { return (object)GetValue(CalendarDatePickerForegroundDisabledProperty); }
            set { SetValue(CalendarDatePickerForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerForegroundDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerCalendarGlyphForeground
        {
            get { return (object)GetValue(CalendarDatePickerCalendarGlyphForegroundProperty); }
            set { SetValue(CalendarDatePickerCalendarGlyphForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerCalendarGlyphForegroundProperty =
            DependencyProperty.Register("CalendarDatePickerCalendarGlyphForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerCalendarGlyphForegroundDisabled
        {
            get { return (object)GetValue(CalendarDatePickerCalendarGlyphForegroundDisabledProperty); }
            set { SetValue(CalendarDatePickerCalendarGlyphForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerCalendarGlyphForegroundDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerCalendarGlyphForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerTextForeground
        {
            get { return (object)GetValue(CalendarDatePickerTextForegroundProperty); }
            set { SetValue(CalendarDatePickerTextForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerTextForegroundProperty =
            DependencyProperty.Register("CalendarDatePickerTextForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerTextForegroundDisabled
        {
            get { return (object)GetValue(CalendarDatePickerTextForegroundDisabledProperty); }
            set { SetValue(CalendarDatePickerTextForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerTextForegroundDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerTextForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerTextForegroundSelected
        {
            get { return (object)GetValue(CalendarDatePickerTextForegroundSelectedProperty); }
            set { SetValue(CalendarDatePickerTextForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerTextForegroundSelectedProperty =
            DependencyProperty.Register("CalendarDatePickerTextForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerHeaderForegroundDisabled
        {
            get { return (object)GetValue(CalendarDatePickerHeaderForegroundDisabledProperty); }
            set { SetValue(CalendarDatePickerHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerHeaderForegroundDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBackground
        {
            get { return (object)GetValue(CalendarDatePickerBackgroundProperty); }
            set { SetValue(CalendarDatePickerBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBackgroundProperty =
            DependencyProperty.Register("CalendarDatePickerBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBackgroundPointerOver
        {
            get { return (object)GetValue(CalendarDatePickerBackgroundPointerOverProperty); }
            set { SetValue(CalendarDatePickerBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBackgroundPointerOverProperty =
            DependencyProperty.Register("CalendarDatePickerBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBackgroundPressed
        {
            get { return (object)GetValue(CalendarDatePickerBackgroundPressedProperty); }
            set { SetValue(CalendarDatePickerBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBackgroundPressedProperty =
            DependencyProperty.Register("CalendarDatePickerBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBackgroundDisabled
        {
            get { return (object)GetValue(CalendarDatePickerBackgroundDisabledProperty); }
            set { SetValue(CalendarDatePickerBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBackgroundDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBackgroundFocused
        {
            get { return (object)GetValue(CalendarDatePickerBackgroundFocusedProperty); }
            set { SetValue(CalendarDatePickerBackgroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBackgroundFocusedProperty =
            DependencyProperty.Register("CalendarDatePickerBackgroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBorderBrush
        {
            get { return (object)GetValue(CalendarDatePickerBorderBrushProperty); }
            set { SetValue(CalendarDatePickerBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBorderBrushProperty =
            DependencyProperty.Register("CalendarDatePickerBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBorderBrushPointerOver
        {
            get { return (object)GetValue(CalendarDatePickerBorderBrushPointerOverProperty); }
            set { SetValue(CalendarDatePickerBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBorderBrushPointerOverProperty =
            DependencyProperty.Register("CalendarDatePickerBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBorderBrushPressed
        {
            get { return (object)GetValue(CalendarDatePickerBorderBrushPressedProperty); }
            set { SetValue(CalendarDatePickerBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBorderBrushPressedProperty =
            DependencyProperty.Register("CalendarDatePickerBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerBorderBrushDisabled
        {
            get { return (object)GetValue(CalendarDatePickerBorderBrushDisabledProperty); }
            set { SetValue(CalendarDatePickerBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerBorderBrushDisabledProperty =
            DependencyProperty.Register("CalendarDatePickerBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewFocusBorderBrush
        {
            get { return (object)GetValue(CalendarViewFocusBorderBrushProperty); }
            set { SetValue(CalendarViewFocusBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewFocusBorderBrushProperty =
            DependencyProperty.Register("CalendarViewFocusBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewSelectedHoverBorderBrush
        {
            get { return (object)GetValue(CalendarViewSelectedHoverBorderBrushProperty); }
            set { SetValue(CalendarViewSelectedHoverBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewSelectedHoverBorderBrushProperty =
            DependencyProperty.Register("CalendarViewSelectedHoverBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewSelectedPressedBorderBrush
        {
            get { return (object)GetValue(CalendarViewSelectedPressedBorderBrushProperty); }
            set { SetValue(CalendarViewSelectedPressedBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewSelectedPressedBorderBrushProperty =
            DependencyProperty.Register("CalendarViewSelectedPressedBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewSelectedBorderBrush
        {
            get { return (object)GetValue(CalendarViewSelectedBorderBrushProperty); }
            set { SetValue(CalendarViewSelectedBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewSelectedBorderBrushProperty =
            DependencyProperty.Register("CalendarViewSelectedBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewHoverBorderBrush
        {
            get { return (object)GetValue(CalendarViewHoverBorderBrushProperty); }
            set { SetValue(CalendarViewHoverBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewHoverBorderBrushProperty =
            DependencyProperty.Register("CalendarViewHoverBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewPressedBorderBrush
        {
            get { return (object)GetValue(CalendarViewPressedBorderBrushProperty); }
            set { SetValue(CalendarViewPressedBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewPressedBorderBrushProperty =
            DependencyProperty.Register("CalendarViewPressedBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewTodayForeground
        {
            get { return (object)GetValue(CalendarViewTodayForegroundProperty); }
            set { SetValue(CalendarViewTodayForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewTodayForegroundProperty =
            DependencyProperty.Register("CalendarViewTodayForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewBlackoutForeground
        {
            get { return (object)GetValue(CalendarViewBlackoutForegroundProperty); }
            set { SetValue(CalendarViewBlackoutForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewBlackoutForegroundProperty =
            DependencyProperty.Register("CalendarViewBlackoutForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewSelectedForeground
        {
            get { return (object)GetValue(CalendarViewSelectedForegroundProperty); }
            set { SetValue(CalendarViewSelectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewSelectedForegroundProperty =
            DependencyProperty.Register("CalendarViewSelectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewPressedForeground
        {
            get { return (object)GetValue(CalendarViewPressedForegroundProperty); }
            set { SetValue(CalendarViewPressedForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewPressedForegroundProperty =
            DependencyProperty.Register("CalendarViewPressedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewOutOfScopeForeground
        {
            get { return (object)GetValue(CalendarViewOutOfScopeForegroundProperty); }
            set { SetValue(CalendarViewOutOfScopeForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewOutOfScopeForegroundProperty =
            DependencyProperty.Register("CalendarViewOutOfScopeForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewCalendarItemForeground
        {
            get { return (object)GetValue(CalendarViewCalendarItemForegroundProperty); }
            set { SetValue(CalendarViewCalendarItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewCalendarItemForegroundProperty =
            DependencyProperty.Register("CalendarViewCalendarItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewOutOfScopeBackground
        {
            get { return (object)GetValue(CalendarViewOutOfScopeBackgroundProperty); }
            set { SetValue(CalendarViewOutOfScopeBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewOutOfScopeBackgroundProperty =
            DependencyProperty.Register("CalendarViewOutOfScopeBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewCalendarItemBackground
        {
            get { return (object)GetValue(CalendarViewCalendarItemBackgroundProperty); }
            set { SetValue(CalendarViewCalendarItemBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewCalendarItemBackgroundProperty =
            DependencyProperty.Register("CalendarViewCalendarItemBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewForeground
        {
            get { return (object)GetValue(CalendarViewForegroundProperty); }
            set { SetValue(CalendarViewForegroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewForegroundProperty =
            DependencyProperty.Register("CalendarViewForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewBackground
        {
            get { return (object)GetValue(CalendarViewBackgroundProperty); }
            set { SetValue(CalendarViewBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewBackgroundProperty =
            DependencyProperty.Register("CalendarViewBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewBorderBrush
        {
            get { return (object)GetValue(CalendarViewBorderBrushProperty); }
            set { SetValue(CalendarViewBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewBorderBrushProperty =
            DependencyProperty.Register("CalendarViewBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewWeekDayForegroundDisabled
        {
            get { return (object)GetValue(CalendarViewWeekDayForegroundDisabledProperty); }
            set { SetValue(CalendarViewWeekDayForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewWeekDayForegroundDisabledProperty =
            DependencyProperty.Register("CalendarViewWeekDayForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewNavigationButtonForegroundPointerOver
        {
            get { return (object)GetValue(CalendarViewNavigationButtonForegroundPointerOverProperty); }
            set { SetValue(CalendarViewNavigationButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewNavigationButtonForegroundPointerOverProperty =
            DependencyProperty.Register("CalendarViewNavigationButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewNavigationButtonForegroundPressed
        {
            get { return (object)GetValue(CalendarViewNavigationButtonForegroundPressedProperty); }
            set { SetValue(CalendarViewNavigationButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewNavigationButtonForegroundPressedProperty =
            DependencyProperty.Register("CalendarViewNavigationButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewNavigationButtonForegroundDisabled
        {
            get { return (object)GetValue(CalendarViewNavigationButtonForegroundDisabledProperty); }
            set { SetValue(CalendarViewNavigationButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewNavigationButtonForegroundDisabledProperty =
            DependencyProperty.Register("CalendarViewNavigationButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarViewNavigationButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(CalendarViewNavigationButtonBorderBrushPointerOverProperty); }
            set { SetValue(CalendarViewNavigationButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty CalendarViewNavigationButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("CalendarViewNavigationButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubForeground
        {
            get { return (object)GetValue(HubForegroundProperty); }
            set { SetValue(HubForegroundProperty, value); }
        }

        public static readonly DependencyProperty HubForegroundProperty =
            DependencyProperty.Register("HubForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubSectionHeaderButtonForeground
        {
            get { return (object)GetValue(HubSectionHeaderButtonForegroundProperty); }
            set { SetValue(HubSectionHeaderButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty HubSectionHeaderButtonForegroundProperty =
            DependencyProperty.Register("HubSectionHeaderButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubSectionHeaderButtonForegroundPointerOver
        {
            get { return (object)GetValue(HubSectionHeaderButtonForegroundPointerOverProperty); }
            set { SetValue(HubSectionHeaderButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty HubSectionHeaderButtonForegroundPointerOverProperty =
            DependencyProperty.Register("HubSectionHeaderButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubSectionHeaderButtonForegroundPressed
        {
            get { return (object)GetValue(HubSectionHeaderButtonForegroundPressedProperty); }
            set { SetValue(HubSectionHeaderButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty HubSectionHeaderButtonForegroundPressedProperty =
            DependencyProperty.Register("HubSectionHeaderButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubSectionHeaderButtonForegroundDisabled
        {
            get { return (object)GetValue(HubSectionHeaderButtonForegroundDisabledProperty); }
            set { SetValue(HubSectionHeaderButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty HubSectionHeaderButtonForegroundDisabledProperty =
            DependencyProperty.Register("HubSectionHeaderButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object HubSectionHeaderForeground
        {
            get { return (object)GetValue(HubSectionHeaderForegroundProperty); }
            set { SetValue(HubSectionHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty HubSectionHeaderForegroundProperty =
            DependencyProperty.Register("HubSectionHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewBackground
        {
            get { return (object)GetValue(FlipViewBackgroundProperty); }
            set { SetValue(FlipViewBackgroundProperty, value); }
        }

        public static readonly DependencyProperty FlipViewBackgroundProperty =
            DependencyProperty.Register("FlipViewBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBackground
        {
            get { return (object)GetValue(FlipViewNextPreviousButtonBackgroundProperty); }
            set { SetValue(FlipViewNextPreviousButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBackgroundProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBackgroundPointerOver
        {
            get { return (object)GetValue(FlipViewNextPreviousButtonBackgroundPointerOverProperty); }
            set { SetValue(FlipViewNextPreviousButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBackgroundPressed
        {
            get { return (object)GetValue(FlipViewNextPreviousButtonBackgroundPressedProperty); }
            set { SetValue(FlipViewNextPreviousButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBackgroundPressedProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousArrowForeground
        {
            get { return (object)GetValue(FlipViewNextPreviousArrowForegroundProperty); }
            set { SetValue(FlipViewNextPreviousArrowForegroundProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousArrowForegroundProperty =
            DependencyProperty.Register("FlipViewNextPreviousArrowForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousArrowForegroundPointerOver
        {
            get { return (object)GetValue(FlipViewNextPreviousArrowForegroundPointerOverProperty); }
            set { SetValue(FlipViewNextPreviousArrowForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousArrowForegroundPointerOverProperty =
            DependencyProperty.Register("FlipViewNextPreviousArrowForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousArrowForegroundPressed
        {
            get { return (object)GetValue(FlipViewNextPreviousArrowForegroundPressedProperty); }
            set { SetValue(FlipViewNextPreviousArrowForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousArrowForegroundPressedProperty =
            DependencyProperty.Register("FlipViewNextPreviousArrowForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBorderBrush
        {
            get { return (object)GetValue(FlipViewNextPreviousButtonBorderBrushProperty); }
            set { SetValue(FlipViewNextPreviousButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBorderBrushProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(FlipViewNextPreviousButtonBorderBrushPointerOverProperty); }
            set { SetValue(FlipViewNextPreviousButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlipViewNextPreviousButtonBorderBrushPressed
        {
            get { return (object)GetValue(FlipViewNextPreviousButtonBorderBrushPressedProperty); }
            set { SetValue(FlipViewNextPreviousButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty FlipViewNextPreviousButtonBorderBrushPressedProperty =
            DependencyProperty.Register("FlipViewNextPreviousButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonBackgroundPointerOver
        {
            get { return (object)GetValue(DateTimePickerFlyoutButtonBackgroundPointerOverProperty); }
            set { SetValue(DateTimePickerFlyoutButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonBackgroundPressed
        {
            get { return (object)GetValue(DateTimePickerFlyoutButtonBackgroundPressedProperty); }
            set { SetValue(DateTimePickerFlyoutButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonBackgroundPressedProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonBorderBrush
        {
            get { return (object)GetValue(DateTimePickerFlyoutButtonBorderBrushProperty); }
            set { SetValue(DateTimePickerFlyoutButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonBorderBrushProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(DateTimePickerFlyoutButtonBorderBrushPointerOverProperty); }
            set { SetValue(DateTimePickerFlyoutButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonBorderBrushPressed
        {
            get { return (object)GetValue(DateTimePickerFlyoutButtonBorderBrushPressedProperty); }
            set { SetValue(DateTimePickerFlyoutButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonBorderBrushPressedProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonForegroundPointerOver
        {
            get { return (object)GetValue(DateTimePickerFlyoutButtonForegroundPointerOverProperty); }
            set { SetValue(DateTimePickerFlyoutButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonForegroundPointerOverProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DateTimePickerFlyoutButtonForegroundPressed
        {
            get { return (object)GetValue(DateTimePickerFlyoutButtonForegroundPressedProperty); }
            set { SetValue(DateTimePickerFlyoutButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty DateTimePickerFlyoutButtonForegroundPressedProperty =
            DependencyProperty.Register("DateTimePickerFlyoutButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerSpacerFill
        {
            get { return (object)GetValue(DatePickerSpacerFillProperty); }
            set { SetValue(DatePickerSpacerFillProperty, value); }
        }

        public static readonly DependencyProperty DatePickerSpacerFillProperty =
            DependencyProperty.Register("DatePickerSpacerFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerSpacerFillDisabled
        {
            get { return (object)GetValue(DatePickerSpacerFillDisabledProperty); }
            set { SetValue(DatePickerSpacerFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty DatePickerSpacerFillDisabledProperty =
            DependencyProperty.Register("DatePickerSpacerFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerHeaderForeground
        {
            get { return (object)GetValue(DatePickerHeaderForegroundProperty); }
            set { SetValue(DatePickerHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty DatePickerHeaderForegroundProperty =
            DependencyProperty.Register("DatePickerHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerHeaderForegroundDisabled
        {
            get { return (object)GetValue(DatePickerHeaderForegroundDisabledProperty); }
            set { SetValue(DatePickerHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty DatePickerHeaderForegroundDisabledProperty =
            DependencyProperty.Register("DatePickerHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBorderBrush
        {
            get { return (object)GetValue(DatePickerButtonBorderBrushProperty); }
            set { SetValue(DatePickerButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBorderBrushProperty =
            DependencyProperty.Register("DatePickerButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(DatePickerButtonBorderBrushPointerOverProperty); }
            set { SetValue(DatePickerButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("DatePickerButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBorderBrushPressed
        {
            get { return (object)GetValue(DatePickerButtonBorderBrushPressedProperty); }
            set { SetValue(DatePickerButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBorderBrushPressedProperty =
            DependencyProperty.Register("DatePickerButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBorderBrushDisabled
        {
            get { return (object)GetValue(DatePickerButtonBorderBrushDisabledProperty); }
            set { SetValue(DatePickerButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("DatePickerButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBackground
        {
            get { return (object)GetValue(DatePickerButtonBackgroundProperty); }
            set { SetValue(DatePickerButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBackgroundProperty =
            DependencyProperty.Register("DatePickerButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBackgroundPointerOver
        {
            get { return (object)GetValue(DatePickerButtonBackgroundPointerOverProperty); }
            set { SetValue(DatePickerButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("DatePickerButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBackgroundPressed
        {
            get { return (object)GetValue(DatePickerButtonBackgroundPressedProperty); }
            set { SetValue(DatePickerButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBackgroundPressedProperty =
            DependencyProperty.Register("DatePickerButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBackgroundDisabled
        {
            get { return (object)GetValue(DatePickerButtonBackgroundDisabledProperty); }
            set { SetValue(DatePickerButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBackgroundDisabledProperty =
            DependencyProperty.Register("DatePickerButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonBackgroundFocused
        {
            get { return (object)GetValue(DatePickerButtonBackgroundFocusedProperty); }
            set { SetValue(DatePickerButtonBackgroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonBackgroundFocusedProperty =
            DependencyProperty.Register("DatePickerButtonBackgroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonForeground
        {
            get { return (object)GetValue(DatePickerButtonForegroundProperty); }
            set { SetValue(DatePickerButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonForegroundProperty =
            DependencyProperty.Register("DatePickerButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonForegroundPointerOver
        {
            get { return (object)GetValue(DatePickerButtonForegroundPointerOverProperty); }
            set { SetValue(DatePickerButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonForegroundPointerOverProperty =
            DependencyProperty.Register("DatePickerButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonForegroundPressed
        {
            get { return (object)GetValue(DatePickerButtonForegroundPressedProperty); }
            set { SetValue(DatePickerButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonForegroundPressedProperty =
            DependencyProperty.Register("DatePickerButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonForegroundDisabled
        {
            get { return (object)GetValue(DatePickerButtonForegroundDisabledProperty); }
            set { SetValue(DatePickerButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonForegroundDisabledProperty =
            DependencyProperty.Register("DatePickerButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerButtonForegroundFocused
        {
            get { return (object)GetValue(DatePickerButtonForegroundFocusedProperty); }
            set { SetValue(DatePickerButtonForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty DatePickerButtonForegroundFocusedProperty =
            DependencyProperty.Register("DatePickerButtonForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerFlyoutPresenterBackground
        {
            get { return (object)GetValue(DatePickerFlyoutPresenterBackgroundProperty); }
            set { SetValue(DatePickerFlyoutPresenterBackgroundProperty, value); }
        }

        public static readonly DependencyProperty DatePickerFlyoutPresenterBackgroundProperty =
            DependencyProperty.Register("DatePickerFlyoutPresenterBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerFlyoutPresenterBorderBrush
        {
            get { return (object)GetValue(DatePickerFlyoutPresenterBorderBrushProperty); }
            set { SetValue(DatePickerFlyoutPresenterBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty DatePickerFlyoutPresenterBorderBrushProperty =
            DependencyProperty.Register("DatePickerFlyoutPresenterBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerFlyoutPresenterSpacerFill
        {
            get { return (object)GetValue(DatePickerFlyoutPresenterSpacerFillProperty); }
            set { SetValue(DatePickerFlyoutPresenterSpacerFillProperty, value); }
        }

        public static readonly DependencyProperty DatePickerFlyoutPresenterSpacerFillProperty =
            DependencyProperty.Register("DatePickerFlyoutPresenterSpacerFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerFlyoutPresenterHighlightFill
        {
            get { return (object)GetValue(DatePickerFlyoutPresenterHighlightFillProperty); }
            set { SetValue(DatePickerFlyoutPresenterHighlightFillProperty, value); }
        }

        public static readonly DependencyProperty DatePickerFlyoutPresenterHighlightFillProperty =
            DependencyProperty.Register("DatePickerFlyoutPresenterHighlightFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerSpacerFill
        {
            get { return (object)GetValue(TimePickerSpacerFillProperty); }
            set { SetValue(TimePickerSpacerFillProperty, value); }
        }

        public static readonly DependencyProperty TimePickerSpacerFillProperty =
            DependencyProperty.Register("TimePickerSpacerFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerSpacerFillDisabled
        {
            get { return (object)GetValue(TimePickerSpacerFillDisabledProperty); }
            set { SetValue(TimePickerSpacerFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty TimePickerSpacerFillDisabledProperty =
            DependencyProperty.Register("TimePickerSpacerFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerHeaderForeground
        {
            get { return (object)GetValue(TimePickerHeaderForegroundProperty); }
            set { SetValue(TimePickerHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty TimePickerHeaderForegroundProperty =
            DependencyProperty.Register("TimePickerHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerHeaderForegroundDisabled
        {
            get { return (object)GetValue(TimePickerHeaderForegroundDisabledProperty); }
            set { SetValue(TimePickerHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TimePickerHeaderForegroundDisabledProperty =
            DependencyProperty.Register("TimePickerHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBorderBrush
        {
            get { return (object)GetValue(TimePickerButtonBorderBrushProperty); }
            set { SetValue(TimePickerButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBorderBrushProperty =
            DependencyProperty.Register("TimePickerButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(TimePickerButtonBorderBrushPointerOverProperty); }
            set { SetValue(TimePickerButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("TimePickerButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBorderBrushPressed
        {
            get { return (object)GetValue(TimePickerButtonBorderBrushPressedProperty); }
            set { SetValue(TimePickerButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBorderBrushPressedProperty =
            DependencyProperty.Register("TimePickerButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBorderBrushDisabled
        {
            get { return (object)GetValue(TimePickerButtonBorderBrushDisabledProperty); }
            set { SetValue(TimePickerButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("TimePickerButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBackground
        {
            get { return (object)GetValue(TimePickerButtonBackgroundProperty); }
            set { SetValue(TimePickerButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBackgroundProperty =
            DependencyProperty.Register("TimePickerButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBackgroundPointerOver
        {
            get { return (object)GetValue(TimePickerButtonBackgroundPointerOverProperty); }
            set { SetValue(TimePickerButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("TimePickerButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBackgroundPressed
        {
            get { return (object)GetValue(TimePickerButtonBackgroundPressedProperty); }
            set { SetValue(TimePickerButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBackgroundPressedProperty =
            DependencyProperty.Register("TimePickerButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBackgroundDisabled
        {
            get { return (object)GetValue(TimePickerButtonBackgroundDisabledProperty); }
            set { SetValue(TimePickerButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBackgroundDisabledProperty =
            DependencyProperty.Register("TimePickerButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonBackgroundFocused
        {
            get { return (object)GetValue(TimePickerButtonBackgroundFocusedProperty); }
            set { SetValue(TimePickerButtonBackgroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonBackgroundFocusedProperty =
            DependencyProperty.Register("TimePickerButtonBackgroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonForeground
        {
            get { return (object)GetValue(TimePickerButtonForegroundProperty); }
            set { SetValue(TimePickerButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonForegroundProperty =
            DependencyProperty.Register("TimePickerButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonForegroundPointerOver
        {
            get { return (object)GetValue(TimePickerButtonForegroundPointerOverProperty); }
            set { SetValue(TimePickerButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonForegroundPointerOverProperty =
            DependencyProperty.Register("TimePickerButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonForegroundPressed
        {
            get { return (object)GetValue(TimePickerButtonForegroundPressedProperty); }
            set { SetValue(TimePickerButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonForegroundPressedProperty =
            DependencyProperty.Register("TimePickerButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonForegroundDisabled
        {
            get { return (object)GetValue(TimePickerButtonForegroundDisabledProperty); }
            set { SetValue(TimePickerButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonForegroundDisabledProperty =
            DependencyProperty.Register("TimePickerButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerButtonForegroundFocused
        {
            get { return (object)GetValue(TimePickerButtonForegroundFocusedProperty); }
            set { SetValue(TimePickerButtonForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty TimePickerButtonForegroundFocusedProperty =
            DependencyProperty.Register("TimePickerButtonForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerFlyoutPresenterBackground
        {
            get { return (object)GetValue(TimePickerFlyoutPresenterBackgroundProperty); }
            set { SetValue(TimePickerFlyoutPresenterBackgroundProperty, value); }
        }

        public static readonly DependencyProperty TimePickerFlyoutPresenterBackgroundProperty =
            DependencyProperty.Register("TimePickerFlyoutPresenterBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerFlyoutPresenterBorderBrush
        {
            get { return (object)GetValue(TimePickerFlyoutPresenterBorderBrushProperty); }
            set { SetValue(TimePickerFlyoutPresenterBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty TimePickerFlyoutPresenterBorderBrushProperty =
            DependencyProperty.Register("TimePickerFlyoutPresenterBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerFlyoutPresenterSpacerFill
        {
            get { return (object)GetValue(TimePickerFlyoutPresenterSpacerFillProperty); }
            set { SetValue(TimePickerFlyoutPresenterSpacerFillProperty, value); }
        }

        public static readonly DependencyProperty TimePickerFlyoutPresenterSpacerFillProperty =
            DependencyProperty.Register("TimePickerFlyoutPresenterSpacerFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerFlyoutPresenterHighlightFill
        {
            get { return (object)GetValue(TimePickerFlyoutPresenterHighlightFillProperty); }
            set { SetValue(TimePickerFlyoutPresenterHighlightFillProperty, value); }
        }

        public static readonly DependencyProperty TimePickerFlyoutPresenterHighlightFillProperty =
            DependencyProperty.Register("TimePickerFlyoutPresenterHighlightFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorButtonBackground
        {
            get { return (object)GetValue(LoopingSelectorButtonBackgroundProperty); }
            set { SetValue(LoopingSelectorButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorButtonBackgroundProperty =
            DependencyProperty.Register("LoopingSelectorButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemForeground
        {
            get { return (object)GetValue(LoopingSelectorItemForegroundProperty); }
            set { SetValue(LoopingSelectorItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemForegroundProperty =
            DependencyProperty.Register("LoopingSelectorItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemForegroundSelected
        {
            get { return (object)GetValue(LoopingSelectorItemForegroundSelectedProperty); }
            set { SetValue(LoopingSelectorItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemForegroundSelectedProperty =
            DependencyProperty.Register("LoopingSelectorItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemForegroundPointerOver
        {
            get { return (object)GetValue(LoopingSelectorItemForegroundPointerOverProperty); }
            set { SetValue(LoopingSelectorItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemForegroundPointerOverProperty =
            DependencyProperty.Register("LoopingSelectorItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemForegroundPressed
        {
            get { return (object)GetValue(LoopingSelectorItemForegroundPressedProperty); }
            set { SetValue(LoopingSelectorItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemForegroundPressedProperty =
            DependencyProperty.Register("LoopingSelectorItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemBackgroundPointerOver
        {
            get { return (object)GetValue(LoopingSelectorItemBackgroundPointerOverProperty); }
            set { SetValue(LoopingSelectorItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemBackgroundPointerOverProperty =
            DependencyProperty.Register("LoopingSelectorItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object LoopingSelectorItemBackgroundPressed
        {
            get { return (object)GetValue(LoopingSelectorItemBackgroundPressedProperty); }
            set { SetValue(LoopingSelectorItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty LoopingSelectorItemBackgroundPressedProperty =
            DependencyProperty.Register("LoopingSelectorItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlForeground
        {
            get { return (object)GetValue(TextControlForegroundProperty); }
            set { SetValue(TextControlForegroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlForegroundProperty =
            DependencyProperty.Register("TextControlForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlForegroundPointerOver
        {
            get { return (object)GetValue(TextControlForegroundPointerOverProperty); }
            set { SetValue(TextControlForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TextControlForegroundPointerOverProperty =
            DependencyProperty.Register("TextControlForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlForegroundFocused
        {
            get { return (object)GetValue(TextControlForegroundFocusedProperty); }
            set { SetValue(TextControlForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty TextControlForegroundFocusedProperty =
            DependencyProperty.Register("TextControlForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlForegroundDisabled
        {
            get { return (object)GetValue(TextControlForegroundDisabledProperty); }
            set { SetValue(TextControlForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TextControlForegroundDisabledProperty =
            DependencyProperty.Register("TextControlForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBackground
        {
            get { return (object)GetValue(TextControlBackgroundProperty); }
            set { SetValue(TextControlBackgroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlBackgroundProperty =
            DependencyProperty.Register("TextControlBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBackgroundPointerOver
        {
            get { return (object)GetValue(TextControlBackgroundPointerOverProperty); }
            set { SetValue(TextControlBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TextControlBackgroundPointerOverProperty =
            DependencyProperty.Register("TextControlBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBackgroundFocused
        {
            get { return (object)GetValue(TextControlBackgroundFocusedProperty); }
            set { SetValue(TextControlBackgroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty TextControlBackgroundFocusedProperty =
            DependencyProperty.Register("TextControlBackgroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBackgroundDisabled
        {
            get { return (object)GetValue(TextControlBackgroundDisabledProperty); }
            set { SetValue(TextControlBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TextControlBackgroundDisabledProperty =
            DependencyProperty.Register("TextControlBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBorderBrush
        {
            get { return (object)GetValue(TextControlBorderBrushProperty); }
            set { SetValue(TextControlBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty TextControlBorderBrushProperty =
            DependencyProperty.Register("TextControlBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBorderBrushPointerOver
        {
            get { return (object)GetValue(TextControlBorderBrushPointerOverProperty); }
            set { SetValue(TextControlBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TextControlBorderBrushPointerOverProperty =
            DependencyProperty.Register("TextControlBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBorderBrushFocused
        {
            get { return (object)GetValue(TextControlBorderBrushFocusedProperty); }
            set { SetValue(TextControlBorderBrushFocusedProperty, value); }
        }

        public static readonly DependencyProperty TextControlBorderBrushFocusedProperty =
            DependencyProperty.Register("TextControlBorderBrushFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlBorderBrushDisabled
        {
            get { return (object)GetValue(TextControlBorderBrushDisabledProperty); }
            set { SetValue(TextControlBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty TextControlBorderBrushDisabledProperty =
            DependencyProperty.Register("TextControlBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlPlaceholderForeground
        {
            get { return (object)GetValue(TextControlPlaceholderForegroundProperty); }
            set { SetValue(TextControlPlaceholderForegroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlPlaceholderForegroundProperty =
            DependencyProperty.Register("TextControlPlaceholderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlPlaceholderForegroundPointerOver
        {
            get { return (object)GetValue(TextControlPlaceholderForegroundPointerOverProperty); }
            set { SetValue(TextControlPlaceholderForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TextControlPlaceholderForegroundPointerOverProperty =
            DependencyProperty.Register("TextControlPlaceholderForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlPlaceholderForegroundFocused
        {
            get { return (object)GetValue(TextControlPlaceholderForegroundFocusedProperty); }
            set { SetValue(TextControlPlaceholderForegroundFocusedProperty, value); }
        }

        public static readonly DependencyProperty TextControlPlaceholderForegroundFocusedProperty =
            DependencyProperty.Register("TextControlPlaceholderForegroundFocused", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlPlaceholderForegroundDisabled
        {
            get { return (object)GetValue(TextControlPlaceholderForegroundDisabledProperty); }
            set { SetValue(TextControlPlaceholderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TextControlPlaceholderForegroundDisabledProperty =
            DependencyProperty.Register("TextControlPlaceholderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlHeaderForeground
        {
            get { return (object)GetValue(TextControlHeaderForegroundProperty); }
            set { SetValue(TextControlHeaderForegroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlHeaderForegroundProperty =
            DependencyProperty.Register("TextControlHeaderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlHeaderForegroundDisabled
        {
            get { return (object)GetValue(TextControlHeaderForegroundDisabledProperty); }
            set { SetValue(TextControlHeaderForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TextControlHeaderForegroundDisabledProperty =
            DependencyProperty.Register("TextControlHeaderForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlSelectionHighlightColor
        {
            get { return (object)GetValue(TextControlSelectionHighlightColorProperty); }
            set { SetValue(TextControlSelectionHighlightColorProperty, value); }
        }

        public static readonly DependencyProperty TextControlSelectionHighlightColorProperty =
            DependencyProperty.Register("TextControlSelectionHighlightColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlButtonBackgroundPressed
        {
            get { return (object)GetValue(TextControlButtonBackgroundPressedProperty); }
            set { SetValue(TextControlButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TextControlButtonBackgroundPressedProperty =
            DependencyProperty.Register("TextControlButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlButtonForeground
        {
            get { return (object)GetValue(TextControlButtonForegroundProperty); }
            set { SetValue(TextControlButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlButtonForegroundProperty =
            DependencyProperty.Register("TextControlButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlButtonForegroundPointerOver
        {
            get { return (object)GetValue(TextControlButtonForegroundPointerOverProperty); }
            set { SetValue(TextControlButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TextControlButtonForegroundPointerOverProperty =
            DependencyProperty.Register("TextControlButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlButtonForegroundPressed
        {
            get { return (object)GetValue(TextControlButtonForegroundPressedProperty); }
            set { SetValue(TextControlButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TextControlButtonForegroundPressedProperty =
            DependencyProperty.Register("TextControlButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ContentLinkForegroundColor
        {
            get { return (object)GetValue(ContentLinkForegroundColorProperty); }
            set { SetValue(ContentLinkForegroundColorProperty, value); }
        }

        public static readonly DependencyProperty ContentLinkForegroundColorProperty =
            DependencyProperty.Register("ContentLinkForegroundColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ContentLinkBackgroundColor
        {
            get { return (object)GetValue(ContentLinkBackgroundColorProperty); }
            set { SetValue(ContentLinkBackgroundColorProperty, value); }
        }

        public static readonly DependencyProperty ContentLinkBackgroundColorProperty =
            DependencyProperty.Register("ContentLinkBackgroundColor", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TextControlHighlighterForeground
        {
            get { return (object)GetValue(TextControlHighlighterForegroundProperty); }
            set { SetValue(TextControlHighlighterForegroundProperty, value); }
        }

        public static readonly DependencyProperty TextControlHighlighterForegroundProperty =
            DependencyProperty.Register("TextControlHighlighterForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlyoutPresenterBackground
        {
            get { return (object)GetValue(FlyoutPresenterBackgroundProperty); }
            set { SetValue(FlyoutPresenterBackgroundProperty, value); }
        }

        public static readonly DependencyProperty FlyoutPresenterBackgroundProperty =
            DependencyProperty.Register("FlyoutPresenterBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlyoutBorderThemeBrush
        {
            get { return (object)GetValue(FlyoutBorderThemeBrushProperty); }
            set { SetValue(FlyoutBorderThemeBrushProperty, value); }
        }

        public static readonly DependencyProperty FlyoutBorderThemeBrushProperty =
            DependencyProperty.Register("FlyoutBorderThemeBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemBackgroundPointerOver
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemBackgroundPointerOverProperty); }
            set { SetValue(ToggleMenuFlyoutItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemBackgroundPointerOverProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemBackgroundPressed
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemBackgroundPressedProperty); }
            set { SetValue(ToggleMenuFlyoutItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemBackgroundPressedProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemForeground
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemForegroundProperty); }
            set { SetValue(ToggleMenuFlyoutItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemForegroundProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemForegroundPointerOver
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemForegroundPointerOverProperty); }
            set { SetValue(ToggleMenuFlyoutItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemForegroundPointerOverProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemForegroundPressed
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemForegroundPressedProperty); }
            set { SetValue(ToggleMenuFlyoutItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemForegroundPressedProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemForegroundDisabled
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemForegroundDisabledProperty); }
            set { SetValue(ToggleMenuFlyoutItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemForegroundDisabledProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemKeyboardAcceleratorTextForeground
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundProperty); }
            set { SetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemKeyboardAcceleratorTextForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty); }
            set { SetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressed
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty); }
            set { SetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty); }
            set { SetValue(ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemCheckGlyphForeground
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemCheckGlyphForegroundProperty); }
            set { SetValue(ToggleMenuFlyoutItemCheckGlyphForegroundProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemCheckGlyphForegroundProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemCheckGlyphForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemCheckGlyphForegroundPointerOver
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemCheckGlyphForegroundPointerOverProperty); }
            set { SetValue(ToggleMenuFlyoutItemCheckGlyphForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemCheckGlyphForegroundPointerOverProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemCheckGlyphForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemCheckGlyphForegroundPressed
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemCheckGlyphForegroundPressedProperty); }
            set { SetValue(ToggleMenuFlyoutItemCheckGlyphForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemCheckGlyphForegroundPressedProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemCheckGlyphForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ToggleMenuFlyoutItemCheckGlyphForegroundDisabled
        {
            get { return (object)GetValue(ToggleMenuFlyoutItemCheckGlyphForegroundDisabledProperty); }
            set { SetValue(ToggleMenuFlyoutItemCheckGlyphForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ToggleMenuFlyoutItemCheckGlyphForegroundDisabledProperty =
            DependencyProperty.Register("ToggleMenuFlyoutItemCheckGlyphForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBackground
        {
            get { return (object)GetValue(PivotNextButtonBackgroundProperty); }
            set { SetValue(PivotNextButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBackgroundProperty =
            DependencyProperty.Register("PivotNextButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBackgroundPointerOver
        {
            get { return (object)GetValue(PivotNextButtonBackgroundPointerOverProperty); }
            set { SetValue(PivotNextButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("PivotNextButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBackgroundPressed
        {
            get { return (object)GetValue(PivotNextButtonBackgroundPressedProperty); }
            set { SetValue(PivotNextButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBackgroundPressedProperty =
            DependencyProperty.Register("PivotNextButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBorderBrush
        {
            get { return (object)GetValue(PivotNextButtonBorderBrushProperty); }
            set { SetValue(PivotNextButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBorderBrushProperty =
            DependencyProperty.Register("PivotNextButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(PivotNextButtonBorderBrushPointerOverProperty); }
            set { SetValue(PivotNextButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("PivotNextButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonBorderBrushPressed
        {
            get { return (object)GetValue(PivotNextButtonBorderBrushPressedProperty); }
            set { SetValue(PivotNextButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonBorderBrushPressedProperty =
            DependencyProperty.Register("PivotNextButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonForeground
        {
            get { return (object)GetValue(PivotNextButtonForegroundProperty); }
            set { SetValue(PivotNextButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonForegroundProperty =
            DependencyProperty.Register("PivotNextButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonForegroundPointerOver
        {
            get { return (object)GetValue(PivotNextButtonForegroundPointerOverProperty); }
            set { SetValue(PivotNextButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonForegroundPointerOverProperty =
            DependencyProperty.Register("PivotNextButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotNextButtonForegroundPressed
        {
            get { return (object)GetValue(PivotNextButtonForegroundPressedProperty); }
            set { SetValue(PivotNextButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotNextButtonForegroundPressedProperty =
            DependencyProperty.Register("PivotNextButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBackground
        {
            get { return (object)GetValue(PivotPreviousButtonBackgroundProperty); }
            set { SetValue(PivotPreviousButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBackgroundProperty =
            DependencyProperty.Register("PivotPreviousButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBackgroundPointerOver
        {
            get { return (object)GetValue(PivotPreviousButtonBackgroundPointerOverProperty); }
            set { SetValue(PivotPreviousButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("PivotPreviousButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBackgroundPressed
        {
            get { return (object)GetValue(PivotPreviousButtonBackgroundPressedProperty); }
            set { SetValue(PivotPreviousButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBackgroundPressedProperty =
            DependencyProperty.Register("PivotPreviousButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBorderBrush
        {
            get { return (object)GetValue(PivotPreviousButtonBorderBrushProperty); }
            set { SetValue(PivotPreviousButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBorderBrushProperty =
            DependencyProperty.Register("PivotPreviousButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(PivotPreviousButtonBorderBrushPointerOverProperty); }
            set { SetValue(PivotPreviousButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("PivotPreviousButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonBorderBrushPressed
        {
            get { return (object)GetValue(PivotPreviousButtonBorderBrushPressedProperty); }
            set { SetValue(PivotPreviousButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonBorderBrushPressedProperty =
            DependencyProperty.Register("PivotPreviousButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonForeground
        {
            get { return (object)GetValue(PivotPreviousButtonForegroundProperty); }
            set { SetValue(PivotPreviousButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonForegroundProperty =
            DependencyProperty.Register("PivotPreviousButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonForegroundPointerOver
        {
            get { return (object)GetValue(PivotPreviousButtonForegroundPointerOverProperty); }
            set { SetValue(PivotPreviousButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonForegroundPointerOverProperty =
            DependencyProperty.Register("PivotPreviousButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotPreviousButtonForegroundPressed
        {
            get { return (object)GetValue(PivotPreviousButtonForegroundPressedProperty); }
            set { SetValue(PivotPreviousButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotPreviousButtonForegroundPressedProperty =
            DependencyProperty.Register("PivotPreviousButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemBackgroundUnselectedPointerOver
        {
            get { return (object)GetValue(PivotHeaderItemBackgroundUnselectedPointerOverProperty); }
            set { SetValue(PivotHeaderItemBackgroundUnselectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemBackgroundUnselectedPointerOverProperty =
            DependencyProperty.Register("PivotHeaderItemBackgroundUnselectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemBackgroundUnselectedPressed
        {
            get { return (object)GetValue(PivotHeaderItemBackgroundUnselectedPressedProperty); }
            set { SetValue(PivotHeaderItemBackgroundUnselectedPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemBackgroundUnselectedPressedProperty =
            DependencyProperty.Register("PivotHeaderItemBackgroundUnselectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemBackgroundSelected
        {
            get { return (object)GetValue(PivotHeaderItemBackgroundSelectedProperty); }
            set { SetValue(PivotHeaderItemBackgroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemBackgroundSelectedProperty =
            DependencyProperty.Register("PivotHeaderItemBackgroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemBackgroundSelectedPointerOver
        {
            get { return (object)GetValue(PivotHeaderItemBackgroundSelectedPointerOverProperty); }
            set { SetValue(PivotHeaderItemBackgroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemBackgroundSelectedPointerOverProperty =
            DependencyProperty.Register("PivotHeaderItemBackgroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemBackgroundSelectedPressed
        {
            get { return (object)GetValue(PivotHeaderItemBackgroundSelectedPressedProperty); }
            set { SetValue(PivotHeaderItemBackgroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemBackgroundSelectedPressedProperty =
            DependencyProperty.Register("PivotHeaderItemBackgroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundUnselected
        {
            get { return (object)GetValue(PivotHeaderItemForegroundUnselectedProperty); }
            set { SetValue(PivotHeaderItemForegroundUnselectedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundUnselectedProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundUnselected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundUnselectedPointerOver
        {
            get { return (object)GetValue(PivotHeaderItemForegroundUnselectedPointerOverProperty); }
            set { SetValue(PivotHeaderItemForegroundUnselectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundUnselectedPointerOverProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundUnselectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundUnselectedPressed
        {
            get { return (object)GetValue(PivotHeaderItemForegroundUnselectedPressedProperty); }
            set { SetValue(PivotHeaderItemForegroundUnselectedPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundUnselectedPressedProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundUnselectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundSelected
        {
            get { return (object)GetValue(PivotHeaderItemForegroundSelectedProperty); }
            set { SetValue(PivotHeaderItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundSelectedProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundSelectedPointerOver
        {
            get { return (object)GetValue(PivotHeaderItemForegroundSelectedPointerOverProperty); }
            set { SetValue(PivotHeaderItemForegroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundSelectedPointerOverProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundSelectedPressed
        {
            get { return (object)GetValue(PivotHeaderItemForegroundSelectedPressedProperty); }
            set { SetValue(PivotHeaderItemForegroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundSelectedPressedProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemForegroundDisabled
        {
            get { return (object)GetValue(PivotHeaderItemForegroundDisabledProperty); }
            set { SetValue(PivotHeaderItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemForegroundDisabledProperty =
            DependencyProperty.Register("PivotHeaderItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemFocusPipeFill
        {
            get { return (object)GetValue(PivotHeaderItemFocusPipeFillProperty); }
            set { SetValue(PivotHeaderItemFocusPipeFillProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemFocusPipeFillProperty =
            DependencyProperty.Register("PivotHeaderItemFocusPipeFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PivotHeaderItemSelectedPipeFill
        {
            get { return (object)GetValue(PivotHeaderItemSelectedPipeFillProperty); }
            set { SetValue(PivotHeaderItemSelectedPipeFillProperty, value); }
        }

        public static readonly DependencyProperty PivotHeaderItemSelectedPipeFillProperty =
            DependencyProperty.Register("PivotHeaderItemSelectedPipeFill", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewHeaderItemDividerStroke
        {
            get { return (object)GetValue(GridViewHeaderItemDividerStrokeProperty); }
            set { SetValue(GridViewHeaderItemDividerStrokeProperty, value); }
        }

        public static readonly DependencyProperty GridViewHeaderItemDividerStrokeProperty =
            DependencyProperty.Register("GridViewHeaderItemDividerStroke", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemBackgroundPointerOver
        {
            get { return (object)GetValue(GridViewItemBackgroundPointerOverProperty); }
            set { SetValue(GridViewItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemBackgroundPointerOverProperty =
            DependencyProperty.Register("GridViewItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemBackgroundPressed
        {
            get { return (object)GetValue(GridViewItemBackgroundPressedProperty); }
            set { SetValue(GridViewItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemBackgroundPressedProperty =
            DependencyProperty.Register("GridViewItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemBackgroundSelected
        {
            get { return (object)GetValue(GridViewItemBackgroundSelectedProperty); }
            set { SetValue(GridViewItemBackgroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemBackgroundSelectedProperty =
            DependencyProperty.Register("GridViewItemBackgroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemBackgroundSelectedPointerOver
        {
            get { return (object)GetValue(GridViewItemBackgroundSelectedPointerOverProperty); }
            set { SetValue(GridViewItemBackgroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemBackgroundSelectedPointerOverProperty =
            DependencyProperty.Register("GridViewItemBackgroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemBackgroundSelectedPressed
        {
            get { return (object)GetValue(GridViewItemBackgroundSelectedPressedProperty); }
            set { SetValue(GridViewItemBackgroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemBackgroundSelectedPressedProperty =
            DependencyProperty.Register("GridViewItemBackgroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemForeground
        {
            get { return (object)GetValue(GridViewItemForegroundProperty); }
            set { SetValue(GridViewItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemForegroundProperty =
            DependencyProperty.Register("GridViewItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemForegroundPointerOver
        {
            get { return (object)GetValue(GridViewItemForegroundPointerOverProperty); }
            set { SetValue(GridViewItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemForegroundPointerOverProperty =
            DependencyProperty.Register("GridViewItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemForegroundSelected
        {
            get { return (object)GetValue(GridViewItemForegroundSelectedProperty); }
            set { SetValue(GridViewItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemForegroundSelectedProperty =
            DependencyProperty.Register("GridViewItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemFocusVisualPrimaryBrush
        {
            get { return (object)GetValue(GridViewItemFocusVisualPrimaryBrushProperty); }
            set { SetValue(GridViewItemFocusVisualPrimaryBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemFocusVisualPrimaryBrushProperty =
            DependencyProperty.Register("GridViewItemFocusVisualPrimaryBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemFocusVisualSecondaryBrush
        {
            get { return (object)GetValue(GridViewItemFocusVisualSecondaryBrushProperty); }
            set { SetValue(GridViewItemFocusVisualSecondaryBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemFocusVisualSecondaryBrushProperty =
            DependencyProperty.Register("GridViewItemFocusVisualSecondaryBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemFocusBorderBrush
        {
            get { return (object)GetValue(GridViewItemFocusBorderBrushProperty); }
            set { SetValue(GridViewItemFocusBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemFocusBorderBrushProperty =
            DependencyProperty.Register("GridViewItemFocusBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemFocusSecondaryBorderBrush
        {
            get { return (object)GetValue(GridViewItemFocusSecondaryBorderBrushProperty); }
            set { SetValue(GridViewItemFocusSecondaryBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemFocusSecondaryBorderBrushProperty =
            DependencyProperty.Register("GridViewItemFocusSecondaryBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemCheckBrush
        {
            get { return (object)GetValue(GridViewItemCheckBrushProperty); }
            set { SetValue(GridViewItemCheckBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemCheckBrushProperty =
            DependencyProperty.Register("GridViewItemCheckBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemCheckBoxBrush
        {
            get { return (object)GetValue(GridViewItemCheckBoxBrushProperty); }
            set { SetValue(GridViewItemCheckBoxBrushProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemCheckBoxBrushProperty =
            DependencyProperty.Register("GridViewItemCheckBoxBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemDragForeground
        {
            get { return (object)GetValue(GridViewItemDragForegroundProperty); }
            set { SetValue(GridViewItemDragForegroundProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemDragForegroundProperty =
            DependencyProperty.Register("GridViewItemDragForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object GridViewItemPlaceholderBackground
        {
            get { return (object)GetValue(GridViewItemPlaceholderBackgroundProperty); }
            set { SetValue(GridViewItemPlaceholderBackgroundProperty, value); }
        }

        public static readonly DependencyProperty GridViewItemPlaceholderBackgroundProperty =
            DependencyProperty.Register("GridViewItemPlaceholderBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MediaTransportControlsPanelBackground
        {
            get { return (object)GetValue(MediaTransportControlsPanelBackgroundProperty); }
            set { SetValue(MediaTransportControlsPanelBackgroundProperty, value); }
        }

        public static readonly DependencyProperty MediaTransportControlsPanelBackgroundProperty =
            DependencyProperty.Register("MediaTransportControlsPanelBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MediaTransportControlsFlyoutBackground
        {
            get { return (object)GetValue(MediaTransportControlsFlyoutBackgroundProperty); }
            set { SetValue(MediaTransportControlsFlyoutBackgroundProperty, value); }
        }

        public static readonly DependencyProperty MediaTransportControlsFlyoutBackgroundProperty =
            DependencyProperty.Register("MediaTransportControlsFlyoutBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarLightDismissOverlayBackground
        {
            get { return (object)GetValue(AppBarLightDismissOverlayBackgroundProperty); }
            set { SetValue(AppBarLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("AppBarLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CalendarDatePickerLightDismissOverlayBackground
        {
            get { return (object)GetValue(CalendarDatePickerLightDismissOverlayBackgroundProperty); }
            set { SetValue(CalendarDatePickerLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CalendarDatePickerLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("CalendarDatePickerLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ComboBoxLightDismissOverlayBackground
        {
            get { return (object)GetValue(ComboBoxLightDismissOverlayBackgroundProperty); }
            set { SetValue(ComboBoxLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ComboBoxLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("ComboBoxLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object DatePickerLightDismissOverlayBackground
        {
            get { return (object)GetValue(DatePickerLightDismissOverlayBackgroundProperty); }
            set { SetValue(DatePickerLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty DatePickerLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("DatePickerLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object FlyoutLightDismissOverlayBackground
        {
            get { return (object)GetValue(FlyoutLightDismissOverlayBackgroundProperty); }
            set { SetValue(FlyoutLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty FlyoutLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("FlyoutLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object PopupLightDismissOverlayBackground
        {
            get { return (object)GetValue(PopupLightDismissOverlayBackgroundProperty); }
            set { SetValue(PopupLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty PopupLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("PopupLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitViewLightDismissOverlayBackground
        {
            get { return (object)GetValue(SplitViewLightDismissOverlayBackgroundProperty); }
            set { SetValue(SplitViewLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SplitViewLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("SplitViewLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TimePickerLightDismissOverlayBackground
        {
            get { return (object)GetValue(TimePickerLightDismissOverlayBackgroundProperty); }
            set { SetValue(TimePickerLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty TimePickerLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("TimePickerLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object JumpListDefaultEnabledBackground
        {
            get { return (object)GetValue(JumpListDefaultEnabledBackgroundProperty); }
            set { SetValue(JumpListDefaultEnabledBackgroundProperty, value); }
        }

        public static readonly DependencyProperty JumpListDefaultEnabledBackgroundProperty =
            DependencyProperty.Register("JumpListDefaultEnabledBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object JumpListDefaultEnabledForeground
        {
            get { return (object)GetValue(JumpListDefaultEnabledForegroundProperty); }
            set { SetValue(JumpListDefaultEnabledForegroundProperty, value); }
        }

        public static readonly DependencyProperty JumpListDefaultEnabledForegroundProperty =
            DependencyProperty.Register("JumpListDefaultEnabledForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object JumpListDefaultDisabledBackground
        {
            get { return (object)GetValue(JumpListDefaultDisabledBackgroundProperty); }
            set { SetValue(JumpListDefaultDisabledBackgroundProperty, value); }
        }

        public static readonly DependencyProperty JumpListDefaultDisabledBackgroundProperty =
            DependencyProperty.Register("JumpListDefaultDisabledBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object JumpListDefaultDisabledForeground
        {
            get { return (object)GetValue(JumpListDefaultDisabledForegroundProperty); }
            set { SetValue(JumpListDefaultDisabledForegroundProperty, value); }
        }

        public static readonly DependencyProperty JumpListDefaultDisabledForegroundProperty =
            DependencyProperty.Register("JumpListDefaultDisabledForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object KeyTipForeground
        {
            get { return (object)GetValue(KeyTipForegroundProperty); }
            set { SetValue(KeyTipForegroundProperty, value); }
        }

        public static readonly DependencyProperty KeyTipForegroundProperty =
            DependencyProperty.Register("KeyTipForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object KeyTipBackground
        {
            get { return (object)GetValue(KeyTipBackgroundProperty); }
            set { SetValue(KeyTipBackgroundProperty, value); }
        }

        public static readonly DependencyProperty KeyTipBackgroundProperty =
            DependencyProperty.Register("KeyTipBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object KeyTipBorderBrush
        {
            get { return (object)GetValue(KeyTipBorderBrushProperty); }
            set { SetValue(KeyTipBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty KeyTipBorderBrushProperty =
            DependencyProperty.Register("KeyTipBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutPresenterBackground
        {
            get { return (object)GetValue(MenuFlyoutPresenterBackgroundProperty); }
            set { SetValue(MenuFlyoutPresenterBackgroundProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutPresenterBackgroundProperty =
            DependencyProperty.Register("MenuFlyoutPresenterBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutPresenterBorderBrush
        {
            get { return (object)GetValue(MenuFlyoutPresenterBorderBrushProperty); }
            set { SetValue(MenuFlyoutPresenterBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutPresenterBorderBrushProperty =
            DependencyProperty.Register("MenuFlyoutPresenterBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemBackgroundPointerOver
        {
            get { return (object)GetValue(MenuFlyoutItemBackgroundPointerOverProperty); }
            set { SetValue(MenuFlyoutItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemBackgroundPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemBackgroundPressed
        {
            get { return (object)GetValue(MenuFlyoutItemBackgroundPressedProperty); }
            set { SetValue(MenuFlyoutItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemBackgroundPressedProperty =
            DependencyProperty.Register("MenuFlyoutItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemForeground
        {
            get { return (object)GetValue(MenuFlyoutItemForegroundProperty); }
            set { SetValue(MenuFlyoutItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemForegroundProperty =
            DependencyProperty.Register("MenuFlyoutItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemForegroundPointerOver
        {
            get { return (object)GetValue(MenuFlyoutItemForegroundPointerOverProperty); }
            set { SetValue(MenuFlyoutItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemForegroundPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemForegroundPressed
        {
            get { return (object)GetValue(MenuFlyoutItemForegroundPressedProperty); }
            set { SetValue(MenuFlyoutItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemForegroundPressedProperty =
            DependencyProperty.Register("MenuFlyoutItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemForegroundDisabled
        {
            get { return (object)GetValue(MenuFlyoutItemForegroundDisabledProperty); }
            set { SetValue(MenuFlyoutItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemForegroundDisabledProperty =
            DependencyProperty.Register("MenuFlyoutItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemBackgroundPointerOver
        {
            get { return (object)GetValue(MenuFlyoutSubItemBackgroundPointerOverProperty); }
            set { SetValue(MenuFlyoutSubItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemBackgroundPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutSubItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemBackgroundPressed
        {
            get { return (object)GetValue(MenuFlyoutSubItemBackgroundPressedProperty); }
            set { SetValue(MenuFlyoutSubItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemBackgroundPressedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemBackgroundSubMenuOpened
        {
            get { return (object)GetValue(MenuFlyoutSubItemBackgroundSubMenuOpenedProperty); }
            set { SetValue(MenuFlyoutSubItemBackgroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemBackgroundSubMenuOpenedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemBackgroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemForeground
        {
            get { return (object)GetValue(MenuFlyoutSubItemForegroundProperty); }
            set { SetValue(MenuFlyoutSubItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemForegroundProperty =
            DependencyProperty.Register("MenuFlyoutSubItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemForegroundPointerOver
        {
            get { return (object)GetValue(MenuFlyoutSubItemForegroundPointerOverProperty); }
            set { SetValue(MenuFlyoutSubItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemForegroundPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutSubItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemForegroundPressed
        {
            get { return (object)GetValue(MenuFlyoutSubItemForegroundPressedProperty); }
            set { SetValue(MenuFlyoutSubItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemForegroundPressedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemForegroundSubMenuOpened
        {
            get { return (object)GetValue(MenuFlyoutSubItemForegroundSubMenuOpenedProperty); }
            set { SetValue(MenuFlyoutSubItemForegroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemForegroundSubMenuOpenedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemForegroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemForegroundDisabled
        {
            get { return (object)GetValue(MenuFlyoutSubItemForegroundDisabledProperty); }
            set { SetValue(MenuFlyoutSubItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemForegroundDisabledProperty =
            DependencyProperty.Register("MenuFlyoutSubItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemChevron
        {
            get { return (object)GetValue(MenuFlyoutSubItemChevronProperty); }
            set { SetValue(MenuFlyoutSubItemChevronProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemChevronProperty =
            DependencyProperty.Register("MenuFlyoutSubItemChevron", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemChevronPointerOver
        {
            get { return (object)GetValue(MenuFlyoutSubItemChevronPointerOverProperty); }
            set { SetValue(MenuFlyoutSubItemChevronPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemChevronPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutSubItemChevronPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemChevronPressed
        {
            get { return (object)GetValue(MenuFlyoutSubItemChevronPressedProperty); }
            set { SetValue(MenuFlyoutSubItemChevronPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemChevronPressedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemChevronPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemChevronSubMenuOpened
        {
            get { return (object)GetValue(MenuFlyoutSubItemChevronSubMenuOpenedProperty); }
            set { SetValue(MenuFlyoutSubItemChevronSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemChevronSubMenuOpenedProperty =
            DependencyProperty.Register("MenuFlyoutSubItemChevronSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutSubItemChevronDisabled
        {
            get { return (object)GetValue(MenuFlyoutSubItemChevronDisabledProperty); }
            set { SetValue(MenuFlyoutSubItemChevronDisabledProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutSubItemChevronDisabledProperty =
            DependencyProperty.Register("MenuFlyoutSubItemChevronDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutLightDismissOverlayBackground
        {
            get { return (object)GetValue(MenuFlyoutLightDismissOverlayBackgroundProperty); }
            set { SetValue(MenuFlyoutLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("MenuFlyoutLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlUnselectedForeground
        {
            get { return (object)GetValue(RatingControlUnselectedForegroundProperty); }
            set { SetValue(RatingControlUnselectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlUnselectedForegroundProperty =
            DependencyProperty.Register("RatingControlUnselectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlSelectedForeground
        {
            get { return (object)GetValue(RatingControlSelectedForegroundProperty); }
            set { SetValue(RatingControlSelectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlSelectedForegroundProperty =
            DependencyProperty.Register("RatingControlSelectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlPlaceholderForeground
        {
            get { return (object)GetValue(RatingControlPlaceholderForegroundProperty); }
            set { SetValue(RatingControlPlaceholderForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlPlaceholderForegroundProperty =
            DependencyProperty.Register("RatingControlPlaceholderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlPointerOverPlaceholderForeground
        {
            get { return (object)GetValue(RatingControlPointerOverPlaceholderForegroundProperty); }
            set { SetValue(RatingControlPointerOverPlaceholderForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlPointerOverPlaceholderForegroundProperty =
            DependencyProperty.Register("RatingControlPointerOverPlaceholderForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlPointerOverUnselectedForeground
        {
            get { return (object)GetValue(RatingControlPointerOverUnselectedForegroundProperty); }
            set { SetValue(RatingControlPointerOverUnselectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlPointerOverUnselectedForegroundProperty =
            DependencyProperty.Register("RatingControlPointerOverUnselectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlPointerOverSelectedForeground
        {
            get { return (object)GetValue(RatingControlPointerOverSelectedForegroundProperty); }
            set { SetValue(RatingControlPointerOverSelectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlPointerOverSelectedForegroundProperty =
            DependencyProperty.Register("RatingControlPointerOverSelectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlDisabledSelectedForeground
        {
            get { return (object)GetValue(RatingControlDisabledSelectedForegroundProperty); }
            set { SetValue(RatingControlDisabledSelectedForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlDisabledSelectedForegroundProperty =
            DependencyProperty.Register("RatingControlDisabledSelectedForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object RatingControlCaptionForeground
        {
            get { return (object)GetValue(RatingControlCaptionForegroundProperty); }
            set { SetValue(RatingControlCaptionForegroundProperty, value); }
        }

        public static readonly DependencyProperty RatingControlCaptionForegroundProperty =
            DependencyProperty.Register("RatingControlCaptionForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForeground
        {
            get { return (object)GetValue(NavigationViewItemForegroundProperty); }
            set { SetValue(NavigationViewItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundProperty =
            DependencyProperty.Register("NavigationViewItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundPointerOver
        {
            get { return (object)GetValue(NavigationViewItemForegroundPointerOverProperty); }
            set { SetValue(NavigationViewItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundPointerOverProperty =
            DependencyProperty.Register("NavigationViewItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundPressed
        {
            get { return (object)GetValue(NavigationViewItemForegroundPressedProperty); }
            set { SetValue(NavigationViewItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundPressedProperty =
            DependencyProperty.Register("NavigationViewItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundDisabled
        {
            get { return (object)GetValue(NavigationViewItemForegroundDisabledProperty); }
            set { SetValue(NavigationViewItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundDisabledProperty =
            DependencyProperty.Register("NavigationViewItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundChecked
        {
            get { return (object)GetValue(NavigationViewItemForegroundCheckedProperty); }
            set { SetValue(NavigationViewItemForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundCheckedProperty =
            DependencyProperty.Register("NavigationViewItemForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundCheckedPointerOver
        {
            get { return (object)GetValue(NavigationViewItemForegroundCheckedPointerOverProperty); }
            set { SetValue(NavigationViewItemForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("NavigationViewItemForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundCheckedPressed
        {
            get { return (object)GetValue(NavigationViewItemForegroundCheckedPressedProperty); }
            set { SetValue(NavigationViewItemForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundCheckedPressedProperty =
            DependencyProperty.Register("NavigationViewItemForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundCheckedDisabled
        {
            get { return (object)GetValue(NavigationViewItemForegroundCheckedDisabledProperty); }
            set { SetValue(NavigationViewItemForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundCheckedDisabledProperty =
            DependencyProperty.Register("NavigationViewItemForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundSelected
        {
            get { return (object)GetValue(NavigationViewItemForegroundSelectedProperty); }
            set { SetValue(NavigationViewItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundSelectedProperty =
            DependencyProperty.Register("NavigationViewItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundSelectedPointerOver
        {
            get { return (object)GetValue(NavigationViewItemForegroundSelectedPointerOverProperty); }
            set { SetValue(NavigationViewItemForegroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundSelectedPointerOverProperty =
            DependencyProperty.Register("NavigationViewItemForegroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundSelectedPressed
        {
            get { return (object)GetValue(NavigationViewItemForegroundSelectedPressedProperty); }
            set { SetValue(NavigationViewItemForegroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundSelectedPressedProperty =
            DependencyProperty.Register("NavigationViewItemForegroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewItemForegroundSelectedDisabled
        {
            get { return (object)GetValue(NavigationViewItemForegroundSelectedDisabledProperty); }
            set { SetValue(NavigationViewItemForegroundSelectedDisabledProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewItemForegroundSelectedDisabledProperty =
            DependencyProperty.Register("NavigationViewItemForegroundSelectedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object NavigationViewSelectionIndicatorForeground
        {
            get { return (object)GetValue(NavigationViewSelectionIndicatorForegroundProperty); }
            set { SetValue(NavigationViewSelectionIndicatorForegroundProperty, value); }
        }

        public static readonly DependencyProperty NavigationViewSelectionIndicatorForegroundProperty =
            DependencyProperty.Register("NavigationViewSelectionIndicatorForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TopNavigationViewItemForeground
        {
            get { return (object)GetValue(TopNavigationViewItemForegroundProperty); }
            set { SetValue(TopNavigationViewItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty TopNavigationViewItemForegroundProperty =
            DependencyProperty.Register("TopNavigationViewItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TopNavigationViewItemForegroundPointerOver
        {
            get { return (object)GetValue(TopNavigationViewItemForegroundPointerOverProperty); }
            set { SetValue(TopNavigationViewItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TopNavigationViewItemForegroundPointerOverProperty =
            DependencyProperty.Register("TopNavigationViewItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TopNavigationViewItemForegroundPressed
        {
            get { return (object)GetValue(TopNavigationViewItemForegroundPressedProperty); }
            set { SetValue(TopNavigationViewItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TopNavigationViewItemForegroundPressedProperty =
            DependencyProperty.Register("TopNavigationViewItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TopNavigationViewItemForegroundSelected
        {
            get { return (object)GetValue(TopNavigationViewItemForegroundSelectedProperty); }
            set { SetValue(TopNavigationViewItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty TopNavigationViewItemForegroundSelectedProperty =
            DependencyProperty.Register("TopNavigationViewItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TopNavigationViewItemForegroundDisabled
        {
            get { return (object)GetValue(TopNavigationViewItemForegroundDisabledProperty); }
            set { SetValue(TopNavigationViewItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TopNavigationViewItemForegroundDisabledProperty =
            DependencyProperty.Register("TopNavigationViewItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ColorPickerSliderThumbBackground
        {
            get { return (object)GetValue(ColorPickerSliderThumbBackgroundProperty); }
            set { SetValue(ColorPickerSliderThumbBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ColorPickerSliderThumbBackgroundProperty =
            DependencyProperty.Register("ColorPickerSliderThumbBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ColorPickerSliderThumbBackgroundPointerOver
        {
            get { return (object)GetValue(ColorPickerSliderThumbBackgroundPointerOverProperty); }
            set { SetValue(ColorPickerSliderThumbBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ColorPickerSliderThumbBackgroundPointerOverProperty =
            DependencyProperty.Register("ColorPickerSliderThumbBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ColorPickerSliderThumbBackgroundPressed
        {
            get { return (object)GetValue(ColorPickerSliderThumbBackgroundPressedProperty); }
            set { SetValue(ColorPickerSliderThumbBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ColorPickerSliderThumbBackgroundPressedProperty =
            DependencyProperty.Register("ColorPickerSliderThumbBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ColorPickerSliderThumbBackgroundDisabled
        {
            get { return (object)GetValue(ColorPickerSliderThumbBackgroundDisabledProperty); }
            set { SetValue(ColorPickerSliderThumbBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty ColorPickerSliderThumbBackgroundDisabledProperty =
            DependencyProperty.Register("ColorPickerSliderThumbBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ColorPickerSliderTrackFillDisabled
        {
            get { return (object)GetValue(ColorPickerSliderTrackFillDisabledProperty); }
            set { SetValue(ColorPickerSliderTrackFillDisabledProperty, value); }
        }

        public static readonly DependencyProperty ColorPickerSliderTrackFillDisabledProperty =
            DependencyProperty.Register("ColorPickerSliderTrackFillDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBackgroundPointerOver
        {
            get { return (object)GetValue(MenuBarItemBackgroundPointerOverProperty); }
            set { SetValue(MenuBarItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBackgroundPointerOverProperty =
            DependencyProperty.Register("MenuBarItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBackgroundPressed
        {
            get { return (object)GetValue(MenuBarItemBackgroundPressedProperty); }
            set { SetValue(MenuBarItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBackgroundPressedProperty =
            DependencyProperty.Register("MenuBarItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBackgroundSelected
        {
            get { return (object)GetValue(MenuBarItemBackgroundSelectedProperty); }
            set { SetValue(MenuBarItemBackgroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBackgroundSelectedProperty =
            DependencyProperty.Register("MenuBarItemBackgroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBorderBrush
        {
            get { return (object)GetValue(MenuBarItemBorderBrushProperty); }
            set { SetValue(MenuBarItemBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBorderBrushProperty =
            DependencyProperty.Register("MenuBarItemBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBorderBrushPointerOver
        {
            get { return (object)GetValue(MenuBarItemBorderBrushPointerOverProperty); }
            set { SetValue(MenuBarItemBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBorderBrushPointerOverProperty =
            DependencyProperty.Register("MenuBarItemBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBorderBrushPressed
        {
            get { return (object)GetValue(MenuBarItemBorderBrushPressedProperty); }
            set { SetValue(MenuBarItemBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBorderBrushPressedProperty =
            DependencyProperty.Register("MenuBarItemBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuBarItemBorderBrushSelected
        {
            get { return (object)GetValue(MenuBarItemBorderBrushSelectedProperty); }
            set { SetValue(MenuBarItemBorderBrushSelectedProperty, value); }
        }

        public static readonly DependencyProperty MenuBarItemBorderBrushSelectedProperty =
            DependencyProperty.Register("MenuBarItemBorderBrushSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonBackgroundPointerOver
        {
            get { return (object)GetValue(AppBarButtonBackgroundPointerOverProperty); }
            set { SetValue(AppBarButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("AppBarButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonBackgroundPressed
        {
            get { return (object)GetValue(AppBarButtonBackgroundPressedProperty); }
            set { SetValue(AppBarButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonBackgroundPressedProperty =
            DependencyProperty.Register("AppBarButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonForeground
        {
            get { return (object)GetValue(AppBarButtonForegroundProperty); }
            set { SetValue(AppBarButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonForegroundProperty =
            DependencyProperty.Register("AppBarButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonForegroundPointerOver
        {
            get { return (object)GetValue(AppBarButtonForegroundPointerOverProperty); }
            set { SetValue(AppBarButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonForegroundPressed
        {
            get { return (object)GetValue(AppBarButtonForegroundPressedProperty); }
            set { SetValue(AppBarButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonForegroundPressedProperty =
            DependencyProperty.Register("AppBarButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonForegroundDisabled
        {
            get { return (object)GetValue(AppBarButtonForegroundDisabledProperty); }
            set { SetValue(AppBarButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonForegroundDisabledProperty =
            DependencyProperty.Register("AppBarButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundChecked
        {
            get { return (object)GetValue(AppBarToggleButtonBackgroundCheckedProperty); }
            set { SetValue(AppBarToggleButtonBackgroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundCheckedProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundCheckedPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonBackgroundCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonBackgroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundCheckedPressed
        {
            get { return (object)GetValue(AppBarToggleButtonBackgroundCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonBackgroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundCheckedDisabled
        {
            get { return (object)GetValue(AppBarToggleButtonBackgroundCheckedDisabledProperty); }
            set { SetValue(AppBarToggleButtonBackgroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundCheckedDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundHighLightOverlayPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonBackgroundHighLightOverlayPointerOverProperty); }
            set { SetValue(AppBarToggleButtonBackgroundHighLightOverlayPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundHighLightOverlayPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundHighLightOverlayPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundHighLightOverlayPressed
        {
            get { return (object)GetValue(AppBarToggleButtonBackgroundHighLightOverlayPressedProperty); }
            set { SetValue(AppBarToggleButtonBackgroundHighLightOverlayPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundHighLightOverlayPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundHighLightOverlayPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundHighLightOverlayCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonBackgroundHighLightOverlayCheckedPressed
        {
            get { return (object)GetValue(AppBarToggleButtonBackgroundHighLightOverlayCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonBackgroundHighLightOverlayCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonBackgroundHighLightOverlayCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonBackgroundHighLightOverlayCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForeground
        {
            get { return (object)GetValue(AppBarToggleButtonForegroundProperty); }
            set { SetValue(AppBarToggleButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundProperty =
            DependencyProperty.Register("AppBarToggleButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonForegroundPointerOverProperty); }
            set { SetValue(AppBarToggleButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundPressed
        {
            get { return (object)GetValue(AppBarToggleButtonForegroundPressedProperty); }
            set { SetValue(AppBarToggleButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundDisabled
        {
            get { return (object)GetValue(AppBarToggleButtonForegroundDisabledProperty); }
            set { SetValue(AppBarToggleButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundChecked
        {
            get { return (object)GetValue(AppBarToggleButtonForegroundCheckedProperty); }
            set { SetValue(AppBarToggleButtonForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundCheckedProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundCheckedPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonForegroundCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundCheckedPressed
        {
            get { return (object)GetValue(AppBarToggleButtonForegroundCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonForegroundCheckedDisabled
        {
            get { return (object)GetValue(AppBarToggleButtonForegroundCheckedDisabledProperty); }
            set { SetValue(AppBarToggleButtonForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonForegroundCheckedDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForeground
        {
            get { return (object)GetValue(AppBarToggleButtonCheckGlyphForegroundProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonCheckGlyphForegroundPointerOverProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundPressed
        {
            get { return (object)GetValue(AppBarToggleButtonCheckGlyphForegroundPressedProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundDisabled
        {
            get { return (object)GetValue(AppBarToggleButtonCheckGlyphForegroundDisabledProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundChecked
        {
            get { return (object)GetValue(AppBarToggleButtonCheckGlyphForegroundCheckedProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundCheckedProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundCheckedPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonCheckGlyphForegroundCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundCheckedPressed
        {
            get { return (object)GetValue(AppBarToggleButtonCheckGlyphForegroundCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonCheckGlyphForegroundCheckedDisabled
        {
            get { return (object)GetValue(AppBarToggleButtonCheckGlyphForegroundCheckedDisabledProperty); }
            set { SetValue(AppBarToggleButtonCheckGlyphForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonCheckGlyphForegroundCheckedDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonCheckGlyphForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonOverflowLabelForegroundPointerOverProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundPressed
        {
            get { return (object)GetValue(AppBarToggleButtonOverflowLabelForegroundPressedProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundDisabled
        {
            get { return (object)GetValue(AppBarToggleButtonOverflowLabelForegroundDisabledProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundCheckedPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonOverflowLabelForegroundCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundCheckedPressed
        {
            get { return (object)GetValue(AppBarToggleButtonOverflowLabelForegroundCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonOverflowLabelForegroundCheckedDisabled
        {
            get { return (object)GetValue(AppBarToggleButtonOverflowLabelForegroundCheckedDisabledProperty); }
            set { SetValue(AppBarToggleButtonOverflowLabelForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonOverflowLabelForegroundCheckedDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonOverflowLabelForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarBackground
        {
            get { return (object)GetValue(CommandBarBackgroundProperty); }
            set { SetValue(CommandBarBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CommandBarBackgroundProperty =
            DependencyProperty.Register("CommandBarBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarForeground
        {
            get { return (object)GetValue(CommandBarForegroundProperty); }
            set { SetValue(CommandBarForegroundProperty, value); }
        }

        public static readonly DependencyProperty CommandBarForegroundProperty =
            DependencyProperty.Register("CommandBarForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarHighContrastBorder
        {
            get { return (object)GetValue(CommandBarHighContrastBorderProperty); }
            set { SetValue(CommandBarHighContrastBorderProperty, value); }
        }

        public static readonly DependencyProperty CommandBarHighContrastBorderProperty =
            DependencyProperty.Register("CommandBarHighContrastBorder", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarEllipsisIconForegroundDisabled
        {
            get { return (object)GetValue(CommandBarEllipsisIconForegroundDisabledProperty); }
            set { SetValue(CommandBarEllipsisIconForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty CommandBarEllipsisIconForegroundDisabledProperty =
            DependencyProperty.Register("CommandBarEllipsisIconForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarOverflowPresenterBackground
        {
            get { return (object)GetValue(CommandBarOverflowPresenterBackgroundProperty); }
            set { SetValue(CommandBarOverflowPresenterBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CommandBarOverflowPresenterBackgroundProperty =
            DependencyProperty.Register("CommandBarOverflowPresenterBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarOverflowPresenterBorderBrush
        {
            get { return (object)GetValue(CommandBarOverflowPresenterBorderBrushProperty); }
            set { SetValue(CommandBarOverflowPresenterBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty CommandBarOverflowPresenterBorderBrushProperty =
            DependencyProperty.Register("CommandBarOverflowPresenterBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object CommandBarLightDismissOverlayBackground
        {
            get { return (object)GetValue(CommandBarLightDismissOverlayBackgroundProperty); }
            set { SetValue(CommandBarLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty CommandBarLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("CommandBarLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemBackgroundPointerOver
        {
            get { return (object)GetValue(ListViewItemBackgroundPointerOverProperty); }
            set { SetValue(ListViewItemBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemBackgroundPointerOverProperty =
            DependencyProperty.Register("ListViewItemBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemBackgroundPressed
        {
            get { return (object)GetValue(ListViewItemBackgroundPressedProperty); }
            set { SetValue(ListViewItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemBackgroundPressedProperty =
            DependencyProperty.Register("ListViewItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemBackgroundSelected
        {
            get { return (object)GetValue(ListViewItemBackgroundSelectedProperty); }
            set { SetValue(ListViewItemBackgroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemBackgroundSelectedProperty =
            DependencyProperty.Register("ListViewItemBackgroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemBackgroundSelectedPointerOver
        {
            get { return (object)GetValue(ListViewItemBackgroundSelectedPointerOverProperty); }
            set { SetValue(ListViewItemBackgroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemBackgroundSelectedPointerOverProperty =
            DependencyProperty.Register("ListViewItemBackgroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemBackgroundSelectedPressed
        {
            get { return (object)GetValue(ListViewItemBackgroundSelectedPressedProperty); }
            set { SetValue(ListViewItemBackgroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemBackgroundSelectedPressedProperty =
            DependencyProperty.Register("ListViewItemBackgroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemForeground
        {
            get { return (object)GetValue(ListViewItemForegroundProperty); }
            set { SetValue(ListViewItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemForegroundProperty =
            DependencyProperty.Register("ListViewItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemForegroundPointerOver
        {
            get { return (object)GetValue(ListViewItemForegroundPointerOverProperty); }
            set { SetValue(ListViewItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemForegroundPointerOverProperty =
            DependencyProperty.Register("ListViewItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemForegroundSelected
        {
            get { return (object)GetValue(ListViewItemForegroundSelectedProperty); }
            set { SetValue(ListViewItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemForegroundSelectedProperty =
            DependencyProperty.Register("ListViewItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemFocusVisualPrimaryBrush
        {
            get { return (object)GetValue(ListViewItemFocusVisualPrimaryBrushProperty); }
            set { SetValue(ListViewItemFocusVisualPrimaryBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemFocusVisualPrimaryBrushProperty =
            DependencyProperty.Register("ListViewItemFocusVisualPrimaryBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemFocusVisualSecondaryBrush
        {
            get { return (object)GetValue(ListViewItemFocusVisualSecondaryBrushProperty); }
            set { SetValue(ListViewItemFocusVisualSecondaryBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemFocusVisualSecondaryBrushProperty =
            DependencyProperty.Register("ListViewItemFocusVisualSecondaryBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemFocusBorderBrush
        {
            get { return (object)GetValue(ListViewItemFocusBorderBrushProperty); }
            set { SetValue(ListViewItemFocusBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemFocusBorderBrushProperty =
            DependencyProperty.Register("ListViewItemFocusBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemFocusSecondaryBorderBrush
        {
            get { return (object)GetValue(ListViewItemFocusSecondaryBorderBrushProperty); }
            set { SetValue(ListViewItemFocusSecondaryBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemFocusSecondaryBorderBrushProperty =
            DependencyProperty.Register("ListViewItemFocusSecondaryBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemCheckBrush
        {
            get { return (object)GetValue(ListViewItemCheckBrushProperty); }
            set { SetValue(ListViewItemCheckBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemCheckBrushProperty =
            DependencyProperty.Register("ListViewItemCheckBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemCheckBoxBrush
        {
            get { return (object)GetValue(ListViewItemCheckBoxBrushProperty); }
            set { SetValue(ListViewItemCheckBoxBrushProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemCheckBoxBrushProperty =
            DependencyProperty.Register("ListViewItemCheckBoxBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemDragForeground
        {
            get { return (object)GetValue(ListViewItemDragForegroundProperty); }
            set { SetValue(ListViewItemDragForegroundProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemDragForegroundProperty =
            DependencyProperty.Register("ListViewItemDragForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object ListViewItemPlaceholderBackground
        {
            get { return (object)GetValue(ListViewItemPlaceholderBackgroundProperty); }
            set { SetValue(ListViewItemPlaceholderBackgroundProperty, value); }
        }

        public static readonly DependencyProperty ListViewItemPlaceholderBackgroundProperty =
            DependencyProperty.Register("ListViewItemPlaceholderBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AutoSuggestBoxSuggestionsListBackground
        {
            get { return (object)GetValue(AutoSuggestBoxSuggestionsListBackgroundProperty); }
            set { SetValue(AutoSuggestBoxSuggestionsListBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AutoSuggestBoxSuggestionsListBackgroundProperty =
            DependencyProperty.Register("AutoSuggestBoxSuggestionsListBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AutoSuggestBoxSuggestionsListBorderBrush
        {
            get { return (object)GetValue(AutoSuggestBoxSuggestionsListBorderBrushProperty); }
            set { SetValue(AutoSuggestBoxSuggestionsListBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty AutoSuggestBoxSuggestionsListBorderBrushProperty =
            DependencyProperty.Register("AutoSuggestBoxSuggestionsListBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AutoSuggestBoxLightDismissOverlayBackground
        {
            get { return (object)GetValue(AutoSuggestBoxLightDismissOverlayBackgroundProperty); }
            set { SetValue(AutoSuggestBoxLightDismissOverlayBackgroundProperty, value); }
        }

        public static readonly DependencyProperty AutoSuggestBoxLightDismissOverlayBackgroundProperty =
            DependencyProperty.Register("AutoSuggestBoxLightDismissOverlayBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemBackgroundSelectedDisabled
        {
            get { return (object)GetValue(TreeViewItemBackgroundSelectedDisabledProperty); }
            set { SetValue(TreeViewItemBackgroundSelectedDisabledProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemBackgroundSelectedDisabledProperty =
            DependencyProperty.Register("TreeViewItemBackgroundSelectedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForeground
        {
            get { return (object)GetValue(TreeViewItemForegroundProperty); }
            set { SetValue(TreeViewItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundProperty =
            DependencyProperty.Register("TreeViewItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundPointerOver
        {
            get { return (object)GetValue(TreeViewItemForegroundPointerOverProperty); }
            set { SetValue(TreeViewItemForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundPointerOverProperty =
            DependencyProperty.Register("TreeViewItemForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundPressed
        {
            get { return (object)GetValue(TreeViewItemForegroundPressedProperty); }
            set { SetValue(TreeViewItemForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundPressedProperty =
            DependencyProperty.Register("TreeViewItemForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundDisabled
        {
            get { return (object)GetValue(TreeViewItemForegroundDisabledProperty); }
            set { SetValue(TreeViewItemForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundDisabledProperty =
            DependencyProperty.Register("TreeViewItemForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundSelected
        {
            get { return (object)GetValue(TreeViewItemForegroundSelectedProperty); }
            set { SetValue(TreeViewItemForegroundSelectedProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundSelectedProperty =
            DependencyProperty.Register("TreeViewItemForegroundSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundSelectedPointerOver
        {
            get { return (object)GetValue(TreeViewItemForegroundSelectedPointerOverProperty); }
            set { SetValue(TreeViewItemForegroundSelectedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundSelectedPointerOverProperty =
            DependencyProperty.Register("TreeViewItemForegroundSelectedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundSelectedPressed
        {
            get { return (object)GetValue(TreeViewItemForegroundSelectedPressedProperty); }
            set { SetValue(TreeViewItemForegroundSelectedPressedProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundSelectedPressedProperty =
            DependencyProperty.Register("TreeViewItemForegroundSelectedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemForegroundSelectedDisabled
        {
            get { return (object)GetValue(TreeViewItemForegroundSelectedDisabledProperty); }
            set { SetValue(TreeViewItemForegroundSelectedDisabledProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemForegroundSelectedDisabledProperty =
            DependencyProperty.Register("TreeViewItemForegroundSelectedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemCheckBoxBorderSelected
        {
            get { return (object)GetValue(TreeViewItemCheckBoxBorderSelectedProperty); }
            set { SetValue(TreeViewItemCheckBoxBorderSelectedProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemCheckBoxBorderSelectedProperty =
            DependencyProperty.Register("TreeViewItemCheckBoxBorderSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object TreeViewItemCheckGlyphSelected
        {
            get { return (object)GetValue(TreeViewItemCheckGlyphSelectedProperty); }
            set { SetValue(TreeViewItemCheckGlyphSelectedProperty, value); }
        }

        public static readonly DependencyProperty TreeViewItemCheckGlyphSelectedProperty =
            DependencyProperty.Register("TreeViewItemCheckGlyphSelected", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemBackground
        {
            get { return (object)GetValue(SwipeItemBackgroundProperty); }
            set { SetValue(SwipeItemBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemBackgroundProperty =
            DependencyProperty.Register("SwipeItemBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemForeground
        {
            get { return (object)GetValue(SwipeItemForegroundProperty); }
            set { SetValue(SwipeItemForegroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemForegroundProperty =
            DependencyProperty.Register("SwipeItemForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemBackgroundPressed
        {
            get { return (object)GetValue(SwipeItemBackgroundPressedProperty); }
            set { SetValue(SwipeItemBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemBackgroundPressedProperty =
            DependencyProperty.Register("SwipeItemBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemPreThresholdExecuteForeground
        {
            get { return (object)GetValue(SwipeItemPreThresholdExecuteForegroundProperty); }
            set { SetValue(SwipeItemPreThresholdExecuteForegroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemPreThresholdExecuteForegroundProperty =
            DependencyProperty.Register("SwipeItemPreThresholdExecuteForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemPreThresholdExecuteBackground
        {
            get { return (object)GetValue(SwipeItemPreThresholdExecuteBackgroundProperty); }
            set { SetValue(SwipeItemPreThresholdExecuteBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemPreThresholdExecuteBackgroundProperty =
            DependencyProperty.Register("SwipeItemPreThresholdExecuteBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemPostThresholdExecuteForeground
        {
            get { return (object)GetValue(SwipeItemPostThresholdExecuteForegroundProperty); }
            set { SetValue(SwipeItemPostThresholdExecuteForegroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemPostThresholdExecuteForegroundProperty =
            DependencyProperty.Register("SwipeItemPostThresholdExecuteForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SwipeItemPostThresholdExecuteBackground
        {
            get { return (object)GetValue(SwipeItemPostThresholdExecuteBackgroundProperty); }
            set { SetValue(SwipeItemPostThresholdExecuteBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SwipeItemPostThresholdExecuteBackgroundProperty =
            DependencyProperty.Register("SwipeItemPostThresholdExecuteBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackground
        {
            get { return (object)GetValue(SplitButtonBackgroundProperty); }
            set { SetValue(SplitButtonBackgroundProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundProperty =
            DependencyProperty.Register("SplitButtonBackground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundPointerOver
        {
            get { return (object)GetValue(SplitButtonBackgroundPointerOverProperty); }
            set { SetValue(SplitButtonBackgroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundPointerOverProperty =
            DependencyProperty.Register("SplitButtonBackgroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundPressed
        {
            get { return (object)GetValue(SplitButtonBackgroundPressedProperty); }
            set { SetValue(SplitButtonBackgroundPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundPressedProperty =
            DependencyProperty.Register("SplitButtonBackgroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundDisabled
        {
            get { return (object)GetValue(SplitButtonBackgroundDisabledProperty); }
            set { SetValue(SplitButtonBackgroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundDisabledProperty =
            DependencyProperty.Register("SplitButtonBackgroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundChecked
        {
            get { return (object)GetValue(SplitButtonBackgroundCheckedProperty); }
            set { SetValue(SplitButtonBackgroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundCheckedProperty =
            DependencyProperty.Register("SplitButtonBackgroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundCheckedPointerOver
        {
            get { return (object)GetValue(SplitButtonBackgroundCheckedPointerOverProperty); }
            set { SetValue(SplitButtonBackgroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundCheckedPointerOverProperty =
            DependencyProperty.Register("SplitButtonBackgroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundCheckedPressed
        {
            get { return (object)GetValue(SplitButtonBackgroundCheckedPressedProperty); }
            set { SetValue(SplitButtonBackgroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundCheckedPressedProperty =
            DependencyProperty.Register("SplitButtonBackgroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBackgroundCheckedDisabled
        {
            get { return (object)GetValue(SplitButtonBackgroundCheckedDisabledProperty); }
            set { SetValue(SplitButtonBackgroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBackgroundCheckedDisabledProperty =
            DependencyProperty.Register("SplitButtonBackgroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForeground
        {
            get { return (object)GetValue(SplitButtonForegroundProperty); }
            set { SetValue(SplitButtonForegroundProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundProperty =
            DependencyProperty.Register("SplitButtonForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundPointerOver
        {
            get { return (object)GetValue(SplitButtonForegroundPointerOverProperty); }
            set { SetValue(SplitButtonForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundPointerOverProperty =
            DependencyProperty.Register("SplitButtonForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundPressed
        {
            get { return (object)GetValue(SplitButtonForegroundPressedProperty); }
            set { SetValue(SplitButtonForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundPressedProperty =
            DependencyProperty.Register("SplitButtonForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundDisabled
        {
            get { return (object)GetValue(SplitButtonForegroundDisabledProperty); }
            set { SetValue(SplitButtonForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundDisabledProperty =
            DependencyProperty.Register("SplitButtonForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundChecked
        {
            get { return (object)GetValue(SplitButtonForegroundCheckedProperty); }
            set { SetValue(SplitButtonForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundCheckedProperty =
            DependencyProperty.Register("SplitButtonForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundCheckedPointerOver
        {
            get { return (object)GetValue(SplitButtonForegroundCheckedPointerOverProperty); }
            set { SetValue(SplitButtonForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("SplitButtonForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundCheckedPressed
        {
            get { return (object)GetValue(SplitButtonForegroundCheckedPressedProperty); }
            set { SetValue(SplitButtonForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundCheckedPressedProperty =
            DependencyProperty.Register("SplitButtonForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonForegroundCheckedDisabled
        {
            get { return (object)GetValue(SplitButtonForegroundCheckedDisabledProperty); }
            set { SetValue(SplitButtonForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonForegroundCheckedDisabledProperty =
            DependencyProperty.Register("SplitButtonForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrush
        {
            get { return (object)GetValue(SplitButtonBorderBrushProperty); }
            set { SetValue(SplitButtonBorderBrushProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushProperty =
            DependencyProperty.Register("SplitButtonBorderBrush", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushPointerOver
        {
            get { return (object)GetValue(SplitButtonBorderBrushPointerOverProperty); }
            set { SetValue(SplitButtonBorderBrushPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushPointerOverProperty =
            DependencyProperty.Register("SplitButtonBorderBrushPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushPressed
        {
            get { return (object)GetValue(SplitButtonBorderBrushPressedProperty); }
            set { SetValue(SplitButtonBorderBrushPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushPressedProperty =
            DependencyProperty.Register("SplitButtonBorderBrushPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushDisabled
        {
            get { return (object)GetValue(SplitButtonBorderBrushDisabledProperty); }
            set { SetValue(SplitButtonBorderBrushDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushDisabledProperty =
            DependencyProperty.Register("SplitButtonBorderBrushDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushChecked
        {
            get { return (object)GetValue(SplitButtonBorderBrushCheckedProperty); }
            set { SetValue(SplitButtonBorderBrushCheckedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushCheckedProperty =
            DependencyProperty.Register("SplitButtonBorderBrushChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushCheckedPointerOver
        {
            get { return (object)GetValue(SplitButtonBorderBrushCheckedPointerOverProperty); }
            set { SetValue(SplitButtonBorderBrushCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushCheckedPointerOverProperty =
            DependencyProperty.Register("SplitButtonBorderBrushCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushCheckedPressed
        {
            get { return (object)GetValue(SplitButtonBorderBrushCheckedPressedProperty); }
            set { SetValue(SplitButtonBorderBrushCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushCheckedPressedProperty =
            DependencyProperty.Register("SplitButtonBorderBrushCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object SplitButtonBorderBrushCheckedDisabled
        {
            get { return (object)GetValue(SplitButtonBorderBrushCheckedDisabledProperty); }
            set { SetValue(SplitButtonBorderBrushCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty SplitButtonBorderBrushCheckedDisabledProperty =
            DependencyProperty.Register("SplitButtonBorderBrushCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemKeyboardAcceleratorTextForeground
        {
            get { return (object)GetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundProperty); }
            set { SetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemKeyboardAcceleratorTextForegroundProperty =
            DependencyProperty.Register("MenuFlyoutItemKeyboardAcceleratorTextForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver
        {
            get { return (object)GetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty); }
            set { SetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOverProperty =
            DependencyProperty.Register("MenuFlyoutItemKeyboardAcceleratorTextForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemKeyboardAcceleratorTextForegroundPressed
        {
            get { return (object)GetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty); }
            set { SetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemKeyboardAcceleratorTextForegroundPressedProperty =
            DependencyProperty.Register("MenuFlyoutItemKeyboardAcceleratorTextForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled
        {
            get { return (object)GetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty); }
            set { SetValue(MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabledProperty =
            DependencyProperty.Register("MenuFlyoutItemKeyboardAcceleratorTextForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonKeyboardAcceleratorTextForeground
        {
            get { return (object)GetValue(AppBarButtonKeyboardAcceleratorTextForegroundProperty); }
            set { SetValue(AppBarButtonKeyboardAcceleratorTextForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonKeyboardAcceleratorTextForegroundProperty =
            DependencyProperty.Register("AppBarButtonKeyboardAcceleratorTextForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonKeyboardAcceleratorTextForegroundPointerOver
        {
            get { return (object)GetValue(AppBarButtonKeyboardAcceleratorTextForegroundPointerOverProperty); }
            set { SetValue(AppBarButtonKeyboardAcceleratorTextForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonKeyboardAcceleratorTextForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarButtonKeyboardAcceleratorTextForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonKeyboardAcceleratorTextForegroundPressed
        {
            get { return (object)GetValue(AppBarButtonKeyboardAcceleratorTextForegroundPressedProperty); }
            set { SetValue(AppBarButtonKeyboardAcceleratorTextForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonKeyboardAcceleratorTextForegroundPressedProperty =
            DependencyProperty.Register("AppBarButtonKeyboardAcceleratorTextForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonKeyboardAcceleratorTextForegroundDisabled
        {
            get { return (object)GetValue(AppBarButtonKeyboardAcceleratorTextForegroundDisabledProperty); }
            set { SetValue(AppBarButtonKeyboardAcceleratorTextForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonKeyboardAcceleratorTextForegroundDisabledProperty =
            DependencyProperty.Register("AppBarButtonKeyboardAcceleratorTextForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForeground
        {
            get { return (object)GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOverProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundPressed
        {
            get { return (object)GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundPressedProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabled
        {
            get { return (object)GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabledProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundChecked
        {
            get { return (object)GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundChecked", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOver
        {
            get { return (object)GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOverProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOverProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressed
        {
            get { return (object)GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressedProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressedProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabled
        {
            get { return (object)GetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabledProperty); }
            set { SetValue(AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabledProperty =
            DependencyProperty.Register("AppBarToggleButtonKeyboardAcceleratorTextForegroundCheckedDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonBackgroundSubMenuOpened
        {
            get { return (object)GetValue(AppBarButtonBackgroundSubMenuOpenedProperty); }
            set { SetValue(AppBarButtonBackgroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonBackgroundSubMenuOpenedProperty =
            DependencyProperty.Register("AppBarButtonBackgroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonForegroundSubMenuOpened
        {
            get { return (object)GetValue(AppBarButtonForegroundSubMenuOpenedProperty); }
            set { SetValue(AppBarButtonForegroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonForegroundSubMenuOpenedProperty =
            DependencyProperty.Register("AppBarButtonForegroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpened
        {
            get { return (object)GetValue(AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpenedProperty); }
            set { SetValue(AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpenedProperty =
            DependencyProperty.Register("AppBarButtonKeyboardAcceleratorTextForegroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonSubItemChevronForeground
        {
            get { return (object)GetValue(AppBarButtonSubItemChevronForegroundProperty); }
            set { SetValue(AppBarButtonSubItemChevronForegroundProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonSubItemChevronForegroundProperty =
            DependencyProperty.Register("AppBarButtonSubItemChevronForeground", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonSubItemChevronForegroundPointerOver
        {
            get { return (object)GetValue(AppBarButtonSubItemChevronForegroundPointerOverProperty); }
            set { SetValue(AppBarButtonSubItemChevronForegroundPointerOverProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonSubItemChevronForegroundPointerOverProperty =
            DependencyProperty.Register("AppBarButtonSubItemChevronForegroundPointerOver", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonSubItemChevronForegroundPressed
        {
            get { return (object)GetValue(AppBarButtonSubItemChevronForegroundPressedProperty); }
            set { SetValue(AppBarButtonSubItemChevronForegroundPressedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonSubItemChevronForegroundPressedProperty =
            DependencyProperty.Register("AppBarButtonSubItemChevronForegroundPressed", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonSubItemChevronForegroundSubMenuOpened
        {
            get { return (object)GetValue(AppBarButtonSubItemChevronForegroundSubMenuOpenedProperty); }
            set { SetValue(AppBarButtonSubItemChevronForegroundSubMenuOpenedProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonSubItemChevronForegroundSubMenuOpenedProperty =
            DependencyProperty.Register("AppBarButtonSubItemChevronForegroundSubMenuOpened", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));
        public object AppBarButtonSubItemChevronForegroundDisabled
        {
            get { return (object)GetValue(AppBarButtonSubItemChevronForegroundDisabledProperty); }
            set { SetValue(AppBarButtonSubItemChevronForegroundDisabledProperty, value); }
        }

        public static readonly DependencyProperty AppBarButtonSubItemChevronForegroundDisabledProperty =
            DependencyProperty.Register("AppBarButtonSubItemChevronForegroundDisabled", typeof(object), typeof(ResourcesMapper), new PropertyMetadata(null));

    }
}
