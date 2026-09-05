//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Telegram.Views;
using Telegram.Views.Folders;
using Telegram.Views.Settings;
using Telegram.Views.Settings.Privacy;

namespace Telegram.Services
{
    public interface ISettingsSearchService
    {
        IEnumerable<SettingsSearchEntry> Search(string query);
    }

    public partial class SettingsSearchService : ISettingsSearchService
    {
        private readonly IClientService _clientService;
        private List<SettingsSearchEntry> _searchIndex;

        public SettingsSearchService(IClientService clientService)
        {
            _clientService = clientService;
        }

        public IEnumerable<SettingsSearchEntry> Search(string query)
        {
            if (_searchIndex == null)
            {
                BuildSearchIndex();
            }

            var results = new List<SettingsSearchEntry>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            foreach (var item in _searchIndex)
            {
                var first = true;
                results.AddRange(Search(query, item, ref first));
            }

            return results;
        }

        private IEnumerable<SettingsSearchEntry> Search(string query, SettingsSearchEntry entry, ref bool first)
        {
            var results = new List<SettingsSearchEntry>();

            var sane = "\\b" + Regex.Escape(query);
            //var sane = "\\b" + query.Replace(' ', '.').Replace("\\", "\\\\");
            if (entry.IsValid && Regex.IsMatch(entry.Text, sane, RegexOptions.IgnoreCase))
            {
                var clone = entry.Clone();
                if (first)
                {
                    first = false;
                }
                else
                {
                    clone.Icon = null;
                }

                results.Add(clone);
            }

            if (entry is SettingsSearchPage page && page.Items != null)
            {
                foreach (var item in page.Items)
                {
                    results.AddRange(Search(query, item, ref first));
                }
            }

            return results;
        }

        private async void BuildSearchIndex()
        {
            _searchIndex = new List<SettingsSearchEntry>
            {
                BuildProfile(),
                BuildAppearance(),
                BuildPrivacyAndSecurity(),
                BuildNotificationsAndSounds(),
                BuildDataAndStorage(),
                BuildPowerSaving(),
                BuildFolders(),
                BuildSessions(),
                BuildLanguage(),
                BuildAdvanced()
            };

            // FAQ indexing is done asyncronously
            var response = await _clientService.SendAsync(new GetWebPageInstantView(Strings.TelegramFaqUrl, false));
            if (response is WebPageInstantView linkPreview)
            {
                var title = string.Empty;
                var cicci = new List<SettingsSearchEntry>();

                foreach (var block in linkPreview.Blocks)
                {
                    if (block is PageBlockList list)
                    {
                        var items = new List<SettingsSearchEntry>();

                        foreach (var item in list.Items)
                        {
                            if (item.Blocks.Count == 1 && item.Blocks[0] is PageBlockParagraph paragraph && paragraph.Text is RichTextAnchorLink anchorLink)
                            {
                                items.Add(new SettingsSearchFaq(anchorLink.Url, anchorLink.ToPlainText()));
                            }
                        }

                        if (!string.IsNullOrEmpty(title) && items.Count > 0)
                        {
                            cicci.Add(new SettingsSearchPage(null, title, new Assets.Icons.FAQ(), items.ToArray()));
                        }
                    }
                    else if (block is PageBlockTitle blockTitle)
                    {
                        title = blockTitle.Title.ToPlainText();
                    }
                    else if (block is PageBlockAnchor)
                    {
                        break;
                    }
                }

                _searchIndex.Add(new SettingsSearchPage(typeof(InstantPage), Strings.SettingsSearchFaq, new Assets.Icons.FAQ(), cicci.ToArray()));
            }
        }

        private SettingsSearchEntry BuildProfile()
        {
            return new SettingsSearchPage(typeof(SettingsProfilePage), Strings.AccountSettings, new Assets.Icons.Profile(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsProfilePage), Strings.FirstNameSmall),
                new SettingsSearchPage(typeof(SettingsProfilePage), Strings.LastNameSmall),
                new SettingsSearchPage(typeof(SettingsProfilePage), Strings.ChatSetNewPhoto),
                new SettingsSearchPage(typeof(SettingsProfilePage), Strings.EditProfileBio),
                new SettingsSearchPage(typeof(SettingsProfilePage), Strings.EditAccountInfoHeader, new Assets.Icons.Profile(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsProfilePage), Strings.PhoneNumberChange2),
                    new SettingsSearchPage(typeof(SettingsProfilePage), Strings.Username),
                    new SettingsSearchPage(typeof(SettingsProfileColorPage), Strings.YourNameColor),
                    new SettingsSearchPage(typeof(SettingsProfilePage), Strings.EditProfileBirthdayText),
                    new SettingsSearchPage(typeof(SettingsProfilePage), Strings.EditProfileBirthdayRemove)
                }),
                new SettingsSearchPage(typeof(SettingsProfilePage), Strings.EditProfileChannelTitle),
                new SettingsSearchPage(typeof(SettingsProfilePage), Strings.EditProfileHours),
                new SettingsSearchPage(typeof(SettingsProfilePage), Strings.EditProfileLocation),
                new SettingsSearchPage(typeof(SettingsProfilePage), Strings.EditProfileChatAutomation),
                new SettingsSearchPage(typeof(SettingsProfilePage), Strings.LogOutTitle)
            });
        }

        private SettingsSearchEntry BuildNotificationsAndSounds()
        {
            return new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.NotificationsAndSounds, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.ShowNotificationsFor, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.AllAccounts)
                }),
                new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.NotificationPreviewName),
                new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.NotificationPreviewText),
                new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.NotificationPreviewReply),
                new SettingsSearchPage(typeof(SettingsNotificationsExceptionsPage), SettingsNotificationsExceptionsScope.PrivateChats, Strings.NotificationsPrivateChats),
                new SettingsSearchPage(typeof(SettingsNotificationsExceptionsPage), SettingsNotificationsExceptionsScope.GroupChats, Strings.NotificationsGroups),
                new SettingsSearchPage(typeof(SettingsNotificationsExceptionsPage), SettingsNotificationsExceptionsScope.ChannelChats, Strings.NotificationsChannels),
                new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.InAppNotifications, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.InAppSounds),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.InAppPreview)
                }),
                new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Events, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.ContactJoined),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.PinnedMessages)
                }),
                new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.BadgeNumber, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.BadgeNumberMutedChats),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.BadgeNumberMutedChatsFolders),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.BadgeNumberUnread)
                }),
                new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.ResetAllNotifications)
            });
        }

        private SettingsSearchEntry BuildPrivacyAndSecurity()
        {
            return new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.PrivacySettings, new Assets.Icons.Privacy(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsBlockedChatsPage), Strings.BlockedUsers),
                new SettingsSearchPage(typeof(SettingsPasscodePage), Strings.Passcode),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.Passkey),
                new SettingsSearchPage(typeof(SettingsPasswordPage), Strings.TwoStepVerification),
                new SettingsSearchPage(typeof(SettingsAutoDeletePage), Strings.AutoDeleteMessages),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.EmailLogin),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.PrivacyTitle, new Assets.Icons.Privacy(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsPrivacyPhonePage), Strings.PrivacyPhone),
                    new SettingsSearchPage(typeof(SettingsPrivacyShowStatusPage), Strings.PrivacyLastSeen),
                    new SettingsSearchPage(typeof(SettingsPrivacyShowPhotoPage), Strings.PrivacyProfilePhoto),
                    new SettingsSearchPage(typeof(SettingsPrivacyShowBioPage), Strings.PrivacyBio),
                    new SettingsSearchPage(typeof(SettingsPrivacyAutosaveGiftsPage), Strings.PrivacyGifts),
                    new SettingsSearchPage(typeof(SettingsPrivacyShowBirthdatePage), Strings.PrivacyBirthday),
                    new SettingsSearchPage(typeof(SettingsPrivacyShowProfileAudioPage), Strings.PrivacyMusic),
                    new SettingsSearchPage(typeof(SettingsPrivacyShowForwardedPage), Strings.PrivacyForwards),
                    new SettingsSearchPage(typeof(SettingsPrivacyAllowCallsPage), Strings.Calls, new Assets.Icons.Privacy(), new SettingsSearchEntry[]
                    {
                        new SettingsSearchPage(typeof(SettingsPrivacyAllowP2PCallsPage), Strings.PrivacyP2P)
                    }),
                    new SettingsSearchPage(typeof(SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage), Strings.PrivacyVoiceMessages),
                    new SettingsSearchPage(typeof(SettingsPrivacyNewChatPage), Strings.PrivacyMessages),
                    new SettingsSearchPage(typeof(SettingsPrivacyAllowChatInvitesPage), Strings.PrivacyInvites)
                }),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.ArchiveSettings),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.DeleteMyAccount, new Assets.Icons.Privacy(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.DeleteAccountIfAwayFor3)
                }),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.PrivacyBots, new Assets.Icons.Privacy(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.PrivacyPaymentsClear),
                    new SettingsSearchPage(typeof(SettingsWebSessionsPage), Strings.WebSessionsTitle)
                }),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.Contacts, new Assets.Icons.Privacy(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.SuggestContacts)
                }),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.ShowSensitiveContent),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.SecretChat, new Assets.Icons.Privacy(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.SecretWebPage)
                })
            });
        }

        private SettingsSearchEntry BuildDataAndStorage()
        {
            return new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.DataSettings, new Assets.Icons.Data(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.DataUsage, new Assets.Icons.Data(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsStoragePage), Strings.StorageUsage, new Assets.Icons.Data(), new SettingsSearchEntry[]
                    {
                        new SettingsSearchPage(typeof(SettingsStoragePage), Strings.KeepMedia)
                    }),
                    new SettingsSearchPage(typeof(SettingsNetworkPage), Strings.NetworkUsage)
                }),
                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.AutomaticMediaDownload, new Assets.Icons.Data(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.AutoDownloadMedia),
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.AutoDownloadPhotos),
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.AutoDownloadVideos),
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.AutoDownloadFiles),
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.ResetAutomaticMediaDownload)
                }),
                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.DownloadPath, new Assets.Icons.Data(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.TemporaryFolder),
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.DownloadFolder)
                }),
                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.Streaming, new Assets.Icons.Data(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.EnableStreaming)
                }),
                new SettingsSearchPage(typeof(SettingsProxyPage), Strings.ProxySettings, new Assets.Icons.Data(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsProxyPage), Strings.AddProxy)
                }),
                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.PrivacyDeleteCloudDrafts)
            });
        }

        private SettingsSearchEntry BuildPowerSaving()
        {
            return new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.PowerUsage, new Assets.Icons.PowerSaving(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LitePowerSaver),
                new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LiteOptionsTitle, new Assets.Icons.PowerSaving(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LiteOptionsStickers),
                    new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LiteOptionsAutoplayKeyboard),
                    new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LiteOptionsAutoplayChat),
                    new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LiteOptionsEmoji),
                    new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LiteOptionsAutoplayVideo),
                    new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LiteOptionsAutoplayGifs),
                    new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LiteOptionsCalls),
                    new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LiteOptionsTransparencyEffects),
                    new SettingsSearchPage(typeof(SettingsPowerSavingPage), Strings.LiteOptionsAnimationEffects)
                })
            });
        }

        private SettingsSearchEntry BuildAppearance()
        {
            return new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.Appearance, new Assets.Icons.Appearance(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsThemesPage), Strings.ChatThemes),
                new SettingsSearchPage(typeof(SettingsBackgroundsPage), Strings.ChatWallpaper),
                new SettingsSearchPage(typeof(SettingsProfileColorPage), Strings.YourNameColor),
                new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.SettingsSwitchToNightMode),
                new SettingsSearchPage(typeof(SettingsNightModePage), Strings.AutoNightTheme),
                new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.InterfaceScale),
                new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.BubbleRadius),
                new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.TextSizeHeader),
                new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.FontFamily),
                new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Installed, Strings.StickersName, new Assets.Icons.Appearance(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Trending, Strings.FeaturedStickers),
                    new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Archived, Strings.ArchivedStickers),
                    new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Emoji, Strings.Emoji),
                    new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.EmojiArchived, Strings.ArchivedEmojiPacks),
                    new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Installed, Strings.SuggestStickers),
                    new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Installed, Strings.LargeEmoji),
                    new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Installed, Strings.DynamicPackOrder)
                }),
                new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.ChatQuickActions, new Assets.Icons.Appearance(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.SwipeShare),
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.SwipeReply),
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.SwipeGoBack)
                }),
                new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.ChatDoubleClickAction, new Assets.Icons.Appearance(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.DoubleClickReply),
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.DoubleClickReact)
                }),
                new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.OtherSettings, new Assets.Icons.Appearance(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.EnableFullScreenGallery),
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.UseSystemSpellChecker),
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.ReplaceEmoji),
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.AdaptiveLayout),
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.SendByEnter2),
                    new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.DistanceUnits)
                })
            });
        }

        private SettingsSearchEntry BuildFolders()
        {
            return new SettingsSearchPage(typeof(FoldersPage), Strings.Filters, new Assets.Icons.Folders(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(FoldersPage), Strings.CreateNewFilter),
                new SettingsSearchPage(typeof(FoldersPage), Strings.FolderShowTags),
                new SettingsSearchPage(typeof(FoldersPage), Strings.TabsView, new Assets.Icons.Folders(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(FoldersPage), Strings.TabsViewOnTop),
                    new SettingsSearchPage(typeof(FoldersPage), Strings.TabsViewOnLeft)
                })
            });
        }

        private SettingsSearchEntry BuildSessions()
        {
            return new SettingsSearchPage(typeof(SettingsSessionsPage), Strings.Devices, new Assets.Icons.Devices(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsSessionsPage), Strings.CurrentSession),
                new SettingsSearchPage(typeof(SettingsSessionsPage), Strings.Rename),
                new SettingsSearchPage(typeof(SettingsSessionsPage), Strings.TerminateAllSessions),
                new SettingsSearchPage(typeof(SettingsSessionsPage), Strings.TerminateOldSessionHeader),
                new SettingsSearchPage(typeof(SettingsSessionsPage), Strings.IfInactiveFor)
            });
        }

        private SettingsSearchEntry BuildLanguage()
        {
            return new SettingsSearchPage(typeof(SettingsLanguagePage), Strings.Language, new Assets.Icons.Language(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsLanguagePage), Strings.TranslateMessages, new Assets.Icons.Language(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsLanguagePage), Strings.ShowTranslateButton),
                    new SettingsSearchPage(typeof(SettingsLanguagePage), Strings.ShowTranslateChatButton),
                    new SettingsSearchPage(typeof(SettingsLanguagePage), Strings.DoNotTranslate)
                })
            });
        }

        private SettingsSearchEntry BuildAdvanced()
        {
            return new SettingsSearchPage(typeof(SettingsAdvancedPage), Strings.PrivacyAdvanced, new Assets.Icons.Advanced(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsAdvancedPage), Strings.SystemIntegration, new Assets.Icons.Advanced(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsAdvancedPage), Strings.ShowTrayIcon),
                    new SettingsSearchPage(typeof(SettingsAdvancedPage), Strings.AutoStart),
                    new SettingsSearchPage(typeof(SettingsAdvancedPage), Strings.AutoStartMinized)
                }),
                new SettingsSearchPage(typeof(SettingsAdvancedPage), Strings.VersionAndUpdates),
                new SettingsSearchPage(typeof(SettingsAdvancedPage), Strings.InstallBetaUpdates),
                new SettingsSearchPage(typeof(SettingsAdvancedPage), Strings.ExperimentalSettings),
                new SettingsSearchPage(typeof(SettingsAdvancedPage), Strings.DiagnosticsShowPeerIds)
            });
        }
    }

    public partial class SettingsSearchPage : SettingsSearchEntry
    {
        public SettingsSearchPage(Type page, string text, IAnimatedVisualSource2 icon = null, SettingsSearchEntry[] items = null)
            : base(text, icon)
        {
            Page = page;
            Items = items;

            if (items != null)
            {
                foreach (var item in items)
                {
                    item.Parent = this;
                    item.Icon ??= icon;
                }
            }
        }

        public SettingsSearchPage(Type page, object parameter, string text, IAnimatedVisualSource2 icon = null, SettingsSearchEntry[] items = null)
            : base(text, icon)
        {
            Page = page;
            Parameter = parameter;
            Items = items;

            if (items != null)
            {
                foreach (var item in items)
                {
                    item.Parent = this;
                    item.Icon ??= icon;
                }
            }
        }

        public SettingsSearchPage(Type page, string text)
            : base(text, null)
        {
            Page = page;
        }

        public SettingsSearchPage(Type page, object parameter, string text)
            : base(text, null)
        {
            Page = page;
            Parameter = parameter;
        }

        public Type Page { get; set; }
        public object Parameter { get; set; }
        public SettingsSearchEntry[] Items { get; set; }

        public override SettingsSearchEntry Clone()
        {
            return new SettingsSearchPage(Page, Parameter, Text, Icon) { Parent = Parent };
        }

        public override bool IsValid => Page != null;
    }

    public partial class SettingsSearchAction : SettingsSearchEntry
    {
        public SettingsSearchAction(Action action, string text)
            : base(text, null)
        {
            Action = action;
        }

        public Action Action { get; set; }

        public override SettingsSearchEntry Clone()
        {
            return new SettingsSearchAction(Action, Text) { Icon = Icon, Parent = Parent };
        }

        public override bool IsValid => true;
    }

    public partial class SettingsSearchFaq : SettingsSearchEntry
    {
        public SettingsSearchFaq(string url, string text, IAnimatedVisualSource2 icon = null)
            : base(text, icon)
        {
            Url = url;
        }

        public string Url { get; set; }

        public override SettingsSearchEntry Clone()
        {
            return new SettingsSearchFaq(Url, Text, Icon) { Parent = Parent };
        }

        public override bool IsValid => true;
    }

    public abstract class SettingsSearchEntry
    {
        public SettingsSearchEntry(string text, IAnimatedVisualSource2 icon)
        {
            Text = text;
            Icon = icon;
        }

        public string Text { get; set; }
        public IAnimatedVisualSource2 Icon { get; set; }

        public SettingsSearchEntry Parent { get; set; }

        public abstract SettingsSearchEntry Clone();

        public abstract bool IsValid { get; }
    }
}
