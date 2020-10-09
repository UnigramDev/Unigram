using System.Numerics;
using System.Text;
using Unigram.Common;
using Unigram.Entities;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.SignIn;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignInPage : Page, ISignInDelegate
    {
        public SignInViewModel ViewModel => DataContext as SignInViewModel;

        public SignInPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SignInViewModel, ISignInDelegate>(this);

            Transitions = ApiInfo.CreateSlideTransition();

            Diagnostics.Text = $"Unigram " + GetVersion();

            var token = ElementCompositionPreview.GetElementVisual(Token);
            token.Opacity = 0;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private string GetVersion()
        {
            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;
            return string.Format("{0}.{1} ({2})", version.Major, version.Minor, version.Build, version.Revision);
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "PHONE_CODE_INVALID":
                    VisualUtilities.ShakeView(PhoneCode);
                    break;
                case "PHONE_NUMBER_INVALID":
                    VisualUtilities.ShakeView(PrimaryInput);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        private void Countries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems?.Count > 0)
            {
                PrimaryInput.Focus(FocusState.Keyboard);
            }
        }

        private void PhoneNumber_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SendCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Back && string.IsNullOrEmpty(PrimaryInput.Text))
            {
                PhoneCode.Focus(FocusState.Keyboard);
                PhoneCode.SelectionStart = PhoneCode.Text.Length;
                e.Handled = true;
            }
        }

        #region Binding

        private string ConvertFormat(Country country)
        {
            if (country == null)
            {
                return null;
            }

            var groups = PhoneNumber.Parse(country.PhoneCode);
            var builder = new StringBuilder();

            for (int i = 1; i < groups.Length; i++)
            {
                for (int j = 0; j < groups[i]; j++)
                {
                    builder.Append('-');
                }

                if (i + 1 < groups.Length)
                {
                    builder.Append(' ');
                }
            }

            return builder.ToString();
        }

        #endregion

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
            TokenPanel.Visibility = Visibility.Visible;
            PhonePanel.Visibility = Visibility.Visible;

            await PhonePanel.UpdateLayoutAsync();

            var logo1 = ElementCompositionPreview.GetElementVisual(Logo1);
            var logo2 = ElementCompositionPreview.GetElementVisual(Logo2);
            var token = ElementCompositionPreview.GetElementVisual(Token);
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
            TokenPanel.Visibility = Visibility.Visible;
            PhonePanel.Visibility = Visibility.Visible;

            await TokenPanel.UpdateLayoutAsync();

            var logo1 = ElementCompositionPreview.GetElementVisual(Logo1);
            var logo2 = ElementCompositionPreview.GetElementVisual(Logo2);
            var token = ElementCompositionPreview.GetElementVisual(Token);
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
            if (mode == QrCodeMode.Loading)
            {
                Loading.IsActive = true;
                TokenPanel.Visibility = Visibility.Collapsed;
                PhonePanel.Visibility = Visibility.Collapsed;
            }
            else if (mode == QrCodeMode.Disabled || mode == QrCodeMode.Secondary)
            {
                Loading.IsActive = false;
                TokenPanel.Visibility = Visibility.Collapsed;
                PhonePanel.Visibility = Visibility.Visible;
                Switch2.Visibility = mode == QrCodeMode.Secondary
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else if (mode == QrCodeMode.Primary)
            {
                Loading.IsActive = false;
                TokenPanel.Visibility = Visibility.Visible;
                PhonePanel.Visibility = Visibility.Collapsed;
                Switch2.Visibility = Visibility.Visible;
            }
        }

        public void UpdateQrCode(string code)
        {
            //if (Token.Source != null)
            //{
            //    return;
            //}

            var token = ElementCompositionPreview.GetElementVisual(Token);
            if (token.Opacity != 0)
            {
                return;
            }

            var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0.0f, 0);
            opacity.InsertKeyFrame(1.0f, 1);
            token.StartAnimation("Opacity", opacity);
        }
    }
}