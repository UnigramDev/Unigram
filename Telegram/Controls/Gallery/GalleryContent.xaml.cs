//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LibVLCSharp.Shared;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Gallery;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryContent : AspectView
    {
        private IGalleryDelegate _delegate;
        private GalleryMedia _item;

        private int _itemId;

        public GalleryMedia Item => _item;

        private long _fileToken;
        private long _thumbnailToken;

        private int _appliedId;

        private Stretch _appliedStretch;
        private int _appliedRotation;

        private bool _fromSizeChanged;

        public bool IsEnabled
        {
            get => Button.IsEnabled;
            set => Button.IsEnabled = value;
        }

        public GalleryContent()
        {
            InitializeComponent();

            RotationAngleChanged += OnRotationAngleChanged;
            SizeChanged += OnSizeChanged;

            Texture.ImageOpened += OnImageOpened;
        }

        private void OnImageOpened(object sender, RoutedEventArgs e)
        {
            MediaOpened();
        }

        private void MediaOpened()
        {
            if (_item is GalleryMessage message && message.IsProtected)
            {
                UpdateManager.Unsubscribe(this, ref _fileToken);

                _delegate.ClientService?.Send(new OpenMessageContent(message.ChatId, message.Id));
            }
        }

        private void OnRotationAngleChanged(object sender, RoutedEventArgs e)
        {
            if (_fromSizeChanged)
            {
                return;
            }

            OnSizeChanged(sender, null);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_item == null || _itemId != _appliedId)
            {
                _appliedId = _itemId;
                return;
            }

            _appliedId = _itemId;

            var angle = RotationAngle switch
            {
                RotationAngle.Angle90 => 90,
                RotationAngle.Angle180 => 180,
                RotationAngle.Angle270 => 270,
                _ => 0
            };

            var visual = ElementComposition.GetElementVisual(this);
            visual.CenterPoint = new Vector3(ActualSize / 2, 0);
            visual.Clip ??= visual.Compositor.CreateInsetClip();

            if (_appliedStretch == Stretch && _appliedRotation == angle)
            {
                visual.RotationAngleInDegrees = angle;
                return;
            }

            _appliedStretch = Stretch;
            _fromSizeChanged = e != null;

            if (e != null)
            {
                var prev = e.PreviousSize.ToVector2();
                var next = e.NewSize.ToVector2();

                var anim = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0, new Vector3(prev / next, 1));
                anim.InsertKeyFrame(1, Vector3.One);

                var panel = ElementComposition.GetElementVisual(Children[0]);
                panel.CenterPoint = new Vector3(next.X / 2, next.Y / 2, 0);
                panel.StartAnimation("Scale", anim);

                var factor = Window.Current.Compositor.CreateExpressionAnimation("Vector3(1 / content.Scale.X, 1 / content.Scale.Y, 1)");
                factor.SetReferenceParameter("content", panel);

                var button = ElementComposition.GetElementVisual(Button);
                button.CenterPoint = new Vector3(Button.ActualSize.X / 2, Button.ActualSize.Y / 2, 0);
                button.StartAnimation("Scale", factor);
            }

            if (_appliedRotation != angle)
            {
                var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0, angle > _appliedRotation ? 360 : _appliedRotation);
                animation.InsertKeyFrame(1, angle);

                _appliedRotation = angle;
                visual.StartAnimation("RotationAngleInDegrees", animation);
            }
        }

        public void UpdateItem(IGalleryDelegate delegato, GalleryMedia item)
        {
            _delegate = delegato;
            _item = item;

            _appliedRotation = item?.RotationAngle switch
            {
                RotationAngle.Angle90 => 90,
                RotationAngle.Angle180 => 180,
                RotationAngle.Angle270 => 270,
                _ => 0
            };

            Tag = item;
            RotationAngle = item?.RotationAngle ?? RotationAngle.Angle0;
            Background = null;
            Texture.Source = null;

            //ScrollingHost.ChangeView(0, 0, 1, true);

            var file = item?.GetFile();
            if (file == null)
            {
                return;
            }

            _itemId = file.Id;

            if (item.IsVideoNote)
            {
                MaxWidth = 384;
                MaxHeight = 384;

                CornerRadius = new CornerRadius(384 / 2);
                Constraint = new Size(384, 384);
            }
            else
            {
                MaxWidth = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;

                CornerRadius = new CornerRadius(0);
                Constraint = item.Constraint;
            }

            var thumbnail = item.GetThumbnail();
            if (thumbnail != null && (item.IsVideo || (item.IsPhoto && !file.Local.IsDownloadingCompleted)))
            {
                UpdateThumbnail(item, thumbnail, null, true);
            }

            UpdateManager.Subscribe(this, delegato.ClientService, file, ref _fileToken, UpdateFile);
            UpdateFile(item, file);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_item, file);
        }

        private void UpdateFile(GalleryMedia item, File file)
        {
            var reference = item?.GetFile();
            if (reference == null || reference.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;
                Button.Opacity = 1;
            }
            else if (file.Remote.IsUploadingActive)
            {
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;
                Button.Opacity = 1;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;
                Button.Opacity = 1;

                if (item.IsPhoto)
                {
                    item.ClientService.DownloadFile(file.Id, 1);
                }
            }
            else
            {
                if (item.IsVideo)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 1;
                    Button.Opacity = 1;
                }
                else if (item.IsPhoto)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Photo);
                    Button.Opacity = 0;

                    Texture.Source = UriEx.ToBitmap(file.Local.Path, 0, 0);
                }
            }

            Canvas.SetZIndex(Button,
                Button.State == MessageContentState.Photo ? -1 : 0);
        }

        private void UpdateThumbnail(object target, File file)
        {
            UpdateThumbnail(_item, file, null, false);
        }

        private void UpdateThumbnail(GalleryMedia item, File file, Minithumbnail minithumbnail, bool download)
        {
            BitmapImage source = null;
            ImageBrush brush;

            if (Background is ImageBrush existing)
            {
                brush = existing;
            }
            else
            {
                brush = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                Background = brush;
            }

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    source = new BitmapImage();
                    PlaceholderHelper.GetBlurred(source, file.Local.Path, 3);
                }
                else
                {
                    if (download)
                    {
                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            _delegate.ClientService.DownloadFile(file.Id, 1);
                        }

                        UpdateManager.Subscribe(this, _delegate.ClientService, file, ref _thumbnailToken, UpdateThumbnail, true);
                    }

                    if (minithumbnail != null)
                    {
                        source = new BitmapImage();
                        PlaceholderHelper.GetBlurred(source, minithumbnail.Data, 3);
                    }
                }
            }
            else if (minithumbnail != null)
            {
                source = new BitmapImage();
                PlaceholderHelper.GetBlurred(source, minithumbnail.Data, 3);
            }

            brush.ImageSource = source;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var item = _item;
            if (item == null)
            {
                return;
            }

            var file = item.GetFile();
            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingActive)
            {
                item.ClientService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (SettingsService.Current.IsStreamingEnabled && item.IsVideo && item.IsStreamable)
                {
                    _delegate?.OpenFile(item, file);
                }
                else
                {
                    item.ClientService.DownloadFile(file.Id, 32);
                }
            }
            else if (item.IsVideo)
            {
                _delegate?.OpenFile(item, file);
            }
        }

        private AsyncMediaPlayer _player;

        private RemoteFileStream _fileStream;
        private GalleryTransportControls _controls;

        private bool _stopped;

        private bool _unloaded;
        private int _fileId;

        private long _initialPosition;

        public void Play(GalleryMedia item, long position, GalleryTransportControls controls)
        {
            if (_unloaded)
            {
                return;
            }

            try
            {
                var file = item.GetFile();
                if (file.Id == _fileId || (!file.Local.IsDownloadingCompleted && !SettingsService.Current.IsStreamingEnabled))
                {
                    return;
                }

                _fileId = file.Id;

                controls.Attach(item, file);

                if (_player == null)
                {
                    _controls = controls;
                    _fileStream = new RemoteFileStream(item.ClientService, file);
                    _initialPosition = position;
                    FindName(nameof(Video));
                }
                else
                {
                    controls.Attach(_player);

                    _fileStream = null;
                    _controls = null;
                    _player.Play(new RemoteFileStream(item.ClientService, file));
                    _player.Time = position;
                }
            }
            catch { }
        }

        private void OnInitialized(object sender, LibVLCSharp.Platforms.Windows.InitializedEventArgs e)
        {
            _player = new AsyncMediaPlayer(e.SwapChainOptions);
            _player.Buffering += OnBuffering;
            _player.Stopped += OnStopped;

            var stream = _fileStream;
            var position = _initialPosition;

            _controls.Attach(_player);
            _player.Play(stream);
            _player.Time = position;

            _controls = null;
            _fileStream = null;
            _initialPosition = 0;
        }

        public void Unload()
        {
            if (_unloaded)
            {
                return;
            }

            _unloaded = true;

            if (Video != null)
            {
                Video.Initialized -= OnInitialized;
            }

            if (_player != null)
            {
                _player.Buffering -= OnBuffering;
                _player.Stopped -= OnStopped;
                _player.Close();
            }

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken, true);
        }

        private void OnBuffering(AsyncMediaPlayer sender, MediaPlayerBufferingEventArgs args)
        {
            if (args.Cache == 100)
            {
                MediaOpened();
            }
        }

        private void OnStopped(object sender, EventArgs e)
        {
            if (_stopped)
            {
                _stopped = false;
                Video.Clear();
            }
        }

        public void Stop(out int fileId, out long position)
        {
            if (_player != null && !_unloaded)
            {
                fileId = _fileId;
                position = _player.Time;

                _stopped = true;
                _player.Stop();
            }
            else
            {
                fileId = 0;
                position = 0;
            }

            _fileId = 0;
        }
    }
}
