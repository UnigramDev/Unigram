using LinqToVisualTree;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
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
                    message.Delegate.Unselect(message.Id);
                }
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.OriginalSource is Grid grid && grid.Name == "LayoutRoot")
            {
                if (_parent == null)
                {
                    _parent = this.Ancestors<LazoListViewItem>().FirstOrDefault();
                }

                if (_parent != null)
                {
                    _parent.PointerPressed(e);
                }
            }
        }

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);

            if (e.OriginalSource is Grid grid && grid.Name == "LayoutRoot")
            {
                if (_parent == null)
                {
                    _parent = this.Ancestors<LazoListViewItem>().FirstOrDefault();
                }

                if (_parent != null)
                {
                    _parent.PointerEntered(e);
                }
            }
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);

            if (e.OriginalSource is Grid grid && grid.Name == "LayoutRoot")
            {
                if (_parent == null)
                {
                    _parent = this.Ancestors<LazoListViewItem>().FirstOrDefault();
                }

                if (_parent != null)
                {
                    _parent.PointerMoved(e);
                }
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (e.OriginalSource is Grid grid && grid.Name == "LayoutRoot")
            {
                if (_parent == null)
                {
                    _parent = this.Ancestors<LazoListViewItem>().FirstOrDefault();
                }

                if (_parent != null)
                {
                    _parent.PointerReleased(e);
                }
            }
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            base.OnPointerCanceled(e);

            if (e.OriginalSource is Grid grid && grid.Name == "LayoutRoot")
            {
                if (_parent == null)
                {
                    _parent = this.Ancestors<LazoListViewItem>().FirstOrDefault();
                }

                if (_parent != null)
                {
                    _parent.PointerCanceled(e);
                }
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
                Presenter.IsHitTestVisible = !_isSelectionEnabled;

                CreateIcon();

                if (Icon != null)
                {
                    AnimatedIcon.SetState(Icon, IsChecked is true ? "Checked" : "Normal");

                    var icon = ElementCompositionPreview.GetElementVisual(Icon);
                    icon.Properties.InsertVector3("Translation", new Vector3(_isSelectionEnabled ? 36 : 0, 0, 0));
                }

                var presenter = ElementCompositionPreview.GetElementVisual(Presenter);
                presenter.Properties.InsertVector3("Translation", new Vector3(_isSelectionEnabled && message.IsChannelPost || !message.IsOutgoing ? 36 : 0, 0, 0));

                var action = message.IsSaved() || message.IsShareable();

                if (message.IsService())
                {
                    Padding = new Thickness(12, 0, 12, 0);
                }
                else if (message.IsSaved() || (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup) && !message.IsChannelPost)
                {
                    if (message.IsOutgoing && !message.IsSaved())
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
                Presenter.IsHitTestVisible = !value;

                CreateIcon();

                var presenter = ElementCompositionPreview.GetElementVisual(Presenter);
                var incoming = message.IsChannelPost || !message.IsOutgoing;

                if (animate)
                {
                    var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                    offset.InsertKeyFrame(0, new Vector3(value ? 0 : 36, 0, 0));
                    offset.InsertKeyFrame(1, new Vector3(value ? 36 : 0, 0, 0));

                    if (Icon != null)
                    {
                        AnimatedIcon.SetState(Icon, IsChecked is true ? "Checked" : "Normal");

                        var icon = ElementCompositionPreview.GetElementVisual(Icon);
                        icon.StartAnimation("Translation", offset);
                    }

                    if (incoming)
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
                        icon.Properties.InsertVector3("Translation", new Vector3(value ? 36 : 0, 0, 0));
                    }

                    presenter.Properties.InsertVector3("Translation", new Vector3(value && incoming ? 36 : 0, 0, 0));
                }
            }
        }

        public void UpdateSelection()
        {
            var message = _message;

            var chat = message?.GetChat();

            if (message != null && chat != null && _templateApplied)
            {
                IsChecked = _isSelectionEnabled && message.Delegate.SelectedItems.ContainsKey(message.Id);
                Presenter.IsHitTestVisible = !_isSelectionEnabled;

                CreateIcon();

                if (Icon != null)
                {
                    AnimatedIcon.SetState(Icon, IsChecked is true ? "Checked" : "Normal");
                }
            }
        }
    }
}
