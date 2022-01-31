using RLottie;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Graphics.Display;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Messages
{
    public class ReactionButton : ToggleButton
    {
        private Image Presenter;
        private Popup Overlay;
        private NumericTextBlock Count;
        private RecentUserHeads RecentChoosers;

        public ReactionButton()
        {
            DefaultStyleKey = typeof(ReactionButton);
            Click += OnClick;
        }

        private MessageViewModel _message;
        private MessageReaction _interaction;
        private Reaction _reaction;
        private UnreadReaction _unread;

        public MessageReaction Reaction => _interaction;

        private int _presenterId;

        public async void SetReaction(MessageViewModel message, MessageReaction interaction, Reaction value, UnreadReaction unread)
        {
            if (Presenter == null)
            {
                _message = message;
                _interaction = interaction;
                _reaction = value;
                _unread = unread;
                return;
            }

            var recycled = _message?.Id == message.Id
                && _message?.ChatId == message.ChatId
                && _interaction?.Reaction == interaction.Reaction;

            _message = message;
            _interaction = interaction;
            _reaction = value;
            _unread = null;

            IsChecked = interaction.IsChosen;

            if (interaction.TotalCount > interaction.RecentSenderIds.Count)
            {
                Count ??= GetTemplateChild(nameof(Count)) as NumericTextBlock;
                Count.Text = Converter.ShortNumber(interaction.TotalCount);

                if (RecentChoosers != null)
                {
                    RecentChoosers.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
            }
            else
            {
                RecentChoosers ??= GetRecentChoosers();

                var destination = RecentChoosers.Items;
                var origin = interaction.RecentSenderIds;

                if (destination.Count > 0 && recycled)
                {
                    destination.ReplaceDiff(origin);
                }
                else
                {
                    destination.Clear();
                    destination.AddRange(origin);
                }

                if (Count != null)
                {
                    Count.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
            }

            if (unread != null)
            {
                Animate();
            }

            var file = value.CenterAnimation.StickerValue;
            if (file.Id == _presenterId)
            {
                return;
            }

            _presenterId = file.Id;

            if (file.Local.IsDownloadingCompleted)
            {
                Presenter.Source = await GetLottieFrame(file.Local.Path, 0, 32, 32);
            }
            else
            {
                Presenter.Source = null;

                UpdateManager.Subscribe(this, _message, file, UpdateFile, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    _message.ProtoService.DownloadFile(file.Id, 12);
                }
            }
        }

        private async void UpdateFile(object target, File file)
        {
            Presenter.Source = await GetLottieFrame(file.Local.Path, 0, 32, 32);
        }

        private RecentUserHeads GetRecentChoosers()
        {
            RecentChoosers ??= GetTemplateChild(nameof(RecentChoosers)) as RecentUserHeads;
            RecentChoosers.RecentUserHeadChanged += RecentChoosers_RecentUserHeadChanged;

            return RecentChoosers;
        }

        private void RecentChoosers_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            if (_message.ProtoService.TryGetUser(sender, out Telegram.Td.Api.User user))
            {
                photo.SetUser(_message.ProtoService, user, 20);
            }
            else if (_message.ProtoService.TryGetChat(sender, out Chat chat))
            {
                photo.SetChat(_message.ProtoService, chat, 20);
            }
            else
            {
                photo.Source = null;
            }
        }

        private static async Task<ImageSource> GetLottieFrame(string path, int frame, int width, int height)
        {
            var dpi = DisplayInformation.GetForCurrentView().LogicalDpi / 96.0f;

            width = (int)(width * dpi);
            height = (int)(height * dpi);

            var cache = $"{path}.{width}x{height}.png";
            if (System.IO.File.Exists(cache))
            {
                return new BitmapImage(UriEx.ToLocal(cache));
            }

            await Task.Run(() =>
            {
                var frameSize = new Windows.Graphics.SizeInt32 { Width = width, Height = height };

                var animation = LottieAnimation.LoadFromFile(path, frameSize, false, null);
                if (animation != null)
                {
                    animation.RenderSync(cache, frame);
                    animation.Dispose();
                }
            });

            return new BitmapImage(UriEx.ToLocal(cache));
        }

        protected override void OnApplyTemplate()
        {
            Presenter = GetTemplateChild(nameof(Presenter)) as Image;
            Overlay = GetTemplateChild(nameof(Overlay)) as Popup;

            SetReaction(_message, _interaction, _reaction, _unread);
            base.OnApplyTemplate();
        }

        protected override void OnToggle()
        {
            //base.OnToggle();
        }

        private void OnClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var chosen = _interaction;
            if (chosen == null || Presenter == null)
            {
                return;
            }

            _message.ProtoService.Send(new SetMessageReaction(_message.ChatId, _message.Id, chosen.Reaction, false));

            if (chosen.IsChosen is false)
            {
                Animate();
            }
        }

        private void Animate()
        {
            var reaction = _reaction;
            if (reaction == null)
            {
                return;
            }

            var file1 = reaction.CenterAnimation.StickerValue;
            var file2 = reaction.AroundAnimation.StickerValue;

            if (file1.Local.IsDownloadingCompleted && file2.Local.IsDownloadingCompleted)
            {
                var presenter = Presenter;
                var popup = Overlay;

                var dispatcher = DispatcherQueue.GetForCurrentThread();

                var center = new LottieView();
                center.Width = 32;
                center.Height = 32;
                center.IsLoopingEnabled = false;
                center.FrameSize = new Windows.Graphics.SizeInt32 { Width = 32, Height = 32 };
                center.DecodeFrameType = DecodePixelType.Logical;
                center.Source = UriEx.ToLocal(file1.Local.Path);
                center.FirstFrameRendered += (s, args) =>
                {
                    dispatcher.TryEnqueue(Start);
                };
                center.PositionChanged += (s, args) =>
                {
                    if (args == 1)
                    {
                        dispatcher.TryEnqueue(Continue1);
                        //dispatcher.TryEnqueue(() => popup.IsOpen = false);
                    }
                };

                var around = new LottieView();
                around.Width = 32 * 3;
                around.Height = 32 * 3;
                around.IsLoopingEnabled = false;
                around.FrameSize = new Windows.Graphics.SizeInt32 { Width = 32 * 3, Height = 32 * 3 };
                around.DecodeFrameType = DecodePixelType.Logical;
                around.Source = UriEx.ToLocal(file2.Local.Path);
                around.PositionChanged += (s, args) =>
                {
                    if (args == 1)
                    {
                        dispatcher.TryEnqueue(Continue2);
                        //dispatcher.TryEnqueue(() => popup.IsOpen = false);
                    }
                };

                var root = new Grid();
                //root.Background = new SolidColorBrush(Colors.Blue);
                //root.Opacity = 0.5;
                root.Width = 32 * 3;
                root.Height = 32 * 3;
                root.Children.Add(center);
                root.Children.Add(around);

                popup.Child = root;
                //popup.PlacementTarget = this;
                //popup.DesiredPlacement = PopupPlacementMode.BottomEdgeAlignedLeft;
                popup.ShouldConstrainToRootBounds = false;
                //popup.HorizontalOffset = -((71 * 3 - 71) / 2d);
                //popup.VerticalOffset = -((71 * 3 + 71) / 2d);
                popup.IsOpen = true;
            }
            else
            {
                if (file1.Local.CanBeDownloaded && !file1.Local.IsDownloadingActive)
                {
                    _message.ProtoService.DownloadFile(file1.Id, 12);
                }

                if (file2.Local.CanBeDownloaded && !file2.Local.IsDownloadingActive)
                {
                    _message.ProtoService.DownloadFile(file2.Id, 12);
                }
            }
        }

        private void Start()
        {
            var presenter = Presenter;
            if (presenter == null)
            {
                return;
            }

            presenter.Opacity = 0;
        }

        private void Continue1()
        {
            var presenter = Presenter;
            if (presenter == null)
            {
                return;
            }

            presenter.Opacity = 1;
        }

        private void Continue2()
        {
            var popup = Overlay;
            if (popup == null)
            {
                return;
            }

            popup.IsOpen = false;
            popup.Child = null;
        }
    }
}
