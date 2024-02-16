//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Td;
using Telegram.ViewModels.Settings.Privacy;

namespace Telegram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyNewChatPage : HostedPage
    {
        public SettingsPrivacyNewChatViewModel ViewModel => DataContext as SettingsPrivacyNewChatViewModel;

        public SettingsPrivacyNewChatPage()
        {
            InitializeComponent();
            Title = Strings.PrivacyMessages;

            var formatted = Extensions.ReplacePremiumLink(Strings.PrivacyMessagesInfo, null);
            var markdown = ClientEx.GetMarkdownText(formatted);

            Group.Footer = markdown.Text;
        }
    }
}
