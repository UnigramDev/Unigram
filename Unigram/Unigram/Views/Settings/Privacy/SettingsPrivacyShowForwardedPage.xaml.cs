using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using Unigram.Common;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Windows.Foundation.Metadata;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyShowForwardedPage : HostedPage
    {
        public SettingsPrivacyShowForwardedViewModel ViewModel => DataContext as SettingsPrivacyShowForwardedViewModel;

        public SettingsPrivacyShowForwardedPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPrivacyShowForwardedViewModel>();

            var user = ViewModel.CacheService.GetUser(ViewModel.CacheService.Options.MyId);
            if (user != null)
            {
                MessagePreview.Mockup(Strings.Resources.PrivacyForwardsMessageLine, user.GetFullName(), true, false, DateTime.Now);
            }

            BackgroundPresenter.Update(ViewModel.SessionId, ViewModel.ProtoService, ViewModel.Aggregator);

            if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.UIElement", "Shadow"))
            {
                var themeShadow = new ThemeShadow();
                ToolTip.Shadow = themeShadow;
                ToolTip.Translation += new Vector3(0, 0, 32);

                themeShadow.Receivers.Add(BackgroundPresenter);
                themeShadow.Receivers.Add(MessagePreview);
            }
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
