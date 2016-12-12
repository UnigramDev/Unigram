using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Template10.Common;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class PhotosView : ContentDialogBase
    {
        public PhotosViewModelBase ViewModel => DataContext as PhotosViewModelBase;

        private FrameworkElement _firstImage;

        public PhotosView()
        {
            this.InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BootStrapper.BackRequested += BootStrapper_BackRequested;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            BootStrapper.BackRequested -= BootStrapper_BackRequested;
        }

        private void BootStrapper_BackRequested(object sender, HandledEventArgs e)
        {
            if (Flip.SelectedIndex == 0 && _firstImage != null)
            {
                var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", _firstImage);
                if (animation != null)
                {
                    LayoutRoot.Background = null;
                    Prepare();

                    animation.Completed += (s, args) =>
                    {
                        Hide();
                    };
                }
            }
            else
            {
                Hide();
            }

            e.Handled = true;
        }

        private void ImageView_ImageOpened(object sender, RoutedEventArgs e)
        {
            var image = sender as FrameworkElement;
            if (image.DataContext == ViewModel.SelectedItem)
            {
                _firstImage = image;

                var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                if (animation != null)
                {
                    Flip.Opacity = 1;
                    animation.Completed += (s1, args1) =>
                    {
                        LayoutRoot.Background = new SolidColorBrush(Windows.UI.Colors.Black);
                    };
                    animation.TryStart(image);
                }
            }
        }

        //protected override void UpdateView(Rect bounds)
        //{

        //}
    }
}
