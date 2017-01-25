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

        private void Gifs_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.SendGifCommand.Execute(e.ClickedItem);
        }

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.SendStickerCommand.Execute(e.ClickedItem);
        }
    }
}
