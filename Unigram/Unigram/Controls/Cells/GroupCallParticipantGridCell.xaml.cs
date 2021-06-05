using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Native.Calls;
using Unigram.Services;
using Unigram.Views;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Cells
{
    public sealed partial class GroupCallParticipantGridCell : HyperlinkButton
    {
        private GroupCallParticipant _participant;
        private GroupCallParticipantVideoInfo _videoInfo;

        private SpriteVisual _pausedVisual;
        private CompositionEffectBrush _pausedBrush;

        private readonly bool _screenSharing;

        public GroupCallParticipantGridCell(ICacheService cacheService, GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo, bool screenSharing)
        {
            _screenSharing = screenSharing;

            InitializeComponent();
            UpdateGroupCallParticipant(cacheService, participant, videoInfo);

            if (screenSharing)
            {
                ScreenSharing.Text = Icons.SmallScreencastFilled;
            }

            var pin = ElementCompositionPreview.GetElementVisual(PinRoot);
            pin.Opacity = 0;
        }

        public bool IsMatch(GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo)
        {
            return participant != null && participant.ParticipantId.IsEqual(ParticipantId) && _videoInfo.EndpointId == _videoInfo.EndpointId;
        }

        public CanvasControl Surface
        {
            get => CanvasRoot.Child as CanvasControl;
            set => CanvasRoot.Child = value;
        }

        public VoipVideoChannelQuality Quality => ActualHeight switch
        {
            double h when h >= 720 => VoipVideoChannelQuality.Full,
            double h when h >= 360 => VoipVideoChannelQuality.Medium,
            _ => VoipVideoChannelQuality.Thumbnail
        };

        public Stretch GetStretch(ParticipantsGridMode mode, bool list)
        {
            if (_screenSharing || IsSelected || IsPinned)
            {
                return Stretch.Uniform;
            }

            return mode == ParticipantsGridMode.Compact || list ? Stretch.Fill : Stretch.UniformToFill;
        }

        public GroupCallParticipant Participant => _participant;
        public MessageSender ParticipantId => _participant.ParticipantId;

        public GroupCallParticipantVideoInfo VideoInfo => _videoInfo;
        public string EndpointId => _videoInfo.EndpointId;

        public bool IsScreenSharing => _screenSharing;

        public bool IsSelected { get; set; }

        public bool IsPinned
        {
            get => Pin.IsChecked == true;
            set => Pin.IsChecked = value;
        }

        public bool IsList
        {
            set => LayoutRoot.Constraint = value ? new Size(16, 9) : null;
        }

        public event EventHandler TogglePinned;

        public void UpdateGroupCallParticipant(ICacheService cacheService, GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo)
        {
            _participant = participant;
            _videoInfo = videoInfo;

            Tag = participant;

            ShowHidePaused(videoInfo.IsPaused);

            if (cacheService.TryGetUser(participant.ParticipantId, out User user))
            {
                Title.Text = user.GetFullName();
            }
            else if (cacheService.TryGetChat(participant.ParticipantId, out Chat chat))
            {
                Title.Text = cacheService.GetTitle(chat);
            }

            if (participant.IsSpeaking)
            {
                Speaking.BorderBrush = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) };
                Glyph.Text = Icons.MicOn;
            }
            else
            {
                Speaking.BorderBrush = null;
                Glyph.Text = participant.IsMutedForAllUsers || participant.IsMutedForCurrentUser ? Icons.MicOff : Icons.MicOn;
            }
        }

        private bool _infoCollapsed;

        public void ShowHideInfo(bool show)
        {
            if (_infoCollapsed == !show)
            {
                return;
            }

            _infoCollapsed = !show;

            var anim = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            //anim.InsertKeyFrame(0, show ? 0 : 1);
            anim.InsertKeyFrame(1, show ? 1 : 0);

            var info = ElementCompositionPreview.GetElementVisual(Info);
            info.StartAnimation("Opacity", anim);

            if (IsSelected || !show)
            {
                var pin = ElementCompositionPreview.GetElementVisual(PinRoot);
                pin.StartAnimation("Opacity", anim);
            }
        }

        private bool _pausedCollapsed;

        private void ShowHidePaused(bool show)
        {
            if (_pausedCollapsed == !show)
            {
                return;
            }

            _pausedCollapsed = !show;

            if (show)
            {
                var paused = ElementCompositionPreview.GetElementVisual(PausedRoot);

                var graphicsEffect = new GaussianBlurEffect
                {
                    Name = "Blur",
                    BlurAmount = 10,
                    BorderMode = EffectBorderMode.Hard,
                    Source = new CompositionEffectSourceParameter("backdrop")
                };

                var effectFactory = Window.Current.Compositor.CreateEffectFactory(graphicsEffect, new[] { "Blur.BlurAmount" });
                var effectBrush = effectFactory.CreateBrush();
                var backdrop = Window.Current.Compositor.CreateBackdropBrush();
                effectBrush.SetSourceParameter("backdrop", backdrop);

                _pausedBrush = effectBrush;
                _pausedVisual = Window.Current.Compositor.CreateSpriteVisual();
                _pausedVisual.Size = this.GetActualSize();
                _pausedVisual.Brush = effectBrush;

                ElementCompositionPreview.SetElementChildVisual(CanvasRoot, _pausedVisual);
                PausedRoot.Visibility = Visibility.Visible;
                Scrim.Visibility = Visibility.Collapsed;

                //var blur = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                //blur.Duration = TimeSpan.FromMilliseconds(300);
                //blur.InsertKeyFrame(1, 10);

                //var anim = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                //anim.InsertKeyFrame(0, show ? 0 : 1);
                //anim.InsertKeyFrame(1, show ? 1 : 0);

                //paused.StartAnimation("Opacity", anim);
                //effectBrush.Properties.StartAnimation("Blur.BlurAmount", blur);
            }
            else
            {
                ElementCompositionPreview.SetElementChildVisual(CanvasRoot, null);
                PausedRoot.Visibility = Visibility.Collapsed;
                Scrim.Visibility = Visibility.Visible;

                _pausedBrush?.Dispose();
                _pausedBrush = null;

                _pausedVisual?.Dispose();
                _pausedVisual = null;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_pausedVisual != null)
            {
                _pausedVisual.Size = finalSize.ToVector2();
            }

            return base.ArrangeOverride(finalSize);
        }


        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            TogglePinned?.Invoke(this, EventArgs.Empty);
        }
    }
}
