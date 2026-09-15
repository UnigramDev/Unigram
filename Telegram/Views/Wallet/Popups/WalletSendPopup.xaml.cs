//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// Sends grams to one recipient.
    /// </summary>
    /// <remarks>
    /// The recipient is settled before this opens - a Telegram user, an address, or both - so
    /// everything here is about how much, and about the comment that travels with it.
    /// </remarks>
    public sealed partial class WalletSendPopup : ModalPopup
    {
        // What a comment may weigh, in UTF-8 bytes rather than characters: the engine packs it into
        // a message body and refuses anything larger.
        private const int CommentMaxBytes = 960;

        private readonly IClientService _clientService;
        private readonly IWalletService _wallet;
        private readonly INavigationService _navigationService;

        private readonly long _userId;
        private readonly string _domain;

        // Where the grams are going. Known from the start when the caller had it, and otherwise
        // settled at the moment of sending - see ResolveRecipientAsync for why not before.
        private string _address;

        private TonWalletGaslessTransfersInfo _gasless;

        // Whether the field holds the chosen currency rather than grams. The transfer is always in
        // grams; this only decides which of the two the user is typing.
        private bool _inCurrency;

        private string _comment;
        private bool _isCommentPublic;

        // Set while the code writes the field, so that a swap is not mistaken for typing.
        private bool _updating;

        // Raised the moment a transfer is under way and never lowered. Every click after the first
        // would be a second transfer of real money, and the button is not the only way in: Enter
        // reaches it too, and a click can arrive while the first is still in flight.
        private bool _sending;

        /// <summary>
        /// Sends to a Telegram user, whose address is looked up when the transfer is confirmed.
        /// </summary>
        public WalletSendPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, long userId)
            : this(clientService, wallet, navigationService, userId, Address(clientService, userId))
        {
        }

        /// <summary>
        /// The address the account already knows for a user, if it happens to have it.
        /// </summary>
        /// <remarks>
        /// Full info is a cache, so this is free when it is there and nothing when it is not:
        /// asking for it would be a request, and there is a better request to make later.
        /// </remarks>
        private static string Address(IClientService clientService, long userId)
        {
            return clientService.TryGetUserFull(userId, out UserFullInfo full)
                ? full.WalletAddress
                : null;
        }

        public WalletSendPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, long userId, string address, string domain = null)
        {
            InitializeComponent();

            _clientService = clientService;
            _wallet = wallet;
            _navigationService = navigationService;
            _userId = userId;
            _address = address;
            _domain = domain;

            Title = CreateTitle();

            SwapGlyph.Text = Icons.ArrowSort;
            Suffix.Text = GramSuffix;

            BalanceLabel.Text = string.Format("[Balance: {0} Grams]", Formatter.TonBalance(State.BalanceNanograms).Join());

            UpdateAmount();
            UpdateGasless();

            LoadGaslessAsync();
        }

        /// <summary>
        /// The address the transfer is bound for, which is the one thing the caller has to have
        /// settled: a user without an address is not somewhere grams can go.
        /// </summary>
        private const string GramSuffix = "GRAM";

        private UIElement CreateTitle()
        {
            var title = new TextBlock
            {
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            TextBlockHelper.SetMarkdown(title, string.Format("[Send to **{0}**]", Recipient()));
            return title;
        }

        private string Recipient()
        {
            if (_userId != 0 && _clientService.TryGetUser(_userId, out User user))
            {
                return user.FullName();
            }

            if (!string.IsNullOrEmpty(_domain))
            {
                return _domain;
            }

            // The ends are what a reader compares, and no title is 48 characters wide.
            return _address.Length > 8
                ? _address.Substring(0, 4) + "…" + _address.Substring(_address.Length - 4)
                : _address;
        }

        #region Amount

        /// <summary>
        /// What the field holds, in nanograms, or zero while it holds nothing that parses.
        /// </summary>
        private BigInteger Nanograms()
        {
            if (!TryParseUnits(Amount.Text, _inCurrency ? Formatter.GetAmountExponent(Currency) : TonDecimals, out var units))
            {
                return BigInteger.Zero;
            }

            if (!_inCurrency)
            {
                return units;
            }

            // Typed in money: through the rate, and back to the smallest unit the chain deals in.
            var amount = (double)units / Math.Pow(10, Formatter.GetAmountExponent(Currency));
            return WalletHelper.ToNanograms(_clientService, State, amount);
        }

        private const int TonDecimals = 9;

        /// <summary>
        /// The wallet as it is now, not as it was when this opened: the balance moves while the
        /// popup is up, and it is what the amount is judged against.
        /// </summary>
        private WalletState State => _wallet.State;

        private string Currency => State?.Currency ?? "USD";

        /// <summary>
        /// Reads a typed amount as an exact integer of its smallest unit.
        /// </summary>
        /// <remarks>
        /// Never through a double: a tenth is not one, and an amount of money must arrive as the
        /// number that was typed.
        /// </remarks>
        private static bool TryParseUnits(string text, int exponent, out BigInteger units)
        {
            units = BigInteger.Zero;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var separator = text.IndexOfAny(Separators);

            var whole = separator < 0 ? text : text.Substring(0, separator);
            var fraction = separator < 0 ? string.Empty : text.Substring(separator + 1);

            if (fraction.Length > exponent)
            {
                return false;
            }

            if (!IsDigits(whole) || !IsDigits(fraction))
            {
                return false;
            }

            var scale = BigInteger.Pow(10, exponent);

            units = (whole.Length > 0 ? BigInteger.Parse(whole) : BigInteger.Zero) * scale;

            if (fraction.Length > 0)
            {
                units += BigInteger.Parse(fraction.PadRight(exponent, '0'));
            }

            return true;
        }

        private static readonly char[] Separators = new[] { '.', ',' };

        private static bool IsDigits(string value)
        {
            foreach (var character in value)
            {
                if (!char.IsDigit(character))
                {
                    return false;
                }
            }

            return true;
        }

        private void Amount_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            var separators = 0;

            foreach (var character in args.NewText)
            {
                if (char.IsDigit(character))
                {
                    continue;
                }

                if (Array.IndexOf(Separators, character) >= 0 && ++separators == 1)
                {
                    continue;
                }

                args.Cancel = true;
                return;
            }

            // More decimals than the unit has cannot be entered rather than silently dropped, so
            // that what is on screen is what will be sent.
            args.Cancel = !TryParseUnits(args.NewText, _inCurrency ? Formatter.GetAmountExponent(Currency) : TonDecimals, out _)
                && args.NewText.Length > 0;
        }

        private void Amount_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updating || _sending)
            {
                return;
            }

            UpdateAmount();
        }

        /// <summary>
        /// The converted amount, the validation and the button, all of which follow the field.
        /// </summary>
        private void UpdateAmount()
        {
            var nanograms = Nanograms();

            Converted.Text = _inCurrency
                ? string.Format("≈ {0} {1}", Formatter.TonBalance(nanograms).Join(), GramSuffix)
                : string.Format("≈ {0}", Formatter.FormatAmountExact(WalletHelper.ToCurrency(_clientService, State, nanograms), Currency));

            var message = Validate(nanograms);

            Validation.Text = message ?? string.Empty;
            Validation.Visibility = Visibility.Visible;

            IsPrimaryButtonEnabled = message == null && nanograms > BigInteger.Zero;
            PrimaryButtonContent = string.Format("[Send {0} Grams]", Formatter.TonBalance(nanograms).Join());
        }

        /// <summary>
        /// What is wrong with the amount, or null when nothing is.
        /// </summary>
        private string Validate(BigInteger nanograms)
        {
            if (nanograms.IsZero)
            {
                // Nothing typed yet is not an error, it is the starting point.
                return null;
            }

            var minimum = _gasless is { LeftCount: > 0 }
                ? _clientService.Options.TonWalletGaslessTransferAmountMin
                : _clientService.Options.TonWalletTransferAmountMin;

            if (minimum > 0 && nanograms < minimum)
            {
                return string.Format("[Min Amount: {0} GRAM]", Formatter.TonBalance(minimum).Join());
            }

            if (nanograms > State.BalanceNanograms)
            {
                return "[Not enough Grams]";
            }

            return null;
        }

        private void Swap_Click(object sender, RoutedEventArgs e)
        {
            var nanograms = Nanograms();

            _inCurrency = !_inCurrency;
            Suffix.Text = _inCurrency ? Currency : GramSuffix;

            _updating = true;

            try
            {
                if (_inCurrency)
                {
                    var amount = WalletHelper.ToCurrency(_clientService, State, nanograms);
                    var units = (long)Math.Round(amount * Math.Pow(10, Formatter.GetAmountExponent(Currency)));

                    var split = Formatter.SplitAmount(units, Formatter.GetAmountExponent(Currency), Formatter.GetAmountExponent(Currency));
                    Amount.Text = split.Join();
                }
                else
                {
                    Amount.Text = Formatter.TonBalance(nanograms).Join();
                }

                Amount.SelectionStart = Amount.Text.Length;
            }
            finally
            {
                _updating = false;
            }

            UpdateAmount();
        }

        #endregion

        #region Fees

        private async void LoadGaslessAsync()
        {
            var response = await _clientService.SendAsync(new GetTonWalletGaslessTransfersInfo());
            if (response is TonWalletGaslessTransfersInfo info)
            {
                _gasless = info;

                UpdateGasless();

                // The free transfers have their own minimum, so the amount has to be judged again.
                UpdateAmount();
            }
        }

        private void UpdateGasless()
        {
            FeesButton.Content = _gasless is { LeftCount: > 0 }
                ? "[Fees are covered by Telegram ›]"
                : "[This transfer pays a network fee ›]";
        }

        private void Fees_Click(object sender, RoutedEventArgs e)
        {
            var text = new StringBuilder();

            if (_gasless == null)
            {
                text.Append("[Telegram covers the fee on a number of transfers each day.]");
            }
            else
            {
                text.AppendFormat("[Telegram covers the fee on {0} transfers a day.]", _gasless.LeftCount);
                text.AppendLine();
                text.AppendLine();

                if (_gasless.LeftCount > 0)
                {
                    text.AppendFormat("[{0} left today.]", _gasless.LeftCount);
                }
                else
                {
                    // No number to give: what a transfer costs is settled when it is signed, and
                    // nothing is signed yet.
                    text.Append("[None left today, so this transfer pays the network's own fee.]");
                }
            }

            _ = MessagePopup.ShowNestedAsync(XamlRoot, text.ToString(), "[Fees]", Strings.OK);
        }

        #endregion

        #region Comment

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(MenuItemComment, _comment == null ? "[Add Comment]" : "[Edit Comment]", Icons.Chat);
            flyout.CreateFlyoutItem(MenuItemTopUp, "[Top Up]", Icons.AddCircle);

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private async void MenuItemComment()
        {
            var popup = new InputPopup
            {
                Title = "[Add comment]",
                PlaceholderText = "[Optional message]",
                Text = _comment ?? string.Empty,
                CheckBoxText = "[Make comment public]",
                IsChecked = _isCommentPublic,
                MinLength = 0,
                PrimaryButtonText = "[Add]",
                SecondaryButtonText = Strings.Cancel
            };

            // In bytes, because that is what the limit is: a comment of emoji runs out four times
            // sooner than one of letters, and MaxLength counts characters.
            popup.Validating += Comment_Validating;

            var confirm = await _navigationService.ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            _comment = popup.Text.Length > 0 ? popup.Text : null;
            _isCommentPublic = popup.IsChecked;

            Comment.Text = _comment ?? string.Empty;
            CommentRoot.Visibility = _comment != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Comment_Validating(object sender, InputPopupValidatingEventArgs e)
        {
            e.Cancel = Encoding.UTF8.GetByteCount(e.Text) > CommentMaxBytes;
        }

        private void MenuItemTopUp()
        {
            _navigationService.ShowPopup(new WalletSharePopup(_clientService, _navigationService, State.Address));
        }

        #endregion

        private async void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            // A second click while the first is still going spends real money twice. Refused here
            // rather than by disabling the button, which would say the popup is finished when it is
            // not.
            args.Cancel = _sending;

            if (_sending)
            {
                return;
            }

            var nanograms = Nanograms();
            if (nanograms.IsZero || Validate(nanograms) != null)
            {
                args.Cancel = true;
                return;
            }

            // Held for everything that follows: the popup is what the user is waiting in front of,
            // and the close only happens when this is completed.
            var deferral = args.GetDeferral();

            _sending = true;
            IsPrimaryButtonPending = true;

            var recipient = await ResolveRecipientAsync();
            if (recipient != null || string.IsNullOrEmpty(_address))
            {
                // Nothing left, and nothing spent: the amount is still on screen for a second try,
                // so the popup stays.
                _sending = false;

                IsPrimaryButtonPending = false;
                args.Cancel = true;

                deferral.Complete();

                _ = MessagePopup.ShowNestedAsync(XamlRoot, recipient != null
                    ? "[This transfer can't be sent yet.]" + Environment.NewLine + Environment.NewLine + recipient.Message
                    : "[This transfer can't be sent yet.]", "[Send Grams]", Strings.OK);
                return;
            }

            if (!await WalletHelper.EnsureBoundAsync(_wallet, _navigationService))
            {
                // They were asked for a password or a phrase and said no. The amount they typed is
                // still here, so the popup is too.
                _sending = false;

                IsPrimaryButtonPending = false;
                args.Cancel = true;

                deferral.Complete();
                return;
            }

            Error error;

            try
            {
                var result = await _wallet.SendAsync(_address, _userId, _domain, nanograms, _comment, _isCommentPublic, _gasless is { LeftCount: > 0 });
                if (result.Error == null)
                {
                    Result = result;

                    // Completing the deferral is what closes the popup: the button click it belongs
                    // to has been waiting for this.
                    deferral.Complete();
                    return;
                }

                error = result.Error;
            }
            catch (Exception ex)
            {
                // The engine's own failures still arrive this way: nothing was signed, so nothing
                // was sent, and there is no message from the server to show.
                Logger.Error("wallet transfer failed: " + ex.Message);
                error = null;
            }

            // Still closed, and still only sendable once: retrying means opening the popup again,
            // which is a deliberate act, where leaving this one armed would put a second transfer of
            // real money one stray click away with no way to be sure the first did not leave.
            IsPrimaryButtonPending = false;
            deferral.Complete();

            _ = MessagePopup.ShowNestedAsync(XamlRoot, error != null
                ? "[The transfer could not be sent.]" + Environment.NewLine + Environment.NewLine + error.Message
                : "[The transfer could not be sent.]", "[Send Grams]", Strings.OK);
        }

        /// <summary>
        /// Makes sure there is somewhere to send to, and answers with what went wrong if there is
        /// not.
        /// </summary>
        /// <remarks>
        /// Here rather than when the popup opens, because the second half of this creates a wallet
        /// for somebody else: doing it on open would make one for every person whose send screen
        /// was looked at and closed. The account also refuses it unless this wallet has a balance,
        /// which is the anti-abuse side of the same thing.
        /// </remarks>
        private async Task<Error> ResolveRecipientAsync()
        {
            if (!string.IsNullOrEmpty(_address))
            {
                return null;
            }

            var addresses = await _clientService.SendAsync(new GetUserTonWalletAddresses(new[] { _userId }));
            if (addresses is Error error)
            {
                return error;
            }

            // A user with no wallet is left out of the answer rather than reported, so an empty
            // list is the question being answered, not a failure.
            if (addresses is UserTonWalletAddresses known)
            {
                foreach (var address in known.Addresses)
                {
                    if (address.UserId == _userId && address.WalletAddress.Length > 0)
                    {
                        _address = address.WalletAddress;
                        return null;
                    }
                }
            }

            var created = await _clientService.SendAsync(new CreateUserTonWallet(_userId));
            if (created is Text text && text.TextValue.Length > 0)
            {
                _address = text.TextValue;
            }

            // Null with nothing to send to is the caller's cue that it failed without the account
            // saying why, which is the one case there are no words for but ours.
            return created as Error;
        }

        /// <summary>
        /// What was sent, once something was. Null means the popup was dismissed.
        /// </summary>
        public WalletTransferResult Result { get; private set; }
    }
}
