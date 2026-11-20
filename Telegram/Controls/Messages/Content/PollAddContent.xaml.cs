//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Controls.RichEditor;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class PollAddContent : Control
    {
        private MessageViewModel _message;

        private MenuFlyout _emojiFlyout;
        private EmojiDrawer _emojiDrawer;

        private bool _isEditing;

        public PollAddContent()
        {
            DefaultStyleKey = typeof(PollAddContent);
        }

        #region InitializeComponent

        private Button Placeholder;
        private Grid FieldRoot;
        private GlyphButton Emoji;
        private MessageTextBox Field;
        private PollMediaButton Attach;

        protected override void OnApplyTemplate()
        {
            Placeholder = GetTemplateChild(nameof(Placeholder)) as Button;
            Placeholder.Click += Placeholder_Click;
        }

        #endregion

        /// <summary>
        /// While the field has the focus, any text or media: the poll's footer offers to save the option
        /// instead of its usual button.
        /// </summary>
        public bool IsEditing => _isEditing;

        public event EventHandler IsEditingChanged;

        public void UpdateMessage(MessageViewModel message)
        {
            // The bubble is recycled for other polls, which must not get the text typed for this one.
            if (Field != null && (_message == null || _message.ChatId != message.ChatId || _message.Id != message.Id))
            {
                Field.ClearText();
                Attach.Media = null;
                SetEditing(false);
            }

            _message = message;

            if (Field != null)
            {
                Field.ClientService = message.ClientService;
                Field.ChatId = message.ChatId;
                Attach.ClientService = message.ClientService;
            }
        }

        public void Recycle()
        {
            _message = null;
        }

        public void Submit()
        {
            if (Field == null || _message?.Content is not MessagePoll { CanAddOption: true })
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Field.Text))
            {
                VisualUtilities.ShakeView(this);
                return;
            }

            // The server takes no entities in an option but custom emoji, so no markdown either.
            _message.Delegate.AddPollOption(_message, Field.GetFormattedText(true, false), Attach.Media?.Input);
            Close();
        }

        private void Close()
        {
            Field.ClearText();
            Attach.Media = null;
            SetEditing(false);

            // Or the field, collapsed with the focus, sends it wherever the window decides.
            Placeholder.Focus(FocusState.Programmatic);
        }

        private void SetEditing(bool editing)
        {
            if (_isEditing == editing)
            {
                return;
            }

            _isEditing = editing;

            Placeholder.Visibility = editing
                ? Visibility.Collapsed
                : Visibility.Visible;
            FieldRoot.Visibility = editing
                ? Visibility.Visible
                : Visibility.Collapsed;

            IsEditingChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateEditing()
        {
            // The emoji flyout and what the attach button opens take the focus while they are up,
            // and the field has to stay up under them.
            SetEditing(_emojiFlyout != null || Attach.IsBusy || Attach.Media != null || !Field.IsEmpty || Field.InputFocusState != FocusState.Unfocused);
        }

        private void Placeholder_Click(object sender, RoutedEventArgs e)
        {
            if (_message == null)
            {
                return;
            }

            if (FieldRoot == null)
            {
                FieldRoot = GetTemplateChild(nameof(FieldRoot)) as Grid;
                Emoji = GetTemplateChild(nameof(Emoji)) as GlyphButton;
                Field = GetTemplateChild(nameof(Field)) as MessageTextBox;
                Attach = GetTemplateChild(nameof(Attach)) as PollMediaButton;

                Emoji.Click += Emoji_Click;

                Attach.ClientService = _message.ClientService;
                Attach.MediaChanged += Attach_MediaChanged;
                Attach.IsBusyChanged += Attach_IsBusyChanged;

                Field.ClientService = _message.ClientService;
                Field.ChatId = _message.ChatId;
                Field.Submitted += Field_Submitted;
                Field.ContentChanged += Field_ContentChanged;
                Field.GotFocus += Field_GotFocus;
                Field.LostFocus += Field_LostFocus;
                Field.KeyDown += Field_KeyDown;

                // The editor makes its input context as it loads, and takes no text before.
                Field.Loaded += Field_Loaded;
                SetEditing(true);
            }
            else
            {
                SetEditing(true);
                Field.FocusInput(FocusState.Programmatic);
            }
        }

        private void Attach_MediaChanged(object sender, EventArgs e)
        {
            UpdateEditing();
            Field.FocusInput(FocusState.Programmatic);
        }

        private void Attach_IsBusyChanged(object sender, EventArgs e)
        {
            UpdateEditing();
        }

        private void Field_Loaded(object sender, RoutedEventArgs e)
        {
            Field.Loaded -= Field_Loaded;
            Field.FocusInput(FocusState.Programmatic);
        }

        private void Field_Submitted(object sender, EventArgs e)
        {
            Submit();
        }

        private void Field_ContentChanged(object sender, EventArgs e)
        {
            UpdateEditing();
        }

        private void Field_GotFocus(object sender, RoutedEventArgs e)
        {
            SetEditing(true);
        }

        private void Field_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateEditing();
        }

        private void Field_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            if (_message == null || _emojiFlyout != null)
            {
                return;
            }

            // Made for each use and dropped on close, rather than held by every poll in the chat.
            _emojiDrawer = new EmojiDrawer
            {
                DataContext = EmojiDrawerViewModel.Create(_message.ClientService.Session),
                Width = 300,
                Height = 360
            };

            _emojiDrawer.ItemClick += EmojiDrawer_ItemClick;
            _emojiDrawer.ViewModel.Update();

            _emojiFlyout = new MenuFlyout();
            _emojiFlyout.MenuFlyoutPresenterStyle = new Style(typeof(MenuFlyoutPresenter));
            _emojiFlyout.MenuFlyoutPresenterStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));
            _emojiFlyout.Items.Add(new MenuFlyoutContent
            {
                Content = _emojiDrawer,
                Padding = new Thickness(0)
            });

            _emojiFlyout.Closed += EmojiFlyout_Closed;
            _emojiFlyout.ShowAt(Emoji, FlyoutPlacementMode.Auto, FlyoutShowMode.Transient, source: Emoji);
        }

        private void EmojiDrawer_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                Field.InsertText(emoji.Value);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                Field.InsertEmoji(sticker);
            }

            Field.FocusInput(FocusState.Programmatic);
        }

        private void EmojiFlyout_Closed(object sender, object e)
        {
            _emojiFlyout.Closed -= EmojiFlyout_Closed;
            _emojiFlyout = null;

            // The view model stays subscribed to the aggregator, and an update it queues later
            // would reach a drawer that is no longer in any tree.
            _emojiDrawer.ItemClick -= EmojiDrawer_ItemClick;
            _emojiDrawer.Deactivate();
            _emojiDrawer.DataContext = null;
            _emojiDrawer = null;

            UpdateEditing();
        }
    }
}
