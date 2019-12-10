using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyShowPhonePage : Page
    {
        public SettingsPrivacyPhoneViewModel ViewModel => DataContext as SettingsPrivacyPhoneViewModel;

        public SettingsPrivacyShowPhonePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPrivacyPhoneViewModel>();
        }

        #region Binding

        private Visibility ConvertNever(PrivacyValue value)
        {
            return value == PrivacyValue.AllowAll || value == PrivacyValue.AllowContacts ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertAlways(PrivacyValue value)
        {
            return value == PrivacyValue.AllowContacts || value == PrivacyValue.DisallowAll ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertFinding(PrivacyValue value)
        {
            return value == PrivacyValue.DisallowAll ? Visibility.Visible : Visibility.Collapsed;
        }

        private string ConvertFooter(PrivacyValue value)
        {
            if (value == PrivacyValue.AllowAll)
            {
                return Strings.Resources.PrivacyPhoneInfo2;
            }

            return Strings.Resources.PrivacyPhoneInfo3;
        }

        #endregion
    }
}
