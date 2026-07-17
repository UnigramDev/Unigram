//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Assets.Icons;
using Telegram.Common;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    // TODO: turn the whole control into a Button
    public sealed partial class VoiceNoteContent : ControlEx, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;

        public VoiceNoteContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(VoiceNoteContent);
        }

        public VoiceNoteContent()
        {
            DefaultStyleKey = typeof(VoiceNoteContent);
        }

        protected override void OnLoaded()
        {
            var voiceNote = GetContent(_message);
            if (voiceNote == null || !_templateApplied)
            {
                return;
            }

            // Subscribe to the session-lived Playback service only while connected (see AudioContent):
            // bind-path subscriptions leaked controls prepared but never loaded. Visuals were already
            // set during bind; this only (re)establishes the subscriptions, so it stays cheap.
            var playback = LifetimeService.Current.Playback;
            playback.SourceChanged -= OnPlaybackStateChanged;
            playback.SourceChanged += OnPlaybackStateChanged;

            UpdateFile(_message, voiceNote.Voice);
        }

        protected override void OnUnloaded()
        {
            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;
        }

        #region InitializeComponent

        private AutomaticDragHelper ButtonDrag;

        private FileButton Button;
        private Border ViewOnce;
        private ProgressVoice Progress;
        private TextBlock Subtitle;
        private ToggleButton Recognize;
        private RichTextBlock RecognizedText;
        private Run RecognizedSpan;
        private Border RecognizedIcon;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Button = GetTemplateChild(nameof(Button)) as FileButton;
            ViewOnce = GetTemplateChild(nameof(ViewOnce)) as Border;
            Progress = GetTemplateChild(nameof(Progress)) as ProgressVoice;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            Recognize = GetTemplateChild(nameof(Recognize)) as ToggleButton;

            ButtonDrag = new AutomaticDragHelper(Button, true);
            ButtonDrag.StartDetectingDrag();

            Progress.PositionChanged += Progress_PositionChanged;

            Button.Click += Button_Click;
            Button.DragStarting += Button_DragStarting;

            Recognize.Click += Recognize_Click;
            Recognize.Checked += Recognize_Checked;
            Recognize.Unchecked += Recognize_Checked;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;

            var voiceNote = GetContent(message);
            if (voiceNote == null || !_templateApplied)
            {
                return;
            }

            if (IsConnected)
            {
                LifetimeService.Current.Playback.SourceChanged += OnPlaybackStateChanged;
            }

            Progress.UpdateWaveform(voiceNote.Waveform, voiceNote.Duration);
            ViewOnce.Visibility = message.SelfDestructType is MessageSelfDestructTypeImmediately
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (message.ClientService.IsPremium && message.SchedulingState == null && message.SelfDestructType == null)
            {
                Recognize.Visibility = Visibility.Visible;
            }
            else if (message.ClientService.IsPremiumAvailable && message.SchedulingState == null && message.SelfDestructType == null)
            {
                var duration = voiceNote.Duration <= message.ClientService.SpeechRecognitionTrial.MaxMediaDuration;
                var received = message.IsSaved || !message.IsOutgoing;

                Recognize.Visibility = duration && received
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                Recognize.Visibility = Visibility.Collapsed;
            }

            UpdateRecognitionResult(voiceNote.SpeechRecognitionResult);

            UpdateManager.Subscribe(this, message, voiceNote.Voice, ref _fileToken, UpdateFile);
            UpdateFile(message, voiceNote.Voice);
        }

        private void UpdateRecognitionResult(SpeechRecognitionResult result)
        {
            if (result != null && Recognize.IsChecked is true)
            {
                if (RecognizedText == null)
                {
                    RecognizedText = GetTemplateChild(nameof(RecognizedText)) as RichTextBlock;
                    RecognizedSpan = GetTemplateChild(nameof(RecognizedSpan)) as Run;

                    RecognizedText.ContextMenuOpening += RecognizedText_ContextMenuOpening;
                }

                if (result is SpeechRecognitionResultError)
                {
                    RecognizedText.Style = BootStrapper.Current.Resources["InfoCaptionRichTextBlockStyle"] as Style;
                    RecognizedSpan.Text = Strings.NoWordsRecognized;
                    UnloadPending();
                }
                else if (result is SpeechRecognitionResultPending pending)
                {
                    RecognizedText.Style = BootStrapper.Current.Resources["BodyRichTextBlockStyle"] as Style;
                    RecognizedSpan.Text = pending.PartialText.TrimEnd('.');
                    LoadPending();
                }
                else if (result is SpeechRecognitionResultText text)
                {
                    RecognizedText.Style = BootStrapper.Current.Resources["BodyRichTextBlockStyle"] as Style;
                    RecognizedSpan.Text = text.Text;
                    UnloadPending();
                }

                RecognizedText.Visibility = Visibility.Visible;
            }
            else if (RecognizedText != null)
            {
                RecognizedText.Visibility = Visibility.Collapsed;
                UnloadPending();
            }
        }

        private void RecognizedText_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private CompositionPropertySet _props;
        private IAnimatedVisual _previous;

        private void LoadPending()
        {
            RecognizedIcon ??= GetTemplateChild(nameof(RecognizedIcon)) as Border;
            RecognizedIcon.Visibility = Visibility.Visible;

            Color foreground = default;
            if (RecognizedText.Foreground is SolidColorBrush brush)
            {
                foreground = brush.Color;
            }

            _previous = GetVisual(BootStrapper.Current.Compositor, foreground, out _props);
            ElementCompositionPreview.SetElementChildVisual(RecognizedIcon, _previous.RootVisual);
        }

        private void UnloadPending()
        {
            if (RecognizedIcon != null)
            {
                RecognizedIcon.Visibility = Visibility.Collapsed;

                _previous = null;
                _props = null;

                ElementCompositionPreview.SetElementChildVisual(RecognizedIcon, null);
            }
        }

        private IAnimatedVisual GetVisual(Compositor compositor, Color color, out CompositionPropertySet properties)
        {
            var source = new Dots();
            source.Foreground = color;

            var visual = source.TryCreateAnimatedVisual(compositor, out _);
            if (visual == null)
            {
                properties = null;
                return null;
            }

            var linearEasing = compositor.CreateLinearEasingFunction();
            var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = visual.Duration;
            animation.InsertKeyFrame(1, 1, linearEasing);
            animation.IterationBehavior = AnimationIterationBehavior.Forever;

            properties = compositor.CreatePropertySet();
            properties.InsertScalar("Progress", 0);

            var progressAnimation = compositor.CreateExpressionAnimation("_.Progress");
            progressAnimation.SetReferenceParameter("_", properties);
            visual.RootVisual.Properties.InsertScalar("Progress", 0.0F);
            visual.RootVisual.Properties.StartAnimation("Progress", progressAnimation);
            visual.RootVisual.Scale = new System.Numerics.Vector3(16f / 60f);

            properties.StartAnimation("Progress", animation);

            return visual;
        }

        public void Mockup(MessageVoiceNote voiceNote)
        {
            Progress.UpdateWaveform(voiceNote.VoiceNote.Waveform, voiceNote.VoiceNote.Duration);
            Progress.UpdateValue(0.3, 1, false);

            Subtitle.Text = FormatTime(TimeSpan.FromSeconds(1), 0) + " / " + FormatTime(TimeSpan.FromSeconds(3), 0);

            Button.SetGlyph(0, MessageContentState.Pause);
        }

        #region Playback

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(() =>
            {
                var voiceNote = GetContent(_message);
                if (voiceNote == null)
                {
                    Recycle();
                    return;
                }

                UpdateFile(_message, voiceNote.Voice);
            });
        }

        private void OnPositionChanged(IPlaybackService sender, PlaybackPositionChangedEventArgs args)
        {
            var position = args.Position;
            var duration = args.Duration;
            var playing = sender.IsPlaying;

            this.BeginOnUIThread(() => UpdatePosition(position, duration, playing));
        }

        private void UpdateDuration()
        {
            var message = _message;
            if (message == null || !_templateApplied)
            {
                return;
            }

            var voiceNote = GetContent(message);
            if (voiceNote == null)
            {
                return;
            }

            if (message.Content is MessageVoiceNote voiceNoteMessage)
            {
                Subtitle.Text = voiceNote.GetDuration() + (voiceNoteMessage.IsListened ? string.Empty : " ●");
                Progress.UpdateValue(message.IsOutgoing || voiceNoteMessage.IsListened ? 0 : voiceNote.Duration, voiceNote.Duration, false);
            }
            else
            {
                Subtitle.Text = voiceNote.GetDuration();
                Progress.UpdateValue(0, voiceNote.Duration, false);
            }
        }

        private void UpdatePosition(TimeSpan position, TimeSpan duration, bool playing)
        {
            var message = _message;
            if (message == null || Progress.IsScrubbing)
            {
                return;
            }

            if (message.AreTheSame(LifetimeService.Current.Playback.CurrentItem) /*&& !_pressed*/)
            {
                if (duration.TotalSeconds == 0)
                {
                    return;
                }

                Subtitle.Text = FormatTime(duration - position, duration.TotalHours);
                Progress.UpdateValue(position, duration, playing);
            }
        }

        private string FormatTime(TimeSpan span, double totalHours)
        {
            if (totalHours >= 1)
            {
                return span.ToString("h\\:mm\\:ss");
            }
            else
            {
                return span.ToString("mm\\:ss");
            }
        }

        #endregion

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            UpdateDuration();
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private void UpdateFile(MessageViewModel message, File file)
        {
            var voiceNote = GetContent(message);
            if (voiceNote == null || !_templateApplied)
            {
                return;
            }

            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;

            if (voiceNote.Voice.Id != file.Id)
            {
                return;
            }

            if (message.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
            {
                if (LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Pause);
                }

                UpdatePosition(LifetimeService.Current.Playback.Position, LifetimeService.Current.Playback.Duration, LifetimeService.Current.Playback.IsPlaying);

                if (IsConnected)
                {
                    LifetimeService.Current.Playback.StateChanged += OnPlaybackStateChanged;
                    LifetimeService.Current.Playback.PositionChanged += OnPositionChanged;
                }

                Button.Progress = 1;
                Progress.IsEnabled = true;
            }
            else
            {
                var canBeDownloaded = file.Local.CanBeDownloaded
                    && !file.Local.IsDownloadingCompleted
                    && !file.Local.IsDownloadingActive;

                var size = Math.Max(file.Size, file.ExpectedSize);
                if (file.Local.IsDownloadingActive || (canBeDownloaded && message.Delegate.CanBeDownloaded(voiceNote, file)))
                {
                    if (canBeDownloaded)
                    {
                        _message.ClientService.DownloadFile(file.Id, 32);
                    }

                    Button.SetGlyph(file.Id, MessageContentState.Downloading);
                    Button.Progress = (double)file.Local.DownloadedSize / size;

                    UpdateDuration();
                }
                else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed || (message.SendingState is MessageSendingStatePending && !file.Remote.IsUploadingCompleted))
                {
                    Button.SetGlyph(file.Id, MessageContentState.Uploading);
                    Button.Progress = (double)file.Remote.UploadedSize / size;

                    UpdateDuration();
                }
                else if (canBeDownloaded)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Download);
                    Button.Progress = 0;

                    UpdateDuration();
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    UpdateDuration();

                    Button.Progress = 1;
                }

                Progress.IsEnabled = false;
            }
        }

        public void Recycle()
        {
            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;

            _message = null;

            UpdateManager.Unsubscribe(this, ref _fileToken);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageVoiceNote)
            {
                return true;
            }
            else if (content is MessageText text && text.LinkPreview != null && !primary)
            {
                return text.LinkPreview.Type is LinkPreviewTypeVoiceNote;
            }

            return false;
        }

        private VoiceNote GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            if (content is MessageVoiceNote voiceNote)
            {
                return voiceNote.VoiceNote;
            }
            else if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeVoiceNote previewVoiceNote)
            {
                return previewVoiceNote.VoiceNote;
            }

            return null;
        }

        private void Progress_PositionChanged(PlaybackSlider sender, PlaybackSliderPositionChanged args)
        {
            LifetimeService.Current.Playback.Seek(args.NewPosition);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var voiceNote = GetContent(_message);
            if (voiceNote == null)
            {
                return;
            }

            var file = voiceNote.Voice;
            if (file.Local.IsDownloadingActive)
            {
                _message.ClientService.CancelDownloadFile(file);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                if (_message.SendingState is MessageSendingStateFailed or MessageSendingStatePending)
                {
                    _message.ClientService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
                }
                else
                {
                    _message.ClientService.Send(new CancelPreliminaryUploadFile(file.Id));
                }
            }
            else if (_message.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
            {
                if (LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused)
                {
                    LifetimeService.Current.Playback.Play();
                }
                else
                {
                    LifetimeService.Current.Playback.Pause();
                }
            }
            else
            {
                _message.Delegate.PlayMessage(_message);
            }
        }

        private void Button_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            MessageHelper.DragStarting(_message, args);
        }

        private void Recognize_Click(object sender, RoutedEventArgs e)
        {
            if (Recognize.IsChecked is false)
            {
                var voiceNote = GetContent(_message);
                if (voiceNote == null)
                {
                    return;
                }

                if (voiceNote.SpeechRecognitionResult == null)
                {
                    Recognize.IsChecked = _message.Delegate.RecognizeSpeech(_message);
                }
                else
                {
                    Recognize.IsChecked = true;
                    UpdateRecognitionResult(voiceNote.SpeechRecognitionResult);
                }
            }
            else if (RecognizedText != null)
            {
                Recognize.IsChecked = false;
                UpdateRecognitionResult(null);
            }
        }

        private void Recognize_Checked(object sender, RoutedEventArgs e)
        {
            AutomationProperties.SetName(Recognize, Recognize.IsChecked is true
                ? Strings.AccActionCloseTranscription
                : Strings.AccActionOpenTranscription);
        }
    }
}
