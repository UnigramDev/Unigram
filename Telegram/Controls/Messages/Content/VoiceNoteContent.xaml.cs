//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Assets.Icons;
using Telegram.Common;
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

namespace Telegram.Controls.Messages.Content
{
    public sealed class VoiceNoteContent : ControlEx, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private long _fileToken;

        public VoiceNoteContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(VoiceNoteContent);

            Disconnected += OnUnloaded;
        }

        public VoiceNoteContent()
        {
            DefaultStyleKey = typeof(VoiceNoteContent);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_message != null)
            {
                _message.PlaybackService.SourceChanged -= OnPlaybackStateChanged;
                _message.PlaybackService.StateChanged -= OnPlaybackStateChanged;
                _message.PlaybackService.PositionChanged -= OnPositionChanged;
            }
        }

        #region InitializeComponent

        private FileButton Button;
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
            Progress = GetTemplateChild(nameof(Progress)) as ProgressVoice;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            Recognize = GetTemplateChild(nameof(Recognize)) as ToggleButton;

            Button.Click += Button_Click;
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

            message.PlaybackService.SourceChanged -= OnPlaybackStateChanged;

            var voiceNote = GetContent(message);
            if (voiceNote == null || !_templateApplied)
            {
                return;
            }

            message.PlaybackService.SourceChanged += OnPlaybackStateChanged;

            Progress.UpdateWaveform(voiceNote);

            if (message.ClientService.IsPremium && message.SchedulingState == null)
            {
                Recognize.Visibility = Visibility.Visible;
            }
            else if (message.ClientService.IsPremiumAvailable && message.SchedulingState == null)
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
                RecognizedText ??= GetTemplateChild(nameof(RecognizedText)) as RichTextBlock;
                RecognizedSpan ??= GetTemplateChild(nameof(RecognizedSpan)) as Run;

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

        private CompositionPropertySet _props;
        private IAnimatedVisual _previous;

        private void LoadPending()
        {
            RecognizedIcon ??= GetTemplateChild(nameof(RecognizedIcon)) as Border;
            RecognizedIcon.Visibility = Visibility.Visible;

            _previous = GetVisual(Window.Current.Compositor, Colors.Black, out _props);
            ElementCompositionPreview.SetElementChildVisual(RecognizedIcon, _previous.RootVisual);
        }

        private void UnloadPending()
        {
            if (RecognizedIcon != null)
            {
                RecognizedIcon.Visibility = Visibility.Collapsed;

                _previous?.Dispose();
                _previous = null;

                _props?.Dispose();
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
            Progress.UpdateWaveform(voiceNote.VoiceNote);
            Progress.Minimum = 0;
            Progress.Maximum = 1;
            Progress.Value = 0.3;

            Subtitle.Text = FormatTime(TimeSpan.FromSeconds(1)) + " / " + FormatTime(TimeSpan.FromSeconds(3));

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
                    Recycle(sender);
                    return;
                }

                UpdateFile(_message, voiceNote.Voice);
            });
        }

        private void OnPositionChanged(IPlaybackService sender, PlaybackPositionChangedEventArgs args)
        {
            var position = args.Position;
            var duration = args.Duration;

            this.BeginOnUIThread(() => UpdatePosition(position, duration));
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
                Progress.Maximum = voiceNote.Duration;
                Progress.Value = message.IsOutgoing || voiceNoteMessage.IsListened ? 0 : voiceNote.Duration;
            }
            else
            {
                Subtitle.Text = voiceNote.GetDuration();
                Progress.Maximum = voiceNote.Duration;
                Progress.Value = 0;
            }
        }

        private void UpdatePosition(TimeSpan position, TimeSpan duration)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.AreTheSame(message.PlaybackService.CurrentItem) /*&& !_pressed*/)
            {
                Subtitle.Text = FormatTime(position) + " / " + FormatTime(duration);
                Progress.Maximum = /*Slider.Maximum =*/ duration.TotalMilliseconds;
                Progress.Value = /*Slider.Value =*/ position.TotalMilliseconds;
            }
        }

        private string FormatTime(TimeSpan span)
        {
            if (span.TotalHours >= 1)
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

            message.PlaybackService.StateChanged -= OnPlaybackStateChanged;
            message.PlaybackService.PositionChanged -= OnPositionChanged;

            if (voiceNote.Voice.Id != file.Id)
            {
                return;
            }

            var canBeDownloaded = file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted;

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive || (canBeDownloaded && message.Delegate.CanBeDownloaded(voiceNote, file)))
            {
                if (canBeDownloaded)
                {
                    _message.ClientService.DownloadFile(file.Id, 32);
                }

                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;
            }
            else if (canBeDownloaded)
            {
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                UpdateDuration();
            }
            else
            {
                if (message.AreTheSame(message.PlaybackService.CurrentItem))
                {
                    if (message.PlaybackService.PlaybackState == PlaybackState.Paused)
                    {
                        Button.SetGlyph(file.Id, MessageContentState.Play);
                    }
                    else
                    {
                        Button.SetGlyph(file.Id, MessageContentState.Pause);
                    }

                    UpdatePosition(message.PlaybackService.Position, message.PlaybackService.Duration);

                    message.PlaybackService.StateChanged += OnPlaybackStateChanged;
                    message.PlaybackService.PositionChanged += OnPositionChanged;
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    UpdateDuration();
                }

                Button.Progress = 1;
            }
        }

        public void Recycle()
        {
            Recycle(_message?.PlaybackService);
        }

        private void Recycle(object sender)
        {
            if (sender is IPlaybackService playback)
            {
                playback.SourceChanged -= OnPlaybackStateChanged;
                playback.StateChanged -= OnPlaybackStateChanged;
                playback.PositionChanged -= OnPositionChanged;
            }

            _message = null;

            UpdateManager.Unsubscribe(this, ref _fileToken);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageVoiceNote)
            {
                return true;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.VoiceNote != null;
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
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.VoiceNote;
            }

            return null;
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
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _message.Delegate.PlayMessage(_message);
            }
            else
            {
                if (_message.AreTheSame(_message.PlaybackService.CurrentItem))
                {
                    if (_message.PlaybackService.PlaybackState == PlaybackState.Paused)
                    {
                        _message.PlaybackService.Play();
                    }
                    else
                    {
                        _message.PlaybackService.Pause();
                    }
                }
                else
                {
                    _message.Delegate.PlayMessage(_message);
                }
            }
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
