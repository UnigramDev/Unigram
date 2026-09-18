//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.ComponentModel;
using System.Numerics;
using System.Text;
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
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Telegram.Views.Wallet.Popups;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
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

        private readonly WalletCardSheen _sheen = new(/*Theme.AccentLight.Dark2*/);

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

            //Card.Constraint = new Size(85.60, 53.98);
            Card.Constraint = new Size(360, 220);
            Card.SizeChanged += Card_SizeChanged;

            CardBalanceGram.Foreground = new SolidColorBrush(_sheen.Accent);
            CardBalanceUsd.Foreground = new SolidColorBrush(_sheen.Accent);

            CardAddress.SizeChanged += CardAddress_SizeChanged;

            StateLabel.Text = "[Wallet]";

            // The handle, not the whole strip: a drag region takes the pointer away from anything
            // under it, and the window's own close button is drawn over the right of this bar.
            Window.CaptionButtons = CaptionButtons.Close;

            // What navigation would do for a page, and once rather than on every load: it subscribes
            // the view model to the aggregator, and OnWindowClosed is the other half of that.
            _ = viewModel.NavigatedToAsync(null, NavigationMode.New, null);

            //        background: linear - gradient(0deg, #0079FF, #0079FF),
            //conic - gradient(from 20.99deg at 50 % 50 %, #0079FF 0deg, #169AF9 90deg, #0079FF 180deg, #169AF9 270deg, #0079FF 360deg);

        }

        public void Test()
        {
            _sheen.Background = Theme.AccentLight.Dark2;

            CardBalanceGram.Foreground = new SolidColorBrush(_sheen.Accent);
            CardBalanceUsd.Foreground = new SolidColorBrush(_sheen.Accent);
        }

        protected override UIElement TitleBarElement => TitleBarHandle;

        protected override void OnLoaded()
        {
            VisualUtilities.AttachTilt(Card, Sheen, 10, 20, 0, OnCardTilt);

            // box-shadow: 0px 1px 0px 0px rgba(255, 255, 255, 0.06)
            //
            // No blur and a single pixel down: this is the highlight that makes the
            // address read as engraved into the card rather than printed on it. Here
            // rather than in the markup because the mask comes from the glyphs, which
            // only exist once the text has been laid out.
            VisualUtilities.DropShadow(CardAddress, radius: 0, opacity: 1.0f,
                target: CardAddressShadow, color: Colors.White, offset: new Vector3(1, 0, 0));

            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateAddress(ViewModel.Address);
            UpdateBalance(ViewModel.Balance);
            UpdateEmpty(ViewModel.IsEmpty);
        }

        protected override void OnUnloaded()
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;

            VisualUtilities.DetachTilt(Card);

            // Detaching stops the per-frame callback wherever it happens to be, and the window can
            // be shown again with its surface intact, so the bands are reset explicitly.
            OnCardTilt(Vector2.Zero, 0);
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
            if (e.PropertyName == nameof(ViewModel.Address))
            {
                UpdateAddress(ViewModel.Address);
            }
            else if (e.PropertyName == nameof(ViewModel.Balance))
            {
                UpdateBalance(ViewModel.Balance);
            }
            else if (e.PropertyName == nameof(ViewModel.Currency) || e.PropertyName == nameof(ViewModel.CurrencyRate))
            {
                UpdateBalance(ViewModel.Balance);
            }
            else if (e.PropertyName == nameof(ViewModel.IsEmpty))
            {
                UpdateEmpty(ViewModel.IsEmpty);
            }
        }

        private void UpdateAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                CardAddress.Text = string.Empty;
            }
            else
            {
                var builder = new StringBuilder();

                for (int i = 0; i < address.Length; i += 4)
                {
                    if (i > 0)
                    {
                        builder.Append(i == 24 ? "\n" : " ");
                    }

                    builder.Append(address.Substring(i, 4).ToUpperInvariant());
                }

                CardAddress.Text = builder.ToString();
            }

            if (_clientService.TryGetUser(_clientService.Options.MyId, out User user))
            {
                CardName.Text = user.FullName().ToUpperInvariant();
            }
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

            var amount = Formatter.TonBalance(archived).Join();

            var text = new TextBlock();
            text.Inlines.Add(new Run
            {
                Text = Icons.Ton,
                FontFamily = BootStrapper.Current.Resources["SymbolThemeFontFamily"] as FontFamily
            });
            text.Inlines.Add(new Run { Text = string.Format(" {0} [in old wallets]", amount) });

            ArchiveButton.Content = text;
            ArchiveButton.Visibility = Visibility.Visible;
        }

        private void Archive_Click(object sender, RoutedEventArgs e)
        {
            _navigationService.ShowPopup(new WalletBackupPopup(_navigationService));
        }

        private void UpdateBalance(BigInteger balance)
        {
            UpdateArchive();

            // Half an answer is not shown: grams with no rate to convert them at would be a number
            // beside a currency it has not been converted into, and a rate of one is what an
            // unfetched rate looks like - dollars wearing the wrong name.
            var known = ViewModel.IsSynchronized && ViewModel.CurrencyRate > 0;

            CardBalanceIcon.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
            CardBalanceText.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
            CardBalanceUsd.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
            CardBalanceSkeleton.Visibility = known ? Visibility.Collapsed : Visibility.Visible;

            if (!known)
            {
                ShowSkeleton();
                return;
            }

            var amount = Formatter.TonBalance(balance);
            CardBalance.Text = amount.Integer;
            CardBalanceFraction.Text = amount.Fraction;
            // million_gram_to_usd_rate is whole dollars per 1,000,000 grams, and a balance is in
            // nanograms: 1e9 to grams and 1e6 more to millions. In floating point, because in
            // integers the division lands on whole cents and a balance smaller than one - which a
            // wallet holds more often than not - comes out as nothing.
            var rate = _clientService.Options.MillionGramToUsdRate;
            var dollars = (double)balance * rate / 1e15;

            // TDLib quotes every rate as what one dollar buys, so the chosen currency is one
            // multiplication away - and none at all when it is the dollar, or when no rate has
            // arrived for it.
            var currency = ViewModel.Currency ?? "USD";
            var converted = ViewModel.CurrencyRate > 0
                ? dollars * ViewModel.CurrencyRate
                : dollars;

            CardBalanceUsd.Text = Formatter.FormatAmountExact(converted, currency);
        }

        /// <summary>
        /// The two bars the balance and its converted line will fill, shimmering.
        /// </summary>
        /// <remarks>
        /// Once: every call builds a visual and hands it to the element, so calling it on each
        /// update would stack them. The card is drawn at a fixed design size inside a Viewbox, so
        /// these numbers are that design's, not the screen's.
        ///
        /// White rather than the theme's hover colour, which the skeleton would otherwise take: the
        /// card is the same blue in either theme, and a light theme's hover is dark.
        /// </remarks>
        private void ShowSkeleton()
        {
            if (_skeleton)
            {
                return;
            }

            _skeleton = true;

            VisualUtilities.SetSkeleton(CardBalanceSkeleton, new Vector2(180, 62),
                Color.FromArgb(0x3A, 0xFF, 0xFF, 0xFF),
                CanvasGeometry.CreateRoundedRectangle(null, 0, 0, 170, 32, 8, 8),
                CanvasGeometry.CreateRoundedRectangle(null, 0, 40, 120, 22, 8, 8));
        }

        private bool _skeleton;

        private void Card_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Card.CornerRadius = new CornerRadius(e.NewSize.Width * (3.18 / 85.60));

            // A new surface only when the size actually moved, and the card settles at one size
            // and is measured again at that size on every reflow.
            if (_sheen.Update(e.NewSize))
            {
                if (Sheen.Fill is ImageBrush brush)
                {
                    brush.ImageSource = _sheen.Source;
                }
                else
                {
                    Sheen.Fill = new ImageBrush
                    {
                        ImageSource = _sheen.Source
                    };
                }
            }
        }

        /// <summary>
        /// Repaints the card's gradient for one frame of the tilt. What changes is the shape of
        /// the ramp, which is why it is a redraw rather than something the compositor can do.
        /// </summary>
        private void OnCardTilt(Vector2 pointer, float amount)
        {
            _sheen.Render(pointer, amount);
        }

        private void CardAddress_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CardAddressShadow.RenderTransform = new CompositeTransform
            {
                Rotation = 90,
                TranslateX = -(e.NewSize.Height - e.NewSize.Width) / 2
            };
            CardAddress.RenderTransform = new CompositeTransform
            {
                Rotation = 90,
                TranslateX = -(e.NewSize.Height - e.NewSize.Width) / 2
            };
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var currency = flyout.CreateFlyoutItem(MenuItemCurrency, "[Currency]", Icons.Globe);
            currency.KeyboardAcceleratorTextOverride = ViewModel.Currency;

            flyout.CreateFlyoutItem(MenuItemRecoveryPhrase, "[Keys & Backup]", Icons.Cloud);
            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(MenuItemAbout, "[How It Works]", Icons.QuestionCircle);

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
            _navigationService.ShowPopup(new WalletBackupPopup(_navigationService));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _navigationService.ShowPopup(new WalletBackupPopup(_navigationService));
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
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
