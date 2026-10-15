//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Controls.Messages;
using Telegram.Controls.RichEditor;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Text.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    /// <summary>
    /// How far the native editor has replaced <see cref="FormattedTextBox"/>: each tier takes in
    /// the ones before it. Set from the diagnostics page while the migration lasts.
    /// </summary>
    public enum NativeTextInputMode
    {
        /// <summary>Every surface on <see cref="FormattedTextBox"/>.</summary>
        Off,

        /// <summary>The short-lived surfaces: popups and pages of settings.</summary>
        Popups,

        /// <summary>And stories, live stories and the group call chat.</summary>
        Secondary,

        /// <summary>And the chat composer.</summary>
        All,
    }

    /// <summary>Which old box a surface had, which it keeps while the switch is off.</summary>
    public enum FormattedTextInputKind
    {
        /// <summary><see cref="FormattedTextBox"/>.</summary>
        Plain,

        /// <summary><see cref="Chats.ChatTextBox"/>, the chat's composer.</summary>
        Composer,

        /// <summary><see cref="CaptionTextBox"/>, which offers emoji as they are typed.</summary>
        Caption,
    }

    /// <summary>
    /// Where a box for a message's text goes. It holds a <see cref="FormattedTextBox"/> or a
    /// <see cref="MessageTextBox"/>, as the diagnostics switch says for this surface's
    /// <see cref="Tier"/>, and is itself the <see cref="IFormattedTextInput"/> callers use, so a
    /// surface moves over by changing its element and nothing else.
    /// </summary>
    public partial class FormattedTextInputHost : ContentControl, IFormattedTextInput
    {
        // Read once: switching a box under a live caret, an IME composition and an undo history
        // is not worth supporting, so the page says the change needs a restart.
        private static readonly NativeTextInputMode _mode = AppSettings.Diagnostics.NativeTextInput;

        // Set on the host as they would be on a text box, and handed on to the box: the host's
        // own template draws none of them.
        private static readonly DependencyProperty[] _forwarded = new[]
        {
            PaddingProperty,
            BackgroundProperty,
            BorderBrushProperty,
            BorderThicknessProperty,
            CornerRadiusProperty,
            FontFamilyProperty,
            FontSizeProperty,

            // The box's style has a minimum of its own, which would hold it taller than the host.
            MinHeightProperty
        };

        private IFormattedTextInput _input;

        public FormattedTextInputHost()
        {
            DefaultStyleKey = typeof(FormattedTextInputHost);
        }

        /// <summary>
        /// The box, made on first use: properties set in markup are all in by then, since callers
        /// reach it from code after <c>InitializeComponent</c>, or the template is applied.
        /// </summary>
        public IFormattedTextInput Input => _input ?? CreateInput();

        /// <summary>Whether this surface is on the native editor.</summary>
        public bool IsNative => Tier != NativeTextInputMode.Off && _mode >= Tier;

        /// <summary>
        /// What takes focus inside the box, for a page that hands focus on to it: this element
        /// takes none itself.
        /// </summary>
        public DependencyObject FocusTarget => Input is MessageTextBox native ? native.FocusTarget : Input as DependencyObject;

        /// <summary>How the box has focus, if it does.</summary>
        public FocusState InputFocusState => Input is MessageTextBox native ? native.InputFocusState : (Input as Control)?.FocusState ?? FocusState.Unfocused;

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _ = Input;
        }

        private IFormattedTextInput CreateInput()
        {
            Control element;

            if (IsNative)
            {
                // The composer's Enter sends and Shift+Enter is a new line, which the old box
                // does with AcceptsReturn off and this one only with it on.
                var box = new MessageTextBox
                {
                    AcceptsReturn = AcceptsReturn || Kind == FormattedTextInputKind.Composer,
                    TextInputScope = ToTextInputScope(_inputScope),
                    PreventKeyboardDisplayOnProgrammaticFocus = PreventKeyboardDisplayOnProgrammaticFocus
                };

                if (NativeStyle != null)
                {
                    box.Style = NativeStyle;
                }

                element = box;
                _input = box;
            }
            else
            {
                FormattedTextBox box = Kind switch
                {
                    FormattedTextInputKind.Composer => new ChatTextBox(),
                    FormattedTextInputKind.Caption => new CaptionTextBox(),
                    _ => new FormattedTextBox()
                };

                box.AcceptsReturn = AcceptsReturn;
                box.CustomEmoji = _customEmoji;

                // Only when set, so as not to override the style.
                if (PreventKeyboardDisplayOnProgrammaticFocus)
                {
                    box.PreventKeyboardDisplayOnProgrammaticFocus = true;
                }

                if (_inputScope != InputScopeNameValue.Default)
                {
                    var scope = new InputScope();
                    scope.Names.Add(new InputScopeName(_inputScope));
                    box.InputScope = scope;
                }

                if (LegacyStyle != null)
                {
                    box.Style = LegacyStyle;
                }

                element = box;
                _input = box;
            }

            foreach (var property in _forwarded)
            {
                if (ReadLocalValue(property) != DependencyProperty.UnsetValue)
                {
                    element.SetValue(property, GetValue(property));
                }

                // On the host itself, so nothing outlives it. It also carries a theme change.
                RegisterPropertyChangedCallback(property, OnForwardedPropertyChanged);
            }

            UpdateViewModel();
            UpdateControlledList();

            _input.AllowedEntities = AllowedEntities;
            _input.MaxLength = MaxLength;
            _input.PlaceholderText = PlaceholderText;
            _input.IsReadOnly = IsReadOnly;
            _input.IsReplaceEmojiEnabled = IsReplaceEmojiEnabled;

            // The box is this element's own content, so the subscriptions go when both do.
            _input.ContentChanged += OnInputContentChanged;
            _input.SelectionChanged += OnInputSelectionChanged;
            _input.Submitted += OnInputSubmitted;
            _input.PasteRequested += OnInputPasteRequested;

            Content = element;
            return _input;
        }

        private void OnForwardedPropertyChanged(DependencyObject sender, DependencyProperty dp)
        {
            (Content as Control)?.SetValue(dp, GetValue(dp));
        }

        private static CoreTextInputScope ToTextInputScope(InputScopeNameValue scope)
        {
            return scope switch
            {
                InputScopeNameValue.Chat => CoreTextInputScope.Chat,
                InputScopeNameValue.Number => CoreTextInputScope.Number,
                InputScopeNameValue.Url => CoreTextInputScope.Url,
                InputScopeNameValue.Search => CoreTextInputScope.Search,
                _ => CoreTextInputScope.Text
            };
        }

        private void OnInputContentChanged(object sender, EventArgs e)
        {
            ContentChanged?.Invoke(this, e);
        }

        private void OnInputSelectionChanged(object sender, EventArgs e)
        {
            SelectionChanged?.Invoke(this, e);
        }

        private void OnInputSubmitted(object sender, EventArgs e)
        {
            Submitted?.Invoke(this, e);
        }

        private void OnInputPasteRequested(object sender, FormattedTextPasteEventArgs e)
        {
            PasteRequested?.Invoke(this, e);
        }

        public event EventHandler ContentChanged;

        public event EventHandler SelectionChanged;

        public event EventHandler Submitted;

        public event EventHandler<FormattedTextPasteEventArgs> PasteRequested;

        #region IFormattedTextInput

        public string Text => Input.Text;

        public bool IsEmpty => Input.IsEmpty;

        public FormattedText GetFormattedText(bool clear = false, bool parseMarkdown = true) => Input.GetFormattedText(clear, parseMarkdown);

        public PreparedText GetPreparedText(bool clear = false, bool parseMarkdown = true) => Input.GetPreparedText(clear, parseMarkdown);

        public void SetText(FormattedText text) => Input.SetText(text);

        public void ClearText() => Input.ClearText();

        public void InsertText(string text) => Input.InsertText(text);

        public void InsertEmoji(Sticker sticker, bool track = false) => Input.InsertEmoji(sticker, track);

        public void InsertFormattedText(FormattedText text) => Input.InsertFormattedText(text);

        public void InsertMention(string text, long userId) => Input.InsertMention(text, userId);

        public void PasteFromClipboard() => Input.PasteFromClipboard();

        public bool IsSelectionEmpty => Input.IsSelectionEmpty;

        public int CaretOffset => Input.CaretOffset;

        public void MoveCaretToEnd() => Input.MoveCaretToEnd();

        public bool IsCaretAtStart => Input.IsCaretAtStart;

        public bool IsCaretAtEnd => Input.IsCaretAtEnd;

        public void SplitAtSelection(out FormattedText before, out FormattedText after) => Input.SplitAtSelection(out before, out after);

        public bool TryGetQuery(out AutocompleteEntity entity, out string query, out int start) => Input.TryGetQuery(out entity, out query, out start);

        public void ReplaceQuery(int start, FormattedText text) => Input.ReplaceQuery(start, text);

        public bool HasOpenSuggestions => Input.HasOpenSuggestions;

        public void SetInlinePlaceholder(string prefix, string hint) => Input.SetInlinePlaceholder(prefix, hint);

        public bool IsSpellCheckEnabled
        {
            get => Input.IsSpellCheckEnabled;
            set => Input.IsSpellCheckEnabled = value;
        }

        public bool IsTextPredictionEnabled
        {
            get => Input.IsTextPredictionEnabled;
            set => Input.IsTextPredictionEnabled = value;
        }

        public bool IsSubmitEnabled
        {
            get => Input.IsSubmitEnabled;
            set => Input.IsSubmitEnabled = value;
        }

        public bool FocusInput(FocusState state) => Input.FocusInput(state);

        #endregion

        #region Kind

        /// <summary>Set in markup, before the box is made.</summary>
        public FormattedTextInputKind Kind { get; set; }

        /// <summary>The style for the old box, when the switch leaves it in. Set in markup, before the box is made.</summary>
        public Style LegacyStyle { get; set; }

        /// <summary>The style for <see cref="MessageTextBox"/>, when the switch puts it in. Set in markup, before the box is made.</summary>
        public Style NativeStyle { get; set; }

        private InputScopeNameValue _inputScope;

        /// <summary>What the touch keyboard offers. Set in markup, before the box is made.</summary>
        public InputScopeNameValue InputScope
        {
            get => _inputScope;
            set => _inputScope = value;
        }

        /// <summary>Whether focus given from code leaves the touch keyboard down. Set in markup, before the box is made.</summary>
        public bool PreventKeyboardDisplayOnProgrammaticFocus { get; set; }

        private CustomEmojiCanvas _customEmoji;

        /// <summary>
        /// Where the old box draws its custom emoji, a canvas the page places over it. The native
        /// box draws its own and leaves the canvas empty.
        /// </summary>
        public CustomEmojiCanvas CustomEmoji
        {
            get => _customEmoji;
            set
            {
                _customEmoji = value;

                if (_input is FormattedTextBox legacy)
                {
                    legacy.CustomEmoji = value;
                }
            }
        }

        private ListViewBase _controlledList;
        private ListViewBase _appliedList;

        /// <summary>The suggestions shown for the box outside it, which a screen reader is told the box controls.</summary>
        public ListViewBase ControlledList
        {
            get => _controlledList;
            set
            {
                _controlledList = value;
                UpdateControlledList();
            }
        }

        private void UpdateControlledList()
        {
            if (_input is ChatTextBox composer)
            {
                composer.ControlledList = _controlledList as OrientableListView;
            }
            else if (_input is MessageTextBox native)
            {
                native.ControlledList = _controlledList;
            }
            else if (_input is FormattedTextBox legacy)
            {
                var peers = AutomationProperties.GetControlledPeers(legacy);

                if (_appliedList != null)
                {
                    peers.Remove(_appliedList);
                }

                if (_controlledList != null)
                {
                    peers.Add(_controlledList);
                }

                _appliedList = _controlledList;
            }
        }

        private ViewModelBase _viewModel;

        /// <summary>The session and the chat that emoji suggestions are found for, on either box.</summary>
        public ViewModelBase ViewModel
        {
            get => _viewModel;
            set
            {
                if (_viewModel != value)
                {
                    _viewModel = value;
                    UpdateViewModel();
                }
            }
        }

        private void UpdateViewModel()
        {
            if (_input is CaptionTextBox caption)
            {
                caption.ViewModel = _viewModel;
            }
            else if (_input is MessageTextBox native && _viewModel != null)
            {
                native.ClientService = _viewModel.ClientService;
                native.ChatId = _viewModel is ComposeViewModel compose ? compose.Chat?.Id ?? 0 : 0;
            }
        }

        #endregion

        #region Tier

        /// <summary>The tier this surface moves over with. Set in markup, before the box is made.</summary>
        public NativeTextInputMode Tier
        {
            get => (NativeTextInputMode)GetValue(TierProperty);
            set => SetValue(TierProperty, value);
        }

        public static readonly DependencyProperty TierProperty =
            DependencyProperty.Register("Tier", typeof(NativeTextInputMode), typeof(FormattedTextInputHost), new PropertyMetadata(NativeTextInputMode.All));

        #endregion

        #region AllowedEntities

        public FormattedTextEntity AllowedEntities
        {
            get => (FormattedTextEntity)GetValue(AllowedEntitiesProperty);
            set => SetValue(AllowedEntitiesProperty, value);
        }

        public static readonly DependencyProperty AllowedEntitiesProperty =
            DependencyProperty.Register("AllowedEntities", typeof(FormattedTextEntity), typeof(FormattedTextInputHost), new PropertyMetadata(FormattedTextEntity.All, OnAllowedEntitiesChanged));

        private static void OnAllowedEntitiesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FormattedTextInputHost)d)._input?.AllowedEntities = (FormattedTextEntity)e.NewValue;
        }

        #endregion

        #region MaxLength

        public int MaxLength
        {
            get => (int)GetValue(MaxLengthProperty);
            set => SetValue(MaxLengthProperty, value);
        }

        public static readonly DependencyProperty MaxLengthProperty =
            DependencyProperty.Register("MaxLength", typeof(int), typeof(FormattedTextInputHost), new PropertyMetadata(0, OnMaxLengthChanged));

        private static void OnMaxLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FormattedTextInputHost)d)._input?.MaxLength = (int)e.NewValue;
        }

        #endregion

        #region PlaceholderText

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register("PlaceholderText", typeof(string), typeof(FormattedTextInputHost), new PropertyMetadata(string.Empty, OnPlaceholderTextChanged));

        private static void OnPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FormattedTextInputHost)d)._input?.PlaceholderText = (string)e.NewValue;
        }

        #endregion

        #region IsReadOnly

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(FormattedTextInputHost), new PropertyMetadata(false, OnIsReadOnlyChanged));

        private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FormattedTextInputHost)d)._input?.IsReadOnly = (bool)e.NewValue;
        }

        #endregion

        #region IsReplaceEmojiEnabled

        public bool IsReplaceEmojiEnabled
        {
            get => (bool)GetValue(IsReplaceEmojiEnabledProperty);
            set => SetValue(IsReplaceEmojiEnabledProperty, value);
        }

        public static readonly DependencyProperty IsReplaceEmojiEnabledProperty =
            DependencyProperty.Register("IsReplaceEmojiEnabled", typeof(bool), typeof(FormattedTextInputHost), new PropertyMetadata(true, OnIsReplaceEmojiEnabledChanged));

        private static void OnIsReplaceEmojiEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FormattedTextInputHost)d)._input?.IsReplaceEmojiEnabled = (bool)e.NewValue;
        }

        #endregion

        #region AcceptsReturn

        /// <summary>Set before the box is made; neither box takes a change to it afterwards.</summary>
        public bool AcceptsReturn
        {
            get => (bool)GetValue(AcceptsReturnProperty);
            set => SetValue(AcceptsReturnProperty, value);
        }

        public static readonly DependencyProperty AcceptsReturnProperty =
            DependencyProperty.Register("AcceptsReturn", typeof(bool), typeof(FormattedTextInputHost), new PropertyMetadata(true));

        #endregion
    }
}
