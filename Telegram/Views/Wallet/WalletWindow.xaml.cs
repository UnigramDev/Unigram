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
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Controls.Wallet;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Wallet;
using Telegram.Views.Grams;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Telegram.Views.Stars.Popups;
using Telegram.Views.Wallet.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
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
    public sealed partial class WalletWindow : WindowContent, IWalletDelegate
    {
        private readonly IClientService _clientService;
        private readonly IWalletService _wallet;
        private readonly SecondaryNavigationService _navigationService;

        private readonly ScrollViewerIncrementalLoader _loader;

        // The NFT whose colours the card wears, which has to be one of the stickers on it.
        private string _backgroundAddress = string.Empty;

        // The gift being dragged from the list, until the drag is over, and the address of the one
        // out of its cell meanwhile, until it lands or is back.
        private TonNft _dragged;
        private string _carried;

        private DragEventHandler _dragOverHandler;

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
            viewModel.Delegate = this;

            DataContext = viewModel;

            // Assigned rather than bound: x:Bind would have run inside InitializeComponent,
            // before there was a view model to read.
            TransactionsList.ItemsSource = viewModel.Transactions;
            CollectiblesList.ItemsSource = viewModel.Collectibles;

            // The lists have no scroll viewer of their own to ask for more as they near the end.
            _loader = new ScrollViewerIncrementalLoader(ScrollingHost);
            _loader.ItemsSource = viewModel.Transactions;

            StateLabel.Text = Strings.WalletTitle;

            Card.Stickers.HomeResolver = GetHome;
            Card.Stickers.Changed += OnStickersChanged;
            Card.Stickers.StickerLeft += OnStickerLeft;
            Card.Stickers.StickerContextRequested += OnStickerContextRequested;
            Card.Stickers.ActiveChanged += OnStickersActiveChanged;

            DragLayer.IsReadOnly = true;
            DragLayer.HomeResolver = GetCarrierHome;
            DragLayer.StickerLeft += OnCarriedLeft;

            // Handled ones too: over its own items the list takes DragOver for itself, and the
            // carried gift would stop following the pointer there.
            _dragOverHandler = OnDragOver;
            DropRoot.AddHandler(DragOverEvent, _dragOverHandler, true);

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

            _ = LoadStickersAsync();
        }

        protected override void OnUnloaded()
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;

            _unpinTimer?.Stop();
        }

        protected override void OnWindowClosed()
        {
            // Unsubscribes the view model from the aggregator. A subscription that outlives the
            // window keeps this whole view alive, and this is the signal that the view is going:
            // the window is closed, which is what consolidation follows.
            ViewModel?.NavigatedFrom(null, false);

            if (ViewModel != null)
            {
                ViewModel.Delegate = null;
            }

            Card.Stickers.HomeResolver = null;
            Card.Stickers.Changed -= OnStickersChanged;
            Card.Stickers.StickerLeft -= OnStickerLeft;
            Card.Stickers.StickerContextRequested -= OnStickerContextRequested;
            Card.Stickers.ActiveChanged -= OnStickersActiveChanged;

            DragLayer.HomeResolver = null;
            DragLayer.StickerLeft -= OnCarriedLeft;

            DropRoot.RemoveHandler(DragOverEvent, _dragOverHandler);
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
            else if (e.PropertyName == nameof(ViewModel.HasCollectibles) && !ViewModel.HasCollectibles)
            {
                // The tabs unload with the last NFT, and the history is all there is to show.
                ShowCollectibles(false);
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

        // It stands in for the history, so not over the NFTs.
        private void UpdateEmpty(bool empty)
        {
            EmptyPanel.Visibility = empty && CollectiblesList.Visibility == Visibility.Collapsed
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
                if (args.ItemContainer.ContentTemplateRoot is WalletTransactionCell recycled)
                {
                    WalletTransferFlight.Bind(recycled, null, null);
                    WalletPendingRow.Update(args.ItemContainer, recycled, null, null, false);
                }

                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is WalletTransactionCell transactionCell && args.Item is TonWalletTransaction transaction)
            {
                UpdateTransaction(args.ItemContainer, transactionCell, transaction);
                args.Handled = true;
            }
        }

        // A bind, and a new version of a row already shown, which the view model hands over rather
        // than replacing in the list - a replaced item gets a new container, and a pending row is
        // in the middle of its own animation when its transfer settles.
        private void UpdateTransaction(SelectorItem container, WalletTransactionCell cell, TonWalletTransaction transaction)
        {
            cell.UpdateInfo(_clientService, transaction);

            // The key the list holds the row under, which a transaction that replaced a pending
            // row shares with it.
            var key = _wallet.PredecessorOf(transaction.Id) ?? transaction.Id;

            // The flight first: whether a stone is on its way decides whether the row may settle.
            var incoming = WalletTransferFlight.Bind(cell, transaction, key);
            WalletPendingRow.Update(container, cell, transaction, key, incoming);
        }

        public void UpdateTransaction(TonWalletTransaction transaction)
        {
            if (TransactionsList.ContainerFromItem(transaction) is SelectorItem container && container.ContentTemplateRoot is WalletTransactionCell cell)
            {
                UpdateTransaction(container, cell, transaction);
            }
        }

        public void UpdateCollectible(TonNft collectible)
        {
            if (CollectiblesList.ContainerFromItem(collectible) is SelectorItem container && container.ContentTemplateRoot is WalletCollectibleCell cell)
            {
                cell.UpdateInfo(_clientService, collectible, IsOutOfCell(collectible.Address), Card.Stickers.Contains(collectible.Address));
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
            TextBlockHelper.SetMarkdown(block, string.Format(format, $"**{Icons.Ton} {Formatter.GramSummary(nanograms).Join()}**"));
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
            flyout.CreateFlyoutItem(MenuItemConnectedApps, Strings.WalletConnectedApps, Icons.Apps);

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
            var popup = new WalletCurrencyPopup(_clientService);

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

        private void MenuItemConnectedApps()
        {
            _navigationService.ShowPopup(new WalletConnectedAppsPopup(_clientService, _wallet, _navigationService));
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
                _navigationService.ShowToast(Strings.ErrorOccurred, ToastPopupIcon.Error);
            }
        }

        private void AddFunds_Click(object sender, RoutedEventArgs e)
        {
            _navigationService.ShowPopup(new WalletSharePopup(_clientService, _wallet, _navigationService));
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            // Somewhere to send to, typed. The other way in is the Send pill on a transaction,
            // which already knows who it is sending to; this is the one for everybody else, until
            // there is a picker.
            var popup = new InputPopup
            {
                Title = Strings.WalletSendGrams,
                PlaceholderText = Strings.WalletAddressOrName,
                PrimaryButtonText = Strings.Next,
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

                try
                {
                    recipient = await _wallet.ResolveDnsAsync(recipient);
                }
                catch (Exception ex)
                {
                    // An unknown name is one of the ways this fails, so it is said the same way.
                    Logger.Error("wallet name could not be resolved: " + ex.Message);
                    recipient = null;
                }

                if (string.IsNullOrEmpty(recipient))
                {
                    _navigationService.ShowToast(string.Format(Strings.WalletSearchNoResults, domain), ToastPopupIcon.Error);
                    return;
                }
            }
            else if (!WalletHelper.IsValidAddress(recipient))
            {
                _navigationService.ShowToast(Strings.WalletInvalidAddress, ToastPopupIcon.Error);
                return;
            }

            // The name is kept, not just what it resolved to: it is what the user typed and what
            // the history should say the transfer went to.
            await SendAsync(0, recipient, domain);
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }

            ShowCollectibles(Navigation.SelectedIndex == 1);
        }

        private ListViewBase ActiveList => CollectiblesList.Visibility == Visibility.Visible
            ? CollectiblesList
            : TransactionsList;

        private void ShowCollectibles(bool show)
        {
            TransactionsList.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            CollectiblesList.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            _loader.ItemsSource = show ? ViewModel.Collectibles : ViewModel.Transactions;

            UpdateEmpty(ViewModel.IsEmpty);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ScrollingHost.SetVerticalPadding(48, 0);
            AttachPin();
        }

        private void HeaderPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            TransactionsHeader.Height = e.NewSize.Height;
            CollectiblesHeader.Height = e.NewSize.Height;

            UpdateMinHeight();
            UpdatePinTop();
        }

        private void HeaderTabs_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Height != 0 || ActiveList.ItemsPanelRoot is not Panel panel)
            {
                return;
            }

            var margin = 16 + 6;
            var visual = ElementComposition.GetElementVisual(panel);
            ElementCompositionPreview.SetIsTranslationEnabled(panel, true);

            panel.Margin = new Thickness(0, -HeaderTabs.ActualSize.Y - margin, 0, 0);
            Canvas.SetZIndex(panel, 2);

            var translation = visual.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, -HeaderTabs.ActualSize.Y - margin);
            translation.InsertKeyFrame(0, 0);
            translation.InsertKeyFrame(1, HeaderTabs.ActualSize.Y + margin);

            var batch = visual.Compositor.CreateScopedBatch(Windows.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Properties.InsertVector3("Translation", Vector3.Zero);

                panel.Margin = new Thickness(0, 0, 0, 0);
                Canvas.SetZIndex(panel, 0);
            };

            visual.StartAnimation("Translation.Y", translation);
            batch.End();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMinHeight();
        }

        // Room under the header for the tabs to reach the top, however little is in the list.
        private void UpdateMinHeight()
        {
            var minHeight = HeaderPanel.ActualHeight + Math.Max(0, ScrollingHost.ActualHeight - 80 - 24 - 8); // Header and footer + outer

            TransactionsList.MinHeight = minHeight;
            CollectiblesList.MinHeight = minHeight;
        }

        #region Card stickers

        private async Task LoadStickersAsync()
        {
            var card = await _wallet.LoadCardAsync();
            if (card == null)
            {
                return;
            }

            Card.Stickers.Show(_clientService, card);

            var background = Card.Stickers.Find(card.BackgroundNftAddress);
            _backgroundAddress = background?.Address ?? string.Empty;

            Card.SetStyle(_clientService, background?.Collectible.Gift, default, false);
            CollectiblesList.ForEach(UpdateOnCard);
        }

        private void Save()
        {
            _wallet.SaveCard(Card.Stickers.ToInput(), _backgroundAddress);
        }

        private void OnStickersChanged(object sender, EventArgs e)
        {
            // The colours go with the sticker that gave them, however it left.
            if (_backgroundAddress.Length > 0 && !Card.Stickers.Contains(_backgroundAddress))
            {
                _backgroundAddress = string.Empty;
                Card.SetStyle(_clientService, null, new Vector2(WalletStickerLayer.FaceWidth / 2, WalletStickerLayer.FaceHeight / 2), true);
            }

            Save();
        }

        private void OnStickerLeft(object sender, TonNft collectible)
        {
            UpdateCell(collectible.Address);
        }

        // By address: a sticker loaded with the card is not the instance the list holds.
        private WalletCollectibleCell CellOf(string address)
        {
            var collectibles = ViewModel.Collectibles;

            for (int i = 0; i < collectibles.Count; i++)
            {
                if (collectibles[i].Address == address)
                {
                    return CollectiblesList.ContainerFromIndex(i) is SelectorItem container
                        ? container.ContentTemplateRoot as WalletCollectibleCell
                        : null;
                }
            }

            return null;
        }

        private void UpdateCell(string address)
        {
            if (CellOf(address) is WalletCollectibleCell cell)
            {
                UpdateOnCard(cell);
            }
        }

        private void UpdateOnCard(SelectorItem container)
        {
            if (container.ContentTemplateRoot is WalletCollectibleCell cell)
            {
                UpdateOnCard(cell);
            }
        }

        private void UpdateOnCard(WalletCollectibleCell cell)
        {
            if (cell.Collectible is TonNft collectible)
            {
                cell.UpdateOnCard(IsOutOfCell(collectible.Address), Card.Stickers.Contains(collectible.Address));
            }
        }

        // On the card, or on its way there.
        private bool IsOutOfCell(string address)
        {
            return address == _carried || Card.Stickers.Contains(address);
        }

        // The cell of a gift, while it is on screen.
        private WalletCollectibleCell VisibleCellOf(string address)
        {
            if (CollectiblesList.Visibility != Visibility.Visible || CellOf(address) is not WalletCollectibleCell cell)
            {
                return null;
            }

            var size = cell.ActualSize;
            var bounds = cell.TransformToVisual(ScrollingHost).TransformBounds(new Rect(0, 0, size.X, size.Y));

            return bounds.Bottom < 0 || bounds.Top > ScrollingHost.ActualHeight ? null : cell;
        }

        /// <summary>
        /// Where a gift's cell is on the card, and how big its art is against a sticker: where a
        /// sticker taken off goes back to, and where one put on from the menu comes from.
        /// </summary>
        private (Vector2 Center, float Scale)? GetHome(TonNft collectible)
        {
            if (VisibleCellOf(collectible.Address) is not WalletCollectibleCell cell)
            {
                return null;
            }

            var size = cell.ActualSize;
            return (ToFace(cell, new Point(size.X / 2, size.Y / 2)), (float)cell.ArtSize * FaceScale / WalletStickerView.BaseSize);
        }

        // The same, for the gift carried over the window.
        private (Vector2 Center, float Scale)? GetCarrierHome(TonNft collectible)
        {
            if (VisibleCellOf(collectible.Address) is not WalletCollectibleCell cell)
            {
                return null;
            }

            var size = cell.ActualSize;
            var center = cell.TransformToVisual(DragLayer).TransformPoint(new Point(size.X / 2, size.Y / 2));

            return (new Vector2((float)center.X, (float)center.Y), (float)cell.ArtSize * FaceScale / WalletStickerView.BaseSize);
        }

        // Face units to a pixel of the card as it is shown.
        private float FaceScale => Card.ActualSize.X > 0
            ? WalletStickerLayer.FaceWidth / Card.ActualSize.X
            : 1;

        private Vector2 ToFace(DragEventArgs e)
        {
            return ToFace(DropRoot, e.GetPosition(DropRoot));
        }

        // Whether a transform through the card includes the pin's translation is not relied on:
        // the point goes to the card's parent, which is not translated, and from there into the
        // card by its layout offset and the pin offset. Between the card and its stickers the
        // translation is common to both either way.
        private Vector2 ToFace(UIElement from, Point point)
        {
            var parent = VisualTreeHelper.GetParent(Card) as UIElement;
            var offset = Card.ActualOffset;

            var local = from.TransformToVisual(parent).TransformPoint(point);
            local = new Point(local.X - offset.X, local.Y - offset.Y - PinOffset);

            var face = Card.TransformToVisual(Card.Stickers).TransformPoint(local);
            return new Vector2((float)face.X, (float)face.Y);
        }

        private void Collectibles_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new WalletNftGridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;

                args.ItemContainer.ContextRequested += Collectible_ContextRequested;
                args.ItemContainer.DragStarting += Collectible_DragStarting;
                args.ItemContainer.DropCompleted += Collectible_DropCompleted;
            }

            args.IsContainerPrepared = true;
        }

        private void Collectibles_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is WalletCollectibleCell cell && args.Item is TonNft collectible)
            {
                cell.UpdateInfo(_clientService, collectible, IsOutOfCell(collectible.Address), Card.Stickers.Contains(collectible.Address));
                args.ItemContainer.CanDrag = collectible.Gift != null;
            }

            args.Handled = true;
        }

        private void Collectibles_ItemClick(object sender, ItemClickEventArgs e)
        {
            // By name: the NFT's own copy of the gift leaves out the owner.
            if (e.ClickedItem is TonNft { Gift: UpgradedGift gift })
            {
                MessageHelper.NavigateToUpgradedGift(_clientService, _navigationService, gift.Name);
            }
        }

        private void Collectible_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (CollectiblesList.ItemFromContainer(sender) is not TonNft { Gift: not null } collectible)
            {
                return;
            }

            var flyout = new MenuFlyout();

            CreateColorItem(flyout, collectible);

            if (Card.Stickers.Contains(collectible.Address))
            {
                flyout.CreateFlyoutItem(RemoveFromCard, collectible, Strings.WalletGiftRemoveFromCard, Icons.Sticker);
            }
            else
            {
                flyout.CreateFlyoutItem(AddToCard, collectible, Strings.WalletGiftAddToCard, Icons.Sticker);
            }

            flyout.ShowAt(sender, args);
        }

        private void OnStickerContextRequested(object sender, WalletStickerContextRequestedEventArgs e)
        {
            var collectible = e.Sticker.Collectible;
            var flyout = new MenuFlyout();

            CreateColorItem(flyout, collectible);
            flyout.CreateFlyoutItem(RemoveStickerFromCard, collectible, Strings.WalletGiftRemoveFromCard, Icons.Sticker);

            flyout.ShowAt(e.Sticker, e.Args);
        }

        private void CreateColorItem(MenuFlyout flyout, TonNft collectible)
        {
            if (collectible.Address == _backgroundAddress)
            {
                flyout.CreateFlyoutItem(RemoveColor, Strings.WalletGiftRemoveColor, Icons.Color);
            }
            else if (collectible.Gift.CanBeTonWalletCardBackground)
            {
                flyout.CreateFlyoutItem(ApplyColor, collectible, Strings.WalletGiftApplyColor, Icons.Color);
            }
        }

        private void AddToCard(TonNft collectible)
        {
            TryAddToCard(collectible);
        }

        private bool TryAddToCard(TonNft collectible)
        {
            if (Card.Stickers.Contains(collectible.Address))
            {
                return true;
            }
            else if (Card.Stickers.Count >= WalletStickerLayer.MaximumStickers)
            {
                ShowCardFull();
                return false;
            }

            // Brought into view first, so the toss has somewhere to land that can be seen.
            Pin(true, ActionHold);

            Card.Stickers.Add(_clientService, collectible, GetHome(collectible));
            UpdateCell(collectible.Address);
            return true;
        }

        // From the cell the card is left where it is, out of view or not: the sticker flies home
        // to the cell either way.
        private void RemoveFromCard(TonNft collectible)
        {
            if (collectible.Address == _backgroundAddress)
            {
                RemoveColor();
            }

            Card.Stickers.Remove(collectible.Address);
        }

        private void RemoveStickerFromCard(TonNft collectible)
        {
            Pin(true, ActionHold / 2);
            RemoveFromCard(collectible);
        }

        private void ApplyColor(TonNft collectible)
        {
            if (!TryAddToCard(collectible) || Card.Stickers.Find(collectible.Address) is not WalletStickerView sticker)
            {
                return;
            }

            Pin(true, ActionHold);

            _backgroundAddress = collectible.Address;
            Card.SetStyle(_clientService, collectible.Gift, new Vector2(sticker.X.Target, sticker.Y.Target), true);

            Save();
        }

        private void RemoveColor()
        {
            var sticker = Card.Stickers.Find(_backgroundAddress);
            var origin = sticker != null
                ? sticker.Center
                : new Vector2(WalletStickerLayer.FaceWidth / 2, WalletStickerLayer.FaceHeight / 2);

            _backgroundAddress = string.Empty;
            Card.SetStyle(_clientService, null, origin, true);

            Save();
        }

        private void ShowCardFull()
        {
            _navigationService.ShowToast(string.Format(Strings.WalletCardFull, WalletStickerLayer.MaximumStickers), ToastPopupIcon.Info);
        }

        #endregion

        #region Drag and drop

        // From the moment the gift leaves its cell it is carried by DragLayer, and the system's
        // drag visual is empty, then hidden while it is over the window. Dropped on the card, the
        // card's own sticker, made when the drag started, takes over where the carried one is.
        //
        // The drag starts from the container, not from the list's CanDragItems: the list draws
        // its own visual of the item before anything can replace it.

        private void Collectible_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            if (CollectiblesList.ItemFromContainer(sender) is not TonNft { Gift: not null } collectible || Card.Stickers.Contains(collectible.Address))
            {
                args.Cancel = true;
                return;
            }
            else if (Card.Stickers.Count >= WalletStickerLayer.MaximumStickers)
            {
                args.Cancel = true;
                ShowCardFull();
                return;
            }

            _dragged = collectible;
            _carried = collectible.Address;

            args.AllowedOperations = DataPackageOperation.Copy;
            args.Data.RequestedOperation = DataPackageOperation.Copy;
            args.DragUI.SetContentFromSoftwareBitmap(_emptyDragContent ??= new SoftwareBitmap(BitmapPixelFormat.Bgra8, 1, 1, BitmapAlphaMode.Premultiplied));

            Card.Stickers.Deselect();
            Card.Stickers.Prepare(_clientService, collectible);
            Pin(true);

            // The size it will have on the card.
            DragLayerScale.ScaleX = DragLayerScale.ScaleY = 1 / FaceScale;

            var home = GetCarrierHome(collectible);
            DragLayer.PickUp(_clientService, collectible, home, home?.Center ?? Vector2.Zero);

            UpdateCell(collectible.Address);
        }

        private SoftwareBitmap _emptyDragContent;

        private void Collectible_DropCompleted(UIElement sender, DropCompletedEventArgs args)
        {
            _dragged = null;

            // Dropped, it is landing; carried away, it goes back into its cell.
            if (args.DropResult == DataPackageOperation.None)
            {
                Card.Stickers.HidePreview(false);
                DragLayer.ReturnPreview();
                Pin(false);
            }
        }

        private void OnCarriedLeft(object sender, TonNft collectible)
        {
            if (_carried == collectible.Address)
            {
                _carried = null;
            }

            UpdateCell(collectible.Address);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (_dragged == null)
            {
                return;
            }

            e.DragUIOverride.IsContentVisible = false;
            e.DragUIOverride.IsCaptionVisible = false;
            e.DragUIOverride.IsGlyphVisible = false;

            var position = e.GetPosition(DragLayer);
            DragLayer.MovePreview(new Vector2((float)position.X, (float)position.Y));

            e.AcceptedOperation = WalletStickerLayer.IsOnCard(ToFace(e))
                ? DataPackageOperation.Copy
                : DataPackageOperation.None;

            e.Handled = true;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            DragLayer.FadePreview();
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            var collectible = _dragged;
            if (collectible == null)
            {
                return;
            }

            if (Card.Stickers.Drop(_clientService, collectible, ToFace(e), DragLayer.PreviewMotion))
            {
                DragLayer.HidePreview(false);

                _carried = null;
                UpdateCell(collectible.Address);

                Pin(true, DropHold);
            }

            e.Handled = true;
        }

        #endregion

        #region Card pin

        // While a gift is on its way to the card, the card comes down over the list to meet it, as
        // far as just under the title bar, and goes back once the gift has landed. Translated
        // rather than scrolled to, so the list stays where the user left it.

        // How long the card stays down after an action from the menu, for the toss and the colour
        // wave, and after a drop, for the roll-on.
        private const double ActionHold = 2.6;
        private const double DropHold = 1.2;

        // Under the title bar, measured from the top of the scroll viewer, which starts 8 above it.
        private const float PinInset = 40 + 8 + 8;

        private CompositionPropertySet _pin;
        private float _pinTop;
        private bool _pinned;
        private bool _pinRaised;
        private DispatcherTimer _unpinTimer;

        // How far the translation has moved the card, which layout does not know about: hit
        // testing and TransformToVisual still see it where it was.
        private float PinOffset => _pinned
            ? Math.Max(0, (float)ScrollingHost.VerticalOffset - _pinTop)
            : 0;

        private void AttachPin()
        {
            if (_pin != null)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(Card);
            var scroll = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);

            ElementCompositionPreview.SetIsTranslationEnabled(Card, true);

            _pin = visual.Compositor.CreatePropertySet();
            _pin.InsertScalar("Pinned", 0);
            _pin.InsertScalar("Top", _pinTop);

            var translation = visual.Compositor.CreateExpressionAnimation("max(0, -scroll.Translation.Y - pin.Top) * pin.Pinned");
            translation.SetReferenceParameter("scroll", scroll);
            translation.SetReferenceParameter("pin", _pin);

            visual.StartAnimation("Translation.Y", translation);
        }

        private void UpdatePinTop()
        {
            if (ScrollingHost.Content is not UIElement content || VisualTreeHelper.GetParent(Card) is not UIElement parent)
            {
                return;
            }

            // Through the parent, as in ToFace, so a header resize while pinned doesn't count the
            // translation in.
            var offset = Card.ActualOffset;
            _pinTop = (float)parent.TransformToVisual(content).TransformPoint(new Point(offset.X, offset.Y)).Y - PinInset;
            _pin?.InsertScalar("Top", _pinTop);
        }

        private void Pin(bool pinned, double releaseAfter = 0)
        {
            _unpinTimer?.Stop();

            if (pinned && releaseAfter > 0)
            {
                if (_unpinTimer == null)
                {
                    _unpinTimer = new DispatcherTimer();
                    _unpinTimer.Tick += OnUnpinTick;
                }

                _unpinTimer.Interval = TimeSpan.FromSeconds(releaseAfter);
                _unpinTimer.Start();
            }

            if (_pin == null || _pinned == pinned)
            {
                return;
            }

            _pinned = pinned;

            if (pinned)
            {
                _pinRaised = true;
                UpdateCardLayer();
            }

            var animation = _pin.Compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(1, pinned ? 1 : 0);
            animation.Duration = TimeSpan.FromMilliseconds(pinned ? 250 : 350);

            var batch = _pin.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += OnPinCompleted;

            _pin.StartAnimation("Pinned", animation);
            batch.End();
        }

        private void OnPinCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= OnPinCompleted;
            }

            // Back in the header, under the sticky parts again.
            if (!_pinned)
            {
                _pinRaised = false;
                UpdateCardLayer();
            }
        }

        private void OnStickersActiveChanged(object sender, EventArgs e)
        {
            UpdateCardLayer();
        }

        // Over the sticky header, the buttons and the tabs while it is down, and while a sticker
        // may be beyond its edge: one in hand or flying home is drawn as part of the card.
        private void UpdateCardLayer()
        {
            Canvas.SetZIndex(Card, _pinRaised || Card.Stickers.IsActive ? 2 : 0);
        }

        private void OnUnpinTick(object sender, object e)
        {
            _unpinTimer.Stop();
            Pin(false);
        }

        #endregion
    }
}
