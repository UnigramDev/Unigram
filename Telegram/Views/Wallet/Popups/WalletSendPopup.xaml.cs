//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// Sends grams to one recipient.
    /// </summary>
    /// <remarks>
    /// The recipient is chosen before this opens - a Telegram user, an address, or both - and
    /// looked up as it opens, so everything else here is about how much, and about the comment
    /// that travels with it.
    /// </remarks>
    public sealed partial class WalletSendPopup : WalletPopup
    {
        // What a comment may weigh, in UTF-8 bytes rather than characters: the engine packs it into
        // a message body and refuses anything larger.
        private const int CommentMaxBytes = 960;

        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly long _userId;
        private readonly string _domain;

        // Where the grams are going. Known from the start when the caller had it, otherwise read
        // by the lookup, and for a user with no wallet yet settled at the moment of sending - see
        // ResolveRecipientAsync for why not before.
        private string _address;

        // The lookup started on open, kept so that sending waits for it instead of asking again.
        private Task<Object> _lookup;

        private TonWalletGaslessTransfersInfo _gasless;

        // Whether the field holds the chosen currency rather than grams. The transfer is always in
        // grams; this only decides which of the two the user is typing.
        private bool _inCurrency;

        private string _comment;
        private bool _isCommentPublic;

        // Whether the recipient can receive a private comment, or null while that is still being
        // asked. An encrypted comment is encrypted to their public key, so a recipient whose key
        // the account does not have cannot be sent one - see UpdateCommentPrivacyAsync.
        private bool? _canEncryptComment;

        // Whether the recipient has no wallet and this one has no balance to have one made with.
        // Nothing can be sent until that changes. See CreateRecipientWalletAsync.
        private bool _unfunded;

        // The key a private comment is encrypted to. Passed on rather than left to the engine,
        // which can only read it off a deployed contract - and a wallet that has never sent
        // anything has none, while the account still knows its key.
        private byte[] _publicKey;

        // Set while the code writes the field, so that a swap is not mistaken for typing.
        private bool _updating;

        // What the field means, when the field cannot say it exactly. The chosen currency has two
        // decimals and grams have nine, so rendering an amount into money truncates it and reading
        // that back truncates again - always downward, because BigInteger division rounds towards
        // zero. Swapping back and forth without typing would walk the amount to nothing. So a swap
        // pins the amount it converted, every read prefers the pin, and the first keystroke drops
        // it.
        private BigInteger? _pinned;

        // Raised the moment a transfer is under way and never lowered. Every click after the first
        // would be a second transfer of real money, and the button is not the only way in: Enter
        // reaches it too, and a click can arrive while the first is still in flight.
        private bool _sending;

        /// <summary>
        /// Sends to a Telegram user, whose address is looked up when the popup opens.
        /// </summary>
        public WalletSendPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, long userId)
            : this(clientService, wallet, navigationService, userId, null)
        {
        }

        /// <summary>
        /// From a transfer link, which may have settled the amount as well as the recipient.
        /// </summary>
        public WalletSendPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, long userId, long nanograms)
            : this(clientService, wallet, navigationService, userId, null)
        {
            Prefill(nanograms);
        }

        public WalletSendPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, long userId, string address, long nanograms)
            : this(clientService, wallet, navigationService, userId, address)
        {
            Prefill(nanograms);
        }

        /// <summary>
        /// Puts an amount in the field as though it had been typed.
        /// </summary>
        /// <remarks>
        /// In grams rather than in the chosen currency, whatever the field was last showing: a link
        /// asks for an amount of grams, and converting it would make what is sent depend on a rate
        /// that moves. The user can still swap, and then it converts like anything else.
        /// </remarks>
        private void Prefill(long nanograms)
        {
            if (nanograms <= 0)
            {
                return;
            }

            _inCurrency = false;

            _updating = true;
            Amount.Text = Typed(nanograms, TonDecimals);
            _updating = false;

            _pinned = nanograms;
            UpdateAmount();
        }

        public WalletSendPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, long userId, string address, string domain = null)
            : base(wallet, navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _userId = userId;
            _address = address;
            _domain = domain;

            Title = Strings.WalletSendMoneyTo;

            if (_clientService.TryGetUser(userId, out User user))
            {
                Photo.Source = ProfilePictureSource.User(clientService, user);
                NameText.Text = user.FullName();
            }
            else
            {
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.Ton);
                NameText.Text = Strings.WalletGramWalletAddress;
            }

            UpdateAddress(address);

            Amount.Validate = ValidateAmount;

            // Typed at the popup rather than at the field, so it does not matter which part of the
            // screen holds focus - the amount is what this screen is for.
            CharacterReceived += OnCharacterReceived;

            // Ctrl+V and Ctrl+Z arrive as control characters that CharacterReceived would offer to
            // the field as digits, so they are taken here before that.
            KeyDown += OnKeyDown;

            // And focus stays off Send, because a focused button takes the space bar as a press:
            // on this popup that is a transfer, sent by a key that was meant for the number.
            FocusPrimaryButton = false;

            SwapGlyph.Text = Icons.ArrowSort;
            SuffixGram.Text = GramSuffix;
            PrefixCurrency.Text = CurrencySymbol;
            SuffixCurrency.Text = Currency;

            UpdateWalletState(State);
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();

            // The field is where every keystroke has to land, and it is not a text box: nothing
            // gives it focus on its own. FocusPrimaryButton is off for the same reason - the popup
            // hands focus to Send once it is up, which would take it away again, and its check for
            // content that focused itself only holds if this succeeded first.
            Amount.Focus(FocusState.Programmatic);

            // OnLoaded can run more than once, and the answer does not change while this is up.
            _lookup ??= LookupRecipientAsync();
        }

        /// <summary>
        /// Reads the recipient's wallet: its address when the caller only had a user, and whether
        /// a private comment is possible for it.
        /// </summary>
        /// <remarks>
        /// A private comment is encrypted to the recipient's public key, which the account reports
        /// alongside the address when it has one. That is why the option turns on for some
        /// recipients and not others, and it is settled here rather than at the moment of sending
        /// so it is never offered and then refused.
        ///
        /// A user with no wallet gets one here, while the popup opens, so the address, the fee and
        /// the comment option are all there before the amount is typed. The account creates one
        /// only for a sender with a balance, so with none it is not asked: the popup says the funds
        /// are missing up front, and asks again once the balance arrives.
        /// </remarks>
        private async Task<Object> LookupRecipientAsync()
        {
            Object response;

            UpdateRecipientPending(true);

            if (!string.IsNullOrEmpty(_address))
            {
                response = await _clientService.SendAsync(new GetAddressTonWallet(_address));
            }
            else if (_userId != 0)
            {
                response = await _clientService.SendAsync(new GetUserTonWalletAddresses(new[] { _userId }));

                // Read first: creating is only for a user who has none.
                if (response is UserTonWalletAddresses addresses && First(addresses) == null)
                {
                    response = await CreateRecipientWalletAsync();
                }
            }
            else
            {
                response = null;
            }

            var wallet = response switch
            {
                UserTonWalletAddress single => single,
                UserTonWalletAddresses many => First(many),
                _ => null
            };

            if (wallet != null && string.IsNullOrEmpty(_address))
            {
                _address = wallet.WalletAddress;

                UpdateAddress(_address);
                UpdateFeeAsync();
            }

            // A failed lookup says nothing about the recipient, so it stays unknown and the engine
            // decides at sending time. Either way the user's choice is left as they made it: a
            // private comment that cannot be encrypted is refused with CommentUnavailable, never
            // quietly sent in the clear.
            _canEncryptComment = response is Error ? null : HasPublicKey(wallet?.PublicKey);
            _publicKey = _canEncryptComment == true ? wallet.PublicKey : null;

            UpdateRecipientPending(false);
            return response;
        }

        /// <summary>
        /// Has the account make a wallet for the recipient, or answers why not.
        /// </summary>
        /// <remarks>
        /// Not asked while this wallet's balance is known to be empty, which the account would
        /// refuse anyway. Either way the popup is marked unfunded, and UpdateWalletState asks again
        /// once there is a balance.
        /// </remarks>
        private async Task<Object> CreateRecipientWalletAsync()
        {
            if (State is { IsSynchronized: true } state && state.BalanceNanograms <= 0)
            {
                SetUnfunded(true);
                return null;
            }

            var created = await _clientService.SendAsync(new CreateUserTonWallet(_userId));
            if (created is Error error && string.Equals(error.Message, "WALLET_BALANCE_EMPTY", StringComparison.Ordinal))
            {
                SetUnfunded(true);
                return null;
            }

            SetUnfunded(false);
            return created;
        }

        private void SetUnfunded(bool unfunded)
        {
            if (_unfunded != unfunded)
            {
                _unfunded = unfunded;
                UpdateAmount();
            }
        }

        /// <remarks>
        /// Not matched against the user id: the request names one user, and the account may answer
        /// with user_id 0, which means unknown rather than somebody else.
        /// </remarks>
        private static UserTonWalletAddress First(UserTonWalletAddresses addresses)
        {
            foreach (var address in addresses.Addresses)
            {
                if (address.WalletAddress.Length > 0)
                {
                    return address;
                }
            }

            return null;
        }

        private void UpdateRecipientPending(bool pending)
        {
            // TODO: play the lookup animation in RecipientRoot.
        }

        /// <summary>
        /// Whether this is a key rather than the absence of one.
        /// </summary>
        /// <remarks>
        /// All zeros counts as absent, and checking for it is not defensive: the account fills the
        /// field rather than omitting it when it has no key to report, so a null check alone would
        /// read that as a usable key and encrypt to nothing.
        /// </remarks>
        private static bool HasPublicKey(byte[] key)
        {
            if (key == null)
            {
                return false;
            }

            foreach (var part in key)
            {
                if (part != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Everything on this screen is priced against the wallet, so all of it is rebuilt: the
        /// balance it is spent from, who is paying the fee, and the amount as converted.
        /// </summary>
        protected override void UpdateWalletState(WalletState state)
        {
            BalanceLabel.Text = string.Format(Strings.WalletBalanceAmount, Formatter.Grams(state.BalanceNanograms));

            // Funds arrived while the popup was open, so the recipient's wallet can be made now.
            if (_unfunded && state.BalanceNanograms > 0 && _lookup is { IsCompleted: true })
            {
                _lookup = LookupRecipientAsync();
            }

            UpdateGasless();
            UpdateAmount();

            // The rates may have just arrived, or gone: the swap is only offered while there is
            // something to swap into.
            SwapButton.IsEnabled = _inCurrency || State is { CurrencyRate: > 0 };
        }

        private const string GramSuffix = "GRAM";

        /// <summary>
        /// Draws the address in groups of four, which is the only way 48 characters of base64 can
        /// be compared by eye.
        /// </summary>
        /// <remarks>
        /// Empty when there is none. Opened from a chat the popup holds a user id, and the address
        /// behind it arrives with the lookup, or for a user with no wallet only when the transfer
        /// commits - so the card shows who it is going to and says nothing it does not know yet.
        /// </remarks>
        private void UpdateAddress(string address)
        {
            address = WalletHelper.DisplayAddress(address) ?? string.Empty;

            var groups = new[]
            {
                AddressGroup0, AddressGroup1, AddressGroup2, AddressGroup3,
                AddressGroup4, AddressGroup5, AddressGroup6, AddressGroup7,
                AddressGroup8, AddressGroup9, AddressGroup10, AddressGroup11
            };

            for (int i = 0; i < groups.Length; i++)
            {
                var offset = i * 4;

                groups[i].Text = offset < address.Length
                    ? address.Substring(offset, Math.Min(4, address.Length - offset))
                    : string.Empty;
            }
        }

        #region Amount

        /// <summary>
        /// What the field holds, in nanograms, or zero while it holds nothing that parses.
        /// </summary>
        private BigInteger Nanograms()
        {
            // The pin is what the user entered; the field is only how it is being shown. Sending
            // the pin rather than the rendering is also what keeps the fee and the button
            // agreeing with themselves across a swap.
            if (_pinned is BigInteger pinned)
            {
                return pinned;
            }

            if (!TryParseUnits(Amount.Text, _inCurrency ? Formatter.GetAmountExponent(Currency) : TonDecimals, out var units))
            {
                return BigInteger.Zero;
            }

            if (!_inCurrency)
            {
                return units;
            }

            // Typed in money: through the rate, and back to the smallest unit the chain deals in.
            // Zero when the rate is not known, which reads as nothing typed and leaves the button
            // disabled - the one answer that cannot send the wrong amount.
            return WalletHelper.TryToNanograms(_clientService, State, units, Formatter.GetAmountExponent(Currency), out var nanograms)
                ? nanograms
                : BigInteger.Zero;
        }

        private const int TonDecimals = 9;

        /// <summary>
        /// The wallet as it is now, not as it was when this opened: the balance moves while the
        /// popup is up, and it is what the amount is judged against.
        /// </summary>
        private string Currency => State?.Currency ?? "USD";

        private string CurrencySymbol
        {
            get
            {
                try
                {
                    var formatter = Locale.GetCurrencyFormatter(Currency);
                    if (formatter.Symbol != Currency)
                    {
                        return formatter.Symbol ?? string.Empty;
                    }

                    return string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// An amount as it would have been typed: no group separators, and no trailing zeros.
        /// </summary>
        /// <remarks>
        /// The formatters group thousands, which is right everywhere an amount is read and wrong
        /// in the one place it is written - the field holds what <see cref="TryParseUnits"/> reads
        /// back, and that treats a group separator as a decimal one and gives up.
        /// </remarks>
        private static string Typed(BigInteger units, int exponent)
        {
            if (units <= BigInteger.Zero)
            {
                return string.Empty;
            }

            var scale = BigInteger.Pow(10, exponent);
            var whole = BigInteger.DivRem(units, scale, out var rest);

            var text = whole.ToString(CultureInfo.InvariantCulture);
            if (rest.IsZero)
            {
                return text;
            }

            var fraction = rest.ToString(CultureInfo.InvariantCulture)
                .PadLeft(exponent, '0')
                .TrimEnd('0');

            return text + LocaleService.Current.CurrentCulture.NumberFormat.NumberDecimalSeparator + fraction;
        }

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
                if (character is < '0' or > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
        {
            if (args.Handled = Amount.TryAppend(args.Character))
            {
                Diamond.Kick(args.Character == '\b' ? -360 : 250);
            }
        }

        private void OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_sending || !WindowContext.IsKeyDown(VirtualKey.Control))
            {
                return;
            }

            if (e.Key == VirtualKey.V)
            {
                e.Handled = true;
                PasteAmount();
            }
            else if (e.Key == VirtualKey.Z)
            {
                if (e.Handled = Amount.TryUndo())
                {
                    Diamond.Kick(-360);
                }
            }
        }

        /// <summary>
        /// Puts the clipboard in the field, if it holds an amount.
        /// </summary>
        /// <remarks>
        /// It replaces rather than inserts, because the field has no caret for a paste to land at -
        /// see <see cref="AmountTextBlock.TryReplace"/>. Anything that is not a number is left
        /// alone rather than partly taken: half a pasted string is not an amount the user meant.
        /// </remarks>
        private async void PasteAmount()
        {
            var clipboard = ClipboardEx.TryGetContent();
            if (clipboard == null || !clipboard.Contains(StandardDataFormats.Text))
            {
                return;
            }

            try
            {
                Amount.TryReplace(await clipboard.GetTextAsync());
            }
            catch
            {
                // Reading it is a remote call and the other side can go away mid-paste.
            }
        }

        /// <summary>
        /// Whether the field will take what this keystroke would make of it.
        /// </summary>
        /// <remarks>
        /// More decimals than the unit has are refused rather than silently dropped, so that what
        /// is on screen is what will be sent. The unit changes with the swap, which is why this is
        /// asked per keystroke rather than settled once.
        /// </remarks>
        private bool ValidateAmount(string text)
        {
            var exponent = _inCurrency ? Formatter.GetAmountExponent(Currency) : TonDecimals;

            if (!TryParseUnits(text, exponent, out var units))
            {
                return false;
            }

            // Nanograms go on the wire as an int64, so long.MaxValue of them is the ceiling. In
            // grams that is 9223372036.854775807 and the field stops there; in money it is
            // whatever the rate makes of the same amount, so more digits are allowed for it.
            if (!_inCurrency)
            {
                return units <= long.MaxValue;
            }

            // No rate is not a reason to refuse a keystroke: nothing typed here can be sent
            // either, because Nanograms answers zero and the button stays down.
            return !WalletHelper.TryToNanograms(_clientService, State, units, exponent, out var nanograms)
                || nanograms <= long.MaxValue;
        }

        private void Amount_TextChanged(object sender, EventArgs e)
        {
            if (_updating || _sending)
            {
                return;
            }

            // Typed, so the field is the truth again.
            _pinned = null;

            UpdateAmount();
        }

        /// <summary>
        /// The converted amount, the validation and the button, all of which follow the field.
        /// </summary>
        private void UpdateAmount()
        {
            var nanograms = Nanograms();

            UpdateConverted(nanograms);
            UpdateValidation(nanograms);

            PrimaryButtonContent = nanograms > BigInteger.Zero
                ? string.Format(Strings.WalletSendAmount, Formatter.Grams(nanograms))
                : Strings.WalletSendGrams;

            UpdateFeeAsync();
        }

        /// <summary>
        /// The same amount the other way round, under the field.
        /// </summary>
        /// <remarks>
        /// Blank rather than approximate while the rates are still coming: an amount converted at
        /// no rate is not a rough answer, it is a different currency's number wearing this one's
        /// name. The field itself is always grams or always the chosen currency, so nothing here
        /// is lost by saying nothing.
        /// </remarks>
        private void UpdateConverted(BigInteger nanograms)
        {
            if (_inCurrency)
            {
                Converted.Text = string.Format("{0} {1}", Formatter.GramExact(nanograms).Join(), GramSuffix);
            }
            else if (WalletHelper.TryToCurrency(_clientService, State, nanograms, out var amount))
            {
                Converted.Text = string.Format("\u2248 {0}", Formatter.FormatAmountExact(amount, WalletHelper.CurrencyDecimals, Currency));
            }
            else
            {
                Converted.Text = string.Empty;
            }
        }

        private void UpdateValidation(BigInteger nanograms)
        {
            var message = Validate(nanograms);

            Validation.Text = message ?? string.Empty;
            Validation.Visibility = Visibility.Visible;

            IsPrimaryButtonEnabled = message == null && nanograms > BigInteger.Zero;
        }

        /// <summary>
        /// What is wrong with the amount, or null when nothing is.
        /// </summary>
        private string Validate(BigInteger nanograms)
        {
            if (_unfunded)
            {
                // Before anything is typed, because nothing typed can change it.
                return Strings.WalletInsufficientFunds;
            }

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
                return string.Format(Strings.WalletMinimumAmount, Formatter.Grams(minimum, capital: true));
            }

            if (nanograms > State.BalanceNanograms)
            {
                return Strings.WalletInsufficientFunds;
            }

            // The fee comes out of the balance too, unless Telegram pays it. Sending all of it
            // would fail on chain and still be charged for the attempt. Checked once a fee is
            // known for this recipient; it does not scale with the amount.
            if (_gasless is not { LeftCount: > 0 } && _feePriced is { } priced && priced.Address == _address && nanograms + _feeValue > State.BalanceNanograms)
            {
                return Strings.WalletInsufficientFunds;
            }

            return null;
        }

        private void Swap_Click(object sender, RoutedEventArgs e)
        {
            // Nothing to swap into while the rates are still coming: the field would be handed an
            // amount converted at no rate, and the button would then send it.
            if (!_inCurrency && State is not { CurrencyRate: > 0 })
            {
                return;
            }

            var nanograms = Nanograms();

            _inCurrency = !_inCurrency;
            _pinned = nanograms > BigInteger.Zero ? nanograms : null;
            ShowHideCurrency(_inCurrency);

            _updating = true;

            try
            {
                if (_inCurrency)
                {
                    var exponent = Formatter.GetAmountExponent(Currency);

                    Amount.Text = WalletHelper.TryToCurrency(_clientService, State, nanograms, out var amount)
                        ? Typed(Formatter.Rescale(amount, WalletHelper.CurrencyDecimals, exponent), exponent)
                        : string.Empty;
                }
                else
                {
                    Amount.Text = Typed(nanograms, TonDecimals);
                }
            }
            finally
            {
                _updating = false;
            }

            UpdateAmount();
        }

        private bool _currencyCollapsed = true;
        private bool _currencySwapped;
        private int _currencyGeneration;

        private async void ShowHideCurrency(bool show)
        {
            if (_currencyCollapsed != show)
            {
                return;
            }

            var generation = ++_currencyGeneration;

            _currencyCollapsed = !show;
            PrefixGram.Visibility = Visibility.Visible;
            SuffixGram.Visibility = Visibility.Visible;

            PrefixCurrency.Visibility = Visibility.Visible;
            SuffixCurrency.Visibility = Visibility.Visible;

            if (!_currencySwapped)
            {
                _currencySwapped = true;
                await Amount.UpdateLayoutAsync();
            }

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (generation != _currencyGeneration)
                {
                    return;
                }

                PrefixCurrency.Margin = new Thickness(0);
                SuffixCurrency.Margin = new Thickness(0);

                PrefixGram.Margin = new Thickness(0);
                SuffixGram.Margin = new Thickness(0);

                if (_currencyCollapsed)
                {
                    PrefixCurrency.Visibility = Visibility.Collapsed;
                    SuffixCurrency.Visibility = Visibility.Collapsed;
                }
                else
                {
                    PrefixGram.Visibility = Visibility.Collapsed;
                    SuffixGram.Visibility = Visibility.Collapsed;
                }
            };

            ShowHideCurrencyPrefix(show);
            ShowHideCurrencySuffix(show);

            batch.End();
        }

        private void ShowHideCurrencyPrefix(bool show)
        {
            FrameworkElement next = show ? PrefixCurrency : PrefixGram;
            FrameworkElement prev = show ? PrefixGram : PrefixCurrency;

            var nextVisual = ElementComposition.GetElementVisual(next);
            var prevVisual = ElementComposition.GetElementVisual(prev);

            Canvas.SetZIndex(next, 1);
            Canvas.SetZIndex(prev, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(PrefixGram, true);
            ElementCompositionPreview.SetIsTranslationEnabled(PrefixCurrency, true);

            nextVisual.CenterPoint = new Vector3(nextVisual.Size.X / 2, 0, 0);

            // A spring rather than a curve: the symbol swings in and settles, which reads as the
            // one mark turning over rather than as two marks crossfading.
            var anim = prevVisual.Compositor.CreateSpringScalarAnimation();
            anim.InitialValue = show ? 35 : -35;
            anim.FinalValue = 0;
            anim.DampingRatio = 0.5f;

            var translation = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, show ? 8 : -8);
            translation.InsertKeyFrame(1, 0);

            var easing = prevVisual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1));

            var fadeOut = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
            fadeOut.InsertKeyFrame(0, 1);
            fadeOut.InsertKeyFrame(1, 0, easing);
            fadeOut.Duration = Constants.FastAnimation;

            var fadeIn = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
            fadeIn.InsertKeyFrame(0, 0);
            fadeIn.InsertKeyFrame(1, 1, easing);
            fadeIn.Duration = Constants.FastAnimation;

            prevVisual.StartAnimation("Opacity", fadeOut);
            nextVisual.StartAnimation("Opacity", fadeIn);

            nextVisual.StartAnimation("RotationAngleInDegrees", anim);
            nextVisual.StartAnimation("Translation.X", translation);

            if (next.ActualSize.X < prev.ActualSize.X)
            {
                prev.Margin = new Thickness(next.ActualSize.X - prev.ActualSize.X, 0, 0, 0);
            }
            else
            {
                prev.Margin = new Thickness();
            }

            next.Margin = new Thickness();
        }

        private void ShowHideCurrencySuffix(bool show)
        {
            var next = show ? SuffixCurrency : SuffixGram;
            var prev = show ? SuffixGram : SuffixCurrency;

            var nextVisual = ElementComposition.GetElementVisual(next);
            var prevVisual = ElementComposition.GetElementVisual(prev);

            nextVisual.CenterPoint = new Vector3(nextVisual.Size.X / 2, -4, 0);
            prevVisual.CenterPoint = new Vector3(prevVisual.Size.X / 2, prevVisual.Size.Y - 4, 0);

            var easing = prevVisual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1));

            var fadeOut = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
            fadeOut.InsertKeyFrame(0, 1);
            fadeOut.InsertKeyFrame(1, 0, easing);
            fadeOut.Duration = Constants.FastAnimation;

            var fadeIn = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
            fadeIn.InsertKeyFrame(0, 0);
            fadeIn.InsertKeyFrame(1, 1, easing);
            fadeIn.Duration = Constants.FastAnimation;

            var slideOut = prevVisual.Compositor.CreateVector3KeyFrameAnimation();
            slideOut.InsertKeyFrame(0, new Vector3(1, 1, 1));
            slideOut.InsertKeyFrame(1, new Vector3(1, 0, 1), easing);
            slideOut.Duration = Constants.FastAnimation;

            var slideIn = prevVisual.Compositor.CreateVector3KeyFrameAnimation();
            slideIn.InsertKeyFrame(0, new Vector3(1, 0, 1));
            slideIn.InsertKeyFrame(1, new Vector3(1, 1, 1), easing);
            slideIn.Duration = Constants.FastAnimation;

            prevVisual.StartAnimation("Opacity", fadeOut);
            nextVisual.StartAnimation("Opacity", fadeIn);
            prevVisual.StartAnimation("Scale", slideOut);
            nextVisual.StartAnimation("Scale", slideIn);

            if (next.ActualSize.X < prev.ActualSize.X)
            {
                prev.Margin = new Thickness(0, 0, next.ActualSize.X - prev.ActualSize.X, 0);
            }
            else
            {
                prev.Margin = new Thickness();
            }

            next.Margin = new Thickness();
        }

        #endregion

        #region Fees

        private void UpdateGasless()
        {
            // Reading it is what asks for it: the quota arrives as an update and is cached with the
            // rest of the state, so the first read here is a request and the answer lands through
            // UpdateWalletState like everything else the popup shows.
            _gasless = State?.Gasless;

            UpdateFeeAsync();
        }

        /// <summary>
        /// The line under the balance, which says what the transfer will cost when it costs
        /// anything.
        /// </summary>
        /// <remarks>
        /// Nothing is shown for a transfer Telegram is paying for, and nothing before there is an
        /// address to price against: opened from a chat the popup holds a user id, and the
        /// recipient is only resolved when it commits.
        ///
        /// The estimate is an emulation on the account's side, so it is asked for as rarely as it
        /// can be. Three things bound that:
        ///
        /// - **A key.** The fee is a function of the recipient, the amount and the comment, so an
        ///   answer already had for those three is reused rather than asked for again. That is what
        ///   stops the wallet state updating - a balance change, a gasless quota, the exchange
        ///   rates arriving - from costing a request each: none of them move the key, and
        ///   UpdateWalletState reaches this twice every time.
        /// - **The one in flight.** A repeat of a question already being asked waits for the
        ///   answer to that one instead of asking it again.
        /// - **A delay**, so that typing an amount prices it once rather than once a digit.
        ///
        /// The line is left standing while a new estimate is on its way. A TON fee does not scale
        /// with the amount - it is the cost of the message, not of what is in it - so the number on
        /// screen is almost always the right one already, and blanking it on every keystroke would
        /// be a flicker that buys nothing.
        /// </remarks>
        private async void UpdateFeeAsync()
        {
            var nanograms = Nanograms();

            if (_gasless is { LeftCount: > 0 } || string.IsNullOrEmpty(_address) || nanograms <= BigInteger.Zero)
            {
                // Supersedes whatever is in flight: an estimate landing after the field was
                // cleared would put a fee back under an amount that is no longer there.
                _feeGeneration++;
                _feePending = null;

                FeeLabel.Text = string.Empty;
                FeeLabel.Visibility = Visibility.Collapsed;
                return;
            }

            var key = (Address: _address, Nanograms: nanograms, Comment: _comment, IsCommentPublic: _isCommentPublic);

            if (_feePriced is { } priced && priced == key)
            {
                ShowFee(_feeValue);
                return;
            }

            if (_feePending is { } pending && pending == key)
            {
                return;
            }

            var generation = ++_feeGeneration;
            _feePending = key;

            await Task.Delay(FeeDebounceMilliseconds);

            if (generation != _feeGeneration)
            {
                return;
            }

            var fee = await _wallet.EstimateFeeAsync(key.Address, key.Nanograms, key.Comment, key.IsCommentPublic);

            if (generation != _feeGeneration)
            {
                return;
            }

            _feePending = null;

            if (fee is BigInteger value)
            {
                _feePriced = key;
                _feeValue = value;

                ShowFee(value);

                // The fee may be what puts the amount over the balance.
                if (!_sending)
                {
                    UpdateValidation(Nanograms());
                }
            }
        }

        private void ShowFee(BigInteger fee)
        {
            FeeLabel.Text = string.Format(Strings.WalletNetworkFeeAmount, Formatter.Grams(fee));
            FeeLabel.Visibility = Visibility.Visible;
        }

        private const int FeeDebounceMilliseconds = 500;

        private int _feeGeneration;

        // What the fee on screen is the fee for, and what is being asked about right now. Null
        // means nothing has been priced, or nothing is in flight.
        private (string Address, BigInteger Nanograms, string Comment, bool IsCommentPublic)? _feePriced;
        private (string Address, BigInteger Nanograms, string Comment, bool IsCommentPublic)? _feePending;

        private BigInteger _feeValue;

        #endregion

        #region Comment

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(MenuItemTopUp, Strings.WalletDepositFunds, Icons.AddCircle);
            flyout.CreateFlyoutItem(MenuItemComment, Strings.WalletAddComment, Icons.ChatEmpty);

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private async void MenuItemComment()
        {
            var popup = new InputPopup
            {
                Title = Strings.WalletAddComment,
                PlaceholderText = Strings.WalletCommentOptionalMessage,
                Text = _comment ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                CheckBoxText = Strings.WalletMakeCommentPublic,
                IsChecked = _isCommentPublic || _canEncryptComment == false,
                IsCheckBoxEnabled = _canEncryptComment != false,
                MinLength = 0,
                PrimaryButtonText = Strings.Add,
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

            UpdateFeeAsync();
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
            if (recipient == null && !string.IsNullOrEmpty(_address) && !WalletHelper.IsValidAddress(_address))
            {
                // Typed or from a link, and not an address at all: said as such, rather than as a
                // transfer that failed.
                _sending = false;

                IsPrimaryButtonPending = false;
                args.Cancel = true;

                deferral.Complete();

                _ = MessagePopup.ShowNestedAsync(XamlRoot, Strings.WalletInvalidAddress, Strings.WalletSendGrams, Strings.OK);
                return;
            }
            else if (recipient != null || string.IsNullOrEmpty(_address))
            {
                // Nothing left, and nothing spent: the amount is still on screen for a second try,
                // so the popup stays.
                _sending = false;

                IsPrimaryButtonPending = false;
                args.Cancel = true;

                deferral.Complete();

                if (recipient != null)
                {
                    Logger.Error("wallet recipient could not be resolved: " + recipient.Message);
                }

                _ = MessagePopup.ShowNestedAsync(XamlRoot, Strings.WalletRecipientUnavailable, Strings.WalletSendGrams, Strings.OK);
                return;
            }

            // The comment is what the prompt shows, so the user sees which spend they confirm.
            using var lease = _wallet.CreateLease(_navigationService, _comment);

            if (!await WalletHelper.EnsureBoundAsync(_clientService, _wallet, _navigationService, lease))
            {
                // They were asked for a password or a phrase and said no. The amount they typed is
                // still here, so the popup is too.
                _sending = false;

                IsPrimaryButtonPending = false;
                args.Cancel = true;

                deferral.Complete();
                return;
            }

            // Before the transfer, because the row it becomes can be bound before this resumes.
            var flight = WalletTransferFlight.Expect(XamlRoot, _address, (long)nanograms);

            try
            {
                var result = await _wallet.SendAsync(_address, _publicKey, _userId, _domain, nanograms, _comment, _isCommentPublic, _gasless is { LeftCount: > 0 }, flight.SendingId, lease);
                if (result.IsCommentUnavailable)
                {
                    flight.Cancel();

                    // Nothing was signed and nothing was spent, and what to do about it is on this
                    // screen: the comment can be made public, or taken off. So the popup stays, with
                    // the amount still in it and the button armed again.
                    _sending = false;

                    IsPrimaryButtonPending = false;
                    args.Cancel = true;

                    deferral.Complete();

                    _ = MessagePopup.ShowNestedAsync(XamlRoot, Strings.WalletPrivateCommentUnavailable, Strings.WalletSendGrams, Strings.OK);
                    return;
                }

                Result = result;

                flight.Launch(Diamond);

                // Completing the deferral is what closes the popup: the button click it belongs to
                // has been waiting for this. What becomes of the transfer is the history's to show.
                deferral.Complete();
                return;
            }
            catch (WalletTransferInProgressException)
            {
                flight.Cancel();

                // Nothing was signed, so the popup stays armed with the amount in it.
                _sending = false;

                IsPrimaryButtonPending = false;
                args.Cancel = true;

                deferral.Complete();

                _ = MessagePopup.ShowNestedAsync(XamlRoot, Strings.WalletTransferInProgress, Strings.WalletSendGrams, Strings.OK);
                return;
            }
            catch (WalletAccessDeniedException)
            {
                flight.Cancel();

                // Asked to authorize the spend and declined, before anything was signed. That is an
                // answer, so it gets no message, and the popup stays armed like the refusal above.
                _sending = false;

                IsPrimaryButtonPending = false;
                args.Cancel = true;

                deferral.Complete();
                return;
            }
            catch (Exception ex)
            {
                flight.Cancel();

                // The engine's own failures still arrive this way: nothing was signed, so nothing
                // was sent, and there is no message from the server to show.
                Logger.Error("wallet transfer failed: " + ex.Message);
            }

            // Still closed, and still only sendable once: retrying means opening the popup again,
            // which is a deliberate act, where leaving this one armed would put a second transfer of
            // real money one stray click away with no way to be sure the first did not leave.
            IsPrimaryButtonPending = false;
            deferral.Complete();

            _ = MessagePopup.ShowNestedAsync(XamlRoot, Strings.WalletTransferFailed, Strings.WalletSendGrams, Strings.OK);
        }

        /// <summary>
        /// Makes sure there is somewhere to send to, and answers with what went wrong if there is
        /// not.
        /// </summary>
        /// <remarks>
        /// The lookup that ran when the popup opened has usually settled this already, the
        /// recipient's wallet created along the way. What is left is the case where it failed:
        /// it is asked once more rather than failing on the same answer.
        /// </remarks>
        private async Task<Error> ResolveRecipientAsync()
        {
            if (!string.IsNullOrEmpty(_address))
            {
                return null;
            }

            var response = await (_lookup ??= LookupRecipientAsync());
            if (string.IsNullOrEmpty(_address) && _userId != 0)
            {
                response = await (_lookup = LookupRecipientAsync());
            }

            if (!string.IsNullOrEmpty(_address) || _userId == 0)
            {
                return null;
            }

            // Null with nothing to send to is the caller's cue that it failed without the account
            // saying why, which is the one case there are no words for but ours.
            return response as Error;
        }

        /// <summary>
        /// What was sent, once something was. Null means the popup was dismissed.
        /// </summary>
        public WalletTransferResult Result { get; private set; }
    }
}
