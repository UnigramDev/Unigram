using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Unigram.Views;
using Unigram.Views.Folders;
using Unigram.Views.Settings;
using Unigram.Views.Settings.Privacy;

namespace Unigram.Services
{
    public interface ISettingsSearchService
    {
        IEnumerable<SettingsSearchEntry> Search(string query);
    }

    public class SettingsSearchService : ISettingsSearchService
    {
        private readonly IProtoService _protoService;
        private List<SettingsSearchEntry> _searchIndex;

        public SettingsSearchService(IProtoService protoService)
        {
            _protoService = protoService;
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
                    clone.Glyph = null;
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
                new SettingsSearchPage(null, Strings.Resources.Language, Icons.Globe),
                new SettingsSearchPage(null, Strings.Resources.AskAQuestion, Icons.QuestionCircle),
                new SettingsSearchPage(typeof(FoldersPage), Strings.Resources.Filters, Icons.FolderOpen)
            };

            // FAQ indexing is done asyncronously
            var response = await _protoService.SendAsync(new GetWebPageInstantView(Strings.Resources.TelegramFaqUrl, true));
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
                            if (item.PageBlocks.Count == 1 && item.PageBlocks[0] is PageBlockParagraph paragraph && paragraph.Text is RichTextUrl url)
                            {
                                items.Add(new SettingsSearchFaq(url.Url, url.ToPlainText()));
                            }
                        }

                        if (!string.IsNullOrEmpty(title) && items.Count > 0)
                        {
                            cicci.Add(new SettingsSearchPage(null, title, Icons.ChatBubblesQuestion, items.ToArray()));
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

                _searchIndex.Add(new SettingsSearchPage(typeof(InstantPage), Strings.Resources.SettingsSearchFaq, Icons.ChatBubblesQuestion, cicci.ToArray()));
            }
        }

        private SettingsSearchEntry BuildNotificationsAndSounds()
        {
            return new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.NotificationsAndSounds, Icons.Channel, new SettingsSearchEntry[]
            {
                // Notifications for private chats
                new SettingsSearchPage(null, Strings.Resources.NotificationsForPrivateChats, Icons.Channel, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.MessagePreview),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.Sound)
                }),

                // Notifications for groups
                new SettingsSearchPage(null, Strings.Resources.NotificationsForGroups, Icons.Channel, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.MessagePreview),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.Sound)
                }),

                // Notifications for channels
                new SettingsSearchPage(null, Strings.Resources.NotificationsForChannels, Icons.Channel, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.MessagePreview),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.Sound)
                }),

                // In-app notifications
                new SettingsSearchPage(null, Strings.Resources.InAppNotifications, Icons.Channel, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.InAppSounds),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.InAppVibrate),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.InAppPreview)
                }),

                // Events
                new SettingsSearchPage(null, Strings.Resources.Events, Icons.Channel, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.ContactJoined),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.PinnedMessages)
                }),

                // Badge Counter
                new SettingsSearchPage(null, Strings.Resources.BadgeNumber, Icons.Channel, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.BadgeNumberShow),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.BadgeNumberMutedChats),
                    new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.BadgeNumberUnread)
                }),

                // Reset All Notifications
                new SettingsSearchPage(typeof(SettingsNotificationsPage), Strings.Resources.BadgeNumberUnread)
            });
        }

        private SettingsSearchEntry BuildPrivacyAndSecurity()
        {
            return new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.Resources.PrivacySettings, Icons.Lock, new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsBlockedChatsPage), Strings.Resources.BlockedUsers),
                new SettingsSearchPage(typeof(SettingsPrivacyShowStatusPage), Strings.Resources.PrivacyLastSeen),
                //yield return new SettingsSearchEntry(typeof(SettingsPrivacyAndSecurityPage), Strings.Resources.ProfilePhoto, group);
                //yield return new SettingsSearchEntry(typeof(SettingsPrivacyAndSecurityPage), Strings.Resources.Forwards, group);
                new SettingsSearchPage(typeof(SettingsPrivacyAllowCallsPage), Strings.Resources.Calls),
                new SettingsSearchPage(typeof(SettingsPrivacyAllowP2PCallsPage), Strings.Resources.PrivacyP2P),
                new SettingsSearchPage(typeof(SettingsPrivacyAllowChatInvitesPage), Strings.Resources.GroupsAndChannels),

                new SettingsSearchPage(typeof(SettingsPasscodePage), Strings.Resources.Passcode),
                new SettingsSearchPage(typeof(SettingsPasswordPage), Strings.Resources.TwoStepVerification),
                new SettingsSearchPage(typeof(SettingsSessionsPage), Strings.Resources.SessionsTitle),

                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.Resources.PrivacyDeleteCloudDrafts),
                new SettingsSearchPage(typeof(SettingsPrivacyAndSecurityPage), Strings.Resources.DeleteAccountIfAwayFor2)
            });
        }

        private SettingsSearchEntry BuildDataAndStorage()
        {
            return new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.Resources.DataSettings, Icons.DataPie, new SettingsSearchEntry[]
            {
                // Storage Usage
                new SettingsSearchPage(typeof(SettingsStoragePage), Strings.Resources.StorageUsage, Icons.DataPie, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsStoragePage), Strings.Resources.KeepMedia)
                }),

                // Data Usage
                new SettingsSearchPage(typeof(SettingsNetworkPage), Strings.Resources.NetworkUsage, Icons.DataPie, new SettingsSearchEntry[]
                {

                }),

                // TODO: new autodownload settings

                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.Resources.ResetAutomaticMediaDownload),

                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.Resources.AutoplayMedia, Icons.DataPie, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.Resources.AutoplayGifs),
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.Resources.AutoplayVideo)
                }),

                // Calls
                new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.Resources.Calls, Icons.DataPie, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsDataAndStoragePage), Strings.Resources.VoipUseLessData)
                }),

                // Proxy
                new SettingsSearchPage(typeof(SettingsProxiesPage), Strings.Resources.Proxy, Icons.DataPie, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsProxiesPage), Strings.Resources.AddProxy)
                })
            });
        }

        private SettingsSearchEntry BuildStickersAndMasks()
        {
            return new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Installed, Strings.Resources.StickersAndMasks, Icons.Sticker, new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Installed, Strings.Resources.SuggestStickers),
                new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Trending, Strings.Resources.FeaturedStickers),

                // Masks
                new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Masks, Strings.Resources.Masks, Icons.Sticker, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.MasksArchived, Strings.Resources.ArchivedMasks)
                }),

                new SettingsSearchPage(typeof(SettingsStickersPage), (int)StickersType.Archived, Strings.Resources.ArchivedStickers)
            });
        }

        private SettingsSearchEntry BuildAppearance()
        {
            return new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.Resources.Appearance, Icons.Color, new SettingsSearchEntry[]
            {
                new SettingsSearchPage(typeof(SettingsAppearancePage), Strings.Resources.TextSizeHeader),

                new SettingsSearchPage(typeof(SettingsBackgroundsPage), Strings.Resources.ChatBackground, Icons.Color, new SettingsSearchEntry[]
                {
                    new SettingsSearchPage(typeof(SettingsBackgroundsPage), Strings.Resources.SelectFromGallery),
                    new SettingsSearchPage(typeof(SettingsBackgroundsPage), Strings.Resources.SetColor)
                }),

                new SettingsSearchPage(typeof(SettingsNightModePage), Strings.Resources.AutoNightTheme)
            });
        }
    }

    public class SettingsSearchPage : SettingsSearchEntry
    {
        public SettingsSearchPage(Type page, string text, string glyph = null, SettingsSearchEntry[] items = null)
            : base(text, glyph)
        {
            Page = page;
            Items = items;

            if (items != null)
            {
                foreach (var item in items)
                {
                    item.Parent = this;

                    if (item.Glyph == null)
                    {
                        item.Glyph = glyph;
                    }
                }
            }
        }

        public SettingsSearchPage(Type page, object parameter, string text, string glyph = null, SettingsSearchEntry[] items = null)
            : base(text, glyph)
        {
            Page = page;
            Items = items;

            if (items != null)
            {
                foreach (var item in items)
                {
                    item.Parent = this;

                    if (item.Glyph == null)
                    {
                        item.Glyph = glyph;
                    }
                }
            }
        }

        public SettingsSearchPage(Type page, string text)
            : this(page, null, text)
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
            return new SettingsSearchPage(Page, Text, Glyph) { Parent = Parent };
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
            return new SettingsSearchAction(Action, Text) { Glyph = Glyph, Parent = Parent };
        }

        public override bool IsValid => true;
    }

    public class SettingsSearchFaq : SettingsSearchEntry
    {
        public SettingsSearchFaq(string url, string text, string glyph = null)
            : base(text, glyph)
        {
            Url = url;
        }

        public string Url { get; set; }

        public override SettingsSearchEntry Clone()
        {
            return new SettingsSearchFaq(Url, Text, Glyph) { Parent = Parent };
        }

        public override bool IsValid => true;
    }

    public abstract class SettingsSearchEntry
    {
        public SettingsSearchEntry(string text, string glyph)
        {
            Text = text;
            Glyph = glyph;
        }

        public string Text { get; set; }
        public string Glyph { get; set; }

        public SettingsSearchEntry Parent { get; set; }

        public abstract SettingsSearchEntry Clone();

        public abstract bool IsValid { get; }
    }
}
