using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Views;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Telegram.Api.TL.Auth;
using Unigram.Common;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPhoneSentCodePage : Page
    {
        public SettingsPhoneSentCodeViewModel ViewModel => DataContext as SettingsPhoneSentCodeViewModel;

        public SettingsPhoneSentCodePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsPhoneSentCodeViewModel>();

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "SENT_CODE_INVALID":
                    VisualUtilities.ShakeView(PrimaryInput);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        #region Binding

        private string ConvertType(TLAuthSentCodeTypeBase type, string number)
        {
            switch (type)
            {
                case TLAuthSentCodeTypeApp appType:
                    return Strings.Android.SentAppCode;
                case TLAuthSentCodeTypeSms smsType:
                    return string.Format(Strings.Android.SentSmsCode, number);
            }

            return null;
        }

        #endregion

        public class NavigationParameters
        {
            public string PhoneNumber { get; set; }
            public TLAuthSentCode Result { get; set; }
        }
    }
}
