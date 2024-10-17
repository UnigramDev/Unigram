//
// Copyright Fela Ameghino & Contributors 2015-2024
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
using Telegram.Services.Factories;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public partial class DialogViewModel
    {
        public override void ViewSticker(Sticker sticker)
        {
            Delegate?.HideStickers();

            OpenSticker(sticker);
        }

        protected override void HideStickers()
        {
            Delegate?.HideStickers();
        }

        protected override void SetFormattedText(FormattedText text)
        {
            TextField?.SetText(text);
        }

        protected override bool CanSchedule => _type is DialogType.History or DialogType.Thread;

        public override async Task<MessageSendOptions> PickMessageSendOptionsAsync(bool? schedule = null, bool? silent = null, bool reorder = false)
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            if (schedule == true || (_type == DialogType.ScheduledMessages && schedule == null))
            {
                var user = ClientService.GetUser(chat);
                var popup = new ScheduleMessagePopup(user, ClientService.IsSavedMessages(chat));

                var confirm = await ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    return null;
                }

                if (popup.IsUntilOnline)
                {
                    return new MessageSendOptions(false, false, false, Settings.Stickers.DynamicPackOrder && reorder, new MessageSchedulingStateSendWhenOnline(), 0, 0, false);
                }
                else
                {
                    return new MessageSendOptions(false, false, false, Settings.Stickers.DynamicPackOrder && reorder, new MessageSchedulingStateSendAtDate(popup.Value.ToTimestamp()), 0, 0, false);
                }
            }
            else
            {
                return new MessageSendOptions(silent ?? false, false, false, Settings.Stickers.DynamicPackOrder && reorder, null, 0, 0, false);
            }
        }

        protected override void ContinueSendMessage(MessageSendOptions options)
        {
            if (Chat is not Chat chat)
            {
                return;
            }

            if (options?.SchedulingState != null && Type != DialogType.ScheduledMessages)
            {
                NavigationService.NavigateToChat(chat, scheduled: true);
            }
        }

        public async Task HandlePackageAsync(DataPackageView package)
        {
            try
            {
                if (false && package.AvailableFormats.Contains("application/x-tl-message"))
                {
                    var data = await package.GetDataAsync("application/x-tl-message") as IRandomAccessStream;
                    var reader = new DataReader(data.GetInputStreamAt(0));
                    var length = await reader.LoadAsync((uint)data.Size);

                    var chatId = reader.ReadInt64();
                    var messageId = reader.ReadInt64();

                    if (chatId == _chat?.Id)
                    {
                        return;
                    }

                    // TODO: this is a forward
                }

                if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
                {
                    var bitmap = await package.GetBitmapAsync();

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

                        var header = _composerHeader;
                        if (header?.EditingMessage != null)
                        {
                            await EditMediaAsync(photo);
                        }
                        else
                        {
                            var captionElements = new List<string>();

                            if (package.AvailableFormats.Contains(StandardDataFormats.Text))
                            {
                                var text = await package.GetTextAsync();
                                captionElements.Add(text);
                            }

                            FormattedText caption = null;
                            if (captionElements.Count > 0)
                            {
                                var resultCaption = string.Join(Environment.NewLine, captionElements);
                                caption = new FormattedText(resultCaption, Array.Empty<TextEntity>())
                                    .Substring(0, ClientService.Options.MessageCaptionLengthMax);
                            }

                            SendFileExecute(new[] { photo }, caption);
                        }
                    }
                }
                else if (package.AvailableFormats.Contains(StandardDataFormats.WebLink))
                {
                    var field = TextField;
                    if (field == null)
                    {
                        return;
                    }

                    var link = await package.GetWebLinkAsync();
                    field.Document.GetRange(field.Document.Selection.EndPosition, field.Document.Selection.EndPosition).SetText(TextSetOptions.None, link.AbsoluteUri);
                }
                else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await package.GetStorageItemsAsync();
                    var files = new List<StorageFile>(items.Count);

                    foreach (var file in items.OfType<StorageFile>())
                    {
                        files.Add(file);
                    }

                    SendFileExecute(files);
                }
                //else if (e.DataView.Contains(StandardDataFormats.WebLink))
                //{
                //    // TODO: Invoke getting a preview of the weblink above the Textbox
                //    var link = await e.DataView.GetWebLinkAsync();
                //    if (TextField.Text == "")
                //    {
                //        TextField.Text = link.AbsolutePath;
                //    }
                //    else
                //    {
                //        TextField.Text = (TextField.Text + " " + link.AbsolutePath);
                //    }
                //
                //    gridLoading.Visibility = Visibility.Collapsed;
                //
                //}
                else if (package.AvailableFormats.Contains(StandardDataFormats.Text))
                {
                    var field = TextField;
                    if (field == null)
                    {
                        return;
                    }

                    var text = await package.GetTextAsync();

                    if (package.Contains(StandardDataFormats.WebLink))
                    {
                        var link = await package.GetWebLinkAsync();
                        text += Environment.NewLine + link.AbsoluteUri;
                    }

                    field.Document.GetRange(field.Document.Selection.EndPosition, field.Document.Selection.EndPosition).SetText(TextSetOptions.None, text);
                }
            }
            catch { }
        }



        public async void EditDocument()
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                var file = await picker.PickSingleFileAsync();
                if (file == null)
                {
                    return;
                }

                var media = await StorageMedia.CreateAsync(file, false);
                if (media == null)
                {
                    return;
                }

                var factory = await MessageFactory.CreateDocumentAsync(media, false, false);
                if (factory != null)
                {
                    header.EditingMessageMedia = factory;
                }
            }
            catch { }
        }

        public async void EditMedia()
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.MediaTypes);

                var file = await picker.PickSingleFileAsync();
                if (file == null)
                {
                    return;
                }

                await EditMediaAsync(file);
            }
            catch { }
        }

        public async void EditCurrent()
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            var file = header.EditingMessage.GetFile();
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var cached = await ClientService.GetFileAsync(file);
            if (cached == null)
            {
                return;
            }

            await EditMediaAsync(cached);
        }

        public async Task EditMediaAsync(StorageFile file)
        {
            var storage = await StorageMedia.CreateAsync(file);
            if (storage != null)
            {
                await EditMediaAsync(storage);
            }
        }

        public async Task EditMediaAsync(StorageMedia storage)
        {
            var header = _composerHeader;
            if (header?.EditingMessage == null)
            {
                return;
            }

            var formattedText = GetFormattedText(true);

            var mediaAllowed = header.EditingMessage.Content is not MessageDocument;

            var items = new[] { storage };
            var popup = new SendFilesPopup(this, items, mediaAllowed, mediaAllowed, true, false, false, false, true);
            popup.ShowCaptionAboveMedia = header.EditingMessage.ShowCaptionAboveMedia();
            popup.Caption = formattedText
                .Substring(0, ClientService.Options.MessageCaptionLengthMax);

            var confirm = await popup.OpenAsync(XamlRoot);

            TextField?.Focus(FocusState.Programmatic);

            if (confirm != ContentDialogResult.Primary)
            {
                TextField?.SetText(formattedText);
                return;
            }

            storage = popup.Items[0];

            var captionAboveMedia = popup.ShowCaptionAboveMedia;
            var hasSpoiler = popup.SendWithSpoiler && !popup.IsFilesSelected;

            Task<InputMessageFactory> request = null;
            if (popup.IsFilesSelected)
            {
                request = MessageFactory.CreateDocumentAsync(storage, false, storage.IsScreenshot);
            }
            else if (storage is StoragePhoto photo)
            {
                request = MessageFactory.CreatePhotoAsync(photo, captionAboveMedia, hasSpoiler, storage.Ttl, storage.IsEdited ? storage.EditState : null);
            }
            else if (storage is StorageVideo video)
            {
                request = MessageFactory.CreateVideoAsync(video, video.IsMuted, captionAboveMedia, hasSpoiler, storage.Ttl, video.GetTransform());
            }

            if (request == null)
            {
                return;
            }

            var factory = await request;
            if (factory != null)
            {
                header.EditingMessageMedia = factory;
                await BeforeSendMessageAsync(popup.Caption);
            }
        }
    }
}
