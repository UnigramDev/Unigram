//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System.Numerics;
using Telegram.Assets.Icons;
using Telegram.Converters;
using Telegram.Services.Settings;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
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
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);

            _animateOnToggle = false;
        }

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
