using Windows.Storage;

namespace Unigram.Services.Settings
{
    public class NotificationsSettings : SettingsServiceBase
    {
        public NotificationsSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        private bool? _inAppPreview;
        public bool InAppPreview
        {
            get => _inAppPreview ??= GetValueOrDefault("InAppPreview", true);
            set => AddOrUpdateValue(ref _inAppPreview, "InAppPreview", value);
        }

        private bool? _inAppVibrate;
        public bool InAppVibrate
        {
            get => _inAppVibrate ??= GetValueOrDefault("InAppVibrate", true);
            set => AddOrUpdateValue(ref _inAppVibrate, "InAppVibrate", value);
        }

        private bool? _inAppFlash;
        public bool InAppFlash
        {
            get => _inAppFlash ??= GetValueOrDefault("InAppFlash", true);
            set => AddOrUpdateValue(ref _inAppFlash, "InAppFlash", value);
        }

        private bool? _inAppSounds;
        public bool InAppSounds
        {
            get => _inAppSounds ??= GetValueOrDefault("InAppSounds", true);
            set => AddOrUpdateValue(ref _inAppSounds, "InAppSounds", value);
        }

        private bool? _includeMutedChats;
        public bool IncludeMutedChats
        {
            get => _includeMutedChats ??= GetValueOrDefault("IncludeMutedChats", false);
            set => AddOrUpdateValue(ref _includeMutedChats, "IncludeMutedChats", value);
        }

        private bool? _countUnreadMessages;
        public bool CountUnreadMessages
        {
            get => _countUnreadMessages ??= GetValueOrDefault("CountUnreadMessages", true);
            set => AddOrUpdateValue(ref _countUnreadMessages, "CountUnreadMessages", value);
        }

        private bool? _isPinnedEnabled;
        public bool IsPinnedEnabled
        {
            get => _isPinnedEnabled ??= GetValueOrDefault("IsPinnedEnabled", true);
            set => AddOrUpdateValue(ref _isPinnedEnabled, "IsPinnedEnabled", value);
        }

        private bool? _isContactEnabled;
        public bool IsContactEnabled
        {
            get => _isContactEnabled ??= GetValueOrDefault("IsContactEnabled", true);
            set => AddOrUpdateValue(ref _isContactEnabled, "IsContactEnabled", value);
        }
    }
}
