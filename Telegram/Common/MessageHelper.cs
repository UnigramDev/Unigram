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
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Controls.Stories;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Stories;
using Telegram.ViewModels.Supergroups;
using Telegram.Views;
using Telegram.Views.Business;
using Telegram.Views.Chats.Popups;
using Telegram.Views.Create;
using Telegram.Views.Folders;
using Telegram.Views.Folders.Popups;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Stars.Popups;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;

namespace Telegram.Common
{
    public partial class OpenUrlSource
    {

    }

    public partial class OpenUrlSourceChat : OpenUrlSource
    {
        public long ChatId { get; }

        public MessageSender SenderId { get; }

        public OpenUrlSourceChat(long chatId, MessageSender senderId)
        {
            ChatId = chatId;
            SenderId = senderId;
        }
    }

    public partial class TonSite
    {
        public static bool TryCreate(IClientService clientService, Uri uri, out string magic)
        {
            magic = null;

            if (string.Equals(uri.Scheme, "tonsite", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".ton", StringComparison.OrdinalIgnoreCase))
            {
                var domain = clientService.Config.GetNamedString("ton_proxy_address", "magic.org");

                magic = uri.Host
                    .Replace("-", "-h")
                    .Replace(".", "-d");
                magic = $"https://{magic}.{domain}" + uri.PathAndQuery + uri.Fragment;
                return true;
            }

            return false;
        }

        public static bool TryUnmask(IClientService clientService, string url, out Uri magic)
        {
            if (MessageHelper.TryCreateUri(url, out Uri navigation))
            {
                magic = Unmask(clientService, navigation);
                return true;
            }

            magic = null;
            return false;
        }

        public static Uri Unmask(IClientService clientService, Uri navigation)
        {
            var domain = clientService.Config.GetNamedString("ton_proxy_address", "magic.org");

            var host = navigation.Host;
            if (host.EndsWith("." + domain))
            {
                host = host.Replace("." + domain, string.Empty)
                    .Replace("-d", ".")
                    .Replace("-h", "-");
            }

            return new Uri("tonsite://" + host + navigation.PathAndQuery + navigation.Fragment);
        }
    }

    public partial class MessageHelper
    {
        public static async void CopyLink(IClientService clientService, XamlRoot xamlRoot, InternalLinkType type)
        {
            var response = await clientService.SendAsync(new GetInternalLink(type, true));
            if (response is HttpUrl httpUrl)
            {
                CopyLink(xamlRoot, httpUrl.Url);
            }
        }

        public static void CopyLink(XamlRoot xamlRoot, string link, bool publiz = true)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(link);
            ClipboardEx.TrySetContent(dataPackage);

            ToastPopup.Show(xamlRoot, publiz ? Strings.LinkCopied : Strings.LinkCopiedPrivate, ToastPopupIcon.LinkCopied);
        }

        public static void CopyText(XamlRoot xamlRoot, string text)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            ClipboardEx.TrySetContent(dataPackage);

            ToastPopup.Show(xamlRoot, Strings.TextCopied, ToastPopupIcon.Copied);
        }

        public static async void CopyText(XamlRoot xamlRoot, FormattedText text)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text.Text);

            var entities = text.Entities.Where(x => x.IsEditable()).ToList();
            if (entities.Count > 0)
            {
                using (var stream = new InMemoryRandomAccessStream())
                {
                    using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                    {
                        writer.WriteInt32(entities.Count);

                        foreach (var entity in entities)
                        {
                            writer.WriteInt32(entity.Offset);
                            writer.WriteInt32(entity.Length);

                            switch (entity.Type)
                            {
                                case TextEntityTypeBold:
                                    writer.WriteByte(1);
                                    break;
                                case TextEntityTypeItalic:
                                    writer.WriteByte(2);
                                    break;
                                case TextEntityTypeStrikethrough:
                                    writer.WriteByte(3);
                                    break;
                                case TextEntityTypeUnderline:
                                    writer.WriteByte(4);
                                    break;
                                case TextEntityTypeSpoiler:
                                    writer.WriteByte(5);
                                    break;
                                case TextEntityTypeBlockQuote:
                                case TextEntityTypeExpandableBlockQuote:
                                    writer.WriteByte(6);
                                    break;
                                case TextEntityTypeCustomEmoji customEmoji:
                                    writer.WriteByte(7);
                                    writer.WriteInt64(customEmoji.CustomEmojiId);
                                    break;
                                case TextEntityTypeCode:
                                    writer.WriteByte(8);
                                    break;
                                case TextEntityTypePre:
                                    writer.WriteByte(9);
                                    break;
                                case TextEntityTypePreCode preCode:
                                    writer.WriteByte(10);
                                    writer.WriteUInt32(writer.MeasureString(preCode.Language));
                                    writer.WriteString(preCode.Language);
                                    break;
                                case TextEntityTypeTextUrl textUrl:
                                    writer.WriteByte(11);
                                    writer.WriteUInt32(writer.MeasureString(textUrl.Url));
                                    writer.WriteString(textUrl.Url);
                                    break;
                                case TextEntityTypeMentionName mentionName:
                                    writer.WriteByte(12);
                                    writer.WriteInt64(mentionName.UserId);
                                    break;
                            }
                        }

                        await writer.FlushAsync();
                        await writer.StoreAsync();
                    }

                    stream.Seek(0);
                    dataPackage.SetData("application/x-tl-field-tags", stream.CloneStream());
                }
            }

            ClipboardEx.TrySetContent(dataPackage);

            if (xamlRoot != null)
            {
                ToastPopup.Show(xamlRoot, Strings.TextCopied, ToastPopupIcon.Copied);
            }
        }

        public static async void DragStarting(MessageViewModel message, DragStartingEventArgs args)
        {
            var file = message?.GetFile();
            if (file != null && file.Local.IsDownloadingCompleted && message.CanBeSaved)
            {
                var deferral = args.GetDeferral();

                try
                {
                    var item = await StorageFile.GetFileFromPathAsync(file.Local.Path);

                    args.Data.RequestedOperation = DataPackageOperation.Copy;
                    args.Data.SetStorageItems(new[] { item });

                    using (var stream = new InMemoryRandomAccessStream())
                    {
                        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                        {
                            writer.WriteInt64(message.ChatId);
                            writer.WriteInt64(message.Id);

                            await writer.FlushAsync();
                            await writer.StoreAsync();
                        }

                        stream.Seek(0);
                        args.Data.SetData("application/x-tl-message", stream.CloneStream());
                    }

                    args.DragUI.SetContentFromDataPackage();
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
                finally
                {
                    deferral.Complete();
                }
            }
            else
            {
                args.Cancel = true;
            }
        }

        public static async Task<FormattedText> PasteTextAsync(DataPackageView package)
        {
            if (package.AvailableFormats.Contains(StandardDataFormats.Text))
            {
                string text = await package.GetTextAsync();
                IList<TextEntity> entities = null;

                if (package.AvailableFormats.Contains("application/x-tl-field-tags"))
                {
                    var data = await package.GetDataAsync("application/x-tl-field-tags") as IRandomAccessStream;
                    var reader = new DataReader(data.GetInputStreamAt(0));

                    await reader.LoadAsync((uint)data.Size);

                    var count = reader.ReadInt32();
                    entities = new List<TextEntity>(count);

                    for (int i = 0; i < count; i++)
                    {
                        var entity = new TextEntity
                        {
                            Offset = reader.ReadInt32(),
                            Length = reader.ReadInt32()
                        };

                        var type = reader.ReadByte();

                        switch (type)
                        {
                            case 1:
                                entity.Type = new TextEntityTypeBold();
                                break;
                            case 2:
                                entity.Type = new TextEntityTypeItalic();
                                break;
                            case 3:
                                entity.Type = new TextEntityTypeStrikethrough();
                                break;
                            case 4:
                                entity.Type = new TextEntityTypeUnderline();
                                break;
                            case 5:
                                entity.Type = new TextEntityTypeSpoiler();
                                break;
                            case 6:
                                entity.Type = new TextEntityTypeBlockQuote();
                                break;
                            case 7:
                                entity.Type = new TextEntityTypeCustomEmoji(reader.ReadInt64());
                                break;
                            case 8:
                                entity.Type = new TextEntityTypeCode();
                                break;
                            case 9:
                                entity.Type = new TextEntityTypePre();
                                break;
                            case 10:
                                entity.Type = new TextEntityTypePreCode(reader.ReadString(reader.ReadUInt32()));
                                break;
                            case 11:
                                entity.Type = new TextEntityTypeTextUrl(reader.ReadString(reader.ReadUInt32()));
                                break;
                            case 12:
                                entity.Type = new TextEntityTypeMentionName(reader.ReadInt64());
                                break;
                        }

                        entities.Add(entity);
                    }
                }

                return new FormattedText(text, entities ?? Array.Empty<TextEntity>());
            }

            return null;
        }

        public static bool AreTheSame(string bae, string url, out string fragment)
        {
            if (TryCreateUri(bae, out Uri current) && TryCreateUri(url, out Uri result))
            {
                fragment = result.Fragment.Length > 0 ? result.Fragment?.Substring(1) : null;
                return fragment != null && Uri.Compare(current, result, UriComponents.Host | UriComponents.PathAndQuery, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
            }

            fragment = null;
            return false;
        }

        public static bool TryCreateUri(string url, out Uri uri)
        {
            if (url == null)
            {
                uri = null;
                return false;
            }

            if (!url.StartsWith("http://")
                && !url.StartsWith("https://")
                && !url.StartsWith("tg:")
                && !url.StartsWith("tonsite:")
                && !url.StartsWith("ftp:")
                && !url.StartsWith("mailto:"))
            {
                url = "https://" + url;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out uri);
        }

        public static bool IsTelegramUrl(Uri uri)
        {
            var host = uri.Host;

            var splitHostName = uri.Host.Split('.');
            if (splitHostName.Length >= 2)
            {
                host = splitHostName[^2] + "." +
                       splitHostName[^1];
            }

            if (Constants.TelegramHosts.Contains(host))
            {
                return string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
            }

            return IsTelegramScheme(uri);
        }

        public static bool IsTelegramScheme(Uri uri)
        {
            return string.Equals(uri.Scheme, "tg", StringComparison.OrdinalIgnoreCase);
        }

        public static async void OpenTelegramUrl(IClientService clientService, INavigationService navigation, Uri uri, OpenUrlSource source = null)
        {
            var url = uri.ToString();
            if (url.Contains("telegra.ph"))
            {
                navigation.NavigateToInstant(url);
                return;
            }

            var response = await clientService.SendAsync(new GetInternalLinkType(url));
            if (response is InternalLinkType internalLink)
            {
                OpenTelegramUrl(clientService, navigation, internalLink, source);
            }
            else
            {
                OpenLoginUrl(clientService, navigation, url, await clientService.SendAsync(new GetExternalLinkInfo(url)));
            }
        }

        private static async void OpenLoginUrl(IClientService clientService, INavigationService navigation, string url, Object info)
        {
            if (info is LoginUrlInfoOpen infoOpen)
            {
                OpenUrl(null, navigation, infoOpen.Url, !infoOpen.SkipConfirmation);
            }
            else if (info is LoginUrlInfoRequestConfirmation requestConfirmation)
            {
                var popup = new LoginUrlInfoPopup(clientService, navigation, requestConfirmation);

                var confirm = await navigation.ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                // TODO: emoji
                var response = await clientService.SendAsync(new GetExternalLink(url, string.Empty, popup.AllowWriteAccess, popup.AllowPhoneNumberAccess));
                if (response is HttpUrl httpUrl)
                {
                    OpenUrl(null, null, httpUrl.Url);
                }
                else if (response is Error)
                {
                    OpenUrl(null, null, url);
                }
            }
        }

        public static void OpenTelegramUrl(IClientService clientService, INavigationService navigation, InternalLinkType internalLink, OpenUrlSource source = null)
        {
            switch (internalLink)
            {
                case InternalLinkTypeAuthenticationCode authenticationCode:
                    if (clientService.AuthorizationState is AuthorizationStateWaitCode)
                    {
                        clientService.Send(new CheckAuthenticationCode(authenticationCode.Code));
                    }
                    break;
                case InternalLinkTypeAttachmentMenuBot attachmentMenuBot:
                    NavigateToAttachmentMenuBot(clientService, navigation, attachmentMenuBot, source);
                    break;
                case InternalLinkTypeBackground background:
                    NavigateToBackground(clientService, navigation, background.BackgroundName);
                    break;
                case InternalLinkTypeBotStart botStart:
                    NavigateToBotStart(clientService, navigation, botStart.BotUsername, botStart.StartParameter, botStart.Autostart, false);
                    break;
                case InternalLinkTypeBotStartInGroup botStartInGroup:
                    // Not yet supported: AdministratorRights
                    NavigateToBotStart(clientService, navigation, botStartInGroup.BotUsername, botStartInGroup.StartParameter, false, true);
                    break;
                case InternalLinkTypeBusinessChat businessChat:
                    NavigateToBusinessChat(clientService, navigation, businessChat.LinkName);
                    break;
                case InternalLinkTypeChatBoost chatBoost:
                    NavigateToChatBoost(clientService, navigation, chatBoost.Url);
                    break;
                case InternalLinkTypeChatInvite chatInvite:
                    NavigateToInviteLink(clientService, navigation, chatInvite.InviteLink);
                    break;
                case InternalLinkTypeChatFolderInvite chatFolderInvite:
                    NavigateToChatFolderInviteLink(clientService, navigation, chatFolderInvite.InviteLink);
                    break;
                case InternalLinkTypeGame game:
                    NavigateToUsername(clientService, navigation, game.BotUsername, null, game.GameShortName);
                    break;
                case InternalLinkTypeInstantView instantView:
                    navigation.NavigateToInstant(instantView.Url, instantView.FallbackUrl);
                    break;
                case InternalLinkTypeInvoice invoice:
                    NavigateToInvoice(navigation, invoice.InvoiceName);
                    break;
                case InternalLinkTypeLanguagePack languagePack:
                    NavigateToLanguage(clientService, navigation, languagePack.LanguagePackId);
                    break;
                case InternalLinkTypeMessage message:
                    NavigateToMessage(clientService, navigation, message.Url);
                    break;
                case InternalLinkTypeMessageDraft messageDraft:
                    NavigateToShare(navigation, messageDraft.Text, messageDraft.ContainsLink);
                    break;
                case InternalLinkTypePassportDataRequest:
                    break;
                case InternalLinkTypePremiumFeaturesPage premiumFeatures:
                    navigation.ShowPromo(new PremiumSourceLink(premiumFeatures.Referrer));
                    break;
                case InternalLinkTypePremiumGiftCode premiumGiftCode:
                    NavigateToPremiumGiftCode(clientService, navigation, premiumGiftCode.Code, source);
                    break;
                case InternalLinkTypePhoneNumberConfirmation phoneNumberConfirmation:
                    NavigateToConfirmPhone(clientService, phoneNumberConfirmation.PhoneNumber, phoneNumberConfirmation.Hash);
                    break;
                case InternalLinkTypeProxy proxy:
                    NavigateToProxy(clientService, navigation, proxy.Proxy);
                    break;
                case InternalLinkTypePublicChat publicChat:
                    NavigateToUsername(clientService, navigation, publicChat.ChatUsername, draftText: publicChat.DraftText, openProfile: publicChat.OpenProfile);
                    break;
                case InternalLinkTypeQrCodeAuthentication:
                    break;
                case InternalLinkTypeCallsPage calls:
                    NavigateToCalls(clientService, navigation, calls.Section);
                    break;
                case InternalLinkTypeMyProfilePage myProfile:
                    NavigateToMyProfile(clientService, navigation, myProfile.Section);
                    break;
                case InternalLinkTypeNewChannelChat:
                    navigation.ShowPopup(new NewChannelPopup());
                    break;
                case InternalLinkTypeNewGroupChat:
                    navigation.ShowPopup(new NewGroupPopup());
                    break;
                case InternalLinkTypeNewPrivateChat:
                    navigation.ShowPopup(new ContactsPopup());
                    break;
                case InternalLinkTypeSavedMessages:
                    navigation.NavigateToChat(clientService.Options.MyId, force: false);
                    break;
                case InternalLinkTypeSettings settings:
                    NavigateToSettings(clientService, navigation, settings.Section);
                    break;
                case InternalLinkTypeStickerSet stickerSet:
                    NavigateToStickerSet(navigation, stickerSet.StickerSetName);
                    break;
                case InternalLinkTypeStory story:
                    NavigateToStory(clientService, navigation, story.StoryPosterUsername, story.StoryId);
                    break;
                case InternalLinkTypeLiveStory liveStory:
                    NavigateToLiveStory(clientService, navigation, liveStory.StoryPosterUsername);
                    break;
                case InternalLinkTypeTheme theme:
                    NavigateToTheme(clientService, navigation, theme.ThemeName);
                    break;
                case InternalLinkTypeUnknownDeepLink unknownDeepLink:
                    NavigateToUnknownDeepLink(clientService, navigation, unknownDeepLink.Link);
                    break;
                case InternalLinkTypeUserPhoneNumber phoneNumber:
                    NavigateToPhoneNumber(clientService, navigation, phoneNumber.PhoneNumber, phoneNumber.DraftText, phoneNumber.OpenProfile);
                    break;
                case InternalLinkTypeUserToken userToken:
                    NavigateToUserToken(clientService, navigation, userToken.Token);
                    break;
                case InternalLinkTypeVideoChat videoChat:
                    NavigateToUsername(clientService, navigation, videoChat.ChatUsername, videoChat.InviteHash, null);
                    break;
                case InternalLinkTypeWebApp webApp:
                    NavigateToWebApp(clientService, navigation, webApp.BotUsername, webApp.StartParameter, webApp.WebAppShortName, webApp.Mode, source);
                    break;
                case InternalLinkTypeMainWebApp mainWebApp:
                    NavigateToMainWebApp(clientService, navigation, mainWebApp.BotUsername, mainWebApp.StartParameter, mainWebApp.Mode, source);
                    break;
                case InternalLinkTypeChatAffiliateProgram chatAffiliateProgram:
                    NavigateToUsername(clientService, navigation, chatAffiliateProgram.Username, referrer: chatAffiliateProgram.Referrer);
                    break;
                case InternalLinkTypeUpgradedGift upgradedGift:
                    NavigateToUpgradedGift(clientService, navigation, upgradedGift.Name);
                    break;
                case InternalLinkTypeGroupCall groupCall:
                    NavigateToGroupCall(clientService, navigation, new InputGroupCallLink(groupCall.InviteLink));
                    break;
                case InternalLinkTypeBotAddToChannel botAddToChannel:
                    NavigateToBotAddToChannel(clientService, navigation, botAddToChannel.BotUsername, botAddToChannel.AdministratorRights);
                    break;
                case InternalLinkTypeDirectMessagesChat directMessagesChat:
                    NavigateToDirectMessagesChat(clientService, navigation, directMessagesChat.ChannelUsername);
                    break;
                case InternalLinkTypeStoryAlbum storyAlbum:
                    NavigateToUsername(clientService, navigation, storyAlbum.StoryAlbumOwnerUsername);
                    break;
                case InternalLinkTypeGiftCollection giftCollection:
                    NavigateToUsername(clientService, navigation, giftCollection.GiftOwnerUsername);
                    break;
            }
        }

        private static void NavigateToCalls(IClientService clientService, INavigationService navigation, string section)
        {
            switch (section)
            {
                case "start-call":
                    CallsViewModel.NewCall(clientService, navigation);
                    break;
                case "all": break;
                case "missed": break;
                case "edit": break;
                case "show-tab": break;
                default:
                    navigation.ShowPopup(new CallsPopup());
                    break;
            }
        }

        private static void NavigateToMyProfile(IClientService clientService, INavigationService navigation, string section)
        {
            switch (section)
            {
                case "posts": break;
                case "posts/all-stories": break;
                case "posts/add-album": break;
                case "gifts": break;
                case "archived-posts": break;
                default:
                    navigation.Navigate(typeof(ProfilePage), clientService.Options.MyId);
                    break;
            }
        }

        private static void NavigateToSettings(IClientService clientService, INavigationService navigation, SettingsSection section)
        {
            switch (section)
            {
                case SettingsSectionAppearance appearance:
                    switch (appearance.Subsection)
                    {
                        case "themes": goto default;
                        case "themes/edit":
                        case "themes/create":
                            navigation.Navigate(typeof(SettingsThemesPage));
                            break;
                        case "wallpapers": goto default;
                        case "wallpapers/edit":
                        case "wallpapers/set":
                        case "wallpapers/choose-photo":
                            navigation.Navigate(typeof(SettingsBackgroundsPage));
                            break;
                        case "your-color/profile": goto default;
                        case "your-color/profile/add-icons":
                        case "your-color/profile/use-gift":
                        case "your-color/profile/reset":
                        case "your-color/name":
                        case "your-color/name/add-icons":
                        case "your-color/name/use-gift":
                            navigation.Navigate(typeof(SettingsProfileColorPage));
                            break;
                        case "stickers-and-emoji": goto default;
                        case "stickers-and-emoji/edit":
                        case "stickers-and-emoji/trending":
                        case "stickers-and-emoji/archived":
                        case "stickers-and-emoji/emoji":
                        case "stickers-and-emoji/suggest-by-emoji":
                        case "stickers-and-emoji/large-emoji":
                        case "stickers-and-emoji/dynamic-order":
                            navigation.Navigate(typeof(SettingsStickersPage));
                            break;
                        case "stickers-and-emoji/archived/edit":
                            navigation.Navigate(typeof(SettingsStickersPage), StickersType.Archived);
                            break;
                        case "stickers-and-emoji/emoji/edit":
                        case "stickers-and-emoji/emoji/archived":
                        case "stickers-and-emoji/emoji/suggest":
                        case "stickers-and-emoji/emoji/show-more":
                            navigation.Navigate(typeof(SettingsStickersPage), StickersType.Emoji);
                            break;
                        case "stickers-and-emoji/emoji/archived/edit":
                            navigation.Navigate(typeof(SettingsStickersPage), StickersType.EmojiArchived);
                            break;
                        case "stickers-and-emoji/emoji/quick-reaction":
                        case "stickers-and-emoji/emoji/quick-reaction/choose":
                        case "night-mode":
                        case "auto-night-mode":
                        case "text-size":
                        case "text-size/use-system":
                        case "message-corners":
                        case "animations":
                        case "app-icon":
                        case "tap-for-next-media":
                        default:
                            navigation.Navigate(typeof(SettingsAppearancePage));
                            break;
                    }
                    break;
                case SettingsSectionAskQuestion:
                    break;
                case SettingsSectionBusiness business:
                    switch (business.Subsection)
                    {
                        case "do-not-hide-ads":
                        default:
                            navigation.Navigate(typeof(BusinessPage));
                            break;
                    }
                    break;
                case SettingsSectionChatFolders chatFolders:
                    switch (chatFolders.Subsection)
                    {
                        case "edit":
                        case "create":
                        case "add-recommended":
                        case "show-tags":
                        case "tab-view":
                        default:
                            navigation.Navigate(typeof(FoldersPage));
                            break;
                    }
                    break;
                case SettingsSectionDataAndStorage dataAndStorage:
                    switch (dataAndStorage.Subsection)
                    {
                        case "storage":
                        case "storage/edit":
                        case "storage/auto-remove":
                        case "storage/clear-cache":
                        case "storage/max-cache":
                            navigation.Navigate(typeof(SettingsStoragePage));
                            break;
                        case "usage":
                        case "usage/mobile":
                        case "usage/wifi":
                        case "usage/roaming":
                        case "usage/reset":
                            navigation.Navigate(typeof(SettingsNetworkPage));
                            break;
                        //case "usage/mobile/auto-download": break;
                        //case "usage/mobile/auto-download/enable": break;
                        //case "usage/mobile/auto-download/usage": break;
                        //case "usage/mobile/auto-download/photos": break;
                        //case "usage/mobile/auto-download/stories": break;
                        //case "usage/mobile/auto-download/videos": break;
                        //case "usage/mobile/auto-download/files": break;
                        //case "usage/wifi/auto-download": break;
                        //case "usage/wifi/auto-download/enable": break;
                        //case "usage/wifi/auto-download/usage": break;
                        //case "usage/wifi/auto-download/photos": break;
                        //case "usage/wifi/auto-download/stories": break;
                        //case "usage/wifi/auto-download/videos": break;
                        //case "usage/wifi/auto-download/files": break;
                        //case "usage/roaming/auto-download": break;
                        //case "usage/roaming/auto-download/enable": break;
                        //case "usage/roaming/auto-download/usage": break;
                        //case "usage/roaming/auto-download/photos": break;
                        //case "usage/roaming/auto-download/stories": break;
                        //case "usage/roaming/auto-download/videos": break;
                        //case "usage/roaming/auto-download/files": break;
                        case "auto-download/data":
                        case "auto-download/data/enable":
                        case "auto-download/data/usage":
                        case "auto-download/data/photos":
                        case "auto-download/data/stories":
                        case "auto-download/data/videos":
                        case "auto-download/data/files":
                        case "auto-download/wifi":
                        case "auto-download/wifi/enable":
                        case "auto-download/wifi/usage":
                        case "auto-download/wifi/photos":
                        case "auto-download/wifi/stories":
                        case "auto-download/wifi/videos":
                        case "auto-download/wifi/files":
                        case "auto-download/roaming":
                        case "auto-download/roaming/enable":
                        case "auto-download/roaming/usage":
                        case "auto-download/roaming/photos":
                        case "auto-download/roaming/stories":
                        case "auto-download/roaming/videos":
                        case "auto-download/roaming/files":
                        case "auto-download/reset":
                        case "save-to-photos/chats":
                        case "save-to-photos/chats/max-video-size":
                        case "save-to-photos/chats/add-exception":
                        case "save-to-photos/chats/delete-all":
                        case "save-to-photos/groups":
                        case "save-to-photos/groups/max-video-size":
                        case "save-to-photos/groups/add-exception":
                        case "save-to-photos/groups/delete-all":
                        case "save-to-photos/channels":
                        case "save-to-photos/channels/max-video-size":
                        case "save-to-photos/channels/add-exception":
                        case "save-to-photos/channels/delete-all":
                        case "less-data-calls":
                        case "open-links":
                        case "share-sheet":
                        case "share-sheet/suggested-chats":
                        case "share-sheet/suggest-by":
                        case "share-sheet/reset":
                        case "saved-edited-photos":
                        case "pause-music":
                        case "raise-to-listen":
                        case "raise-to-speak":
                        case "show-18-content":
                        default:
                            navigation.Navigate(typeof(SettingsDataAndStoragePage));
                            break;
                        case "proxy":
                        case "proxy/edit":
                        case "proxy/use-proxy":
                        case "proxy/add-proxy":
                        case "proxy/share-list":
                        case "proxy/use-for-calls":
                            navigation.Navigate(typeof(SettingsProxyPage));
                            break;
                    }
                    break;
                case SettingsSectionDevices devices:
                    switch (devices.Subsection)
                    {
                        case "edit":
                        case "link-desktop":
                        case "terminate-sessions":
                        case "auto-terminate":
                        default:
                            navigation.Navigate(typeof(SettingsSessionsPage));
                            break;
                    }
                    break;
                case SettingsSectionEditProfile editProfile:
                    switch (editProfile.Subsection)
                    {
                        case "set-photo":
                        case "first-name":
                        case "last-name":
                        case "emoji-status":
                        case "bio":
                        case "birthday":
                        case "change-number":
                        case "username":
                        case "your-color":
                        case "channel":
                        case "add-account":
                        case "log-out":
                        case "profile-photo/use-emoji":
                        default:
                            navigation.Navigate(typeof(SettingsProfilePage));
                            break;
                        case "profile-color/profile":
                        case "profile-color/profile/add-icons":
                        case "profile-color/profile/use-gift":
                        case "profile-color/name":
                        case "profile-color/name/add-icons":
                        case "profile-color/name/use-gift":
                            navigation.Navigate(typeof(SettingsProfileColorPage));
                            break;
                    }
                    break;
                case SettingsSectionFaq:
                    break;
                case SettingsSectionFeatures:
                    break;
                case SettingsSectionInAppBrowser inAppBrowser:
                    switch (inAppBrowser.Subsection)
                    {
                        case "enable-browser": break;
                        case "clear-cookies": break;
                        case "clear-cache": break;
                        case "history": break;
                        case "clear-history": break;
                        case "never-open": break;
                        case "clear-list": break;
                        case "search": break;
                        default: break;
                    }
                    break;
                case SettingsSectionLanguage language:
                    switch (language.Subsection)
                    {
                        case "show-button":
                        case "translate-chats":
                        case "do-not-translate":
                        default:
                            navigation.Navigate(typeof(SettingsLanguagePage));
                            break;
                    }
                    break;
                case SettingsSectionMyStars myStars:
                    switch (myStars.Subsection)
                    {
                        case "top-up": break;
                        case "stats": break;
                        case "gift": break;
                        case "earn": break;
                        default: break;
                    }
                    break;
                case SettingsSectionMyToncoins myToncoins:
                    break;
                case SettingsSectionPowerSaving powerSaving:
                    switch (powerSaving.Subsection)
                    {
                        case "videos":
                        case "gifs":
                        case "stickers":
                        case "emoji":
                        case "effects":
                        case "preload":
                        case "background":
                        case "call-animations":
                        case "particles":
                        case "transitions":
                        default:
                            navigation.Navigate(typeof(SettingsPowerSavingPage));
                            break;
                    }
                    break;
                case SettingsSectionPremium premium:
                    break;
                case SettingsSectionPrivacyAndSecurity privacyAndSecurity:
                    switch (privacyAndSecurity.Subsection)
                    {
                        case "blocked": goto default;
                        case "blocked/edit":
                        case "blocked/block-user":
                        case "blocked/block-user/chats":
                        case "blocked/block-user/contacts":
                            navigation.Navigate(typeof(SettingsBlockedChatsPage));
                            break;
                        case "active-websites": goto default;
                        case "active-websites/edit":
                        case "active-websites/disconnect-all":
                            navigation.Navigate(typeof(SettingsWebSessionsPage));
                            break;
                        case "passcode": goto default;
                        case "passcode/disable":
                        case "passcode/change":
                        case "passcode/auto-lock":
                        case "passcode/face-id":
                        case "passcode/fingerprint":
                            break;
                        case "2sv": goto default;
                        case "2sv/change":
                        case "2sv/disable":
                        case "2sv/change-email":
                            break;
                        case "passkey": goto default;
                        case "passkey/create":
                            break;
                        case "auto-delete": goto default;
                        case "auto-delete/set-custom":
                            break;
                        case "login-email": goto default;
                        case "phone-number": goto default;
                        case "phone-number/never":
                        case "phone-number/always":
                            break;
                        case "last-seen": goto default;
                        case "last-seen/never":
                        case "last-seen/always":
                        case "last-seen/hide-read-time":
                            break;
                        case "profile-photos": goto default;
                        case "profile-photos/never":
                        case "profile-photos/always":
                        case "profile-photos/set-public":
                        case "profile-photos/update-public":
                        case "profile-photos/remove-public":
                            break;
                        case "bio": goto default;
                        case "bio/never":
                        case "bio/always":
                            break;
                        case "gifts": goto default;
                        case "gifts/show-icon":
                        case "gifts/never":
                        case "gifts/always":
                        case "gifts/accepted-types":
                            break;
                        case "birthday": goto default;
                        case "birthday/add":
                        case "birthday/never":
                        case "birthday/always":
                            break;
                        case "saved-music": goto default;
                        case "saved-music/never":
                        case "saved-music/always":
                            break;
                        case "forwards": goto default;
                        case "forwards/never":
                        case "forwards/always":
                            break;
                        case "calls": goto default;
                        case "calls/never":
                        case "calls/always":
                        case "calls/p2p":
                            break;
                        case "calls/p2p/never":
                        case "calls/p2p/always":
                            break;
                        case "calls/ios-integration": break;
                        case "voice": goto default;
                        case "voice/never":
                        case "voice/always":
                            break;
                        case "messages": goto default;
                        case "messages/set-price":
                        case "messages/exceptions":
                            break;
                        case "invites": goto default;
                        case "invites/never":
                        case "invites/always":
                            break;
                        case "self-destruct":
                        case "data-settings":
                        case "data-settings/sync-contacts":
                        case "data-settings/delete-synced":
                        case "data-settings/suggest-contacts":
                        case "data-settings/delete-cloud-drafts":
                        case "data-settings/clear-payment-info":
                        case "data-settings/link-previews":
                        case "data-settings/bot-settings":
                        case "data-settings/map-provider":
                        case "archive-and-mute":
                        default:
                            navigation.Navigate(typeof(SettingsPrivacyAndSecurityPage));
                            break;
                    }
                    break;
                case SettingsSectionPrivacyPolicy:
                    break;
                case SettingsSectionQrCode qrCode:
                    switch (qrCode.Subsection)
                    {
                        case "share": break;
                        case "scan": break;
                        default: break;
                    }
                    break;
                case SettingsSectionSearch:
                    break;
                case SettingsSectionSendGift sendGift:
                    switch (sendGift.Subsection)
                    {
                        case "self": break;
                        default: break;
                    }
                    break;

            }
        }

        private static async void NavigateToDirectMessagesChat(IClientService clientService, INavigationService navigation, string channelUsername)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(channelUsername));
            if (response is Chat chat && clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.IsChannel && supergroup.HasDirectMessagesGroup)
                {
                    var fullInfo = clientService.GetSupergroupFull(supergroup.Id);
                    fullInfo ??= await clientService.SendAsync(new GetSupergroupFullInfo(supergroup.Id)) as SupergroupFullInfo;

                    if (fullInfo != null && fullInfo.DirectMessagesChatId != 0)
                    {
                        navigation.NavigateToChat(fullInfo.DirectMessagesChatId);
                    }
                }
            }
        }

        private static async void NavigateToBotAddToChannel(IClientService clientService, INavigationService navigation, string botUsername, ChatAdministratorRights administratorRights)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(botUsername));
            if (response is Chat chat && clientService.TryGetUser(chat, out User botUser))
            {
                navigation.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationBotAddToChannel(botUser.Id, administratorRights));
            }
        }

        public static async void NavigateToGroupCall(IClientService clientService, INavigationService navigation, InputGroupCall inputGroupCall)
        {
            var response = await clientService.SendAsync(new GetGroupCallParticipants(inputGroupCall, 3));
            if (response is GroupCallParticipants participants)
            {
                var confirm = await navigation.ShowPopupAsync(new JoinGroupCallPopup(clientService, participants));
                if (confirm == ContentDialogResult.Primary)
                {
                    clientService.Session.Resolve<IVoipService>().JoinGroupCall(navigation, inputGroupCall);
                }
            }
            else
            {
                navigation.ShowToast(Strings.LinkIsNoActive, ToastPopupIcon.Error);
            }
        }

        public static async void NavigateToUpgradedGift(IClientService clientService, INavigationService navigation, string name)
        {
            var response = await clientService.SendAsync(new GetUpgradedGift(name));
            if (response is UpgradedGift gift)
            {
                var text = gift.OriginalDetails?.Text ?? string.Empty.AsFormattedText();
                var receivedGift = new ReceivedGift(string.Empty, null, text, 0, true, false, false, false, false, false, 0, new SentGiftUpgraded(gift), Array.Empty<int>(), 0, 0, false, 0, 0, 0, 0, 0, string.Empty, 0);

                navigation.ShowPopup(new ReceivedGiftPopup(clientService, navigation, receivedGift, null, null));
            }
            else
            {
                navigation.ShowToast(Strings.UniqueGiftNotFound, ToastPopupIcon.Error);
            }
        }

        private static async void NavigateToPremiumGiftCode(IClientService clientService, INavigationService navigation, string code, OpenUrlSource source)
        {
            var response = await clientService.SendAsync(new CheckPremiumGiftCode(code));
            if (response is PremiumGiftCodeInfo info)
            {
                if (source is OpenUrlSourceChat sourceChat)
                {
                    navigation.ShowPopup(new PromoPopup(clientService, sourceChat.SenderId ?? new MessageSenderChat(sourceChat.ChatId), info, code));
                }
                else
                {
                    navigation.ShowPopup(new PromoPopup(clientService, null, info, code));
                }
            }
            else
            {
                // TODO: error
            }
        }

        private static async void NavigateToAttachmentMenuBot(IClientService clientService, INavigationService navigation, InternalLinkTypeAttachmentMenuBot attachmentMenuBot, OpenUrlSource source)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(attachmentMenuBot.BotUsername));
            if (response is Chat chat && clientService.TryGetUser(chat, out User botUser))
            {
                if (botUser.Type is not UserTypeBot userTypeBot || !userTypeBot.CanBeAddedToAttachmentMenu)
                {
                    return;
                }

                var sourceChat = source switch
                {
                    OpenUrlSourceChat sourceMessage => clientService.GetChat(sourceMessage.ChatId),
                    _ => null
                };

                var response2 = await clientService.SendAsync(new GetAttachmentMenuBot(botUser.Id));
                if (response2 is AttachmentMenuBot menuBot)
                {
                    OpenMiniApp(clientService, navigation, botUser, menuBot, attachmentMenuBot.Url, sourceChat, attachmentMenuBot);
                }
            }
        }

        public static async void OpenMiniApp(IClientService clientService, INavigationService navigation, User user, AttachmentMenuBot bot, string url, Chat sourceChat = null, InternalLinkType sourceLink = null, Action<bool> continuation = null)
        {
            if (bot.ShowDisclaimerInSideMenu || !clientService.IsBotAddedToAttachmentMenu(bot.BotUserId))
            {
                var textBlock = new TextBlock();

                var markdown = ClientEx.ParseMarkdown(Strings.BotWebAppDisclaimerCheck);
                if (markdown != null && markdown.Entities.Count == 1)
                {
                    markdown.Entities[0].Type = new TextEntityTypeTextUrl(Strings.WebAppDisclaimerUrl);
                    TextBlockHelper.SetFormattedText(textBlock, markdown);
                }
                else
                {
                    textBlock.Text = Strings.BotWebAppDisclaimerCheck;
                }

                var popup = new MessagePopup
                {
                    Title = Strings.TermsOfUse,
                    Message = Strings.BotWebAppDisclaimerSubtitle,
                    CheckBoxLabel = textBlock,
                    PrimaryButtonText = Strings.Continue,
                    SecondaryButtonText = Strings.Cancel,
                    IsCheckedRequired = true
                };

                var confirm = await navigation.ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    continuation?.Invoke(false);
                    return;
                }

                await clientService.SendAsync(new ToggleBotIsAddedToAttachmentMenu(bot.BotUserId, true, true));
            }

            continuation?.Invoke(true);

            var response = await clientService.SendAsync(new GetWebAppUrl(bot.BotUserId, url, new WebAppOpenParameters(Theme.Current.Parameters, Constants.WebAppHostName, new WebAppOpenModeFullSize())));
            if (response is HttpUrl httpUrl)
            {
                navigation.NavigateToWebApp(user, httpUrl.Url, 0, bot, null, sourceChat, sourceLink);
            }
        }

        private static async void NavigateToChatBoost(IClientService clientService, INavigationService navigation, string url)
        {
            var response = await clientService.SendAsync(new GetChatBoostLinkInfo(url));
            if (response is ChatBoostLinkInfo linkInfo)
            {
                if (linkInfo.ChatId == 0 || !clientService.TryGetChat(linkInfo.ChatId, out Chat chat))
                {
                    navigation.ShowToast(Strings.NoUsernameFound, ToastPopupIcon.Info);
                    return;
                }

                var response1 = await clientService.SendAsync(new GetChatBoostFeatures(chat.Type is ChatTypeSupergroup { IsChannel: true }));
                var response2 = await clientService.SendAsync(new GetAvailableChatBoostSlots());
                var response3 = await clientService.SendAsync(new GetChatBoostStatus(linkInfo.ChatId));

                if (response1 is ChatBoostFeatures features && response2 is ChatBoostSlots slots && response3 is ChatBoostStatus status)
                {
                    navigation.ShowPopup(new ChatBoostFeaturesPopup(clientService, navigation, chat, status, slots, features, ChatBoostFeature.None, 0));
                }
            }
        }

        private static async void NavigateToStory(IClientService clientService, INavigationService navigation, string username, int storyId)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat)
            {
                var response2 = await clientService.SendAsync(new GetStory(chat.Id, storyId, false));
                if (response2 is Story story)
                {
                    var settings = clientService.Session.Resolve<ISettingsService>();
                    var aggregator = clientService.Session.Resolve<IEventAggregator>();

                    var activeStories = new ActiveStoriesViewModel(clientService, settings, aggregator, story);
                    var viewModel = StoryListViewModel.Create(navigation, activeStories);

                    var window = new StoriesWindow();
                    window.Update(viewModel, activeStories, StoryOpenOrigin.Card, Rect.Empty, null);
                    _ = window.ShowAsync(navigation.XamlRoot);
                }
                else
                {
                    navigation.ShowToast(Strings.StoryNotFound, ToastPopupIcon.ExpiredStory);
                }
            }
            else
            {
                navigation.ShowToast(Strings.NoUsernameFound, ToastPopupIcon.Info);
            }
        }

        private static async void NavigateToLiveStory(IClientService clientService, INavigationService navigation, string username)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat)
            {
                var response2 = await clientService.SendAsync(new GetChatActiveStories(chat.Id));
                if (response2 is ChatActiveStories stories)
                {
                    var liveStory = stories.Stories.FirstOrDefault(x => x.IsLive);
                    if (liveStory != null)
                    {
                        var response3 = await clientService.SendAsync(new GetStory(chat.Id, liveStory.StoryId, false));
                        if (response3 is Story story)
                        {
                            if (story.Content is StoryContentLive live && !clientService.TryGetGroupCall(live.GroupCallId, out _))
                            {
                                await clientService.SendAsync(new GetGroupCall(live.GroupCallId));
                            }

                            var settings = clientService.Session.Resolve<ISettingsService>();
                            var aggregator = clientService.Session.Resolve<IEventAggregator>();

                            var activeStories = new ActiveStoriesViewModel(clientService, settings, aggregator, story);
                            var viewModel = StoryListViewModel.Create(navigation, activeStories);

                            var window = new StoriesWindow();
                            window.Update(viewModel, activeStories, StoryOpenOrigin.Card, Rect.Empty, null);
                            _ = window.ShowAsync(navigation.XamlRoot);

                            return;
                        }
                    }
                }

                navigation.ShowToast(Strings.StoryNotFound, ToastPopupIcon.ExpiredStory);
            }
            else
            {
                navigation.ShowToast(Strings.NoUsernameFound, ToastPopupIcon.Info);
            }
        }

        public static async void NavigateToWebApp(IClientService clientService, INavigationService navigation, string botUsername, string startParameter, string webAppShortName, WebAppOpenMode mode, OpenUrlSource source)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(botUsername));
            if (response is Chat chat && clientService.TryGetUser(chat, out User botUser))
            {
                if (botUser.Type is not UserTypeBot)
                {
                    return;
                }

                var responss = await clientService.SendAsync(new SearchWebApp(botUser.Id, webAppShortName));
                if (responss is FoundWebApp foundWebApp)
                {
                    var popup = new MessagePopup
                    {
                        Title = Strings.AppName,
                        Message = Strings.BotWebViewStartPermission,
                        PrimaryButtonText = Strings.Start,
                        SecondaryButtonText = Strings.Cancel,
                    };

                    if (foundWebApp.RequestWriteAccess)
                    {
                        var textBlock = new TextBlock
                        {
                            TextWrapping = TextWrapping.Wrap
                        };

                        var markdown = ClientEx.ParseMarkdown(string.Format(Strings.OpenUrlOption2, botUser.FirstName));
                        if (markdown != null)
                        {
                            TextBlockHelper.SetFormattedText(textBlock, markdown);
                        }
                        else
                        {
                            textBlock.Text = Strings.OpenUrlOption2;
                        }

                        popup.CheckBoxLabel = textBlock;
                    }

                    var confirm = await navigation.ShowPopupAsync(popup);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    var chatId = source switch
                    {
                        OpenUrlSourceChat sourceMessage => sourceMessage.ChatId,
                        _ => 0
                    };

                    var sourceChat = clientService.GetChat(chatId);

                    var responsa = await clientService.SendAsync(new GetWebAppLinkUrl(chatId, botUser.Id, webAppShortName, startParameter, foundWebApp.RequestWriteAccess && popup.IsChecked is true, new WebAppOpenParameters(Theme.Current.Parameters, Constants.WebAppHostName, mode)));
                    if (responsa is HttpUrl url)
                    {
                        navigation.NavigateToWebApp(botUser, url.Url, openMode: mode, sourceChat: sourceChat, sourceLink: new InternalLinkTypeWebApp(botUsername, webAppShortName, startParameter, mode));
                    }
                }
                else
                {
                    navigation.NavigateToChat(chat);
                }
            }
            else
            {
                navigation.ShowToast(Strings.NoUsernameFound, ToastPopupIcon.Info);
            }
        }

        public static async void NavigateToMainWebApp(IClientService clientService, INavigationService navigation, string botUsername, string startParameter, WebAppOpenMode mode, OpenUrlSource source = null)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(botUsername));
            if (response is Chat chat && clientService.TryGetUser(chat, out User botUser))
            {
                NavigateToMainWebApp(clientService, navigation, botUser, startParameter, mode, source);
            }
            else
            {
                navigation.ShowToast(Strings.NoUsernameFound, ToastPopupIcon.Info);
            }
        }

        public static async void NavigateToMainWebApp(IClientService clientService, INavigationService navigation, User botUser, string startParameter, WebAppOpenMode mode, OpenUrlSource source = null)
        {
            if (botUser.Type is not UserTypeBot { HasMainWebApp: true })
            {
                return;
            }

            AttachmentMenuBot menuBot = null;
            if (botUser.Type is UserTypeBot { CanBeAddedToAttachmentMenu: true })
            {
                menuBot = await clientService.SendAsync(new GetAttachmentMenuBot(botUser.Id)) as AttachmentMenuBot;
            }

            if (menuBot?.RequestWriteAccess is true)
            {
                var popup = new MessagePopup
                {
                    Title = Strings.AppName,
                    Message = Strings.BotWebViewStartPermission,
                    PrimaryButtonText = Strings.Start,
                    SecondaryButtonText = Strings.Cancel,
                };

                var textBlock = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap
                };

                var markdown = ClientEx.ParseMarkdown(string.Format(Strings.OpenUrlOption2, botUser.FirstName));
                if (markdown != null)
                {
                    TextBlockHelper.SetFormattedText(textBlock, markdown);
                }
                else
                {
                    textBlock.Text = Strings.OpenUrlOption2;
                }

                popup.CheckBoxLabel = textBlock;

                var confirm = await navigation.ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            var chatId = source switch
            {
                OpenUrlSourceChat sourceMessage => sourceMessage.ChatId,
                _ => 0
            };

            var responsa = await clientService.SendAsync(new GetMainWebApp(chatId, botUser.Id, startParameter, new WebAppOpenParameters(Theme.Current.Parameters, Constants.WebAppHostName, mode)));
            if (responsa is MainWebApp webApp)
            {
                navigation.NavigateToWebApp(botUser, webApp.Url, menuBot: menuBot, openMode: webApp.Mode, sourceLink: new InternalLinkTypeMainWebApp(botUser.ActiveUsername(), startParameter, webApp.Mode));
            }
        }

        private static async void NavigateToUnknownDeepLink(IClientService clientService, INavigationService navigation, string url)
        {
            var response = await clientService.SendAsync(new GetDeepLinkInfo(url));
            if (response is DeepLinkInfo info)
            {
                var confirm = await navigation.ShowPopupAsync(info.Text, Strings.AppName, Strings.OK, info.NeedUpdateApplication ? Strings.UpdateApp : null);
                if (confirm == ContentDialogResult.Secondary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?PFN=" + Package.Current.Id.FamilyName));
                }
            }
        }

        private static async void NavigateToBackground(IClientService clientService, INavigationService navigation, string slug)
        {
            var response = await clientService.SendAsync(new SearchBackground(slug));
            if (response is Background background)
            {
                navigation.ShowPopup(new BackgroundPopup(), new BackgroundParameters(background));
            }
        }

        private static async void NavigateToMessage(IClientService clientService, INavigationService navigation, string url)
        {
            var response = await clientService.SendAsync(new GetMessageLinkInfo(url));
            if (response is MessageLinkInfo info && clientService.TryGetChat(info.ChatId, out Chat chat))
            {
                if (info.Message != null)
                {
                    if (info.TopicId is MessageTopicThread topicThread)
                    {
                        var thread = await clientService.SendAsync(new GetMessageThread(info.ChatId, topicThread.MessageThreadId));
                        if (thread is MessageThreadInfo)
                        {
                            navigation.NavigateToChat(chat, info.Message.Id, topic: info.TopicId);
                        }
                        else
                        {
                            navigation.ShowPopup(Strings.LinkNotFound, Strings.AppName, Strings.OK);
                        }
                    }
                    if (info.TopicId is MessageTopicForum topicForum)
                    {
                        var topic = await clientService.SendAsync(new GetForumTopic(chat.Id, topicForum.ForumTopicId)) as ForumTopic;
                        if (topic != null)
                        {
                            navigation.NavigateToChat(chat, info.Message.Id, topic: info.TopicId);
                        }
                        else
                        {
                            navigation.ShowPopup(Strings.LinkNotFound, Strings.AppName, Strings.OK);
                        }
                    }
                    else
                    {
                        navigation.NavigateToChat(chat, info.Message.Id);
                    }
                }
                else
                {
                    navigation.NavigateToChat(chat, topic: info.TopicId);
                }
            }
            else
            {
                navigation.ShowPopup(Strings.LinkNotFound, Strings.AppName, Strings.OK);
            }
        }

        private static void NavigateToTheme(IClientService clientService, INavigationService navigation, string slug)
        {
            navigation.ShowPopup(Strings.ThemeNotSupported, Strings.Theme, Strings.OK);
        }

        private static void NavigateToInvoice(INavigationService navigation, string invoiceName)
        {
            navigation.NavigateToInvoice(new InputInvoiceName(invoiceName));
        }

        public static async void NavigateToLanguage(IClientService clientService, INavigationService navigation, string languagePackId)
        {
            var response = await clientService.SendAsync(new GetLanguagePackInfo(languagePackId));
            if (response is LanguagePackInfo info)
            {
                if (info.Id == SettingsService.Current.LanguagePackId)
                {
                    var confirm = await navigation.ShowPopupAsync(string.Format(Strings.LanguageSame, info.Name), Strings.Language, Strings.OK, Strings.Settings);
                    if (confirm != ContentDialogResult.Secondary)
                    {
                        return;
                    }

                    navigation.Navigate(typeof(SettingsLanguagePage));
                }
                else if (info.TotalStringCount == 0)
                {
                    navigation.ShowPopup(string.Format(Strings.LanguageUnknownCustomAlert, info.Name), Strings.LanguageUnknownTitle, Strings.OK);
                }
                else
                {
                    var message = info.IsOfficial
                        ? Strings.LanguageAlert
                        : Strings.LanguageCustomAlert;

                    var start = message.IndexOf('[');
                    var end = message.IndexOf(']');
                    if (start != -1 && end != -1)
                    {
                        message = message.Insert(end + 1, $"({info.TranslationUrl})");
                    }

                    var confirm = await navigation.ShowPopupAsync(string.Format(message, info.Name, (int)Math.Ceiling(info.TranslatedStringCount / (float)info.TotalStringCount * 100)), Strings.LanguageTitle, Strings.Change, Strings.Cancel);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    var set = await LocaleService.Current.SetLanguageAsync(info, true);
                    if (set is Ok)
                    {
                        //ApplicationLanguages.PrimaryLanguageOverride = info.Id;
                        //ResourceContext.GetForCurrentView().Reset();
                        //ResourceContext.GetForViewIndependentUse().Reset();

                        //TLWindowContext.Current.NavigationServices.Remove(NavigationService);
                        //BootStrapper.Current.NavigationService.Reset();

                        WindowContext.ForEach(window =>
                        {
                            ResourceContext.GetForCurrentView().Reset();
                            ResourceContext.GetForViewIndependentUse().Reset();

                            if (window.Content is FrameworkElement frameworkElement)
                            {
                                //window.CoreWindow.FlowDirection = _localeService.FlowDirection == FlowDirection.RightToLeft
                                //    ? CoreWindowFlowDirection.RightToLeft
                                //    : CoreWindowFlowDirection.LeftToRight;

                                frameworkElement.FlowDirection = LocaleService.Current.FlowDirection;
                            }

                            if (window.Content is RootPage root)
                            {
                                root.UpdateComponent();
                            }
                        });
                    }
                }
            }
        }

        public static async void NavigateToSendCode(IClientService clientService, INavigationService navigation, string phoneCode)
        {
            if (clientService.AuthorizationState is AuthorizationStateWaitCode)
            {
                if (clientService.Options.TryGetValue("x_firstname", out string firstValue))
                {
                }

                if (clientService.Options.TryGetValue("x_lastname", out string lastValue))
                {
                }

                var response = await clientService.SendAsync(new CheckAuthenticationCode(phoneCode));
                if (response is Error error)
                {
                    if (error.MessageEquals(ErrorType.PHONE_NUMBER_INVALID))
                    {
                        navigation.ShowPopup(error.Message, Strings.InvalidPhoneNumber, Strings.OK);
                    }
                    else if (error.MessageEquals(ErrorType.PHONE_CODE_EMPTY) || error.MessageEquals(ErrorType.PHONE_CODE_INVALID))
                    {
                        navigation.ShowPopup(error.Message, Strings.InvalidCode, Strings.OK);
                    }
                    else if (error.MessageEquals(ErrorType.PHONE_CODE_EXPIRED))
                    {
                        navigation.ShowPopup(error.Message, Strings.CodeExpired, Strings.OK);
                    }
                    else if (error.MessageEquals(ErrorType.FIRSTNAME_INVALID))
                    {
                        navigation.ShowPopup(error.Message, Strings.InvalidFirstName, Strings.OK);
                    }
                    else if (error.MessageEquals(ErrorType.LASTNAME_INVALID))
                    {
                        navigation.ShowPopup(error.Message, Strings.InvalidLastName, Strings.OK);
                    }
                    else if (error.Message.StartsWith("FLOOD_WAIT"))
                    {
                        navigation.ShowPopup(Strings.FloodWait, Strings.AppName, Strings.OK);
                    }
                    else if (error.Code != -1000)
                    {
                        navigation.ShowPopup(error.Message, Strings.AppName, Strings.OK);
                    }

                    Logger.Error("account.signIn error " + error);
                }
            }
            else
            {
                if (phoneCode.Length > 3)
                {
                    phoneCode = phoneCode.Substring(0, 3) + "-" + phoneCode.Substring(3);
                }

                navigation.ShowPopup(string.Format(Strings.OtherLoginCode, phoneCode), Strings.AppName, Strings.OK);
            }
        }

        public static async void NavigateToShare(INavigationService navigation, FormattedText text, bool hasUrl)
        {
            await navigation.ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostText(text));
        }

        public static async void NavigateToProxy(IClientService clientService, INavigationService navigation, Proxy proxy)
        {
            if (proxy == null)
            {
                navigation.ShowToast(Strings.ProxyLinkUnsupported, ToastPopupIcon.Error);
                return;
            }

            var confirm = await navigation.ShowPopupAsync(new AddProxyPopup(clientService, navigation, proxy));
            if (confirm == ContentDialogResult.Primary)
            {
                LifetimeService.Current.Proxy.AddProxy(proxy, Constants.RELEASE);
            }
        }

        public static void NavigateToConfirmPhone(IClientService clientService, string phone, string hash)
        {
            //var response = await clientService.SendConfirmPhoneCodeAsync(hash, false);
            //if (response.IsSucceeded)
            //{
            //    var state = new SignInSentCodePage.NavigationParameters
            //    {
            //        PhoneNumber = phone,
            //        //Result = response.Result,
            //    };

            //    App.Current.NavigationService.Navigate(typeof(SignInSentCodePage), state);

            //    //Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
            //    //{
            //    //    if (frame != null)
            //    //    {
            //    //        frame.CloseBlockingProgress();
            //    //    }
            //    //    TelegramViewBase.NavigateToConfirmPhone(result);
            //    //});
            //}
            //else
            //{
            //    //if (error.CodeEquals(ErrorCode.BAD_REQUEST) && error.TypeEquals(ErrorType.USERNAME_NOT_OCCUPIED))
            //    //{
            //    //    return;
            //    //}
            //    //Telegram.Api.Helpers.Logs.Log.Write(string.Format("account.sendConfirmPhoneCode error {0}", error));
            //};
        }

        public static async void NavigateToStickerSet(INavigationService navigation, string text)
        {
            await StickersPopup.ShowAsync(navigation, text);
        }

        public static async void NavigateToPhoneNumber(IClientService clientService, INavigationService navigation, string phoneNumber, string draftText = null, bool openProfile = false)
        {
            await NavigateToUserByResponse(clientService, navigation, new SearchUserByPhoneNumber(phoneNumber, false), draftText, openProfile);
        }

        public static async void NavigateToUserToken(IClientService clientService, INavigationService navigation, string userToken)
        {
            await NavigateToUserByResponse(clientService, navigation, new SearchUserByToken(userToken));
        }

        private static async Task NavigateToUserByResponse(IClientService clientService, INavigationService navigation, Function request, string draftText = null, bool openProfile = false)
        {
            var response = await clientService.SendAsync(request);
            if (response is User user)
            {
                var chat = await clientService.SendAsync(new CreatePrivateChat(user.Id, false)) as Chat;
                if (chat != null)
                {
                    if (draftText != null)
                    {
                        navigation.NavigateToChat(chat, state: new NavigationState { { "draft", draftText.AsFormattedText() } });
                    }
                    else if (openProfile)
                    {
                        navigation.Navigate(typeof(ProfilePage), chat.Id);
                    }
                    else
                    {
                        navigation.NavigateToChat(chat);
                    }
                }
                else
                {
                    navigation.ShowToast(Strings.NoUsernameFound, ToastPopupIcon.Info);
                }
            }
            else
            {
                navigation.ShowToast(Strings.NoUsernameFound, ToastPopupIcon.Info);
            }
        }

        public static async void NavigateToBotStart(IClientService clientService, INavigationService navigation, string username, string startParameter, bool autoStart, bool group)
        {
            var response = await clientService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat && clientService.TryGetUser(chat, out User user))
            {
                if (group)
                {
                    navigation.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationStartBot(user, startParameter));
                }
                else if (autoStart)
                {
                    clientService.Send(new SendBotStartMessage(user.Id, chat.Id, startParameter));
                    navigation.NavigateToChat(chat);
                }
                else
                {
                    navigation.NavigateToChat(chat, accessToken: startParameter);
                }
            }
            else
            {
                navigation.ShowToast(Strings.NoUsernameFound, ToastPopupIcon.Info);
            }
        }

        public static async void NavigateToBusinessChat(IClientService clientService, INavigationService navigation, string linkName)
        {
            var response = await clientService.SendAsync(new GetBusinessChatLinkInfo(linkName));
            if (response is BusinessChatLinkInfo info)
            {
                navigation.NavigateToChat(info.ChatId, state: new NavigationState { { "draft", info.Text } });
            }
        }

        public static async void NavigateToUsername(IClientService clientService, INavigationService navigation, string username, string videoChat = null, string game = null, string draftText = null, string referrer = null, bool openProfile = false)
        {
            var response = await clientService.SendAsync(referrer != null ? new SearchChatAffiliateProgram(username, referrer) : new SearchPublicChat(username));
            if (response is Chat chat)
            {
                if (game != null)
                {

                }
                else if (clientService.TryGetUser(chat, out User user))
                {
                    if (draftText != null)
                    {
                        navigation.NavigateToChat(chat, state: new NavigationState { { "draft", draftText.AsFormattedText() } });
                    }
                    else if (openProfile)
                    {
                        navigation.Navigate(typeof(ProfilePage), chat.Id);
                    }
                    else
                    {
                        navigation.NavigateToChat(chat);

                        if (chat.LastMessage != null && referrer != null)
                        {
                            clientService.Send(new SendBotStartMessage(user.Id, chat.Id, string.Empty));
                        }
                    }
                }
                else if (videoChat != null)
                {
                    navigation.NavigateToChat(chat, state: new NavigationState { { "videoChat", videoChat } });
                }
                else if (clientService.IsForum(chat))
                {
                    navigation.NavigateToForum(chat);
                }
                else
                {
                    navigation.NavigateToChat(chat);
                }
            }
            else if (referrer != null)
            {
                navigation.ShowPopup(Strings.AffiliateLinkExpiredText, Strings.AffiliateLinkExpiredTitle, Strings.OK);
            }
            else
            {
                navigation.ShowToast(Strings.NoUsernameFound, ToastPopupIcon.Info);
            }
        }

        public static async void NavigateToInviteLink(IClientService clientService, INavigationService navigation, string link)
        {
            var response = await clientService.CheckChatInviteLinkAsync(link);
            if (response is ChatInviteLinkInfo info)
            {
                if (info.ChatId != 0)
                {
                    navigation.NavigateToChat(info.ChatId);
                }
                else
                {
                    var popup = new JoinChatPopup(clientService, info);

                    var confirm = await navigation.ShowPopupAsync(popup);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    var import = await clientService.SendAsync(new JoinChatByInviteLink(link));
                    if (import is Chat chat)
                    {
                        navigation.NavigateToChat(chat);
                    }
                    else if (import is Error error)
                    {
                        if (error.MessageEquals(ErrorType.INVITE_REQUEST_SENT))
                        {
                            navigation.ShowPopup(info.Type is InviteLinkChatTypeChannel ? Strings.RequestToJoinChannelSentDescription : Strings.RequestToJoinGroupSentDescription, Strings.RequestToJoinSent, Strings.OK);
                            return;

                            var message = Strings.RequestToJoinSent + Environment.NewLine + (info.Type is InviteLinkChatTypeChannel ? Strings.RequestToJoinChannelSentDescription : Strings.RequestToJoinGroupSentDescription);
                            var entity = new TextEntity(0, Strings.RequestToJoinSent.Length, new TextEntityTypeBold());

                            var text = new FormattedText(message, new[] { entity });

                            navigation.ShowToast(text, ToastPopupIcon.JoinRequested);
                        }
                        else if (error.MessageEquals(ErrorType.FLOOD_WAIT))
                        {
                            navigation.ShowPopup(Strings.FloodWait, Strings.AppName, Strings.OK);
                        }
                        else if (error.MessageEquals(ErrorType.USERS_TOO_MUCH))
                        {
                            navigation.ShowPopup(Strings.JoinToGroupErrorFull, Strings.AppName, Strings.OK);
                        }
                        else
                        {
                            navigation.ShowPopup(Strings.JoinToGroupErrorNotExist, Strings.AppName, Strings.OK);
                        }
                    }
                }
            }
            else if (response is Error error)
            {
                if (error.MessageEquals(ErrorType.FLOOD_WAIT))
                {
                    navigation.ShowPopup(Strings.FloodWait, Strings.AppName, Strings.OK);
                }
                else
                {
                    navigation.ShowPopup(Strings.JoinToGroupErrorNotExist, Strings.AppName, Strings.OK);
                }
            }
        }

        public static async void NavigateToChatFolderInviteLink(IClientService clientService, INavigationService navigation, string link)
        {
            var response = await clientService.SendAsync(new CheckChatFolderInviteLink(link));
            if (response is ChatFolderInviteLinkInfo info)
            {
                var tsc = new TaskCompletionSource<object>();

                var confirm = await navigation.ShowPopupAsync(new AddFolderPopup(tsc), info);
                if (confirm == ContentDialogResult.Primary)
                {
                    var result = await tsc.Task;
                    if (result is IList<long> chats)
                    {
                        if (info.ChatFolderInfo.Id == 0)
                        {
                            var import = await clientService.SendAsync(new AddChatFolderByInviteLink(link, chats));
                            if (import is Error error)
                            {
                                if (error.MessageEquals(ErrorType.CHATLISTS_TOO_MUCH))
                                {
                                    navigation.ShowLimitReached(new PremiumLimitTypeShareableChatFolderCount());
                                }
                                else if (error.MessageEquals(ErrorType.FILTER_INCLUDE_TOO_MUCH))
                                {
                                    navigation.ShowLimitReached(new PremiumLimitTypeChatFolderChosenChatCount());
                                }
                                else if (error.MessageEquals(ErrorType.CHANNELS_TOO_MUCH))
                                {
                                    navigation.ShowLimitReached(new PremiumLimitTypeSupergroupCount());
                                }
                                else
                                {
                                    navigation.ShowPopup(Strings.FolderLinkExpiredAlert, Strings.AppName, Strings.OK);
                                }
                            }
                        }
                        else if (chats.Count > 0)
                        {
                            clientService.Send(new ProcessChatFolderNewChats(info.ChatFolderInfo.Id, chats));
                        }
                    }
                }
            }
            else if (response is Error error)
            {
                navigation.ShowPopup(Strings.FolderLinkExpiredAlert, Strings.AppName, Strings.OK);
            }
        }

        public static bool IsValidUsername(string username)
        {
            if (username.Length <= 2)
            {
                return false;
            }
            if (username.Length > 32)
            {
                return false;
            }
            if (username[0] != '@')
            {
                return false;
            }
            for (int i = 1; i < username.Length; i++)
            {
                if (!IsValidUsernameSymbol(username[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsValidCommandSymbol(char symbol)
        {
            return (symbol >= 'a' && symbol <= 'z') || (symbol >= 'A' && symbol <= 'Z') || (symbol >= '0' && symbol <= '9') || symbol == '_';
        }

        public static bool IsValidUsernameSymbol(char symbol)
        {
            return (symbol >= 'a' && symbol <= 'z') || (symbol >= 'A' && symbol <= 'Z') || (symbol >= '0' && symbol <= '9') || symbol == '_';
        }

        public static async void OpenUrl(IClientService clientService, INavigationService navigationService, string url, bool untrust = false, OpenUrlSource source = null)
        {
            if (TryCreateUri(url, out Uri uri))
            {
                var telegramUrl = IsTelegramUrl(uri);
                if (telegramUrl && clientService != null && navigationService != null)
                {
                    OpenTelegramUrl(clientService, navigationService, uri, source);
                }
                else if (clientService != null && navigationService != null && TonSite.TryCreate(clientService, uri, out string magic))
                {
                    if (navigationService is TLNavigationService tl)
                    {
                        tl.NavigateToWeb3(magic);
                    }
                }
                else
                {
                    if (untrust)
                    {
                        var confirm = await navigationService.ShowPopupAsync(string.Format(Strings.OpenUrlAlert, string.Format("[{0}]({0})", url)), Strings.OpenUrlTitle, Strings.Open, Strings.Cancel);
                        if (confirm != ContentDialogResult.Primary)
                        {
                            return;
                        }
                    }

                    try
                    {
                        var options = new LauncherOptions
                        {
                            IgnoreAppUriHandlers = telegramUrl
                        };

                        await Launcher.LaunchUriAsync(uri, options);
                    }
                    catch { }
                }
            }
        }

        #region Entity

        public static void Hyperlink_ContextRequested(ITranslateService service, UIElement sender, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point point))
            {
                var flyout = new MenuFlyout();

                if (sender is RichTextBlock text)
                {
                    Hyperlink_ContextRequested(flyout, service, text, point);
                }

                if (flyout.Items.Count > 0)
                {
                    // We don't want to unfocus the text are when the context menu gets opened
                    flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });
                    args.Handled = true;
                }
                else
                {
                    args.Handled = false;
                }
            }
            else
            {
                args.Handled = false;
            }
        }

        public static void Hyperlink_ContextRequested(ITranslateService service, Hyperlink sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            Hyperlink_ContextRequested(flyout, service, sender);

            if (flyout.Items.Count > 0)
            {
                // We don't want to unfocus the text are when the context menu gets opened
                flyout.ShowAt(sender.ElementStart.VisualParent as FrameworkElement);
                args.Handled = true;
            }
            else
            {
                args.Handled = false;
            }
        }

        public static void Hyperlink_ContextRequested(ITranslateService service, UIElement sender, string text, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point point))
            {
                var flyout = new MenuFlyout();

                Hyperlink_ContextRequested(sender.XamlRoot, flyout, service, text, point);

                if (flyout.Items.Count > 0)
                {
                    // We don't want to unfocus the text are when the context menu gets opened
                    flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });
                    args.Handled = true;
                }
                else
                {
                    args.Handled = false;
                }
            }
            else
            {
                args.Handled = false;
            }
        }

        private static void Hyperlink_ContextRequested(XamlRoot xamlRoot, MenuFlyout flyout, ITranslateService service, string text, Point point)
        {
            if (point.X < 0 || point.Y < 0)
            {
                point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
            }

            var length = text.Length;
            if (length > 0)
            {
                flyout.CreateFlyoutItem(() => LinkCopy_Click(xamlRoot, text), Strings.Copy, Icons.Copy);

                if (service != null && service.CanTranslateText(text))
                {
                    var translate = flyout.CreateFlyoutItem(null as Action, Strings.TranslateMessage, Icons.Translate);

                    async void handler(object sender, RoutedEventArgs e)
                    {
                        translate.Click -= handler;

                        var language = LanguageIdentification.IdentifyLanguage(text);
                        var popup = new TranslatePopup(service, text, language, SettingsService.Current.Translate.To, true);
                        await popup.ShowQueuedAsync(translate.XamlRoot);
                    }

                    translate.Click += handler;
                    translate.IsEnabled = true;
                }
            }
        }

        public static void Hyperlink_ContextRequested(MenuFlyout flyout, ITranslateService service, RichTextBlock text, Point point)
        {
            if (point.X < 0 || point.Y < 0)
            {
                point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
            }

            if (text.SelectedText.Length > 0)
            {
                Hyperlink_ContextRequested(text.XamlRoot, flyout, service, text.SelectedText, point);
            }
            else
            {
                var hyperlink = text.GetHyperlinkFromPoint(point);
                if (hyperlink != null)
                {
                    Hyperlink_ContextRequested(flyout, service, hyperlink);
                }
            }
        }

        public static void Hyperlink_ContextRequested(MenuFlyout flyout, ITranslateService service, Hyperlink hyperlink)
        {
            var info = GetHyperlinkInfo(hyperlink);
            if (info == null)
            {
                return;
            }

            if (info.Type is null or TextEntityTypeUrl or TextEntityTypeTextUrl)
            {
                var action = GetEntityAction(hyperlink);
                if (action != null)
                {
                    flyout.CreateFlyoutItem(action, Strings.Open, Icons.OpenIn);
                }
                else
                {
                    flyout.CreateFlyoutItem(() => LinkOpen_Click(hyperlink.XamlRoot, info.Text), Strings.Open, Icons.OpenIn);
                }

                flyout.CreateFlyoutItem(() => LinkCopy_Click(hyperlink.XamlRoot, info.Text), Strings.CopyLink, Icons.Copy);
            }
            else if (info.Type is TextEntityTypePhoneNumber)
            {
                flyout.CreateFlyoutItem(() => TextCopy_Click(hyperlink.XamlRoot, info.Text), Strings.CopyNumber, Icons.Copy);
                flyout.CreateFlyoutSeparator();

                CreateProfileFlyoutItem(flyout, service.ClientService, hyperlink, new SearchUserByPhoneNumber(info.Text, false));
            }
            else if (info.Type is TextEntityTypeMention)
            {
                flyout.CreateFlyoutItem(() => TextCopy_Click(hyperlink.XamlRoot, info.Text), Strings.CopyUsername, Icons.Copy);
                flyout.CreateFlyoutSeparator();

                CreateProfileFlyoutItem(flyout, service.ClientService, hyperlink, new SearchPublicChat(info.Text));
            }
            else
            {
                var text = info.Type switch
                {
                    TextEntityTypeHashtag or TextEntityTypeCashtag => Strings.CopyHashtag,
                    TextEntityTypeEmailAddress => Strings.CopyMail,
                    _ => Strings.Copy
                };

                flyout.CreateFlyoutItem(() => TextCopy_Click(hyperlink.XamlRoot, info.Text), text, Icons.Copy);
            }
        }

        private static async void CreateProfileFlyoutItem(MenuFlyout flyout, IClientService clientService, Hyperlink hyperlink, Function function)
        {
            var profile = new ProfileCell();
            var button = new Button
            {
                Content = profile,
                Style = BootStrapper.Current.Resources["ListEmptyButtonStyle"] as Style,
                CornerRadius = new CornerRadius(4),
                IsEnabled = false
            };

            var content = new MenuFlyoutContent
            {
                Content = button,
                Height = 48,
                Width = 200,
                Padding = new Thickness(0)
            };

            void handler(object sender, RoutedEventArgs e)
            {
                profile.Loaded -= handler;
                profile.ShowHideSkeleton(true);
            }

            profile.Loaded += handler;

            flyout.Items.Add(content);

            var response = await clientService.SendAsync(function);
            if (response is User user)
            {
                button.IsEnabled = true;
                button.Click += (s, args) =>
                {
                    flyout.Hide();
                    WindowContext.GetNavigationService(hyperlink.XamlRoot).NavigateToUser(user.Id);
                };

                profile.Loaded -= handler;
                profile.ShowHideSkeleton(false);
                profile.UpdateUser(clientService, user, 36, true);
                profile.Subtitle = Strings.ViewProfile;
            }
            if (response is Chat chat)
            {
                button.IsEnabled = true;
                button.Click += (s, args) =>
                {
                    flyout.Hide();
                    WindowContext.GetNavigationService(hyperlink.XamlRoot).Navigate(typeof(ProfilePage), chat.Id);
                };

                profile.Loaded -= handler;
                profile.ShowHideSkeleton(false);
                profile.UpdateChat(clientService, chat, 36);
                profile.Subtitle = Strings.ViewProfile;
            }
            else
            {
                button.Content = new TextBlock
                {
                    Text = function is SearchPublicChat
                        ? Strings.UsernameNotOnTelegram
                        : Strings.NumberNotOnTelegram,
                    TextWrapping = TextWrapping.Wrap,
                    Style = BootStrapper.Current.Resources["InfoCaptionTextBlockStyle"] as Style,
                    Margin = new Thickness(12, 0, 12, 0)
                };
                button.HorizontalContentAlignment = HorizontalAlignment.Center;
                button.VerticalContentAlignment = VerticalAlignment.Center;
            }
        }

        public static void Hyperlink_ContextRequested(UIElement sender, string link, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                var flyout = new MenuFlyout();
                flyout.CreateFlyoutItem(() => LinkOpen_Click(sender.XamlRoot, link), Strings.Open, Icons.OpenIn);
                flyout.CreateFlyoutItem(() => LinkCopy_Click(sender.XamlRoot, link), Strings.Copy, Icons.Copy);

                // We don't want to unfocus the text are when the context menu gets opened
                flyout.ShowAt(sender, new FlyoutShowOptions { Position = point, ShowMode = FlyoutShowMode.Transient });

                args.Handled = true;
            }
        }

        private static async void LinkOpen_Click(XamlRoot xamlRoot, string link)
        {
            if (TryCreateUri(link, out Uri uri))
            {
                try
                {
                    await Launcher.LaunchUriAsync(uri);
                }
                catch
                {
                    Logger.Error();
                }
            }
        }

        private static void LinkCopy_Click(XamlRoot xamlRoot, string link)
        {
            CopyLink(xamlRoot, link);
        }

        private static void TextCopy_Click(XamlRoot xamlRoot, string link)
        {
            CopyText(xamlRoot, link);
        }



        public static Action GetEntityAction(DependencyObject obj)
        {
            return (Action)obj.GetValue(EntityActionProperty);
        }

        public static void SetEntityAction(DependencyObject obj, Action value)
        {
            obj.SetValue(EntityActionProperty, value);
        }

        public static readonly DependencyProperty EntityActionProperty =
            DependencyProperty.RegisterAttached("EntityAction", typeof(Action), typeof(MessageHelper), new PropertyMetadata(null));





        public static TextEntityClickEventArgs GetHyperlinkInfo(DependencyObject obj)
        {
            return (TextEntityClickEventArgs)obj.GetValue(HyperlinkInfoProperty);
        }

        public static void SetHyperlinkInfo(DependencyObject obj, TextEntityClickEventArgs value)
        {
            obj.SetValue(HyperlinkInfoProperty, value);
        }

        public static readonly DependencyProperty HyperlinkInfoProperty =
            DependencyProperty.RegisterAttached("HyperlinkInfo", typeof(TextEntityClickEventArgs), typeof(MessageHelper), new PropertyMetadata(null));

        #endregion
    }

    public enum MessageCommandType
    {
        Invoke,
        Mention,
        Hashtag
    }
}
