﻿//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;

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

                var visual1 = ElementComposition.GetElementVisual(DonePanel);
                var visual2 = ElementComposition.GetElementVisual(Cooldown);

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
