using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.ViewModels.Settings.Password;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings.Password
{
    public sealed partial class SettingsPasswordEmailPage : Page
    {
        public SettingsPasswordEmailViewModel ViewModel => DataContext as SettingsPasswordEmailViewModel;

        public SettingsPasswordEmailPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasswordEmailViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }

        private void Walkthrough_Loaded(object sender, RoutedEventArgs e)
        {
            Walkthrough.Header.IndexChanged += Lottie_IndexChanged;
        }

        private int _stop;

        private void Field_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var index = Field1.SelectionStart + Field1.SelectionLength - 1;
            if (index >= 0)
            {
                var rect = Field1.GetRectFromCharacterIndex(Field1.SelectionStart + Field1.SelectionLength - 1, false);
                var position = rect.X / Field1.ActualWidth;

                _stop = (int)(20 + (Math.Min(1, Math.Max(0, position)) * 140));
                Walkthrough.Header.Play(Walkthrough.Header.Index > _stop);
            }
            else
            {
                _stop = 0;
                Walkthrough.Header.Play(true);
            }
        }

        private void Lottie_IndexChanged(object sender, int e)
        {
            this.BeginOnUIThread(() =>
            {
                if (e == _stop)
                {
                    Walkthrough.Header.Pause();
                }
            });
        }
    }
}
