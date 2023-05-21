//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Settings.Privacy;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyShowForwardedPage : HostedPage
    {
        public SettingsPrivacyShowForwardedViewModel ViewModel => DataContext as SettingsPrivacyShowForwardedViewModel;

        public SettingsPrivacyShowForwardedPage()
        {
            InitializeComponent();
            Title = Strings.PrivacyForwards;

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.UIElement", "Shadow"))
            {
                var themeShadow = new ThemeShadow();
                ToolTip.Shadow = themeShadow;
                ToolTip.Translation += new Vector3(0, 0, 32);

                themeShadow.Receivers.Add(BackgroundControl);
                themeShadow.Receivers.Add(MessagePreview);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var user = ViewModel.ClientService.GetUser(ViewModel.ClientService.Options.MyId);
            if (user != null)
            {
                MessagePreview.Mockup(Strings.PrivacyForwardsMessageLine, user.FullName(), true, false, DateTime.Now);
            }

            BackgroundControl.Update(ViewModel.ClientService, ViewModel.Aggregator);
        }

        #region Binding

        private string ConvertToolTip(PrivacyValue value)
        {
            return value == PrivacyValue.AllowAll ? Strings.PrivacyForwardsEverybody : value == PrivacyValue.AllowContacts ? Strings.PrivacyForwardsContacts : Strings.PrivacyForwardsNobody;
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
