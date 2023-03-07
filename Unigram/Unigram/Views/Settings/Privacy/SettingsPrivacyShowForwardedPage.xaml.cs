//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
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
            Title = Strings.Resources.PrivacyForwards;

            if (ApiInformation.IsPropertyPresent("Microsoft.UI.Xaml.UIElement", "Shadow"))
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
