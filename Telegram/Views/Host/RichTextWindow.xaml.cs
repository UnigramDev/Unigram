//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Controls.RichEditor;
using Telegram.Entities;
using Windows.Storage;
using Windows.Storage.Pickers;
using Telegram.Converters;
using Telegram.Native.Highlight;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Popups;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Host
{
    public sealed partial class RichTextWindow : WindowContent
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly long _chatId;
        private readonly MessageTopic _topic;
        private readonly long _messageId;

        private readonly RichMessage _message;

        // Reply + send options for the outgoing message (a new send; ignored when editing, i.e. _messageId != 0).
        private readonly InputMessageReplyTo _replyTo;
        private readonly MessageSendOptions _sendOptions;

        private bool _closedExpected;

        private RichEditorEngine _engine;
        private RichEditorStatus _status;

        // Whether the document can only go as a rich message: worked out from the whole document,
        // so not on every keystroke but once typing pauses.
        private readonly DispatcherTimer _lockTimer;

        public RichTextWindow(WindowContext window, IClientService clientService, INavigationService navigationService, long chatId, MessageTopic topic, long messageId, RichMessage message, InputMessageReplyTo replyTo = null, MessageSendOptions sendOptions = null)
            : base(window)
        {
            InitializeComponent();
            InitializeGroups();

            StateLabel.Text = Constants.RELEASE
                ? Strings.AppDisplayName
                : Strings.AppName;

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(clientService.Session);

            _clientService = clientService;
            // NavigationService is window-specific: `navigationService` belongs to the originating chat
            // window, but this popup runs in its own window. Wrap it in a SecondaryNavigationService bound
            // to THIS window (forwards navigations — e.g. the premium promo — back to the source window),
            // like WebAppWindow.
            _navigationService = new SecondaryNavigationService(clientService.Session, navigationService, Window);
            _chatId = chatId;
            _topic = topic;
            _messageId = messageId;
            _message = message;
            _replyTo = replyTo;
            _sendOptions = sendOptions;

            _lockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _lockTimer.Tick += OnLockTimerTick;

            if (ApiInfo.CanCreateThemeShadow)
            {
                var shadow = new ThemeShadow();
                var translation = new Vector3(0, 0, Constants.BubbleElevation * 2);

                SendShadow.Shadow = shadow;
                SendShadow.Translation = translation;
            }

            Initialize(message);
        }

        protected override UIElement TitleBarElement => TitleBarHandle;

        public bool AreTheSame(long chatId, long messageId)
        {
            return chatId == _chatId && messageId == _messageId;
        }

        private void Initialize(RichMessage message)
        {
            // From scratch, or the message being edited: a sent message comes back with its media
            // as file ids, which is how it is sent again.
            var document = message != null
                ? RichDocumentConverter.FromRichMessage(message)
                : new RichDocument();

            _engine = new RichEditorEngine(document);

            Editor.StateChanged += OnEditorStateChanged;
            Editor.MenuRequested += OnMenuRequested;
            Editor.LanguageRequested += OnLanguageRequested;
            Editor.ButtonRequested += OnButtonRequested;
            Editor.ObjectRequested += OnObjectRequested;
            Editor.NodeActivated += OnNodeActivated;
            Editor.MediaAddRequested += OnMediaAddRequested;
            Editor.LinkRequested += OnLinkRequested;
            Editor.PasteRequested += OnPasteRequested;

            Editor.Suggestions = new RichSuggestionContext(_clientService.Session.Settings, _chatId);
            Editor.Uploads = new RichMediaUploads(Editor, _clientService);
            Editor.Initialize(_clientService, _engine);
            UpdateToolbar();
            UpdateSendLock();
            UpdatePremiumMarks();

            // Premium can change at runtime (UpdateOption "is_premium"); re-evaluate the send-button lock.
            _aggregator = _clientService.Session.Resolve<IEventAggregator>();
            _aggregator.Subscribe<UpdateOption>(this, Handle);
        }

        private IEventAggregator _aggregator;

        protected override void OnWindowClosed()
        {
            Editor.StateChanged -= OnEditorStateChanged;
            Editor.MenuRequested -= OnMenuRequested;
            Editor.LanguageRequested -= OnLanguageRequested;
            Editor.ButtonRequested -= OnButtonRequested;
            Editor.ObjectRequested -= OnObjectRequested;
            Editor.NodeActivated -= OnNodeActivated;
            Editor.MediaAddRequested -= OnMediaAddRequested;
            Editor.LinkRequested -= OnLinkRequested;
            Editor.PasteRequested -= OnPasteRequested;

            _lockTimer.Stop();
            _lockTimer.Tick -= OnLockTimerTick;

            // Sent, the message carries the uploads on; otherwise nothing will use them. A draft
            // names the files on the device, so it does not either.
            if (_mediaSent)
            {
                Editor.Uploads?.Detach();
            }
            else
            {
                Editor.Uploads?.Cancel();
            }

            _aggregator?.Unsubscribe(this);
        }

        // Closing the editor window offers to save the current text as a chat draft. Skipped when we're
        // closing after an explicit send (_closedExpected), when editing an existing message (_messageId
        // != 0 — nothing to draft), or when the document is empty.
        protected override async void OnWindowCloseRequested(WindowCloseRequestedEventArgs e)
        {
            if (_closedExpected || _messageId != 0 || (_engine?.Document.IsEmpty ?? true))
            {
                return;
            }

            var deferral = e.GetDeferral();

            var confirm = await MessagePopup.ShowAsync(XamlRoot, Strings.ArticleSaveDraftMessage, Strings.ArticleSaveDraftTitle, Strings.Save, Strings.Discard);
            if (confirm == ContentDialogResult.Primary)
            {
                SaveDraft();
            }
            else if (confirm != ContentDialogResult.Secondary)
            {
                // Dismissed — cancel the close and keep editing.
                e.Handled = true;
            }

            deferral.Complete();
        }

        private void SaveDraft()
        {
            var richMessage = GetInputModel();
            if (richMessage == null)
            {
                return;
            }

            var draft = new DraftMessage(_replyTo, 0, new DraftMessageContentInputRichMessage(richMessage), 0, null);
            _clientService.Send(new SetChatDraftMessage(_chatId, _topic, draft));
        }

        /// <summary>
        /// The document as TDLib takes it; null when there is nothing to send. With
        /// <paramref name="inputs"/>, media already uploading goes by its file id: for sending,
        /// never for a draft, which outlives the uploads.
        /// </summary>
        private InputRichMessage GetInputModel(IRichMediaInputs inputs = null)
        {
            if (_engine.Document.IsEmpty)
            {
                return null;
            }

            return RichDocumentConverter.ToInputRichMessage(_engine.Document, inputs);
        }

        // Whether what was sent took the preliminary uploads with it: a rich message does, the
        // text a free user is sent instead has no media to take.
        private bool _mediaSent;

        public void Handle(UpdateOption update)
        {
            if (update.Name == OptionsService.R.IsPremium)
            {
                this.BeginOnUIThread(OnPremiumChanged);
            }
        }

        private void OnPremiumChanged()
        {
            UpdateSendLock();
            UpdatePremiumMarks();
        }

        /// <summary>
        /// Whether what needs Premium is marked with a star. Only marked, as TDesktop does: every
        /// tool still works, and it is sending the result that asks for Premium.
        /// </summary>
        private bool IsPremiumMarked => _clientService.IsPremiumAvailable && !_clientService.IsPremium;

        private void UpdatePremiumMarks()
        {
            PremiumMarksVisibility = IsPremiumMarked ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>What every star on the toolbar binds to.</summary>
        public Visibility PremiumMarksVisibility
        {
            get => (Visibility)GetValue(PremiumMarksVisibilityProperty);
            set => SetValue(PremiumMarksVisibilityProperty, value);
        }

        public static readonly DependencyProperty PremiumMarksVisibilityProperty =
            DependencyProperty.Register("PremiumMarksVisibility", typeof(Visibility), typeof(RichTextWindow), new PropertyMetadata(Visibility.Collapsed));

        // The grouped toolbar's marks and lists, each button with what it toggles.
        private (GlyphToggleButton Button, RichMarks Mark)[] _markButtons;
        private (GlyphToggleButton Button, RichListStyle Style)[] _listButtons;

        private void InitializeGroups()
        {
            _markButtons = new[]
            {
                (GroupBoldButton, RichMarks.Bold),
                (GroupItalicButton, RichMarks.Italic),
                (GroupUnderlineButton, RichMarks.Underline),
                (GroupStrikethroughButton, RichMarks.Strikethrough),
                (GroupSpoilerButton, RichMarks.Spoiler),
                (GroupMonospaceButton, RichMarks.Monospace),
                (GroupSubscriptButton, RichMarks.Subscript),
                (GroupSuperscriptButton, RichMarks.Superscript),
                (GroupMarkedButton, RichMarks.Marked)
            };

            _listButtons = new[]
            {
                (GroupNumberedButton, RichListStyle.Ordered),
                (GroupBulletedButton, RichListStyle.Bullet),
                (GroupChecklistButton, RichListStyle.Checklist)
            };

            // With the shortcut, as the Text Style menu shows it.
            var shift = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift;

            ToolTipService.SetToolTip(GroupBoldButton, $"{Strings.Bold} ({Shortcut(VirtualKey.B)})");
            ToolTipService.SetToolTip(GroupItalicButton, $"{Strings.Italic} ({Shortcut(VirtualKey.I)})");
            ToolTipService.SetToolTip(GroupUnderlineButton, $"{Strings.Underline} ({Shortcut(VirtualKey.U)})");
            ToolTipService.SetToolTip(GroupStrikethroughButton, $"{Strings.Strike} ({Shortcut(VirtualKey.X, shift)})");
            ToolTipService.SetToolTip(GroupSpoilerButton, $"{Strings.Spoiler} ({Shortcut(VirtualKey.P, shift)})");
            ToolTipService.SetToolTip(GroupMonospaceButton, $"{Strings.Mono} ({Shortcut(VirtualKey.M, shift)})");
            ToolTipService.SetToolTip(GroupRegularButton, $"{Strings.Regular} ({Shortcut(VirtualKey.N, shift)})");
        }

        private MenuFlyoutItem CreateItem(string text, string icon, ICommand command, object parameter = null, bool premium = false, string shortcut = null)
        {
            var item = premium && IsPremiumMarked ? new MenuFlyoutPremiumItem() : new MenuFlyoutItem();
            item.Text = text;
            item.Command = command;
            item.CommandParameter = parameter;
            item.KeyboardAcceleratorTextOverride = shortcut ?? string.Empty;

            if (icon != null)
            {
                item.Icon = MenuFlyoutHelper.CreateIcon(icon);
            }

            return item;
        }

        private ToggleMenuFlyoutItem CreateToggle(string text, string icon, bool isChecked, ICommand command, object parameter = null, bool premium = false, string shortcut = null)
        {
            var item = premium && IsPremiumMarked ? new ToggleMenuFlyoutPremiumItem() : new ToggleMenuFlyoutItem();
            item.Text = text;
            item.IsChecked = isChecked;
            item.Command = command;
            item.CommandParameter = parameter;
            item.KeyboardAcceleratorTextOverride = shortcut ?? string.Empty;

            if (icon != null)
            {
                item.Icon = MenuFlyoutHelper.CreateIcon(icon);
            }

            return item;
        }

        private static string Shortcut(VirtualKey key, VirtualKeyModifiers modifiers = VirtualKeyModifiers.Control)
        {
            return ShortcutsService.GetStringRepresentation(key, modifiers);
        }

        // A new one each time: it is a WinRT object, and every editor window has a thread of its own.
        private static FlyoutShowOptions ToolbarMenu => new()
        {
            Placement = FlyoutPlacementMode.Bottom,
            ShowMode = FlyoutShowMode.Transient
        };

        // The lock follows the first change of a burst at once and the rest at most every tick:
        // waiting for typing to stop left it out of date until a send was tried.
        private bool _lockPending;

        private void ScheduleSendLock()
        {
            if (_lockTimer.IsEnabled)
            {
                _lockPending = true;
                return;
            }

            UpdateSendLock();
            _lockTimer.Start();
        }

        private void OnLockTimerTick(object sender, object e)
        {
            _lockTimer.Stop();

            if (_lockPending)
            {
                _lockPending = false;
                UpdateSendLock();
                _lockTimer.Start();
            }
        }

        // Free users see a lock over the send button when the document contains rich content that
        // can't be sent as a plain message FormattedText.
        private void UpdateSendLock()
        {
            var locked = false;

            if (_engine != null && _clientService.IsPremiumAvailable && !_clientService.IsPremium)
            {
                var input = GetInputModel();
                locked = input != null && !PageBlockHelper.TryGetFormattedText(input, out _);
            }

            SendLock.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;

            if (_engine != null)
            {
                var within = RichDocumentLimits.Validate(_engine.Document, Limits, out _, out _) == RichLimitError.None;
                SendButton.Opacity = within ? 1 : 0.5;
            }
        }

        // The window is shown after its content exists, and activating it moves focus to the
        // first focusable element in the tree - a toolbar button. So focus goes to the editor
        // on activation too.
        protected override void OnWindowActivated(bool active)
        {
            if (active)
            {
                // Low priority: XAML assigns its own initial focus while activating, and
                // whichever runs last wins.
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, TryFocus);
            }
        }

        private void TryFocus()
        {
            if (FocusManager.GetFocusedElement(XamlRoot) is not DependencyObject focused || !IsInsideEditor(focused))
            {
                Editor.Focus(FocusState.Programmatic);
            }
        }

        private bool IsInsideEditor(DependencyObject element)
        {
            for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (ReferenceEquals(current, Editor))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnEditorStateChanged(object sender, EventArgs e)
        {
            UpdateToolbar();
            ScheduleSendLock();
        }

        private void UpdateToolbar()
        {
            var status = _status = _engine.GetStatus();

            UndoButton.IsEnabled = status.CanUndo;
            RedoButton.IsEnabled = status.CanRedo;

            // Expanded groups or folded, the same: folding is a layout pass, not a change
            // anything here hears about.
            foreach (var (button, mark) in _markButtons)
            {
                button.IsChecked = (status.Marks & mark) != 0;
                button.IsEnabled = status.CanFormat;
            }

            foreach (var (button, style) in _listButtons)
            {
                button.IsChecked = status.ListStyle == style;
            }

            GroupToggleBlockButton.IsChecked = status.InDetails;
            GroupTextButton.IsChecked = status.TextKind == RichBlockKind.Paragraph && !status.InBlockQuote;
            GroupQuoteButton.IsChecked = status.InBlockQuote;
            GroupPullQuoteButton.IsChecked = status.TextKind == RichBlockKind.PullQuote;
            GroupCodeButton.IsChecked = status.TextKind == RichBlockKind.Preformatted;
            GroupFooterButton.IsChecked = status.TextKind == RichBlockKind.Footer;
            GroupTableButton.IsChecked = status.Table != null;
            GroupRegularButton.IsEnabled = status.CanFormat;

            // Links go on text, so the button waits for some, as TDesktop's does.
            GroupLinkButton.IsEnabled = status.CanFormat;
            GroupIndentButton.IsEnabled = _engine.CanIndent(MaxDepth);
            GroupOutdentButton.IsEnabled = _engine.CanOutdent();
        }

        // The grouped toolbar's buttons. A toggle checks itself when clicked and what it shows is
        // what the editor says, so the toolbar is updated after whether or not anything changed.
        private void GroupMark_Click(object sender, RoutedEventArgs e)
        {
            foreach (var (button, mark) in _markButtons)
            {
                if (ReferenceEquals(button, sender))
                {
                    _engine.ToggleMark(mark);
                    break;
                }
            }

            UpdateToolbar();
        }

        private void GroupList_Click(object sender, RoutedEventArgs e)
        {
            foreach (var (button, style) in _listButtons)
            {
                if (ReferenceEquals(button, sender))
                {
                    _engine.ToggleList(style);
                    break;
                }
            }

            UpdateToolbar();
        }

        private void GroupRegular_Click(object sender, RoutedEventArgs e) => Run(_engine.ClearFormatting);

        private void GroupToggleBlock_Click(object sender, RoutedEventArgs e) => Run(_engine.ToggleDetails);

        private void GroupIndent_Click(object sender, RoutedEventArgs e) => Run(Indent);

        private void GroupOutdent_Click(object sender, RoutedEventArgs e) => Run(Outdent);

        private void GroupText_Click(object sender, RoutedEventArgs e) => Run(SetParagraph);

        private void GroupQuote_Click(object sender, RoutedEventArgs e) => Run(_engine.ToggleBlockQuote);

        private void GroupPullQuote_Click(object sender, RoutedEventArgs e) => Run(_engine.TogglePullQuote);

        private void GroupFooter_Click(object sender, RoutedEventArgs e) => Run(SetFooter);

        private void GroupDivider_Click(object sender, RoutedEventArgs e) => Run(_engine.InsertDivider);

        private void GroupCode_Click(object sender, RoutedEventArgs e)
        {
            if ((_status ?? _engine.GetStatus()).TextKind == RichBlockKind.Preformatted)
            {
                Run(SetParagraph);
            }
            else
            {
                Run(SetPreformatted);
            }
        }

        private void GroupInlineButton_Click(object sender, RoutedEventArgs e) => InsertInlineButton();

        private void GroupDate_Click(object sender, RoutedEventArgs e) => InsertDate();

        private void GroupHeading_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            PopulateHeadingFlyout(flyout.Items, _status ?? _engine.GetStatus());

            flyout.ShowAt(sender as FrameworkElement, ToolbarMenu);
        }

        private void Run(Action action)
        {
            action();
            UpdateToolbar();
        }

        private async void OnLanguageRequested(object sender, RichLanguageRequestedEventArgs e)
        {
            var language = await CodeLanguagePopup.ChooseAsync(XamlRoot, e.Block.Language);
            if (language != null)
            {
                _engine.SetLanguage(e.Block, language);
            }

            Editor.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// Double click or Enter on a block that is selected whole: what editing it means for the
        /// kind of block it is.
        /// </summary>
        private async void OnNodeActivated(object sender, RichNodeActivatedEventArgs e)
        {
            switch (e.Node)
            {
                case RichMathBlock math:
                    {
                        e.Handled = true;

                        var popup = new FormulaPopup(math.Expression);
                        var confirm = await popup.ShowQueuedAsync(XamlRoot);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            _engine.BeginChange();
                            math.Expression = popup.Source;
                            _engine.EndChange();
                        }

                        break;
                    }
                case RichButtonRowBlock row:
                    e.Handled = true;
                    EditButton(row, 0);
                    break;
                case RichMediaBlock media when media.Source.Local is StorageMedia:
                    e.Handled = true;
                    EditMedia(media);
                    break;
                case RichMapBlock map:
                    e.Handled = true;
                    ChangeLocation(map);
                    break;
            }
        }

        private async void OnObjectRequested(object sender, RichObjectRequestedEventArgs e)
        {
            if (e.Object.Kind == RichInlineKind.Button)
            {
                EditInlineButton(e.Leaf, e.Object.Button);
                return;
            }

            if (e.Object.Kind == RichInlineKind.Math && e.Offset >= 0)
            {
                var popup = new FormulaPopup(e.Object.Text);
                var confirm = await popup.ShowQueuedAsync(XamlRoot);
                if (confirm == ContentDialogResult.Primary)
                {
                    _engine.ReplaceObject(new RichPosition(e.Leaf, e.Offset), RichInlineObject.FromMath(popup.Source));
                }
            }
        }

        private void OnButtonRequested(object sender, RichButtonRequestedEventArgs e)
        {
            EditButton(e.Row, e.Index);
        }

        /// <summary>One button of a row, or a new one at the end with an index past the last.</summary>
        private async void EditButton(RichButtonRowBlock row, int index)
        {
            var existing = index >= 0 && index < row.Buttons.Count ? row.Buttons[index] : null;

            var popup = new RichButtonPopup(_clientService, _navigationService, existing, existing != null);
            var result = await popup.ShowQueuedAsync(XamlRoot);

            var buttons = new List<InlineButton>(row.Buttons);

            if (result == RichButtonPopupResult.Save)
            {
                if (existing != null)
                {
                    buttons[index] = popup.Button;
                }
                else
                {
                    buttons.Add(popup.Button);
                }

                _engine.SetButtons(row, buttons, row.Align);
            }
            else if (result == RichButtonPopupResult.Delete && existing != null)
            {
                buttons.RemoveAt(index);
                _engine.SetButtons(row, buttons, row.Align);
            }

            Editor.Focus(FocusState.Programmatic);
        }

        private void InsertInlineButton() => InsertButton(false);

        /// <summary>A new button, in a row of its own or in the text as the popup is told.</summary>
        private async void InsertButton(bool separateLine)
        {
            var choice = GetSeparateLineChoice(separateLine);

            var popup = new RichButtonPopup(_clientService, _navigationService, null, false, choice);
            if (await popup.ShowQueuedAsync(XamlRoot) == RichButtonPopupResult.Save)
            {
                if (choice == null ? _engine.SelectedNode != null : popup.IsSeparateLine)
                {
                    _engine.InsertButtonRow(new[] { popup.Button });
                }
                else
                {
                    _engine.InsertObject(RichInlineObject.FromButton(popup.Button));
                }
            }

            Editor.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// Whether a new button or formula may go on a line of its own or in the text, starting
        /// as <paramref name="value"/>; null where there is no choice. A selected block takes
        /// only a block after it, and a cell or a caption only text.
        /// </summary>
        private bool? GetSeparateLineChoice(bool value)
        {
            if (_engine.SelectedNode != null)
            {
                return null;
            }

            var leaf = _engine.Focus.Leaf;
            return leaf.Role == RichLeafRole.Text && leaf.Owner is RichParagraphBlock ? value : null;
        }

        /// <summary>
        /// An inline button clicked in the text. The click names the button, not where it is, so
        /// it is found in its leaf by identity - the button object is shared, never copied.
        /// </summary>
        private async void EditInlineButton(RichTextLeaf leaf, InlineButton button)
        {
            var offset = FindButton(leaf, button);
            if (offset < 0)
            {
                return;
            }

            var popup = new RichButtonPopup(_clientService, _navigationService, button, true);
            var result = await popup.ShowQueuedAsync(XamlRoot);

            // The leaf may have changed while the popup was open.
            offset = FindButton(leaf, button);
            if (offset < 0)
            {
                return;
            }

            var position = new RichPosition(leaf, offset);

            if (result == RichButtonPopupResult.Save)
            {
                _engine.ReplaceObject(position, RichInlineObject.FromButton(popup.Button));
            }
            else if (result == RichButtonPopupResult.Delete)
            {
                _engine.Select(position, new RichPosition(leaf, offset + 1));
                _engine.DeleteSelection();
            }

            Editor.Focus(FocusState.Programmatic);
        }

        private static int FindButton(RichTextLeaf leaf, InlineButton button)
        {
            var position = 0;
            foreach (var run in leaf.Runs)
            {
                if (ReferenceEquals(run.Object?.Button, button))
                {
                    return position;
                }

                position += run.Length;
            }

            return -1;
        }

        private void SetButtonAlignment(PageBlockHorizontalAlignment align)
        {
            if (_engine.SelectedNode is RichButtonRowBlock row)
            {
                _engine.SetButtons(row, row.Buttons, align);
            }
        }

        // =====================================================================================
        // Media
        // =====================================================================================

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            PopulateAttachFlyout(flyout.Items);

            flyout.ShowAt(sender as FrameworkElement, ToolbarMenu);
        }

        private void PopulateAttachFlyout(IList<MenuFlyoutItemBase> flyout)
        {
            // The divider, the button and the toggle block went to the menus TDesktop has them in:
            // the block style, the link and the list.
            flyout.Add(CreateItem(Strings.ArticleCommandImage, Icons.Image, new RelayCommand(PickMedia), premium: true));
            flyout.Add(CreateItem(Strings.ArticleCommandAudio, Icons.MusicNote2, new RelayCommand(PickAudio), premium: true));
            flyout.Add(CreateItem(Strings.AttachDocument, Icons.Document, new RelayCommand(PickFile), premium: true));
            flyout.Add(CreateItem(Strings.AttachLocation, Icons.Location, new RelayCommand(PickLocation), premium: true));
        }

        /// <summary>The Insert group folded: every tool its buttons have, in one menu.</summary>
        private void Insert_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            var status = _status ?? _engine.GetStatus();

            // Inside a table, its menu, as the table button opens.
            if (status.Table == null)
            {
                flyout.Items.Add(CreateItem(Strings.AccDescrIVTable, Icons.Table, new RelayCommand(InsertTable), premium: true));
            }
            else
            {
                var table = new MenuFlyoutSubItem
                {
                    Text = Strings.AccDescrIVTable,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.Table)
                };

                PopulateTableFlyout(table.Items);
                flyout.Items.Add(table);
            }

            flyout.Items.Add(CreateItem(Strings.AccDescrIVFormula, Icons.MathFormula, new RelayCommand(InsertFormula), premium: true));
            flyout.Items.Add(new MenuFlyoutSeparator());
            PopulateAttachFlyout(flyout.Items);
            flyout.Items.Add(new MenuFlyoutSeparator());
            PopulateLinkFlyout(flyout.Items, status);

            flyout.ShowAt(sender as FrameworkElement, ToolbarMenu);
        }

        private void InsertTable() => _engine.InsertTable(2, 2);

        private async void PickMedia()
        {
            var files = await PickAsync(PickerLocationId.PicturesLibrary, Constants.MediaTypes);
            await InsertFilesAsync(files, false);
        }

        /// <summary>The + on a picture or a collage: what is picked joins it, a lone picture becoming a collage.</summary>
        private async void OnMediaAddRequested(object sender, RichNodeActivatedEventArgs e)
        {
            var target = e.Node;

            var files = await PickAsync(PickerLocationId.PicturesLibrary, Constants.MediaTypes);
            if (files == null || files.Count == 0)
            {
                Editor.Focus(FocusState.Programmatic);
                return;
            }

            var sources = await RichMediaFactory.CreateAsync(files, false);
            if (sources.Count == 0 || !CheckMediaLimit(sources.Count))
            {
                Editor.Focus(FocusState.Programmatic);
                return;
            }

            switch (target)
            {
                case RichMediaGroupBlock group:
                    _engine.AddToGroup(group, sources);
                    break;
                case RichMediaBlock { Parent: RichMediaGroupBlock parent }:
                    _engine.AddToGroup(parent, sources);
                    break;
                case RichMediaBlock media:
                    _engine.GroupWith(media, sources);
                    break;
            }

            Editor.Focus(FocusState.Programmatic);
        }

        private async void PickFile()
        {
            var files = await PickAsync(PickerLocationId.DocumentsLibrary, null);
            await InsertFilesAsync(files, true);
        }

        /// <summary>
        /// The composer's music picker: tracks already on Telegram go in by file id, and asking
        /// for a file from the device falls through to the picker, as it does there.
        /// </summary>
        private async void PickAudio()
        {
            var sources = await PickAudioAsync();
            if (sources == null)
            {
                Editor.Focus(FocusState.Programmatic);
                return;
            }

            InsertMedia(sources);
        }

        /// <returns>What was picked, or null when nothing was.</returns>
        private async Task<IList<RichMediaSource>> PickAudioAsync()
        {
            var popup = new SendAudiosPopup(_clientService, _navigationService);

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            if (popup.SelectedItems?.Count > 0)
            {
                var sources = new List<RichMediaSource>(popup.SelectedItems.Count);

                foreach (var item in popup.SelectedItems)
                {
                    if (item.ToInputMessage() is InputMessageAudio audio)
                    {
                        sources.Add(RichMediaSource.FromInput(RichMediaKind.Audio, audio.Audio, null, 0, 0, audio.Audio.Duration));
                    }
                }

                return sources;
            }

            var files = await PickAsync(PickerLocationId.MusicLibrary, null);
            if (files == null || files.Count == 0)
            {
                return null;
            }

            return await RichMediaFactory.CreateAsync(files, false);
        }

        private async Task<IReadOnlyList<StorageFile>> PickAsync(PickerLocationId location, IEnumerable<string> types)
        {
            try
            {
                var picker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.Thumbnail,
                    SuggestedStartLocation = location
                };

                if (types == null)
                {
                    picker.FileTypeFilter.Add("*");
                }
                else
                {
                    foreach (var type in types)
                    {
                        picker.FileTypeFilter.Add(type);
                    }
                }

                return await picker.PickMultipleFilesAsync(XamlRoot);
            }
            catch
            {
                // The picker refuses to open while another one is.
                return null;
            }
        }

        private async Task InsertFilesAsync(IReadOnlyList<IStorageItem> files, bool asDocuments)
        {
            if (files == null || files.Count == 0)
            {
                Editor.Focus(FocusState.Programmatic);
                return;
            }

            var sources = await RichMediaFactory.CreateAsync(files, asDocuments);
            InsertMedia(sources);
        }

        /// <summary>
        /// Into a selected collage, photos and videos join it; anything else goes in at the caret,
        /// photos and videos picked together as a collage and files a block each.
        /// </summary>
        private void InsertMedia(IList<RichMediaSource> sources)
        {
            if (sources.Count == 0)
            {
                Editor.Focus(FocusState.Programmatic);
                return;
            }

            if (!CheckMediaLimit(sources.Count))
            {
                return;
            }

            // Files picked with a collage selected go after it, not into it.
            if (_engine.SelectedNode is RichMediaGroupBlock group && CanGroupAll(sources))
            {
                _engine.AddToGroup(group, sources);
            }
            else
            {
                _engine.InsertMedia(sources);
            }

            Editor.Focus(FocusState.Programmatic);
        }

        private static bool CanGroupAll(IList<RichMediaSource> sources)
        {
            foreach (var source in sources)
            {
                if (!RichEditorEngine.CanGroup(source))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckMediaLimit(int adding)
        {
            var limit = (int)_clientService.Options.RichMessageMediaCountMax;
            if (limit > 0 && _engine.CountMedia() + adding > limit)
            {
                ToastPopup.Show(XamlRoot, string.Format(Strings.ArticleLimitMedia, limit));
                return false;
            }

            return true;
        }

        private async void OnPasteRequested(object sender, RichPasteRequestedEventArgs e)
        {
            var content = e.Content;

            try
            {
                if (content.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await content.GetStorageItemsAsync();
                    await InsertFilesAsync(items, false);
                }
                else if (content.Contains(StandardDataFormats.Bitmap))
                {
                    var media = await StorageMedia.CreateFromBitmapAsync(content);
                    if (await RichMediaFactory.CreateAsync(media) is RichMediaSource source)
                    {
                        InsertMedia(new[] { source });
                    }
                }
            }
            catch
            {
                // The clipboard or the drag source went away mid-read.
            }
        }

        /// <summary>The media editor over a block made in this editor; a sent file cannot be opened again.</summary>
        private async void EditMedia(RichMediaBlock block)
        {
            if (block.Source.Local is not StorageMedia media)
            {
                return;
            }

            var popup = new EditMediaPopup(XamlRoot, _clientService, media);

            var confirm = await popup.ShowAsync();
            if (confirm == ContentDialogResult.Primary && await RichMediaFactory.CreateAsync(media) is RichMediaSource source)
            {
                _engine.SetSource(block, source);
            }

            Editor.Focus(FocusState.Programmatic);
        }

        private async void ReplaceMedia(RichMediaBlock block)
        {
            if (block.Source.Kind == RichMediaKind.Audio)
            {
                var sources = await PickAudioAsync();
                if (sources?.Count > 0)
                {
                    _engine.SetSource(block, sources[0]);
                }

                Editor.Focus(FocusState.Programmatic);
                return;
            }

            var types = block.Source.Kind switch
            {
                RichMediaKind.Photo or RichMediaKind.Video or RichMediaKind.Animation => Constants.MediaTypes,
                _ => null
            };

            var files = await PickAsync(PickerLocationId.PicturesLibrary, types);
            if (files == null || files.Count == 0)
            {
                return;
            }

            var media = await StorageMedia.CreateAsync(files[0]);
            if (await RichMediaFactory.CreateAsync(media, types == null) is RichMediaSource source)
            {
                _engine.SetSource(block, source);
            }

            Editor.Focus(FocusState.Programmatic);
        }

        private async void PickLocation()
        {
            var (location, caption) = await PickPlaceAsync();
            if (location != null)
            {
                _engine.InsertMap(location, caption);
            }

            Editor.Focus(FocusState.Programmatic);
        }

        private async void ChangeLocation(RichMapBlock map)
        {
            var location = await PickLocationAsync();
            if (location != null)
            {
                _engine.BeginChange();
                map.Location = location;
                _engine.EndChange();
            }

            Editor.Focus(FocusState.Programmatic);
        }

        private async Task<Location> PickLocationAsync()
        {
            return (await PickPlaceAsync()).Location;
        }

        /// <summary>A point, or a venue: the caption of a map of a venue is its name and address, as TDesktop has it.</summary>
        private async Task<(Location Location, string Caption)> PickPlaceAsync()
        {
            var popup = new SendLocationPopup(_clientService.Session);

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                return default;
            }

            if (popup.Media is InputMessageVenue venue)
            {
                var title = venue.Venue.Title;
                var address = venue.Venue.Address;

                var caption = !string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(address)
                    ? title + "\n" + address
                    : string.IsNullOrEmpty(title) ? address : title;

                return (venue.Venue.Location, caption);
            }

            return (popup.Media is InputMessageLocation location ? location.Location : null, null);
        }

        /// <summary>One item naming the other layout, as the switch on the collage does.</summary>
        private void AddLayoutItem(IList<MenuFlyoutItemBase> flyout, RichMediaGroupBlock group)
        {
            flyout.CreateFlyoutItem(() => _engine.SetSlideshow(group, !group.IsSlideshow),
                group.IsSlideshow ? Strings.AccDescrCollage : Strings.AccDescrIVSlideshow,
                group.IsSlideshow ? Icons.Apps : Icons.Image);
        }

        /// <summary>
        /// What can be done to a selected block, moving it included. A right click puts these in a
        /// submenu named for the block, next to the editing commands, whose Delete already deletes
        /// it; the block's own menu button shows them alone, so it brings its own.
        /// </summary>
        private void PopulateNodeFlyout(IList<MenuFlyoutItemBase> flyout, RichBlock node, bool delete)
        {
            switch (node)
            {
                case RichMediaBlock media:
                    {
                        var visual = media.Source.Kind is RichMediaKind.Photo or RichMediaKind.Video or RichMediaKind.Animation;

                        if (media.Source.Local is StorageMedia && visual)
                        {
                            flyout.CreateFlyoutItem(() => EditMedia(media), Strings.Edit, Icons.Edit);
                        }

                        flyout.CreateFlyoutItem(() => ReplaceMedia(media), Strings.Replace, Icons.ArrowSync);

                        if (visual)
                        {
                            flyout.CreateFlyoutItem(() => _engine.SetSpoiler(media, !media.HasSpoiler), media.HasSpoiler ? Strings.DisablePhotoSpoiler : Strings.EnablePhotoSpoiler, Icons.Spoiler);
                        }

                        if (media.IsGroupItem)
                        {
                            // A photo of a collage: out of it, to stand on its own after it.
                            flyout.CreateFlyoutItem(() => _engine.ExtractFromGroup(media), Strings.CommunityUngroupChatsButton);

                            if (media.Parent is RichMediaGroupBlock parent)
                            {
                                AddLayoutItem(flyout, parent);
                            }

                            break;
                        }

                        if (_engine.GetAdjacentMedia(media, 1) != null || _engine.GetAdjacentMedia(media, -1) != null)
                        {
                            var direction = _engine.GetAdjacentMedia(media, 1) != null ? 1 : -1;
                            flyout.CreateFlyoutItem(() => _engine.Group(media, direction), Strings.StoriesCollage);
                        }

                        flyout.CreateFlyoutItem(() => Editor.FocusLeaf(media.Credit), Strings.ArticleHintAuthor, Icons.Person);
                        break;
                    }

                case RichMediaGroupBlock group:
                    AddLayoutItem(flyout, group);
                    flyout.Add(new MenuFlyoutSeparator());
                    flyout.CreateFlyoutItem(PickMedia, Strings.ArticleCommandImage, Icons.Image);
                    flyout.CreateFlyoutItem(() => _engine.Ungroup(group), Strings.CommunityUngroupChatsButton);
                    flyout.CreateFlyoutItem(() => Editor.FocusLeaf(group.Credit), Strings.ArticleHintAuthor, Icons.Person);
                    break;

                case RichMapBlock map:
                    flyout.CreateFlyoutItem(() => ChangeLocation(map), Strings.Edit, Icons.Location);
                    flyout.CreateFlyoutItem(() => Editor.FocusLeaf(map.Credit), Strings.ArticleHintAuthor, Icons.Person);
                    break;

                case RichMathBlock math:
                    flyout.CreateFlyoutItem(() => OnNodeActivated(Editor, new RichNodeActivatedEventArgs(math)), Strings.Edit, Icons.Edit);
                    break;

                case RichButtonRowBlock row:
                    {
                        for (int i = 0; i < row.Buttons.Count; i++)
                        {
                            var index = i;
                            flyout.CreateFlyoutItem(() => EditButton(row, index), RichDocumentConverter.GetPlainText(row.Buttons[i].Text), Icons.Edit);
                        }

                        flyout.CreateFlyoutItem(() => EditButton(row, row.Buttons.Count), Strings.RichEditorAddButton, Icons.Add);

                        var alignment = new MenuFlyoutSubItem
                        {
                            Text = Strings.ArticleAlignment,
                            Icon = MenuFlyoutHelper.CreateIcon(Icons.TextAlignCenter)
                        };

                        var command = new RelayCommand<PageBlockHorizontalAlignment>(SetButtonAlignment);

                        alignment.Items.Add(new ToggleMenuFlyoutItem { Text = Strings.Default, IsChecked = row.Align == null, Command = command, CommandParameter = null });
                        alignment.Items.Add(new ToggleMenuFlyoutItem { Text = Strings.ArticleAlignLeft, IsChecked = row.Align is PageBlockHorizontalAlignmentLeft, Command = command, CommandParameter = new PageBlockHorizontalAlignmentLeft() });
                        alignment.Items.Add(new ToggleMenuFlyoutItem { Text = Strings.ArticleAlignCenter, IsChecked = row.Align is PageBlockHorizontalAlignmentCenter, Command = command, CommandParameter = new PageBlockHorizontalAlignmentCenter() });
                        alignment.Items.Add(new ToggleMenuFlyoutItem { Text = Strings.ArticleAlignRight, IsChecked = row.Align is PageBlockHorizontalAlignmentRight, Command = command, CommandParameter = new PageBlockHorizontalAlignmentRight() });

                        flyout.Add(alignment);
                        break;
                    }
            }

            if (flyout.Count > 0)
            {
                flyout.Add(new MenuFlyoutSeparator());
            }

            flyout.CreateFlyoutItem(_engine.CanMoveBlock(-1), MoveBlockUp, Strings.LinkAbove, Icons.ChevronUp);
            flyout.CreateFlyoutItem(_engine.CanMoveBlock(1), MoveBlockDown, Strings.LinkBelow, Icons.ChevronDown);

            if (delete)
            {
                flyout.Add(new MenuFlyoutSeparator());
                flyout.CreateFlyoutItem(DeleteNode, Strings.Delete, Icons.Delete, destructive: true);
            }
        }

        /// <summary>What the block's submenu is called: what the block is.</summary>
        private static string GetNodeName(RichBlock node)
        {
            return node switch
            {
                RichMediaBlock { Source.Kind: RichMediaKind.Photo } => Strings.ArticleCommandImage,
                RichMediaBlock { Source.Kind: RichMediaKind.Video } => Strings.ArticleCommandVideo,
                RichMediaBlock { Source.Kind: RichMediaKind.Animation } => Strings.AttachGif,
                RichMediaBlock { Source.Kind: RichMediaKind.Audio } => Strings.ArticleCommandAudio,
                RichMediaBlock { Source.Kind: RichMediaKind.VoiceNote } => Strings.AttachAudio,
                RichMediaBlock => Strings.AttachDocument,
                RichMediaGroupBlock { IsSlideshow: true } => Strings.AccDescrIVSlideshow,
                RichMediaGroupBlock => Strings.AccDescrCollage,
                RichMapBlock => Strings.AttachLocation,
                RichMathBlock => Strings.AccDescrIVFormula,
                RichButtonRowBlock => Strings.RichEditorButton,
                RichBlockQuote => Strings.AccDescrIVBlockquote,
                _ => Strings.ArticleCommandDivider
            };
        }

        private static string GetNodeIcon(RichBlock node)
        {
            return node switch
            {
                RichMediaBlock { Source.Kind: RichMediaKind.Audio } => Icons.MusicNote2,
                RichMediaBlock { Source.Kind: RichMediaKind.VoiceNote } => Icons.MicOn,
                RichMediaBlock { Source.Kind: RichMediaKind.Document } => Icons.Document,
                RichMediaBlock or RichMediaGroupBlock => Icons.Image,
                RichMapBlock => Icons.Location,
                RichMathBlock => Icons.Edit,
                RichButtonRowBlock => Icons.Button,
                RichDividerBlock => Icons.LineHorizontal1,
                RichBlockQuote => Icons.QuoteBlock,
                _ => null
            };
        }

        private async void OnMenuRequested(object sender, RichMenuRequestedEventArgs e)
        {
            var flyout = new MenuFlyout();

            if (e.IsBlockMenu && _engine.SelectedNode is RichBlock selected)
            {
                PopulateNodeFlyout(flyout.Items, selected, true);
                Editor.ShowMenu(flyout, e.Position, e.IsTouch, e.IsKeyboard);
                return;
            }

            // A misspelled word under the caret: what it could be first, as text boxes have it.
            if (Editor.TryGetMisspelling(out string misspelled))
            {
                foreach (var suggestion in await Editor.SuggestAsync(misspelled))
                {
                    flyout.CreateFlyoutItem(() => Editor.ReplaceMisspelling(suggestion), suggestion);
                }

                flyout.CreateFlyoutItem(Editor.AddToDictionary, Strings.SpellingAddToDictionary, Icons.Add);
                flyout.CreateFlyoutItem(Editor.IgnoreMisspelling, Strings.SpellingIgnoreWord);
                flyout.CreateFlyoutSeparator();
            }

            var status = _engine.GetStatus();

            var canPaste = ClipboardEx.TryGetContent()?.Contains(StandardDataFormats.Text) ?? false;

            flyout.CreateFlyoutItem(status.CanUndo, _engine.Undo, Strings.TextUndo, Icons.ArrowUndo, VirtualKey.Z);
            flyout.CreateFlyoutItem(status.CanRedo, _engine.Redo, Strings.Redo, Icons.ArrowRedo, VirtualKey.Y);
            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(status.HasTextSelection, Editor.Cut, Strings.Cut, Icons.Cut, VirtualKey.X);
            flyout.CreateFlyoutItem(status.HasTextSelection, Editor.Copy, Strings.Copy, Icons.Copy, VirtualKey.C);
            flyout.CreateFlyoutItem(canPaste, PasteFormatted, Strings.Paste, Icons.ClipboardPaste, VirtualKey.V);
            flyout.CreateFlyoutItem(!status.IsSelectionEmpty, _engine.DeleteSelection, Strings.Delete);
            flyout.CreateFlyoutSeparator();

            if (_engine.SelectedNode is RichBlock node)
            {
                var block = new MenuFlyoutSubItem
                {
                    Text = GetNodeName(node)
                };

                if (GetNodeIcon(node) is string icon)
                {
                    block.Icon = MenuFlyoutHelper.CreateIcon(icon);
                }

                PopulateNodeFlyout(block.Items, node, false);

                flyout.Items.Add(block);
            }
            else if (status.Table != null)
            {
                var table = new MenuFlyoutSubItem
                {
                    Text = Strings.AccDescrIVTable,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.Table)
                };

                PopulateTableFlyout(table.Items);

                flyout.Items.Add(table);
            }
            else if (status.ListStyle != null || status.InDetails)
            {
                var list = new MenuFlyoutSubItem
                {
                    Text = Strings.AccDescrIVListStyle,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextBulletList)
                };

                PopulateListFlyout(list.Items);

                flyout.Items.Add(list);
            }
            else if (status.TextKind is RichBlockKind.Heading or RichBlockKind.Paragraph or RichBlockKind.PullQuote or RichBlockKind.Preformatted or RichBlockKind.Footer)
            {
                var paragraph = new MenuFlyoutSubItem
                {
                    Text = Strings.Formatting,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextParagraph)
                };

                PopulateParagraphFlyout(paragraph.Items);

                flyout.Items.Add(paragraph);
            }

            if (status.CanFormat)
            {
                var formatting = new MenuFlyoutSubItem
                {
                    Text = Strings.AccDescrIVTextStyle,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextFont)
                };

                PopulateTextStyleFlyout(formatting.Items, status);
                formatting.Items.Add(new MenuFlyoutSeparator());
                PopulateLinkFlyout(formatting.Items, status);

                flyout.Items.Add(formatting);
            }

            flyout.CreateFlyoutSeparator();
            flyout.CreateFlyoutItem(!status.IsDocumentEmpty, _engine.SelectAll, Strings.SelectAll, null, VirtualKey.A);

            Editor.ShowMenu(flyout, e.Position, e.IsTouch, e.IsKeyboard);
        }

        private void PasteFormatted() => Editor.Paste(false);

        private void MoveBlockUp() => _engine.MoveBlock(-1);

        private void MoveBlockDown() => _engine.MoveBlock(1);

        private void DeleteNode()
        {
            if (_engine.SelectedNode is RichBlock node)
            {
                _engine.Delete(node);
            }
        }

        private void TextStyle_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            PopulateTextStyleFlyout(flyout.Items, _status ?? _engine.GetStatus());

            flyout.ShowAt(sender as FrameworkElement, ToolbarMenu);
        }

        /// <summary>
        /// The marks, as TDesktop's Text Style menu has them: what a regular message has first,
        /// unmarked, then what only a rich message has.
        /// </summary>
        private void PopulateTextStyleFlyout(IList<MenuFlyoutItemBase> flyout, RichEditorStatus status)
        {
            var command = new RelayCommand<RichMarks>(_engine.ToggleMark);
            var control = VirtualKeyModifiers.Control;
            var shift = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift;

            flyout.Add(CreateToggle(Strings.Bold, Icons.TextBold, (status.Marks & RichMarks.Bold) != 0, command, RichMarks.Bold, shortcut: Shortcut(VirtualKey.B, control)));
            flyout.Add(CreateToggle(Strings.Italic, Icons.TextItalic, (status.Marks & RichMarks.Italic) != 0, command, RichMarks.Italic, shortcut: Shortcut(VirtualKey.I, control)));
            flyout.Add(CreateToggle(Strings.Underline, Icons.TextUnderline, (status.Marks & RichMarks.Underline) != 0, command, RichMarks.Underline, shortcut: Shortcut(VirtualKey.U, control)));
            flyout.Add(CreateToggle(Strings.Strike, Icons.TextStrikethrough, (status.Marks & RichMarks.Strikethrough) != 0, command, RichMarks.Strikethrough, shortcut: Shortcut(VirtualKey.X, shift)));
            flyout.Add(CreateToggle(Strings.Spoiler, Icons.Spoiler, (status.Marks & RichMarks.Spoiler) != 0, command, RichMarks.Spoiler, shortcut: Shortcut(VirtualKey.P, shift)));
            flyout.Add(CreateToggle(Strings.Mono, Icons.Code, (status.Marks & RichMarks.Monospace) != 0, command, RichMarks.Monospace, shortcut: Shortcut(VirtualKey.M, shift)));
            flyout.Add(CreateToggle(Strings.Subscript, Icons.TextSubscript, (status.Marks & RichMarks.Subscript) != 0, command, RichMarks.Subscript, premium: true));
            flyout.Add(CreateToggle(Strings.Superscript, Icons.TextSuperscript, (status.Marks & RichMarks.Superscript) != 0, command, RichMarks.Superscript, premium: true));
            flyout.Add(CreateToggle(Strings.RichEditorMarked, Icons.Highlight, (status.Marks & RichMarks.Marked) != 0, command, RichMarks.Marked, premium: true));
            flyout.Add(new MenuFlyoutSeparator());
            flyout.Add(CreateItem(Strings.Regular, null, new RelayCommand(_engine.ClearFormatting), shortcut: Shortcut(VirtualKey.N, shift)));

            // On a caret too: what is toggled there is what the next text typed gets, and the
            // menu shows it checked. Only where text takes no marks - a code block, a selected
            // block - is there nothing to do.
            foreach (var item in flyout)
            {
                if (item is MenuFlyoutItem menu)
                {
                    menu.IsEnabled = status.CanFormat;
                }
            }
        }

        /// <summary>A link on the caret is edited straight away; anything else is a choice of what to insert.</summary>
        private void Link_Click(object sender, RoutedEventArgs e)
        {
            var status = _status ?? _engine.GetStatus();
            if (status.HasLink)
            {
                CreateLink();
                return;
            }

            var flyout = new MenuFlyout();

            PopulateLinkFlyout(flyout.Items, status);

            flyout.ShowAt(sender as FrameworkElement, ToolbarMenu);
        }

        private void PopulateLinkFlyout(IList<MenuFlyoutItemBase> flyout, RichEditorStatus status)
        {
            flyout.Add(CreateItem(status.HasLink ? Strings.EditLink : Strings.CreateLink, Icons.Link, new RelayCommand(CreateLink), shortcut: Shortcut(VirtualKey.K)));
            flyout.Add(CreateItem(Strings.RichEditorButton, Icons.Button, new RelayCommand(InsertInlineButton)));
            flyout.Add(CreateItem(Strings.AccDescrIVInsertDate, Icons.Calendar, new RelayCommand(InsertDate)));
        }

        private void OnLinkRequested(object sender, EventArgs e) => CreateLink();

        private async void CreateLink()
        {
            var selected = _engine.GetSelectedText();
            var existing = _engine.GetSelectionLink();

            var popup = new CreateLinkPopup
            {
                Text = selected,
                Link = existing?.Kind == RichLinkKind.Url ? existing.Url : string.Empty,
                IsLightDismissEnabled = true
            };

            if (string.IsNullOrEmpty(popup.Link))
            {
                var clipboard = ClipboardEx.TryGetContent();
                if (clipboard != null && clipboard.Contains(StandardDataFormats.Text))
                {
                    try
                    {
                        var link = await clipboard.GetTextAsync();
                        if (link.IsValidUrl())
                        {
                            popup.Link = link;
                        }
                    }
                    catch
                    {
                        // The clipboard changed or went away while being read.
                    }
                }
            }

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm != true || string.IsNullOrEmpty(popup.Link))
            {
                Editor.Focus(FocusState.Programmatic);
                return;
            }

            ApplyLink(RichLink.FromUrl(popup.Link), popup.Text, selected);
            Editor.Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// A link over the selection: as it is when its text was kept, or over new text that
        /// replaces it when the text was changed in the popup.
        /// </summary>
        private void ApplyLink(RichLink link, string text, string selected)
        {
            if (!string.IsNullOrEmpty(selected) && text == selected)
            {
                _engine.SetLink(link);
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _engine.BeginBatch();

            if (_engine.IsSelectionEmpty)
            {
                _engine.SetLink(link, text);
            }
            else
            {
                _engine.InsertText(text);

                var end = _engine.Focus;
                _engine.Select(new RichPosition(end.Leaf, Math.Max(0, end.Offset - text.Length)), end);
                _engine.SetLink(link);
                _engine.SetCaret(end);
            }

            _engine.EndBatch();
        }

        private async void InsertDate()
        {
            var selected = _engine.GetSelectedText();

            var popup = new ChooseDateTimeToast
            {
                Title = Strings.RelativeDateAddDate,
                PrimaryButtonContent = Strings.OK,
                PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                SecondaryButtonContent = Strings.Cancel,
                IsLightDismissEnabled = true,
            };

            var confirm = await popup.ShowAsync(XamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                Editor.Focus(FocusState.Programmatic);
                return;
            }

            var format = new DateTimeFormattingTypeAbsolute(new DateTimePartPrecisionShort(), new DateTimePartPrecisionShort(), false);
            var link = RichLink.FromDateTime((int)popup.Value.ToUnixTimeSeconds(), format);
            var text = string.IsNullOrEmpty(selected) ? Formatter.DateAt((int)popup.Value.ToUnixTimeSeconds()) : selected;

            ApplyLink(link, text, selected);
            Editor.Focus(FocusState.Programmatic);
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            _engine.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            _engine.Redo();
        }

        private void Paragraph_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            PopulateParagraphFlyout(flyout.Items);

            flyout.ShowAt(sender as FrameworkElement, ToolbarMenu);
        }

        private void PopulateParagraphFlyout(IList<MenuFlyoutItemBase> flyout)
        {
            var status = _status ?? _engine.GetStatus();

            var heading = new MenuFlyoutSubItem
            {
                Text = Strings.ArticleHeading,
                Icon = MenuFlyoutHelper.CreateIcon(Icons.TextHeader1)
            };

            PopulateHeadingFlyout(heading.Items, status);

            // In TDesktop's order, starred where it is: a quote and code are in regular messages too.
            flyout.Add(heading);
            flyout.Add(CreateToggle(Strings.ArticleText, Icons.TextT, status.TextKind == RichBlockKind.Paragraph && !status.InBlockQuote, new RelayCommand(SetParagraph)));
            flyout.Add(CreateToggle(Strings.ArticleQuote, Icons.QuoteBlock, status.InBlockQuote, new RelayCommand(_engine.ToggleBlockQuote)));
            flyout.Add(CreateToggle(Strings.ArticlePullquote, Icons.CommentQuote, status.TextKind == RichBlockKind.PullQuote, new RelayCommand(_engine.TogglePullQuote), premium: true));
            flyout.Add(CreateToggle(Strings.ArticleCode, Icons.Code, status.TextKind == RichBlockKind.Preformatted, new RelayCommand(SetPreformatted)));
            flyout.Add(CreateToggle(Strings.ArticleFooter, Icons.DocumentFooter, status.TextKind == RichBlockKind.Footer, new RelayCommand(SetFooter), premium: true));
            flyout.Add(CreateItem(Strings.ArticleCommandDivider, Icons.LineHorizontal1, new RelayCommand(_engine.InsertDivider), premium: true));
        }

        private void PopulateHeadingFlyout(IList<MenuFlyoutItemBase> flyout, RichEditorStatus status)
        {
            var command = new RelayCommand<int>(SetHeading);

            for (int i = 1; i < 7; i++)
            {
                var child = CreateToggle(
                    i == 1 ? Strings.ArticleHeading1 : i == 2 ? Strings.ArticleHeading2 : i == 3 ? Strings.ArticleHeading3 : i == 4 ? Strings.ArticleHeading4 : i == 5 ? Strings.ArticleHeading5 : Strings.ArticleHeading6,
                    i == 1 ? Icons.TextHeader1 : i == 2 ? Icons.TextHeader2 : i == 3 ? Icons.TextHeader3 : i == 4 ? Icons.TextHeader4 : i == 5 ? Icons.TextHeader5 : Icons.TextHeader6,
                    status.TextKind == RichBlockKind.Heading && status.HeadingSize == i,
                    command,
                    i,
                    premium: true);

                child.FontFamily = new FontFamily("Times New Roman");
                child.FontSize = 24 - ((i - 1) * 2);
                child.FontWeight = FontWeights.SemiBold;

                flyout.Add(child);
            }
        }

        private void SetHeading(int size) => _engine.SetTextKind(RichBlockKind.Heading, size);

        private void SetParagraph() => _engine.SetTextKind(RichBlockKind.Paragraph);

        private void SetPreformatted() => _engine.SetTextKind(RichBlockKind.Preformatted);

        private void SetFooter() => _engine.SetTextKind(RichBlockKind.Footer);

        private void List_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            PopulateListFlyout(flyout.Items);

            flyout.ShowAt(sender as FrameworkElement, ToolbarMenu);
        }

        private void PopulateListFlyout(IList<MenuFlyoutItemBase> flyout)
        {
            var status = _status ?? _engine.GetStatus();
            var command = new RelayCommand<RichListStyle>(_engine.ToggleList);

            // Outside a list, the lists that can be inserted; in one, what it can be changed to -
            // inserting there would change it instead - as TDesktop's List menu has it.
            if (status.ListStyle != null)
            {
                var change = new MenuFlyoutSubItem
                {
                    Text = Strings.RichEditorChangeList,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextBulletList)
                };

                change.Items.Add(CreateToggle(Strings.ArticleListNumbered, Icons.TextNumberList, status.ListStyle == RichListStyle.Ordered, command, RichListStyle.Ordered));
                change.Items.Add(CreateToggle(Strings.ArticleListBulleted, Icons.TextBulletList, status.ListStyle == RichListStyle.Bullet, command, RichListStyle.Bullet));
                change.Items.Add(CreateToggle(Strings.ArticleListTodo, Icons.CheckmarkSquare, status.ListStyle == RichListStyle.Checklist, command, RichListStyle.Checklist));
                change.Items.Add(new MenuFlyoutSeparator());
                change.Items.Add(CreateItem(Strings.ArticleNone, null, new RelayCommand(_engine.RemoveList)));

                flyout.Add(change);
                var indent = CreateItem(Strings.ArticleIndent, Icons.TextIndentIncrease, new RelayCommand(Indent));
                indent.IsEnabled = _engine.CanIndent(MaxDepth);
                flyout.Add(indent);

                var outdent = CreateItem(Strings.ArticleOutdent, Icons.TextIndentDecrease, new RelayCommand(Outdent));
                outdent.IsEnabled = _engine.CanOutdent();
                flyout.Add(outdent);
                flyout.Add(new MenuFlyoutSeparator());
            }
            else
            {
                flyout.Add(CreateItem(Strings.ArticleListNumbered, Icons.TextNumberList, command, RichListStyle.Ordered, premium: true));
                flyout.Add(CreateItem(Strings.ArticleListBulleted, Icons.TextBulletList, command, RichListStyle.Bullet, premium: true));
                flyout.Add(CreateItem(Strings.ArticleListTodo, Icons.CheckmarkSquare, command, RichListStyle.Checklist, premium: true));
            }

            // Checked inside one, where choosing it takes the block away again.
            flyout.Add(CreateToggle(Strings.ArticleToggleBlock, Icons.ChevronDown, status.InDetails, new RelayCommand(_engine.ToggleDetails), premium: true));
        }

        private int MaxDepth => (int)_clientService.Options.RichMessageDepthMax;

        private void Indent() => _engine.Indent(MaxDepth);

        private void Outdent() => _engine.Outdent();

        private void Table_Click(object sender, RoutedEventArgs e)
        {
            if (_status?.Table == null)
            {
                _engine.InsertTable(2, 2);
                return;
            }

            var flyout = new MenuFlyout();

            PopulateTableFlyout(flyout.Items);

            flyout.ShowAt(sender as FrameworkElement, ToolbarMenu);

            // The button is a toggle, and opening a menu changed nothing to check it by.
            UpdateToolbar();
        }

        private void PopulateTableFlyout(IList<MenuFlyoutItemBase> flyout)
        {
            var table = (_status ?? _engine.GetStatus()).Table;
            if (table == null)
            {
                return;
            }

            var alignment = new MenuFlyoutSubItem
            {
                Text = Strings.ArticleAlignment,
                Icon = MenuFlyoutHelper.CreateIcon(Icons.TextboxAlignCenter),
            };

            {
                var command = new RelayCommand<PageBlockHorizontalAlignment>(_engine.TableSetAlignment);

                alignment.Items.Add(new ToggleMenuFlyoutItem
                {
                    Text = Strings.ArticleAlignLeft,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextAlignLeft),
                    IsChecked = table.Align is PageBlockHorizontalAlignmentLeft,
                    CommandParameter = new PageBlockHorizontalAlignmentLeft(),
                    Command = command
                });
                alignment.Items.Add(new ToggleMenuFlyoutItem
                {
                    Text = Strings.ArticleAlignCenter,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextAlignCenter),
                    IsChecked = table.Align is PageBlockHorizontalAlignmentCenter,
                    CommandParameter = new PageBlockHorizontalAlignmentCenter(),
                    Command = command
                });
                alignment.Items.Add(new ToggleMenuFlyoutItem
                {
                    Text = Strings.ArticleAlignRight,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextAlignRight),
                    IsChecked = table.Align is PageBlockHorizontalAlignmentRight,
                    CommandParameter = new PageBlockHorizontalAlignmentRight(),
                    Command = command
                });
            }

            alignment.CreateFlyoutSeparator();

            {
                var command = new RelayCommand<PageBlockVerticalAlignment>(_engine.TableSetVerticalAlignment);

                alignment.Items.Add(new ToggleMenuFlyoutItem
                {
                    Text = Strings.ArticleAlignTop,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextboxAlignTop),
                    IsChecked = table.Valign is PageBlockVerticalAlignmentTop,
                    CommandParameter = new PageBlockVerticalAlignmentTop(),
                    Command = command
                });
                alignment.Items.Add(new ToggleMenuFlyoutItem
                {
                    Text = Strings.ArticleAlignMiddle,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextboxAlignMiddle),
                    IsChecked = table.Valign is PageBlockVerticalAlignmentMiddle,
                    CommandParameter = new PageBlockVerticalAlignmentMiddle(),
                    Command = command
                });
                alignment.Items.Add(new ToggleMenuFlyoutItem
                {
                    Text = Strings.ArticleAlignBottom,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TextboxAlignBottom),
                    IsChecked = table.Valign is PageBlockVerticalAlignmentBottom,
                    CommandParameter = new PageBlockVerticalAlignmentBottom(),
                    Command = command
                });
            }

            flyout.Add(alignment);
            flyout.CreateFlyoutItem(_engine.TableToggleHeader, table.IsHeader ? Strings.ArticleRemoveHighlight : Strings.ArticleHighlightCell, Icons.TabInPrivate);

            if (table.CanMerge)
            {
                flyout.CreateFlyoutItem(_engine.TableMergeCells, Strings.ArticleMergeCells, Icons.TableCellMerge);
            }

            if (table.CanSplit)
            {
                flyout.CreateFlyoutItem(_engine.TableSplitCell, Strings.ArticleSplitCells, Icons.TableCellSplit);
            }

            flyout.CreateFlyoutItem(InsertRowAbove, Strings.ArticleInsertAbove, Icons.TableInsertRow);
            flyout.CreateFlyoutItem(InsertRowBelow, Strings.ArticleInsertBelow, Icons.TableInsertRow);

            if (table.CanDeleteRow)
            {
                flyout.CreateFlyoutItem(_engine.TableDeleteRows, Strings.ArticleDeleteRow, Icons.TableDeleteRow, destructive: true);
            }

            flyout.CreateFlyoutItem(InsertColumnLeft, Strings.ArticleInsertLeft, Icons.TableInsertColumn);
            flyout.CreateFlyoutItem(InsertColumnRight, Strings.ArticleInsertRight, Icons.TableInsertColumn);

            if (table.CanDeleteColumn)
            {
                flyout.CreateFlyoutItem(_engine.TableDeleteColumns, Strings.ArticleDeleteColumn, Icons.TableDeleteColumn, destructive: true);
            }

            flyout.Add(new MenuFlyoutSeparator());
            flyout.Add(new ToggleMenuFlyoutItem { Text = Strings.ArticleTableBordered, IsChecked = table.Table.IsBordered, Command = new RelayCommand(ToggleTableBordered) });
            flyout.Add(new ToggleMenuFlyoutItem { Text = Strings.ArticleTableStriped, IsChecked = table.Table.IsStriped, Command = new RelayCommand(ToggleTableStriped) });
            flyout.Add(new ToggleMenuFlyoutItem { Text = Strings.ArticleTableCompact, IsChecked = table.Table.IsCompact, Command = new RelayCommand(ToggleTableCompact) });

            flyout.Add(new MenuFlyoutSeparator());
            flyout.CreateFlyoutItem(DeleteTable, Strings.ArticleDeleteTable, Icons.Delete, destructive: true);
        }

        private void InsertRowAbove() => _engine.TableInsertRow(false);

        private void InsertRowBelow() => _engine.TableInsertRow(true);

        private void InsertColumnLeft() => InsertColumn(false);

        private void InsertColumnRight() => InsertColumn(true);

        private void InsertColumn(bool after)
        {
            var max = _clientService.Options.RichMessageTableColumnCountMax;
            if (max > 0 && _engine.GetTableStatus()?.Columns >= max)
            {
                ToastPopup.Show(XamlRoot, string.Format(Strings.ArticleLimitTableColumns, max));
                return;
            }

            _engine.TableInsertColumn(after);
        }

        private void ToggleTableBordered()
        {
            if (_engine.GetTableStatus()?.Table is RichTableBlock table)
            {
                _engine.TableSetStyle(table, !table.IsBordered, null, null);
            }
        }

        private void ToggleTableStriped()
        {
            if (_engine.GetTableStatus()?.Table is RichTableBlock table)
            {
                _engine.TableSetStyle(table, null, !table.IsStriped, null);
            }
        }

        private void ToggleTableCompact()
        {
            if (_engine.GetTableStatus()?.Table is RichTableBlock table)
            {
                _engine.TableSetStyle(table, null, null, !table.IsCompact);
            }
        }

        private void DeleteTable()
        {
            if (_engine.GetTableStatus()?.Table is RichTableBlock table)
            {
                _engine.Delete(table);
            }
        }

        private void Formula_Click(object sender, RoutedEventArgs e) => InsertFormula();

        private async void InsertFormula()
        {
            // On a line of its own unless told otherwise, as TDesktop's new formula is.
            var choice = GetSeparateLineChoice(true);
            var popup = new FormulaPopup(null, choice);

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                if (choice == null ? _engine.SelectedNode != null : popup.IsSeparateLine)
                {
                    _engine.InsertMathBlock(popup.Source);
                }
                else
                {
                    _engine.InsertObject(RichInlineObject.FromMath(popup.Source));
                }
            }

            Editor.Focus(FocusState.Programmatic);
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(sender as FrameworkElement, new FlyoutShowOptions
            {
                ShowMode = FlyoutShowMode.Transient,
                Placement = FlyoutPlacementMode.BottomEdgeAlignedRight
            });
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                _engine.InsertText(emoji.Value);
            }
            else if (e.ClickedItem is StickerViewModel sticker && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                _engine.InsertObject(RichInlineObject.FromCustomEmoji(customEmoji.CustomEmojiId, sticker.Emoji));
            }
        }

        private RichLimits Limits => new(
            _clientService.Options.RichMessageTextLengthMax,
            _clientService.Options.RichMessageBlockCountMax,
            _clientService.Options.RichMessageDepthMax,
            _clientService.Options.RichMessageMediaCountMax,
            _clientService.Options.RichMessageTableColumnCountMax);

        /// <summary>Says which limit the document is over, so the send is refused here and not by the server.</summary>
        private bool CheckLimits()
        {
            var error = RichDocumentLimits.Validate(_engine.Document, Limits, out _, out long limit);
            if (error == RichLimitError.None)
            {
                return true;
            }

            var format = error switch
            {
                RichLimitError.Length => Strings.ArticleLimitLength,
                RichLimitError.Blocks => Strings.ArticleLimitBlocks,
                RichLimitError.Depth => Strings.ArticleLimitDepth,
                RichLimitError.Media => Strings.ArticleLimitMedia,
                _ => Strings.ArticleLimitTableColumns
            };

            ToastPopup.Show(XamlRoot, string.Format(format, limit));
            return false;
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            var richMessage = GetInputModel(Editor.Uploads);
            if (richMessage == null || !CheckLimits())
            {
                return;
            }

            // Editing an existing rich message.
            if (_messageId != 0)
            {
                _mediaSent = true;
                await _clientService.SendAsync(new EditMessageMedia(_chatId, _messageId, new InputMessageRichMessage(richMessage, false)));
                Close();
                return;
            }

            // Sending a new one — mirrors ComposeViewModel.SendRichMessage: a free user sends the plain
            // FormattedText when the content is representable, otherwise gets the Premium feature promo;
            // a Premium user sends the full rich message.
            if (_clientService.IsPremiumAvailable && !_clientService.IsPremium)
            {
                if (PageBlockHelper.TryGetFormattedText(richMessage, out FormattedText formatted))
                {
                    await SendAsync(new InputMessageText(formatted, null, false));
                    Close();
                }
                else
                {
                    ToastPopup.ShowFeaturePromo(_navigationService, new PremiumFeatureRichMessages());
                }

                return;
            }

            _mediaSent = true;
            await SendAsync(new InputMessageRichMessage(richMessage, true));
            Close();
        }

        private async Task SendAsync(InputMessageContent content)
        {
            var options = _sendOptions ?? new MessageSendOptions();
            options.SendingId = Math.Max(options.SendingId, 1);
            await _clientService.SendAsync(new SendMessage(_chatId, _topic, _replyTo, options, content));
        }

        private void Close()
        {
            _closedExpected = true;
            Window.Close();
        }
    }

    public static class Test
    {
        public static string ToJson(this BaseObject obj)
        {
            using var buffer = new ArrayPoolBufferWriter();
            using var writer = new Utf8JsonWriter(buffer, new()
            {
#if ARM64
                Encoder = new Arm64SafeEncoder(),
#else
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
#endif
                SkipValidation = true
            });

            writer.WriteStartObject();
            try
            {
                obj.ToJson(writer);
            }
            catch { }
            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
