//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Gallery;
using Windows.Storage;
using Windows.UI.Xaml;

namespace Telegram.ViewModels
{
    public enum ChatMemberRank
    {
        Owner,
        Admin,
        Other
    };

    public record ChatMemberTag(ChatMemberRank Rank, string Tag);

    public partial class MessageDelegate : ViewModelBase, IMessageDelegate
    {
        private readonly ViewModelBase _viewModel;

        protected static readonly ConcurrentDictionary<long, Dictionary<long, ChatMemberTag>> _admins = new();

        public MessageDelegate(ViewModelBase viewModel)
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

        public bool IsForum => _viewModel is DialogViewModel { IsForum: true };

        public bool IsDirectMessagesGroup => _viewModel is DialogViewModel { IsDirectMessagesGroup: true };

        public bool IsSavedMessagesTab => _viewModel is DialogViewModel { IsSavedMessagesTab: true };

        public override INavigationService NavigationService
        {
            get => _viewModel?.NavigationService;
            set => _viewModel.NavigationService = value;
        }

        public override IDispatcherContext Dispatcher
        {
            get => _viewModel?.Dispatcher;
            set => _viewModel.Dispatcher = value;
        }

        public virtual ReactionType SavedMessagesTag { get; set; }



        private static readonly string _executableExtensions = "ad ade adp ahk app application appref-ms asp aspx asx bas bat bin cab cdxml cer cfg cgi chi chm cmd cnt com conf cpl crt csh der diagcab dll drv eml exe fon fxp gadget grp hlp hpj hta htt inf ini ins inx isp isu its jar jnlp job js jse jsp key ksh lexe library-ms lnk local lua mad maf mag mam manifest maq mar mas mat mau mav maw mcf mda mdb mde mdt mdw mdz mht mhtml mjs mmc mof msc msg msh msh1 msh2 msh1xml msh2xml mshxml msi msp mst ops osd paf pcd phar php php3 php4 php5 php7 phps php-s pht phtml pif pl plg pm pod prf prg ps1 ps2 ps1xml ps2xml psc1 psc2 psd1 psm1 pssc pst py py3 pyc pyd pyi pyo pyw pyzw pyz rb reg rgs scf scr sct search-ms settingcontent-ms sh shb shs slk sys swf t tmp u3p url vb vbe vbp vbs vbscript vdx vsmacros vsd vsdm vsdx vss vssm vssx vst vstm vstx vsw vsx vtx website wlua ws wsc wsf wsh xbap xll xlsb xlsm xnk xs";

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
                var extension = System.IO.Path.GetExtension(document.FileName);
                if (_executableExtensions.Contains(extension.TrimStart('.')))
                {
                    return false;
                }

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
            if (file.Local.Path.EndsWith(".unigram-theme", StringComparison.OrdinalIgnoreCase))
            {

            }
            else if (file.Local.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                var instantView = await ParseMarkdown(file);
                if (instantView != null)
                {
                    _viewModel.NavigationService.NavigateToInstant(instantView, "tg://" + System.IO.Path.GetFileName(file.Local.Path));
                    return;
                }
            }

            var service = ClientService.Session.Resolve<IStorageService>();
            if (service != null)
            {
                _ = service.OpenFileAsync(file);
            }
        }

        private async Task<WebPageInstantView> ParseMarkdown(File file)
        {
            try
            {
                var storage = await ClientService.GetFileAsync(file);
                var text = await FileIO.ReadTextAsync(storage);

                var blocks = MarkdownToInstantView.Parse(text);
                if (blocks.Count > 0)
                {
                    return new WebPageInstantView(blocks, 0, 2, false, true, null);
                }
            }
            catch
            {

            }

            return null;
        }

        public void OpenUsername(string username)
        {
            MessageHelper.NavigateToUsername(ClientService, NavigationService, username);
        }

        public void OpenUser(long userId)
        {
            NavigationService.NavigateToUser(userId);
        }

        public void OpenUrl(string url, bool untrust, OpenUrlSource source = null)
        {
            source ??= (Chat == null ? null : new OpenUrlSourceChat(Chat.Id, null));
            MessageHelper.OpenUrl(ClientService, NavigationService, url, untrust, source);
        }

        public string GetMemberTag(MessageViewModel message, out ChatMemberRank rank)
        {
            if (message.IsChannelPost)
            {
                rank = ChatMemberRank.Other;
                return string.Empty;
            }

            if (message.SenderId is MessageSenderUser senderUser)
            {
                return GetAdminTitle(message, senderUser.UserId, out rank);
            }
            else if (message.SenderId is MessageSenderChat senderChat && !message.IsChannelPost)
            {
                rank = ChatMemberRank.Other;
                return message.AuthorSignature.Length > 0 ? message.AuthorSignature : senderChat.ChatId == Chat.Id ? Strings.ChannelAdmin : string.Empty;
            }

            rank = ChatMemberRank.Other;
            return string.Empty;
        }

        public string GetAdminTitle(MessageViewModel message, long userId, out ChatMemberRank rank)
        {
            if (_admins.TryGetValue(message.ChatId, out Dictionary<long, ChatMemberTag> value))
            {
                if (value.TryGetValue(userId, out ChatMemberTag tag))
                {
                    rank = tag.Rank;

                    if (string.IsNullOrEmpty(tag.Tag))
                    {
                        if (tag.Rank == ChatMemberRank.Owner)
                        {
                            return Strings.ChatTagOwner;
                        }

                        return Strings.ChatTagAdmin;
                    }

                    return tag.Tag;
                }
            }

            rank = ChatMemberRank.Other;
            return message.SenderTag;
        }

        public bool IsAdministrator(MessageSender memberId)
        {
            var chat = Chat;
            if (chat == null || memberId is not MessageSenderUser user)
            {
                return false;
            }

            if (_admins.TryGetValue(chat.Id, out Dictionary<long, ChatMemberTag> value))
            {
                if (value.TryGetValue(user.UserId, out var tag))
                {
                    return tag.Rank != ChatMemberRank.Owner;
                }
            }

            return false;
        }

        public void UpdateAdministrators(Chat chat)
        {
            if (ClientService.TryGetBasicGroupFull(chat, out BasicGroupFullInfo fullInfo))
            {
                var members = new Dictionary<long, ChatMemberTag>();

                foreach (var member in fullInfo.Members)
                {
                    if (member.MemberId is not MessageSenderUser senderUser)
                    {
                        continue;
                    }

                    var type = member.Status switch
                    {
                        ChatMemberStatusCreator => ChatMemberRank.Owner,
                        ChatMemberStatusAdministrator => ChatMemberRank.Admin,
                        _ => ChatMemberRank.Other
                    };

                    members[senderUser.UserId] = new ChatMemberTag(type, member.Tag);
                }

                _admins[chat.Id] = members;
            }
            else if (chat.Type is ChatTypeSupergroup { IsChannel: false })
            {
                ClientService.Send(new GetChatAdministrators(chat.Id), result =>
                {
                    if (result is ChatAdministrators users)
                    {
                        _admins[chat.Id] = users.Administrators.ToDictionary(x => x.UserId, y => new ChatMemberTag(y.IsOwner ? ChatMemberRank.Owner : ChatMemberRank.Admin, y.CustomTitle));
                    }
                });
            }
        }


        #region Facades

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void HideSponsoredMessage(MessageViewModel message) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void ForwardMessage(MessageViewModel message) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void SummarizeMessage(MessageViewModel message) { }

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

        public virtual void OpenSender(MessageViewModel message)
        {
            if (message.IsSaved || message.IsVerificationCode)
            {
                if (message.ForwardInfo?.Origin is MessageOriginUser fromUser)
                {
                    OpenUser(fromUser.SenderUserId);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChat fromChat)
                {
                    OpenChat(fromChat.SenderChatId, true);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel)
                {
                    // TODO: verify if this is sufficient
                    OpenChat(fromChannel.ChatId);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser)
                {
                    ToastPopup.Show(XamlRoot, Strings.HidAccount);
                }
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                if (senderChat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                {
                    OpenChat(senderChat.Id);
                }
                else
                {
                    OpenChat(senderChat.Id, true);
                }
            }
            else if (message.SenderId is MessageSenderUser senderUser)
            {
                OpenUser(senderUser.UserId);
            }
        }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenWebPage(MessageViewModel message) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenSticker(Sticker sticker) { }

        public async void OpenLocation(Location location, string title)
        {
            try
            {
                if (title != null)
                {
                    // TODO: is title supported?
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format(CultureInfo.InvariantCulture, "https://www.google.com/maps/search/?api=1&query={0},{1}", location.Latitude, location.Longitude)));
                }
                else
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(string.Format(CultureInfo.InvariantCulture, "https://www.google.com/maps/search/?api=1&query={0},{1}", location.Latitude, location.Longitude)));
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenGame(MessageViewModel message) { }

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
        public virtual void VotePoll(MessageViewModel message, IList<int> options) { }

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
        public virtual void OpenMedia(MessageViewModel message, FrameworkElement target, double timestamp = 0) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenMedia(GalleryMedia media, FrameworkElement target) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenPoll(MessageViewModel message) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void OpenPaidMedia(MessageViewModel message, PaidMedia media, FrameworkElement target, double timestamp = 0) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void PlayMessage(MessageViewModel message)
        {
            LifetimeService.Current.Playback.Play(XamlRoot, message);
        }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual bool RecognizeSpeech(MessageViewModel message) { return false; }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void SendBotCommand(string command) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void SendMessage(InputMessageContent content) { }



        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual bool IsTranslating { get; }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual bool IsSelectionEnabled { get; }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual IDictionary<long, MessageViewModel> SelectedItems { get; }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void Select(MessageViewModel message) { }

        /// <summary>
        /// Only available when created through DialogViewModel
        /// </summary>
        public virtual void Unselect(MessageViewModel message, bool updateSelection) { }

        #endregion
    }

    public partial class ChatMessageDelegate : MessageDelegate
    {
        private readonly Chat _chat;

        public ChatMessageDelegate(IClientService clientService, ISettingsService settings, Chat chat)
            : base(clientService, settings)
        {
            _chat = chat;
        }

        public ChatMessageDelegate(ViewModelBase viewModel, Chat chat)
            : base(viewModel)
        {
            _chat = chat;
        }

        public override Chat Chat => _chat;
    }

    public partial class DialogMessageDelegate : MessageDelegate
    {
        private readonly DialogViewModel _viewModel;

        public DialogMessageDelegate(DialogViewModel viewModel)
            : base(viewModel)
        {
            _viewModel = viewModel;
        }

        public DialogViewModel ViewModel => _viewModel;

        public override Chat Chat => _viewModel.Chat;

        #region Facades

        public override ReactionType SavedMessagesTag
        {
            get => _viewModel.Search?.SavedMessagesTag;
            set
            {
                if (_viewModel.Search == null)
                {
                    _viewModel.SearchExecute(string.Empty);
                }

                _viewModel.Search.SavedMessagesTag = value;
            }
        }

        public override void HideSponsoredMessage(MessageViewModel message) => _viewModel.HideSponsoredMessage(message);

        public override void ForwardMessage(MessageViewModel message) => _viewModel.ForwardMessage(message);

        public override void SummarizeMessage(MessageViewModel message) => _viewModel.SummarizeMessage(message);

        public override void ViewVisibleMessages() => _viewModel.ViewVisibleMessages();

        public override void OpenReply(MessageViewModel message) => _viewModel.OpenReply(message);

        public override void OpenThread(MessageViewModel message) => _viewModel.OpenThread(message);

        public override void OpenWebPage(MessageViewModel message) => _viewModel.OpenWebPage(message);

        public override void OpenSticker(Sticker sticker) => _viewModel.OpenSticker(sticker);

        public override void OpenGame(MessageViewModel message) => _viewModel.OpenGame(message);

        public override void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button) => _viewModel.OpenInlineButton(message, button);

        public override void Call(MessageViewModel message, bool video) => _viewModel.Call(message, video);

        public override void VotePoll(MessageViewModel message, IList<int> options) => _viewModel.VotePoll(message, options);

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

        public override void OpenMedia(MessageViewModel message, FrameworkElement target, double timestamp = 0) => _viewModel.OpenMedia(message, target, timestamp);

        public override void OpenMedia(GalleryMedia media, FrameworkElement target) => _viewModel.OpenMedia(media, target);

        public override void OpenPoll(MessageViewModel message) => _viewModel.OpenPoll(message);

        public override void OpenPaidMedia(MessageViewModel message, PaidMedia media, FrameworkElement target, double timestamp = 0)
        {
            _viewModel.OpenPaidMedia(message, media, target, timestamp);
        }

        public override void PlayMessage(MessageViewModel message) => _viewModel.PlayMessage(message);

        public override bool RecognizeSpeech(MessageViewModel message) => _viewModel.RecognizeSpeech(message);



        public override void SendBotCommand(string command) => _viewModel.SendBotCommand(command);

        public override void SendMessage(InputMessageContent content) => _viewModel.SendContent(content);


        public override bool IsTranslating => _viewModel.IsTranslating;

        public override bool IsSelectionEnabled => _viewModel.IsSelectionEnabled;

        public override IDictionary<long, MessageViewModel> SelectedItems => _viewModel.SelectedItems;

        public override void Select(MessageViewModel message) => _viewModel.Select(message);

        public override void Unselect(MessageViewModel message, bool updateSelection) => _viewModel.Unselect(message, updateSelection);

        #endregion
    }

    public partial class InstantMessageDelegate : MessageDelegate
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

        public override void OpenMedia(MessageViewModel message, FrameworkElement target, double timestamp = 0)
        {
            var content = target.Tag as GalleryMedia;
            content ??= _viewModel.Gallery.Items.FirstOrDefault();

            _viewModel.Gallery.SelectedItem = content;
            _viewModel.Gallery.FirstItem = content;

            _viewModel.NavigationService.ShowGallery(_viewModel.Gallery, target);
        }
    }

    public partial class RichMessageDelegate : DialogMessageDelegate
    {
        private readonly RichMessage _message;
        private readonly DialogMessageDelegate _delegate;

        public RichMessageDelegate(RichMessage message, DialogMessageDelegate delegato)
            : base(delegato.ViewModel)
        {
            _message = message;
        }

        public override void OpenMedia(MessageViewModel message, FrameworkElement target, double timestamp = 0)
        {
            PageBlockMediaKind kind;
            if (message.Content is MessagePhoto or MessageVideo)
            {
                kind = PageBlockMediaKind.Media;
            }
            else if (message.Content is MessageAnimation)
            {
                kind = PageBlockMediaKind.Animation;
            }
            else
            {
                // ?
                return;
            }

            var gallery = new InstantGalleryViewModel(ClientService, ClientService.Session.Resolve<IStorageService>(), ClientService.Session.Resolve<IEventAggregator>());
            var media = PageBlockHelper.FindAllMedia(_message.Blocks, kind);

            var source = FindBlock(target);

            foreach (var block in media)
            {
                GalleryMedia item;
                if (block is PageBlockPhoto photo)
                {
                    item = new GalleryPhoto(ClientService, photo.Photo, photo.Caption?.ToFormattedText());
                }
                else if (block is PageBlockVideo video)
                {
                    item = new GalleryVideo(ClientService, video.Video, video.Caption?.ToFormattedText());
                }
                else if (block is PageBlockAnimation animation)
                {
                    item = new GalleryAnimation(ClientService, animation.Animation, animation.Caption?.ToFormattedText());
                }
                else
                {
                    // ?
                    continue;
                }

                gallery.Items.Add(item);

                if (source == block)
                {
                    gallery.SelectedItem = item;
                    gallery.FirstItem = item;
                }
            }

            ViewModel.NavigationService.ShowGallery(gallery, target);

            static PageBlock FindBlock(FrameworkElement element)
            {
                if (element.Tag is PageBlock block)
                {
                    return block;
                }
                else if (element.Parent is FrameworkElement parent)
                {
                    return FindBlock(parent);
                }

                return null;
            }
        }
    }
}
