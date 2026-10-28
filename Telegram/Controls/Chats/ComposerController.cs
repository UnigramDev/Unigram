//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.RichEditor;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Chats
{
    /// <summary>
    /// What makes a text box the chat's composer: sending and scheduling, the inline bot, the
    /// mention, hashtag and command lists under it, the typing indicator, and the keys that
    /// reach into the chat (Up to edit the last message, Ctrl+Up and Ctrl+Down to reply). It
    /// works on any <see cref="IFormattedTextInput"/>, so the composer is the same over RichEdit
    /// and over the native editor; what is left in a box is only how it edits text.
    /// </summary>
    public sealed partial class ComposerController
    {
        private readonly IFormattedTextInput _input;
        private readonly UIElement _element;

        private DialogViewModel _viewModel;

        public ComposerController(IFormattedTextInput input, UIElement element)
        {
            _input = input;
            _element = element;

            _input.SelectionChanged += OnSelectionChanged;
            _input.ContentChanged += OnContentChanged;
            _input.Submitted += OnSubmitted;
            _input.PasteRequested += OnPasteRequested;

            // Tunnelling, so it comes before either box's own keys: Enter and Tab pick from the
            // list here before a box sends or moves focus with them.
            _element.PreviewKeyDown += OnPreviewKeyDown;
        }

        /// <summary>The chat the composer is for. Null while the page is not showing one, which cancels what is pending.</summary>
        public DialogViewModel ViewModel
        {
            get => _viewModel;
            set
            {
                if (_viewModel != value)
                {
                    CancelInlineBotToken();
                    CancelOffer();

                    _viewModel = value;
                    _isMenuExpanded = false;
                    _wasEmpty = true;
                }
            }
        }

        /// <summary>The list the mentions, hashtags and commands are shown in, which the arrows move through.</summary>
        public ListViewBase ListAutocomplete { get; set; }

        /// <summary>The message effect sent with the text.</summary>
        public MessageEffect Effect { get; set; }

        public void Detach()
        {
            ViewModel = null;

            _input.SelectionChanged -= OnSelectionChanged;
            _input.ContentChanged -= OnContentChanged;
            _input.Submitted -= OnSubmitted;
            _input.PasteRequested -= OnPasteRequested;
            _element.PreviewKeyDown -= OnPreviewKeyDown;
        }

        // =====================================================================================
        // Sending
        // =====================================================================================

        private void OnSubmitted(object sender, EventArgs e)
        {
            Send();
        }

        public async void Send(bool disableNotification = false)
        {
            var viewModel = _viewModel;
            if (viewModel == null)
            {
                return;
            }

            var plain = GetPlainText();

            // This needs to run before text empty check as it cleans up
            // some stuff if inline bot isn't found
            if (SearchInlineBotResults(plain, false, out string inlineQuery))
            {
                if (string.IsNullOrEmpty(inlineQuery))
                {
                    _input.SetText(null);
                }
                else
                {
                    var username = plain.Split(' ')[0] + " ";

                    _input.SetText(new FormattedText(username, new TextEntity[0]));
                    _input.MoveCaretToEnd();
                }

                return;
            }
            else if (viewModel.Type == DialogType.ScheduledMessages && viewModel.ComposerHeader?.Editing == null)
            {
                Schedule(false);
                return;
            }

            // Read, not consumed: how many messages the text splits into is part of what the user
            // is asked to confirm, and nothing may disappear from the field while they decide.
            var text = _input.GetPreparedText();

            await viewModel.SendComposerTextAsync(text, SchedulingState.Auto, disableNotification, Effect?.Id ?? 0);
        }

        public async void Schedule(bool whenOnline)
        {
            var viewModel = _viewModel;
            if (viewModel == null)
            {
                return;
            }

            var text = _input.GetPreparedText();

            await viewModel.SendComposerTextAsync(text, whenOnline ? SchedulingState.WhenOnline : SchedulingState.Schedule, null, Effect?.Id ?? 0);
        }

        // Without the box's hidden payloads, which are what its own Text would include.
        private string GetPlainText()
        {
            return _input.IsEmpty ? string.Empty : _input.GetFormattedText(false, false).Text ?? string.Empty;
        }

        // =====================================================================================
        // Typing
        // =====================================================================================

        private ulong _lastKeystroke;
        private bool _wasEmpty = true;

        private void OnContentChanged(object sender, EventArgs e)
        {
            if (_pendingOffer != null || _shownOffer != null)
            {
                UpdateOffer();
            }

            if (_input.IsEmpty)
            {
                _wasEmpty = true;
                return;
            }

            // Milliseconds since boot rather than the clock: this measures an interval, and
            // DateTime.Now both resolves the time zone on every call and can jump backwards
            // on a clock correction, which would leave the indicator silent until it caught up.
            var timestamp = Logger.TickCount;

            if (timestamp - _lastKeystroke > 4000 || _wasEmpty)
            {
                _lastKeystroke = timestamp;
                _viewModel?.ChatActionManager.SetTyping(new ChatActionTyping());
            }

            _wasEmpty = false;
        }

        // =====================================================================================
        // Paste
        // =====================================================================================

        // Taken here synchronously, so the box does not paste the text as well, and finished
        // once the clipboard has been read.
        private void OnPasteRequested(object sender, FormattedTextPasteEventArgs e)
        {
            var viewModel = _viewModel;
            if (viewModel == null)
            {
                return;
            }

            var formats = e.Package.AvailableFormats;

            if (formats.Contains(StandardDataFormats.Bitmap) || formats.Contains(StandardDataFormats.StorageItems))
            {
                e.Handled = true;
                PasteMedia(viewModel, e.Package);
            }
            else if (formats.Contains(RichClipboard.FormatId))
            {
                e.Handled = true;
                PasteRich(viewModel, e.Package);
            }
            else if (formats.Contains(StandardDataFormats.Html))
            {
                // The box pastes the text it reads as; all this adds is the offer.
                OfferRich(viewModel, e.Package);
            }
        }

        private async void PasteMedia(DialogViewModel viewModel, DataPackageView package)
        {
            try
            {
                await viewModel.HandlePackageAsync(package);
            }
            catch
            {
                // The clipboard changed, or its source went away, while being read.
            }
        }

        /// <summary>
        /// A copy out of a rich message: its text and entities when a message can say all of it,
        /// as any message's copy pastes, and otherwise its plain text and the offer to restore
        /// the rest in the rich editor.
        /// </summary>
        private async void PasteRich(DialogViewModel viewModel, DataPackageView package)
        {
            var offer = CreateOffer(viewModel);

            try
            {
                var blocks = await GetRichBlocksAsync(package);
                if (blocks == null)
                {
                    _input.InsertFormattedText(await MessageHelper.PasteTextAsync(package));
                    return;
                }

                _input.InsertText(await package.GetTextAsync());

                if (offer != null)
                {
                    offer.Blocks = blocks;
                    Offer(offer);
                }
            }
            catch
            {
                // The clipboard changed, or its source went away, while being read.
            }
        }

        private async void OfferRich(DialogViewModel viewModel, DataPackageView package)
        {
            var offer = CreateOffer(viewModel);
            if (offer != null && await GetRichBlocksAsync(package) is Vector<PageBlock> blocks)
            {
                offer.Blocks = blocks;
                Offer(offer);
            }
        }

        /// <summary>The clipboard's HTML as page blocks when it says more than a message's text and entities can; null otherwise.</summary>
        private static async Task<Vector<PageBlock>> GetRichBlocksAsync(DataPackageView package)
        {
            try
            {
                // CF_HTML wraps the markup in a header of byte offsets and a source URL.
                var blocks = RichHtml.Parse(HtmlFormatHelper.GetStaticFragment(await package.GetHtmlFormatAsync()));

                if (blocks.Count > 0 && !PageBlockHelper.TryGetFormattedText(new RichMessage(blocks, false, true), out _))
                {
                    return blocks;
                }
            }
            catch
            {
                // The clipboard changed, or its source went away, while being read.
            }

            return null;
        }

        // =====================================================================================
        // Rich paste offer
        // =====================================================================================

        private sealed class RichPasteOffer
        {
            public RichPasteOffer(DialogViewModel viewModel, string text, FormattedText was, FormattedText before, FormattedText after)
            {
                ViewModel = viewModel;
                Text = text;
                Was = was;
                Before = before;
                After = after;
            }

            public DialogViewModel ViewModel { get; }

            /// <summary>The field's text before the paste, and the same with its formatting, markdown unparsed.</summary>
            public string Text { get; }

            public FormattedText Was { get; }

            /// <summary>The field around the selection the paste replaced, as the editor gets it.</summary>
            public FormattedText Before { get; }

            public FormattedText After { get; }

            public Vector<PageBlock> Blocks { get; set; }

            /// <summary>The field's text once the paste landed; anything else and the toast goes.</summary>
            public string Pasted { get; set; }

            public ulong Expires { get; set; }

            public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();
        }

        // On screen for 9 seconds, and offered at most once in 30.
        private const int OfferDuration = 9;
        private const ulong OfferInterval = 30000;

        private ulong _lastOffer;
        private RichPasteOffer _pendingOffer;
        private RichPasteOffer _shownOffer;

        private bool IsOfferThrottled => _lastOffer != 0 && Logger.TickCount - _lastOffer < OfferInterval;

        // Taken before the paste lands: the offer, taken up, puts the field back as it was.
        private RichPasteOffer CreateOffer(DialogViewModel viewModel)
        {
            // The editor sends a new message, so an edit stays in the field.
            if (viewModel.ComposerHeader?.Editing != null || IsOfferThrottled)
            {
                return null;
            }

            if (!viewModel.IsPremium && (!viewModel.IsPremiumAvailable || viewModel.Settings.HideRichPasteToast))
            {
                return null;
            }

            _input.SplitAtSelection(out FormattedText before, out FormattedText after);
            return new RichPasteOffer(viewModel, _input.Text, _input.GetFormattedText(false, false), before, after);
        }

        // The box reads the clipboard as this does, so its paste may not have landed yet: the
        // offer then waits for it, though not for long, in case the paste changed nothing.
        private void Offer(RichPasteOffer offer)
        {
            if (_input.Text == offer.Text)
            {
                offer.Expires = Logger.TickCount + 2000;
                _pendingOffer = offer;
            }
            else
            {
                ShowOffer(offer);
            }
        }

        private void UpdateOffer()
        {
            if (_pendingOffer is RichPasteOffer pending)
            {
                _pendingOffer = null;

                if (Logger.TickCount < pending.Expires)
                {
                    ShowOffer(pending);
                }
            }
            else if (_shownOffer is RichPasteOffer shown && _input.Text != shown.Pasted)
            {
                shown.Cancellation.Cancel();
            }
        }

        private void CancelOffer()
        {
            _pendingOffer = null;
            _shownOffer?.Cancellation.Cancel();
        }

        private async void ShowOffer(RichPasteOffer offer)
        {
            var viewModel = _viewModel;
            if (viewModel != offer.ViewModel || IsOfferThrottled)
            {
                return;
            }

            _lastOffer = Logger.TickCount;
            _shownOffer = offer;
            offer.Pasted = _input.Text;

            var locked = !viewModel.IsPremium;

            TextBlock label;
            if (locked)
            {
                label = ToastPopup.CreatePromoLabel(viewModel.NavigationService, Strings.RichPasteToastPremium, new PremiumFeatureRichMessages());
            }
            else
            {
                label = new TextBlock { TextWrapping = TextWrapping.Wrap };
                label.Inlines.Add(new Run { Text = Strings.RichPasteToastEditor });
            }

            label.Inlines.Insert(0, new LineBreak());
            label.Inlines.Insert(0, new Run { Text = Strings.RichPasteToast, FontWeight = FontWeights.SemiBold });

            var icon = locked ? ToastPopup.CreateActionIcon(ToastPopupIcon.Premium) : null;
            var confirm = await ToastPopup.ShowActionAsync(_element.XamlRoot, label, locked ? Strings.GotIt : Strings.Open, icon, ToastPlacementMode.Center,
                dismissAfter: TimeSpan.FromSeconds(OfferDuration), cancellationToken: offer.Cancellation.Token);

            if (_shownOffer == offer)
            {
                _shownOffer = null;
            }

            offer.Cancellation.Dispose();

            if (confirm != ContentDialogResult.Primary || _viewModel != viewModel)
            {
                return;
            }

            if (locked)
            {
                viewModel.Settings.HideRichPasteToast = true;
                return;
            }

            // The toast closes on any change, but the field may have been set from outside.
            if (_input.Text == offer.Pasted)
            {
                _input.SetText(offer.Was);
            }

            viewModel.PasteRichMessage(offer.Before, offer.Blocks, offer.After);
        }

        // =====================================================================================
        // Keys
        // =====================================================================================

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var viewModel = _viewModel;

            // 229 is a key the IME is consuming; the box's own suggestions take their keys first.
            if (viewModel == null || e.Key == (VirtualKey)229 || _input.HasOpenSuggestions)
            {
                return;
            }

            var modifiers = WindowContext.KeyModifiers();
            var autocomplete = viewModel.Autocomplete;
            var list = ListAutocomplete;

            switch (e.Key)
            {
                case VirtualKey.Up when modifiers == VirtualKeyModifiers.Control:
                    viewModel.MessageReplyPrevious();
                    e.Handled = true;
                    break;

                case VirtualKey.Down when modifiers == VirtualKeyModifiers.Control:
                    viewModel.MessageReplyNext();
                    e.Handled = true;
                    break;

                case VirtualKey.Up or VirtualKey.Down when modifiers == VirtualKeyModifiers.None:
                    if (autocomplete == null)
                    {
                        if (e.Key == VirtualKey.Up && _input.IsEmpty)
                        {
                            viewModel.EditLastMessage();
                            e.Handled = true;
                        }
                    }
                    else if (list != null && list.Items.Count > 0 && autocomplete.Orientation == Orientation.Vertical)
                    {
                        list.SelectionMode = ListViewSelectionMode.Single;

                        var next = list.SelectedIndex + (e.Key == VirtualKey.Up ? -1 : 1);
                        if (next >= 0 && next < autocomplete.Count)
                        {
                            list.SelectedIndex = next;
                            list.ScrollIntoView(list.SelectedItem);
                        }

                        e.Handled = true;
                    }
                    break;

                case VirtualKey.Left or VirtualKey.Right when modifiers == VirtualKeyModifiers.None:
                    if (autocomplete != null && list != null && list.Items.Count > 0 && autocomplete.Orientation == Orientation.Horizontal)
                    {
                        e.Handled = MoveHorizontally(list, autocomplete, e.Key == VirtualKey.Right);
                    }
                    break;

                case VirtualKey.Tab or VirtualKey.Enter when modifiers != VirtualKeyModifiers.Shift
                    && autocomplete != null && list != null && list.Items.Count > 0
                    && (autocomplete.InsertOnKeyDown || list.SelectedItem != null):
                    {
                        // Invoked as a click would be, so the page's own handler picks it.
                        if (list.ContainerFromIndex(Math.Max(0, list.SelectedIndex)) is GridViewItem container
                            && new GridViewItemAutomationPeer(container).GetPattern(PatternInterface.Invoke) is IInvokeProvider provider)
                        {
                            provider.Invoke();
                        }

                        e.Handled = true;
                        break;
                    }

                case VirtualKey.Enter:
                    // Asked now rather than kept in step: whether a bot is being typed to
                    // changes from the view model as well as from here.
                    _input.IsSubmitEnabled = viewModel.CurrentInlineBot == null;
                    break;
            }
        }

        internal static bool MoveHorizontally(ListViewBase list, IAutocompleteCollection autocomplete, bool forward)
        {
            if (list.SelectedIndex == 0 && !forward)
            {
                list.SelectedIndex = -1;
                return true;
            }
            else if (list.SelectedIndex == list.Items.Count - 1 && forward)
            {
                list.SelectedIndex = 0;
                return true;
            }

            list.SelectionMode = ListViewSelectionMode.Single;

            var next = list.SelectedIndex + (forward ? 1 : -1);
            if (next >= 0 && next < autocomplete.Count)
            {
                list.SelectedIndex = next;
                list.ScrollIntoView(list.SelectedItem);
                return true;
            }

            return false;
        }

        // =====================================================================================
        // The lists
        // =====================================================================================

        private bool _isMenuExpanded;
        public bool IsMenuExpanded
        {
            get => _isMenuExpanded;
            set
            {
                if (_viewModel?.Chat?.Type is not ChatTypePrivate && value)
                {
                    return;
                }

                _isMenuExpanded = value;
                UpdateAutocomplete();
            }
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            UpdateAutocomplete();
        }

        private async void UpdateAutocomplete()
        {
            var viewModel = _viewModel;
            if (viewModel == null)
            {
                return;
            }

            if (_isMenuExpanded)
            {
                if (viewModel.Autocomplete is not AutocompleteList)
                {
                    ClearInlineBotResults();
                    SetAutocomplete(GetCommands(string.Empty));
                }

                return;
            }

            // This runs on every keystroke and every caret move, and the draft is the largest
            // thing here, so only the branches that need it materialize it. Every path below
            // still ends at ClearInlineBotResults, which is what the search did when it found
            // nothing.
            if (viewModel.CurrentInlineBot != null && SearchInlineBotResults(GetPlainText(), true, out string inlineQuery))
            {
                SetAutocomplete(null);
                GetInlineBotResults(inlineQuery);
                return;
            }

            if (_input.IsEmpty || !_input.IsSelectionEmpty)
            {
                ClearInlineBotResults();
                SetAutocomplete(null);
                return;
            }

            var prev = viewModel.Autocomplete;
            if (prev is AutocompleteCollection collection)
            {
                prev = collection.Source;
            }

            if (TryGetAutocomplete(viewModel, prev, out var autocomplete, out bool recycle))
            {
                ClearInlineBotResults();
                SetAutocomplete(autocomplete, recycle);
                return;
            }

            SetAutocomplete(null);
            CancelInlineBotToken();

            var token = (_inlineBotToken = new CancellationTokenSource()).Token;
            var text = GetPlainText();

            // SearchByInlineBot only matches a username that starts the message
            if (text.Length > 0 && text[0] == '@')
            {
                var query = text.Substring(0, Math.Min(_input.CaretOffset, text.Length));

                if (SearchByInlineBot(query, out string username, out _) && await viewModel.ResolveInlineBotAsync(username, token))
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (SearchInlineBotResults(text, true, out query))
                    {
                        SetAutocomplete(null);
                        GetInlineBotResults(query);
                        return;
                    }
                }
            }

            ClearInlineBotResults();
        }

        /// <summary>
        /// The list for what is being typed. True with no list for an emoji query: the box shows
        /// those itself, and they still close the inline bot.
        /// </summary>
        private bool TryGetAutocomplete(DialogViewModel viewModel, IAutocompleteCollection prev, out IAutocompleteCollection autocomplete, out bool recycle)
        {
            autocomplete = null;
            recycle = false;

            if (!_input.TryGetQuery(out AutocompleteEntity entity, out string result, out int index))
            {
                return false;
            }

            var chat = viewModel.Chat;
            if (chat == null)
            {
                return false;
            }

            if (entity == AutocompleteEntity.Username)
            {
                if (prev is ChatTextBox.UsernameCollection && prev.Query.Equals(result))
                {
                    autocomplete = prev;
                    return true;
                }

                var guestBots = chat.Type is not ChatTypeSecret;
                var members = chat.Type is /*ChatTypePrivate or ChatTypeSecret or*/ ChatTypeBasicGroup or ChatTypeSupergroup { IsChannel: false };

                autocomplete = new ChatTextBox.UsernameCollection(viewModel.ClientService, chat.Id, viewModel.TopicId, result, index == 0, guestBots, members, false);
                recycle = prev is ChatTextBox.UsernameCollection;
                return true;
            }
            else if (entity == AutocompleteEntity.Hashtag)
            {
                if (prev is ChatTextBox.SearchHashtagsCollection && prev.Query.Equals(result))
                {
                    autocomplete = prev;
                    return true;
                }

                autocomplete = new ChatTextBox.SearchHashtagsCollection(viewModel.ClientService, result);
                recycle = prev is ChatTextBox.SearchHashtagsCollection;
                return true;
            }
            else if (entity == AutocompleteEntity.Sticker)
            {
                // A message that is a single emoji is also a query for stickers to send instead.
                if (index == 0 && viewModel.ComposerHeader?.Editing == null && chat.CanSendOtherMessages(viewModel.ClientService))
                {
                    var text = GetPlainText().Trim();

                    if (prev is SearchStickersCollection stickers && !stickers.IsCustomEmoji && prev.Query.Equals(text))
                    {
                        autocomplete = prev;
                        return true;
                    }

                    autocomplete = new SearchStickersCollection(viewModel.ClientService, false, text, chat.Id);
                }

                return true;
            }
            else if (entity == AutocompleteEntity.Emoji)
            {
                return true;
            }
            else if (entity == AutocompleteEntity.Command && index == 0)
            {
                if (prev is AutocompleteList && prev.Query.Equals(result))
                {
                    autocomplete = prev;
                    return true;
                }

                autocomplete = GetCommands(result);
                recycle = prev is AutocompleteList;
                return true;
            }

            return false;
        }

        private void SetAutocomplete(IAutocompleteCollection collection, bool recycle = false)
        {
            var viewModel = _viewModel;

            if (collection == null)
            {
                viewModel.Autocomplete = null;
            }
            else if (viewModel.Autocomplete is not AutocompleteCollection autocomplete)
            {
                viewModel.Autocomplete = new AutocompleteCollection(collection);
            }
            else if (autocomplete.Source != collection)
            {
                if (recycle)
                {
                    autocomplete.Update(collection);
                }
                else
                {
                    viewModel.Autocomplete = new AutocompleteCollection(collection);
                }
            }
        }

        private AutocompleteList GetCommands(string command)
        {
            var viewModel = _viewModel;

            var all = viewModel.BotCommands;
            if (all != null)
            {
                var results = new AutocompleteList(command, all.Where(x => x.Command.StartsWith(command, StringComparison.OrdinalIgnoreCase)));
                if (results.Count > 0)
                {
                    return results;
                }
            }
            else if (viewModel.ClientService.TryGetUser(viewModel.Chat, out var user) && user.Type is UserTypeRegular)
            {
                viewModel.ClientService.LoadQuickReplyShortcuts();

                var replies = viewModel.ClientService.GetQuickReplyShortcuts();

                var results = new AutocompleteList(command, replies.Where(x => x.Name.StartsWith(command, StringComparison.OrdinalIgnoreCase)));
                if (results.Count > 0)
                {
                    return results;
                }
            }

            return null;
        }

        /// <summary>
        /// An item picked from the list: a mention, a hashtag or a command completes what is
        /// typed; a command without Tab, a sticker or a quick reply is sent at once.
        /// </summary>
        public void Accept(object item)
        {
            var viewModel = _viewModel;
            var chat = viewModel?.Chat;
            if (chat == null)
            {
                return;
            }

            var found = _input.TryGetQuery(out AutocompleteEntity entity, out string result, out int index);

            if (item is User user && found && entity == AutocompleteEntity.Username)
            {
                var username = user.ActiveUsername(result);

                if (string.IsNullOrEmpty(username))
                {
                    var name = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                    if (FormattedTextBox.IsUnsafe(name))
                    {
                        name = Strings.Username;
                    }

                    _input.ReplaceQuery(index, new FormattedText(name + " ", new[] { new TextEntity(0, name.Length, new TextEntityTypeMentionName(user.Id)) }));
                }
                else
                {
                    _input.ReplaceQuery(index, new FormattedText($"@{username} ", new TextEntity[0]));
                }

                if (index == 0 && user.Type is UserTypeBot bot && bot.IsInline)
                {
                    viewModel.ResolveInlineBot(username);
                }
            }
            else if (item is BotCommandFullInfo command)
            {
                var insert = $"/{command.Command}";
                if (chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup)
                {
                    var bot = viewModel.ClientService.GetUser(command.BotUserId);
                    if (bot != null && bot.HasActiveUsername(out string username))
                    {
                        insert += $"@{username}";
                    }
                }

                if (WindowContext.IsKeyDown(VirtualKey.Tab) && found && entity == AutocompleteEntity.Command)
                {
                    _input.ReplaceQuery(index, new FormattedText($"{insert} ", new TextEntity[0]));
                }
                else
                {
                    _input.ClearText();
                    viewModel.SendMessage(insert);
                }

                IsMenuExpanded = false;
            }
            else if (item is string hashtag && found && entity == AutocompleteEntity.Hashtag)
            {
                _input.ReplaceQuery(index, new FormattedText($"{hashtag} ", new TextEntity[0]));
            }
            else if (item is Sticker sticker)
            {
                _input.ClearText();
                viewModel.SendSticker(sticker, SchedulingState.Auto, null, result);
            }
            else if (item is QuickReplyShortcut shortcut)
            {
                _input.ClearText();

                if (viewModel.IsPremium)
                {
                    viewModel.ClientService.Send(new SendQuickReplyShortcutMessages(chat.Id, shortcut.Id, 0));
                }
                else
                {
                    viewModel.NavigationService.ShowPromo(new PremiumFeatureBusiness());
                }
            }
        }

        // =====================================================================================
        // Inline bots
        // =====================================================================================

        private CancellationTokenSource _inlineBotToken;

        private void CancelInlineBotToken()
        {
            if (_inlineBotToken != null)
            {
                _inlineBotToken.Cancel();
                _inlineBotToken.Dispose();
                _inlineBotToken = null;
            }
        }

        private void GetInlineBotResults(string inlineQuery)
        {
            var viewModel = _viewModel;
            if (viewModel.InlineBotResults != null && string.Equals(inlineQuery, viewModel.InlineBotResults.Query, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CancelInlineBotToken();

            _inlineBotToken = new CancellationTokenSource();
            viewModel.GetInlineBotResults(inlineQuery, _inlineBotToken.Token);
        }

        private void ClearInlineBotResults()
        {
            CancelInlineBotToken();

            var viewModel = _viewModel;
            if (viewModel != null)
            {
                viewModel.CurrentInlineBot = null;
                viewModel.InlineBotResults = null;
                viewModel.IsInlineBotResultsLoading = false;
            }

            _input.SetInlinePlaceholder(null, null);
        }

        public static bool SearchByInlineBot(string text, out string searchText, out int index)
        {
            index = -1;
            searchText = string.Empty;

            var found = true;
            var i = 0;

            while (i < text.Length)
            {
                if (i == 0 && text[i] != '@')
                {
                    found = false;
                    break;
                }
                else if (text[i] == ' ')
                {
                    index = i;
                    break;
                }
                else if (text[i] == '@')
                {
                    i++;
                }
                else
                {
                    if (!MessageHelper.IsValidUsernameSymbol(text[i]))
                    {
                        found = false;
                        break;
                    }

                    i++;
                }
            }

            if (found)
            {
                if (index == -1)
                {
                    return false;
                }

                searchText = text.Substring(0, index).TrimStart('@');
            }

            return found;
        }

        private bool SearchInlineBotResults(string text, bool apply, out string searchText)
        {
            var flag = false;
            searchText = string.Empty;

            // This runs on every keystroke, and without an inline bot to address none of the
            // string work below can lead anywhere, so it's not worth an allocation.
            var inlineBot = _viewModel?.CurrentInlineBot;
            if (inlineBot == null)
            {
                if (apply)
                {
                    ClearInlineBotResults();
                }

                return false;
            }

            if (text.EndsWith('\r'))
            {
                text = text.Substring(0, text.Length - 1);
            }

            if (text.StartsWith('@'))
            {
                text = text.Substring(1);
            }

            var space = text.IndexOf(' ');
            var first = space < 0 ? text : text.Substring(0, space);

            if (inlineBot.HasActiveUsername(first, out string username))
            {
                searchText = ReplaceFirst(text.TrimStart(), username, string.Empty);
                if (searchText.StartsWith(" "))
                {
                    searchText = ReplaceFirst(searchText, " ", string.Empty);
                    flag = true;
                }

                if (apply)
                {
                    var placeholder = inlineBot.Type is UserTypeBot bot ? bot.InlineQueryPlaceholder : null;

                    if (!flag)
                    {
                        if (string.Equals(text.TrimStart(), "@" + username, StringComparison.OrdinalIgnoreCase))
                        {
                            ClearInlineBotResults();
                        }
                        else if (placeholder != null)
                        {
                            _input.SetInlinePlaceholder("@" + username + " ", placeholder);
                        }
                    }
                    else if (string.IsNullOrEmpty(searchText) && placeholder != null)
                    {
                        _input.SetInlinePlaceholder("@" + username + " ", placeholder);
                    }
                    else
                    {
                        _input.SetInlinePlaceholder(null, null);
                    }
                }
            }
            else if (apply)
            {
                ClearInlineBotResults();
            }

            searchText = searchText.TrimEnd('\r', '\n');
            return flag;
        }

        private static string ReplaceFirst(string text, string search, string replace)
        {
            var index = text.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return text;
            }

            return text.Substring(0, index) + replace + text.Substring(index + search.Length);
        }
    }
}
