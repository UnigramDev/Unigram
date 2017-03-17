using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class StickerSetView : ContentDialog
    {
        public StickerSetViewModel ViewModel => DataContext as StickerSetViewModel;

        public StickerSetView(TLInputStickerSetBase parameter)
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<StickerSetViewModel>();

            Loaded += async (s, args) =>
            {
                await ViewModel.OnNavigatedToAsync(parameter, NavigationMode.New, null);
            };
        }

        private string ConvertIsInstalled(bool installed)
        {
            return string.Format(installed ? "Remove {0} stickers" : "Add {0} stickers", ViewModel.StickerSet.Count);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void GridView_Loaded(object sender, RoutedEventArgs e)
        {
            var scroll = List.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scroll != null)
            {
                scroll.ViewChanged += Scroll_ViewChanged;
                Scroll_ViewChanged(scroll, null);
            }

            var panel = List.ItemsPanelRoot as ItemsWrapGrid;
            if (panel != null)
            {
                panel.SizeChanged += (s, args) =>
                {
                    Scroll_ViewChanged(scroll, null);
                };
            }
        }

        private void Scroll_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scroll = sender as ScrollViewer;
            var top = 1;
            var bottom = 1;

            if (scroll.VerticalOffset == 0)
            {
                top = 0;
            }
            if (scroll.VerticalOffset == scroll.ScrollableHeight)
            {
                bottom = 0;
            }

            Line.BorderThickness = new Thickness(0, top, 0, bottom);
        }
    }
}
