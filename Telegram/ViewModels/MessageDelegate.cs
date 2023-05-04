//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Telegram.Common;
using Telegram.Controls.Gallery;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Gallery;
using Telegram.Views;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public class MessageDelegate : TLViewModelBase, IMessageDelegate
    {
        private readonly TLViewModelBase _viewModel;

        protected static readonly Dictionary<long, IList<ChatAdministrator>> _admins = new();

        public MessageDelegate(TLViewModelBase viewModel)
            : base(viewModel.ClientService, viewModel.Settings, viewModel.Aggregator)
        {
            _viewModel = viewModel;
        }

        public MessageDelegate(IClientService clientService, ISettingsService settings)
            : base(clientService, settings, null)
        {
            _viewModel = null;
        }

        public virtual Chat Chat { get; }

        public bool IsDialog => _viewModel is DialogViewModel;

        public override INavigationService NavigationService
        {
            get => _viewModel.NavigationService;
            set => _viewModel.NavigationService = value;
        }

        public override IDispatcherContext Dispatcher
        {
            get => _viewModel.Dispatcher;
            set => _viewModel.Dispatcher = value;
        }



        public virtual bool CanBeDownloaded(object content, File file)
        {
            var chat = Chat;
            if (chat == null || ClientService.IsDownloadFileCanceled(file.Id))
            {
                return false;
            }

            if (content is Animation animation)
            {
                return Settings.AutoDownload.ShouldDownloadVideo(GetChatType(chat), animation.AnimationValue.Size);
            }
            else if (content is Audio audio)
            {
                return Settings.AutoDownload.ShouldDownloadDocument(GetChatType(chat), audio.AudioValue.Size);
            }
            else if (content is Document document)
            {
                return Settings.AutoDownload.ShouldDownloadDocument(GetChatType(chat), document.DocumentValue.Size);
            }
            else if (content is Photo photo)
            {
                var big = photo.GetBig();
                if (big != null && ClientService.IsDownloadFileCanceled(big.Photo.Id))
                {
                    return false;
                }

                return Settings.AutoDownload.ShouldDownloadPhoto(GetChatType(chat));
            }
            else if (content is Sticker)
            {
                // Stickers aren't part of the deal
                return true;
            }
            else if (content is Video video)
            {
                return Settings.AutoDownload.ShouldDownloadVideo(GetChatType(chat), video.VideoValue.Size);
            }
            else if (content is VideoNote videoNote)
            {
                return Settings.AutoDownload.ShouldDownloadDocument(GetChatType(chat), videoNote.Video.Size);
            }
            else if (content is VoiceNote)
            {
                return !Settings.AutoDownload.Disabled;
            }

            return false;
        }

        private AutoDownloadChat GetChatType(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel || chat.Type is ChatTypeBasicGroup)
            {
                return AutoDownloadChat.Group;
            }
            else if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = ClientService.GetUser(chat);
                if (user == null)
                {
                    return AutoDownloadChat.OtherPrivateChat;
                }
                else if (user.IsContact)
                {
                    return AutoDownloadChat.Contact;
                }
            }

            return AutoDownloadChat.Channel;
        }

        public void DownloadFile(MessageViewModel message, File file)
        {
            ClientService.DownloadFile(file.Id, 32);
        }

        public async void OpenFile(File file)
        {
            var permanent = await ClientService.GetPermanentFileAsync(file);
            if (permanent != null)
            {
                if (file.Local.Path.EndsWith(".unigram-theme"))
                {
                    await ShowPopupAsync(new ThemePreviewPopup(permanent));
                }
                else
                {
                    await Windows.System.Launcher.LaunchFileAsync(permanent);
                }
            }
        }

        public async void OpenUsername(string username)
        {
            var response = await ClientService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat)
            {
                if (chat.Type is ChatTypePrivate privata)
                {
                    var user = ClientService.GetUser(privata.UserId);
                    if (user?.Type is UserTypeBot)
                    {
                        NavigationService.NavigateToChat(chat);
                    }
                    else
                    {
                        NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                    }
                }
                else
                {
                    NavigationService.NavigateToChat(chat);
                }
            }
            else
            {
                await ShowPopupAsync(Strings.NoUsernameFound, Strings.AppName, Strings.OK);
            }
        }

        public async void OpenUser(long userId)
        {
            var response = await ClientService.SendAsync(new CreatePrivateChat(userId, false));
            if (response is Chat chat)
            {
                var user = ClientService.GetUser(userId);
                if (user?.Type is UserTypeBot)
                {
                    NavigationService.NavigateToChat(chat);
                }
                else
                {
                    NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                }
            }
        }

        public async void OpenUrl(string url, bool untrust)
        {
            if (MessageHelper.TryCreateUri(url, out Uri uri))
            {
                if (MessageHelper.IsTelegramUrl(uri))
                {
                    MessageHelper.OpenTelegramUrl(ClientService, NavigationService, uri);
                }
                else
                {
                    //if (message?.Media is TLMessageMediaWebPage webpageMedia)
                    //{
                    //    if (webpageMedia.WebPage is TLWebPage webpage && webpage.HasCachedPage && webpage.Url.Equals(navigation))
                    //    {
                    //        var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                    //        if (service != null)
                    //        {
                    //            service.Navigate(typeof(InstantPage), webpageMedia);
                    //            return;
                    //        }
                    //    }
                    //}

                    if (untrust)
                    {
                        var confirm = await ShowPopupAsync(string.Format(Strings.OpenUrlAlert, url), Strings.AppName, Strings.OK, Strings.Cancel);
                        if (confirm != ContentDialogResult.Primary)
                        {
                            return;
                        }
                    }

                    try
                    {
                        await Windows.System.Launcher.LaunchUriAsync(uri);
                    }
                    catch { }
                }
            }
        }

        public string GetAdminTitle(MessageViewModel message)
        {
            if (message.SenderId is MessageSenderUser senderUser)
            {
                return GetAdminTitle(senderUser.UserId);
            }
            else if (message.SenderId is MessageSenderChat && !message.IsChannelPost)
            {
                return message.AuthorSignature.Length > 0 ? message.AuthorSignature : string.Empty;
            }

            return string.Empty;
        }

        public string GetAdminTitle(long userId)
        {
            var chat = Chat;
            if (chat == null)
            {
                return string.Empty;
            }

            if (_admins.TryGetValue(chat.Id, out IList<ChatAdministrator> value))
            {
                var admin = value.FirstOrDefault(x => x.UserId == userId);
                if (admin != null)
                {
                    if (string.IsNullOrEmpty(admin.CustomTitle))
                    {
                        if (admin.IsOwner)
                        {
                            return Strings.ChannelCreator;
                        }

                        return Strings.ChannelAdmin;
                    }

                    return admin.CustomTitle;
                }
            }

            return string.Empty;
        }

        public void UpdateAdministrators(long chatId)
        {
            ClientService.Send(new GetChatAdministrators(chatId), result =>
            {
                if (result is ChatAdministrators users)
                {
                    _admins[chatId] = users.Administrators;
                }
            });
        }


        #region Facades

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void ForwardMessage(MessageViewModel message) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void ViewVisibleMessages() { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenReply(MessageViewModel message) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenThread(MessageViewModel message) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenWebPage(WebPage webPage) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenSticker(Sticker sticker) { }

        public async void OpenLocation(Location location, string title)
        {
            var options = new Windows.System.LauncherOptions();
            options.FallbackUri = new Uri(string.Format(CultureInfo.InvariantCulture, "https://www.google.com/maps/search/?api=1&query={0},{1}", location.Latitude, location.Longitude));

            if (title != null)
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format(CultureInfo.InvariantCulture, "bingmaps:?collection=point.{0}_{1}_{2}", location.Latitude, location.Longitude, WebUtility.UrlEncode(title))), options);
            }
            else
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format(CultureInfo.InvariantCulture, "bingmaps:?collection=point.{0}_{1}", location.Latitude, location.Longitude)), options);
            }
        }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void Call(MessageViewModel message, bool video) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void VotePoll(MessageViewModel message, IList<PollOption> options) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenViaBot(long viaBotUserId) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenChat(long chatId, bool profile = false) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenChat(long chatId, long messageId) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenHashtag(string hashtag) { }

        public virtual void OpenBankCardNumber(string number)
        {
            //var response = await ClientService.SendAsync(new GetBankCardInfo(number));
            //if (response is BankCardInfo info)
            //{
            //    var url = info.Actions.FirstOrDefault(x => x.)
            //}
        }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenMedia(MessageViewModel message, FrameworkElement target, int timestamp = 0) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void PlayMessage(MessageViewModel message) { }



        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void SendBotCommand(string command) { }



        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual IDictionary<long, MessageViewModel> SelectedItems { get; }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual bool IsSelectionEnabled { get; }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void Select(MessageViewModel message) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void Unselect(MessageViewModel message) { }

        #endregion
    }

    public class ChatMessageDelegate : MessageDelegate
    {
        private readonly Chat _chat;

        public ChatMessageDelegate(IClientService clientService, ISettingsService settings, Chat chat)
            : base(clientService, settings)
        {
            _chat = chat;
        }

        public override Chat Chat => _chat;
    }

    public class DialogMessageDelegate : MessageDelegate
    {
        private readonly DialogViewModel _viewModel;

        public DialogMessageDelegate(DialogViewModel viewModel)
            : base(viewModel)
        {
            _viewModel = viewModel;
        }

        public override Chat Chat => _viewModel.Chat;

        #region Facades

        public override void ForwardMessage(MessageViewModel message) => _viewModel.ForwardMessage(message);

        public override void ViewVisibleMessages() => _viewModel.ViewVisibleMessages();

        public override void OpenReply(MessageViewModel message) => _viewModel.OpenReply(message);

        public override void OpenThread(MessageViewModel message) => _viewModel.OpenThread(message);

        public override void OpenWebPage(WebPage webPage) => _viewModel.OpenWebPage(webPage);

        public override void OpenSticker(Sticker sticker) => _viewModel.OpenSticker(sticker);

        public override void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button) => _viewModel.OpenInlineButton(message, button);

        public override void Call(MessageViewModel message, bool video) => _viewModel.Call(video);

        public override void VotePoll(MessageViewModel message, IList<PollOption> options) => _viewModel.VotePoll(message, options);

        public override void OpenViaBot(long viaBotUserId) => _viewModel.OpenViaBot(viaBotUserId);

        public override void OpenChat(long chatId, bool profile = false) => _viewModel.OpenChat(chatId, profile);

        public override void OpenChat(long chatId, long messageId) => _viewModel.OpenChat(chatId, messageId);

        public override void OpenHashtag(string hashtag) => _viewModel.OpenHashtag(hashtag);

        public override void OpenBankCardNumber(string number)
        {
            //var response = await ClientService.SendAsync(new GetBankCardInfo(number));
            //if (response is BankCardInfo info)
            //{
            //    var url = info.Actions.FirstOrDefault(x => x.)
            //}
        }

        public override void OpenMedia(MessageViewModel message, FrameworkElement target, int timestamp = 0) => _viewModel.OpenMedia(message, target, timestamp);

        public override void PlayMessage(MessageViewModel message) => _viewModel.PlayMessage(message);



        public override void SendBotCommand(string command) => _viewModel.SendBotCommand(command);



        public override IDictionary<long, MessageViewModel> SelectedItems => _viewModel.SelectedItems;

        public override bool IsSelectionEnabled => _viewModel.IsSelectionEnabled;

        public override void Select(MessageViewModel message) => _viewModel.Select(message);

        public override void Unselect(MessageViewModel message) => _viewModel.Unselect(message);

        #endregion
    }

    public class InstantMessageDelegate : MessageDelegate
    {
        private readonly InstantViewModel _viewModel;

        public InstantMessageDelegate(InstantViewModel viewModel)
            : base(viewModel)
        {
            _viewModel = viewModel;
        }

        public override Chat Chat => null;

        public override bool CanBeDownloaded(object content, File file)
        {
            return !Settings.AutoDownload.Disabled;
        }

        public override async void OpenMedia(MessageViewModel message, FrameworkElement target, int timestamp = 0)
        {
            var content = target.Tag as GalleryContent;
            content ??= _viewModel.Gallery.Items.FirstOrDefault();

            _viewModel.Gallery.SelectedItem = content;
            _viewModel.Gallery.FirstItem = content;

            await GalleryView.ShowAsync(_viewModel.Gallery, () => target);
        }
    }
}
