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
using Telegram.Controls;
using Telegram.Controls.Media;
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

        protected override bool CanSchedule => Type is DialogType.History or DialogType.Thread;

        private async Task<ContentDialogResult> ShowPaidMessageConfirmationAsync(int messageCount, long starCount)
        {
            Settings.Chats.TryGet(Chat.Id, null, Services.ChatSetting.PaidMessageStarCount, out long savedMessageStarCount);

            if (starCount != 0 && starCount != savedMessageStarCount)
            {
                var message1 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessage1, starCount, Chat.Title);

                string message2;
                if (messageCount > 1)
                {
                    var message3 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessage2Many1, starCount * messageCount);
                    var message4 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessage2Many2, messageCount);

                    message2 = string.Format("{0} {1}", message3, message4);
                }
                else
                {
                    message2 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessage2One, starCount);
                }

                var popup = new MessagePopup
                {
                    Title = Strings.MessageLockedStarsConfirmTitle,
                    Message = string.Format("{0} {1}", message1, message2),
                    CheckBoxLabel = Strings.MessageLockedStarsConfirmMessageDontAsk,
                    PrimaryButtonText = Icons.Premium16 + Icons.Spacing + (starCount * messageCount).ToString("N0"), //Locale.Declension(Strings.R.MessageLockedStarsConfirmMessagePay, messageCount),
                    SecondaryButtonText = Strings.Cancel
                };

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary && popup.IsChecked is true)
                {
                    Settings.Chats[Chat.Id, null, Services.ChatSetting.PaidMessageStarCount] = starCount;
                }

                return confirm;
            }

            return ContentDialogResult.Primary;
        }

        public override async Task<MessageSendOptions> PickMessageSendOptionsAsync(int messageCount = 1, SchedulingState schedule = SchedulingState.Auto, bool? disableNotification = null, bool reorder = false)
        {
            var chat = _chat;
            if (chat == null || ComposerHeader?.Editing != null)
            {
                return new MessageSendOptions(ComposerHeader?.SuggestedPostInfo, false, false, 0, false, null, 0, 0, false);
            }

            var paidMessageStarCount = 0L;

            if (ClientService.TryGetUserFull(Chat, out UserFullInfo userFullInfo))
            {
                paidMessageStarCount = userFullInfo.OutgoingPaidMessageStarCount;
            }
            else if (ClientService.TryGetSupergroup(Chat, out Supergroup supergroup))
            {
                if (supergroup.IsAdministeredDirectMessagesGroup)
                {
                    paidMessageStarCount = 0;
                }
                else
                {
                    paidMessageStarCount = supergroup.PaidMessageStarCount;
                }
            }

            var paid = await ShowPaidMessageConfirmationAsync(messageCount, paidMessageStarCount);
            if (paid != ContentDialogResult.Primary)
            {
                return null;
            }

            MessageSchedulingState schedulingState = null;
            if (schedule == SchedulingState.Schedule || (Type == DialogType.ScheduledMessages && schedule == SchedulingState.Auto))
            {
                var user = ClientService.GetUser(chat);
                var popup = new ScheduleMessagePopup(ClientService, NavigationService, user, ClientService.IsSavedMessages(chat));

                var confirm = await ShowPopupAsync(popup);

                if (popup.SchedulingState != null)
                {
                    schedulingState = popup.SchedulingState;
                }
                else
                {
                    return null;
                }
            }
            else if (schedule == SchedulingState.WhenOnline)
            {
                schedulingState = new MessageSchedulingStateSendWhenOnline();
            }

            return new MessageSendOptions(ComposerHeader?.SuggestedPostInfo, disableNotification ?? false, false, messageCount * paidMessageStarCount, Settings.Stickers.DynamicPackOrder && reorder, schedulingState, 0, 0, false);
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
                        if (header?.Editing != null)
                        {
                            await EditMediaAsync(photo, true);
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
                                caption = resultCaption.AsFormattedText()
                                    .Substring(0, ClientService.Options.MessageCaptionLengthMax);
                            }

                            SendFileExecute(new[] { photo }, caption);
                        }
                    }
                }
                else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await package.GetStorageItemsAsync();
                    var files = new List<StorageFile>(items.Count);

                    foreach (var file in items.OfType<StorageFile>())
                    {
                        files.Add(file);
                    }

                    var header = _composerHeader;
                    if (header?.Editing != null && files.Count > 0)
                    {
                        await EditMediaAsync(files[0], header.Editing.Message?.Content is not MessageDocument and not MessageAudio);
                    }
                    else
                    {
                        SendFileExecute(files);
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
            if (header?.Editing == null)
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

                await EditMediaAsync(file, false);
            }
            catch { }
        }

        public async void EditMedia()
        {
            var header = _composerHeader;
            if (header?.Editing == null)
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

                await EditMediaAsync(file, true);
            }
            catch { }
        }

        public async void EditCurrent()
        {
            var header = _composerHeader;
            if (header?.Editing == null)
            {
                return;
            }

            var file = header.Editing.Message.GetFile();
            if (file == null || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            var cached = await ClientService.GetFileAsync(file);
            if (cached == null)
            {
                return;
            }

            await EditMediaAsync(cached, true);
        }

        public async Task EditMediaAsync(StorageFile file, bool mediaSelected)
        {
            var storage = await StorageMedia.CreateAsync(file);
            if (storage != null)
            {
                await EditMediaAsync(storage, mediaSelected);
            }
        }

        public async Task EditMediaAsync(StorageMedia storage, bool mediaSelected)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var header = _composerHeader;
            if (header?.Editing == null)
            {
                return;
            }

            var linkPreview = GetLinkPreviewOptions();
            var formattedText = GetFormattedText(true, false);

            var permissions = ClientService.GetPermissions(chat, out _);

            var items = new[] { storage };
            var popup = new SendFilesPopup(this, items, mediaSelected, permissions, false, false, false, true);
            popup.ShowCaptionAboveMedia = header.Editing.Message.ShowCaptionAboveMedia();
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
            var highQuality = popup.SendHighQuality && !popup.IsFilesSelected;

            Task<Object> request = null;
            if (storage is StoragePhoto photo && !popup.IsFilesSelected)
            {
                request = MessageFactory.CreatePhotoAsync(photo, popup.Caption, highQuality, captionAboveMedia, hasSpoiler, storage.Ttl, 0);
            }
            else if (storage is StorageVideo video && !popup.IsFilesSelected)
            {
                request = MessageFactory.CreateVideoAsync(video, popup.Caption, video.IsMuted, captionAboveMedia, hasSpoiler, storage.Ttl, 0);
            }
            else
            {
                request = MessageFactory.CreateDocumentAsync(storage, popup.Caption, false);
            }

            if (request == null)
            {
                return;
            }

            var factory = await request;
            if (factory is InputMessageContent input)
            {
                if (header.Editing != null)
                {
                    header.Editing = new MessageComposerEditing(header.Editing.Message, input);
                }

                await BeforeSendMessageAsync(popup.Caption, linkPreview);
            }
        }
    }
}
