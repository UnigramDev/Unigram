//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Messages.Content;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public sealed partial class MessageSelector : CheckBox
    {
        private AnimatedIcon Icon;
        private ContentPresenter Presenter;

        private bool _templateApplied;

        private MessageViewModel _message;
        private LazoListViewItem _parent;

        public MessageSelector()
        {
            DefaultStyleKey = typeof(MessageSelector);
        }

        public MessageSelector(MessageViewModel message, UIElement child)
            : this()
        {
            _message = message;
            Content = child;
        }

        public MessageViewModel Message => _message;

        public void Unload()
        {
            if (Content is MessageBubble bubble)
            {
                bubble.UpdateMessage(null);
                bubble.UnregisterEvents();
            }

            _message?.UpdateSelectionCallback(null, null);
            _message = null;

            _parent = null;
        }

        private void CreateIcon()
        {
            if (Icon != null || !_isSelectionEnabled)
            {
                return;
            }

            Icon = GetTemplateChild(nameof(Icon)) as AnimatedIcon;
            ElementCompositionPreview.SetIsTranslationEnabled(Icon, true);

            RegisterPropertyChangedCallback(BackgroundProperty, OnBackgroundChanged);
            OnBackgroundChanged(this, BackgroundProperty);

            if (IsAlbumChild)
            {
                if (_message.Content is MessagePhoto or MessageVideo)
                {
                    Icon.VerticalAlignment = VerticalAlignment.Top;
                    Icon.HorizontalAlignment = HorizontalAlignment.Right;
                    Icon.Margin = new Thickness(0, 4, 6, 0);
                }
                else
                {
                    Icon.VerticalAlignment = VerticalAlignment.Bottom;
                    Icon.HorizontalAlignment = HorizontalAlignment.Left;
                    Icon.Margin = new Thickness(28, 0, 0, 4);
                }

                Grid.SetColumn(Icon, 1);
            }
        }

        private void OnBackgroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            var icon = Icon?.Source as Assets.Icons.Select;
            if (icon != null && Background is SolidColorBrush background)
            {
                icon.Background = background.Color;
            }
        }

        protected override void OnApplyTemplate()
        {
            Presenter = GetTemplateChild(nameof(Presenter)) as ContentPresenter;
            ElementCompositionPreview.SetIsTranslationEnabled(Presenter, true);
            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            _templateApplied = true;
            UpdateMessage(_message);

            base.OnApplyTemplate();
        }

        protected override void OnToggle()
        {
            if (_isSelectionEnabled && _message is MessageViewModel message)
            {
                base.OnToggle();

                CreateIcon();

                if (IsChecked is true)
                {
                    AnimatedIcon.SetState(Icon, "Checked");
                    message.Delegate.Select(message);
                }
                else
                {
                    AnimatedIcon.SetState(Icon, "Normal");
                    message.Delegate.Unselect(message);
                }
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.OriginalSource is Grid grid && grid.Name == "LayoutRoot")
            {
                _parent ??= this.Ancestors<LazoListViewItem>().FirstOrDefault();
                _parent?.PointerPressed(e);
            }
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);

            if (e.OriginalSource is Grid grid && grid.Name == "LayoutRoot")
            {
                _parent ??= this.Ancestors<LazoListViewItem>().FirstOrDefault();
                _parent?.PointerEntered(e);
            }
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);

            if (e.OriginalSource is Grid grid && grid.Name == "LayoutRoot")
            {
                _parent ??= this.Ancestors<LazoListViewItem>().FirstOrDefault();
                _parent?.PointerMoved(e);
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (e.OriginalSource is Grid grid && grid.Name == "LayoutRoot")
            {
                _parent ??= this.Ancestors<LazoListViewItem>().FirstOrDefault();
                _parent?.PointerReleased(e);
            }
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            base.OnPointerCanceled(e);

            if (e.OriginalSource is Grid grid && grid.Name == "LayoutRoot")
            {
                _parent ??= this.Ancestors<LazoListViewItem>().FirstOrDefault();
                _parent?.PointerCanceled(e);
            }
        }

        public void UpdateMessage(MessageViewModel message)
        {
            if (_message?.Id != message?.Id || _message?.ChatId != message?.ChatId)
            {
                _parent = null;
            }

            _message = message;

            var chat = message?.GetChat();

            if (message != null && chat != null && _templateApplied)
            {
                message.UpdateSelectionCallback(this, UpdateSelection);

                IsChecked = _isSelectionEnabled && message.Delegate.SelectedItems.ContainsKey(message.Id);
                Presenter.IsHitTestVisible = !_isSelectionEnabled || IsAlbum;

                CreateIcon();

                if (Icon != null)
                {
                    AnimatedIcon.SetState(Icon, IsChecked is true ? "Checked" : "Normal");

                    var icon = ElementCompositionPreview.GetElementVisual(Icon);
                    icon.Properties.InsertVector3("Translation", new Vector3(_isSelectionEnabled ? 36 : 0, 0, 0));
                }

                if (IsAlbumChild)
                {
                    Padding = new Thickness();

                    if (Icon != null)
                    {
                        if (_message.Content is MessagePhoto or MessageVideo)
                        {
                            Icon.VerticalAlignment = VerticalAlignment.Top;
                            Icon.HorizontalAlignment = HorizontalAlignment.Right;
                            Icon.Margin = new Thickness(0, 4, 6, 0);
                        }
                        else
                        {
                            Icon.VerticalAlignment = VerticalAlignment.Bottom;
                            Icon.HorizontalAlignment = HorizontalAlignment.Left;
                            Icon.Margin = new Thickness(28, 0, 0, 4);
                        }

                        Grid.SetColumn(Icon, 1);
                    }
                }
                else
                {
                    var presenter = ElementCompositionPreview.GetElementVisual(Presenter);
                    presenter.Properties.InsertVector3("Translation", new Vector3(_isSelectionEnabled && (message.IsChannelPost || !message.IsOutgoing) ? 36 : 0, 0, 0));

                    var action = message.IsSaved || message.IsShareable;

                    if (message.IsService())
                    {
                        Padding = new Thickness(12, 0, 12, 0);
                    }
                    else if (message.IsSaved || (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup) && !message.IsChannelPost)
                    {
                        if (message.IsOutgoing && !message.IsSaved)
                        {
                            if (message.Content is MessageSticker or MessageVideoNote)
                            {
                                Padding = new Thickness(12, 0, 12, 0);
                            }
                            else
                            {
                                Padding = new Thickness(50, 0, 12, 0);
                            }
                        }
                        else
                        {
                            if (message.Content is MessageSticker or MessageVideoNote)
                            {
                                Padding = new Thickness(12, 0, 12, 0);
                            }
                            else
                            {
                                Padding = new Thickness(12, 0, action ? 14 : 50, 0);
                            }
                        }
                    }
                    else
                    {
                        if (message.Content is MessageSticker or MessageVideoNote)
                        {
                            Padding = new Thickness(12, 0, 12, 0);
                        }
                        else
                        {
                            if (message.IsOutgoing && !message.IsChannelPost)
                            {
                                Padding = new Thickness(50, 0, 12, 0);
                            }
                            else
                            {
                                Padding = new Thickness(12, 0, action ? 14 : 50, 0);
                            }
                        }
                    }
                }
            }
        }

        private bool _isSelectionEnabled;

        public void UpdateSelectionEnabled(bool value, bool animate)
        {
            if (_isSelectionEnabled == value)
            {
                return;
            }

            _isSelectionEnabled = value;

            if (_message is MessageViewModel message)
            {
                IsChecked = value && message.Delegate.SelectedItems.ContainsKey(message.Id);
                Presenter.IsHitTestVisible = !value || IsAlbum;

                CreateIcon();

                var presenter = ElementCompositionPreview.GetElementVisual(Presenter);
                var incoming = message.IsChannelPost || !message.IsOutgoing;

                if (animate)
                {
                    var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                    offset.InsertKeyFrame(0, new Vector3(value ? 0 : 36, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3(value ? 36 : 0, 0, 0));

                    var scale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                    scale.InsertKeyFrame(0, value ? Vector3.Zero : Vector3.One);
                    scale.InsertKeyFrame(1, value ? Vector3.One : Vector3.Zero);

                    if (Icon != null)
                    {
                        AnimatedIcon.SetState(Icon, IsChecked is true ? "Checked" : "Normal");

                        var icon = ElementCompositionPreview.GetElementVisual(Icon);
                        icon.CenterPoint = new Vector3(12, 12, 0);
                        icon.StartAnimation("Scale", scale);

                        if (!IsAlbumChild)
                        {
                            icon.StartAnimation("Translation", offset);
                        }
                    }

                    if (incoming && !IsAlbumChild)
                    {
                        presenter.StartAnimation("Translation", offset);
                    }
                    else
                    {
                        presenter.Properties.InsertVector3("Translation", Vector3.Zero);
                    }
                }
                else
                {
                    if (Icon != null)
                    {
                        var icon = ElementCompositionPreview.GetElementVisual(Icon);
                        icon.Properties.InsertVector3("Translation", new Vector3(value && !IsAlbumChild ? 36 : 0, 0, 0));
                        icon.Scale = value ? Vector3.One : Vector3.Zero;
                    }

                    if (!IsAlbumChild)
                    {
                        presenter.Properties.InsertVector3("Translation", new Vector3(value && incoming ? 36 : 0, 0, 0));
                    }
                }
            }

            if (Content is MessageBubble bubble && bubble.MediaTemplateRoot is AlbumContent album)
            {
                album.UpdateSelectionEnabled(value, animate);
            }
        }

        public void UpdateSelection()
        {
            var message = _message;

            var chat = message?.GetChat();

            if (message != null && chat != null && _templateApplied)
            {
                bool selected;
                if (message.Content is MessageAlbum album)
                {
                    selected = album.Messages.All(x => message.Delegate.SelectedItems.ContainsKey(x.Id));
                }
                else
                {
                    selected = message.Delegate.SelectedItems.ContainsKey(message.Id);
                }

                IsChecked = _isSelectionEnabled && selected;
                Presenter.IsHitTestVisible = !_isSelectionEnabled || IsAlbum;

                CreateIcon();

                if (Icon != null)
                {
                    AnimatedIcon.SetState(Icon, IsChecked is true ? "Checked" : "Normal");
                }
            }
        }

        private bool IsAlbum => _message?.Content is MessageAlbum;

        private bool IsAlbumChild => _message?.Content is not MessageAlbum && _message.MediaAlbumId != 0;

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MessageSelectorAutomationPeer(this);
        }
    }

    public class MessageSelectorAutomationPeer : CheckBoxAutomationPeer
    {
        private readonly MessageSelector _owner;
        private readonly IClientService _clientService;

        public MessageSelectorAutomationPeer(MessageSelector owner)
            : base(owner)
        {
            _owner = owner;
        }

        public MessageSelectorAutomationPeer(MessageSelector owner, IClientService clientService)
            : base(owner)
        {
            _owner = owner;
            _clientService = clientService;
        }

        protected override string GetNameCore()
        {
            if (_owner.Content is MessageBubble bubble)
            {
                return bubble.GetAutomationName() ?? base.GetNameCore();
            }
            else if (_owner.ContentTemplateRoot is MessageBubble child)
            {
                return child.GetAutomationName() ?? base.GetNameCore();
            }
            else if (_owner.Content is Message message && _clientService != null)
            {
                return Automation.GetDescription(_clientService, message);
            }

            return base.GetNameCore();
        }
    }
}
