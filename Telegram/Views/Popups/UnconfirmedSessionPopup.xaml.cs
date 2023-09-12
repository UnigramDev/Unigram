using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Popups
{
    public sealed partial class UnconfirmedSessionPopup : ContentPopup
    {
        private DispatcherTimer _timer;
        private int _cooldown = 5;

        public UnconfirmedSessionPopup(UnconfirmedSession session)
        {
            InitializeComponent();

            Icon.Source = new LocalFileSource("ms-appx:///Assets/Animations/Banned.tgs");

            // TODO: multiple sessions?
            Title.Text = Locale.Declension(Strings.R.UnconfirmedAuthDeniedTitle, 1);
            TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.UnconfirmedAuthDeniedMessageSingle, string.Format("{0}, {1}", session.DeviceModel, session.Location)));

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += OnTick;
            _timer.Start();

            ElementCompositionPreview.SetIsTranslationEnabled(DonePanel, true);
        }

        private void OnTick(object sender, object e)
        {
            _cooldown--;

            if (_cooldown <= 0)
            {
                _timer.Stop();

                var visual1 = ElementCompositionPreview.GetElementVisual(DonePanel);
                var visual2 = ElementCompositionPreview.GetElementVisual(Cooldown);

                var translation = visual1.Compositor.CreateScalarKeyFrameAnimation();
                translation.InsertKeyFrame(0, 0);
                translation.InsertKeyFrame(1, Cooldown.ActualSize.X);

                var opacity = visual1.Compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(0, 1);
                opacity.InsertKeyFrame(1, 0);

                visual1.StartAnimation("Translation.X", translation);
                visual2.StartAnimation("Opacity", opacity);
            }

            Cooldown.Text = $"{_cooldown}";
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            if (_cooldown > 0)
            {
                VisualUtilities.ShakeView(sender as Button);
            }
            else
            {
                Hide();
            }
        }

        private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            await Task.Delay(1000);
            Icon.Play();
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            args.Cancel = _cooldown > 0;
        }
    }
}
