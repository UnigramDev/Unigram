//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.ViewModels.Authorization;
using Telegram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Authorization
{
    public sealed partial class AuthorizationPage : Page, ISignInDelegate
    {
        public AuthorizationViewModel ViewModel => DataContext as AuthorizationViewModel;

        public AuthorizationPage()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);

            TokenPlaceholder.FrameSize = new Size(259, 259);
            TokenPlaceholder.DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical;
            TokenPlaceholder.ColorReplacements = new Dictionary<int, int>
            {
                { 0xffffff, 0x000000 }
            };

            Diagnostics.Text = $"Unigram " + SettingsPage.GetVersion();
        }

        private bool _waiting = true;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "PHONE_NUMBER_INVALID":
                    VisualUtilities.ShakeView(PrimaryInput);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        private void Countries_SelectionChanged(object sender, EventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        private void PhoneNumber_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SendCommand.Execute(null);
                e.Handled = true;
            }
        }

        private int _advanced;

        private void Diagnostics_Click(object sender, RoutedEventArgs e)
        {
            _advanced++;

            if (_advanced >= 10)
            {
                _advanced = 0;

                Frame.Navigate(typeof(DiagnosticsPage));
            }
        }

        private async void Switch1_Click(object sender, RoutedEventArgs e)
        {
            _waiting = false;
            TokenPanel.Visibility = Visibility.Visible;
            PhonePanel.Visibility = Visibility.Visible;

            await PhonePanel.UpdateLayoutAsync();

            var logo1 = ElementCompositionPreview.GetElementVisual(Logo1);
            var logo2 = ElementCompositionPreview.GetElementVisual(Logo2);
            var token = ElementCompositionPreview.GetElementVisual(TokenRoot);
            var inner1 = ElementCompositionPreview.GetElementVisual(TokenInnerPanel);
            var inner2 = ElementCompositionPreview.GetElementVisual(PhoneInnerPanel);

            var transform1 = Logo2Panel.TransformToVisual(Logo1Panel);
            var point1 = transform1.TransformPoint(new Point()).ToVector2();

            var transform2 = Logo1Panel.TransformToVisual(Logo2Panel);
            var point2 = transform2.TransformPoint(new Point()).ToVector2();

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                TokenPanel.Visibility = Visibility.Collapsed;
                PhonePanel.Visibility = Visibility.Visible;
            };

            // Small to big
            var offset1 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(0, new Vector3());
            offset1.InsertKeyFrame(1, new Vector3(point1.X, point1.Y, 0));

            var scale1 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(0, new Vector3(1));
            scale1.InsertKeyFrame(1, new Vector3(160f / 48f));

            var opacity1 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(0, 1);
            opacity1.InsertKeyFrame(1, 0);

            logo1.StartAnimation("Offset", offset1);
            logo1.StartAnimation("Scale", scale1);
            logo1.StartAnimation("Opacity", opacity1);

            // Big to small
            var offset2 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            offset2.InsertKeyFrame(0, new Vector3(point2.X, point2.Y, 0));
            offset2.InsertKeyFrame(1, new Vector3());

            var scale2 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale2.InsertKeyFrame(0, new Vector3(48f / 160f));
            scale2.InsertKeyFrame(1, new Vector3(1));

            var opacity2 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(0, 0);
            opacity2.InsertKeyFrame(1, 1);

            logo2.StartAnimation("Offset", offset2);
            logo2.StartAnimation("Scale", scale2);
            logo2.StartAnimation("Opacity", opacity2);

            // Other
            var scale3 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale3.InsertKeyFrame(0, new Vector3(1));
            scale3.InsertKeyFrame(1, new Vector3(0));

            var opacity3 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity3.InsertKeyFrame(0, 1);
            opacity3.InsertKeyFrame(1, 0);

            token.CenterPoint = new Vector3(120);
            token.StartAnimation("Opacity", opacity3);
            token.StartAnimation("Scale", scale3);

            inner1.StartAnimation("Opacity", opacity1);
            inner2.StartAnimation("Opacity", opacity2);

            batch.End();
        }

        private async void Switch2_Click(object sender, RoutedEventArgs e)
        {
            _waiting = true;
            TokenPanel.Visibility = Visibility.Visible;
            PhonePanel.Visibility = Visibility.Visible;

            await TokenPanel.UpdateLayoutAsync();

            var logo1 = ElementCompositionPreview.GetElementVisual(Logo1);
            var logo2 = ElementCompositionPreview.GetElementVisual(Logo2);
            var token = ElementCompositionPreview.GetElementVisual(TokenRoot);
            var inner1 = ElementCompositionPreview.GetElementVisual(TokenInnerPanel);
            var inner2 = ElementCompositionPreview.GetElementVisual(PhoneInnerPanel);

            var transform1 = Logo2Panel.TransformToVisual(Logo1Panel);
            var point1 = transform1.TransformPoint(new Point()).ToVector2();

            var transform2 = Logo1Panel.TransformToVisual(Logo2Panel);
            var point2 = transform2.TransformPoint(new Point()).ToVector2();

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                TokenPanel.Visibility = Visibility.Visible;
                PhonePanel.Visibility = Visibility.Collapsed;
            };

            // Small to big
            var offset1 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(1, new Vector3());
            offset1.InsertKeyFrame(0, new Vector3(point1.X, point1.Y, 0));

            var scale1 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(1, new Vector3(1));
            scale1.InsertKeyFrame(0, new Vector3(160f / 48f));

            var opacity1 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(1, 1);
            opacity1.InsertKeyFrame(0, 0);

            logo1.StartAnimation("Offset", offset1);
            logo1.StartAnimation("Scale", scale1);
            logo1.StartAnimation("Opacity", opacity1);

            // Big to small
            var offset2 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            offset2.InsertKeyFrame(1, new Vector3(point2.X, point2.Y, 0));
            offset2.InsertKeyFrame(0, new Vector3());

            var scale2 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale2.InsertKeyFrame(1, new Vector3(48f / 160f));
            scale2.InsertKeyFrame(0, new Vector3(1));

            var opacity2 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(1, 0);
            opacity2.InsertKeyFrame(0, 1);

            logo2.StartAnimation("Offset", offset2);
            logo2.StartAnimation("Scale", scale2);
            logo2.StartAnimation("Opacity", opacity2);

            // Other
            var scale3 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale3.InsertKeyFrame(0, new Vector3(0));
            scale3.InsertKeyFrame(1, new Vector3(1));

            var opacity3 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity3.InsertKeyFrame(0, 0);
            opacity3.InsertKeyFrame(1, 1);

            token.CenterPoint = new Vector3(120);
            token.StartAnimation("Opacity", opacity3);
            token.StartAnimation("Scale", scale3);

            inner1.StartAnimation("Opacity", opacity1);
            inner2.StartAnimation("Opacity", opacity2);

            batch.End();
        }

        public void UpdateQrCodeMode(QrCodeMode mode)
        {
            if (mode is QrCodeMode.Loading or QrCodeMode.Primary)
            {
                if (_waiting)
                {
                    TokenPanel.Visibility = Visibility.Visible;
                    PhonePanel.Visibility = Visibility.Collapsed;
                }

                Switch2.Visibility = Visibility.Visible;

                if (mode == QrCodeMode.Loading)
                {
                    TokenPlaceholder.Source = new Uri("ms-appx:///Assets/Animations/Qr.tgs");
                }
            }
            else if (mode is QrCodeMode.Disabled or QrCodeMode.Secondary)
            {
                TokenPanel.Visibility = Visibility.Collapsed;
                PhonePanel.Visibility = Visibility.Visible;
                Switch2.Visibility = mode == QrCodeMode.Secondary
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                TokenPlaceholder.Unload();
            }
        }

        public void UpdateQrCode(string code, bool firstTime)
        {
            if (firstTime is false)
            {
                return;
            }

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                TokenPlaceholder.Unload();
            };

            var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            //opacity.InsertKeyFrame(0.0f, 0);
            //opacity.InsertKeyFrame(1.0f, 1);
            //token.StartAnimation("Opacity", opacity);

            var placeholder = ElementCompositionPreview.GetElementVisual(TokenPlaceholder);

            opacity.InsertKeyFrame(0.0f, 1);
            opacity.InsertKeyFrame(1.0f, 0);
            placeholder.StartAnimation("Opacity", opacity);

            batch.End();
        }
    }
}