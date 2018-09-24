using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls
{
    public class ProfilePicture : HyperlinkButton, IHandle<UpdateFile>
    {
        private IEventAggregator _aggregator;
        private int _subscription = -1;
        private int _size;

        public ProfilePicture()
        {
            DefaultStyleKey = typeof(ProfilePicture);

            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_subscription != -1)
            {
                _aggregator?.Unsubscribe(this, _subscription);
            }
        }

        #region Source

        public ImageSource Source
        {
            get { return (ImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(ImageSource), typeof(ProfilePicture), new PropertyMetadata(null));

        #endregion

        #region Chat

        public void SetChat(IProtoService protoService, IEventAggregator aggregator, Chat chat, int size)
        {
            Source = GetChat(protoService, aggregator, chat, size);
        }

        private ImageSource GetChat(IProtoService protoService, IEventAggregator aggregator, Chat chat, int size)
        {
            if (chat.Type is ChatTypePrivate privata && protoService != null && protoService.IsChatSavedMessages(chat))
            {
                return PlaceholderHelper.GetSavedMessages(privata.UserId, size, size);
            }

            var file = chat.Photo?.Small;
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = size, DecodePixelHeight = size, DecodePixelType = DecodePixelType.Logical };
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    if (_subscription != file.Id && _subscription != -1)
                    {
                        aggregator?.Unsubscribe(this, _subscription);
                    }

                    _aggregator = aggregator;
                    _subscription = file.Id;
                    _size = size;

                    aggregator?.Subscribe(this, file.Id);
                    protoService?.Send(new DownloadFile(file.Id, 1));
                }
            }
            else
            {
                return PlaceholderHelper.GetChat(chat, size, size);
            }

            return null;
        }

        #endregion

        #region User

        public void SetUser(IProtoService protoService, IEventAggregator aggregator, User user, int size)
        {
            Source = GetUser(protoService, aggregator, user, size);
        }

        private ImageSource GetUser(IProtoService protoService, IEventAggregator aggregator, User user, int size)
        {
            if (_subscription != -1)
            {
                aggregator?.Unsubscribe(this, _subscription);
            }

            var file = user.ProfilePhoto?.Small;
            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    return new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = size, DecodePixelHeight = size, DecodePixelType = DecodePixelType.Logical };
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    _aggregator = aggregator;
                    _subscription = file.Id;
                    _size = size;

                    aggregator?.Subscribe(this, file.Id);
                    protoService?.Send(new DownloadFile(file.Id, 1));
                }
            }

            return PlaceholderHelper.GetUser(user, size, size);
        }

        #endregion

        public void Handle(UpdateFile update)
        {
            var file = update.File;
            if (file.Local.IsDownloadingCompleted && file.Id == _subscription)
            {
                _aggregator?.Unsubscribe(this, file.Id);

                this.BeginOnUIThread(() =>
                {
                    Source = new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = _size, DecodePixelHeight = _size, DecodePixelType = DecodePixelType.Logical };
                });
            }
        }
    }
}
