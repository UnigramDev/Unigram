//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class BackgroundPopup : ContentPopup, IBackgroundDelegate
    {
        public BackgroundViewModel ViewModel => DataContext as BackgroundViewModel;

        private readonly TaskCompletionSource<object> _task;
        private bool _ignoreClosing;

        public BackgroundPopup(INavigationService navigationService, TaskCompletionSource<object> task)
            : this(navigationService)
        {
            _task = task;
        }

        private readonly MessageBubbleBackgroundCoordinator _background;

        public BackgroundPopup(INavigationService navigationService, bool forThemeSettings = false)
        {
            InitializeComponent();

            if (forThemeSettings)
            {
                FindName(nameof(Message3));
                FindName(nameof(Message4));
                FindName(nameof(Message5));
                FindName(nameof(Message6));

                Message1.Resources = Incoming.CreateDictionary();
                Message2.Resources = Outgoing.CreateDictionary();
                Message3.Resources = Outgoing.CreateDictionary();
                Message4.Resources = Incoming.CreateDictionary();
                Message5.Resources = Incoming.CreateDictionary();
                Message6.Resources = Outgoing.CreateDictionary();

                // No window: the fill always comes from the settings being edited.
                _background = new MessageBubbleBackgroundCoordinator(ScrollingHost, null);
                _background.Attach(Message2);
                _background.Attach(Message3);
                _background.Attach(Message6);

                Message1.Mockup(Strings.ThemePreviewLine4, false, DateTime.Now.AddSeconds(-25), true, true);
                Message2.Mockup(Strings.ThemePreviewLine1, true, DateTime.Now, true, false);
                //Message3.Mockup(Strings.FontSizePreviewLine1, Strings.FontSizePreviewName, Strings.FontSizePreviewReply, false, DateTime.Now.AddSeconds(-25));
                Message3.Mockup(new MessageVoiceNote(new VoiceNote(3, new byte[]
                {
                    0, 0, 163, 198, 43, 17, 250, 248, 127, 155, 85, 58, 159, 230, 164, 212, 185, 247, 73, 42,
                    173, 66, 165, 69, 41, 251, 255, 242, 127, 223, 113, 133, 237, 148, 243, 30, 127, 184, 206, 183, 234,
                    108, 175, 168, 250, 207, 114, 229, 233, 154, 35, 254, 21, 66, 99, 134, 141, 92, 159, 2
                }, "audio/ogg", null, null), new FormattedText(), true), true, DateTime.Now.AddSeconds(-25), false, true);
                Message4.Mockup(Strings.ThemePreviewLine3, Strings.ThemePreviewLine3Reply, Strings.ThemePreviewLine1, false, DateTime.Now.AddSeconds(-25), true, false);
                Message5.Mockup(new MessageAudio(new Audio(4 * 60 + 3, Strings.ThemePreviewSongTitle, Strings.ThemePreviewSongPerformer, "preview.mp3", "audio/mp3", null, null, null, null), new FormattedText()), false, DateTime.Now, false, true);
                Message6.Mockup(Strings.ThemePreviewLine2, Strings.ThemePreviewLine3Reply, Strings.ThemePreviewLine3, true, DateTime.Now, true, true);

                ThemeSettingsNavigation.Visibility = Visibility.Visible;
                CloseButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                Message2.IsOutgoing = true;

                _background = new MessageBubbleBackgroundCoordinator(ScrollingHost, navigationService.Window);
                _background.Attach(Message2);

                Message1.Mockup(Strings.BackgroundPreviewLine1, false, DateTime.Now.AddSeconds(-25));
                Message2.Mockup(Strings.BackgroundPreviewLine2, true, DateTime.Now);
            }

            ContentPanel.CreateInsetClip();
        }

        public MessageBrushes Outgoing { get; } = new("Outgoing", ThemeOutgoing.DefaultLight, ThemeOutgoing.DefaultDark);

        public MessageBrushes Incoming { get; } = new("Incoming", ThemeIncoming.DefaultLight, ThemeIncoming.DefaultDark);

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            Grid.SetRow(ColorPanel, ColorRadio.IsChecked == true ? 2 : 6);

            if (ColorRadio.IsChecked == true)
            {
                PatternRadio.IsChecked = false;
            }

            ColorPanel.SelectAll();
        }

        private void Pattern_Click(object sender, RoutedEventArgs e)
        {
            Grid.SetRow(PatternPanel, PatternRadio.IsChecked == true ? 2 : 6);

            if (PatternRadio.IsChecked == true)
            {
                ColorRadio.IsChecked = false;
            }
        }

        #region Delegates

        public void UpdateBackground(Background wallpaper)
        {
            if (wallpaper == null)
            {
                return;
            }

            var chat = ViewModel.ClientService.GetChat(ViewModel.ChatId);
            var user = ViewModel.ClientService.GetUser(chat);

            var line1 = chat != null
                ? Strings.BackgroundColorSinglePreviewLine3
                : null;

            Service1.Text = user != null
                ? string.Format(Strings.ChatBackgroundHint, user.FirstName)
                : Strings.MessageScheduleToday;

            if (user != null && ViewModel.IsPremiumAvailable)
            {
                PrimaryButton.Margin = new Thickness(24, 8, 24, 0);
                PrimaryButton.Content = Strings.ApplyWallpaperForMe;

                var secondary = string.Format(Strings.ApplyWallpaperForMeAndPeer, user.FirstName);

                if (ViewModel.IsPremium is false)
                {
                    secondary += Icons.Spacing + Icons.LockClosedFilled14;
                }

                SecondaryButton.Visibility = Visibility.Visible;
                SecondaryButton.Content = secondary;

                ColorPanel.Margin =
                    PatternPanel.Margin = new Thickness(0, 0, 0, -105);

                ColorPanel.Padding =
                    PatternPanel.Padding = new Thickness(0, 0, 0, 105);

                ColorPanel.Height =
                    PatternPanel.Height = 312;
            }
            else
            {
                PrimaryButton.Margin = new Thickness(24, 8, 24, 24);
                PrimaryButton.Content = user != null
                    ? Strings.ApplyBackgroundForThisChat
                    : Strings.ApplyBackgroundForAllChats;

                SecondaryButton.Visibility = Visibility.Collapsed;

                ColorPanel.Margin =
                    PatternPanel.Margin = new Thickness(0, 0, 0, -65);

                ColorPanel.Padding =
                    PatternPanel.Padding = new Thickness(0, 0, 0, 65);

                ColorPanel.Height =
                    PatternPanel.Height = 272;
            }

            //Header.CommandVisibility = wallpaper.Id != Constants.WallpaperLocalId ? Visibility.Visible : Visibility.Collapsed;

            if (ViewModel.ThemeSettings == null)
            {
                if (wallpaper.Type is BackgroundTypeWallpaper)
                {
                    Message1.Mockup(line1 ?? Strings.BackgroundPreviewLine1, false, DateTime.Now.AddSeconds(-25));
                    Message2.Mockup(Strings.BackgroundPreviewLine2, true, DateTime.Now);
                }
                else
                {
                    Message1.Mockup(line1 ?? Strings.BackgroundColorSinglePreviewLine1, false, DateTime.Now.AddSeconds(-25));
                    Message2.Mockup(Strings.BackgroundColorSinglePreviewLine2, true, DateTime.Now);
                }
            }

            ShowBackgroundSettings();

            if (ViewModel.ThemeSettings != null)
            {
                ColorRadio.IsChecked = true;
            }
        }

        public void UpdateBackgroundColors(IList<Color> colors)
        {
            if (!_colorsChanging && Navigation.SelectedIndex == 0)
            {
                ColorPanel.Colors = colors;
            }

            ChangeRotation.Visibility = colors.Count == 2
                ? Visibility.Visible
                : Visibility.Collapsed;

            Play.Visibility = colors.Count > 2
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void UpdateThemeSettings(ThemeSettings settings)
        {
            var requested = settings.BaseTheme is BuiltInThemeClassic or BuiltInThemeDay ? TelegramTheme.Light : TelegramTheme.Dark;
            var info = ThemeAccentInfo.FromSettings(requested, settings);

            Outgoing.Update(info?.Parent ?? requested, info?.Values);
            Incoming.Update(info?.Parent ?? requested, info?.Values);

            _background?.Settings = settings;
        }

        public void UpdateAccentColors(IList<Color> colors)
        {
            if (!_colorsChanging && Navigation.SelectedIndex == 1)
            {
                ColorPanel.Colors = colors;
            }
        }

        public void UpdateMessageColors(IList<Color> colors)
        {
            if (!_colorsChanging && Navigation.SelectedIndex == 2)
            {
                ColorPanel.Colors = colors;
            }
        }

        #endregion

        #region Binding

        private BackgroundFill ConvertBackground(Background background)
        {
            if (_updatePending || background == null)
            {
                return null;
            }
            else if (_needsUpdate)
            {
                UpdateBackground();
                return null;
            }

            _updatePending = true;
            VisualUtilities.QueueCallbackForCompositionRendering(UpdateBackground);

            return null;
        }

        private bool _needsUpdate = true;
        private bool _updatePending;

        private void UpdateBackground()
        {
            _needsUpdate = false;
            _updatePending = false;

            var background = ViewModel.Item;
            if (background != null)
            {
                Preview.XamlRoot ??= XamlRoot;

                if (Preview.TryUpdateFill(background) is false)
                {
                    Preview.UpdateSource(ViewModel.ClientService, background, false);
                }

                PatternList.ForEach<PatternInfo>((container, pattern) =>
                {
                    var content = container.ContentTemplateRoot as ChatBackgroundPresenter;
                    var background = ViewModel.GetPattern(pattern?.Document);

                    if (content.TryUpdateFill(background) is false)
                    {
                        content.UpdateSource(ViewModel.ClientService, background, true);
                    }
                });
            }
        }

        private double ConvertMinimumIntensity(ElementTheme theme)
        {
            return theme == ElementTheme.Dark ? -100 : 0;
        }

        #endregion

        private bool _colorsChanging;

        private void PickerColor_ColorsChanged(Controls.ColorsPicker sender, Controls.ColorsChangedEventArgs args)
        {
            var row = Grid.GetRow(ColorPanel);
            if (row != 2)
            {
                return;
            }

            _colorsChanging = true;
            if (Navigation.SelectedIndex == 1)
            {
                ViewModel.AccentColors = args.NewColors;
            }
            else if (Navigation.SelectedIndex == 2)
            {
                ViewModel.MessageColors = args.NewColors;
            }
            else
            {
                ViewModel.BackgroundColors = args.NewColors;
            }
            _colorsChanging = false;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatBackgroundPresenter content && args.Item is PatternInfo pattern)
            {
                var background = ViewModel.GetPattern(pattern?.Document);

                content.UpdateSource(ViewModel.ClientService, background, true);
                args.Handled = true;
            }
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            Preview.Next();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Secondary);
        }

        private void Primary_Click(object sender, RoutedEventArgs e)
        {
            _task?.TrySetResult(true);

            Hide(ContentDialogResult.Primary);
            ViewModel.Done(true);
        }

        private async void Secondary_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsPremium is false && ViewModel.IsPremiumAvailable)
            {
                await ShowPromoAsync();
                return;
            }

            _task?.TrySetResult(true);

            Hide(ContentDialogResult.Primary);
            ViewModel.Done(false);
        }

        private async Task ShowPromoAsync()
        {
            _ignoreClosing = true;
            Hide();

            _ignoreClosing = false;

            await ViewModel.NavigationService.ShowPromoAsync(new PremiumSourceFeature(new PremiumFeatureBackgroundForBoth()));
            await this.ShowQueuedAsync(XamlRoot);
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (_ignoreClosing)
            {
                return;
            }

            _task?.TrySetResult(false);
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorPanel == null)
            {
                return;
            }

            if (Navigation.SelectedIndex == 1)
            {
                ColorPanel.Maximum = 2;
                ColorPanel.Colors = ViewModel.AccentColors;

                HideBackgrondSettings();
            }
            else if (Navigation.SelectedIndex == 2)
            {
                ColorPanel.Maximum = 4;
                ColorPanel.Colors = ViewModel.MessageColors;

                HideBackgrondSettings();
            }
            else
            {
                ColorPanel.Maximum = 4;
                ColorPanel.Colors = ViewModel.BackgroundColors;

                ShowBackgroundSettings();
            }

            ColorRadio.IsChecked = true;
            ColorPanel.SelectAll();
        }

        private void HideBackgrondSettings()
        {
            Pattern.Visibility = Visibility.Collapsed;
            Blur.Visibility = Visibility.Collapsed;
            ChangeRotation.Visibility = Visibility.Collapsed;
            Play.Visibility = Visibility.Collapsed;
        }

        private void ShowBackgroundSettings()
        {
            if (ViewModel.Item.Type is BackgroundTypeWallpaper)
            {
                Blur.Visibility = Visibility.Visible;
                Pattern.Visibility = Visibility.Collapsed;
                ChangeRotation.Visibility = Visibility.Collapsed;
                Play.Visibility = Visibility.Collapsed;
            }
            else
            {
                Blur.Visibility = Visibility.Collapsed;

                if (ViewModel.Item.Type is BackgroundTypeFill or BackgroundTypePattern)
                {
                    Pattern.Visibility = Visibility.Visible;
                    Color.Visibility = Visibility.Visible;
                }

                ChangeRotation.Visibility = ViewModel.BackgroundColors?.Count == 2
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                Play.Visibility = ViewModel.BackgroundColors?.Count > 2
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void ScrollingHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScrollingHost.ChangeView(null, ScrollingHost.ScrollableHeight, null, true);
        }
    }
}
