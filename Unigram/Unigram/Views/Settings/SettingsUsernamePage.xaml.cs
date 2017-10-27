using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Controls;
using Unigram.Views;
using Unigram.ViewModels.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsUsernamePage : Page
    {
        public SettingsUsernameViewModel ViewModel => DataContext as SettingsUsernameViewModel;

        public SettingsUsernamePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsUsernameViewModel>();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(Username, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                if (ViewModel.UpdateIsValid(Username.Text))
                {
                    ViewModel.CheckAvailability(Username.Text);
                }
            });
        }

        private void Copy_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            ViewModel.CopyCommand.Execute();
        }
    }
}
