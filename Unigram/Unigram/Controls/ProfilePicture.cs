using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public enum ProfilePictureShape
    {
        Ellipse,
        Superellipse
    }

    public class ProfilePicture : HyperlinkButton
    {
        private string _fileToken;
        private int? _fileId;
        private long? _referenceId;

        private object _parameters;

        private readonly CompositionRoundedRectangleGeometry _clip;

        private Image LayoutRoot;

        public ProfilePicture()
        {
            _clip = Window.Current.Compositor.CreateRoundedRectangleGeometry();
            DefaultStyleKey = typeof(ProfilePicture);
        }

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Image;

            var visual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            visual.Clip = visual.Compositor.CreateGeometricClip(_clip);

            base.OnApplyTemplate();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _clip.Size = finalSize.ToVector2();

            if (Shape == ProfilePictureShape.Superellipse)
            {
                _clip.CornerRadius = new Vector2(_clip.Size.X / 4);
            }
            else
            {
                _clip.CornerRadius = new Vector2(_clip.Size.Y / 2);
            }

            return base.ArrangeOverride(finalSize);
        }

        #region Shape

        public ProfilePictureShape Shape
        {
            get { return (ProfilePictureShape)GetValue(ShapeProperty); }
            set { SetValue(ShapeProperty, value); }
        }

        public static readonly DependencyProperty ShapeProperty =
            DependencyProperty.Register("Shape", typeof(ProfilePictureShape), typeof(ProfilePicture), new PropertyMetadata(ProfilePictureShape.Ellipse, OnShapeChanged));

        private static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ProfilePicture)d).OnShapeChanged((ProfilePictureShape)e.NewValue, (ProfilePictureShape)e.OldValue);
        }

        private void OnShapeChanged(ProfilePictureShape newValue, ProfilePictureShape oldValue)
        {
            if (newValue != oldValue)
            {
                if (IsAnimated && !double.IsNaN(ActualWidth))
                {
                    var visual = ElementCompositionPreview.GetElementVisual(this);

                    var compositor = Window.Current.Compositor;
                    var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                    batch.Completed += (s, args) =>
                    {
                        InvalidateArrange();
                    };

                    var ellipse = newValue == ProfilePictureShape.Ellipse;

                    var anim = compositor.CreateVector2KeyFrameAnimation();
                    anim.InsertKeyFrame(0, new Vector2(ellipse ? ActualSize.X / 4 : ActualSize.X / 2));
                    anim.InsertKeyFrame(1, new Vector2(ellipse ? ActualSize.X / 2 : ActualSize.X / 4));

                    _clip.Size = ActualSize;
                    _clip.StartAnimation("CornerRadius", anim);

                    batch.End();
                }
                else
                {
                    InvalidateArrange();
                }
            }
        }

        #endregion

        #region IsAnimated

        public bool IsAnimated
        {
            get { return (bool)GetValue(IsAnimatedProperty); }
            set { SetValue(IsAnimatedProperty, value); }
        }

        public static readonly DependencyProperty IsAnimatedProperty =
            DependencyProperty.Register("IsAnimated", typeof(bool), typeof(ProfilePicture), new PropertyMetadata(false));

        #endregion

        public void Clear()
        {
            if (_fileToken is string fileToken)
            {
                _fileToken = null;
                EventAggregator.Default.Unregister<File>(this, fileToken);
            }

            _fileId = null;
            _referenceId = null;

            _parameters = null;

            Source = null;
        }

        #region Source

        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(ImageSource), typeof(ProfilePicture), new PropertyMetadata(null));

        #endregion

        private void UpdateFile(object target, File file)
        {
            if (_parameters is ChatParameters chat)
            {
                SetChat(chat.ClientService, chat.Chat, chat.Side, false);
            }
            else if (_parameters is UserParameters user)
            {
                SetUser(user.ClientService, user.User, user.Side, false);
            }
            else if (_parameters is ChatInviteParameters chatInvite)
            {
                SetChat(chatInvite.ClientService, chatInvite.Chat, chatInvite.Side, false);
            }
        }

        #region MessageSender

        public void SetMessageSender(IClientService clientService, MessageSender sender, int side, bool download = true)
        {
            if (clientService.TryGetUser(sender, out User user))
            {
                SetUser(clientService, user, side, download);
            }
            else if (clientService.TryGetChat(sender, out Chat chat))
            {
                SetChat(clientService, chat, side, download);
            }
        }

        #endregion

        #region Chat

        struct ChatParameters
        {
            public IClientService ClientService;
            public Chat Chat;
            public int Side;

            public ChatParameters(IClientService clientService, Chat chat, int side)
            {
                ClientService = clientService;
                Chat = chat;
                Side = side;
            }
        }

        public void SetChat(IClientService clientService, Chat chat, int side, bool download = true)
        {
            SetChat(clientService, chat, chat.Photo?.Small, side, download);
        }

        private void SetChat(IClientService clientService, Chat chat, File file, int side, bool download = true)
        {
            if (_fileToken is string fileToken)
            {
                _fileToken = null;
                EventAggregator.Default.Unregister<File>(this, fileToken);
            }

            if (_referenceId != chat.Id || _fileId != file?.Id || Source == null || !download)
            {
                _referenceId = chat.Id;
                _fileId = file?.Id;

                Source = GetChat(clientService, chat, file, side, out var shape, download);
                Shape = shape;
            }
        }

        private ImageSource GetChat(IClientService clientService, Chat chat, File file, int side, out ProfilePictureShape shape, bool download = true)
        {
            shape = ProfilePictureShape.Ellipse;

            if (chat.Type is ChatTypePrivate privata && clientService.IsSavedMessages(chat))
            {
                return PlaceholderHelper.GetSavedMessages(privata.UserId, side);
            }
            else if (clientService.IsRepliesChat(chat))
            {
                return PlaceholderHelper.GetGlyph(Icons.ChatMultiple, 5, side);
            }
            
            if (clientService.IsForum(chat))
            {
                shape = ProfilePictureShape.Superellipse;
            }

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return UriEx.ToBitmap(file.Local.Path, side, side);
                }
                else if (download)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        clientService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new ChatParameters(clientService, chat, side);
                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }
            else if (clientService.TryGetUser(chat, out User user) && user.Type is UserTypeDeleted)
            {
                return PlaceholderHelper.GetDeletedUser(user, side);
            }

            if (chat.Photo?.Minithumbnail != null)
            {
                return PlaceholderHelper.GetBlurred(chat.Photo.Minithumbnail.Data);
            }

            return PlaceholderHelper.GetChat(chat, side);
        }

        #endregion

        #region User

        struct UserParameters
        {
            public IClientService ClientService;
            public User User;
            public int Side;

            public UserParameters(IClientService clientService, User user, int side)
            {
                ClientService = clientService;
                User = user;
                Side = side;
            }
        }

        public void SetUser(IClientService clientService, User user, int side, bool download = true)
        {
            SetUser(clientService, user, user.ProfilePhoto?.Small, side, download);
        }

        public void SetUser(IClientService clientService, User user, File file, int side, bool download = true)
        {
            if (_fileToken is string fileToken)
            {
                _fileToken = null;
                EventAggregator.Default.Unregister<File>(this, fileToken);
            }

            if (_referenceId != user.Id || _fileId != file?.Id || Source == null || !download)
            {
                _referenceId = user.Id;
                _fileId = file?.Id;

                Source = GetUser(clientService, user, file, side, download);
                Shape = ProfilePictureShape.Ellipse;
            }
        }

        private ImageSource GetUser(IClientService clientService, User user, File file, int side, bool download = true)
        {
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return UriEx.ToBitmap(file.Local.Path, side, side);
                }
                else if (download)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        clientService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new UserParameters(clientService, user, side);
                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }
            else if (user.Type is UserTypeDeleted)
            {
                return PlaceholderHelper.GetDeletedUser(user, side);
            }

            if (user.ProfilePhoto?.Minithumbnail != null)
            {
                return PlaceholderHelper.GetBlurred(user.ProfilePhoto.Minithumbnail.Data);
            }

            return PlaceholderHelper.GetUser(user, side);
        }


        #endregion

        #region Chat invite

        struct ChatInviteParameters
        {
            public IClientService ClientService;
            public ChatInviteLinkInfo Chat;
            public int Side;

            public ChatInviteParameters(IClientService clientService, ChatInviteLinkInfo chat, int side)
            {
                ClientService = clientService;
                Chat = chat;
                Side = side;
            }
        }

        public void SetChat(IClientService clientService, ChatInviteLinkInfo chat, int side, bool download = true)
        {
            SetChat(clientService, chat, chat.Photo?.Small, side, download);
        }

        private void SetChat(IClientService clientService, ChatInviteLinkInfo chat, File file, int side, bool download = true)
        {
            if (_fileToken is string fileToken)
            {
                _fileToken = null;
                EventAggregator.Default.Unregister<File>(this, fileToken);
            }

            Source = GetChat(clientService, chat, file, side, download);
            Shape = ProfilePictureShape.Ellipse;
        }

        private ImageSource GetChat(IClientService clientService, ChatInviteLinkInfo chat, File file, int side, bool download = true)
        {
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return UriEx.ToBitmap(file.Local.Path, side, side);
                }
                else
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && download)
                    {
                        clientService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new ChatInviteParameters(clientService, chat, side);
                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }

            if (chat.Photo?.Minithumbnail != null)
            {
                return PlaceholderHelper.GetBlurred(chat.Photo.Minithumbnail.Data);
            }

            return PlaceholderHelper.GetChat(chat, side);
        }

        #endregion

        public void SetMessage(MessageViewModel message)
        {
            if (message.IsSaved)
            {
                if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser && message.ClientService.TryGetUser(fromUser.SenderUserId, out User fromUserUser))
                {
                    SetUser(message.ClientService, fromUserUser, 30);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChat fromChat && message.ClientService.TryGetChat(fromChat.SenderChatId, out Chat fromChatChat))
                {
                    SetChat(message.ClientService, fromChatChat, 30);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel && message.ClientService.TryGetChat(fromChannel.ChatId, out Chat fromChannelChat))
                {
                    SetChat(message.ClientService, fromChannelChat, 30);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginMessageImport fromImport)
                {
                    Source = PlaceholderHelper.GetNameForUser(fromImport.SenderName, 30);
                    Shape = ProfilePictureShape.Ellipse;
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser fromHiddenUser)
                {
                    Source = PlaceholderHelper.GetNameForUser(fromHiddenUser.SenderName, 30);
                    Shape = ProfilePictureShape.Ellipse;
                }
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                SetUser(message.ClientService, senderUser, 30);
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                SetChat(message.ClientService, senderChat, 30);
            }
        }

        public void CloneSource(ProfilePicture photo)
        {
            if (_referenceId != photo._referenceId || _fileId != photo._fileId || Source == null)
            {
                _referenceId = photo._referenceId;
                _fileId = photo._fileId;

                Source = photo.Source;
                Shape = photo.Shape;
            }
        }
    }
}
