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
using Telegram.Assets.Icons;
using Telegram.Common;
using Telegram.Controls.Messages.Content;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Composition;
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
        private Border Icon;
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

            _message?.UpdateSelectionCallback(null);
            _message = null;

            _parent = null;
        }

        private void CreateIcon()
        {
            if (Icon != null || !_isSelectionEnabled)
            {
                return;
            }

            var visual = GetVisual(Window.Current.Compositor, out var source, out _props);

            _source = source;
            _previous = visual;

            Icon = GetTemplateChild(nameof(Icon)) as Border;
            ElementCompositionPreview.SetIsTranslationEnabled(Icon, true);
            ElementCompositionPreview.SetElementChildVisual(Icon, visual?.RootVisual);

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
            if (_source != null && Background is SolidColorBrush background)
            {
                _source.SetColorProperty("Color_FF0000", background.Color);
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
                UpdateIcon(IsChecked is true, true);

                if (IsChecked is true)
                {
                    message.Delegate.Select(message);
                }
                else
                {
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

            if (message != null && _templateApplied)
            {
                message.UpdateSelectionCallback(UpdateSelection);

                IsChecked = _isSelectionEnabled && message.Delegate.SelectedItems.ContainsKey(message.Id);
                Presenter.IsHitTestVisible = !_isSelectionEnabled || IsAlbum;

                CreateIcon();
                UpdateIcon(IsChecked is true, false);

                if (Icon != null)
                {
                    var icon = ElementCompositionPreview.GetElementVisual(Icon);
                    icon.Properties.InsertVector3("Translation", new Vector3(_isSelectionEnabled ? 36 : 0, 0, 0));
                }

                if (IsAlbumChild)
                {
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
                        UpdateIcon(IsChecked is true, true);

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
                        UpdateIcon(IsChecked is true, false);

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
            if (message != null && _templateApplied)
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
                UpdateIcon(IsChecked is true, true);
            }
        }


        // This should be held in memory, or animation will stop
        private CompositionPropertySet _props;

        private IAnimatedVisual _previous;
        private IAnimatedVisualSource2 _source;

        private IAnimatedVisual GetVisual(Compositor compositor, out IAnimatedVisualSource2 source, out CompositionPropertySet properties)
        {
            source = new Select();

            if (source == null)
            {
                properties = null;
                return null;
            }

            var visual = source.TryCreateAnimatedVisual(compositor, out _);
            if (visual == null)
            {
                properties = null;
                return null;
            }

            properties = compositor.CreatePropertySet();
            properties.InsertScalar("Progress", 1.0F);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", properties);
            visual.RootVisual.Properties.InsertScalar("Progress", 1.0F);
            visual.RootVisual.Properties.StartAnimation("Progress", progressAnimation);

            return visual;
        }

        private void UpdateIcon(bool selected, bool animate)
        {
            if (_props != null && _previous != null)
            {
                if (animate)
                {
                    var linearEasing = _props.Compositor.CreateLinearEasingFunction();
                    var animation = _props.Compositor.CreateScalarKeyFrameAnimation();
                    animation.Duration = _previous.Duration;
                    animation.InsertKeyFrame(1, selected ? 1 : 0, linearEasing);

                    _props.StartAnimation("Progress", animation);
                }
                else
                {
                    _props.InsertScalar("Progress", selected ? 1.0F : 0.0F);
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
