//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public enum ProfilePictureShape
    {
        None,
        Ellipse,
        Superellipse
    }

    public class ProfilePicture : HyperlinkButton
    {
        private long _fileToken;
        private int? _fileId;
        private long? _referenceId;

        private int _fontSize;
        private bool _glyph;

        private object _parameters;

        private Border LayoutRoot;
        private ImageBrush Texture;
        private LinearGradientBrush Gradient;

        // TODO: consider lazy loading
        private TextBlock Initials;

        public ProfilePicture()
        {
            DefaultStyleKey = typeof(ProfilePicture);
        }

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Border;

            Initials = GetTemplateChild(nameof(Initials)) as TextBlock;
            Texture = GetTemplateChild(nameof(Texture)) as ImageBrush;

            Gradient = new LinearGradientBrush();
            Gradient.StartPoint = new Windows.Foundation.Point(0, 0);
            Gradient.EndPoint = new Windows.Foundation.Point(0, 1);
            Gradient.GradientStops.Add(new GradientStop { Offset = 0 });
            Gradient.GradientStops.Add(new GradientStop { Offset = 1 });

            //UpdateCornerRadius();
            //UpdateFontSize();

            OnSourceChanged(Source);
            base.OnApplyTemplate();
        }

        private void UpdateCornerRadius()
        {
            if (LayoutRoot == null || double.IsNaN(Width))
            {
                return;
            }

            LayoutRoot.CornerRadius = new CornerRadius(Shape switch
            {
                ProfilePictureShape.Superellipse => Width / 4,
                ProfilePictureShape.Ellipse => Width / 2,
                _ => 0
            });
        }

        private void UpdateFontSize()
        {
            if (Initials == null || double.IsNaN(Width))
            {
                return;
            }

            var fontSize = Width switch
            {
                < 30 => 12,
                < 36 => 14,
                < 48 => 16,
                < 64 => 20,
                < 96 => 24,
                < 120 => 32,
                _ => 64
            };

            if (_fontSize != fontSize)
            {
                _fontSize = fontSize;
                Initials.FontSize = fontSize;
            }
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
            ((ProfilePicture)d).UpdateCornerRadius();
        }

        #endregion

        public void Clear()
        {
            UpdateManager.Unsubscribe(this, ref _fileToken, true);

            _fileId = null;
            _referenceId = null;

            _parameters = null;

            Source = null;
        }

        #region Source

        public object Source
        {
            get => (object)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(object), typeof(ProfilePicture), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ProfilePicture)d).OnSourceChanged((object)e.NewValue);
        }

        private void OnSourceChanged(object newValue)
        {
            if (LayoutRoot == null)
            {
                return;
            }

            if (newValue is PlaceholderImage placeholder)
            {
                Gradient.GradientStops[0].Color = placeholder.TopColor;
                Gradient.GradientStops[1].Color = placeholder.BottomColor;

                LayoutRoot.Background = Gradient;

                Initials.Visibility = Visibility.Visible;
                Initials.Text = placeholder.Initials;

                if (_glyph != placeholder.IsGlyph)
                {
                    _glyph = placeholder.IsGlyph;
                    Initials.Margin = new Thickness(0, 1, 0, _glyph ? 0 : 2);
                }
            }
            else if (newValue is ImageSource source)
            {
                Texture.ImageSource = source;

                LayoutRoot.Background = Texture;

                Initials.Visibility = Visibility.Collapsed;
            }

            UpdateCornerRadius();
            UpdateFontSize();
        }

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
            else if (_parameters is ChatPhotoParameters chatPhoto)
            {
                SetChatPhoto(chatPhoto.ClientService, chatPhoto.Photo, chatPhoto.Side, false);
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
            UpdateManager.Unsubscribe(this, ref _fileToken, true);

            if (_referenceId != chat.Id || _fileId != file?.Id || Source == null || !download)
            {
                _referenceId = chat.Id;
                _fileId = file?.Id;

                Source = GetChat(clientService, chat, file, side, out var shape, download);
                Shape = shape;
            }
        }

        private object GetChat(IClientService clientService, Chat chat, File file, int side, out ProfilePictureShape shape, bool download = true)
        {
            // TODO: this method may throw a NullReferenceException in some conditions

            System.Diagnostics.Debug.Assert(side == Width);

            shape = ProfilePictureShape.Ellipse;

            if (chat.Type is ChatTypePrivate privata && clientService.IsSavedMessages(chat))
            {
                return PlaceholderImage.GetGlyph(Icons.BookmarkFilled, 5);
            }
            else if (clientService.IsRepliesChat(chat))
            {
                return PlaceholderImage.GetGlyph(Icons.ChatMultipleFilled, 5);
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
                return PlaceholderImage.GetGlyph(Icons.GhostFilled, long.MinValue);
            }

            if (chat.Photo?.Minithumbnail != null)
            {
                return PlaceholderHelper.GetBlurred(chat.Photo.Minithumbnail.Data);
            }

            return PlaceholderImage.GetChat(chat);
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
            UpdateManager.Unsubscribe(this, ref _fileToken, true);

            if (_referenceId != user.Id || _fileId != file?.Id || Source == null || !download)
            {
                _referenceId = user.Id;
                _fileId = file?.Id;

                Source = GetUser(clientService, user, file, side, download);
                Shape = ProfilePictureShape.Ellipse;
            }
        }

        private object GetUser(IClientService clientService, User user, File file, int side, bool download = true)
        {
            System.Diagnostics.Debug.Assert(side == Width);

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
                return PlaceholderImage.GetGlyph(Icons.GhostFilled, long.MinValue);
            }

            if (user.ProfilePhoto?.Minithumbnail != null)
            {
                return PlaceholderHelper.GetBlurred(user.ProfilePhoto.Minithumbnail.Data);
            }

            return PlaceholderImage.GetUser(user);
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
            UpdateManager.Unsubscribe(this, ref _fileToken, true);

            Source = GetChat(clientService, chat, file, side, download);
            Shape = ProfilePictureShape.Ellipse;
        }

        private object GetChat(IClientService clientService, ChatInviteLinkInfo chat, File file, int side, bool download = true)
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

            return PlaceholderImage.GetChat(chat);
        }

        #endregion

        #region Chat invite

        struct ChatPhotoParameters
        {
            public IClientService ClientService;
            public ChatPhoto Photo;
            public int Side;

            public ChatPhotoParameters(IClientService clientService, ChatPhoto photo, int side)
            {
                ClientService = clientService;
                Photo = photo;
                Side = side;
            }
        }

        public void SetChatPhoto(IClientService clientService, ChatPhoto photo, int side, bool download = true)
        {
            SetChatPhoto(clientService, photo, photo.GetBig()?.Photo, side, download);
        }

        private void SetChatPhoto(IClientService clientService, ChatPhoto photo, File file, int side, bool download = true)
        {
            UpdateManager.Unsubscribe(this, ref _fileToken, true);

            Source = GetChatPhoto(clientService, photo, file, side, download);
            Shape = ProfilePictureShape.Ellipse;
        }

        private object GetChatPhoto(IClientService clientService, ChatPhoto photo, File file, int side, bool download = true)
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

                    _parameters = new ChatPhotoParameters(clientService, photo, side);
                    UpdateManager.Subscribe(this, clientService, file, ref _fileToken, UpdateFile, true);
                }
            }

            if (photo.Minithumbnail != null)
            {
                return PlaceholderHelper.GetBlurred(photo.Minithumbnail.Data);
            }

            return null;
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
                    Source = PlaceholderImage.GetNameForUser(fromImport.SenderName);
                    Shape = ProfilePictureShape.Ellipse;
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser fromHiddenUser)
                {
                    Source = PlaceholderImage.GetNameForUser(fromHiddenUser.SenderName);
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

    public class PlaceholderImage
    {
        public string Initials { get; }

        public bool IsGlyph { get; }

        public Color TopColor { get; }

        public Color BottomColor { get; }

        public PlaceholderImage(string initials, bool isGlyph, long id)
        {
            Initials = initials;
            IsGlyph = isGlyph;

            if (id == long.MinValue)
            {
                TopColor = _disabledTop;
                BottomColor = _disabled;
            }
            else
            {
                TopColor = _colorsTop[Math.Abs(id % _colors.Length)];
                BottomColor = _colors[Math.Abs(id % _colors.Length)];
            }
        }

        #region Static stuff

        private static readonly Color[] _colorsTop = new Color[7]
        {
            Color.FromArgb(0xFF, 0xEF, 0x8E, 0x67),
            Color.FromArgb(0xFF, 0xF7, 0xCE, 0x79),
            Color.FromArgb(0xFF, 0x8C, 0xAF, 0xF9),
            Color.FromArgb(0xFF, 0xAC, 0xDC, 0x89),
            Color.FromArgb(0xFF, 0x81, 0xE9, 0xD6),
            Color.FromArgb(0xFF, 0x8A, 0xD3, 0xF9),
            Color.FromArgb(0xFF, 0xFF, 0xAF, 0xC7),
        };

        private static readonly Color[] _colors = new Color[7]
        {
            Color.FromArgb(0xFF, 0xEC, 0x5F, 0x6D),
            Color.FromArgb(0xFF, 0xF2, 0xAC, 0x6A),
            Color.FromArgb(0xFF, 0x65, 0x60, 0xF6),
            Color.FromArgb(0xFF, 0x75, 0xC8, 0x73),
            Color.FromArgb(0xFF, 0x62, 0xC6, 0xB7),
            Color.FromArgb(0xFF, 0x51, 0x9D, 0xEA),
            Color.FromArgb(0xFF, 0xF2, 0x74, 0x9A),
        };

        private static readonly Color _disabledTop = Color.FromArgb(0xFF, 0xA6, 0xAB, 0xB7);
        private static readonly Color _disabled = Color.FromArgb(0xFF, 0x86, 0x89, 0x92);

        public static SolidColorBrush GetBrush(long i)
        {
            return new SolidColorBrush(_colors[Math.Abs(i % _colors.Length)]);
        }

        public static CompositionBrush GetBrush(Compositor compositor, long i)
        {
            return compositor.CreateColorBrush(_colors[Math.Abs(i % _colors.Length)]);
        }

        public static PlaceholderImage GetChat(Chat chat)
        {
            if (chat.Type is ChatTypePrivate privata)
            {
                return new PlaceholderImage(InitialNameStringConverter.Convert(chat), false, privata.UserId);
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                return new PlaceholderImage(InitialNameStringConverter.Convert(chat), false, secret.UserId);
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                return new PlaceholderImage(InitialNameStringConverter.Convert(chat), false, basic.BasicGroupId);
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                return new PlaceholderImage(InitialNameStringConverter.Convert(chat), false, super.SupergroupId);
            }

            return new PlaceholderImage(InitialNameStringConverter.Convert(chat), false, 5);
        }

        public static PlaceholderImage GetChat(ChatInviteLinkInfo chat)
        {
            if (chat.ChatId != 0)
            {
                return new PlaceholderImage(InitialNameStringConverter.Convert(chat.Title), false, chat.ChatId);
            }

            return new PlaceholderImage(InitialNameStringConverter.Convert(chat.Title), false, 5);
        }

        public static PlaceholderImage GetUser(User user)
        {
            return new PlaceholderImage(InitialNameStringConverter.Convert(user), false, user.Id);
        }

        public static PlaceholderImage GetNameForUser(string firstName, string lastName, long id = 5)
        {
            return new PlaceholderImage(InitialNameStringConverter.Convert(firstName, lastName), false, id);
        }

        public static PlaceholderImage GetNameForUser(string name, long id = 5)
        {
            return new PlaceholderImage(InitialNameStringConverter.Convert((object)name), false, id);
        }

        public static PlaceholderImage GetNameForChat(string title, long id = 5)
        {
            return new PlaceholderImage(InitialNameStringConverter.Convert(title), false, id);
        }

        public static PlaceholderImage GetGlyph(string glyph, long id = 5)
        {
            return new PlaceholderImage(glyph, true, id);
        }

        #endregion
    }
}
