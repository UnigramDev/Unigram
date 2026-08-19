//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services;
using Telegram.ViewModels;
using Windows.ApplicationModel;
using Windows.Services.Store;
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
            if (ApiInfo.IsStoreRelease || clientService == null)
            {
                try
                {
                    var context = StoreContext.GetDefault();

                    var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();
                    if (updates == null && updates.Count == 0)
                    {
                        ToastPopup.Show(xamlRoot, Strings.CheckForUpdatesInfo, ToastPopupIcon.Info);
                        return;
                    }
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
                finally
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?PFN=" + Package.Current.Id.FamilyName));
                }
            }
            else
            {
                var service = clientService.Session.Resolve<ICloudUpdateService>();
                if (service != null)
                {
                    if (service.NextUpdate == null)
                    {
                        await service.UpdateAsync(true);
                    }

                    if (service.NextUpdate != null)
                    {
                        await CloudUpdateService.LaunchAsync(false);
                    }
                    else
                    {
                        ToastPopup.Show(xamlRoot, Strings.CheckForUpdatesInfo, ToastPopupIcon.Info);
                    }
                }
            }
        }
    }
}
