using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using Unigram.Common;
using Telegram.Api.TL;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class StickersView : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public StickersView()
        {
            InitializeComponent();
        }

        private void Gifs_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = GifsView.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scrollingHost != null)
            {
                // Source: https://github.com/JustinXinLiu/StickyHeader_WindowsComposition
                // Thanks Justin! :D

                var scrollProperties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollingHost);
                var stickyHeaderVisual = ElementCompositionPreview.GetElementVisual(StickyHeader);
                var compositor = scrollProperties.Compositor;

                var scrollingAnimation = compositor.CreateExpressionAnimation("ScrollingProperties.Translation.Y > OffsetY ? 0 : OffsetY - ScrollingProperties.Translation.Y");
                scrollingAnimation.SetReferenceParameter("ScrollingProperties", scrollProperties);
                scrollingAnimation.SetScalarParameter("OffsetY", 0);

                stickyHeaderVisual.StartAnimation("Offset.Y", scrollingAnimation);
            }
        }

        private void Stickers_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = Stickers.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scrollingHost != null)
            {
                // Syncronizes GridView with the toolbar ListView
                scrollingHost.ViewChanged += ScrollingHost_ViewChanged;
            }
        }

        private void Gifs_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.SendGifCommand.Execute(e.ClickedItem);
        }

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.SendStickerCommand.Execute(e.ClickedItem);
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Pivot.SelectedIndex == 0)
            {
                if (Toolbar.Items.Count > 0)
                {
                    Toolbar.SelectedIndex = 0;
                }
            }
            else
            {
                // TODO
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Pivot.SelectedIndex = Math.Min(1, Toolbar.SelectedIndex);
            //Stickers.ScrollIntoView(ViewModel.StickerSets[Toolbar.SelectedIndex][0]);
        }

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scrollingHost = Stickers.ItemsPanelRoot as ItemsWrapGrid;
            if (scrollingHost != null)
            {
                var first = Stickers.ContainerFromIndex(scrollingHost.FirstVisibleIndex);
                if (first != null)
                {
                    var header = Stickers.GroupHeaderContainerFromItemContainer(first) as GridViewHeaderItem;
                    if (header != null && header != Toolbar.SelectedItem)
                    {
                        Toolbar.SelectedItem = header.Content;
                        Toolbar.ScrollIntoView(header.Content);
                    }
                }
            }
        }
    }
}
