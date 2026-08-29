//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Native.Calls;
using Telegram.Native;
using Telegram.Services.Settings;

namespace Telegram.Services
{
    public enum DistanceUnits
    {
        Automatic,
        Kilometers,
        Miles
    }

    // One store, one process, no per-instance state -- so there is nothing for a Current to
    // disambiguate, and every read saves a null check.
    public static partial class AppSettings
    {
        private static readonly ISettingsStore _local = ApplicationDataSettingsStore.Local;

        private static StickersSettings _stickers;
        public static StickersSettings Stickers => _stickers ??= new StickersSettings(_local);

        private static EmojiSettings _emoji;
        public static EmojiSettings Emoji => _emoji ??= new EmojiSettings();

        private static AppearanceSettings _appearance;
        public static AppearanceSettings Appearance => _appearance ??= new AppearanceSettings();

        private static DiagnosticsSettings _diagnostics;
        public static DiagnosticsSettings Diagnostics => _diagnostics ??= new DiagnosticsSettings();

        private static PasscodeLockSettings _passcodeLock;
        public static PasscodeLockSettings PasscodeLock => _passcodeLock ??= new PasscodeLockSettings();

        private static PlaybackSettings _playback;
        public static PlaybackSettings Playback => _playback ??= new PlaybackSettings(_local);


        private static VoIPSettings _voip;
        public static VoIPSettings VoIP => _voip ??= new VoIPSettings();

        private static ToolTipSettings _toolTip;
        public static ToolTipSettings ToolTip => _toolTip ??= new ToolTipSettings();

        private static int? _verbosityLevel;
        public static int VerbosityLevel
        {
            get => _verbosityLevel ??= _local.GetValueOrDefault("VerbosityLevel", ApiInfo.IsPackagedRelease ? 4 : 2);
            set => _local.AddOrUpdateValue(ref _verbosityLevel, "VerbosityLevel", value);
        }

        private static int? _distanceUnits;
        public static DistanceUnits DistanceUnits
        {
            get => (DistanceUnits)(_distanceUnits ??= _local.GetValueOrDefault("DistanceUnits", 0));
            set => _local.AddOrUpdateValue(ref _distanceUnits, "DistanceUnits", (int)value);
        }

        private static double? _dialogsWidthRatio;
        public static double DialogsWidthRatio
        {
            get => _dialogsWidthRatio ??= _local.GetValueOrDefault("DialogsWidthRatio", 5d / 14d);
            set => _local.AddOrUpdateValue(ref _dialogsWidthRatio, "DialogsWidthRatio", value);
        }

        private static bool? _isSidebarOpen;
        public static bool IsSidebarOpen
        {
            get => _isSidebarOpen ??= _local.GetValueOrDefault("IsSidebarOpen", true);
            set => _local.AddOrUpdateValue(ref _isSidebarOpen, "IsSidebarOpen", value);
        }

        private static bool? _isAdaptiveWideEnabled;
        public static bool IsAdaptiveWideEnabled
        {
            get => _isAdaptiveWideEnabled ??= _local.GetValueOrDefault("IsAdaptiveWideEnabled", false);
            set => _local.AddOrUpdateValue(ref _isAdaptiveWideEnabled, "IsAdaptiveWideEnabled", value);
        }

        private static bool? _areSmoothTransitionsEnabled;
        public static bool AreSmoothTransitionsEnabled
        {
            get => _areSmoothTransitionsEnabled ??= _local.GetValueOrDefault("AreSmoothTransitionsEnabled", true);
            set => _local.AddOrUpdateValue(ref _areSmoothTransitionsEnabled, "AreSmoothTransitionsEnabled", value);
        }

        private static bool? _areCallsAnimated;
        public static bool AreCallsAnimated
        {
            get => _areCallsAnimated ??= _local.GetValueOrDefault("AreCallsAnimated", true);
            set => _local.AddOrUpdateValue(ref _areCallsAnimated, "AreCallsAnimated", value);
        }

        private static bool? _areMaterialsEnabled;
        public static bool AreMaterialsEnabled
        {
            get => _areMaterialsEnabled ??= _local.GetValueOrDefault("AreMaterialsEnabled", true);
            set => _local.AddOrUpdateValue(ref _areMaterialsEnabled, "AreMaterialsEnabled", value);
        }

        private static bool? _isTrayVisible;
        public static bool IsTrayVisible
        {
            get => _isTrayVisible ??= _local.GetValueOrDefault("IsTrayVisible", Constants.RELEASE);
            set => _local.AddOrUpdateValue(ref _isTrayVisible, "IsTrayVisible", value);
        }

        private static bool? _isLaunchMinimized;
        public static bool IsLaunchMinimized
        {
            get => _isLaunchMinimized ??= _local.GetValueOrDefault("IsLaunchMinimized", false);
            set => _local.AddOrUpdateValue(ref _isLaunchMinimized, "IsLaunchMinimized", value);
        }

        private static bool? _isAccountsSelectorExpanded;
        public static bool IsAccountsSelectorExpanded
        {
            get => _isAccountsSelectorExpanded ??= _local.GetValueOrDefault("IsAccountsSelectorExpanded", false);
            set => _local.AddOrUpdateValue(ref _isAccountsSelectorExpanded, "IsAccountsSelectorExpanded", value);
        }

        private static int[] _accountsSelectorOrder;
        public static int[] AccountsSelectorOrder
        {
            get
            {
                if (_accountsSelectorOrder == null)
                {
                    var value = _local.GetValueOrDefault<string>("AccountsSelectorOrder", null);
                    if (value == null)
                    {
                        _accountsSelectorOrder = Array.Empty<int>();
                    }
                    else
                    {
                        _accountsSelectorOrder = value.Split(',').Select(x => int.Parse(x)).ToArray();
                    }
                }

                return _accountsSelectorOrder;
            }
            set
            {
                _accountsSelectorOrder = value;
                _local.SetValue("AccountsSelectorOrder", value != null ? string.Join(",", value) : null);
            }
        }

        private static bool? _isAllAccountsNotifications;
        public static bool IsAllAccountsNotifications
        {
            get => _isAllAccountsNotifications ??= _local.GetValueOrDefault("IsAllAccountsNotifications", true);
            set => _local.AddOrUpdateValue(ref _isAllAccountsNotifications, "IsAllAccountsNotifications", value);
        }

        private static bool? _useLeftTabsForChats;
        public static bool UseLeftTabsForChats
        {
            get => _useLeftTabsForChats ??= _local.GetValueOrDefault("IsLeftTabsEnabled", true);
            set => _local.AddOrUpdateValue(ref _useLeftTabsForChats, "IsLeftTabsEnabled", value);
        }

        private static bool? _useLeftTabsForForums;
        public static bool UseLeftTabsForForums
        {
            get => _useLeftTabsForForums ??= _local.GetValueOrDefault("UseLeftTabsForForums", false);
            set => _local.AddOrUpdateValue(ref _useLeftTabsForForums, "UseLeftTabsForForums", value);
        }

        private static bool? _swipeToShare;
        public static bool SwipeToShare
        {
            get => _swipeToShare ??= _local.GetValueOrDefault("SwipeToShare", false);
            set => _local.AddOrUpdateValue(ref _swipeToShare, "SwipeToShare", value);
        }

        private static bool? _swipeToReply;
        public static bool SwipeToReply
        {
            get => _swipeToReply ??= _local.GetValueOrDefault("SwipeToReply", true);
            set => _local.AddOrUpdateValue(ref _swipeToReply, "SwipeToReply", value);
        }

        private static bool? _swipeToGoBack;
        public static bool SwipeToGoBack
        {
            get => _swipeToGoBack ??= _local.GetValueOrDefault("SwipeToGoBack", true);
            set => _local.AddOrUpdateValue(ref _swipeToGoBack, "SwipeToGoBack", value);
        }

        private static bool? _fullScreenGallery;
        public static bool FullScreenGallery
        {
            get => _fullScreenGallery ??= _local.GetValueOrDefault("FullScreenGallery", false);
            set => _local.AddOrUpdateValue(ref _fullScreenGallery, "FullScreenGallery", value);
        }

        private static bool? _disableHighlightWords;
        public static bool UseSystemSpellChecker
        {
            get => !(_disableHighlightWords ??= _local.GetValueOrDefault("DisableHighlightWords", false));
            set => _local.AddOrUpdateValue(ref _disableHighlightWords, "DisableHighlightWords", !value);
        }

        private static bool? _isSendByEnterEnabled;
        public static bool IsSendByEnterEnabled
        {
            get => _isSendByEnterEnabled ??= _local.GetValueOrDefault("IsSendByEnterEnabled", true);
            set => _local.AddOrUpdateValue(ref _isSendByEnterEnabled, "IsSendByEnterEnabled", value);
        }

        private static bool? _isReplaceEmojiEnabled;
        public static bool IsReplaceEmojiEnabled
        {
            get => _isReplaceEmojiEnabled ??= _local.GetValueOrDefault("IsReplaceEmojiEnabled", true);
            set => _local.AddOrUpdateValue(ref _isReplaceEmojiEnabled, "IsReplaceEmojiEnabled", value);
        }

        private static bool? _isContactsSortedByEpoch;
        public static bool IsContactsSortedByEpoch
        {
            get => _isContactsSortedByEpoch ??= _local.GetValueOrDefault("IsContactsSortedByEpoch", true);
            set => _local.AddOrUpdateValue(ref _isContactsSortedByEpoch, "IsContactsSortedByEpoch", value);
        }


        private static bool? _isAutoPlayAnimationsEnabled;
        public static bool AutoPlayAnimations
        {
            get => _isAutoPlayAnimationsEnabled ??= _local.GetValueOrDefault("IsAutoPlayEnabled", true);
            set => _local.AddOrUpdateValue(ref _isAutoPlayAnimationsEnabled, "IsAutoPlayEnabled", value);
        }

        private static bool? _isAutoPlayVideosEnabled;
        public static bool AutoPlayVideos
        {
            get => _isAutoPlayVideosEnabled ??= _local.GetValueOrDefault("IsAutoPlayVideosEnabled", true);
            set => _local.AddOrUpdateValue(ref _isAutoPlayVideosEnabled, "IsAutoPlayVideosEnabled", value);
        }

        private static bool? _autoPlayStickers;
        public static bool AutoPlayStickers
        {
            get => _autoPlayStickers ??= _local.GetValueOrDefault("AutoPlayStickers", true);
            set => _local.AddOrUpdateValue(ref _autoPlayStickers, "AutoPlayStickers", value);
        }

        private static bool? _autoPlayStickersInChats;
        public static bool AutoPlayStickersInChats
        {
            get => _autoPlayStickersInChats ??= _local.GetValueOrDefault("AutoPlayStickersInChats", true);
            set => _local.AddOrUpdateValue(ref _autoPlayStickersInChats, "AutoPlayStickersInChats", value);
        }

        private static bool? _autoPlayEmoji;
        public static bool AutoPlayEmoji
        {
            get => _autoPlayEmoji ??= _local.GetValueOrDefault("AutoPlayEmoji", true);
            set => _local.AddOrUpdateValue(ref _autoPlayEmoji, "AutoPlayEmoji", value);
        }

        private static bool? _autoPlayEmojiInChats;
        public static bool AutoPlayEmojiInChats
        {
            get => _autoPlayEmojiInChats ??= _local.GetValueOrDefault("AutoPlayEmojiInChats", true);
            set => _local.AddOrUpdateValue(ref _autoPlayEmojiInChats, "AutoPlayEmojiInChats", value);
        }

        private static bool? _isPowerSavingEnabled;
        public static bool IsPowerSavingEnabled
        {
            get => _isPowerSavingEnabled ??= _local.GetValueOrDefault("IsPowerSavingEnabled", true);
            set => _local.AddOrUpdateValue(ref _isPowerSavingEnabled, "IsPowerSavingEnabled", value);
        }

        private static bool? _sendLargePhotos;
        public static bool SendLargePhotos
        {
            get => _sendLargePhotos ??= Diagnostics.GetValueOrDefault("SendLargePhotos", false);
            set => Diagnostics.AddOrUpdateValue(ref _sendLargePhotos, "SendLargePhotos", value);
        }

        private static bool? _isStreamingEnabled;
        public static bool IsStreamingEnabled
        {
            get => _isStreamingEnabled ??= _local.GetValueOrDefault("IsStreamingEnabled", true);
            set => _local.AddOrUpdateValue(ref _isStreamingEnabled, "IsStreamingEnabled", value);
        }

        private static bool? _isDownloadFolderEnabled;
        public static bool IsDownloadFolderEnabled
        {
            get => _isDownloadFolderEnabled ??= _local.GetValueOrDefault("IsDownloadFolderEnabled", true);
            set => _local.AddOrUpdateValue(ref _isDownloadFolderEnabled, "IsDownloadFolderEnabled", value);
        }

        private static double? _volumeLevel;
        public static double VolumeLevel
        {
            get => _volumeLevel ??= _local.GetValueOrDefault("VolumeLevel", 1d);
            set => _local.AddOrUpdateValue(ref _volumeLevel, "VolumeLevel", value);
        }

        private static bool? _volumeMuted;
        public static bool VolumeMuted
        {
            get => _volumeMuted ??= _local.GetValueOrDefault("VolumeMuted", false);
            set => _local.AddOrUpdateValue(ref _volumeMuted, "VolumeMuted", value);
        }

        private static Vector2? _pencil;
        public static Vector2 Pencil
        {
            get
            {
                if (_pencil == null)
                {
                    var offset = _local.GetValueOrDefault("PencilOffset", 0f);
                    var thickness = _local.GetValueOrDefault("PencilThickness", 0.22f);

                    _pencil = new Vector2(offset, thickness);
                }

                return _pencil ?? new Vector2(0f, 0.22f);
            }
            set
            {
                _pencil = value;
                _local.SetValue("PencilOffset", value.X);
                _local.SetValue("PencilThickness", value.Y);
            }
        }

        private static int? _previousSession;
        public static int PreviousSession
        {
            get => _previousSession ??= _local.GetValueOrDefault("PreviousSession", 0);
            set => _local.AddOrUpdateValue(ref _previousSession, "PreviousSession", value);
        }

        private static int? _activeSession;
        public static int ActiveSession
        {
            get => _activeSession ??= _local.GetValueOrDefault("SelectedAccount", 0);
            set => _local.AddOrUpdateValue(ref _activeSession, "SelectedAccount", value);
        }

        private static string _languagePackId;
        public static string LanguagePackId
        {
            get => _languagePackId ??= _local.GetValueOrDefault("LanguagePackId", LocaleService.SystemLanguageId());
            set => _local.AddOrUpdateValue(ref _languagePackId, "LanguagePackId", value);
        }

        private static string _languagePluralId;
        public static string LanguagePluralId
        {
            get => _languagePluralId ??= _local.GetValueOrDefault("LanguagePluralId", LocaleService.SystemLanguageId());
            set => _local.AddOrUpdateValue(ref _languagePluralId, "LanguagePluralId", value);
        }

        private static string _languageBaseId;
        public static string LanguageBaseId
        {
            get => _languageBaseId ??= _local.GetValueOrDefault("LanguageBaseId", LocaleService.SystemLanguageId());
            set => _local.AddOrUpdateValue(ref _languageBaseId, "LanguageBaseId", value);
        }

        private static string _languageShownId;
        public static string LanguageShownId
        {
            get => _languageShownId ??= _local.GetValueOrDefault<string>("LanguageShownId", null);
            set => _local.AddOrUpdateValue(ref _languageShownId, "LanguageShownId", value);
        }

        private static bool? _installBetaUpdates;
        public static bool InstallBetaUpdates
        {
            get => _installBetaUpdates ??= _local.GetValueOrDefault("InstallBetaUpdates", true);
            set => _local.AddOrUpdateValue(ref _installBetaUpdates, "InstallBetaUpdates", value);
        }

        private static int? _enabledProxyId;
        public static int EnabledProxyId
        {
            get => _enabledProxyId ??= _local.GetValueOrDefault("EnabledProxyId", 0);
            set => _local.AddOrUpdateValue(ref _enabledProxyId, "EnabledProxyId", value);
        }

        private static bool? _migratedProxy;
        public static bool MigratedProxy
        {
            get => _migratedProxy ??= _local.GetValueOrDefault("MigratedProxy", false);
            set => _local.AddOrUpdateValue(ref _migratedProxy, "MigratedProxy", value);
        }

        private static int? _useLessData;
        public static VoipDataSaving UseLessData
        {
            get => (VoipDataSaving)(_useLessData ??= _local.GetValueOrDefault("UseLessData", 0));
            set => _local.AddOrUpdateValue(ref _useLessData, "UseLessData", (int)value);
        }

        // Removing the toast collections is a one-off for the whole app rather than a
        // notification preference, so it does not belong on the per-account section.
        private static bool? _hasRemovedCollections;
        public static bool HasRemovedCollections
        {
            get => _hasRemovedCollections ??= _local.GetValueOrDefault("HasRemovedCollections", false);
            set => _local.AddOrUpdateValue(ref _hasRemovedCollections, "HasRemovedCollections", value);
        }

        private static int? _reportsCount;
        public static int ReportsCount
        {
            get => _reportsCount ??= _local.GetValueOrDefault("ReportsCount", 100);
            set => _local.AddOrUpdateValue(ref _reportsCount, "ReportsCount", value);
        }

        private static long? _reportsDate;
        public static DateTime ReportsDate
        {
            get => DateTime.FromFileTimeUtc(_reportsDate ??= _local.GetValueOrDefault("ReportsDate", DateTime.Now.ToFileTimeUtc()));
            set => _local.AddOrUpdateValue(ref _reportsDate, "ReportsDate", value.ToFileTimeUtc());
        }

        private static string _anonymousUserId;
        public static string AnonymousUserId
        {
            get
            {
                if (_anonymousUserId != null)
                {
                    return _anonymousUserId;
                }

                var value = _local.GetValueOrDefault<string>("AnonymousUserId", null);
                if (value == null)
                {
                    value = Guid.NewGuid().ToString();
                    _local.SetValue("AnonymousUserId", value);
                }

                return _anonymousUserId = value;
            }
            set => _local.AddOrUpdateValue(ref _anonymousUserId, "AnonymousUserId", value);
        }

        public static void Initialize()
        {
            if (Diagnostics.LastUpdateVersion < Constants.BuildNumber)
            {
                var updateCount = Diagnostics.UpdateCount;

                Diagnostics.LastUpdateVersion = Constants.BuildNumber;
                Diagnostics.UpdateCount++;

                if (updateCount > 0)
                {
                    if (!_local.ContainsKey("IsLeftTabsEnabled"))
                    {
                        _local.SetValue("IsLeftTabsEnabled", false);
                        _useLeftTabsForChats = false;
                    }
                }
            }

            MigrateContainers();

            LottieAnimation.UseTLottie = Diagnostics.UseTLottieRenderer;
        }

        // Session 0 predates multi-account: its settings were addressed as the root container and
        // stayed there when per-account containers arrived, so the root holds the first account's
        // settings mixed in with the app-wide ones. These are the keys on the wrong side.
        private static readonly string[] _accountScopedKeys = new[]
        {
            "InAppPreview",
            "InAppVibrate",
            "InAppFlash",
            "InAppSounds",
            "ShowName",
            "ShowText",
            "ShowReply",
            "IncludeMutedChats",
            "IncludeMutedChatsInFolderCounters",
            "CountUnreadMessages",
            "IsSecretPreviewsEnabled",
            "LastMessageTtl"
        };

        // HasRemovedCollections is deliberately absent: it was on NotificationsSettings but is
        // app-wide, and was already being written to the root through Current.
        private static readonly string[] _appScopedKeys = new[]
        {
            "IsReplaceEmojiEnabled",
            "IsContactsSortedByEpoch",
            "UseLeftTabsForForums",
            "UseLessData",
            "DistanceUnits",
            "VolumeMuted"
        };

        // Settings that were app-wide and are not any more. Unlike the keys above these are not
        // moving to one account: every account inherits the value it had been seeing, or the
        // multi-account user silently loses it on all but the first.
        private static readonly string[] _unsharedKeys = new[]
        {
            "HideArchivedChats",
            "IsTranslateEnabled",
            "IsTranslateAllEnabled",
            "TranslateTo",
            "DoNotTranslate"
        };

        // Every move is a copy followed by a delete, so a second pass finds nothing to do and no
        // version marker is needed. A downgrade that writes the root again is migrated afresh,
        // which is right: the older build's value is then the newer one.
        private static void MigrateContainers()
        {
            ISettingsStore zero = null;

            foreach (var key in _accountScopedKeys)
            {
                if (_local.TryGetValue(key, out object value))
                {
                    zero ??= _local.GetContainer("0");
                    zero.SetValue(key, value);
                    _local.Remove(key);
                }
            }

            // Snapshot: the loop writes into the containers it is walking.
            var names = _local.ContainerNames.ToArray();
            var active = ActiveSession;

            foreach (var name in names)
            {
                // Session 0 wrote to the root, so its copy of an app-wide key is already in place.
                if (!int.TryParse(name, out int id) || id == 0 || !_local.TryGetContainer(name, out var container))
                {
                    continue;
                }

                foreach (var key in _appScopedKeys)
                {
                    if (container.TryGetValue(key, out object value))
                    {
                        // Only one value can survive, and it is the one the user is looking at.
                        if (id == active)
                        {
                            _local.SetValue(key, value);
                        }

                        container.Remove(key);
                    }
                }
            }

            Unshare(names);
            UnshareRecentEmoji(names);

            _local.Flush();
        }

        // Recent emoji live in a container rather than at the root, so they do not fit Unshare.
        private static void UnshareRecentEmoji(string[] names)
        {
            if (!_local.TryGetContainer("Emoji", out var shared))
            {
                return;
            }

            foreach (var key in new[] { "RecentEmoji", "RecentEmojiFilledDefault" })
            {
                if (!shared.TryGetValue(key, out object value))
                {
                    continue;
                }

                foreach (var name in names)
                {
                    if (int.TryParse(name, out _) && _local.TryGetContainer(name, out var account))
                    {
                        account.GetContainer("Emoji").SetValue(key, value);
                    }
                }

                shared.Remove(key);
            }
        }

        private static void Unshare(string[] names)
        {
            foreach (var key in _unsharedKeys)
            {
                if (!_local.TryGetValue(key, out object value))
                {
                    continue;
                }

                foreach (var name in names)
                {
                    if (int.TryParse(name, out _) && _local.TryGetContainer(name, out var account))
                    {
                        account.SetValue(key, value);
                    }
                }

                _local.Remove(key);
            }
        }
    }
}
