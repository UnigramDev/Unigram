//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if DEBUG
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Xml.Linq;
using Telegram.Common;
using Telegram.Td.Api;
using JsonValue = System.Text.Json.Nodes.JsonValue;

#nullable enable

namespace Telegram.Td
{
    public sealed class MockShot
    {
        public MockShot(string name, string? theme, long chatId, int delay, bool expandStories, string? folders, string? panel, IReadOnlyList<string>? recentEmoji, bool scrollToTop, bool profile, string? tab)
        {
            Profile = profile;
            Tab = tab;
            Name = name;
            Theme = theme;
            ChatId = chatId;
            Delay = delay;
            ExpandStories = expandStories;
            Folders = folders;
            Panel = panel;
            RecentEmoji = recentEmoji;
            ScrollToTop = scrollToTop;
        }

        public string Name { get; }

        /// <summary>"light", "dark" (Night), "tinted", or null to leave the theme as it is.</summary>
        public string? Theme { get; }

        /// <summary>The chat to open, or 0 to leave the chat as it is.</summary>
        public long ChatId { get; }

        /// <summary>Milliseconds to let the screen settle before it is captured.</summary>
        public int Delay { get; }

        /// <summary>"stories": "expanded" - the strip starts collapsed into the title bar.</summary>
        public bool ExpandStories { get; }

        /// <summary>"top", "left", or null to leave the folder tabs where the settings put them.</summary>
        public string? Folders { get; }

        /// <summary>"emoji", "stickers" or "animations": the composer's panel, open on that tab.</summary>
        public string? Panel { get; }

        /// <summary>"emoji;customEmojiId" entries that replace the recent emoji, most used first.</summary>
        public IReadOnlyList<string>? RecentEmoji { get; }

        /// <summary>"scroll": "top" - the chat opens at its last message, and its first one is wanted.</summary>
        public bool ScrollToTop { get; }

        /// <summary>"profile": true - the chat's profile opens in place of its history.</summary>
        public bool Profile { get; }

        /// <summary>
        /// "posts", "media", "files", "links" or "music": the profile tab to select. The main tab
        /// would select one too, but it also moves that tab to the front of the strip.
        /// </summary>
        public string? Tab { get; }
    }

    /// <summary>
    /// Stands in for TDLib in the package Tools\Screenshots\Screenshots.ps1 registers, answering
    /// from a scenario instead of the network, so a screenshot shows exactly what it describes.
    /// </summary>
    /// <remarks>
    /// The scenario is mock\scenario.json in LocalState: TDLib's own JSON, int64 values as strings,
    /// comments allowed. Its keys are "options" (name to value), "updates" (sent in order once
    /// authorized; chats, users, groups and their full info are also answered from these),
    /// "messages" (chat id to its messages, oldest first), "responses" ([{request, response}],
    /// matched on the request's fields before anything built in), "custom_emoji" (stickers,
    /// answered by custom emoji id in any combination) and "shots"
    /// (see <see cref="ScreenshotDriver"/>); "frame" is for Screenshots.ps1 alone.
    ///
    /// Objects are completed from <see cref="Templates"/>, so a scenario only spells out what it
    /// shows, and these keys, none of which TDLib uses, are expanded:
    /// "@path" on a file - a path relative to the scenario, given as fully downloaded;
    /// "@photo" on a user or chat - such a path, given as the profile photo;
    /// "@photo" on a message - {path, width, height, caption}, given as a photo message;
    /// "text" on a message - a plain text message; "@from" on a message - the sending user's id;
    /// "@pinned" on a chat - pinned in the main list;
    /// {"@string": key} in place of any string - that string of <see cref="Language"/>, else the
    /// English one this build has;
    /// a date, edit_date, was_online or expires under 10^9 and not 0 - seconds from launch, or
    /// "HH:mm" - that time today.
    /// A message without an id or date gets one in list order, a private or secret chat without a
    /// title or photo takes its user's, and a chat without positions goes in the main list in
    /// update order.
    ///
    /// Any other request is answered with an error and logged once per type to mock\unhandled.log,
    /// which is the list of what a screen still needs.
    /// </remarks>
    public sealed class MockClient
    {
        public static string Folder { get; } = System.IO.Path.Combine(AppPaths.LocalPath, "mock");

        // Only where Screenshots.ps1 runs - the package it registers, or for an unpackaged build
        // Windows Sandbox, whose account always has this name: a scenario left in the LocalState
        // of anything else must not stand in for the account it is logged in to.
        public static bool IsEnabled { get; } =
            (AppIdentity.FamilyName.IndexOf(".Screenshots_", StringComparison.Ordinal) >= 0
#if UNPACKAGED
            || Environment.UserName == "WDAGUtilityAccount"
#endif
            ) && System.IO.File.Exists(System.IO.Path.Combine(Folder, "scenario.json"));

        // After IsEnabled, which it reads: static initializers run in the order they are written.
        private static readonly MockLanguagePack? _languagePack = IsEnabled ? MockLanguagePack.Load(System.IO.Path.Combine(Folder, "langpack")) : null;

        /// <summary>
        /// The language of the pack Screenshots.ps1 left in mock\langpack, which the app shows in
        /// place of the one it is set to: null when there is none.
        /// </summary>
        public static string? Language => _languagePack?.Language;

        /// <summary>The plural rules of <see cref="Language"/>, which the pack itself does not name.</summary>
        public static string? PluralCode => _languagePack?.PluralCode;

        /// <summary>Answers a string of <see cref="Language"/> from the pack, where TDLib would read its database.</summary>
        public static Object GetLanguagePackString(string key)
        {
            var strings = _languagePack!.Strings;
            if (strings.TryGetValue(key, out string? value))
            {
                return new LanguagePackStringValueOrdinary(value);
            }

            if (strings.TryGetValue(key + "_other", out string? other))
            {
                return new LanguagePackStringValuePluralized(
                    GetForm(strings, key, "_zero"),
                    GetForm(strings, key, "_one"),
                    GetForm(strings, key, "_two"),
                    GetForm(strings, key, "_few"),
                    GetForm(strings, key, "_many"),
                    other);
            }

            return new Error(404, "Not Found");
        }

        private static string GetForm(Dictionary<string, string> strings, string key, string suffix)
        {
            return strings.TryGetValue(key + suffix, out string? value) ? value : string.Empty;
        }

        // The pack first, then the English the app is built with, then the key itself, so that a
        // key the scenario misspells shows up in the screenshot.
        private static string GetString(string key)
        {
            if (_languagePack != null && _languagePack.Strings.TryGetValue(key, out string? value))
            {
                return value;
            }

            return LocaleFallback.GetString(key) ?? key;
        }

        private const string Ok = "{\"@type\":\"ok\"}";
        private const string NotFound = "{\"@type\":\"error\",\"code\":404,\"message\":\"Not Found\"}";

        private static readonly HashSet<string> _acknowledged = new()
        {
            "setOption",
            "setNetworkType",
            "closeChat",
            "viewMessages",
            "setChatDraftMessage",
            "sendChatAction",
            "cancelDownloadFile",
            "readAllChatMentions",
            "readAllChatReactions"
        };

        // Responses and updates go out on one thread of their own, in order, as TDLib's do: the
        // app expects neither on the thread that sent the request.
        private static readonly BlockingCollection<Action> _queue = new();
        private static readonly object _threadLock = new();
        private static Thread? _thread;

        // Well clear of the ids td_create_client_id hands out, which count up from 1.
        private static int _nextId = 1 << 20;

        private readonly ClientResultHandler _handler;

        public int Id { get; }

        public MockClient(ClientResultHandler handler)
        {
            _handler = handler;
            Id = Interlocked.Increment(ref _nextId);

            Post(EmitWaitParameters);
        }

        public void Send(Function function, Action<Object>? handler)
        {
            string request;
            using (var buffer = new ArrayPoolBufferWriter())
            {
                ClientJson.ToJson(buffer, function);
                request = Encoding.UTF8.GetString(buffer.Bytes, 0, buffer.WrittenCount);
            }

            Post(() => Handle(request, handler));
        }

        private static void Post(Action action)
        {
            lock (_threadLock)
            {
                if (_thread == null)
                {
                    _thread = new Thread(Run)
                    {
                        Name = "TdMock",
                        IsBackground = true
                    };
                    _thread.Start();
                }
            }

            _queue.Add(action);
        }

        private static void Run()
        {
            foreach (var action in _queue.GetConsumingEnumerable())
            {
                action();
            }
        }

        private void Handle(string request, Action<Object>? handler)
        {
            string response;
            try
            {
                response = Respond(JsonNode.Parse(request)!.AsObject());
            }
            catch (Exception ex)
            {
                Scenario.Log("errors.log", request + Environment.NewLine + ex);
                response = "{\"@type\":\"error\",\"code\":500,\"message\":\"MOCK_FAILED\"}";
            }

            // Outside the catch: an exception in the app's own handler must surface as it would
            // with TDLib, not be filed as a scenario error.
            var result = Parse(response);
            if (result != null)
            {
                handler?.Invoke(result);
            }
        }

        private void Emit(string update)
        {
            var result = Parse(update);
            if (result != null)
            {
                _handler.OnResult(result);
            }
        }

        // Null for updateFile, which the parser applies to the cached file in place, and for what
        // does not parse: that is the scenario's mistake, and TDLib never sends one.
        private Object? Parse(string json)
        {
            try
            {
                return ClientJson.FromJson(json, _handler);
            }
            catch (Exception ex)
            {
                Scenario.Log("errors.log", json + Environment.NewLine + ex);
                return null;
            }
        }

        private void EmitWaitParameters()
        {
            Emit(AuthorizationState("authorizationStateWaitTdlibParameters"));
        }

        private void EmitStartup()
        {
            var scenario = Scenario.Current;

            foreach (var option in scenario.Options)
            {
                var update = new JsonObject
                {
                    ["@type"] = "updateOption",
                    ["name"] = option.Key,
                    ["value"] = JsonNode.Parse(option.Value)
                };

                Emit(Scenario.Serialize(update));
            }

            Emit(AuthorizationState("authorizationStateReady"));

            // The title bar shows the app name only once the connection is ready.
            Emit("{\"@type\":\"updateConnectionState\",\"state\":{\"@type\":\"connectionStateReady\"}}");

            foreach (var update in scenario.Updates)
            {
                Emit(update);
            }

            ScreenshotDriver.Start(scenario.Shots);
        }

        private void EmitClosed()
        {
            Emit(AuthorizationState("authorizationStateClosing"));
            Emit(AuthorizationState("authorizationStateClosed"));
        }

        private static string AuthorizationState(string type)
        {
            return "{\"@type\":\"updateAuthorizationState\",\"authorization_state\":{\"@type\":\"" + type + "\"}}";
        }

        private string Respond(JsonObject request)
        {
            var scenario = Scenario.Current;
            var type = (string?)request["@type"] ?? string.Empty;

            var matched = scenario.Match(request);
            if (matched != null)
            {
                return matched;
            }

            switch (type)
            {
                case "setTdlibParameters":
                    // Queued behind this Ok, which is the order TDLib answers in.
                    Post(EmitStartup);
                    return Ok;
                case "close":
                case "destroy":
                case "logOut":
                    Post(EmitClosed);
                    return Ok;
                case "getOption":
                    return scenario.GetOption((string?)request["name"]);
                case "getApplicationConfig":
                    return scenario.ApplicationConfig;
                case "loadChats":
                case "loadActiveStories":
                    // Every chat went out with the startup updates, and 404 is how TDLib says a
                    // list has nothing more to load. Any other error reads as a page that may have
                    // more, and the story list asks again on every layout pass.
                    return NotFound;
                case "getChat":
                    return Find(scenario.Chats, request["chat_id"]);
                case "createPrivateChat":
                    return scenario.PrivateChats.TryGetValue(ReadLong(request["user_id"]), out long chatId)
                        ? Find(scenario.Chats, chatId)
                        : NotFound;
                case "getUser":
                    return Find(scenario.Users, request["user_id"]);
                case "getUserFullInfo":
                    return scenario.UserFullInfos.TryGetValue(ReadLong(request["user_id"]), out string? userFullInfo)
                        ? userFullInfo
                        : scenario.Default("userFullInfo");
                case "getSupergroup":
                    return Find(scenario.Supergroups, request["supergroup_id"]);
                case "getSupergroupFullInfo":
                    return scenario.SupergroupFullInfos.TryGetValue(ReadLong(request["supergroup_id"]), out string? supergroupFullInfo)
                        ? supergroupFullInfo
                        : scenario.Default("supergroupFullInfo");
                case "getBasicGroup":
                    return Find(scenario.BasicGroups, request["basic_group_id"]);
                case "getBasicGroupFullInfo":
                    return scenario.BasicGroupFullInfos.TryGetValue(ReadLong(request["basic_group_id"]), out string? basicGroupFullInfo)
                        ? basicGroupFullInfo
                        : scenario.Default("basicGroupFullInfo");
                case "getChatHistory":
                    return scenario.GetHistory(
                        ReadLong(request["chat_id"]),
                        ReadLong(request["from_message_id"]),
                        (int)ReadLong(request["offset"]),
                        (int)ReadLong(request["limit"]));
                case "getChatMessageCount":
                    return scenario.CountMessages(
                        ReadLong(request["chat_id"]),
                        (string?)request["filter"]?["@type"]);
                case "searchChatMessages":
                    return scenario.SearchMessages(
                        ReadLong(request["chat_id"]),
                        (string?)request["filter"]?["@type"],
                        ReadLong(request["from_message_id"]),
                        (int)ReadLong(request["offset"]),
                        (int)ReadLong(request["limit"]));
                case "getMessage":
                case "getMessageLocally":
                    return scenario.GetMessage(ReadLong(request["chat_id"]), ReadLong(request["message_id"])) ?? NotFound;
                case "getMessages":
                    return scenario.GetMessages(ReadLong(request["chat_id"]), request["message_ids"] as JsonArray);
                case "getRepliedMessage":
                    return scenario.GetRepliedMessage(ReadLong(request["chat_id"]), ReadLong(request["message_id"])) ?? NotFound;
                case "getSecretChat":
                    return Find(scenario.SecretChats, request["secret_chat_id"]);
                case "openChat":
                    // TDLib sends the count to whoever opens the chat, and only the open chat's
                    // view model keeps it, so the one sent at startup reaches nobody.
                    if (scenario.OnlineMemberCounts.TryGetValue(ReadLong(request["chat_id"]), out string? online))
                    {
                        Post(() => Emit(online));
                    }

                    return Ok;
                case "getFile":
                case "downloadFile":
                    return Find(scenario.Files, request["file_id"]);
                case "getCustomEmojiStickers":
                    return scenario.GetCustomEmojiStickers(request["custom_emoji_ids"] as JsonArray);
            }

            if (_acknowledged.Contains(type))
            {
                return Ok;
            }

            scenario.Unhandled(type, request);
            return "{\"@type\":\"error\",\"code\":400,\"message\":\"MOCK_UNHANDLED\"}";
        }

        private static string Find(Dictionary<long, string> objects, JsonNode? id)
        {
            return Find(objects, ReadLong(id));
        }

        private static string Find(Dictionary<long, string> objects, long id)
        {
            return objects.TryGetValue(id, out string? value) ? value : NotFound;
        }

        /// <summary>
        /// Reads an integer written either way TDLib writes one: int53 as a number, int64 as a string.
        /// </summary>
        private static long ReadLong(JsonNode? node)
        {
            if (node == null)
            {
                return 0;
            }

            var text = Scenario.Serialize(node).Trim('"');
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;
        }

        private static bool TryReadNumber(JsonNode? node, out long value)
        {
            value = 0;
            if (node == null)
            {
                return false;
            }

            var text = Scenario.Serialize(node);
            return text.Length > 0 && text[0] != '"' && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private sealed class MockMessage
        {
            public MockMessage(long id, long replyToId, string? contentType, string json)
            {
                Id = id;
                ReplyToId = replyToId;
                ContentType = contentType;
                Json = json;
            }

            public long Id { get; }

            public long ReplyToId { get; }

            public string? ContentType { get; }

            public string Json { get; }
        }

        private sealed class MockResponse
        {
            public MockResponse(JsonObject request, string json)
            {
                Request = request;
                Json = json;
            }

            public JsonObject Request { get; }

            public string Json { get; }
        }

        /// <summary>
        /// A localization target's export, named for its language: the format of Resources.xml,
        /// plurals as _one/_other entries and \n, \' and \" escaped.
        /// </summary>
        private sealed class MockLanguagePack
        {
            private MockLanguagePack(string language, Dictionary<string, string> strings)
            {
                Language = language;
                Strings = strings;

                var dash = language.IndexOf('-');
                PluralCode = dash > 0 ? language.Substring(0, dash) : language;
            }

            public string Language { get; }

            public string PluralCode { get; }

            public Dictionary<string, string> Strings { get; }

            public static MockLanguagePack? Load(string folder)
            {
                if (!System.IO.Directory.Exists(folder))
                {
                    return null;
                }

                try
                {
                    var files = System.IO.Directory.GetFiles(folder, "*.xml");
                    if (files.Length != 1)
                    {
                        Scenario.Log("errors.log", "langpack: expected one pack, found " + files.Length);
                        return null;
                    }

                    var strings = new Dictionary<string, string>();

                    foreach (var element in XDocument.Load(files[0]).Root!.Elements("string"))
                    {
                        if ((string?)element.Attribute("name") is string name)
                        {
                            strings[name] = Unescape(element.Value);
                        }
                    }

                    return new MockLanguagePack(System.IO.Path.GetFileNameWithoutExtension(files[0]), strings);
                }
                catch (Exception ex)
                {
                    Scenario.Log("errors.log", "langpack: " + ex);
                    return null;
                }
            }

            // Android resource escaping: an unescaped quote only marks a span whose spaces are kept, and the
            // export quotes every value that starts or ends with one. TDLib's copy has neither the quotes
            // nor the backslashes.
            private static string Unescape(string value)
            {
                var builder = new StringBuilder(value.Length);

                for (int i = 0; i < value.Length; i++)
                {
                    var c = value[i];
                    if (c == '"')
                    {
                        continue;
                    }
                    else if (c == '\\' && i + 1 < value.Length)
                    {
                        c = value[++i];
                        builder.Append(c == 'n' ? '\n' : c == 't' ? '\t' : c);
                    }
                    else
                    {
                        builder.Append(c);
                    }
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// What a scenario leaves out and the app cannot do without: TDLib always sends these
        /// fields, and the generated classes leave a missing object or vector null.
        /// </summary>
        private static readonly Dictionary<string, string> Templates = new()
        {
            ["chat"] = """
            {
                "accent_color_id": 0, "background_custom_emoji_id": "0", "profile_accent_color_id": -1, "profile_background_custom_emoji_id": "0",
                "permissions": { "@type": "chatPermissions" },
                "positions": [], "chat_lists": [],
                "can_be_deleted_only_for_self": true,
                "notification_settings": { "@type": "chatNotificationSettings" },
                "available_reactions": { "@type": "chatAvailableReactionsAll", "max_reaction_count": 11 },
                "video_chat": { "@type": "videoChat", "group_call_id": 0, "has_participants": false },
                "client_data": ""
            }
            """,
            ["chatPermissions"] = """{ "can_send_basic_messages": true, "can_send_audios": true, "can_send_documents": true, "can_send_photos": true, "can_send_videos": true, "can_send_video_notes": true, "can_send_voice_notes": true, "can_send_polls": true, "can_send_other_messages": true, "can_add_link_previews": true, "can_react_to_messages": true, "can_edit_tag": false, "can_change_info": true, "can_invite_users": true, "can_pin_messages": true, "can_create_topics": true }""",
            ["chatNotificationSettings"] = """{ "use_default_mute_for": true, "mute_for": 0, "use_default_sound": true, "sound_id": "0", "use_default_show_preview": true, "show_preview": true, "use_default_mute_stories": true, "mute_stories": false, "use_default_story_sound": true, "story_sound_id": "0", "use_default_show_story_poster": true, "show_story_poster": true, "use_default_disable_pinned_message_notifications": true, "disable_pinned_message_notifications": false, "use_default_disable_mention_notifications": true, "disable_mention_notifications": false }""",
            ["chatPosition"] = """{ "list": { "@type": "chatListMain" }, "order": "0", "is_pinned": false }""",
            ["chatPhotoInfo"] = """{ "has_animation": false, "is_personal": false }""",
            ["profilePhoto"] = """{ "id": "0", "has_animation": false, "is_personal": false }""",
            ["user"] = """
            {
                "first_name": "", "last_name": "", "phone_number": "",
                "status": { "@type": "userStatusRecently", "by_my_privacy_settings": false },
                "background_custom_emoji_id": "0", "profile_accent_color_id": -1, "profile_background_custom_emoji_id": "0",
                "is_contact": true, "is_mutual_contact": true, "have_access": true,
                "type": { "@type": "userTypeRegular" }, "language_code": ""
            }
            """,
            ["usernames"] = """{ "active_usernames": [], "disabled_usernames": [], "editable_username": "", "collectible_usernames": [] }""",
            ["userFullInfo"] = """
            {
                "can_be_called": true, "supports_video_calls": true,
                "gift_settings": { "@type": "giftSettings", "show_gift_button": false, "accepted_gift_types": { "@type": "acceptedGiftTypes", "unlimited_gifts": true, "limited_gifts": true, "upgraded_gifts": true, "gifts_from_channels": true, "premium_subscription": true } }
            }
            """,
            ["supergroup"] = """{ "status": { "@type": "chatMemberStatusMember", "member_until_date": 0 } }""",
            ["supergroupFullInfo"] = """{ "description": "", "bot_commands": [] }""",
            ["basicGroup"] = """{ "status": { "@type": "chatMemberStatusMember", "member_until_date": 0 }, "is_active": true }""",
            ["basicGroupFullInfo"] = """{ "description": "", "members": [], "bot_commands": [] }""",
            ["message"] = """{ "can_be_saved": true, "unread_reactions": [] }""",
            ["messageReplyToMessage"] = """{ "checklist_task_id": 0, "poll_option_id": "", "origin_send_date": 0 }""",
            ["formattedText"] = """{ "entities": [] }""",
            ["photo"] = """{ "has_stickers": false, "sizes": [] }""",
            ["photoSize"] = """{ "type": "y", "progressive_sizes": [] }""",
            ["thumbnail"] = """{ "format": { "@type": "thumbnailFormatJpeg" } }""",
            ["document"] = """{ "file_name": "", "mime_type": "application/octet-stream" }""",
            ["sticker"] = """{ "id": "0", "set_id": "0", "width": 512, "height": 512, "emoji": "", "format": { "@type": "stickerFormatWebp" }, "full_type": { "@type": "stickerFullTypeRegular" } }""",
            ["voiceNote"] = """{ "duration": 0, "waveform": "", "mime_type": "audio/ogg" }""",
            ["videoNote"] = """{ "duration": 0, "waveform": "", "length": 384 }""",
            ["poll"] = """{ "id": "0", "options": [], "total_voter_count": 0, "recent_voter_ids": [], "can_get_voters": false, "can_see_results": true, "is_anonymous": true, "allows_multiple_answers": false, "allows_revoting": true, "members_only": false, "country_codes": [], "option_order": [], "type": { "@type": "pollTypeRegular" }, "open_period": 0, "close_date": 0, "is_closed": false }""",
            ["pollOption"] = """{ "id": "", "voter_count": 0, "vote_percentage": 0, "recent_voter_ids": [], "is_chosen": false, "is_being_chosen": false }""",
            ["messageText"] = """{ "text": { "@type": "formattedText", "text": "" } }""",
            ["messagePhoto"] = """{ "caption": { "@type": "formattedText", "text": "" } }""",
            ["messageDocument"] = """{ "caption": { "@type": "formattedText", "text": "" } }""",
            ["messageVoiceNote"] = """{ "caption": { "@type": "formattedText", "text": "" }, "is_listened": true }""",
            ["messageVideoNote"] = """{ "is_viewed": true, "is_secret": false }""",
            ["messageSticker"] = """{ "is_premium": false }""",
            ["messagePoll"] = """{ "description": { "@type": "formattedText", "text": "" }, "can_add_option": false }""",
            ["messageInteractionInfo"] = """{ "view_count": 0, "forward_count": 0 }""",
            ["messageReactions"] = """{ "reactions": [], "are_tags": false, "paid_reactors": [], "can_get_added_reactions": false }""",
            ["messageReaction"] = """{ "total_count": 1, "is_chosen": false, "recent_sender_ids": [] }""",
            ["emojiReaction"] = """{ "title": "", "is_active": true }""",
            ["background"] = """{ "id": "1", "is_default": false, "is_dark": false, "name": "" }""",
            ["chatBackground"] = """{ "dark_theme_dimming": 0 }""",
            ["secretChat"] = """{ "state": { "@type": "secretChatStateReady" }, "is_outbound": true, "key_hash": "", "layer": 144 }"""
        };

        private sealed class Scenario
        {
            private static readonly object _loadLock = new();
            private static Scenario? _current;

            public static Scenario Current
            {
                get
                {
                    lock (_loadLock)
                    {
                        return _current ??= Load();
                    }
                }
            }

            public readonly Dictionary<string, string> Options = new();
            public readonly List<string> Updates = new();
            public readonly List<MockShot> Shots = new();
            public string ApplicationConfig = "{\"@type\":\"jsonValueObject\",\"members\":[]}";

            public readonly Dictionary<long, string> Chats = new();
            public readonly Dictionary<long, long> PrivateChats = new();
            public readonly Dictionary<long, string> Users = new();
            public readonly Dictionary<long, string> UserFullInfos = new();
            public readonly Dictionary<long, string> Supergroups = new();
            public readonly Dictionary<long, string> SupergroupFullInfos = new();
            public readonly Dictionary<long, string> BasicGroups = new();
            public readonly Dictionary<long, string> BasicGroupFullInfos = new();
            public readonly Dictionary<long, string> SecretChats = new();
            public readonly Dictionary<long, string> OnlineMemberCounts = new();
            public readonly Dictionary<long, string> Files = new();
            public readonly Dictionary<long, string> CustomEmoji = new();

            private readonly Dictionary<long, List<MockMessage>> _messages = new();
            private readonly List<MockResponse> _responses = new();
            private readonly HashSet<string> _unhandled = new();

            private readonly long _now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            private long _myId;

            // Well above the ids a scenario writes by hand.
            private int _nextFileId = 1 << 24;
            private long _nextPhotoId = 1 << 24;

            private static Scenario Load()
            {
                var scenario = new Scenario();

                try
                {
                    var text = System.IO.File.ReadAllText(System.IO.Path.Combine(Folder, "scenario.json"));
                    var root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
                    {
                        CommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    });

                    if (root is JsonObject obj)
                    {
                        Localize(obj);
                        scenario.Read(obj);
                    }
                }
                catch (Exception ex)
                {
                    Log("errors.log", "scenario.json: " + ex);
                }

                return scenario;
            }

            // Before anything else reads the scenario, so the shorthands and the completion from
            // chats and users see plain strings.
            private static void Localize(JsonNode node)
            {
                if (node is JsonObject obj)
                {
                    List<string>? keys = null;

                    foreach (var pair in obj)
                    {
                        if (pair.Value is JsonObject child && child["@string"] != null)
                        {
                            (keys ??= new List<string>()).Add(pair.Key);
                        }
                        else if (pair.Value != null)
                        {
                            Localize(pair.Value);
                        }
                    }

                    // Replacing a value while enumerating the object would invalidate the enumerator.
                    if (keys != null)
                    {
                        foreach (var name in keys)
                        {
                            obj[name] = JsonValue.Create(GetString((string)obj[name]!["@string"]!));
                        }
                    }
                }
                else if (node is JsonArray array)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        if (array[i] is JsonObject child && child["@string"] is JsonNode key)
                        {
                            array[i] = JsonValue.Create(GetString((string)key));
                        }
                        else if (array[i] != null)
                        {
                            Localize(array[i]!);
                        }
                    }
                }
            }

            private void Read(JsonObject root)
            {
                ReadOptions(root["options"] as JsonObject);

                var updates = root["updates"] as JsonArray ?? new JsonArray();

                // Messages are completed from their chat, and chats from their user and newest
                // message, so both are looked up before anything is expanded.
                var chats = new Dictionary<long, JsonObject>();
                var users = new Dictionary<long, JsonObject>();

                foreach (var update in updates)
                {
                    if (update is not JsonObject obj)
                    {
                        continue;
                    }

                    if (obj["chat"] is JsonObject chat && (string?)obj["@type"] == "updateNewChat")
                    {
                        chats[ReadLong(chat["id"])] = chat;
                    }
                    else if (obj["user"] is JsonObject user && (string?)obj["@type"] == "updateUser")
                    {
                        users[ReadLong(user["id"])] = user;
                    }
                }

                if (root["messages"] is JsonObject messages)
                {
                    foreach (var pair in messages)
                    {
                        if (long.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out long chatId) && pair.Value is JsonArray list)
                        {
                            chats.TryGetValue(chatId, out JsonObject? chat);
                            ReadMessages(chatId, chat, list);
                        }
                    }
                }

                foreach (var user in users.Values)
                {
                    if (user["accent_color_id"] == null)
                    {
                        user["accent_color_id"] = (int)(Math.Abs(ReadLong(user["id"])) % 7);
                    }
                }

                var index = 0;
                foreach (var chat in chats.Values)
                {
                    CompleteChat(chat, users, index++, chats.Count);
                }

                foreach (var update in updates)
                {
                    if (update is JsonObject obj)
                    {
                        Expand(obj);
                        Index(obj);
                        Updates.Add(Serialize(obj));
                    }
                }

                if (root["responses"] is JsonArray responses)
                {
                    foreach (var item in responses)
                    {
                        if (item?["request"] is JsonObject request && item["response"] is JsonObject response)
                        {
                            Expand(response);
                            _responses.Add(new MockResponse(request, Serialize(response)));
                        }
                    }
                }

                if (root["custom_emoji"] is JsonArray customEmoji)
                {
                    foreach (var item in customEmoji)
                    {
                        if (item is JsonObject sticker && sticker["full_type"]?["custom_emoji_id"] is JsonNode id)
                        {
                            Expand(sticker);
                            CustomEmoji[ReadLong(id)] = Serialize(sticker);
                        }
                    }
                }

                if (root["application_config"] is JsonObject config)
                {
                    ApplicationConfig = Serialize(config);
                }

                if (root["shots"] is JsonArray shots)
                {
                    for (int i = 0; i < shots.Count; i++)
                    {
                        if (shots[i] is JsonObject shot)
                        {
                            var delay = shot["delay"] != null ? (int)ReadLong(shot["delay"]) : 1500;
                            List<string>? recentEmoji = null;

                            if (shot["recent_emoji"] is JsonArray recent)
                            {
                                recentEmoji = new List<string>(recent.Count);

                                foreach (var entry in recent)
                                {
                                    recentEmoji.Add((string?)entry ?? string.Empty);
                                }
                            }

                            Shots.Add(new MockShot((string?)shot["name"] ?? $"shot{i}", (string?)shot["theme"], ReadLong(shot["chat"]), delay, (string?)shot["stories"] == "expanded", (string?)shot["folders"], (string?)shot["panel"], recentEmoji, (string?)shot["scroll"] == "top", Serialize(shot["profile"]) == "true", (string?)shot["tab"]));
                        }
                    }
                }
            }

            private void ReadOptions(JsonObject? options)
            {
                if (options == null)
                {
                    return;
                }

                foreach (var pair in options)
                {
                    string value;
                    if (pair.Value is JsonObject obj)
                    {
                        value = Serialize(obj);
                    }
                    else if (pair.Value != null && TryReadNumber(pair.Value, out long number))
                    {
                        value = "{\"@type\":\"optionValueInteger\",\"value\":\"" + number.ToString(CultureInfo.InvariantCulture) + "\"}";
                    }
                    else
                    {
                        var text = Serialize(pair.Value);
                        value = text is "true" or "false"
                            ? "{\"@type\":\"optionValueBoolean\",\"value\":" + text + "}"
                            : "{\"@type\":\"optionValueString\",\"value\":" + text + "}";
                    }

                    Options[pair.Key] = value;

                    if (pair.Key == "my_id")
                    {
                        _myId = ReadLong(pair.Value);
                    }
                }
            }

            private void ReadMessages(long chatId, JsonObject? chat, JsonArray list)
            {
                var type = chat?["type"] as JsonObject;
                var typeName = (string?)type?["@type"];

                var result = new List<MockMessage>(list.Count);

                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is not JsonObject message)
                    {
                        continue;
                    }

                    // Expand dispatches on it, for the shorthands and the template alike.
                    message["@type"] ??= "message";

                    // TDLib numbers server messages in steps of 2^20.
                    message["id"] ??= (long)(i + 1) << 20;
                    message["chat_id"] ??= chatId;
                    message["date"] ??= -60L * (list.Count - i);

                    long replyToId = 0;
                    if (message["reply_to"] is JsonObject replyTo && (string?)replyTo["@type"] == "messageReplyToMessage")
                    {
                        replyTo["chat_id"] ??= chatId;
                        replyToId = ReadLong(replyTo["message_id"]);
                    }

                    if (message["sender_id"] == null && message["@from"] == null)
                    {
                        var outgoing = Serialize(message["is_outgoing"]) == "true";
                        if (outgoing)
                        {
                            message["@from"] = _myId;
                        }
                        else if (typeName is "chatTypePrivate" or "chatTypeSecret")
                        {
                            message["@from"] = ReadLong(type?["user_id"]);
                        }
                        else if (typeName == "chatTypeSupergroup" && Serialize(type?["is_channel"]) == "true")
                        {
                            message["sender_id"] = new JsonObject
                            {
                                ["@type"] = "messageSenderChat",
                                ["chat_id"] = chatId
                            };
                        }
                    }

                    Expand(message);
                    result.Add(new MockMessage(ReadLong(message["id"]), replyToId, (string?)message["content"]?["@type"], Serialize(message)));
                }

                result.Sort(NewestFirst);
                _messages[chatId] = result;
            }

            private static int NewestFirst(MockMessage x, MockMessage y)
            {
                return y.Id.CompareTo(x.Id);
            }

            private void CompleteChat(JsonObject chat, Dictionary<long, JsonObject> users, int index, int count)
            {
                var id = ReadLong(chat["id"]);
                var type = chat["type"] as JsonObject;
                var typeName = (string?)type?["@type"];

                long colorSource = id;

                if (typeName is "chatTypePrivate" or "chatTypeSecret")
                {
                    var userId = ReadLong(type?["user_id"]);
                    colorSource = userId;

                    if (users.TryGetValue(userId, out JsonObject? user))
                    {
                        if (chat["title"] == null)
                        {
                            chat["title"] = $"{(string?)user["first_name"]} {(string?)user["last_name"]}".Trim();
                        }

                        if (chat["photo"] == null && chat["@photo"] == null && user["@photo"] != null)
                        {
                            chat["@photo"] = user["@photo"]!.DeepClone();
                        }
                    }
                }
                else if (typeName == "chatTypeSupergroup")
                {
                    colorSource = ReadLong(type?["supergroup_id"]);
                }
                else if (typeName == "chatTypeBasicGroup")
                {
                    colorSource = ReadLong(type?["basic_group_id"]);
                }

                chat["accent_color_id"] ??= (int)(Math.Abs(colorSource) % 7);

                if (_messages.TryGetValue(id, out List<MockMessage>? messages) && messages.Count > 0)
                {
                    chat["last_message"] ??= JsonNode.Parse(messages[0].Json);

                    // Everything read unless the scenario says otherwise, and then everything
                    // but the newest unread_count.
                    if (chat["last_read_inbox_message_id"] == null)
                    {
                        var unread = (int)ReadLong(chat["unread_count"]);
                        chat["last_read_inbox_message_id"] = unread < messages.Count ? messages[unread].Id : 0;
                    }

                    chat["last_read_outbox_message_id"] ??= messages[0].Id;
                }

                var pinned = Serialize(chat["@pinned"]) == "true";
                chat.Remove("@pinned");

                chat["positions"] ??= new JsonArray(new JsonObject
                {
                    ["@type"] = "chatPosition",
                    ["list"] = new JsonObject { ["@type"] = "chatListMain" },
                    ["order"] = ((long)(count - index) * 1000).ToString(CultureInfo.InvariantCulture),
                    ["is_pinned"] = pinned
                });
            }

            private void Index(JsonObject update)
            {
                switch ((string?)update["@type"])
                {
                    case "updateNewChat" when update["chat"] is JsonObject chat:
                        var chatId = ReadLong(chat["id"]);
                        Chats[chatId] = Serialize(chat);

                        if (chat["type"] is JsonObject type && (string?)type["@type"] == "chatTypePrivate")
                        {
                            PrivateChats[ReadLong(type["user_id"])] = chatId;
                        }
                        break;
                    case "updateUser" when update["user"] is JsonObject user:
                        Users[ReadLong(user["id"])] = Serialize(user);
                        break;
                    case "updateSupergroup" when update["supergroup"] is JsonObject supergroup:
                        Supergroups[ReadLong(supergroup["id"])] = Serialize(supergroup);
                        break;
                    case "updateBasicGroup" when update["basic_group"] is JsonObject basicGroup:
                        BasicGroups[ReadLong(basicGroup["id"])] = Serialize(basicGroup);
                        break;
                    case "updateUserFullInfo":
                        UserFullInfos[ReadLong(update["user_id"])] = Serialize(update["user_full_info"]);
                        break;
                    case "updateSupergroupFullInfo":
                        SupergroupFullInfos[ReadLong(update["supergroup_id"])] = Serialize(update["supergroup_full_info"]);
                        break;
                    case "updateBasicGroupFullInfo":
                        BasicGroupFullInfos[ReadLong(update["basic_group_id"])] = Serialize(update["basic_group_full_info"]);
                        break;
                    case "updateSecretChat" when update["secret_chat"] is JsonObject secretChat:
                        SecretChats[ReadLong(secretChat["id"])] = Serialize(secretChat);
                        break;
                    case "updateChatOnlineMemberCount":
                        OnlineMemberCounts[ReadLong(update["chat_id"])] = Serialize(update);
                        break;
                }
            }

            /// <summary>
            /// Rewrites a scenario object, and everything in it, into what TDLib would have sent.
            /// </summary>
            private void Expand(JsonNode? node)
            {
                if (node is JsonArray array)
                {
                    foreach (var item in array)
                    {
                        Expand(item);
                    }

                    return;
                }

                if (node is not JsonObject obj)
                {
                    return;
                }

                var type = (string?)obj["@type"];
                switch (type)
                {
                    case "file":
                        ExpandFile(obj);
                        break;
                    case "chat":
                        ExpandPhoto(obj, "photo", "chatPhotoInfo");
                        break;
                    case "user":
                        ExpandPhoto(obj, "profile_photo", "profilePhoto");
                        break;
                    case "message":
                        ExpandMessage(obj);
                        break;
                }

                if (type != null && Templates.TryGetValue(type, out string? template))
                {
                    var defaults = JsonNode.Parse(template)!.AsObject();
                    foreach (var key in new List<string>(Keys(defaults)))
                    {
                        if (!obj.ContainsKey(key))
                        {
                            var value = defaults[key];
                            defaults.Remove(key);
                            obj[key] = value;
                        }
                    }
                }

                // TDLib leaves a null field out, and the parser builds an empty object for an
                // explicit null: a set thumbnail with no file, which crashes the image showing it.
                // Dropped after the templates so that a null still keeps a default out.
                List<string>? nulls = null;
                foreach (var pair in obj)
                {
                    if (pair.Value == null)
                    {
                        (nulls ??= new List<string>()).Add(pair.Key);
                    }
                }

                if (nulls != null)
                {
                    foreach (var key in nulls)
                    {
                        obj.Remove(key);
                    }
                }

                ResolveDate(obj, "date");
                ResolveDate(obj, "edit_date");
                ResolveDate(obj, "was_online");
                ResolveDate(obj, "expires");

                foreach (var pair in obj)
                {
                    Expand(pair.Value);
                }

                TypeFirst(obj);

                if (type == "file")
                {
                    Files[ReadLong(obj["id"])] = Serialize(obj);
                }
            }

            private static IEnumerable<string> Keys(JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    yield return pair.Key;
                }
            }

            private void ResolveDate(JsonObject obj, string name)
            {
                var node = obj[name];
                if (TryReadNumber(node, out long value))
                {
                    if (value != 0 && Math.Abs(value) < 1_000_000_000)
                    {
                        obj[name] = _now + value;
                    }
                }
                else if (node is JsonValue text && text.TryGetValue(out string? clock)
                    && TimeSpan.TryParseExact(clock, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan time))
                {
                    obj[name] = new DateTimeOffset(DateTime.Today + time).ToUnixTimeSeconds();
                }
            }

            // The parsers read @type first and dispatch on it, so it cannot sit anywhere else.
            private static void TypeFirst(JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    if (pair.Key == "@type" || !obj.ContainsKey("@type"))
                    {
                        return;
                    }

                    break;
                }

                var pairs = new List<KeyValuePair<string, JsonNode?>>(obj);
                obj.Clear();

                obj["@type"] = pairs.Find(IsType).Value;

                foreach (var pair in pairs)
                {
                    if (pair.Key != "@type")
                    {
                        obj[pair.Key] = pair.Value;
                    }
                }
            }

            private static bool IsType(KeyValuePair<string, JsonNode?> pair)
            {
                return pair.Key == "@type";
            }

            private void ExpandFile(JsonObject file)
            {
                if (file["@path"] is not JsonNode pathNode)
                {
                    return;
                }

                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Folder, (string?)pathNode ?? string.Empty));
                file.Remove("@path");

                long size = 0;
                if (System.IO.File.Exists(path))
                {
                    size = new System.IO.FileInfo(path).Length;
                }
                else
                {
                    Log("errors.log", "missing file: " + path);
                }

                file["id"] ??= _nextFileId++;

                var id = ReadLong(file["id"]);

                file["size"] = size;
                file["expected_size"] = size;
                file["local"] = new JsonObject
                {
                    ["@type"] = "localFile",
                    ["path"] = path,
                    ["can_be_downloaded"] = true,
                    ["can_be_deleted"] = false,
                    ["is_downloading_active"] = false,
                    ["is_downloading_completed"] = size > 0,
                    ["download_offset"] = 0,
                    ["downloaded_prefix_size"] = size,
                    ["downloaded_size"] = size
                };
                file["remote"] = new JsonObject
                {
                    ["@type"] = "remoteFile",
                    ["id"] = string.Empty,
                    ["unique_id"] = "mock" + id.ToString(CultureInfo.InvariantCulture),
                    ["is_uploading_active"] = false,
                    ["is_uploading_completed"] = true,
                    ["uploaded_size"] = size
                };
            }

            private void ExpandPhoto(JsonObject obj, string property, string type)
            {
                if (obj["@photo"] is not JsonNode path)
                {
                    return;
                }

                obj.Remove("@photo");

                if (obj[property] != null)
                {
                    return;
                }

                var photo = new JsonObject
                {
                    ["@type"] = type,
                    ["small"] = new JsonObject { ["@type"] = "file", ["@path"] = path.DeepClone() },
                    ["big"] = new JsonObject { ["@type"] = "file", ["@path"] = path.DeepClone() }
                };

                if (type == "profilePhoto")
                {
                    photo["id"] = (_nextPhotoId++).ToString(CultureInfo.InvariantCulture);
                }

                obj[property] = photo;
            }

            private void ExpandMessage(JsonObject message)
            {
                if (message["@from"] is JsonNode from)
                {
                    message.Remove("@from");
                    message["sender_id"] ??= new JsonObject
                    {
                        ["@type"] = "messageSenderUser",
                        ["user_id"] = ReadLong(from)
                    };
                }

                if (message["text"] is JsonNode text)
                {
                    message.Remove("text");
                    message["content"] ??= new JsonObject
                    {
                        ["@type"] = "messageText",
                        ["text"] = ToFormattedText(text)
                    };
                }

                if (message["@photo"] is JsonNode photo)
                {
                    message.Remove("@photo");

                    var path = photo is JsonObject spec ? spec["path"] : photo;
                    var width = photo is JsonObject w && w["width"] != null ? ReadLong(w["width"]) : 1280;
                    var height = photo is JsonObject h && h["height"] != null ? ReadLong(h["height"]) : 960;
                    var caption = photo is JsonObject c ? c["caption"] : null;

                    message["content"] ??= new JsonObject
                    {
                        ["@type"] = "messagePhoto",
                        ["photo"] = new JsonObject
                        {
                            ["@type"] = "photo",
                            ["has_stickers"] = false,
                            ["sizes"] = new JsonArray(new JsonObject
                            {
                                ["@type"] = "photoSize",
                                ["type"] = "y",
                                ["photo"] = new JsonObject { ["@type"] = "file", ["@path"] = path?.DeepClone() },
                                ["width"] = width,
                                ["height"] = height,
                                ["progressive_sizes"] = new JsonArray()
                            })
                        },
                        ["caption"] = ToFormattedText(caption),
                        ["show_caption_above_media"] = false,
                        ["has_spoiler"] = false,
                        ["is_secret"] = false
                    };
                }
            }

            private static JsonObject ToFormattedText(JsonNode? text)
            {
                if (text is JsonObject obj)
                {
                    return obj.DeepClone().AsObject();
                }

                return new JsonObject
                {
                    ["@type"] = "formattedText",
                    ["text"] = text != null ? (string?)text : string.Empty,
                    ["entities"] = new JsonArray()
                };
            }

            public string GetOption(string? name)
            {
                if (name != null && Options.TryGetValue(name, out string? value))
                {
                    return value;
                }

                return "{\"@type\":\"optionValueEmpty\"}";
            }

            public string Default(string type)
            {
                var obj = new JsonObject { ["@type"] = type };
                Expand(obj);
                return Serialize(obj);
            }

            public string GetHistory(long chatId, long fromMessageId, int offset, int limit)
            {
                if (!_messages.TryGetValue(chatId, out List<MockMessage>? messages))
                {
                    return "{\"@type\":\"messages\",\"total_count\":0,\"messages\":[]}";
                }

                // Newest first, from the first message older than from_message_id, moved -offset
                // messages newer: TDLib leaves from_message_id itself out at offset 0, and the app
                // finds the start of a chat by a backward load from its oldest message coming back
                // empty.
                var start = 0;
                if (fromMessageId != 0)
                {
                    while (start < messages.Count && messages[start].Id >= fromMessageId)
                    {
                        start++;
                    }
                }

                start = Math.Max(0, start + offset);
                var end = Math.Min(messages.Count, start + Math.Max(0, limit));

                var builder = new StringBuilder("{\"@type\":\"messages\",\"total_count\":");
                builder.Append(messages.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"messages\":[");

                for (int i = start; i < end; i++)
                {
                    if (i > start)
                    {
                        builder.Append(',');
                    }

                    builder.Append(messages[i].Json);
                }

                builder.Append("]}");
                return builder.ToString();
            }

            public string CountMessages(long chatId, string? filter)
            {
                var count = 0;
                if (_messages.TryGetValue(chatId, out List<MockMessage>? messages))
                {
                    foreach (var message in messages)
                    {
                        if (Matches(message, filter))
                        {
                            count++;
                        }
                    }
                }

                return "{\"@type\":\"count\",\"count\":" + count.ToString(CultureInfo.InvariantCulture) + "}";
            }

            /// <summary>
            /// A query-less search: the chat's messages of the filter's kind, newest first, paged
            /// as <see cref="GetHistory"/> pages.
            /// </summary>
            public string SearchMessages(long chatId, string? filter, long fromMessageId, int offset, int limit)
            {
                var found = new List<MockMessage>();
                if (_messages.TryGetValue(chatId, out List<MockMessage>? messages))
                {
                    foreach (var message in messages)
                    {
                        if (Matches(message, filter))
                        {
                            found.Add(message);
                        }
                    }
                }

                var start = 0;
                if (fromMessageId != 0)
                {
                    while (start < found.Count && found[start].Id >= fromMessageId)
                    {
                        start++;
                    }
                }

                start = Math.Max(0, start + offset);
                var end = Math.Min(found.Count, start + Math.Max(0, limit));

                var builder = new StringBuilder("{\"@type\":\"foundChatMessages\",\"total_count\":");
                builder.Append(found.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"messages\":[");

                for (int i = start; i < end; i++)
                {
                    if (i > start)
                    {
                        builder.Append(',');
                    }

                    builder.Append(found[i].Json);
                }

                // 0 is how TDLib says there is nothing more to load.
                var next = end < found.Count && end > start ? found[end - 1].Id : 0;

                builder.Append("],\"next_from_message_id\":");
                builder.Append(next.ToString(CultureInfo.InvariantCulture));
                builder.Append('}');
                return builder.ToString();
            }

            private static bool Matches(MockMessage message, string? filter)
            {
                return filter switch
                {
                    null or "searchMessagesFilterEmpty" => true,
                    "searchMessagesFilterPhotoAndVideo" => message.ContentType is "messagePhoto" or "messageVideo",
                    "searchMessagesFilterPhoto" => message.ContentType == "messagePhoto",
                    "searchMessagesFilterVideo" => message.ContentType == "messageVideo",
                    "searchMessagesFilterDocument" => message.ContentType == "messageDocument",
                    "searchMessagesFilterAudio" => message.ContentType == "messageAudio",
                    "searchMessagesFilterAnimation" => message.ContentType == "messageAnimation",
                    "searchMessagesFilterVoiceNote" => message.ContentType == "messageVoiceNote",
                    "searchMessagesFilterVideoNote" => message.ContentType == "messageVideoNote",
                    "searchMessagesFilterVoiceAndVideoNote" => message.ContentType is "messageVoiceNote" or "messageVideoNote",
                    "searchMessagesFilterPoll" => message.ContentType == "messagePoll",
                    // TDLib indexes links by entity; a text search is close enough for invented data.
                    "searchMessagesFilterUrl" => message.Json.IndexOf("\"textEntityTypeUrl\"", StringComparison.Ordinal) >= 0
                        || message.Json.IndexOf("\"textEntityTypeTextUrl\"", StringComparison.Ordinal) >= 0,
                    _ => false
                };
            }

            public string? GetMessage(long chatId, long messageId)
            {
                if (_messages.TryGetValue(chatId, out List<MockMessage>? messages))
                {
                    foreach (var message in messages)
                    {
                        if (message.Id == messageId)
                        {
                            return message.Json;
                        }
                    }
                }

                return null;
            }

            public string? GetRepliedMessage(long chatId, long messageId)
            {
                if (_messages.TryGetValue(chatId, out List<MockMessage>? messages))
                {
                    foreach (var message in messages)
                    {
                        if (message.Id == messageId)
                        {
                            return message.ReplyToId != 0 ? GetMessage(chatId, message.ReplyToId) : null;
                        }
                    }
                }

                return null;
            }

            public string GetMessages(long chatId, JsonArray? messageIds)
            {
                var builder = new StringBuilder("{\"@type\":\"messages\",\"total_count\":");
                builder.Append((messageIds?.Count ?? 0).ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"messages\":[");

                if (messageIds != null)
                {
                    for (int i = 0; i < messageIds.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(',');
                        }

                        builder.Append(GetMessage(chatId, ReadLong(messageIds[i])) ?? "null");
                    }
                }

                builder.Append("]}");
                return builder.ToString();
            }

            // TDLib leaves out the ids it does not know, so a partial answer is still an answer.
            public string GetCustomEmojiStickers(JsonArray? ids)
            {
                var builder = new StringBuilder("{\"@type\":\"stickers\",\"stickers\":[");
                var first = true;

                if (ids != null)
                {
                    foreach (var id in ids)
                    {
                        if (CustomEmoji.TryGetValue(ReadLong(id), out string? sticker))
                        {
                            if (!first)
                            {
                                builder.Append(',');
                            }

                            builder.Append(sticker);
                            first = false;
                        }
                    }
                }

                builder.Append("]}");
                return builder.ToString();
            }

            public string? Match(JsonObject request)
            {
                foreach (var response in _responses)
                {
                    if (Matches(response.Request, request))
                    {
                        return response.Json;
                    }
                }

                return null;
            }

            // Compared as text with quotes dropped, so a pattern can give an int64 id either way.
            private static bool Matches(JsonObject pattern, JsonObject request)
            {
                foreach (var pair in pattern)
                {
                    var actual = request[pair.Key];
                    if (actual == null || Serialize(actual).Trim('"') != Serialize(pair.Value).Trim('"'))
                    {
                        return false;
                    }
                }

                return true;
            }

            public void Unhandled(string type, JsonObject request)
            {
                lock (_unhandled)
                {
                    if (!_unhandled.Add(type))
                    {
                        return;
                    }
                }

                Log("unhandled.log", Serialize(request));
            }

            public static void Log(string file, string text)
            {
                try
                {
                    System.IO.File.AppendAllText(System.IO.Path.Combine(Folder, file), text + Environment.NewLine);
                }
                catch
                {
                    // Diagnostics only: a scenario must not fail because its log could not be written.
                }
            }

            // TDLib writes + and / in base64 as they are, and the reader decodes base64 from the raw
            // bytes, so the default encoder's + would not parse.
            private static readonly JsonWriterOptions _writerOptions = new()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            public static string Serialize(JsonNode? node)
            {
                if (node == null)
                {
                    return "null";
                }

                using var stream = new System.IO.MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, _writerOptions))
                {
                    node.WriteTo(writer);
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
#endif
