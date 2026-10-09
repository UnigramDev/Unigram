//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.ViewModels;
using Windows.ApplicationModel;
using Windows.System;
using Windows.UI.Xaml;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class MessageUnsupportedContent : MessageService
    {
        public MessageUnsupportedContent()
            : this(false)
        {
        }

        /// <param name="block">
        /// A pageBlockUnsupported inside a rich message, rather than an unsupported message: the
        /// rest of the message did render, so the prompt says so.
        /// </param>
        public MessageUnsupportedContent(bool block)
        {
            InitializeComponent();

            Title.Text = block ? Strings.UnsupportedBlockTitle : Strings.UnsupportedMessageTitle;
            Subtitle.Text = block ? Strings.UnsupportedBlockMessage : Strings.UnsupportedMessageMessage;
        }

        protected override void UpdateContent(MessageViewModel message)
        {
            // The message has no content to show: the whole control is the update prompt.
        }

        private void Service_Click(object sender, RoutedEventArgs e)
        {
            // TODO: show skeleton

            _ = CheckForUpdatesAsync(XamlRoot, Message?.ClientService);

            // TODO: hide skeleton
        }

        public static async Task CheckForUpdatesAsync(XamlRoot xamlRoot, IClientService clientService)
        {
            var service = clientService?.Session.Resolve<ICloudUpdateService>();
            if (service == null)
            {
                // Called from a window with no session to check through, so the Store listing is all
                // that's left to offer.
                await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?PFN=" + Package.Current.Id.FamilyName));
            }
            else
            {
                if (service.NextUpdate == null)
                {
                    await service.UpdateAsync(true);
                }

                if (service.NextUpdate != null)
                {
                    await service.LaunchAsync();
                }
                else
                {
                    ToastPopup.Show(xamlRoot, Strings.CheckForUpdatesInfo, ToastPopupIcon.Info);
                }
            }
        }
    }
}
