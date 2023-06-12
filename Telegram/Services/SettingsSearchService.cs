//
// Copyright Fela Ameghino 2015-2023
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

    public class SettingsSearchService : ISettingsSearchService
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
                BuildNotificationsAndSounds(),
                BuildPrivacyAndSecurity(),
                BuildDataAndStorage(),
                BuildStickersAndMasks(),
                BuildAppearance(),
                new SettingsSearchPage(null, Strings.Language, new Assets.Icons.Language()),
                new SettingsSearchPage(null, Strings.AskAQuestion, new Assets.Icons.AskQ()),
                new SettingsSearchPage(typeof(FoldersPage), Strings.Filters, new Assets.Icons.Folders())
            };

            // FAQ indexing is done asyncronously
            var response = await _clientService.SendAsync(new GetWebPageInstantView(Strings.TelegramFaqUrl, true));
            if (response is WebPageInstantView webPage)
            {
                var title = string.Empty;
                var cicci = new List<SettingsSearchEntry>();

                foreach (var block in webPage.PageBlocks)
                {
                    if (block is PageBlockList list)
                    {
                        var items = new List<SettingsSearchEntry>();

                        foreach (var item in list.Items)
                        {
                            if (item.PageBlocks.Count == 1 && item.PageBlocks[0] is PageBlockParagraph paragraph && paragraph.Text is RichTextAnchorLink anchorLink)
                            {
                                items.Add(new SettingsSearchFaq(anchorLink.Url, anchorLink.ToPlainText()));
                            }
                        }

                        if (!string.IsNullOrEmpty(title) && items.Count > 0)
                        {
                            cicci.Add(new SettingsSearchPage(null, title, new Assets.Icons.FAQ(), items.ToArray()));
                        }
                    }
                    else if (block is PageBlockParagraph para)
                    {
                        title = para.Text.ToPlainText();
                    }
                    else if (block is PageBlockDivider)
                    {
                        break;
                    }
                }

                _searchIndex.Add(new SettingsSearchPage(typeof(InstantPage), Strings.SettingsSearchFaq, new Assets.Icons.FAQ(), cicci.ToArray()));
            }
        }

        private SettingsSearchEntry BuildNotificationsAndSounds()
        {
            return new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.NotificationsAndSounds, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
            {
                // Notifications for private chats
                new SettingsSearchPage(null, Strings.NotificationsForPrivateChats, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.MessagePreview),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Sound)
                }),

                // Notifications for groups
                new SettingsSearchPage(null, Strings.NotificationsForGroups, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.MessagePreview),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Sound)
                }),

                // Notifications for channels
                new SettingsSearchPage(null, Strings.NotificationsForChannels, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.MessagePreview),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Sound)
                }),

                // In-app notifications
                new SettingsSearchPage(null, Strings.InAppNotifications, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.InAppSounds),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.InAppVibrate),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.InAppPreview)
                }),

                // Events
                new SettingsSearchPage(null, Strings.Events, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.ContactJoined),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.PinnedMessages)
                }),

                // Badge Counter
                new SettingsSearchPage(null, Strings.BadgeNumber, new Assets.Icons.Notifications(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.BadgeNumberShow),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.BadgeNumberMutedChats),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.BadgeNumberUnread)
                }),

                // Reset All Notifications
                new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.BadgeNumberUnread)
            });
        }

        private SettingsSearchEntry BuildPrivacyAndSecurity()
        {
            return new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.PrivacySettings, new Assets.Icons.Privacy(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsBlockedChatsPage), Strings.BlockedUsers),
                new SettingsSearchPage(typeof(SettingsPrivacyShowStatusPage), Strings.PrivacyLastSeen),
                //yield return new SettingsSearchEntry(typeof(SettingsPrivacyAndSecurityPage), Strings.ProfilePhoto, group);
                //yield return new SettingsSearchEntry(typeof(SettingsPrivacyAndSecurityPage), Strings.Forwards, group);
                new SettingsSearchPage(typeof(SettingsPrivacyAllowCallsPage), Strings.Calls),
                new SettingsSearchPage(typeof(SettingsPrivacyAllowP2PCallsPage), Strings.PrivacyP2P),
                new SettingsSearchPage(typeof(SettingsPrivacyAllowChatInvitesPage), Strings.GroupsAndChannels),

                new SettingsSearchPage(typeof(SettingsPasscodePage), Strings.Passcode),
                new SettingsSearchPage(typeof(SettingsPasswordPage), Strings.TwoStepVerification),
                new SettingsSearchPage(typeof(SettingsSessionsPage), Strings.SessionsTitle),

                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.PrivacyDeleteCloudDrafts),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.DeleteAccountIfAwayFor2)
            });
        }

        private SettingsSearchEntry BuildDataAndStorage()
        {
            return new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.DataSettings, new Assets.Icons.Data(), new SettingsSearchEntry[]
            {
                // Storage Usage
                new SettingsSearchPage(typeof(SettingsStoragePage), Strings.StorageUsage, new Assets.Icons.Data(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsStoragePage), Strings.KeepMedia)
                }),

                // Data Usage
                new SettingsSearchPage(typeof(SettingsNetworkPage), Strings.NetworkUsage, new Assets.Icons.Data(), new SettingsSearchEntry[]
                {

                }),

                // TODO: new autodownload settings

                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.ResetAutomaticMediaDownload),

                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.AutoplayMedia, new Assets.Icons.Data(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.AutoplayGifs),
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.AutoplayVideo)
                }),

                // Calls
                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.Calls, new Assets.Icons.Data(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.VoipUseLessData)
                }),

                // Proxy
                new SettingsSearchPage(typeof(SettingsProxyPage), Strings.Proxy, new Assets.Icons.Data(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsProxyPage), Strings.AddProxy)
                })
            });
        }

        private SettingsSearchEntry BuildStickersAndMasks()
        {
            return new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Installed, Strings.StickersAndMasks, new Assets.Icons.Stickers(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Installed, Strings.SuggestStickers),
                new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Trending, Strings.FeaturedStickers),

                // Masks
                new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Masks, Strings.Masks, new Assets.Icons.Stickers(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.MasksArchived, Strings.ArchivedMasks)
                }),

                new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Archived, Strings.ArchivedStickers)
            });
        }

        private SettingsSearchEntry BuildAppearance()
        {
            return new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.Appearance, new Assets.Icons.Appearance(), new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.TextSizeHeader),

                new SettingsSearchPage(typeof(SettingsBackgroundsPage), Strings.ChatBackground, new Assets.Icons.Appearance(), new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsBackgroundsPage), Strings.SelectFromGallery),
                    new SettingsSearchPage(typeof(SettingsBackgroundsPage), Strings.SetColor)
                }),

                new SettingsSearchPage(typeof(SettingsNightModePage), Strings.AutoNightTheme)
            });
        }
    }

    public class SettingsSearchPage : SettingsSearchEntry
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

    public class SettingsSearchAction : SettingsSearchEntry
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

    public class SettingsSearchFaq : SettingsSearchEntry
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
