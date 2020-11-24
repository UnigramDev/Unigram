using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Media.Playback;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class VoiceNoteContent : Grid, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public VoiceNoteContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public VoiceNoteContent()
        {
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            message.PlaybackService.PropertyChanged -= OnCurrentItemChanged;
            message.PlaybackService.PlaybackStateChanged -= OnPlaybackStateChanged;
            message.PlaybackService.QuantumChanged -= OnPositionChanged;
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            message.PlaybackService.PropertyChanged -= OnCurrentItemChanged;
            message.PlaybackService.PropertyChanged += OnCurrentItemChanged;

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            Progress.UpdateWave(voiceNote);

            //UpdateDuration();
            UpdateFile(message, voiceNote.Voice);
        }

        public void Mockup(MessageVoiceNote voiceNote)
        {
            Progress.UpdateWave(voiceNote.VoiceNote);
            Progress.Minimum = 0;
            Progress.Maximum = 1;
            Progress.Value = 0.3;

            Subtitle.Text = FormatTime(TimeSpan.FromSeconds(1)) + " / " + FormatTime(TimeSpan.FromSeconds(3));

            Button.SetGlyph(0, MessageContentState.Pause);
        }

        #region Playback

        private void OnCurrentItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var voiceNote = GetContent(_message?.Content);
            if (voiceNote == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, voiceNote.Voice));
        }

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            var voiceNote = GetContent(_message?.Content);
            if (voiceNote == null)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(_message, voiceNote.Voice));
        }

        private VoiceBlobDrawable _drawable;

        private void OnPositionChanged(IPlaybackService sender, float[] args)
        {
            this.BeginOnUIThread(UpdatePosition);

            //var canvas = Canvas;
            //if (canvas != null)
            //{
            //    _drawable ??= new VoiceBlobDrawable(canvas.Invalidate);
            //    _drawable.SetWaveform(true, true, args);
            //}
        }

        private void UpdateDuration()
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            var voiceNote = GetContent(message.Content);
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

        private void UpdatePosition()
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            if (message.AreEqual(message.PlaybackService.CurrentItem) /*&& !_pressed*/)
            {
                Subtitle.Text = FormatTime(message.PlaybackService.Position) + " / " + FormatTime(message.PlaybackService.Duration);
                Progress.Maximum = /*Slider.Maximum =*/ message.PlaybackService.Duration.TotalMilliseconds;
                Progress.Value = /*Slider.Value =*/ message.PlaybackService.Position.TotalMilliseconds;

                //if (Canvas == null)
                //{
                //    FindName(nameof(Canvas));
                //}
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

        public void UpdateFile(MessageViewModel message, File file)
        {
            message.PlaybackService.PlaybackStateChanged -= OnPlaybackStateChanged;
            message.PlaybackService.QuantumChanged -= OnPositionChanged;

            var voiceNote = GetContent(message.Content);
            if (voiceNote == null)
            {
                return;
            }

            else if (voiceNote.Voice.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;
            }
            else if (file.Remote.IsUploadingActive || message.SendingState is MessageSendingStateFailed)
            {
                //Button.Glyph = Icons.Cancel;
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                //Button.Glyph = Icons.Download;
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;

                if (message.Delegate.CanBeDownloaded(message))
                {
                    _message.ProtoService.DownloadFile(file.Id, 32);
                }
            }
            else
            {
                if (message.AreEqual(message.PlaybackService.CurrentItem))
                {
                    if (message.PlaybackService.PlaybackState == MediaPlaybackState.Playing)
                    {
                        //Button.Glyph = Icons.Pause;
                        Button.SetGlyph(file.Id, MessageContentState.Pause);
                    }
                    else
                    {
                        //Button.Glyph = Icons.Play;
                        Button.SetGlyph(file.Id, MessageContentState.Play);
                    }

                    UpdatePosition();

                    message.PlaybackService.PlaybackStateChanged += OnPlaybackStateChanged;
                    message.PlaybackService.QuantumChanged += OnPositionChanged;
                }
                else
                {
                    if (_drawable != null)
                    {
                        _drawable.SetWaveform(false, true, null);
                    }

                    //Button.Glyph = Icons.Play;
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    UpdateDuration();
                }

                Button.Progress = 1;
            }
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

        private VoiceNote GetContent(MessageContent content)
        {
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
            var voiceNote = GetContent(_message?.Content);
            if (voiceNote == null)
            {
                return;
            }

            var file = voiceNote.Voice;
            if (file.Local.IsDownloadingActive)
            {
                _message.ProtoService.CancelDownloadFile(file.Id);
            }
            else if (file.Remote.IsUploadingActive || _message.SendingState is MessageSendingStateFailed)
            {
                _message.ProtoService.Send(new DeleteMessages(_message.ChatId, new[] { _message.Id }, true));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                //_message.ProtoService.DownloadFile(file.Id, 32);
                _message.PlaybackService.Enqueue(_message.Get());
            }
            else
            {
                if (_message.AreEqual(_message.PlaybackService.CurrentItem))
                {
                    if (_message.PlaybackService.PlaybackState == MediaPlaybackState.Playing)
                    {
                        _message.PlaybackService.Pause();
                    }
                    else
                    {
                        _message.PlaybackService.Play();
                    }
                }
                else
                {
                    _message.Delegate.PlayMessage(_message);
                }
            }
        }

        private void OnDraw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            _drawable?.Draw(args.DrawingSession, 30, 30, false);
        }
    }

    public class VoiceBlobDrawable
    {
        private readonly CircleBezierDrawable[] _drawables;

        private readonly int[] _tmpWaveform = new int[3];
        private readonly float[] _animateTo = new float[8];
        private readonly float[] _current = new float[8];
        private readonly float[] _dt = new float[8];

        private float _idleScale;
        private bool _idleScaleInc;

        private readonly Color p1;

        //private View parentView;
        private readonly Random _random = new Random();

        public const float IDLE_RADIUS = 6 * 0.33f;
        public const float WAVE_RADIUS = 12 * 0.36f;
        public const float ANIMATION_DURATION = 120;
        public const byte ALPHA = 61;

        private readonly Action _invalidate;

        public VoiceBlobDrawable(Action invalidate)
        {
            _invalidate = invalidate;

            _drawables = new CircleBezierDrawable[2];
            for (int i = 0; i < 2; i++)
            {
                CircleBezierDrawable drawable = _drawables[i] = new CircleBezierDrawable(6);
                drawable.IdleStateDiff = 0;
                drawable.Radius = 24;
                drawable.RadiusDiff = 0;
                drawable.randomK = 1f;
            }

            p1 = Color.FromArgb(ALPHA, 0x78, 0xC6, 0x7F);
        }

        const int MAX_SAMPLE_SUM = 6;

        private float[] _lastAmplitude = new float[MAX_SAMPLE_SUM];
        private int _lastAmplitudeCount;
        private int _lastAmplitudePointer;


        public void SetWaveform(bool playing, bool animate, float[] waveform)
        {
            if (!playing && !animate)
            {
                for (int i = 0; i < 8; i++)
                {
                    _animateTo[i] = _current[i] = 0;
                }
                return;
            }

            bool idleState = waveform != null && waveform[6] == 0;
            float amplitude = waveform == null ? 0 : waveform[6];

            if (waveform != null && amplitude > 0.4)
            {
                _lastAmplitude[_lastAmplitudePointer] = amplitude;
                _lastAmplitudePointer++;
                if (_lastAmplitudePointer > MAX_SAMPLE_SUM - 1)
                {
                    _lastAmplitudePointer = 0;
                }
                _lastAmplitudeCount++;
            }
            else
            {
                _lastAmplitudeCount = 0;
            }

            if (idleState)
            {
                for (int i = 0; i < 6; i++)
                {
                    waveform[i] = ((_random.Next() % 500) / 1000f);
                }
            }
            float duration = idleState ? ANIMATION_DURATION * 2 : ANIMATION_DURATION;
            if (_lastAmplitudeCount > MAX_SAMPLE_SUM)
            {
                float a = 0;
                for (int i = 0; i < MAX_SAMPLE_SUM; i++)
                {
                    a += _lastAmplitude[i];
                }
                a /= (float)MAX_SAMPLE_SUM;
                if (a > 0.52f)
                {
                    duration -= ANIMATION_DURATION * (a - 0.40f);
                }
            }
            for (int i = 0; i < 7; i++)
            {
                if (waveform == null)
                {
                    _animateTo[i] = 0;
                }
                else
                {
                    _animateTo[i] = waveform[i];
                }
                if (false) //(parentView == null)
                {
                    _current[i] = _animateTo[i];
                }
                else if (i == 6)
                {
                    _dt[i] = (_animateTo[i] - _current[i]) / (ANIMATION_DURATION + 80);
                }
                else
                {
                    _dt[i] = (_animateTo[i] - _current[i]) / duration;
                }
            }

            _animateTo[7] = playing ? 1f : 0f;
            _dt[7] = (_animateTo[7] - _current[7]) / 120;

            _invalidate();
        }

        private float _rotation;

        public void Draw(CanvasDrawingSession canvas, float cx, float cy, bool outOwner)
        {
            //if (outOwner)
            //{
            //    p1.setColor(Theme.getColor(Theme.key_chat_outLoader));
            //    p1.setAlpha(ALPHA);
            //}
            //else
            //{
            //    p1.setColor(Theme.getColor(Theme.key_chat_inLoader));
            //    p1.setAlpha(ALPHA);
            //}
            for (int i = 0; i < 8; i++)
            {
                if (_animateTo[i] != _current[i])
                {
                    _current[i] += _dt[i] * 16;
                    if ((_dt[i] > 0 && _current[i] > _animateTo[i]) || (_dt[i] < 0 && _current[i] < _animateTo[i]))
                    {
                        _current[i] = _animateTo[i];
                    }
                    _invalidate();
                }
            }

            if (_idleScaleInc)
            {
                _idleScale += 0.02f;
                if (_idleScale > 1f)
                {
                    _idleScaleInc = false;
                    _idleScale = 1f;
                }
            }
            else
            {
                _idleScale -= 0.02f;
                if (_idleScale < 0)
                {
                    _idleScaleInc = true;
                    _idleScale = 0;
                }
            }

            float enterProgress = _current[7];
            float radiusProgress = _current[6] * _current[0];

            if (enterProgress == 0 && radiusProgress == 0)
            {
                return;
            }
            // float idleProgress = radiusProgress > 0.4f ? 0 : (1f - radiusProgress / 0.4f);

            for (int i = 0; i < 3; i++)
            {
                _tmpWaveform[i] = (int)(_current[i] * WAVE_RADIUS);
            }

            //drawables[0].idleStateDiff = enterProgress * idleProgress * IDLE_AMPLITUDE;
            //drawables[1].idleStateDiff = enterProgress * idleProgress * IDLE_AMPLITUDE;

            _drawables[0].SetAdditionals(_tmpWaveform);

            for (int i = 0; i < 3; i++)
            {
                _tmpWaveform[i] = (int)(_current[i + 3] * WAVE_RADIUS);
            }
            _drawables[1].SetAdditionals(_tmpWaveform);
            float radius = 22 + 4 * radiusProgress + IDLE_RADIUS * enterProgress;

            if (radius > 26)
            {
                radius = 26;
            }
            _drawables[0].Radius = _drawables[1].Radius = radius;

            _rotation += 0.6f;

            float s = 1f + 0.04f * _idleScale;
            var rotate = Matrix3x2.CreateRotation(ToRadians(_rotation), new Vector2(cx, cy));
            var scale = Matrix3x2.CreateScale(s, s, new Vector2(cx, cy));
            canvas.Transform = Matrix3x2.Multiply(rotate, scale);
            _drawables[0].Draw(cx, cy, canvas, p1);

            s = 1f + 0.04f * (1f - _idleScale);
            rotate = Matrix3x2.CreateRotation(ToRadians(_rotation + 60), new Vector2(cx, cy));
            scale = Matrix3x2.CreateScale(s, s, new Vector2(cx, cy));
            canvas.Transform = Matrix3x2.Multiply(rotate, scale);
            _drawables[1].Draw(cx, cy, canvas, p1);
        }

        private float ToRadians(float degrees)
        {
            float radians = (MathF.PI / 180) * degrees;
            return radians;
        }

        //public void setParentView(ChatMessageCell parentView)
        //{
        //    this.parentView = parentView;
        //}

        //public View getParentView()
        //{
        //    return parentView;
        //}
    }

    public class CircleBezierDrawable
    {
        private readonly Vector2[] _pointStart = new Vector2[2];
        private readonly Vector2[] _pointEnd = new Vector2[2];
        private Matrix3x2 _m;

        private readonly float L;
        private readonly int N;

        public float IdleStateDiff = 0f;
        public float Radius;
        public float RadiusDiff;
        public float CubicBezierK = 1f;

        private readonly Random _random = new Random();
        private readonly float[] _randomAdditionals;

        public float randomK;

        public CircleBezierDrawable(int n)
        {
            N = n;
            L = 4.0f / 3.0f * MathF.Tan(MathF.PI / (2 * N));

            _randomAdditionals = new float[n];
            CalculateRandomAdditionals();
        }

        public void CalculateRandomAdditionals()
        {
            for (int i = 0; i < N; i++)
            {
                _randomAdditionals[i] = _random.Next() % 100 / 100f;
            }
        }

        public void SetAdditionals(int[] additionals)
        {
            for (int i = 0; i < N; i += 2)
            {
                _randomAdditionals[i] = additionals[i / 2];
                _randomAdditionals[i + 1] = 0;
            }
        }

        public void Draw(float cX, float cY, CanvasDrawingSession canvas, Color paint)
        {
            float r1 = Radius - IdleStateDiff / 2f - RadiusDiff / 2f;
            float r2 = Radius + RadiusDiff / 2 + IdleStateDiff / 2f;

            float l = L * Math.Max(r1, r2) * CubicBezierK;

            var builder = new CanvasPathBuilder(canvas);
            for (int i = 0; i < N; i++)
            {
                float r = (i % 2 == 0 ? r1 : r2) + randomK * _randomAdditionals[i];

                _pointStart[0].X = cX;
                _pointStart[0].Y = cY - r;
                _pointStart[1].X = cX + l + randomK * _randomAdditionals[i] * L;
                _pointStart[1].Y = cY - r;

                _m = SetRotate(360f / N * i, cX, cY);
                _pointStart[0] = Vector2.Transform(_pointStart[0], _m);
                _pointStart[1] = Vector2.Transform(_pointStart[1], _m);

                int j = i + 1;
                if (j >= N) j = 0;

                r = (j % 2 == 0 ? r1 : r2) + randomK * _randomAdditionals[j];


                _pointEnd[0].X = cX;
                _pointEnd[0].Y = cY - r;
                _pointEnd[1].X = cX - l + randomK * _randomAdditionals[j] * L;
                _pointEnd[1].Y = cY - r;

                _m = SetRotate(360f / N * j, cX, cY);
                _pointEnd[0] = Vector2.Transform(_pointEnd[0], _m);
                _pointEnd[1] = Vector2.Transform(_pointEnd[1], _m);

                if (i == 0)
                {
                    builder.BeginFigure(_pointStart[0]);
                }

                builder.AddCubicBezier(_pointStart[1], _pointEnd[1], _pointEnd[0]);
            }

            builder.EndFigure(CanvasFigureLoop.Closed);
            canvas.FillGeometry(CanvasGeometry.CreatePath(builder), paint);
        }

        private Matrix3x2 SetRotate(float degree, float px, float py)
        {
            return Matrix3x2.CreateRotation(ToRadians(degree), new Vector2(px, py));
        }

        private float ToRadians(float degrees)
        {
            float radians = (MathF.PI / 180) * degrees;
            return radians;
        }

        public void SetRandomAdditions(float randomK)
        {
            this.randomK = randomK;
        }
    }
}
