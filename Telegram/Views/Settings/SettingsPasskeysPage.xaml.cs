//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsPasskeysPage : HostedPage
    {
        public SettingsPasskeysViewModel ViewModel => DataContext as SettingsPasskeysViewModel;

        public SettingsPasskeysPage()
        {
            InitializeComponent();
            Title = Strings.Passkey;
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is Passkey passkey)
            {
                var animated = content.FindName("Animated") as AnimatedImage;
                var title = content.FindName("TitleLabel") as TextBlock;
                var subtitle = content.FindName("SubtitleLabel") as TextBlock;

                animated.Source = new CustomEmojiFileSource(ViewModel.ClientService, passkey.SoftwareIconCustomEmojiId);
                title.Text = passkey.Name.Length > 0 ? passkey.Name : Strings.PasskeyUnknown;
                subtitle.Text = passkey.LastUsageDate != 0
                    ? string.Format(Strings.PasskeyLastUsedOn, Formatter.DateAt(passkey.LastUsageDate))
                    : string.Format(Strings.PasskeyCreatedOn, Formatter.DateAt(passkey.AdditionDate));
            }
        }

        #endregion

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var passkey = ScrollingHost.ItemFromContainer(sender) as Passkey;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.Delete, passkey, Strings.Delete, Icons.Delete, destructive: true);
            flyout.ShowAt(sender, args);
        }

        private void Info_Click(object sender, TextUrlClickEventArgs e)
        {
            ViewModel.Info();
        }
    }
}
