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
            get
            {
                if (_inAppPreview == null)
                    _inAppPreview = GetValueOrDefault("InAppPreview", true);

                return _inAppPreview ?? true;
            }
            set
            {
                _inAppPreview = value;
                AddOrUpdateValue("InAppPreview", value);
            }
        }

        private bool? _inAppVibrate;
        public bool InAppVibrate
        {
            get
            {
                if (_inAppVibrate == null)
                    _inAppVibrate = GetValueOrDefault("InAppVibrate", true);

                return _inAppVibrate ?? true;
            }
            set
            {
                _inAppVibrate = value;
                AddOrUpdateValue("InAppVibrate", value);
            }
        }

        private bool? _inAppFlash;
        public bool InAppFlash
        {
            get
            {
                if (_inAppFlash == null)
                    _inAppFlash = GetValueOrDefault("InAppFlash", true);

                return _inAppFlash ?? true;
            }
            set
            {
                _inAppFlash = value;
                AddOrUpdateValue("InAppFlash", value);
            }
        }

        private bool? _inAppSounds;
        public bool InAppSounds
        {
            get
            {
                if (_inAppSounds == null)
                    _inAppSounds = GetValueOrDefault("InAppSounds", true);

                return _inAppSounds ?? true;
            }
            set
            {
                _inAppSounds = value;
                AddOrUpdateValue("InAppSounds", value);
            }
        }

        private bool? _includeMutedChats;
        public bool IncludeMutedChats
        {
            get
            {
                if (_includeMutedChats == null)
                    _includeMutedChats = GetValueOrDefault("IncludeMutedChats", false);

                return _includeMutedChats ?? false;
            }
            set
            {
                _includeMutedChats = value;
                AddOrUpdateValue("IncludeMutedChats", value);
            }
        }

        private bool? _countUnreadMessages;
        public bool CountUnreadMessages
        {
            get
            {
                if (_countUnreadMessages == null)
                    _countUnreadMessages = GetValueOrDefault("CountUnreadMessages", true);

                return _countUnreadMessages ?? true;
            }
            set
            {
                _countUnreadMessages = value;
                AddOrUpdateValue("CountUnreadMessages", value);
            }
        }

        private bool? _isPinnedEnabled;
        public bool IsPinnedEnabled
        {
            get
            {
                if (_isPinnedEnabled == null)
                    _isPinnedEnabled = GetValueOrDefault("IsPinnedEnabled", true);

                return _isPinnedEnabled ?? true;
            }
            set
            {
                _isPinnedEnabled = value;
                AddOrUpdateValue("IsPinnedEnabled", value);
            }
        }

        private bool? _isContactEnabled;
        public bool IsContactEnabled
        {
            get
            {
                if (_isContactEnabled == null)
                    _isContactEnabled = GetValueOrDefault("IsContactEnabled", true);

                return _isContactEnabled ?? true;
            }
            set
            {
                _isContactEnabled = value;
                AddOrUpdateValue("IsContactEnabled", value);
            }
        }
    }
}
