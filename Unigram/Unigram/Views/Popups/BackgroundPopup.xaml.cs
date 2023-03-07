//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Navigation;
using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Chats;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;

namespace Unigram.Views
{
    public sealed partial class BackgroundPopup : ContentPopup, IBackgroundDelegate
    {
        public BackgroundViewModel ViewModel => DataContext as BackgroundViewModel;

        public BackgroundPopup(Background background)
            : this()
        {
            _ = ViewModel.NavigatedToAsync(background, NavigationMode.New, null);
        }

        public BackgroundPopup(string slug)
            : this()
        {
            _ = ViewModel.NavigatedToAsync(slug, NavigationMode.New, null);
        }

        private BackgroundPopup()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<BackgroundViewModel, IBackgroundDelegate>(this);

            Title = Strings.Resources.BackgroundPreview;
            PrimaryButtonText = Strings.Resources.Set;
            SecondaryButtonText = Strings.Resources.Cancel;

            Message1.Mockup(Strings.Resources.BackgroundPreviewLine1, false, DateTime.Now.AddSeconds(-25));
            Message2.Mockup(Strings.Resources.BackgroundPreviewLine2, true, DateTime.Now);

            ElementCompositionPreview.GetElementVisual(ContentPanel).Clip = BootStrapper.Current.Compositor.CreateInsetClip();
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            Grid.SetRow(ColorPanel, ColorRadio.IsChecked == true ? 2 : 4);

            if (ColorRadio.IsChecked == true)
            {
                PatternRadio.IsChecked = false;
            }

            RadioColor_Toggled(null, null);
        }

        private void Pattern_Click(object sender, RoutedEventArgs e)
        {
            Grid.SetRow(PatternPanel, PatternRadio.IsChecked == true ? 2 : 4);

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

            //Header.CommandVisibility = wallpaper.Id != Constants.WallpaperLocalId ? Visibility.Visible : Visibility.Collapsed;

            if (wallpaper.Type is BackgroundTypeWallpaper)
            {
                Blur.Visibility = Visibility.Visible;

                Message1.Mockup(Strings.Resources.BackgroundPreviewLine1, false, DateTime.Now.AddSeconds(-25));
                Message2.Mockup(Strings.Resources.BackgroundPreviewLine2, true, DateTime.Now);
            }
            else
            {
                Blur.Visibility = Visibility.Collapsed;

                if (wallpaper.Type is BackgroundTypeFill or BackgroundTypePattern)
                {
                    Pattern.Visibility = Visibility.Visible;
                    Color.Visibility = Visibility.Visible;
                }

                Message1.Mockup(Strings.Resources.BackgroundColorSinglePreviewLine1, false, DateTime.Now.AddSeconds(-25));
                Message2.Mockup(Strings.Resources.BackgroundColorSinglePreviewLine2, true, DateTime.Now);
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
                    var content = container.ContentTemplateRoot as ChatBackgroundRenderer;
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

            var document = args.Item as Document;
            var content = args.ItemContainer.ContentTemplateRoot as ChatBackgroundRenderer;

            var background = ViewModel.GetPattern(document);

            content.UpdateSource(ViewModel.ClientService, background, true);
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
    }
}
