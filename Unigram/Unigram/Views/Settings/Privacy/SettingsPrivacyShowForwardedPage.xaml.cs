using System;
using System.Numerics;
using Unigram.Common;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyShowForwardedPage : HostedPage
    {
        public SettingsPrivacyShowForwardedViewModel ViewModel => DataContext as SettingsPrivacyShowForwardedViewModel;

        public SettingsPrivacyShowForwardedPage()
        {
            InitializeComponent();
            Title = Strings.Resources.PrivacyForwards;

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "Shadow"))
            {
                var themeShadow = new ThemeShadow();
                ToolTip.Shadow = themeShadow;
                ToolTip.Translation += new Vector3(0, 0, 32);

                themeShadow.Receivers.Add(BackgroundPresenter);
                themeShadow.Receivers.Add(MessagePreview);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var user = ViewModel.ClientService.GetUser(ViewModel.ClientService.Options.MyId);
            if (user != null)
            {
                MessagePreview.Mockup(Strings.Resources.PrivacyForwardsMessageLine, user.FullName(), true, false, DateTime.Now);
            }

            BackgroundPresenter.Update(ViewModel.SessionId, ViewModel.ClientService, ViewModel.Aggregator);
        }

        #region Binding

        private string ConvertToolTip(PrivacyValue value)
        {
            return value == PrivacyValue.AllowAll ? Strings.Resources.PrivacyForwardsEverybody : value == PrivacyValue.AllowContacts ? Strings.Resources.PrivacyForwardsContacts : Strings.Resources.PrivacyForwardsNobody;
        }

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
