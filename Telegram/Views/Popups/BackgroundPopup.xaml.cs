//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Popups
{
    public sealed partial class BackgroundPopup : ContentPopup, IBackgroundDelegate
    {
        public BackgroundViewModel ViewModel => DataContext as BackgroundViewModel;

        private readonly TaskCompletionSource<object> _task;
        private bool _ignoreClosing;

        public BackgroundPopup(TaskCompletionSource<object> task)
            : this()
        {
            _task = task;
        }

        public BackgroundPopup()
        {
            InitializeComponent();

            Message1.Mockup(Strings.BackgroundPreviewLine1, false, DateTime.Now.AddSeconds(-25));
            Message2.Mockup(Strings.BackgroundPreviewLine2, true, DateTime.Now);

            ElementComposition.GetElementVisual(ContentPanel).Clip = Window.Current.Compositor.CreateInsetClip();
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            Grid.SetRow(ColorPanel, ColorRadio.IsChecked == true ? 2 : 6);

            if (ColorRadio.IsChecked == true)
            {
                PatternRadio.IsChecked = false;
            }

            RadioColor_Toggled(null, null);
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

                if ( ViewModel.IsPremium is false)
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

            if (wallpaper.Type is BackgroundTypeWallpaper)
            {
                Blur.Visibility = Visibility.Visible;

                Message1.Mockup(line1 ?? Strings.BackgroundPreviewLine1, false, DateTime.Now.AddSeconds(-25));
                Message2.Mockup(Strings.BackgroundPreviewLine2, true, DateTime.Now);
            }
            else
            {
                Blur.Visibility = Visibility.Collapsed;

                if (wallpaper.Type is BackgroundTypeFill or BackgroundTypePattern)
                {
                    Pattern.Visibility = Visibility.Visible;
                    Color.Visibility = Visibility.Visible;
                }

                Message1.Mockup(line1 ?? Strings.BackgroundColorSinglePreviewLine1, false, DateTime.Now.AddSeconds(-25));
                Message2.Mockup(Strings.BackgroundColorSinglePreviewLine2, true, DateTime.Now);
            }
        }

        #endregion

        #region Binding

        private BackgroundFill ConvertBackground(Background background)
        {
            if (background != null)
            {
                Preview.UpdateSource(ViewModel.ClientService, background, false);

                PatternList.ForEach<Document>((container, document) =>
                {
                    var content = container.ContentTemplateRoot as ChatBackgroundPresenter;
                    var background = ViewModel.GetPattern(document);

                    content.UpdateSource(ViewModel.ClientService, background, true);
                });
            }

            return null;
        }

        private string ConvertColor2Glyph(BackgroundColor color)
        {
            return color.IsEmpty ? Icons.Add : Icons.Dismiss;
        }

        private Visibility ConvertColor2Visibility(BackgroundColor color)
        {
            return color.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        }

        private Visibility ConvertColor2Visibility(BackgroundColor color2, BackgroundColor color3)
        {
            return !color2.IsEmpty && color3.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertColor2Visibility(BackgroundColor color2, BackgroundColor color3, BackgroundColor color4)
        {
            if (!color2.IsEmpty && color3.IsEmpty)
            {
                return Visibility.Visible;
            }

            return !color3.IsEmpty && color4.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private double ConvertMinimumIntensity(ElementTheme theme)
        {
            return theme == ElementTheme.Dark ? -100 : 0;
        }

        #endregion

        private void RadioColor_Toggled(object sender, RoutedEventArgs e)
        {
            var row = Grid.GetRow(ColorPanel);
            if (row != 2)
            {
                return;
            }

            if (RadioColor1.IsChecked == true)
            {
                PickerColor.Color = ViewModel.Color1;
            }
            else if (RadioColor2.IsChecked == true)
            {
                PickerColor.Color = ViewModel.Color2;
            }
            else if (RadioColor3.IsChecked == true)
            {
                PickerColor.Color = ViewModel.Color3;
            }
            else if (RadioColor4.IsChecked == true)
            {
                PickerColor.Color = ViewModel.Color4;
            }

            TextColor1.SelectAll();
        }

        private void PickerColor_ColorChanged(Controls.ColorPicker sender, Controls.ColorChangedEventArgs args)
        {
            var row = Grid.GetRow(ColorPanel);
            if (row != 2)
            {
                return;
            }

            TextColor1.Color = args.NewColor;

            if (RadioColor1.IsChecked == true)
            {
                ViewModel.Color1 = args.NewColor;
            }
            else if (RadioColor2.IsChecked == true)
            {
                ViewModel.Color2 = args.NewColor;
            }
            else if (RadioColor3.IsChecked == true)
            {
                ViewModel.Color3 = args.NewColor;
            }
            else if (RadioColor4.IsChecked == true)
            {
                ViewModel.Color4 = args.NewColor;
            }
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

        private void TextColor_ColorChanged(ColorTextBox sender, Controls.ColorChangedEventArgs args)
        {
            if (sender.FocusState == FocusState.Unfocused)
            {
                return;
            }

            PickerColor.Color = args.NewColor;
        }

        private void RemoveColor_Click(object sender, RoutedEventArgs e)
        {
            if (RadioColor1.IsChecked == true)
            {
                ViewModel.RemoveColor(0);
            }
            else if (RadioColor2.IsChecked == true)
            {
                ViewModel.RemoveColor(1);
            }
            else if (RadioColor3.IsChecked == true)
            {
                ViewModel.RemoveColor(2);
            }
            else if (RadioColor4.IsChecked == true)
            {
                ViewModel.RemoveColor(3);
            }
        }

        private void AddRemoveColor_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Color2.IsEmpty || ViewModel.Color3.IsEmpty || ViewModel.Color4.IsEmpty)
            {
                ViewModel.AddColor();
            }
            else if (RadioColor1.IsChecked == true)
            {
                ViewModel.RemoveColor(0);
            }
            else if (RadioColor2.IsChecked == true)
            {
                ViewModel.RemoveColor(1);
            }
            else if (RadioColor3.IsChecked == true)
            {
                ViewModel.RemoveColor(2);
            }
            else if (RadioColor4.IsChecked == true)
            {
                ViewModel.RemoveColor(3);
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
            await this.ShowQueuedAsync();
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (_ignoreClosing)
            {
                return;
            }

            _task?.TrySetResult(false);
        }
    }
}
