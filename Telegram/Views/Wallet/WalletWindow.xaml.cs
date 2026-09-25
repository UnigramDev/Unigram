//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Telegram.ViewModels.Wallet;
using Telegram.Views.Grams;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Telegram.Views.Wallet.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Wallet
{
    /// <summary>
    /// The wallet, in a window of its own.
    /// </summary>
    /// <remarks>
    /// One per account, enforced where it is opened (<c>TLNavigationService.NavigateToWallet</c>):
    /// a second one would be a second view of the same state, and the card is expensive enough -
    /// a per-frame tilt and a repainted conic gradient - that two of them is worth avoiding.
    ///
    /// The view model is resolved and driven here rather than by the navigation machinery, which
    /// only runs for pages. It is given this window's <see cref="SecondaryNavigationService"/>, so
    /// a popup opens over the wallet and a page navigation lands in the main window, which is what
    /// the mini apps do.
    /// </remarks>
    public sealed partial class WalletWindow : WindowContent
    {
        private readonly IClientService _clientService;
        private readonly IWalletService _wallet;
        private readonly SecondaryNavigationService _navigationService;


        public WalletViewModel ViewModel => DataContext as WalletViewModel;

        /// <summary>
        /// The account this window belongs to. Read from another window's thread to find an already
        /// open wallet, so it is fixed at construction and never touches XAML.
        /// </summary>
        public int SessionId { get; }

        public WalletWindow(WindowContext context, IClientService clientService, INavigationService navigationService)
            : base(context)
        {
            InitializeComponent();

            _clientService = clientService;
            _wallet = clientService.Session.Resolve<IWalletService>();
            _navigationService = new SecondaryNavigationService(clientService.Session, navigationService, context);

            SessionId = clientService.SessionId;

            var viewModel = clientService.Session.Resolve<WalletViewModel>();
            viewModel.NavigationService = _navigationService;
            viewModel.Dispatcher = context.Dispatcher;

            DataContext = viewModel;

            // Assigned rather than bound: x:Bind would have run inside InitializeComponent,
            // before there was a view model to read.
            ScrollingHost.ItemsSource = viewModel.Items;


            StateLabel.Text = Strings.WalletTitle;

            // The handle, not the whole strip: a drag region takes the pointer away from anything
            // under it, and the window's own close button is drawn over the right of this bar.
            Window.CaptionButtons = CaptionButtons.Close;

            // What navigation would do for a page, and once rather than on every load: it subscribes
            // the view model to the aggregator, and OnWindowClosed is the other half of that.
            _ = viewModel.NavigatedToAsync(null, NavigationMode.New, null);

            CheckWalletBotAsync();

            UpdateEarnings();

            //        background: linear - gradient(0deg, #0079FF, #0079FF),
            //conic - gradient(from 20.99deg at 50 % 50 %, #0079FF 0deg, #169AF9 90deg, #0079FF 180deg, #169AF9 270deg, #0079FF 360deg);
        }

        protected override UIElement TitleBarElement => TitleBarHandle;

        protected override void OnLoaded()
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateCard();
            UpdateEmpty(ViewModel.IsEmpty);
        }

        protected override void OnUnloaded()
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        protected override void OnWindowClosed()
        {
            // Unsubscribes the view model from the aggregator. A subscription that outlives the
            // window keeps this whole view alive, and this is the signal that the view is going:
            // the window is closed, which is what consolidation follows.
            ViewModel?.NavigatedFrom(null, false);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Address)
                || e.PropertyName == nameof(ViewModel.Balance)
                || e.PropertyName == nameof(ViewModel.Currency)
                || e.PropertyName == nameof(ViewModel.CurrencyRate))
            {
                UpdateCard();
            }
            else if (e.PropertyName == nameof(ViewModel.IsEmpty))
            {
                UpdateEmpty(ViewModel.IsEmpty);
            }
            else if (e.PropertyName == nameof(ViewModel.EarnedGramCount))
            {
                UpdateEarnings();
            }
        }

        /// <summary>
        /// Hands the card what it draws. One call for all of it: the balance and what it converts
        /// to are one answer, and the card is the thing that knows not to show half of it.
        /// </summary>
        private void UpdateCard()
        {
            Card.SetState(_clientService, _wallet.State);
        }

        private void UpdateEmpty(bool empty)
        {
            EmptyPanel.Visibility = empty
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not TonWalletTransaction transaction)
            {
                return;
            }

            var popup = new WalletTransactionPopup(_clientService, _wallet, _navigationService, transaction);

            // The popup opens the send flow itself, having the transfer and its peer in hand.
            await _navigationService.ShowPopupAsync(popup);
        }

        /// <summary>
        /// Sends grams to one recipient, and shows what was sent at the top of the history.
        /// </summary>
        private async Task SendAsync(long userId, string address, string domain = null)
        {
            var popup = new WalletSendPopup(_clientService, _wallet, _navigationService, userId, address, domain);

            await _navigationService.ShowPopupAsync(popup);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is WalletTransactionCell cell && args.Item is TonWalletTransaction transaction)
            {
                cell.UpdateInfo(_clientService, transaction);
                args.Handled = true;
            }
        }

        /// <summary>
        /// The banner pointing at the balance the user has in the @walt bot, which is a different
        /// wallet from this one and is not part of anything else here.
        /// </summary>
        /// <remarks>
        /// Asked once per window rather than watched: nothing updates to say the bot balance moved,
        /// and a banner that appears mid-session would be stranger than one that waits for the next
        /// time the wallet is opened. Hidden until the answer arrives, so the row does not offer
        /// somewhere to go and then take it away.
        /// </remarks>
        private async void CheckWalletBotAsync()
        {
            var response = await _clientService.SendAsync(new CheckWalletBotBalance());
            if (response is WalletBotBalance balance && balance.HasBalance)
            {
                _walletBotUrl = balance.Url;

                WalletBotButton.Visibility = Visibility.Visible;
            }
        }

        private string _walletBotUrl;

        private async void WalletBot_Click(object sender, RoutedEventArgs e)
        {
            // Empty is a documented answer - the bot is there but there is no link to reach it by -
            // so the row stays and does nothing rather than opening the app's fallback.
            if (string.IsNullOrEmpty(_walletBotUrl))
            {
                return;
            }

            // Resolved before it is opened, the same way the on-ramp session URL is: it is a bot
            // link, and handing the string straight to OpenUrl would send it out to the browser
            // instead of opening the bot in the app.
            var response = await _clientService.SendAsync(new GetInternalLinkType(_walletBotUrl));
            if (response is InternalLinkType internalLink)
            {
                MessageHelper.OpenTelegramUrl(_clientService, _navigationService, internalLink, null);
            }
        }

        /// <summary>
        /// The line pointing at the Grams the account has earned, which are not in this wallet and
        /// are spent from their own page.
        /// </summary>
        /// <remarks>
        /// Shown on exactly the terms the My Grams entry in settings is, so the two never disagree:
        /// a balance, or a history of one. Reading the balance is what asks for it, and the answer
        /// arrives as an update that comes back through here.
        /// </remarks>
        private void UpdateEarnings()
        {
            if (!ViewModel.HasEarnedGrams)
            {
                EarningsButton.Visibility = Visibility.Collapsed;
                return;
            }

            EarningsButton.Content = CreateAmountContent(Strings.WalletGramEarningsBalance, ViewModel.EarnedGramCount);
            EarningsButton.Visibility = Visibility.Visible;
        }

        private void Earnings_Click(object sender, RoutedEventArgs e)
        {
            // The wallet's navigation service forwards to the window it was opened from, so this
            // puts the page in the main window and brings that forward - which is where a page
            // belongs, the wallet window having no frame of its own.
            _navigationService.Navigate(typeof(GramsPage));
        }

        /// <summary>
        /// One of the banner sentences, with the amount written into it where its own
        /// <c>{0}</c> is.
        /// </summary>
        /// <remarks>
        /// The whole sentence is one string rather than a fragment with the amount stuck on the
        /// front: the words around a number are not in the same order in every language, and only
        /// the translator of that language knows where they belong.
        ///
        /// The font is the theme's own chain ending in the icon font, because the amount carries
        /// the gram glyph: that way it is part of the text rather than a run of its own, and the
        /// user's chosen font and emoji set still apply to the words around it.
        /// </remarks>
        private static TextBlock CreateAmountContent(string format, BigInteger nanograms)
        {
            var block = new TextBlock
            {
                FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSymbols"] as FontFamily
            };

            TextBlockHelper.SetIsLink(block, true);
            TextBlockHelper.SetMarkdown(block, string.Format(format, $"**{Icons.Ton} {Formatter.TonBalance(nanograms).Join()}**"));
            return block;
        }

        /// <summary>
        /// The line offering the wallets the account has moved on from, when any of them still
        /// holds something.
        /// </summary>
        /// <remarks>
        /// Zero is the ordinary answer and hides the row - either there is no archive, or the chain
        /// has not been asked yet and the balances are still zero. It appears when the answer comes
        /// back rather than sitting there saying nothing.
        /// </remarks>
        private void UpdateArchive()
        {
            var archived = ViewModel.ArchivedBalance;
            if (archived <= BigInteger.Zero)
            {
                ArchiveButton.Visibility = Visibility.Collapsed;
                return;
            }

            ArchiveButton.Content = CreateAmountContent(Strings.WalletOldWalletsBalance, archived);
            ArchiveButton.Visibility = Visibility.Visible;
        }

        private void Archive_Click(object sender, RoutedEventArgs e)
        {
            _navigationService.ShowPopup(new WalletBackupPopup(_wallet, _navigationService));
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var currency = flyout.CreateFlyoutItem(MenuItemCurrency, Strings.WalletCurrency, Icons.Globe);
            currency.KeyboardAcceleratorTextOverride = ViewModel.Currency;

            flyout.CreateFlyoutItem(MenuItemProtection, Strings.Passcode, Icons.LockClosed);
            flyout.CreateFlyoutItem(MenuItemRecoveryPhrase, Strings.WalletKeysAndBackup, Icons.Cloud);

            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(MenuItemAbout, Strings.WalletWhatIsWallet, Icons.QuestionCircle);

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void MenuItemRefresh()
        {
            _ = _wallet.RefreshAsync();
        }

        private async void MenuItemCurrency()
        {
            var popup = new WalletCurrencyPopup(_wallet);

            var confirm = await _navigationService.ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary && popup.SelectedItem != null)
            {
                await _wallet.SetCurrencyAsync(popup.SelectedItem);
            }
        }

        private void MenuItemAbout()
        {
            _navigationService.ShowPopup(new WalletAboutPopup());
        }

        private void MenuItemRecoveryPhrase()
        {
            _navigationService.ShowPopup(new WalletBackupPopup(_wallet, _navigationService));
        }

        private async void MenuItemProtection()
        {
            // The vault is built the first time the wallet is restored, and opening this window
            // does that - but the menu is reachable before the restore has come back.
            await _wallet.RestoreAsync();

            var vault = _wallet.Vault;
            if (vault == null)
            {
                return;
            }

            try
            {
                await vault.ReenrollAsync(_navigationService);
            }
            catch (WalletVaultException ex) when (ex.Failure == WalletVaultFailure.Cancelled)
            {
                // They were asked for the current credential and said no, which is the whole
                // point of asking.
            }
            catch (Exception ex)
            {
                Logger.Error("wallet protection could not be changed: " + ex.Message);
                _navigationService.ShowToast("[This could not be changed right now.]", ToastPopupIcon.Error);
            }
        }

        private void AddFunds_Click(object sender, RoutedEventArgs e)
        {
            _navigationService.ShowPopup(new WalletSharePopup(_clientService, _navigationService, _wallet.State.Address));
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            // Somewhere to send to, typed. The other way in is the Send pill on a transaction,
            // which already knows who it is sending to; this is the one for everybody else, until
            // there is a picker.
            var popup = new InputPopup
            {
                Title = "[Send Grams]",
                Header = "[Enter a wallet address or a .ton name.]",
                PlaceholderText = "[Address]",
                PrimaryButtonText = "[Next]",
                SecondaryButtonText = Strings.Cancel,
                MinLength = 1
            };

            var confirm = await _navigationService.ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var recipient = popup.Text.Trim();
            var domain = string.Empty;

            if (recipient.EndsWith(".ton", StringComparison.OrdinalIgnoreCase))
            {
                domain = recipient;
                recipient = await _wallet.ResolveDnsAsync(recipient);

                if (string.IsNullOrEmpty(recipient))
                {
                    _navigationService.ShowToast("[That name does not point at a wallet.]", ToastPopupIcon.Error);
                    return;
                }
            }

            // The name is kept, not just what it resolved to: it is what the user typed and what
            // the history should say the transfer went to.
            await SendAsync(0, recipient, domain);
        }
    }
}
