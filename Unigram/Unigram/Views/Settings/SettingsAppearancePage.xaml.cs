using System;
using System.Collections.Generic;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels.Settings;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsAppearancePage : HostedPage
    {
        public SettingsAppearanceViewModel ViewModel => DataContext as SettingsAppearanceViewModel;

        public SettingsAppearancePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsAppearanceViewModel>();

            var preview = ElementCompositionPreview.GetElementVisual(Preview);
            preview.Clip = preview.Compositor.CreateInsetClip();

            Message1.Mockup(Strings.Resources.FontSizePreviewLine1, Strings.Resources.FontSizePreviewName, Strings.Resources.FontSizePreviewReply, false, DateTime.Now.AddSeconds(-25));
            Message2.Mockup(Strings.Resources.FontSizePreviewLine2, true, DateTime.Now);

            BackgroundPresenter.Update(ViewModel.SessionId, ViewModel.ProtoService, ViewModel.Aggregator);

            if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "BottomEdgeAlignedRight"))
            {
                MenuFlyout.Placement = FlyoutPlacementMode.BottomEdgeAlignedRight;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void Wallpaper_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsBackgroundsPage));
        }

        private void NightMode_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsNightModePage));
        }

        private void Themes_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsThemesPage));
        }

        #region Binding

        private SolidColorBrush ConvertAccent(IList<ThemeAccentInfo> accents, int index)
        {
            if (accents != null && accents.Count > index)
            {
                return new SolidColorBrush(accents[index].SelectionColor);
            }

            return null;
        }

        private string ConvertNightMode(NightMode mode)
        {
            return mode == NightMode.Scheduled
                ? Strings.Resources.AutoNightScheduled
                : mode == NightMode.Automatic
                ? Strings.Resources.AutoNightAutomatic
                : mode == NightMode.System
                ? Strings.Resources.AutoNightSystemDefault
                : Strings.Resources.AutoNightDisabled;
        }

        private string ConvertDistanceUnits(DistanceUnits units)
        {
            switch (units)
            {
                case DistanceUnits.Automatic:
                    return Strings.Resources.DistanceUnitsAutomatic;
                case DistanceUnits.Kilometers:
                    return Strings.Resources.DistanceUnitsKilometers;
                case DistanceUnits.Miles:
                    return Strings.Resources.DistanceUnitsMiles;
            }

            return null;
        }

        private Visibility ConvertNightModeVisibility(NightMode mode)
        {
            return mode == NightMode.Disabled ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("FontSize"))
            {
                Message1.UpdateMockup();
                Message2.UpdateMockup();
            }
            else if (e.PropertyName.Equals("BubbleRadius"))
            {
                Message1.UpdateMockup();
                Message2.UpdateMockup();
            }
        }

        private async void Switch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && radio.Tag is ThemeInfoBase info)
            {
                await ViewModel.SetThemeAsync(info);
            }
        }

        #region Context menu

        private void Theme_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var theme = List.ItemFromContainer(element) as ThemeInfoBase;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.ThemeCreateCommand, theme, Strings.Resources.CreateNewThemeMenu, new FontIcon { Glyph = Icons.Theme });

            if (!theme.IsOfficial)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.ThemeShareCommand, theme, Strings.Resources.ShareFile, new FontIcon { Glyph = Icons.Share });
                flyout.CreateFlyoutItem(ViewModel.ThemeEditCommand, theme, Strings.Resources.Edit, new FontIcon { Glyph = Icons.Edit });
                flyout.CreateFlyoutItem(ViewModel.ThemeDeleteCommand, theme, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
            }

            args.ShowAt(flyout, element);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Theme_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var theme = args.Item as ThemeInfoBase;
            var radio = args.ItemContainer.ContentTemplateRoot as RadioButton;

            if (args.ItemContainer.ContentTemplateRoot is StackPanel root)
            {
                radio = root.Children[0] as RadioButton;
            }

            radio.Click -= Switch_Click;
            radio.Click += Switch_Click;

            if (theme is ThemeCustomInfo custom)
            {
                radio.RequestedTheme = custom.Parent.HasFlag(TelegramTheme.Dark) ? ElementTheme.Dark : ElementTheme.Light;
                radio.IsChecked = SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Type == TelegramThemeType.Custom && string.Equals(SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Custom, custom.Path, StringComparison.OrdinalIgnoreCase);
            }
            else if (theme is ThemeAccentInfo accent)
            {
                radio.RequestedTheme = accent.Parent.HasFlag(TelegramTheme.Dark) ? ElementTheme.Dark : ElementTheme.Light;
                radio.IsChecked = SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Type == accent.Type && SettingsService.Current.Appearance.Accents[accent.Type] == accent.AccentColor;
            }
            else
            {
                radio.RequestedTheme = theme.Parent.HasFlag(TelegramTheme.Dark) ? ElementTheme.Dark : ElementTheme.Light;
                radio.IsChecked = string.IsNullOrEmpty(SettingsService.Current.Appearance[SettingsService.Current.Appearance.RequestedTheme].Custom) && SettingsService.Current.Appearance.RequestedTheme == theme.Parent;
            }
        }

        #endregion

    }
}
