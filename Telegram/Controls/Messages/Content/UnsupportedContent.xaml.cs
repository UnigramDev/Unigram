//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.ApplicationModel;
using Windows.Services.Store;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages.Content
{
    public sealed class UnsupportedContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public UnsupportedContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(UnsupportedContent);
        }

        #region InitializeComponent

        private BadgeButton Button;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Button = GetTemplateChild(nameof(Button)) as BadgeButton;
            Button.Content = Strings.UpdateApp.ToUpper();
            Button.Click += Button_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageUnsupported;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            Button.ShowSkeleton();

            if (ApiInfo.IsStoreRelease || _message == null)
            {
                try
                {
                    var context = StoreContext.GetDefault();

                    var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();
                    if (updates == null && updates.Count == 0)
                    {
                        ToastPopup.Show(Strings.CheckForUpdatesInfo, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
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
                var service = TypeResolver.Current.Resolve<ICloudUpdateService>(_message.ClientService.SessionId);
                if (service != null)
                {
                    if (service.NextUpdate == null)
                    {
                        await service.UpdateAsync(true);
                    }

                    if (service.NextUpdate != null)
                    {
                        await CloudUpdateService.LaunchAsync(WindowContext.Current.Dispatcher, false);
                    }
                    else
                    {
                        ToastPopup.Show(Strings.CheckForUpdatesInfo, new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                    }
                }
            }

            Button.HideSkeleton();
        }
    }
}
