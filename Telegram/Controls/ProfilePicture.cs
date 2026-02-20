//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public enum ProfilePictureShape
    {
        None,
        Ellipse,
        Superellipse,
        Tail,
        Auto
    }

    public partial class ProfilePicture : ControlEx
    {
        private Border LayoutRoot;
        private LinearGradientBrush Gradient;
        private TextBlock Initials;

        private ProfilePictureShape _appliedShape;
        private int _appliedSize;

        private int _fontSize;
        private bool _glyph;
        private bool _tail;
        private bool _invalidated;

        private bool _templateApplied;

        private ProfilePicturePresenter _presenter;

        public ProfilePicture()
        {
            DefaultStyleKey = typeof(ProfilePicture);
        }

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Border;
            Initials = GetTemplateChild(nameof(Initials)) as TextBlock;

            Gradient = new LinearGradientBrush();
            Gradient.StartPoint = new Windows.Foundation.Point(0, 0);
            Gradient.EndPoint = new Windows.Foundation.Point(0, 1);
            Gradient.GradientStops.Add(new GradientStop { Offset = 0 });
            Gradient.GradientStops.Add(new GradientStop { Offset = 1 });

            _templateApplied = true;
            InvalidateShape();

            base.OnApplyTemplate();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            availableSize = new Size(Size, Size);

            LayoutRoot.Measure(availableSize);
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            LayoutRoot.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }

        protected override void OnLoaded()
        {
            Load();
        }

        protected override void OnUnloaded()
        {
            Unload();
        }

        private void Load()
        {
            var source = Source;
            if (source != null && IsConnected)
            {
                if (source is ProfilePictureSourceText or ProfilePictureSourceBitmap)
                {
                    _presenter?.Unload(this);
                    _presenter = null;

                    Invalidate(source);
                }
                else
                {
                    var presentation = new ProfilePicturePresentation(source, Size);

                    if (_presenter == null || _presenter.Presentation != presentation)
                    {
                        if (IsCachingEnabled || (_presenter != null && !_presenter.IsCachingEnabled))
                        {
                            _presenter?.Unload(this);
                            _presenter = Loader.Current.GetOrCreate(presentation, true);
                        }
                        else
                        {
                            if (_presenter == null || _presenter.IsCachingEnabled)
                            {
                                _presenter = Loader.Current.GetOrCreate(presentation, false);
                            }
                            else
                            {
                                _presenter.Presentation = presentation;
                            }
                        }

                        _presenter.Load(this);
                    }
                }
            }
            else if (source == null)
            {
                Unload();
            }
        }

        private void Unload()
        {
            if (_presenter != null)
            {
                _presenter.Unload(this);
                _presenter = null;
            }
            else if (_invalidated)
            {
                Invalidate(null);
            }
        }

        private ProfilePictureSource _source;
        public ProfilePictureSource Source
        {
            get => _source;
            set
            {
                if (_source != value)
                {
                    _source = value;
                    InvalidateShape();
                    Load();
                }
            }
        }

        private int _size;
        public int Size
        {
            get => _size;
            set
            {
                if (_size != value)
                {
                    _size = value;
                    InvalidateMeasure();
                    InvalidateShape();
                    Load();
                }
            }
        }

        private ProfilePictureShape _shape = ProfilePictureShape.Auto;
        public ProfilePictureShape Shape
        {
            get => _shape;
            set
            {
                if (_shape != value)
                {
                    _shape = value;
                    InvalidateShape();
                }
            }
        }

        public ProfilePictureShape ComputedShape
        {
            get
            {
                if (_shape == ProfilePictureShape.Auto)
                {
                    if (_source != null)
                    {
                        return _source.Shape;
                    }

                    return ProfilePictureShape.Ellipse;
                }

                return _shape;
            }
        }

        private bool _isCachingEnabled;
        public bool IsCachingEnabled
        {
            get => _isCachingEnabled;
            set
            {
                if (_isCachingEnabled != value)
                {
                    _isCachingEnabled = value;
                    Load();
                }
            }
        }

        private void Invalidate(object newValue)
        {
            if (LayoutRoot == null)
            {
                return;
            }

            if (newValue is ImageBrush texture)
            {
                _invalidated = true;
                LayoutRoot.Background = texture;
                Initials.Visibility = Visibility.Collapsed;
            }
            else if (newValue is ProfilePictureSourceText text)
            {
                _invalidated = true;
                Gradient.GradientStops[0].Color = text.TopColor;
                Gradient.GradientStops[1].Color = text.BottomColor;

                LayoutRoot.Background = Gradient;

                Initials.Visibility = Visibility.Visible;
                Initials.Text = text.Initials;

                if (_glyph != text.IsGlyph)
                {
                    _glyph = text.IsGlyph;
                    Initials.Margin = new Thickness(0, 1, 0, _glyph ? 0 : 2);
                }

                InvalidateFontSize();
            }
            else if (newValue is ProfilePictureSourceBitmap bitmap)
            {
                _invalidated = true;
                LayoutRoot.Background = new ImageBrush
                {
                    ImageSource = bitmap.Bitmap,
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
                Initials.Visibility = Visibility.Collapsed;
            }
            else
            {
                _invalidated = false;
                LayoutRoot.Background = null;
                Initials.Visibility = Visibility.Collapsed;
            }
        }

        private void InvalidateShape()
        {
            var shape = ComputedShape;
            var size = Size;

            if (shape == _appliedShape && size == _appliedSize)
            {
                return;
            }

            if (LayoutRoot == null || size == 0)
            {
                return;
            }

            _appliedShape = shape;
            _appliedSize = size;

            if (shape == ProfilePictureShape.Tail)
            {
                _tail = true;

                static CompositionPath GetTail(float radius)
                {
                    CanvasGeometry result;
                    using (var builder = new CanvasPathBuilder(null))
                    {
                        var cy = radius;
                        var cx = radius;
                        var r = radius;

                        float b = cy + r;
                        float x = r / 81.0f;

                        float startAngle = -180 * (MathF.PI / 180);
                        float sweepAngle = 270 * (MathF.PI / 180);

                        float x1 = cx + MathF.Cos(startAngle) * r;
                        float y1 = cy + MathF.Sin(startAngle) * r;

                        float x2 = cx + MathF.Cos(startAngle + sweepAngle) * r;
                        float y2 = cy + MathF.Sin(startAngle + sweepAngle) * r;

                        builder.BeginFigure(new Vector2(x1, y1));
                        builder.AddArc(new Vector2(x2, y2), r, r, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Large);
                        builder.AddCubicBezier(new Vector2(cx - 13 * x, b), new Vector2(cx - 25 * x, b - 3 * x), new Vector2(cx - 36f * x, b - 8.42f * x));
                        builder.AddCubicBezier(new Vector2(cx - 52 * x, b - x), new Vector2(cx - 56.5f * x, b - x), new Vector2(cx - 78.02f * x, b - x));
                        builder.AddCubicBezier(new Vector2(cx - 80 * x, b - x), new Vector2(cx - 81 * x, b - 3 * x), new Vector2(cx - 79.52f * x, b - 4.5f * x));
                        builder.AddCubicBezier(new Vector2(cx - 78 * x, b - 6 * x), new Vector2(cx - 63.73f * x, b - 15 * x), new Vector2(cx - 63.73f * x, b - 31 * x));
                        builder.AddCubicBezier(new Vector2(cx - 74.5f * x, b - 44.75f * x), new Vector2(cx - r, cy + 18.87f * x), new Vector2(cx - r, cy));
                        builder.EndFigure(CanvasFigureLoop.Closed);
                        result = CanvasGeometry.CreatePath(builder);
                    }
                    return new CompositionPath(result);
                }

                var compositor = BootStrapper.Current.Compositor;

                var polygon = compositor.CreatePathGeometry();
                polygon.Path = GetTail(Size / 2f);

                var visual = ElementComposition.GetElementVisual(this);
                visual.Clip = compositor.CreateGeometricClip(polygon);
            }
            else if (_tail)
            {
                _tail = false;

                var visual = ElementComposition.GetElementVisual(this);
                visual.Clip = null;
            }

            LayoutRoot.CornerRadius = new CornerRadius(shape switch
            {
                ProfilePictureShape.Superellipse => size / 4d,
                ProfilePictureShape.Ellipse => size / 2d,
                _ => 0
            });
        }

        private void InvalidateFontSize()
        {
            if (Initials == null || Size == 0)
            {
                return;
            }

            var fontSize = Size switch
            {
                < 20 => 10,
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

        public record ProfilePicturePresentation(ProfilePictureSource Source, int Size);

        public class ProfilePicturePresenter
        {
            public enum State
            {
                Download,
                Update
            }

            private readonly Loader _loader;
            private ProfilePicturePresentation _presentation;

            private readonly ImageBrush _texture;
            private readonly ThumbnailController _controller;

            private readonly DispatcherQueue _dispatcherQueue;

            private readonly HashSet<ProfilePicture> _pictures = new();

            private int? _fileId;
            private long _fileToken;

            private object _source;

            public ProfilePicturePresenter(Loader loader, ProfilePicturePresentation presentation)
            {
                _loader = loader;
                _presentation = presentation;

                _texture = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                _controller = new ThumbnailController(_texture);

                _dispatcherQueue = loader.DispatcherQueue;
                IsCachingEnabled = true;
            }

            public ProfilePicturePresenter(DispatcherQueue dispatcherQueue, ProfilePicturePresentation presentation)
            {
                _presentation = presentation;

                _texture = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                _controller = new ThumbnailController(_texture);

                _dispatcherQueue = dispatcherQueue;
                IsCachingEnabled = false;
            }

            public bool IsCachingEnabled { get; }

            public ProfilePicturePresentation Presentation
            {
                get => _presentation;
                set
                {
                    if (_loader == null)
                    {
                        _presentation = value;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }

            public void Load(ProfilePicture picture)
            {
                _pictures.Add(picture);
                picture.Invalidate(_source);

                Load(State.Download);
            }

            private void Load(State state)
            {
                var source = _presentation.Source;
                if (source is ProfilePictureSourcePhoto sourcePhoto && (_fileId != sourcePhoto.Photo.Id || state != State.Download))
                {
                    _fileId = sourcePhoto.Photo.Id;
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    Invalidate(sourcePhoto, _presentation.Size, state);
                }
                else if (source is ProfilePictureSourceText sourceText)
                {
                    _fileId = null;
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    Invalidate(sourceText);
                }
            }

            private void Invalidate(ProfilePictureSourcePhoto photo, int side, State state = State.Download)
            {
                if (photo.Photo.Local.IsDownloadingCompleted)
                {
                    _controller.Bitmap(photo.Photo.Local.Path, side, side, photo.Id);
                    Invalidate(_texture);

                    return;
                }
                else
                {
                    if (photo.Photo.Local.CanBeDownloaded && !photo.Photo.Local.IsDownloadingActive && state != State.Update)
                    {
                        photo.ClientService.DownloadFile(photo.Photo.Id, 1);
                    }

                    UpdateManager.Subscribe(this, photo.ClientService, photo.Photo, ref _fileToken, UpdateFile, true);
                }

                if (photo.Minithumbnail != null)
                {
                    _controller.Blur(photo.Minithumbnail.Data, 3, photo.Id);
                    Invalidate(_texture);

                    return;
                }

                _controller.Recycle();
                Invalidate(photo.Text);
            }

            private void Invalidate(object value)
            {
                if (_source != value)
                {
                    _source = value;

                    foreach (var picture in _pictures)
                    {
                        picture.Invalidate(value);
                    }
                }
            }

            private void UpdateFile(object target, File file)
            {
                _dispatcherQueue.TryEnqueue(() => Load(State.Update));
            }

            public void Unload(ProfilePicture picture)
            {
                _pictures.Remove(picture);
                picture.Invalidate(null);

                if (_pictures.Empty())
                {
                    UpdateManager.Unsubscribe(this, ref _fileToken);

                    _controller.Recycle();
                    _loader?.Unload(this);
                }
            }
        }

        public class Loader
        {
            [ThreadStatic]
            private static Loader _current;
            public static Loader Current => _current ??= new();

            public static void Release()
            {
                _current = null;
            }

            private readonly Dictionary<ProfilePicturePresentation, ProfilePicturePresenter> _presenters = new();
            private readonly DispatcherQueue _dispatcherQueue;

            private Loader()
            {
                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            }

            public DispatcherQueue DispatcherQueue => _dispatcherQueue;

            public ProfilePicturePresenter GetOrCreate(ProfilePicturePresentation presentation, bool isCachingEnabled)
            {
                if (isCachingEnabled)
                {
                    if (_presenters.TryGetValue(presentation, out var presenter))
                    {
                        return presenter;
                    }

                    presenter = new ProfilePicturePresenter(this, presentation);
                    _presenters.Add(presentation, presenter);

                    return presenter;
                }

                return new ProfilePicturePresenter(_dispatcherQueue, presentation);
            }

            public void Unload(ProfilePicturePresenter presenter)
            {
                _presenters.Remove(presenter.Presentation);
            }
        }
    }

    public abstract record ProfilePictureSource(ProfilePictureShape Shape)
    {
        public static ProfilePictureSource Message(MessageViewModel message)
        {
            if (message.IsSaved || message.IsVerificationCode)
            {
                if (message.ForwardInfo?.Origin is MessageOriginUser fromUser && message.ClientService.TryGetUser(fromUser.SenderUserId, out User fromUserUser))
                {
                    return ProfilePictureSource.User(message.ClientService, fromUserUser);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChat fromChat && message.ClientService.TryGetChat(fromChat.SenderChatId, out Chat fromChatChat))
                {
                    return ProfilePictureSource.Chat(message.ClientService, fromChatChat);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel && message.ClientService.TryGetChat(fromChannel.ChatId, out Chat fromChannelChat))
                {
                    return ProfilePictureSource.Chat(message.ClientService, fromChannelChat);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser fromHiddenUser)
                {
                    return ProfilePictureSourceText.GetNameForUser(fromHiddenUser.SenderName, long.MinValue);
                }
                else if (message.ImportInfo != null)
                {
                    return ProfilePictureSourceText.GetNameForUser(message.ImportInfo.SenderName, long.MinValue);
                }
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                return ProfilePictureSource.User(message.ClientService, senderUser);
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                return ProfilePictureSource.Chat(message.ClientService, senderChat);
            }

            return null;
        }

        public static ProfilePictureSource MessageSender(IClientService clientService, MessageSender sender)
        {
            if (clientService.TryGetUser(sender, out User user))
            {
                return ProfilePictureSource.User(clientService, user);
            }
            else if (clientService.TryGetChat(sender, out Chat chat))
            {
                return ProfilePictureSource.Chat(clientService, chat);
            }

            return null;
        }

        public static ProfilePictureSource User(IClientService clientService, User user)
        {
            ProfilePictureSourceText text;
            if (user.Type is UserTypeDeleted)
            {
                text = ProfilePictureSourceText.GetGlyph(Icons.GhostFilled, long.MinValue);
            }
            else
            {
                text = ProfilePictureSourceText.GetUser(clientService, user);
            }

            var photo = user.ProfilePhoto;
            if (photo != null)
            {
                return new ProfilePictureSourcePhoto(clientService, user.Id, photo.Small, photo.Minithumbnail, text, ProfilePictureShape.Ellipse);
            }

            return text;
        }

        public static ProfilePictureSource ChatPhoto(IClientService clientService, User user, ChatPhoto chatPhoto, bool big)
        {
            ProfilePictureSourceText text;
            if (user.Type is UserTypeDeleted)
            {
                text = ProfilePictureSourceText.GetGlyph(Icons.GhostFilled, long.MinValue);
            }
            else
            {
                text = ProfilePictureSourceText.GetUser(clientService, user);
            }

            var photo = big ? chatPhoto?.GetBig() : chatPhoto?.GetSmall();
            if (photo != null)
            {
                return new ProfilePictureSourcePhoto(clientService, user.Id, photo.Photo, chatPhoto.Minithumbnail, text, ProfilePictureShape.Ellipse);
            }

            return text;
        }

        public static ProfilePictureSource ChatPhoto(IClientService clientService, Chat chat, ChatPhoto chatPhoto, bool big)
        {
            ProfilePictureSourceText text;
            text = ProfilePictureSourceText.GetChat(clientService, chat);

            var photo = big ? chatPhoto?.GetBig() : chatPhoto?.GetSmall();
            if (photo != null)
            {
                return new ProfilePictureSourcePhoto(clientService, chat.Id, photo.Photo, chatPhoto.Minithumbnail, text, ProfilePictureShape.Ellipse);
            }

            return text;
        }

        public static ProfilePictureSource Chat(IClientService clientService, Chat chat)
        {
            if (chat.Id == clientService.Options.MyId)
            {
                return ProfilePictureSourceText.GetGlyph(Icons.BookmarkFilled, 5);
            }
            else if (chat.Id == clientService.Options.RepliesBotChatId)
            {
                return ProfilePictureSourceText.GetGlyph(Icons.ArrowReplyFilled, 5);
            }

            var shape = ProfilePictureShape.Ellipse;
            if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.IsForum)
                {
                    shape = ProfilePictureShape.Superellipse;
                }
                else if (supergroup.IsDirectMessagesGroup)
                {
                    shape = ProfilePictureShape.Tail;
                }
            }

            ProfilePictureSourceText text;
            if (supergroup == null && clientService.TryGetUser(chat, out User user))
            {
                if (user.Type is UserTypeDeleted)
                {
                    text = ProfilePictureSourceText.GetGlyph(Icons.GhostFilled, long.MinValue);
                }
                else
                {
                    text = ProfilePictureSourceText.GetUser(clientService, user);
                }
            }
            else
            {
                text = ProfilePictureSourceText.GetChat(clientService, chat, shape);
            }

            var photo = chat.Photo;
            if (photo != null)
            {
                return new ProfilePictureSourcePhoto(clientService, chat.Id, photo.Small, photo.Minithumbnail, text, shape);
            }

            return text;
        }

        public static ProfilePictureSource Chat(IClientService clientService, ChatInviteLinkInfo chat)
        {
            ProfilePictureSourceText text;
            text = ProfilePictureSourceText.GetChat(clientService, chat);

            var photo = chat.Photo;
            if (photo != null)
            {
                return new ProfilePictureSourcePhoto(clientService, chat.ChatId, photo.Small, photo.Minithumbnail, text);
            }

            return text;
        }

        public static ProfilePictureSource Story(IClientService clientService, Story story)
        {
            if (story.Content is StoryContentPhoto photo)
            {
                var file = photo.Photo.GetSmall()?.Photo;
                if (file != null)
                {
                    return new ProfilePictureSourcePhoto(clientService, photo.Photo.Sizes[0].Photo.Id, file, photo.Photo.Minithumbnail);
                }
            }
            else if (story.Content is StoryContentVideo video)
            {
                var file = video.Video.Thumbnail?.File;
                if (file != null)
                {
                    return new ProfilePictureSourcePhoto(clientService, video.Video.Video.Id, file, video.Video.Minithumbnail);
                }
            }

            if (story.PosterId != null)
            {
                return ProfilePictureSource.MessageSender(clientService, story.PosterId);
            }

            if (clientService.TryGetChat(story.PosterChatId, out Chat chat))
            {
                return ProfilePictureSource.Chat(clientService, chat);
            }

            return null;
        }
    }

    public record ProfilePictureSourceBitmap(ImageSource Bitmap, ProfilePictureShape Shape = ProfilePictureShape.Ellipse)
        : ProfilePictureSource(Shape);

    public record ProfilePictureSourcePhoto(IClientService ClientService, long Id, File Photo, Minithumbnail Minithumbnail, ProfilePictureSourceText Text = null, ProfilePictureShape Shape = ProfilePictureShape.Ellipse)
        : ProfilePictureSource(Shape);

    public record ProfilePictureSourceText(string Initials, bool IsGlyph, Color TopColor, Color BottomColor, ProfilePictureShape Shape = ProfilePictureShape.Ellipse)
        : ProfilePictureSource(Shape)
    {
        private static readonly Color[] _colorsTop = new Color[7]
        {
            Color.FromArgb(0xFF, 0xEF, 0x8E, 0x67), // orange
            Color.FromArgb(0xFF, 0xF7, 0xCE, 0x79), // yellow
            Color.FromArgb(0xFF, 0x8C, 0xAF, 0xF9), // violet
            Color.FromArgb(0xFF, 0xAC, 0xDC, 0x89), // green
            Color.FromArgb(0xFF, 0x81, 0xE9, 0xD6), // teal
            Color.FromArgb(0xFF, 0x8A, 0xD3, 0xF9), // blue
            Color.FromArgb(0xFF, 0xFF, 0xAF, 0xC7), // rose
        };

        private static readonly Color[] _colors = new Color[7]
        {
            Color.FromArgb(0xFF, 0xEC, 0x5F, 0x6D), // orange
            Color.FromArgb(0xFF, 0xF2, 0xAC, 0x6A), // yellow
            Color.FromArgb(0xFF, 0x65, 0x60, 0xF6), // violet
            Color.FromArgb(0xFF, 0x75, 0xC8, 0x73), // green
            Color.FromArgb(0xFF, 0x62, 0xC6, 0xB7), // teal
            Color.FromArgb(0xFF, 0x51, 0x9D, 0xEA), // blue
            Color.FromArgb(0xFF, 0xF2, 0x74, 0x9A), // rose
        };

        private static readonly Color _disabledTop = Color.FromArgb(0xFF, 0xA6, 0xAB, 0xB7);
        private static readonly Color _disabled = Color.FromArgb(0xFF, 0x86, 0x89, 0x92);

        public static Color GetColor(long i)
        {
            if (i == -1)
            {
                return _disabled;
            }

            return _colors[Math.Abs(i % _colors.Length)];
        }

        public static SolidColorBrush GetBrush(long i, double opacity = 1)
        {
            return new SolidColorBrush(_colors[Math.Abs(i % _colors.Length)])
            {
                Opacity = opacity
            };
        }

        public static CompositionBrush GetBrush(Compositor compositor, long i)
        {
            return compositor.CreateColorBrush(_colors[Math.Abs(i % _colors.Length)]);
        }

        public static ProfilePictureSourceText GetChat(IClientService clientService, Chat chat, ProfilePictureShape shape = ProfilePictureShape.None)
        {
            if (shape == ProfilePictureShape.None)
            {
                shape = ProfilePictureShape.Ellipse;

                if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    if (supergroup.IsForum)
                    {
                        shape = ProfilePictureShape.Superellipse;
                    }
                    else if (supergroup.IsDirectMessagesGroup)
                    {
                        shape = ProfilePictureShape.Tail;
                    }
                }
            }

            return ProfilePictureSourceText.FromNameColor(InitialNameStringConverter.Convert(chat.Title), false, clientService.GetAccentColor(chat.AccentColorId), shape);
        }

        public static ProfilePictureSourceText GetChat(IClientService clientService, ChatInviteLinkInfo chat)
        {
            return ProfilePictureSourceText.FromNameColor(InitialNameStringConverter.Convert(chat.Title), false, clientService.GetAccentColor(chat.AccentColorId));
        }

        public static ProfilePictureSourceText GetUser(IClientService clientService, User user)
        {
            return ProfilePictureSourceText.FromNameColor(InitialNameStringConverter.Convert(user.FirstName, user.LastName), false, clientService.GetAccentColor(user.AccentColorId));
        }

        public static ProfilePictureSourceText GetNameForUser(string firstName, string lastName, long id = 5, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            return ProfilePictureSourceText.FromId(InitialNameStringConverter.Convert(firstName, lastName), false, id);
        }

        public static ProfilePictureSourceText GetNameForUser(string name, long id = 5, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            return ProfilePictureSourceText.FromId(InitialNameStringConverter.Convert(name), false, id);
        }

        public static ProfilePictureSourceText GetNameForChat(string title, long id = 5, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            return ProfilePictureSourceText.FromId(InitialNameStringConverter.Convert(title), false, id);
        }

        public static ProfilePictureSourceText GetGlyph(string glyph, long id = 5, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            return ProfilePictureSourceText.FromId(glyph, true, id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ProfilePictureSourceText FromNameColor(string initials, bool isGlyph, NameColor color, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            if (color == null)
            {
                return new ProfilePictureSourceText(initials, isGlyph, _disabledTop, _disabled, shape);
            }
            else
            {
                return new ProfilePictureSourceText(initials, isGlyph, _colorsTop[Math.Abs(color.BuiltInAccentColorId % _colors.Length)], _colors[Math.Abs(color.BuiltInAccentColorId % _colors.Length)], shape);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ProfilePictureSourceText FromId(string initials, bool isGlyph, long id, ProfilePictureShape shape = ProfilePictureShape.Ellipse)
        {
            if (id == long.MinValue)
            {
                return new ProfilePictureSourceText(initials, isGlyph, _disabledTop, _disabled, shape);
            }
            else
            {
                return new ProfilePictureSourceText(initials, isGlyph, _colorsTop[Math.Abs(id % _colors.Length)], _colors[Math.Abs(id % _colors.Length)], shape);
            }
        }
    }
}
