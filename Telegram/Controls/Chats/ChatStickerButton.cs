//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Numerics;
using Telegram.Assets.Icons;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Views;
using Telegram.Views.Popups;
using Windows.Devices.Input;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Chats
{
    public class ChatStickerButton : AnimatedGlyphToggleButton
    {
        private Border Icon;

        // This should be held in memory, or animation will stop
        private CompositionPropertySet _props;

        private IAnimatedVisual _previous;
        private IAnimatedVisualSource2 _source;

        public ChatStickerButton()
        {
            DefaultStyleKey = typeof(ChatStickerButton);
            Source = SettingsService.Current.Stickers.SelectedTab;

            Click += Stickers_Click;

            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);

            _animateOnToggle = false;
        }

        #region 

        private DispatcherTimer _stickersTimer;
        private Visual _stickersPanel;
        private Visual _stickersShadow;
        private StickersPanelMode _stickersMode = StickersPanelMode.Collapsed;

        private StickerPanel _controlledPanel;
        public StickerPanel ControlledPanel
        {
            get => _controlledPanel;
            set => SetControlledPanel(value);
        }

        public event EventHandler Redirect;

        public event EventHandler Opening;
        public event EventHandler Closing;

        private void SetControlledPanel(StickerPanel value)
        {
            if (_controlledPanel != null)
            {
                return;
            }

            _controlledPanel = value;

            _stickersPanel = ElementCompositionPreview.GetElementVisual(ControlledPanel.Presenter);
            _stickersShadow = ElementCompositionPreview.GetElementChildVisual(ControlledPanel.Shadow);

            _stickersTimer = new DispatcherTimer();
            _stickersTimer.Interval = TimeSpan.FromMilliseconds(300);
            _stickersTimer.Tick += (s, args) =>
            {
                _stickersTimer.Stop();

                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);

                foreach (var popup in popups)
                {
                    if (popup.Child is MenuFlyoutPresenter or ZoomableMediaPopup)
                    {
                        return;
                    }
                }

                Collapse_Click(null, null);
                Redirect?.Invoke(this, EventArgs.Empty);
            };

            PointerEntered += Stickers_PointerEntered;
            PointerExited += Stickers_PointerExited;

            ControlledPanel.PointerEntered += Stickers_PointerEntered;
            ControlledPanel.PointerExited += Stickers_PointerExited;

            ControlledPanel.AllowFocusOnInteraction = true;
        }

        private void Stickers_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (IsPointerOverEnabled(e.Pointer))
            {
                _stickersTimer.Start();
            }
            else if (_stickersTimer.IsEnabled)
            {
                _stickersTimer.Stop();
            }
        }

        private bool IsPointerOverEnabled(Pointer pointer)
        {
            return pointer?.PointerDeviceType == PointerDeviceType.Mouse && SettingsService.Current.Stickers.IsPointerOverEnabled;
        }

        private bool IsPointerOverDisabled(Pointer pointer)
        {
            return pointer != null && (pointer.PointerDeviceType != PointerDeviceType.Mouse || !SettingsService.Current.Stickers.IsPointerOverEnabled);
        }

        private void Stickers_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (_stickersTimer.IsEnabled)
            {
                _stickersTimer.Stop();
            }

            if (ControlledPanel.Visibility == Visibility.Visible || IsPointerOverDisabled(e?.Pointer))
            {
                return;
            }

            _stickersMode = StickersPanelMode.Overlay;
            IsChecked = false;
            SettingsService.Current.IsSidebarOpen = false;

            //Focus(FocusState.Programmatic);
            Redirect?.Invoke(this, EventArgs.Empty);
            Opening?.Invoke(this, EventArgs.Empty);

            _stickersPanel.Opacity = 0;
            _stickersPanel.Clip = Window.Current.Compositor.CreateInsetClip(48, 48, 0, 0);

            _stickersShadow.Opacity = 0;
            _stickersShadow.Clip = Window.Current.Compositor.CreateInsetClip(48, 48, -48, -4);

            ControlledPanel.Visibility = Visibility.Visible;
            ControlledPanel.Activate();

            var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1);

            var clip = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(0, 48);
            clip.InsertKeyFrame(1, 0);

            var clipShadow = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            clipShadow.InsertKeyFrame(0, 48);
            clipShadow.InsertKeyFrame(1, -48);

            _stickersPanel.StartAnimation("Opacity", opacity);
            _stickersPanel.Clip.StartAnimation("LeftInset", clip);
            _stickersPanel.Clip.StartAnimation("TopInset", clip);

            _stickersShadow.StartAnimation("Opacity", opacity);
            _stickersShadow.Clip.StartAnimation("LeftInset", clipShadow);
            _stickersShadow.Clip.StartAnimation("TopInset", clipShadow);
        }

        private void Collapse_Click(object sender, RoutedEventArgs e)
        {
            if (ControlledPanel.Visibility == Visibility.Collapsed || _stickersMode == StickersPanelMode.Collapsed)
            {
                return;
            }

            _stickersMode = StickersPanelMode.Collapsed;
            SettingsService.Current.IsSidebarOpen = false;

            Closing?.Invoke(this, EventArgs.Empty);

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                ControlledPanel.Visibility = Visibility.Collapsed;
                ControlledPanel.Deactivate();
            };

            var opacity = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 1);
            opacity.InsertKeyFrame(1, 0);

            var clip = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(0, 0);
            clip.InsertKeyFrame(1, 48);

            var clipShadow = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            clipShadow.InsertKeyFrame(0, -48);
            clipShadow.InsertKeyFrame(1, 48);

            _stickersPanel.StartAnimation("Opacity", opacity);
            _stickersPanel.Clip.StartAnimation("LeftInset", clip);
            _stickersPanel.Clip.StartAnimation("TopInset", clip);

            _stickersShadow.StartAnimation("Opacity", opacity);
            _stickersShadow.Clip.StartAnimation("LeftInset", clip);
            _stickersShadow.Clip.StartAnimation("TopInset", clip);

            batch.End();

            IsChecked = false;
            Source = SettingsService.Current.Stickers.SelectedTab;
        }

        private void Stickers_Click(object sender, RoutedEventArgs e)
        {
            if (ControlledPanel.Visibility == Visibility.Collapsed || _stickersMode == StickersPanelMode.Collapsed)
            {
                Stickers_PointerEntered(sender, null);
            }
            else
            {
                Collapse_Click(null, null);
            }
        }

        public StickersPanelMode Mode => _stickersMode;

        public void Collapse()
        {
            if (_stickersMode == StickersPanelMode.Overlay)
            {
                Collapse_Click(null, null);
            }
        }

        #endregion

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_source != null && Foreground is SolidColorBrush foreground)
            {
                _source.SetColorProperty("Color_000000", foreground.Color);
            }
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            Icon = GetTemplateChild(nameof(Icon)) as Border;

            OnSourceChanged(Source, StickersTab.None);
            OnGlyphChanged(Source switch
            {
                StickersTab.Emoji => Icons.Emoji24,
                StickersTab.Stickers => Icons.Sticker24,
                StickersTab.Animations => Icons.Gif24,
                _ => Icons.Sticker24
            });
        }

        protected override bool IsRuntimeCompatible()
        {
            return Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 11);
        }

        #region Source

        public StickersTab Source
        {
            get { return (StickersTab)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(StickersTab), typeof(ChatStickerButton), new PropertyMetadata(StickersTab.None, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatStickerButton)d).OnSourceChanged((StickersTab)e.NewValue, (StickersTab)e.OldValue);
        }

        #endregion

        private void OnSourceChanged(StickersTab newValue, StickersTab oldValue)
        {
            if (newValue == oldValue || Icon == null)
            {
                return;
            }

            if (IsRuntimeCompatible())
            {
                if (_previous != null)
                {
                    _previous.Dispose();
                    _previous = null;
                }

                if (_props != null)
                {
                    _props.Dispose();
                    _props = null;
                }

                var animate = oldValue != StickersTab.None;
                var visual = GetVisual(newValue, oldValue, animate, Window.Current.Compositor, out var source, out _props);

                _source = source;
                _previous = visual;

                if (Foreground is SolidColorBrush brush)
                {
                    source.SetColorProperty("Color_000000", brush.Color);
                }

                ElementCompositionPreview.SetElementChildVisual(Icon, visual?.RootVisual);
            }
            else
            {
                OnGlyphChanged(newValue switch
                {
                    StickersTab.Emoji => Icons.Emoji24,
                    StickersTab.Stickers => Icons.Sticker24,
                    StickersTab.Animations => Icons.Gif24,
                    _ => Icons.Sticker24
                });
            }
        }

        private IAnimatedVisual GetVisual(StickersTab newValue, StickersTab oldValue, bool animate, Compositor compositor, out IAnimatedVisualSource2 source, out CompositionPropertySet properties)
        {
            source = GetVisual(newValue, oldValue);

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
            properties.InsertScalar("Progress", animate ? 0.0F : 1.0F);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", properties);
            visual.RootVisual.Properties.InsertScalar("Progress", animate ? 0.0F : 1.0F);
            visual.RootVisual.Properties.StartAnimation("Progress", progressAnimation);

            visual.RootVisual.Scale = new Vector3(0.1f, 0.1f, 0);

            if (animate)
            {
                var linearEasing = compositor.CreateLinearEasingFunction();
                var animation = compositor.CreateScalarKeyFrameAnimation();
                animation.Duration = visual.Duration;
                animation.InsertKeyFrame(1, 1, linearEasing);

                properties.StartAnimation("Progress", animation);
            }

            return visual;
        }

        private IAnimatedVisualSource2 GetVisual(StickersTab newValue, StickersTab oldValue)
        {
            return newValue switch
            {
                StickersTab.Emoji => oldValue switch
                {
                    StickersTab.Stickers => new StickerToEmoji(),
                    StickersTab.Animations => new GifToEmoji(),
                    _ => new GifToEmoji()
                },
                StickersTab.Stickers => oldValue switch
                {
                    StickersTab.Emoji => new EmojiToSticker(),
                    StickersTab.Animations => new GifToSticker(),
                    _ => new GifToSticker()
                },
                StickersTab.Animations => oldValue switch
                {
                    StickersTab.Emoji => new EmojiToGif(),
                    StickersTab.Stickers => new StickerToGif(),
                    _ => new StickerToGif()
                },
                _ => new EmojiToSticker()
            };
        }

        protected override void OnToggle()
        {
            //base.OnToggle();
        }
    }
}
