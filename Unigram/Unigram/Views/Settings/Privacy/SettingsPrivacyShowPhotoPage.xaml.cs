using Microsoft.UI.Xaml;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyShowPhotoPage : HostedPage
    {
        public SettingsPrivacyShowPhotoViewModel ViewModel => DataContext as SettingsPrivacyShowPhotoViewModel;

        public SettingsPrivacyShowPhotoPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPrivacyShowPhotoViewModel>();
        }

        #region Binding

        private Visibility ConvertNever(PrivacyValue value)
        {
            return value is PrivacyValue.AllowAll or PrivacyValue.AllowContacts ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertAlways(PrivacyValue value)
        {
            return value is PrivacyValue.AllowContacts or PrivacyValue.DisallowAll ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

    }
}
