//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Premium.Popups
{
    public enum ChangePrivacyType
    {
        LastSeen,
        ReadDate
    }

    public sealed partial class ChangePrivacyPopup : ContentPopup
    {
        public ChangePrivacyPopup(User user, ChangePrivacyType type, bool premium, bool premiumAvailable)
        {
            InitializeComponent();

            if (type == ChangePrivacyType.LastSeen)
            {
                Icon.Source = new LocalFileSource($"ms-appx:///Assets/Animations/ChangePrivacyLastSeen.tgs");

                Title.Text = Strings.PremiumLastSeenHeader1;
                TextBlockHelper.SetMarkdown(Subtitle, string.Format(premium ? Strings.PremiumLastSeenText1Locked : Strings.PremiumLastSeenText1, user.FirstName));

                if (premium || !premiumAvailable)
                {
                    PrimaryButtonText = Strings.PremiumLastSeenButton1;
                    return;
                }

                FindName(nameof(SubscribeRoot));

                SubscribeTitle.Text = Strings.PremiumLastSeenHeader2;
                TextBlockHelper.SetMarkdown(SubscribeSubtitle, string.Format(Strings.PremiumLastSeenText2, user.FirstName));

                ChangeCommand.Visibility = Visibility.Visible;
                ChangeCommand.Content = Strings.PremiumLastSeenButton1;

                SecondaryButtonText = Strings.PremiumLastSeenButton2;
            }
            else
            {
                Icon.Source = new LocalFileSource($"ms-appx:///Assets/Animations/ChangePrivacyReadDate.tgs");

                Title.Text = Strings.PremiumReadHeader1;
                TextBlockHelper.SetMarkdown(Subtitle, string.Format(premium ? Strings.PremiumReadText1Locked : Strings.PremiumReadText1, user.FirstName));

                if (premium || !premiumAvailable)
                {
                    PrimaryButtonText = Strings.PremiumReadButton1;
                    return;
                }

                FindName(nameof(SubscribeRoot));

                SubscribeTitle.Text = Strings.PremiumReadHeader2;
                TextBlockHelper.SetMarkdown(SubscribeSubtitle, string.Format(Strings.PremiumReadText2, user.FirstName));

                ChangeCommand.Visibility = Visibility.Visible;
                ChangeCommand.Content = Strings.PremiumReadButton1;

                SecondaryButtonText = Strings.PremiumReadButton2;
            }
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            Icon.Play();
        }

        private void Change_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }
    }
}
