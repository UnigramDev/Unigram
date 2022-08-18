using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class ProfilePicture : HyperlinkButton
    {
        private string _fileToken;
        private int? _fileId;
        private long? _referenceId;

        private object _parameters;

        public ProfilePicture()
        {
            DefaultStyleKey = typeof(ProfilePicture);
        }

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
                SetChat(chat.ProtoService, chat.Chat, chat.Side, false);
            }
            else if (_parameters is UserParameters user)
            {
                SetUser(user.ProtoService, user.User, user.Side, false);
            }
            else if (_parameters is ChatInviteParameters chatInvite)
            {
                SetChat(chatInvite.ProtoService, chatInvite.Chat, chatInvite.Side, false);
            }
        }

        #region MessageSender

        public void SetMessageSender(IProtoService protoService, MessageSender sender, int side, bool download = true)
        {
            if (protoService.TryGetUser(sender, out User user))
            {
                SetUser(protoService, user, side, download);
            }
            else if (protoService.TryGetChat(sender, out Chat chat))
            {
                SetChat(protoService, chat, side, download);
            }
        }

        #endregion

        #region Chat

        struct ChatParameters
        {
            public IProtoService ProtoService;
            public Chat Chat;
            public int Side;

            public ChatParameters(IProtoService protoService, Chat chat, int side)
            {
                ProtoService = protoService;
                Chat = chat;
                Side = side;
            }
        }

        public void SetChat(IProtoService protoService, Chat chat, int side, bool download = true)
        {
            SetChat(protoService, chat, chat.Photo?.Small, side, download);
        }

        private void SetChat(IProtoService protoService, Chat chat, File file, int side, bool download = true)
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

                Source = GetChat(protoService, chat, file, side, download);
            }
        }

        private ImageSource GetChat(IProtoService protoService, Chat chat, File file, int side, bool download = true)
        {
            if (chat.Type is ChatTypePrivate privata && protoService.IsSavedMessages(chat))
            {
                return PlaceholderHelper.GetSavedMessages(privata.UserId, side);
            }
            else if (protoService.IsRepliesChat(chat))
            {
                return PlaceholderHelper.GetGlyph(Icons.ArrowReply, 5, side);
            }

            if (file != null)
            {
                if (file.Local.IsFileExisting())
                {
                    return UriEx.ToBitmap(file.Local.Path, side, side);
                }
                else if (download)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        protoService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new ChatParameters(protoService, chat, side);
                    UpdateManager.Subscribe(this, protoService, file, ref _fileToken, UpdateFile, true);
                }
            }
            else if (protoService.TryGetUser(chat, out User user) && user.Type is UserTypeDeleted)
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
            public IProtoService ProtoService;
            public User User;
            public int Side;

            public UserParameters(IProtoService protoService, User user, int side)
            {
                ProtoService = protoService;
                User = user;
                Side = side;
            }
        }

        public void SetUser(IProtoService protoService, User user, int side, bool download = true)
        {
            SetUser(protoService, user, user.ProfilePhoto?.Small, side, download);
        }

        public void SetUser(IProtoService protoService, User user, File file, int side, bool download = true)
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

                Source = GetUser(protoService, user, file, side, download);
            }
        }

        private ImageSource GetUser(IProtoService protoService, User user, File file, int side, bool download = true)
        {
            if (file != null)
            {
                if (file.Local.IsFileExisting())
                {
                    return UriEx.ToBitmap(file.Local.Path, side, side);
                }
                else if (download)
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        protoService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new UserParameters(protoService, user, side);
                    UpdateManager.Subscribe(this, protoService, file, ref _fileToken, UpdateFile, true);
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
            public IProtoService ProtoService;
            public ChatInviteLinkInfo Chat;
            public int Side;

            public ChatInviteParameters(IProtoService protoService, ChatInviteLinkInfo chat, int side)
            {
                ProtoService = protoService;
                Chat = chat;
                Side = side;
            }
        }

        public void SetChat(IProtoService protoService, ChatInviteLinkInfo chat, int side, bool download = true)
        {
            SetChat(protoService, chat, chat.Photo?.Small, side, download);
        }

        private void SetChat(IProtoService protoService, ChatInviteLinkInfo chat, File file, int side, bool download = true)
        {
            if (_fileToken is string fileToken)
            {
                _fileToken = null;
                EventAggregator.Default.Unregister<File>(this, fileToken);
            }

            Source = GetChat(protoService, chat, file, side, download);
        }

        private ImageSource GetChat(IProtoService protoService, ChatInviteLinkInfo chat, File file, int side, bool download = true)
        {
            if (file != null)
            {
                if (file.Local.IsFileExisting())
                {
                    return UriEx.ToBitmap(file.Local.Path, side, side);
                }
                else
                {
                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && download)
                    {
                        protoService.DownloadFile(file.Id, 1);
                    }

                    _parameters = new ChatInviteParameters(protoService, chat, side);
                    UpdateManager.Subscribe(this, protoService, file, ref _fileToken, UpdateFile, true);
                }
            }

            if (chat.Photo?.Minithumbnail != null)
            {
                return PlaceholderHelper.GetBlurred(chat.Photo.Minithumbnail.Data);
            }

            return PlaceholderHelper.GetChat(chat, side);
        }

        #endregion
    }
}
