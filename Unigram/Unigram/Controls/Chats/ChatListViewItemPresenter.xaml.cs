using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Chats
{
    public sealed partial class ChatListViewItemPresenter : CheckBox
    {
        private Grid CheckMark;
        private ContentPresenter ContentPresenter;

        private Visual _checkMark;
        private Visual _content;

        private bool _hasLeftPadding;

        private ChatListView _listView;

        private MessageViewModel _message;

        public ChatListViewItemPresenter()
        {
            this.InitializeComponent();
        }

        public void UpdateMessage(ChatListView listView, MessageViewModel message)
        {
            _listView = listView;
            _message = message;

            var chat = message.GetChat();
            var action = message.IsSaved() || message.IsShareable();

            if (message.IsSaved() || (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup) && !message.IsChannelPost)
            {
                if (message.IsOutgoing && !message.IsSaved())
                {
                    if (message.Content is MessageSticker || message.Content is MessageVideoNote)
                    {
                        _hasLeftPadding = true;
                        Content.Padding = new Thickness(12, 0, 12, 0);
                    }
                    else
                    {
                        _hasLeftPadding = true;
                        Content.Padding = new Thickness(50, 0, 12, 0);
                    }
                }
                else
                {
                    if (message.Content is MessageSticker || message.Content is MessageVideoNote)
                    {
                        _hasLeftPadding = false;
                        Content.Padding = new Thickness(12, 0, 12, 0);
                    }
                    else
                    {
                        _hasLeftPadding = false;
                        Content.Padding = new Thickness(12, 0, action ? 14 : 50, 0);
                    }
                }
            }
            else
            {
                if (message.Content is MessageSticker || message.Content is MessageVideoNote)
                {
                    _hasLeftPadding = message.IsOutgoing && !message.IsChannelPost;
                    Content.Padding = new Thickness(12, 0, 12, 0);
                }
                else
                {
                    if (message.IsOutgoing && !message.IsChannelPost)
                    {
                        _hasLeftPadding = true;
                        Content.Padding = new Thickness(50, 0, 12, 0);
                    }
                    else
                    {
                        _hasLeftPadding = false;
                        Content.Padding = new Thickness(12, 0, action ? 14 : 50, 0);
                    }
                }
            }

            UpdatePhoto(message);
            UpdateAction(message);
        }

        private void UpdatePhoto(MessageViewModel message)
        {
            var visible = IsPhotoVisible(message);
            if (visible && Photo == null)
            {
                FindName(nameof(Photo));
            }
            else if (!visible && Photo == null)
            {
                return;
            }

            Photo.Visibility = message.IsLast && visible ? Visibility.Visible : Visibility.Collapsed;
            PhotoColumn.Width = new GridLength(visible ? 38 : 0, GridUnitType.Pixel);

            if (!visible)
            {
                Photo.Source = null;
                return;
            }

            if (message.IsSaved())
            {
                if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                {
                    var user = message.ProtoService.GetUser(fromUser.SenderUserId);
                    if (user != null)
                    {
                        Photo.Source = PlaceholderHelper.GetUser(message.ProtoService, user, 30);
                    }
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel post)
                {
                    var chat = message.ProtoService.GetChat(post.ChatId);
                    if (chat != null)
                    {
                        Photo.Source = PlaceholderHelper.GetChat(message.ProtoService, chat, 30);
                    }
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser fromHiddenUser)
                {
                    Photo.Source = PlaceholderHelper.GetNameForUser(fromHiddenUser.SenderName, 30);
                }
            }
            else
            {
                var user = message.GetSenderUser();
                if (user != null)
                {
                    Photo.Source = PlaceholderHelper.GetUser(message.ProtoService, user, 30);
                }
            }
        }

        private void UpdateAction(MessageViewModel message)
        { 
            var button = Action.Child as GlyphButton;
            button.Tag = message;

            if (message.IsSaved())
            {
                if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser)
                {
                    Action.Visibility = Visibility.Collapsed;
                }
                else
                {
                    button.Glyph = "\uE72A";
                    Action.Visibility = Visibility.Visible;

                    Automation.SetToolTip(button, Strings.Resources.AccDescrOpenChat);
                }
            }
            else if (message.IsShareable())
            {
                button.Glyph = "\uE72D";
                Action.Visibility = Visibility.Visible;

                Automation.SetToolTip(button, Strings.Resources.ShareFile);
            }
            else
            {
                Action.Visibility = Visibility.Collapsed;
            }
        }



        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            CheckMark = GetTemplateChild("CheckMark") as Grid;
            ContentPresenter = GetTemplateChild("ContentPresenter") as ContentPresenter;

            _checkMark = ElementCompositionPreview.GetElementVisual(CheckMark);
            _content = ElementCompositionPreview.GetElementVisual(ContentPresenter);
        }

        private bool _isSelectionEnabled;
        public void SetSelectionEnabled(bool value, bool animated)
        {
            if (_isSelectionEnabled == value)
            {
                animated = false;
                //return;
            }

            _isSelectionEnabled = value;

            //if (value && CheckMark == null)
            //{
            //    FindName(nameof(CheckMark));

            //    _checkMark = ElementCompositionPreview.GetElementVisual(CheckMark);
            //    _content = ElementCompositionPreview.GetElementVisual(Content);
            //}

            if (value)
            {
                IsChecked = _message.Delegate.IsMessageSelected(_message.Id);
            }

            Content.IsHitTestVisible = !value;

            //SetColumn(CheckMark, value ? 1 : 0);
            //SetColumn(Content, value && !_hasLeftPadding ? 2 : 1);

            if (animated)
            {
                var anim1 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                anim1.InsertKeyFrame(0, value ? 0 : 32);
                anim1.InsertKeyFrame(1, value ? 32 : 0);

                _checkMark.StartAnimation("Offset.X", anim1);
            }
            else
            {
                var offset = _checkMark.Offset;
                offset.X = value ? 32 : 0;

                _checkMark.Offset = offset;
            }

            if (_hasLeftPadding)
            {
                var offset = _content.Offset;
                offset.X = 0;

                _content.Offset = offset;
            }
            else
            {
                if (animated)
                {
                    var anim2 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                    anim2.InsertKeyFrame(0, value ? 0 : 32);
                    anim2.InsertKeyFrame(1, value ? 32 : 0);

                    _content.StartAnimation("Offset.X", anim2);
                }
                else
                {
                    var offset = _content.Offset;
                    offset.X = value ? 32 : 0;

                    _content.Offset = offset;
                }
            }
        }

        public void SetSelected(bool selected)
        {
            IsChecked = selected && _isSelectionEnabled;

            if (selected)
            {
                _message.Delegate.SelectMessage(_message);
            }
            else
            {
                _message.Delegate.DeselectMessage(_message);
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            //if ((e.OriginalSource is ListViewItemPresenter || (e.OriginalSource is FrameworkElement element && element.Parent == null && VisualTreeHelper.GetParent(element) is CheckBox)) && !CantSelect())
            {
                _listView.OnPointerPressed(_listView.ContainerFromItem(_message) as LazoListViewItem, e);
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            //if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource is FrameworkElement element && element.Parent == null && VisualTreeHelper.GetParent(element) is CheckBox))
            {
                _listView.OnPointerEntered(_listView.ContainerFromItem(_message) as LazoListViewItem, e);
            }

            base.OnPointerEntered(e);
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            //if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource is FrameworkElement element && element.Parent == null && VisualTreeHelper.GetParent(element) is CheckBox))
            {
                _listView.OnPointerMoved(_listView.ContainerFromItem(_message) as LazoListViewItem, e);
            }

            base.OnPointerMoved(e);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);

            ReleasePointerCapture(e.Pointer);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            //if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource is FrameworkElement element && element.Parent == null && VisualTreeHelper.GetParent(element) is CheckBox))
            {
                _listView.OnPointerReleased(_listView.ContainerFromItem(_message) as LazoListViewItem, e);
            }

            base.OnPointerReleased(e);
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            //if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource is FrameworkElement element && element.Parent == null && VisualTreeHelper.GetParent(element) is CheckBox))
            {
                _listView.OnPointerCanceled(_listView.ContainerFromItem(_message) as LazoListViewItem, e);
            }

            base.OnPointerCanceled(e);
        }

        protected override void OnToggle()
        {
            if (_isSelectionEnabled)
            {
                base.OnToggle();

                if (IsChecked == true)
                {
                    _message.Delegate.SelectMessage(_message);
                }
                else
                {
                    _message.Delegate.DeselectMessage(_message);
                }
            }
        }



        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.IsSaved())
            {
                if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                {
                    message.Delegate.OpenUser(fromUser.SenderUserId);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel post)
                {
                    // TODO: verify if this is sufficient
                    message.Delegate.OpenChat(post.ChatId);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser)
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.HidAccount, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
            else if (message.IsChannelPost)
            {
                message.Delegate.OpenChat(message.ChatId);
            }
            else
            {
                message.Delegate.OpenUser(message.SenderUserId);
            }
        }

        private void Action_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.IsSaved())
            {
                if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                {
                    message.Delegate.OpenChat(message.ForwardInfo.FromChatId, message.ForwardInfo.FromMessageId);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel post)
                {
                    message.Delegate.OpenChat(post.ChatId, post.MessageId);
                }
            }
            else
            {
                //message.Delegate.ShareMessage(message);
                //ViewModel.MessageShareCommand.Execute(message);
            }
        }



        private bool IsPhotoVisible(MessageViewModel message)
        {
            if (message.IsChannelPost)
            {
                return false;
            }
            else if (message.IsSaved())
            {
                return true;
            }
            else if (message.IsOutgoing)
            {
                return false;
            }

            var chat = message.GetChat();
            if (chat != null && chat.Type is ChatTypeSupergroup || chat.Type is ChatTypeBasicGroup)
            {
                return true;
            }

            return false;
        }
    }

}
