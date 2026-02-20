//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native.AI;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryContent : AspectView
    {
        private GalleryWindow _window;
        private GalleryMedia _item;

        private int _itemId;

        public GalleryMedia Item => _item;

        private ThumbnailController _thumbnailController;

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
            if (_item is GalleryMessage message && message.HasProtectedContent)
            {
                UpdateManager.Unsubscribe(this, ref _fileToken);

                _window.ClientService?.Send(new OpenMessageContent(message.ChatId, message.Id));
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

                var anim = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0, new Vector3(prev / next, 1));
                anim.InsertKeyFrame(1, Vector3.One);

                var panel = ElementComposition.GetElementVisual(Children[0]);
                panel.CenterPoint = new Vector3(next.X / 2, next.Y / 2, 0);
                panel.StartAnimation("Scale", anim);

                var factor = BootStrapper.Current.Compositor.CreateExpressionAnimation("Vector3(1 / content.Scale.X, 1 / content.Scale.Y, 1)");
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

        public void UpdateItem(GalleryWindow window, GalleryMedia item)
        {
            _window = window;
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
            Texture.Source = null;
            Texture.Stretch = item?.Constraint != null
                ? Stretch.UniformToFill
                : Stretch.Uniform;

            //ScrollingHost.ChangeView(0, 0, 1, true);

            var file = item?.File;
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

            if (item is GalleryMessage message && message.Content is MessageDocument document && !message.IsMedia)
            {
                DocumentName.Text = document.Document.FileName;
            }
            else
            {
                DocumentName.Text = string.Empty;
            }

            if (item.IsMedia && (item.IsVideo || (item.IsPhoto && !file.Local.IsDownloadingCompleted)))
            {
                UpdateThumbnail(item, item.Thumbnail, item.Minithumbnail, true);
            }

            UpdateManager.Subscribe(this, window.ClientService, file, ref _fileToken, UpdateFile);
            UpdateFile(item, file);

            if (item.AlternativeVideos.Count > 0)
            {
                var video = item.AlternativeVideos[0];
                window.ClientService.DownloadFile(video.HlsFile.Id, 30);
                window.ClientService.DownloadFile(video.Video.Id, 29, 0, (int)((double)video.Video.Size / item.Duration));
            }

            IsTextSelectionEnabled = false;
            IsTextNotRecognized = false;
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_item, file);
        }

        private void UpdateFile(GalleryMedia item, File file)
        {
            var reference = item?.File;
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

                if (item.IsPhoto && item.IsMedia)
                {
                    item.ClientService.DownloadFile(file.Id, 16);
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
                else if (item.IsPhoto && item.IsMedia)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Photo);
                    Button.Opacity = 0;

                    if (Extensions.IsRelativePath(ApplicationData.Current.LocalFolder.Path, file.Local.Path, out _))
                    {
                        Texture.Source = UriEx.ToBitmap(file.Local.Path, 0, 0);
                    }
                    else
                    {
                        var bitmap = new BitmapImage();
                        Texture.Source = bitmap;
                        UpdateBitmap(bitmap, file.Local.Path);
                    }
                }
                else
                {
                    Button.SetGlyph(file.Id, MessageContentState.Document);
                    Button.Progress = 1;
                    Button.Opacity = 1;
                }
            }

            Canvas.SetZIndex(Button,
                Button.State == MessageContentState.Photo ? -1 : 0);
        }

        private async void UpdateBitmap(BitmapImage bitmap, string path)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                using (var stream = await file.OpenReadAsync())
                {
                    await bitmap.SetSourceAsync(stream);
                }
            }
            catch
            {
                //
            }
        }

        private void UpdateThumbnail(object target, File file)
        {
            UpdateThumbnail(_item, file, null, false);
        }

        private void UpdateThumbnail(GalleryMedia item, File file, Minithumbnail minithumbnail, bool download)
        {
            _thumbnailController ??= new ThumbnailController(ThumbnailTexture);

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    _thumbnailController.Blur(file.Local.Path, 3, item.File.Id);
                }
                else
                {
                    if (download)
                    {
                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            _window.ClientService.DownloadFile(file.Id, 1);
                        }

                        UpdateManager.Subscribe(this, _window.ClientService, file, ref _thumbnailToken, UpdateThumbnail, true);
                    }

                    if (minithumbnail != null)
                    {
                        _thumbnailController.Blur(minithumbnail.Data, 3, item.File.Id);
                    }
                    else
                    {
                        _thumbnailController.Recycle();
                    }
                }
            }
            else if (minithumbnail != null)
            {
                _thumbnailController.Blur(minithumbnail.Data, 3, item.File.Id);
            }
            else
            {
                _thumbnailController.Recycle();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var item = _item;
            if (item == null)
            {
                return;
            }

            var file = item.File;
            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingActive)
            {
                item.ClientService.CancelDownloadFile(file, false);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (SettingsService.Current.IsStreamingEnabled && item.IsVideo && item.IsStreamable)
                {
                    _window?.OpenFile(item, file);
                }
                else
                {
                    item.ClientService.DownloadFile(file.Id, 32);
                }
            }
            else if (item.IsVideo)
            {
                _window?.OpenFile(item, file);
            }
            else if (item is GalleryMessage message && !item.IsMedia)
            {
                var service = _window.ClientService.Session.Resolve<IStorageService>();
                if (service != null)
                {
                    _ = service.OpenFileAsync(file);
                }
            }
        }

        private GalleryTransportControls _controls;

        private bool _unloaded;
        private int _fileId;

        public void Play(GalleryMedia item, double position, GalleryTransportControls controls, bool force = false)
        {
            if (_unloaded)
            {
                return;
            }

            try
            {
                var file = item.File;
                if (!force && file.Id == _fileId || (!file.Local.IsDownloadingCompleted && !SettingsService.Current.IsStreamingEnabled))
                {
                    return;
                }

                _fileId = file.Id;

                if (!item.IsLoopingEnabled)
                {
                    LifetimeService.Current.Playback.Pause();
                }

                // Always recreate HLS player for now, try to reuse native one
                if (!force && (SettingsService.Current.Diagnostics.ForceWebView2 || item.IsHls()) && ChromiumWebPresenter.IsSupported())
                {
                    Video = new WebVideoPlayer();
                }
                else if (Video is not NativeVideoPlayer)
                {
                    Video = new NativeVideoPlayer();
                }

                controls.Attach(item, file);
                controls.Attach(Video);

                Video.Play(item, position);
                Button.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        public void Play(VideoPlayerBase player, GalleryMedia item, GalleryTransportControls controls)
        {
            if (_unloaded)
            {
                return;
            }

            try
            {
                var file = item.File;
                if (file.Id == _fileId || (!file.Local.IsDownloadingCompleted && !SettingsService.Current.IsStreamingEnabled))
                {
                    return;
                }

                _fileId = file.Id;

                Video = player;
                Button.Visibility = Visibility.Collapsed;
                //Video.IsUnloadedExpected = false;

                controls.Attach(item, file);
                controls.Attach(Video);
            }
            catch { }
        }

        public VideoPlayerBase Video
        {
            get => Panel.Child as VideoPlayerBase;
            set
            {
                var video = Panel.Child as VideoPlayerBase;
                if (video != null)
                {
                    video.TreeUpdated -= OnTreeUpdated;
                    video.FirstFrameReady -= OnFirstFrameReady;
                    video.TrackChanged -= OnTrackChanged;
                    video.Failed -= OnFailed;
                }

                if (value != null)
                {
                    value.TreeUpdated += OnTreeUpdated;
                    value.FirstFrameReady += OnFirstFrameReady;
                    value.TrackChanged += OnTrackChanged;
                    value.Failed += OnFailed;
                }

                Panel.Child = value;
            }
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
                Video = null;
                Button.Visibility = Visibility.Visible;
            }

            _thumbnailController?.Recycle();

            UpdateManager.Unsubscribe(this, ref _fileToken);
            UpdateManager.Unsubscribe(this, ref _thumbnailToken);
        }

        private void OnTreeUpdated(VideoPlayerBase sender, EventArgs e)
        {
            // Hopefully this is always triggered after Unloaded/Loaded
            // And even if the events are raced and triggered in the opposite order
            // Not causing Disconnected/Connected to be triggered.
            sender.IsUnloadedExpected = false;
            sender.TreeUpdated -= OnTreeUpdated;
        }

        private void OnFirstFrameReady(VideoPlayerBase sender, EventArgs args)
        {
            MediaOpened();
        }

        private void OnTrackChanged(VideoPlayerBase sender, VideoPlayerTrackChangedEventArgs args)
        {
            if (args.Width != 0 && args.Height != 0 && !ActualConstraint.IsEmpty)
            {
                var size = ImageHelper.ScaleMin(args.Width, args.Height, Math.Max(ActualConstraint.Width, ActualConstraint.Height));
                Constraint = new MaximumSize(size.Width, size.Height);
            }
        }

        private void OnFailed(VideoPlayerBase sender, EventArgs args)
        {
            if (_unloaded)
            {
                return;
            }

            _window.OpenFile(_item, _item.File, force: true);
        }

        public void Stop(out GalleryMedia item, out double position)
        {
            if (Video != null && !_unloaded)
            {
                item = _item;

                var time = Video.Position;
                var length = Video.Duration;

                if (length >= 30 && time >= 10 && time <= length - 10)
                {
                    position = time;
                }
                else
                {
                    position = 0;
                }

                Video = null;
                Button.Visibility = Visibility.Visible;
            }
            else
            {
                item = null;
                position = 0;
            }

            _fileId = 0;
        }

        public bool IsTextSelectionEnabled
        {
            get => Selection.Visibility == Visibility.Visible;
            private set => Selection.Visibility = value
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void SelectAllText()
        {
            VisualUtilities.QueueCallbackForCompositionRendered(Selection.SelectAll);
        }

        public void CopySelectedText()
        {
            MessageHelper.CopyText(XamlRoot, Selection.SelectedText);
        }

        public string RecognizedText => Selection.Text;

        public string SelectedText => Selection.SelectedText;

        public bool IsTextSelected => Selection.SelectedText.Length > 0;

        public bool IsTextNotRecognized { get; private set; }

        private RecognizedText _recognizedText;
        private int _recognizedTextFileId;

        public async void RecognizeText()
        {
            if (IsTextSelectionEnabled)
            {
                Selection.ClearSelection();
                IsTextSelectionEnabled = false;
                return;
            }
            else if (_recognizedText != null && _recognizedTextFileId == _itemId)
            {
                IsTextSelectionEnabled = true;
                return;
            }

            var viewModel = _window?.ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var fileId = _itemId;
            var service = viewModel.Session.Resolve<ITextRecognitionService>();

            var status = await service.EnsureReadyAsync();
            if (status is TextRecognitionStatusUnavailable unavailable)
            {
                // TODO: Error: not available

                WatchDog.TrackEvent("TextRecognizer", new Properties { { "Status", "Unavailable" } });
                return;
            }
            else if (status is TextRecognitionStatusDownloading downloading && fileId == _fileId && IsLoaded)
            {
                WatchDog.TrackEvent("TextRecognizer", new Properties { { "Status", "Downloading" } });

                var confirm = await viewModel.ShowPopupAsync(new TextRecognitionDownloadPopup(viewModel.ClientService, viewModel.Aggregator, downloading.Document), requestedTheme: ElementTheme.Dark);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                status = await service.EnsureReadyAsync();
            }
            else
            {
                WatchDog.TrackEvent("TextRecognizer", new Properties { { "Status", "Available" } });
            }

            if (status is not TextRecognitionStatusAvailable available || fileId != _itemId || !IsLoaded)
            {
                // TODO: Error: not available
                return;
            }

            IsTextSelectionEnabled = true;
            Selection.ShowSkeleton();

            var bitmap = await GetSoftwareBitmapAsync(_item?.File);
            if (bitmap == null)
            {
                // TODO: Error: text recognition is not available

                IsTextSelectionEnabled = false;
                return;
            }

            if (bitmap.PixelWidth < 50 || bitmap.PixelHeight < 50)
            {
                ToastPopup.Show(XamlRoot, Strings.ScanTextTooSmall, ToastPopupIcon.Error);

                IsTextSelectionEnabled = false;
                return;
            }

            if (bitmap.PixelWidth > 10000 || bitmap.PixelHeight > 10000)
            {
                ToastPopup.Show(XamlRoot, Strings.ScanTextTooLarge, ToastPopupIcon.Error);

                IsTextSelectionEnabled = false;
                return;
            }

            if (fileId != _itemId || !IsLoaded)
            {
                IsTextSelectionEnabled = false;
                return;
            }

            var result = await available.Recognizer.RecognizeAsync(bitmap);
            if (result == null || result.Lines.Empty())
            {
                ToastPopup.Show(XamlRoot, Strings.ScanTextNoTextDetected, ToastPopupIcon.Info);

                IsTextSelectionEnabled = false;
                IsTextNotRecognized = true;
                return;
            }

            if (fileId != _itemId || !IsLoaded)
            {
                return;
            }

            _recognizedText = result;
            _recognizedTextFileId = _itemId;

            Selection.ImageSize = new Vector2(bitmap.PixelWidth, bitmap.PixelHeight);
            Selection.RecognizedText = result;
        }

        public async Task<SoftwareBitmap> GetSoftwareBitmapAsync(File file)
        {
            if (_window?.ViewModel == null)
            {
                return null;
            }

            var storage = await _window.ViewModel.ClientService.GetFileAsync(file);
            if (storage == null)
            {
                return null;
            }

            try
            {
                using (var stream = await storage.OpenReadAsync())
                {
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    return await decoder.GetSoftwareBitmapAsync();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
