//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services.Wallet;
using Telegram.ViewModels.Wallet;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// Keys and backup, over the wallet window.
    /// </summary>
    /// <remarks>
    /// A popup rather than a page because the wallet window has no frame to push a page into: its
    /// navigation service forwards to the window it was opened from, so navigating here would take
    /// the user out of the wallet and into the main window.
    ///
    /// The view model is resolved here for the same reason the window resolves its own - the
    /// navigation machinery only runs for pages - and it is the page's, unchanged, so the content
    /// can go back to being a page if the window ever grows a frame of its own.
    /// </remarks>
    public sealed partial class WalletBackupPopup : ModalPopup
    {
        public WalletBackupViewModel ViewModel => DataContext as WalletBackupViewModel;

        public WalletBackupPopup(INavigationService navigationService)
        {
            var viewModel = navigationService.Session.Resolve<WalletBackupViewModel>();
            viewModel.NavigationService = navigationService;
            viewModel.Dispatcher = navigationService.Dispatcher;

            DataContext = viewModel;

            InitializeComponent();

            Title = "[Keys & Backup]";

            UpdateArchive();
        }

        /// <summary>
        /// Lists the wallets the account has moved on from and this device still has the keys for.
        /// </summary>
        /// <remarks>
        /// Built once, here, rather than bound: the archive only changes when the account's wallet
        /// is replaced, which closes this popup along with everything else showing the old one.
        /// </remarks>
        private void UpdateArchive()
        {
            var archive = ViewModel?.Archive;
            if (archive == null || archive.Count == 0)
            {
                return;
            }

            foreach (var wallet in archive)
            {
                ArchivePanel.Children.Add(CreateArchiveItem(wallet));
            }

            ArchiveRoot.Visibility = Visibility.Visible;
        }

        private UIElement CreateArchiveItem(WalletArchivedWallet wallet)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(12, 8, 12, 8)
            };

            panel.Children.Add(CreateAddress(wallet.Address));

            // The amount and when it last moved, which together are what tells the user whether
            // this is a wallet worth going back for.
            var caption = wallet.LastUsedDate > 0
                ? string.Format("[{0} Grams - last used {1}]", Formatter.TonBalance(wallet.BalanceNanograms).Join(), Formatter.DateAt(wallet.LastUsedDate))
                : string.Format("[{0} Grams]", Formatter.TonBalance(wallet.BalanceNanograms).Join());

            panel.Children.Add(new TextBlock
            {
                Text = caption,
                Style = BootStrapper.Current.Resources["InfoCaptionTextBlockStyle"] as Style,
                Margin = new Thickness(0, 2, 0, 0)
            });

            return panel;
        }

        /// <summary>
        /// The address over two lines, six groups of four to a line, every other group dimmed.
        /// </summary>
        /// <remarks>
        /// The alternation is what makes two addresses comparable by eye, and it is the same
        /// pattern the transaction receipt uses - there over four lines, because there is room.
        /// </remarks>
        private TextBlock CreateAddress(string address)
        {
            var text = new TextBlock
            {
                FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithMonospace"] as FontFamily,
                FontSize = 13,
                LineHeight = 18
            };

            var dimmed = BootStrapper.Current.Resources["SystemControlDisabledBaseMediumLowBrush"] as Brush;

            for (int i = 0; i < 12; i++)
            {
                var offset = i * 4;
                if (offset >= address.Length)
                {
                    break;
                }

                if (i == 6)
                {
                    text.Inlines.Add(new LineBreak());
                }
                else if (i > 0)
                {
                    text.Inlines.Add(new Run { Text = " " });
                }

                var run = new Run
                {
                    Text = address.Substring(offset, Math.Min(4, address.Length - offset))
                };

                if ((i & 1) == 1)
                {
                    run.Foreground = dimmed;
                }

                text.Inlines.Add(run);
            }

            return text;
        }
    }
}
