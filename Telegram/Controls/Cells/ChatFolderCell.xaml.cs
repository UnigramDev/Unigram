//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Cells
{
    public sealed partial class ChatFolderCell : UserControl
    {
        public ChatFolderViewModel ViewModel => DataContext as ChatFolderViewModel;

        public ChatFolderCell()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;

            ElementCompositionPreview.SetIsTranslationEnabled(UnselectedIcon, true);
            ElementCompositionPreview.SetIsTranslationEnabled(SelectedIcon, true);

            var iconUnselected = ElementCompositionPreview.GetElementVisual(UnselectedIcon);
            var iconSelected = ElementCompositionPreview.GetElementVisual(SelectedIcon);

            iconUnselected.Opacity = 1;
            iconSelected.Opacity = 0;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            Bindings.Update();
        }

        private void OnCurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            var prev = e.OldState?.Name;
            var next = e.NewState?.Name;

            var compositor = Window.Current.Compositor;

            var iconUnselected = ElementCompositionPreview.GetElementVisual(UnselectedIcon);
            var iconSelected = ElementCompositionPreview.GetElementVisual(SelectedIcon);

            var title = ElementCompositionPreview.GetElementVisual(Title);

            if (next == "PointerOver")
            {
                title.StopAnimation("Opacity");
                title.Opacity = 1;

                iconUnselected.StopAnimation("Opacity");
                iconUnselected.Opacity = 1;
            }
            else if (next == "Pressed")
            {
                var offset = compositor.CreateVector3KeyFrameAnimation();
                offset.InsertKeyFrame(0, new Vector3());
                offset.InsertKeyFrame(1, new Vector3(0, -3, 0));

                iconUnselected.StartAnimation("Translation", offset);
                iconSelected.StartAnimation("Translation", offset);

                title.StopAnimation("Opacity");
                title.Opacity = 0.8f;

                iconUnselected.StopAnimation("Opacity");
                iconUnselected.Opacity = 0.6f;
            }
            else if (next == "Selected" && prev == null)
            {
                title.StopAnimation("Opacity");
                title.Opacity = 0;

                iconUnselected.Opacity = 0;
                iconUnselected.Properties.InsertVector3("Translation", new Vector3(0, 7, 0));

                iconSelected.Opacity = 1;
                iconSelected.Properties.InsertVector3("Translation", new Vector3(0, 7, 0));
            }
            else if (next.Contains("Selected") && prev != null && !prev.Contains("Selected"))
            {
                var spring = compositor.CreateSpringVector3Animation();
                spring.InitialValue = new Vector3(0, -3, 0);
                spring.FinalValue = new Vector3(0, 7, 0);
                spring.DampingRatio = 0.5f;

                var fadeIn = compositor.CreateScalarKeyFrameAnimation();
                fadeIn.InsertKeyFrame(0, 0);
                fadeIn.InsertKeyFrame(1, 1);

                var fadeOut = compositor.CreateScalarKeyFrameAnimation();
                fadeOut.InsertKeyFrame(0, 1);
                fadeOut.InsertKeyFrame(1, 0);

                iconUnselected.StartAnimation("Opacity", fadeOut);
                iconUnselected.StartAnimation("Translation", spring);

                iconSelected.StartAnimation("Opacity", fadeIn);
                iconSelected.StartAnimation("Translation", spring);

                fadeOut.Duration = Constants.FastAnimation;
                title.StartAnimation("Opacity", fadeOut);
            }
            else if (next == "Normal" && prev == "Selected")
            {
                var offset = compositor.CreateVector3KeyFrameAnimation();
                offset.InsertKeyFrame(0, new Vector3(0, 7, 0));
                offset.InsertKeyFrame(1, new Vector3());

                var fadeIn = compositor.CreateScalarKeyFrameAnimation();
                fadeIn.InsertKeyFrame(0, 0);
                fadeIn.InsertKeyFrame(1, 0.6f);

                var fadeOut = compositor.CreateScalarKeyFrameAnimation();
                fadeOut.InsertKeyFrame(0, 1);
                fadeOut.InsertKeyFrame(1, 0);

                iconUnselected.StartAnimation("Opacity", fadeIn);
                iconUnselected.StartAnimation("Translation", offset);

                iconSelected.StartAnimation("Opacity", fadeOut);
                iconSelected.StartAnimation("Translation", offset);

                fadeIn.InsertKeyFrame(1, 0.8f);
                fadeIn.Duration = Constants.FastAnimation;
                title.StartAnimation("Opacity", fadeIn);
            }
            else if (next == "Normal")
            {
                title.StopAnimation("Opacity");
                title.Opacity = 0.8f;

                iconUnselected.StopAnimation("Opacity");
                iconUnselected.Opacity = 0.6f;
            }
        }
    }
}
