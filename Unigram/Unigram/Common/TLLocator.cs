namespace Unigram.Views
{
    public class TLLocator
    {
        private readonly int _session;

        private readonly Unigram.Services.ILifetimeService _lifetimeService;
        private readonly Unigram.Services.ILocaleService _localeService;
        private readonly Unigram.Services.IPasscodeService _passcodeService;
        private readonly Unigram.Services.IPlaybackService _playbackService;

        private readonly Unigram.Services.IDeviceInfoService _deviceInfoService;
        private readonly Unigram.Services.ISettingsService _settingsService;
        private readonly Unigram.Services.IEventAggregator _eventAggregator;
        private readonly Unigram.Services.IProtoService _protoService;
        private readonly Unigram.Services.INetworkService _networkService;
        private readonly Unigram.Services.IGenerationService _generationService;
        private readonly Unigram.Services.ICacheService _cacheService;
        private readonly Unigram.Services.ISessionService _sessionService;
        private readonly Unigram.Services.INotificationsService _notificationsService;

        private Unigram.Services.ISettingsSearchService _settingsSearchService;
        private Unigram.Services.IEmojiSetService _emojiSetService;
        private Unigram.Services.ICloudUpdateService _cloudUpdateService;
        private Unigram.Services.IShortcutsService _shortcutsService;
        private Unigram.Services.IVoipService _voipService;
        private Unigram.Services.IGroupCallService _groupCallService;
        private Unigram.Services.IContactsService _contactsService;
        private Unigram.Services.ILocationService _locationService;
        private Unigram.Services.IThemeService _themeService;
        private Unigram.Services.Factories.IMessageFactory _messageFactory;
        private Unigram.Services.ViewService.IViewService _viewService;
        private Unigram.Services.IStorageService _storageService;
        private Unigram.Services.ITranslateService _translateService;

        public TLLocator(Unigram.Services.ILifetimeService lifetimeService, Unigram.Services.ILocaleService localeService, Unigram.Services.IPasscodeService passcodeService, Unigram.Services.IPlaybackService playbackService, int session, bool active)
        {
            _session = session;

            _lifetimeService = lifetimeService;
            _localeService = localeService;
            _passcodeService = passcodeService;
            _playbackService = playbackService;

            _deviceInfoService = new Unigram.Services.DeviceInfoService();
            _settingsService = new Unigram.Services.SettingsService(_session);
            _eventAggregator = new Unigram.Services.EventAggregator();
            _protoService = new Unigram.Services.ProtoService(
                _session,
                active,
                _deviceInfoService,
                _settingsService,
                _localeService,
                _eventAggregator);
            _networkService = new Unigram.Services.NetworkService(
                _protoService,
                _settingsService,
                _eventAggregator);
            _generationService = new Unigram.Services.GenerationService(
                _protoService,
                _eventAggregator);
            _cacheService = _protoService;
            _sessionService = new Unigram.Services.SessionService(
                _session,
                active,
                _protoService,
                _cacheService,
                _settingsService,
                _eventAggregator,
                _lifetimeService);
            _notificationsService = new Unigram.Services.NotificationsService(
                _protoService,
                _cacheService,
                _settingsService,
                _sessionService,
                _eventAggregator);
        }

        public T Resolve<T>()
        {
            var type = typeof(T);
            if (type == typeof(Unigram.ViewModels.SignIn.SignInViewModel))
            {
                return (T)(object)new Unigram.ViewModels.SignIn.SignInViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _sessionService,
                    _lifetimeService,
                    _notificationsService);
            }
            else if (type == typeof(Unigram.ViewModels.SignIn.SignUpViewModel))
            {
                return (T)(object)new Unigram.ViewModels.SignIn.SignUpViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.SignIn.SignInSentCodeViewModel))
            {
                return (T)(object)new Unigram.ViewModels.SignIn.SignInSentCodeViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.SignIn.SignInPasswordViewModel))
            {
                return (T)(object)new Unigram.ViewModels.SignIn.SignInPasswordViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.SignIn.SignInRecoveryViewModel))
            {
                return (T)(object)new Unigram.ViewModels.SignIn.SignInRecoveryViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.MainViewModel))
            {
                return (T)(object)new Unigram.ViewModels.MainViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _eventAggregator,
                    _notificationsService,
                    _contactsService ??= new Unigram.Services.ContactsService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator),
                    _passcodeService,
                    _lifetimeService,
                    _sessionService,
                    _voipService ??= new Unigram.Services.VoipService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _groupCallService ??= new Unigram.Services.GroupCallService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _settingsSearchService ??= new Unigram.Services.SettingsSearchService(_protoService),
                    _emojiSetService ??= new Unigram.Services.EmojiSetService(
                        _protoService,
                        _settingsService,
                        _eventAggregator),
                    _cloudUpdateService ??= new Unigram.Services.CloudUpdateService(
                        _protoService,
                        _networkService,
                        _eventAggregator),
                    _playbackService,
                    _shortcutsService ??= new Unigram.Services.ShortcutsService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator));
            }
            else if (type == typeof(Unigram.ViewModels.ShareViewModel))
            {
                return (T)(object)new Unigram.ViewModels.ShareViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.SendLocationViewModel))
            {
                return (T)(object)new Unigram.ViewModels.SendLocationViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _locationService ??= new Unigram.Services.LocationService(
                        _protoService,
                        _cacheService));
            }
            else if (type == typeof(Unigram.ViewModels.DialogViewModel))
            {
                return (T)(object)new Unigram.ViewModels.DialogViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _locationService ??= new Unigram.Services.LocationService(
                        _protoService,
                        _cacheService),
                    _notificationsService,
                    _playbackService,
                    _voipService ??= new Unigram.Services.VoipService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _groupCallService ??= new Unigram.Services.GroupCallService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _networkService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _translateService ??= new Unigram.Services.TranslateService(
                        _protoService,
                        _settingsService),
                    _messageFactory ??= new Unigram.Services.Factories.MessageFactory(
                        _protoService,
                        _playbackService));
            }
            else if (type == typeof(Unigram.ViewModels.DialogThreadViewModel))
            {
                return (T)(object)new Unigram.ViewModels.DialogThreadViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _locationService ??= new Unigram.Services.LocationService(
                        _protoService,
                        _cacheService),
                    _notificationsService,
                    _playbackService,
                    _voipService ??= new Unigram.Services.VoipService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _groupCallService ??= new Unigram.Services.GroupCallService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _networkService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _translateService ??= new Unigram.Services.TranslateService(
                        _protoService,
                        _settingsService),
                    _messageFactory ??= new Unigram.Services.Factories.MessageFactory(
                        _protoService,
                        _playbackService));
            }
            else if (type == typeof(Unigram.ViewModels.DialogPinnedViewModel))
            {
                return (T)(object)new Unigram.ViewModels.DialogPinnedViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _locationService ??= new Unigram.Services.LocationService(
                        _protoService,
                        _cacheService),
                    _notificationsService,
                    _playbackService,
                    _voipService ??= new Unigram.Services.VoipService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _groupCallService ??= new Unigram.Services.GroupCallService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _networkService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _translateService ??= new Unigram.Services.TranslateService(
                        _protoService,
                        _settingsService),
                    _messageFactory ??= new Unigram.Services.Factories.MessageFactory(
                        _protoService,
                        _playbackService));
            }
            else if (type == typeof(Unigram.ViewModels.DialogScheduledViewModel))
            {
                return (T)(object)new Unigram.ViewModels.DialogScheduledViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _locationService ??= new Unigram.Services.LocationService(
                        _protoService,
                        _cacheService),
                    _notificationsService,
                    _playbackService,
                    _voipService ??= new Unigram.Services.VoipService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _groupCallService ??= new Unigram.Services.GroupCallService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _networkService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _translateService ??= new Unigram.Services.TranslateService(
                        _protoService,
                        _settingsService),
                    _messageFactory ??= new Unigram.Services.Factories.MessageFactory(
                        _protoService,
                        _playbackService));
            }
            else if (type == typeof(Unigram.ViewModels.DialogEventLogViewModel))
            {
                return (T)(object)new Unigram.ViewModels.DialogEventLogViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _locationService ??= new Unigram.Services.LocationService(
                        _protoService,
                        _cacheService),
                    _notificationsService,
                    _playbackService,
                    _voipService ??= new Unigram.Services.VoipService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _groupCallService ??= new Unigram.Services.GroupCallService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _networkService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _translateService ??= new Unigram.Services.TranslateService(
                        _protoService,
                        _settingsService),
                    _messageFactory ??= new Unigram.Services.Factories.MessageFactory(
                        _protoService,
                        _playbackService));
            }
            else if (type == typeof(Unigram.ViewModels.Drawers.AnimationDrawerViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Drawers.AnimationDrawerViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Drawers.StickerDrawerViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Drawers.StickerDrawerViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.ProfileViewModel))
            {
                return (T)(object)new Unigram.ViewModels.ProfileViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _playbackService,
                    _voipService ??= new Unigram.Services.VoipService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _groupCallService ??= new Unigram.Services.GroupCallService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator,
                        _viewService ??= new Unigram.Services.ViewService.ViewService()),
                    _notificationsService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _translateService ??= new Unigram.Services.TranslateService(
                        _protoService,
                        _settingsService),
                    Resolve<Unigram.ViewModels.Chats.ChatSharedMediaViewModel>(),
                    Resolve<Unigram.ViewModels.Users.UserCommonChatsViewModel>(),
                    Resolve<Unigram.ViewModels.Supergroups.SupergroupMembersViewModel>());
            }
            else if (type == typeof(Unigram.ViewModels.Users.UserCommonChatsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Users.UserCommonChatsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Users.UserCreateViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Users.UserCreateViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupEditViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupEditViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupEditTypeViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupEditTypeViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupEditStickerSetViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupEditStickerSetViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupEditAdministratorViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupEditAdministratorViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupEditRestrictedViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupEditRestrictedViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupEditLinkedChatViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupEditLinkedChatViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupAddAdministratorViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupAddAdministratorViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupAddRestrictedViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupAddRestrictedViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Chats.ChatInviteLinkViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Chats.ChatInviteLinkViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupAdministratorsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupAdministratorsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupBannedViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupBannedViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupPermissionsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupPermissionsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupMembersViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupMembersViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Supergroups.SupergroupReactionsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Supergroups.SupergroupReactionsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Chats.ChatSharedMediaViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Chats.ChatSharedMediaViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _eventAggregator,
                    _playbackService);
            }
            else if (type == typeof(Unigram.ViewModels.Chats.ChatStatisticsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Chats.ChatStatisticsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Chats.MessageStatisticsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Chats.MessageStatisticsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Channels.ChannelCreateStep1ViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Channels.ChannelCreateStep1ViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Channels.ChannelCreateStep2ViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Channels.ChannelCreateStep2ViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.BasicGroups.BasicGroupCreateStep1ViewModel))
            {
                return (T)(object)new Unigram.ViewModels.BasicGroups.BasicGroupCreateStep1ViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.InstantViewModel))
            {
                return (T)(object)new Unigram.ViewModels.InstantViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _translateService ??= new Unigram.Services.TranslateService(
                        _protoService,
                        _settingsService),
                    _messageFactory ??= new Unigram.Services.Factories.MessageFactory(
                        _protoService,
                        _playbackService),
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.LogOutViewModel))
            {
                return (T)(object)new Unigram.ViewModels.LogOutViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _notificationsService,
                    _contactsService ??= new Unigram.Services.ContactsService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator),
                    _passcodeService);
            }
            else if (type == typeof(Unigram.ViewModels.DiagnosticsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.DiagnosticsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.SettingsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.SettingsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _eventAggregator,
                    _settingsSearchService ??= new Unigram.Services.SettingsSearchService(_protoService));
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsAdvancedViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsAdvancedViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _cloudUpdateService ??= new Unigram.Services.CloudUpdateService(
                        _protoService,
                        _networkService,
                        _eventAggregator));
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsPhoneViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsPhoneViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsPhoneSentCodeViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsPhoneSentCodeViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsStorageViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsStorageViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsNetworkViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsNetworkViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsUsernameViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsUsernameViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsSessionsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsSessionsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsWebSessionsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsWebSessionsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsBlockedChatsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsBlockedChatsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsNotificationsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsNotificationsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsNotificationsExceptionsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsNotificationsExceptionsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsDataAndStorageViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsDataAndStorageViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsDataAutoViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsDataAutoViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsProxiesViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsProxiesViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _networkService);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsPrivacyAndSecurityViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsPrivacyAndSecurityViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _contactsService ??= new Unigram.Services.ContactsService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator),
                    _passcodeService,
                    Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowForwardedViewModel>(),
                    Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhoneViewModel>(),
                    Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhotoViewModel>(),
                    Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowStatusViewModel>(),
                    Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowCallsViewModel>(),
                    Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowChatInvitesViewModel>());
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowCallsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowCallsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowP2PCallsViewModel>());
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowP2PCallsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowP2PCallsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowChatInvitesViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowChatInvitesViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowForwardedViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowForwardedViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Privacy.SettingsPrivacyPhoneViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Privacy.SettingsPrivacyPhoneViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhoneViewModel>(),
                    Resolve<Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowFindingByPhoneNumberViewModel>());
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhoneViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhoneViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowFindingByPhoneNumberViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Privacy.SettingsPrivacyAllowFindingByPhoneNumberViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhotoViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhotoViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowStatusViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Privacy.SettingsPrivacyShowStatusViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsProfileViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsProfileViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsPasswordViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsPasswordViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Password.SettingsPasswordIntroViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Password.SettingsPasswordIntroViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Password.SettingsPasswordCreateViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Password.SettingsPasswordCreateViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Password.SettingsPasswordHintViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Password.SettingsPasswordHintViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Password.SettingsPasswordEmailViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Password.SettingsPasswordEmailViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Password.SettingsPasswordConfirmViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Password.SettingsPasswordConfirmViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.Password.SettingsPasswordDoneViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.Password.SettingsPasswordDoneViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsPasscodeViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsPasscodeViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _passcodeService);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsStickersViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsStickersViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsLanguageViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsLanguageViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _localeService);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsAppearanceViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsAppearanceViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _themeService ??= new Unigram.Services.ThemeService(
                        _protoService,
                        _settingsService,
                        _eventAggregator),
                    _emojiSetService ??= new Unigram.Services.EmojiSetService(
                        _protoService,
                        _settingsService,
                        _eventAggregator));
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsThemesViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsThemesViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _themeService ??= new Unigram.Services.ThemeService(
                        _protoService,
                        _settingsService,
                        _eventAggregator));
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsThemeViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsThemeViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _themeService ??= new Unigram.Services.ThemeService(
                        _protoService,
                        _settingsService,
                        _eventAggregator));
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsNightModeViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsNightModeViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _themeService ??= new Unigram.Services.ThemeService(
                        _protoService,
                        _settingsService,
                        _eventAggregator),
                    _locationService ??= new Unigram.Services.LocationService(
                        _protoService,
                        _cacheService));
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsBackgroundsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsBackgroundsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Settings.SettingsShortcutsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Settings.SettingsShortcutsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _shortcutsService ??= new Unigram.Services.ShortcutsService(
                        _protoService,
                        _cacheService,
                        _settingsService,
                        _eventAggregator));
            }
            else if (type == typeof(Unigram.ViewModels.BackgroundViewModel))
            {
                return (T)(object)new Unigram.ViewModels.BackgroundViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.AttachedStickersViewModel))
            {
                return (T)(object)new Unigram.ViewModels.AttachedStickersViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.StickerSetViewModel))
            {
                return (T)(object)new Unigram.ViewModels.StickerSetViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Payments.PaymentAddressViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Payments.PaymentAddressViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Payments.PaymentCredentialsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Payments.PaymentCredentialsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Payments.PaymentFormViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Payments.PaymentFormViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.ChatsNearbyViewModel))
            {
                return (T)(object)new Unigram.ViewModels.ChatsNearbyViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _locationService ??= new Unigram.Services.LocationService(
                        _protoService,
                        _cacheService));
            }
            else if (type == typeof(Unigram.ViewModels.Folders.FoldersViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Folders.FoldersViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Folders.FolderViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Folders.FolderViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.DownloadsViewModel))
            {
                return (T)(object)new Unigram.ViewModels.DownloadsViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _storageService ??= new Unigram.Services.StorageService(_protoService),
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.ChooseSoundViewModel))
            {
                return (T)(object)new Unigram.ViewModels.ChooseSoundViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.ViewModels.Premium.PremiumViewModel))
            {
                return (T)(object)new Unigram.ViewModels.Premium.PremiumViewModel(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator);
            }
            else if (type == typeof(Unigram.Services.IDeviceInfoService))
            {
                return (T)_deviceInfoService;
            }
            else if (type == typeof(Unigram.Services.ISettingsService))
            {
                return (T)_settingsService;
            }
            else if (type == typeof(Unigram.Services.IEventAggregator))
            {
                return (T)_eventAggregator;
            }
            else if (type == typeof(Unigram.Services.IProtoService))
            {
                return (T)_protoService;
            }
            else if (type == typeof(Unigram.Services.INetworkService))
            {
                return (T)_networkService;
            }
            else if (type == typeof(Unigram.Services.IGenerationService))
            {
                return (T)_generationService;
            }
            else if (type == typeof(Unigram.Services.ICacheService))
            {
                return (T)_cacheService;
            }
            else if (type == typeof(Unigram.Services.ISessionService))
            {
                return (T)_sessionService;
            }
            else if (type == typeof(Unigram.Services.INotificationsService))
            {
                return (T)_notificationsService;
            }
            else if (type == typeof(Unigram.Services.ISettingsSearchService))
            {
                return (T)(_settingsSearchService ??= new Unigram.Services.SettingsSearchService(_protoService));
            }
            else if (type == typeof(Unigram.Services.IEmojiSetService))
            {
                return (T)(_emojiSetService ??= new Unigram.Services.EmojiSetService(
                    _protoService,
                    _settingsService,
                    _eventAggregator));
            }
            else if (type == typeof(Unigram.Services.ICloudUpdateService))
            {
                return (T)(_cloudUpdateService ??= new Unigram.Services.CloudUpdateService(
                    _protoService,
                    _networkService,
                    _eventAggregator));
            }
            else if (type == typeof(Unigram.Services.IShortcutsService))
            {
                return (T)(_shortcutsService ??= new Unigram.Services.ShortcutsService(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator));
            }
            else if (type == typeof(Unigram.Services.IVoipService))
            {
                return (T)(_voipService ??= new Unigram.Services.VoipService(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _viewService ??= new Unigram.Services.ViewService.ViewService()));
            }
            else if (type == typeof(Unigram.Services.IGroupCallService))
            {
                return (T)(_groupCallService ??= new Unigram.Services.GroupCallService(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator,
                    _viewService ??= new Unigram.Services.ViewService.ViewService()));
            }
            else if (type == typeof(Unigram.Services.IContactsService))
            {
                return (T)(_contactsService ??= new Unigram.Services.ContactsService(
                    _protoService,
                    _cacheService,
                    _settingsService,
                    _eventAggregator));
            }
            else if (type == typeof(Unigram.Services.ILocationService))
            {
                return (T)(_locationService ??= new Unigram.Services.LocationService(
                    _protoService,
                    _cacheService));
            }
            else if (type == typeof(Unigram.Services.IThemeService))
            {
                return (T)(_themeService ??= new Unigram.Services.ThemeService(
                    _protoService,
                    _settingsService,
                    _eventAggregator));
            }
            else if (type == typeof(Unigram.Services.Factories.IMessageFactory))
            {
                return (T)(_messageFactory ??= new Unigram.Services.Factories.MessageFactory(
                    _protoService,
                    _playbackService));
            }
            else if (type == typeof(Unigram.Services.ViewService.IViewService))
            {
                return (T)(_viewService ??= new Unigram.Services.ViewService.ViewService());
            }
            else if (type == typeof(Unigram.Services.IStorageService))
            {
                return (T)(_storageService ??= new Unigram.Services.StorageService(_protoService));
            }
            else if (type == typeof(Unigram.Services.ITranslateService))
            {
                return (T)(_translateService ??= new Unigram.Services.TranslateService(
                    _protoService,
                    _settingsService));
            }

            return default;
        }
    }
}
