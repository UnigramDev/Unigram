//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Views.Popups
{
    public sealed partial class CollectiblePopup : ContentPopup
    {
        private readonly string _value;
        private readonly string _url;

        public CollectiblePopup(IClientService clientService, Chat chat, CollectibleItemInfo info, CollectibleItemType type)
        {
            InitializeComponent();

            string formattedValue;
            string title;
            string description;
            string secondary;

            if (type is CollectibleItemTypeUsername username)
            {
                Icon.Source = new LocalFileSource($"ms-appx:///Assets/Animations/CollectibleUsername.tgs");

                formattedValue = string.Format("@{0}", username.Username);
                title = Strings.FragmentUsernameTitle;
                description = Strings.FragmentUsernameMessage;
                secondary = Strings.FragmentUsernameCopy;
            }
            else if (type is CollectibleItemTypePhoneNumber phoneNumber)
            {
                Icon.Source = new LocalFileSource($"ms-appx:///Assets/Animations/CollectibleUsername.tgs");

                formattedValue = PhoneNumber.Format(phoneNumber.PhoneNumber);
                title = Strings.FragmentPhoneTitle;
                description = Strings.FragmentPhoneMessage;
                secondary = Strings.FragmentPhoneCopy;
            }
            else
            {
                return;
            }

            _value = formattedValue;
            _url = info.Url;

            var date = Formatter.Date(info.PurchaseDate);
            var crypto = Formatter.Amount(info.CryptocurrencyAmount, info.Cryptocurrency);
            var amount = Formatter.FormatAmount(info.Amount, info.Currency);

            title = string.Format(title, formattedValue);
            description = string.Format(description, date, "\uEA7E" + crypto, $"({amount})");

            Pill.SetChat(clientService, chat);

            var start = title.IndexOf("**");
            var end = title.IndexOf("**", start + 2);

            if (start != -1 && end != -1)
            {
                if (start > 0)
                {
                    Title.Inlines.Add(title.Substring(0, start));
                }

                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(title.Substring(start + 2, end - start - 2));
                hyperlink.UnderlineStyle = UnderlineStyle.None;

                Title.Inlines.Add(hyperlink);

                if (title.Length - end + 2 > 0)
                {
                    Title.Inlines.Add(title.Substring(end + 2));
                }
            }
            else
            {
                Title.Text = title;
            }

            TextBlockHelper.SetMarkdown(Subtitle, description);

            LearnCommand.Content = Strings.FragmentUsernameOpen;
            CopyCommand.Content = secondary;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            Icon.Play();
        }

        private void Learn_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
            MessageHelper.OpenUrl(null, null, _url);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Secondary);
            MessageHelper.CopyText(_value);
        }
    }
}
