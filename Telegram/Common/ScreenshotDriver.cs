//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Controls.Chats;
using Telegram.Controls.Stories;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views;
using File = System.IO.File;

namespace Telegram.Common
{
    /// <summary>
    /// Puts the scenario's shots on screen one at a time for Tools\Screenshots\Screenshots.ps1,
    /// which captures the window from outside with PrintWindow: that gets what the compositor
    /// drew, swap chains included, where RenderTargetBitmap would not.
    /// </summary>
    /// <remarks>
    /// The two meet through files in <see cref="MockClient.Folder"/>. The script writes go.txt
    /// once the window is sized; for each shot the driver writes shot.txt, "index name", and
    /// waits for captured.txt to hold the same index; done.txt follows the last one.
    /// </remarks>
    public static class ScreenshotDriver
    {
        private const int LoadDelay = 2000;

        private static int _started;

        public static void Start(IReadOnlyList<MockShot> shots)
        {
            if (shots.Count > 0 && Interlocked.Exchange(ref _started, 1) == 0)
            {
                _ = Task.Run(() => RunAsync(shots));
            }
        }

        private static async Task RunAsync(IReadOnlyList<MockShot> shots)
        {
            await WaitForAsync("go.txt", null);

            for (int i = 0; i < shots.Count; i++)
            {
                var window = WindowContext.Main;
                if (window == null)
                {
                    return;
                }

                var shot = shots[i];
                await window.Dispatcher.DispatchAsync(() => Apply(window, shot));

                var delay = shot.Delay;
                if (shot.Panel != null || shot.ScrollToTop || shot.Tab != null)
                {
                    // The chat or profile Apply opened is still loading: its composer, history and
                    // tabs are not there yet.
                    await Task.Delay(LoadDelay);
                    await window.Dispatcher.DispatchAsync(() => AfterLoad(window, shot));

                    delay = Math.Max(0, delay - LoadDelay);
                }

                await Task.Delay(delay);

                var index = i.ToString(CultureInfo.InvariantCulture);
                File.WriteAllText(Path.Combine(MockClient.Folder, "shot.txt"), index + " " + shot.Name);

                await WaitForAsync("captured.txt", index);
            }

            File.WriteAllText(Path.Combine(MockClient.Folder, "done.txt"), string.Empty);
        }

        private static void Apply(WindowContext window, MockShot shot)
        {
            if (shot.Theme != null)
            {
                // Switch would otherwise toast that automatic night mode is now off, into the shot.
                if (AppSettings.Appearance.NightMode != NightMode.Disabled)
                {
                    AppSettings.Appearance.NightMode = NightMode.Disabled;
                    NightModeService.Current.UpdateTimer();
                }

                var dark = shot.Theme is "dark" or "tinted";
                if (dark)
                {
                    // The app keeps its settings between runs, so "dark" wears Night rather than
                    // whatever the last run left on.
                    AppSettings.Appearance.WearBuiltIn(shot.Theme == "tinted" ? TelegramThemeType.Tinted : TelegramThemeType.Night, false);
                }

                NightModeService.Current.Switch(dark ? TelegramTheme.Dark : TelegramTheme.Light, window.XamlRoot);
            }

            var root = window.RootNavigationService;

            if (shot.Folders != null && root?.Frame?.Content is MainPage page)
            {
                AppSettings.UseLeftTabsForChats = shot.Folders == "left";
                page.UpdateChatFoldersLayout();
            }

            if (shot.ChatId != 0)
            {
                // The root service would replace the main page with the chat; the one MainPage
                // hosts opens it beside the chat list.
                var service = (root?.Frame?.Content as INavigationHost)?.NavigationService ?? root;

                if (shot.Profile)
                {
                    service?.Navigate(typeof(ProfilePage), shot.ChatId);
                }
                else
                {
                    service?.NavigateToChat(shot.ChatId);
                }
            }

            if (shot.ExpandStories)
            {
                window.XamlRoot?.GetChild<StoriesStrip>()?.Expand();
            }
        }

        private static void AfterLoad(WindowContext window, MockShot shot)
        {
            if (shot.ScrollToTop)
            {
                window.XamlRoot?.GetChild<ChatHistoryView>()?.ScrollToTop();
            }

            if (shot.Panel != null)
            {
                OpenPanel(window, shot.Panel, shot.RecentEmoji);
            }

            if (shot.Tab != null)
            {
                SelectTab(window, shot.Tab);
            }
        }

        private static void SelectTab(WindowContext window, string tab)
        {
            var viewModel = window.XamlRoot?.GetChild<ProfilePage>()?.ViewModel;
            if (viewModel == null)
            {
                return;
            }

            foreach (var item in viewModel.Items)
            {
                var match = tab switch
                {
                    "posts" => item.Type is ProfileTabPosts,
                    "media" => item.Type is ProfileTabMedia,
                    "files" => item.Type is ProfileTabFiles,
                    "links" => item.Type is ProfileTabLinks,
                    "music" => item.Type is ProfileTabMusic,
                    _ => false
                };

                if (match)
                {
                    viewModel.SelectedItem = item;
                    return;
                }
            }
        }

        private static void OpenPanel(WindowContext window, string panel, IReadOnlyList<string> recentEmoji)
        {
            var recents = recentEmoji != null
                ? window.XamlRoot?.GetChild<ChatView>()?.ViewModel?.Settings.RecentEmoji
                : null;

            if (recents != null)
            {
                recents.ClearRecentEmoji();

                // The list is ordered by use count, so the first entry is used the most.
                for (int i = 0; i < recentEmoji.Count; i++)
                {
                    var split = recentEmoji[i].Split(';');
                    if (split.Length == 2 && long.TryParse(split[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long customEmojiId))
                    {
                        for (int j = i; j < recentEmoji.Count; j++)
                        {
                            recents.AddRecentEmoji(split[0], customEmojiId);
                        }
                    }
                }
            }

            var tab = panel switch
            {
                "stickers" => StickersTab.Stickers,
                "animations" => StickersTab.Animations,
                _ => StickersTab.Emoji
            };

            window.XamlRoot?.GetChild<ChatStickerButton>()?.Show(tab);
        }

        private static async Task WaitForAsync(string file, string expected)
        {
            var path = Path.Combine(MockClient.Folder, file);

            while (true)
            {
                try
                {
                    if (File.Exists(path) && (expected == null || File.ReadAllText(path).Trim() == expected))
                    {
                        return;
                    }
                }
                catch (IOException)
                {
                    // Mid-write by the script; the next poll reads it whole.
                }

                await Task.Delay(100);
            }
        }
    }
}
#endif
