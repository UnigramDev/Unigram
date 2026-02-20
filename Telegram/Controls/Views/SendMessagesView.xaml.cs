//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Services;
using Telegram.Services.Factories;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Views
{
    public delegate void SendWithChat(Chat chat, Action<MessageSendOptions, MessageTopic> action);

    public sealed partial class SendMessagesView : UserControl
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly ShareOperation _shareOperation;

        private readonly HashSet<MessageId> _trackedMessages = new();
        private readonly HashSet<File> _trackedFiles = new();

        public SendMessagesView(IClientService clientService, IEventAggregator aggregator, ShareOperation shareOperation, FormattedText caption, List<Chat> chats, SendWithChat action)
        {
            InitializeComponent();

            _clientService = clientService;
            _aggregator = aggregator;
            _shareOperation = shareOperation;

            aggregator.Subscribe<UpdateMessageSendSucceeded>(this, Handle)
                .Subscribe<UpdateMessageSendFailed>(Handle)
                .Subscribe<UpdateDeleteMessages>(Handle);

            Initialize(chats, action, shareOperation, caption);
        }

        private async void Initialize(List<Chat> chats, SendWithChat action, ShareOperation shareOperation, FormattedText caption)
        {
            try
            {
                if (shareOperation.Data.AvailableFormats.Contains(StandardDataFormats.Bitmap))
                {
                    var bitmap = await shareOperation.Data.GetBitmapAsync();

                    var fileName = string.Format("image_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.png", DateTime.Now);
                    var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                    using (var source = await bitmap.OpenReadAsync())
                    using (var destination = await cache.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await RandomAccessStream.CopyAsync(
                            source.GetInputStreamAt(0),
                            destination.GetOutputStreamAt(0));
                    }

                    var photo = await StorageMedia.CreateAsync(cache);
                    if (photo != null)
                    {
                        photo.IsScreenshot = true;

                        var media = new List<StorageMedia>();
                        media.Add(photo);

                        Initialize(chats, action, media, caption);
                    }
                }
                else if (shareOperation.Data.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await shareOperation.Data.GetStorageItemsAsync();
                    var files = new List<StorageFile>(items.Count);

                    foreach (var file in items.OfType<StorageFile>())
                    {
                        files.Add(file);
                    }

                    var media = await StorageMedia.CreateAsync(files);
                    if (media.Count > 0)
                    {
                        Initialize(chats, action, media, caption);
                    }
                }
                else if (shareOperation.Data.AvailableFormats.Contains(StandardDataFormats.WebLink))
                {
                    var link = await shareOperation.Data.GetWebLinkAsync();
                    var content = new List<object>();

                    if (caption?.Text.Length > 0)
                    {
                        content.Add(new InputMessageText(caption, null, false));
                    }

                    content.Add(new InputMessageText(link.AbsoluteUri.AsFormattedText(), null, false));

                    Initialize(chats, action, content);
                }

                shareOperation.TryReportDataRetrieved();
            }
            catch
            {
                shareOperation.TryReportError("Failed to retrieve data");
            }
        }

        private async void Initialize(List<Chat> chats, SendWithChat action, IList<StorageMedia> media, FormattedText caption)
        {
            var itemsView = ComposeViewModel.GetItemsView(media, true, false, true, true, true, true);
            var content = new List<object>();

            for (int i = 0; i < itemsView.Count; i++)
            {
                var item = itemsView[i];
                var itemCaption = i < itemsView.Count - 1 ? null : caption;

                if (item is StorageAlbum album)
                {
                    if (album.Media.Count > 1)
                    {
                        content.Add(await SendGroupedAsync(album.Media, itemCaption));
                    }
                    else if (album.Media.Count > 0)
                    {
                        content.Add(await SendStorageMediaAsync(album.Media[0], itemCaption));
                    }
                }
                else
                {
                    content.Add(await SendStorageMediaAsync(item, itemCaption));
                }
            }

            Initialize(chats, action, content);
        }

        private void Initialize(List<Chat> chats, SendWithChat action, IList<object> content)
        {
            foreach (var chat in chats)
            {
                action(chat, (options, topic) =>
                {
                    foreach (var item in content)
                    {
                        if (item is InputMessageContent input)
                        {
                            _clientService.Send(new SendMessage(chat.Id, topic, null, options, input), Track);
                        }
                        else if (item is List<InputMessageContent> album)
                        {
                            _clientService.Send(new SendMessageAlbum(chat.Id, topic, null, options, album), Track);
                        }
                    }
                });
            }
        }

        private void Track(Object result)
        {
            if (result is Message message)
            {
                TrackMessage(message);
            }
            else if (result is Td.Api.Messages messages)
            {
                foreach (var item in messages.MessagesValue)
                {
                    TrackMessage(item);
                }
            }
        }

        private void TrackMessage(Message message)
        {
            _trackedMessages.Add(new MessageId(message.ChatId, message.Id));

            if (message.SendingState == null)
            {
                this.BeginOnUIThread(() => UpdateMessages(new MessageId(message.ChatId, message.Id)));
                return;
            }

            var file = message.GetFile();
            if (file != null)
            {
                _trackedFiles.Add(file);

                var token = 0L;
                UpdateManager.Subscribe(this, _clientService, file, ref token, Handle);
            }
        }

        private async Task<InputMessageContent> SendStorageMediaAsync(StorageMedia storage, FormattedText caption)
        {
            if (storage is StorageDocument or StorageAudio)
            {
                return await SendDocumentAsync(storage, caption);
            }
            else if (storage is StoragePhoto photo)
            {
                return await SendPhotoAsync(photo, caption);
            }
            else if (storage is StorageVideo video)
            {
                return await SendVideoAsync(video, caption);
            }

            return null;
        }

        private async Task<InputMessageContent> SendDocumentAsync(StorageMedia file, FormattedText caption)
        {
            var factory = await MessageFactory.CreateDocumentAsync(file, caption, false);
            if (factory is InputMessageContent input)
            {
                return input;
            }

            return null;
        }

        private async Task<InputMessageContent> SendPhotoAsync(StoragePhoto file, FormattedText caption)
        {
            var factory = await MessageFactory.CreatePhotoAsync(file, caption, false, false, false, null, 0);
            if (factory is InputMessageContent input)
            {
                return input;
            }

            return null;
        }

        public async Task<InputMessageContent> SendVideoAsync(StorageVideo video, FormattedText caption)
        {
            var factory = await MessageFactory.CreateVideoAsync(video, caption, false, false, false, null, 0);
            if (factory is InputMessageContent input)
            {
                return input;
            }

            return null;
        }

        private async Task<List<InputMessageContent>> SendGroupedAsync(IList<StorageMedia> items, FormattedText caption)
        {
            var operations = new List<InputMessageContent>();
            var audio = items.All(x => x is StorageAudio);

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (audio)
                {
                    var factory = await MessageFactory.CreateDocumentAsync(item, i == items.Count - 1 ? caption : null, false);
                    if (factory is InputMessageContent input)
                    {
                        operations.Add(input);
                    }
                }
                else if (item is StoragePhoto photo)
                {
                    var factory = await MessageFactory.CreatePhotoAsync(photo, i == 0 ? caption : null, false, false, false, null, 0);
                    if (factory is InputMessageContent input)
                    {
                        operations.Add(input);
                    }
                }
                else if (item is StorageVideo video)
                {
                    var factory = await MessageFactory.CreateVideoAsync(video, i == 0 ? caption : null, false, false, false, null, 0);
                    if (factory is InputMessageContent input)
                    {
                        operations.Add(input);
                    }
                }
            }

            return operations;
        }

        private void Handle(UpdateMessageSendSucceeded update)
        {
            this.BeginOnUIThread(() => UpdateMessages(new MessageId(update.Message.ChatId, update.OldMessageId)));
        }

        private void Handle(UpdateMessageSendFailed update)
        {
            this.BeginOnUIThread(() => UpdateMessages(new MessageId(update.Message.ChatId, update.OldMessageId)));
        }

        private void Handle(UpdateDeleteMessages update)
        {
            if (update.FromCache)
            {
                return;
            }

            foreach (var message in update.MessageIds)
            {
                this.BeginOnUIThread(() => UpdateMessages(new MessageId(update.ChatId, message)));
            }
        }

        private void Handle(object target, File update)
        {
            this.BeginOnUIThread(UpdateFiles);
        }

        private void UpdateFiles()
        {
            double maximum = _trackedFiles.Sum(x => Math.Max(x.Size, x.ExpectedSize));
            double value = _trackedFiles.Sum(file =>
            {
                var size = Math.Max(file.Size, file.ExpectedSize);
                var generating = file.Local.DownloadedSize < size;

                return generating ? file.Local.DownloadedSize : file.Remote.UploadedSize;
            });

            Progress.IsIndeterminate = false;
            Progress.Maximum = 1.2;
            Progress.Value = value / maximum;
        }

        private void UpdateMessages(MessageId message)
        {
            if (_trackedMessages.Contains(message))
            {
                _trackedMessages.Remove(message);
            }
            else
            {
                return;
            }

            if (_trackedMessages.Count > 0)
            {
                return;
            }

            if (_canceled)
            {
                _shareOperation.TryReportCompleted();
            }
            else
            {
                Animated.LoopCompleted += Animated_LoopCompleted;

                Progress.Maximum = 1.2;
                Progress.Value = 1.2;
            }
        }

        private bool _completed;
        private bool _canceled;

        private void Animated_LoopCompleted(object sender, AnimatedImageLoopCompletedEventArgs e)
        {
            this.BeginOnUIThread(OnLoopCompleted);
        }

        private void OnLoopCompleted()
        {
            if (_completed)
            {
                _shareOperation.TryReportCompleted();
            }
            else
            {
                _completed = true;

                using (Animated.BeginBatchUpdate())
                {
                    Animated.LoopCount = 1;
                    Animated.Source = new LocalFileSource("ms-appx:///Assets/Animations/SendMessagesCompleted.tgs");
                }
            }
        }

        public void Cancel()
        {
            _canceled = true;

            foreach (var messages in _trackedMessages.GroupBy(x => x.ChatId).ToList())
            {
                _clientService.Send(new DeleteMessages(messages.Key, messages.Select(x => x.Id).ToList(), true));
            }
        }
    }
}
