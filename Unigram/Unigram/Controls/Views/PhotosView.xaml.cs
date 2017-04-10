using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Template10.Common;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
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

        private Visual _layerVisual;
        private Visual _topBarVisual;
        private Visual _botBarVisual;

        public PhotosView()
        {
            InitializeComponent();

            _layerVisual = ElementCompositionPreview.GetElementVisual(Layer);
            _topBarVisual = ElementCompositionPreview.GetElementVisual(TopBar);
            _botBarVisual = ElementCompositionPreview.GetElementVisual(BotBar);

            _layerVisual.Opacity = 0;
            _topBarVisual.Offset = new Vector3(0, -48, 0);
            _botBarVisual.Offset = new Vector3(0, 48, 0);
        }

        protected override void OnBackRequested(object sender, HandledEventArgs e)
        {
            if (Flip.SelectedIndex == 0 && _firstImage != null && _firstImage.ActualWidth > 0)
            {
                var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("FullScreenPicture", _firstImage);
                if (animation != null)
                {
                    Prepare();

                    if (_layerVisual != null && _topBarVisual != null && _botBarVisual != null)
                    {
                        var batch = _layerVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                        var easing = ConnectedAnimationService.GetForCurrentView().DefaultEasingFunction;
                        var duration = ConnectedAnimationService.GetForCurrentView().DefaultDuration;

                        var animOpacity = _layerVisual.Compositor.CreateScalarKeyFrameAnimation();
                        animOpacity.InsertKeyFrame(0, 1, easing);
                        animOpacity.InsertKeyFrame(1, 0, easing);
                        animOpacity.Duration = duration;
                        _layerVisual.StartAnimation("Opacity", animOpacity);

                        var animTop = _layerVisual.Compositor.CreateVector3KeyFrameAnimation();
                        animTop.InsertKeyFrame(1, new Vector3(0, -48, 0), easing);
                        animTop.Duration = duration;
                        _topBarVisual.StartAnimation("Offset", animTop);

                        var animBot = _layerVisual.Compositor.CreateVector3KeyFrameAnimation();
                        animBot.InsertKeyFrame(1, new Vector3(0, 48, 0), easing);
                        animBot.Duration = duration;
                        _botBarVisual.StartAnimation("Offset", animBot);

                        batch.End();
                    }

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
            if (image != null)
            {
                image.Opacity = 1;
            }

            if (image.DataContext == ViewModel.SelectedItem)
            {
                _firstImage = image;

                var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("FullScreenPicture");
                if (animation != null)
                {
                    if (_layerVisual != null && _topBarVisual != null && _botBarVisual != null)
                    {
                        var batch = _layerVisual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                        var easing = ConnectedAnimationService.GetForCurrentView().DefaultEasingFunction;
                        var duration = ConnectedAnimationService.GetForCurrentView().DefaultDuration;

                        var animOpacity = _layerVisual.Compositor.CreateScalarKeyFrameAnimation();
                        animOpacity.InsertKeyFrame(0, 0, easing);
                        animOpacity.InsertKeyFrame(1, 1, easing);
                        animOpacity.Duration = duration;
                        _layerVisual.StartAnimation("Opacity", animOpacity);

                        var animTop = _layerVisual.Compositor.CreateVector3KeyFrameAnimation();
                        animTop.InsertKeyFrame(1, new Vector3(0, 0, 0), easing);
                        animTop.Duration = duration;
                        _topBarVisual.StartAnimation("Offset", animTop);

                        var animBot = _layerVisual.Compositor.CreateVector3KeyFrameAnimation();
                        animBot.InsertKeyFrame(1, new Vector3(0, 0, 0), easing);
                        animBot.Duration = duration;
                        _botBarVisual.StartAnimation("Offset", animBot);

                        batch.End();
                    }

                    Flip.Opacity = 1;
                    animation.TryStart(image);
                }
            }
        }

        private void ImageView_Unloaded(object sender, RoutedEventArgs e)
        {
            var image = sender as FrameworkElement;
            if (image != null)
            {
                image.Opacity = 0;
            }
        }

        //protected override void UpdateView(Rect bounds)
        //{

        //}
    }
}
