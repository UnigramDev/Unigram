//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Views
{
    public partial class TypeLocator
    {
        private readonly int _session;

        private readonly Telegram.Services.ILifetimeService _lifetimeService;
        private readonly Telegram.Services.ILocaleService _localeService;
        private readonly Telegram.Services.IPasscodeService _passcodeService;
        private readonly Telegram.Services.IPlaybackService _playbackService;

        private readonly Telegram.Services.IDeviceInfoService _deviceInfoService;
        private readonly Telegram.Services.ISettingsService _settingsService;
        private readonly Telegram.Services.IEventAggregator _eventAggregator;
        private readonly Telegram.Services.IClientService _clientService;
        private readonly Telegram.Services.INetworkService _networkService;
        private readonly Telegram.Services.IGenerationService _generationService;
        private readonly Telegram.Services.ISessionService _sessionService;
        private readonly Telegram.Services.INotificationsService _notificationsService;
        private readonly Telegram.Services.ViewService.IViewService _viewService;
        private readonly Telegram.Services.IVoipService _voipService;

        private Telegram.Services.ISettingsSearchService _settingsSearchService;
        private Telegram.Services.ICloudUpdateService _cloudUpdateService;
        private Telegram.Services.IShortcutsService _shortcutsService;
        private Telegram.Services.IVoipGroupService _voipGroupService;
        private Telegram.Services.IContactsService _contactsService;
        private Telegram.Services.ILocationService _locationService;
        private Telegram.Services.IThemeService _themeService;
        private Telegram.Services.Factories.IMessageFactory _messageFactory;
        private Telegram.Services.IStorageService _storageService;
        private Telegram.Services.ITranslateService _translateService;
        private Telegram.Services.IProfilePhotoService _profilePhotoService;

        public TypeLocator(Telegram.Services.ILifetimeService lifetimeService, Telegram.Services.ILocaleService localeService, Telegram.Services.IPasscodeService passcodeService, Telegram.Services.IPlaybackService playbackService, int session, bool active)
        {
            _session = session;

            _lifetimeService = lifetimeService;
            _localeService = localeService;
            _passcodeService = passcodeService;
            _playbackService = playbackService;

            _deviceInfoService = new Telegram.Services.DeviceInfoService();
            _settingsService = new Telegram.Services.SettingsService(_session);
            _eventAggregator = new Telegram.Services.EventAggregator();
            _clientService = new Telegram.Services.ClientService(
                _session,
                active,
                _deviceInfoService,
                _settingsService,
                _localeService,
                _eventAggregator);
            _networkService = new Telegram.Services.NetworkService(
                _clientService,
                _settingsService,
                _eventAggregator);
            _generationService = new Telegram.Services.GenerationService(
                _clientService,
                _eventAggregator);
            _sessionService = new Telegram.Services.SessionService(
                _session,
                active,
                _clientService,
                _settingsService,
                _eventAggregator,
                _lifetimeService);
            _notificationsService = new Telegram.Services.NotificationsService(
                _clientService,
                _settingsService,
                _sessionService,
                _eventAggregator);
            _viewService = new Telegram.Services.ViewService.ViewService();
            _voipService = new Telegram.Services.VoipService(
                _clientService,
                _settingsService,
                _eventAggregator,
                _viewService);
        }

        public T Resolve<T>()
        {
            switch (typeof(T).FullName)
            {
                case "Telegram.ViewModels.Authorization.AuthorizationViewModel":
                    return (T)(object)new Telegram.ViewModels.Authorization.AuthorizationViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _sessionService,
                        _lifetimeService,
                        _notificationsService);
                case "Telegram.ViewModels.Authorization.AuthorizationRegistrationViewModel":
                    return (T)(object)new Telegram.ViewModels.Authorization.AuthorizationRegistrationViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Authorization.AuthorizationCodeViewModel":
                    return (T)(object)new Telegram.ViewModels.Authorization.AuthorizationCodeViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Authorization.AuthorizationPasswordViewModel":
                    return (T)(object)new Telegram.ViewModels.Authorization.AuthorizationPasswordViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Authorization.AuthorizationRecoveryViewModel":
                    return (T)(object)new Telegram.ViewModels.Authorization.AuthorizationRecoveryViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Authorization.AuthorizationEmailAddressViewModel":
                    return (T)(object)new Telegram.ViewModels.Authorization.AuthorizationEmailAddressViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Authorization.AuthorizationEmailCodeViewModel":
                    return (T)(object)new Telegram.ViewModels.Authorization.AuthorizationEmailCodeViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.MainViewModel":
                    return (T)(object)new Telegram.ViewModels.MainViewModel(
                        _clientService,
                        _settingsService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _eventAggregator,
                        _notificationsService,
                        _contactsService ??= new Telegram.Services.ContactsService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _passcodeService,
                        _lifetimeService,
                        _sessionService,
                        _voipService,
                        _voipGroupService ??= new Telegram.Services.VoipGroupService(
                            _clientService,
                            _settingsService,
                            _eventAggregator,
                            _viewService),
                        _settingsSearchService ??= new Telegram.Services.SettingsSearchService(_clientService),
                        _cloudUpdateService ??= new Telegram.Services.CloudUpdateService(
                            _clientService,
                            _networkService,
                            _eventAggregator),
                        _playbackService,
                        _shortcutsService ??= new Telegram.Services.ShortcutsService(
                            _clientService,
                            _settingsService,
                            _eventAggregator));
                case "Telegram.ViewModels.ContactsViewModel":
                    return (T)(object)new Telegram.ViewModels.ContactsViewModel(
                        _clientService,
                        _settingsService,
                        _voipService,
                        _eventAggregator);
                case "Telegram.ViewModels.CallsViewModel":
                    return (T)(object)new Telegram.ViewModels.CallsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.ChooseChatsViewModel":
                    return (T)(object)new Telegram.ViewModels.ChooseChatsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.SendLocationViewModel":
                    return (T)(object)new Telegram.ViewModels.SendLocationViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _locationService ??= new Telegram.Services.LocationService(_clientService));
                case "Telegram.ViewModels.DialogViewModel":
                    return (T)(object)new Telegram.ViewModels.DialogViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _locationService ??= new Telegram.Services.LocationService(_clientService),
                        _notificationsService,
                        _playbackService,
                        _voipService,
                        _voipGroupService ??= new Telegram.Services.VoipGroupService(
                            _clientService,
                            _settingsService,
                            _eventAggregator,
                            _viewService),
                        _networkService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _translateService ??= new Telegram.Services.TranslateService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _messageFactory ??= new Telegram.Services.Factories.MessageFactory(
                            _clientService,
                            _playbackService));
                case "Telegram.ViewModels.DialogThreadViewModel":
                    return (T)(object)new Telegram.ViewModels.DialogThreadViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _locationService ??= new Telegram.Services.LocationService(_clientService),
                        _notificationsService,
                        _playbackService,
                        _voipService,
                        _voipGroupService ??= new Telegram.Services.VoipGroupService(
                            _clientService,
                            _settingsService,
                            _eventAggregator,
                            _viewService),
                        _networkService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _translateService ??= new Telegram.Services.TranslateService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _messageFactory ??= new Telegram.Services.Factories.MessageFactory(
                            _clientService,
                            _playbackService));
                case "Telegram.ViewModels.DialogBusinessRepliesViewModel":
                    return (T)(object)new Telegram.ViewModels.DialogBusinessRepliesViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _locationService ??= new Telegram.Services.LocationService(_clientService),
                        _notificationsService,
                        _playbackService,
                        _voipService,
                        _voipGroupService ??= new Telegram.Services.VoipGroupService(
                            _clientService,
                            _settingsService,
                            _eventAggregator,
                            _viewService),
                        _networkService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _translateService ??= new Telegram.Services.TranslateService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _messageFactory ??= new Telegram.Services.Factories.MessageFactory(
                            _clientService,
                            _playbackService));
                case "Telegram.ViewModels.DialogSavedViewModel":
                    return (T)(object)new Telegram.ViewModels.DialogSavedViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _locationService ??= new Telegram.Services.LocationService(_clientService),
                        _notificationsService,
                        _playbackService,
                        _voipService,
                        _voipGroupService ??= new Telegram.Services.VoipGroupService(
                            _clientService,
                            _settingsService,
                            _eventAggregator,
                            _viewService),
                        _networkService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _translateService ??= new Telegram.Services.TranslateService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _messageFactory ??= new Telegram.Services.Factories.MessageFactory(
                            _clientService,
                            _playbackService));
                case "Telegram.ViewModels.DialogPinnedViewModel":
                    return (T)(object)new Telegram.ViewModels.DialogPinnedViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _locationService ??= new Telegram.Services.LocationService(_clientService),
                        _notificationsService,
                        _playbackService,
                        _voipService,
                        _voipGroupService ??= new Telegram.Services.VoipGroupService(
                            _clientService,
                            _settingsService,
                            _eventAggregator,
                            _viewService),
                        _networkService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _translateService ??= new Telegram.Services.TranslateService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _messageFactory ??= new Telegram.Services.Factories.MessageFactory(
                            _clientService,
                            _playbackService));
                case "Telegram.ViewModels.DialogScheduledViewModel":
                    return (T)(object)new Telegram.ViewModels.DialogScheduledViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _locationService ??= new Telegram.Services.LocationService(_clientService),
                        _notificationsService,
                        _playbackService,
                        _voipService,
                        _voipGroupService ??= new Telegram.Services.VoipGroupService(
                            _clientService,
                            _settingsService,
                            _eventAggregator,
                            _viewService),
                        _networkService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _translateService ??= new Telegram.Services.TranslateService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _messageFactory ??= new Telegram.Services.Factories.MessageFactory(
                            _clientService,
                            _playbackService));
                case "Telegram.ViewModels.DialogEventLogViewModel":
                    return (T)(object)new Telegram.ViewModels.DialogEventLogViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _locationService ??= new Telegram.Services.LocationService(_clientService),
                        _notificationsService,
                        _playbackService,
                        _voipService,
                        _voipGroupService ??= new Telegram.Services.VoipGroupService(
                            _clientService,
                            _settingsService,
                            _eventAggregator,
                            _viewService),
                        _networkService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _translateService ??= new Telegram.Services.TranslateService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _messageFactory ??= new Telegram.Services.Factories.MessageFactory(
                            _clientService,
                            _playbackService));
                case "Telegram.ViewModels.Drawers.AnimationDrawerViewModel":
                    return (T)(object)new Telegram.ViewModels.Drawers.AnimationDrawerViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Drawers.StickerDrawerViewModel":
                    return (T)(object)new Telegram.ViewModels.Drawers.StickerDrawerViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Drawers.EmojiDrawerViewModel":
                    return (T)(object)new Telegram.ViewModels.Drawers.EmojiDrawerViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Drawers.EffectDrawerViewModel":
                    return (T)(object)new Telegram.ViewModels.Drawers.EffectDrawerViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.CreateChatPhotoViewModel":
                    return (T)(object)new Telegram.ViewModels.CreateChatPhotoViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.ProfileViewModel":
                    return (T)(object)new Telegram.ViewModels.ProfileViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _playbackService,
                        _voipService,
                        _voipGroupService ??= new Telegram.Services.VoipGroupService(
                            _clientService,
                            _settingsService,
                            _eventAggregator,
                            _viewService),
                        _notificationsService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _translateService ??= new Telegram.Services.TranslateService(
                            _clientService,
                            _settingsService,
                            _eventAggregator));
                case "Telegram.ViewModels.Profile.ProfileStoriesTabViewModel":
                    return (T)(object)new Telegram.ViewModels.Profile.ProfileStoriesTabViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Profile.ProfileMembersTabViewModel":
                    return (T)(object)new Telegram.ViewModels.Profile.ProfileMembersTabViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Profile.ProfileGroupsTabViewModel":
                    return (T)(object)new Telegram.ViewModels.Profile.ProfileGroupsTabViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Profile.ProfileChannelsTabViewModel":
                    return (T)(object)new Telegram.ViewModels.Profile.ProfileChannelsTabViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Profile.ProfileSavedChatsTabViewModel":
                    return (T)(object)new Telegram.ViewModels.Profile.ProfileSavedChatsTabViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Users.UserEditViewModel":
                    return (T)(object)new Telegram.ViewModels.Users.UserEditViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _profilePhotoService ??= new Telegram.Services.ProfilePhotoService(_clientService));
                case "Telegram.ViewModels.Supergroups.SupergroupEditViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupEditViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _profilePhotoService ??= new Telegram.Services.ProfilePhotoService(_clientService));
                case "Telegram.ViewModels.Supergroups.SupergroupEditTypeViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupEditTypeViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupEditStickerSetViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupEditStickerSetViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupEditAdministratorViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupEditAdministratorViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupEditRestrictedViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupEditRestrictedViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupEditLinkedChatViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupEditLinkedChatViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupChooseMemberViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupChooseMemberViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Chats.ChatInviteLinkViewModel":
                    return (T)(object)new Telegram.ViewModels.Chats.ChatInviteLinkViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupAdministratorsViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupAdministratorsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupBannedViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupBannedViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupPermissionsViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupPermissionsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupMembersViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupMembersViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupReactionsViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupReactionsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Chats.ChatStatisticsViewModel":
                    return (T)(object)new Telegram.ViewModels.Chats.ChatStatisticsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Chats.ChatBoostsViewModel":
                    return (T)(object)new Telegram.ViewModels.Chats.ChatBoostsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Chats.ChatRevenueViewModel":
                    return (T)(object)new Telegram.ViewModels.Chats.ChatRevenueViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Chats.ChatStarsViewModel":
                    return (T)(object)new Telegram.ViewModels.Chats.ChatStarsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Chats.MessageStatisticsViewModel":
                    return (T)(object)new Telegram.ViewModels.Chats.MessageStatisticsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Create.NewContactViewModel":
                    return (T)(object)new Telegram.ViewModels.Create.NewContactViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Create.NewChannelViewModel":
                    return (T)(object)new Telegram.ViewModels.Create.NewChannelViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _profilePhotoService ??= new Telegram.Services.ProfilePhotoService(_clientService));
                case "Telegram.ViewModels.Create.NewGroupViewModel":
                    return (T)(object)new Telegram.ViewModels.Create.NewGroupViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _profilePhotoService ??= new Telegram.Services.ProfilePhotoService(_clientService));
                case "Telegram.ViewModels.InstantViewModel":
                    return (T)(object)new Telegram.ViewModels.InstantViewModel(
                        _clientService,
                        _settingsService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _translateService ??= new Telegram.Services.TranslateService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _messageFactory ??= new Telegram.Services.Factories.MessageFactory(
                            _clientService,
                            _playbackService),
                        _eventAggregator);
                case "Telegram.ViewModels.LogOutViewModel":
                    return (T)(object)new Telegram.ViewModels.LogOutViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _notificationsService,
                        _contactsService ??= new Telegram.Services.ContactsService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _passcodeService);
                case "Telegram.ViewModels.DiagnosticsViewModel":
                    return (T)(object)new Telegram.ViewModels.DiagnosticsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Chats.ChatStoriesViewModel":
                    return (T)(object)new Telegram.ViewModels.Chats.ChatStoriesViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.SettingsViewModel":
                    return (T)(object)new Telegram.ViewModels.SettingsViewModel(
                        _clientService,
                        _settingsService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _eventAggregator,
                        _settingsSearchService ??= new Telegram.Services.SettingsSearchService(_clientService));
                case "Telegram.ViewModels.Settings.SettingsAdvancedViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsAdvancedViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _cloudUpdateService ??= new Telegram.Services.CloudUpdateService(
                            _clientService,
                            _networkService,
                            _eventAggregator));
                case "Telegram.ViewModels.Settings.SettingsStorageViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsStorageViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsNetworkViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsNetworkViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsUsernameViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsUsernameViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsSessionsViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsSessionsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsWebSessionsViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsWebSessionsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsBlockedChatsViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsBlockedChatsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsNotificationsViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsNotificationsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsNotificationsExceptionsViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsNotificationsExceptionsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsDataAndStorageViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsDataAndStorageViewModel(
                        _clientService,
                        _settingsService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsDataAutoViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsDataAutoViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsProxyViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsProxyViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _networkService);
                case "Telegram.ViewModels.Settings.SettingsQuickReactionViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsQuickReactionViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsPrivacyAndSecurityViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsPrivacyAndSecurityViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _contactsService ??= new Telegram.Services.ContactsService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _passcodeService);
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowCallsViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowCallsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowP2PCallsViewModel>());
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowP2PCallsViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowP2PCallsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowChatInvitesViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowChatInvitesViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowForwardedViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowForwardedViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyPhoneViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyPhoneViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhoneViewModel>(),
                        Resolve<Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowFindingByPhoneNumberViewModel>());
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhoneViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhoneViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowFindingByPhoneNumberViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowFindingByPhoneNumberViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhotoViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowPhotoViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _profilePhotoService ??= new Telegram.Services.ProfilePhotoService(_clientService));
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowStatusViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowStatusViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowBioViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowBioViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowBirthdateViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyShowBirthdateViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.Privacy.SettingsPrivacyNewChatViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.Privacy.SettingsPrivacyNewChatViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsAutoDeleteViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsAutoDeleteViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsProfileViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsProfileViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _profilePhotoService ??= new Telegram.Services.ProfilePhotoService(_clientService));
                case "Telegram.ViewModels.Settings.SettingsProfileColorViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsProfileColorViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Supergroups.SupergroupProfileColorViewModel":
                    return (T)(object)new Telegram.ViewModels.Supergroups.SupergroupProfileColorViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsPasswordViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsPasswordViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsPasscodeViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsPasscodeViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _passcodeService);
                case "Telegram.ViewModels.Settings.SettingsStickersViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsStickersViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsLanguageViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsLanguageViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _localeService);
                case "Telegram.ViewModels.Settings.SettingsAppearanceViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsAppearanceViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _themeService ??= new Telegram.Services.ThemeService(
                            _clientService,
                            _settingsService,
                            _eventAggregator));
                case "Telegram.ViewModels.Settings.SettingsThemesViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsThemesViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _themeService ??= new Telegram.Services.ThemeService(
                            _clientService,
                            _settingsService,
                            _eventAggregator));
                case "Telegram.ViewModels.Settings.SettingsThemeViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsThemeViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _themeService ??= new Telegram.Services.ThemeService(
                            _clientService,
                            _settingsService,
                            _eventAggregator));
                case "Telegram.ViewModels.Settings.SettingsNightModeViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsNightModeViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _themeService ??= new Telegram.Services.ThemeService(
                            _clientService,
                            _settingsService,
                            _eventAggregator),
                        _locationService ??= new Telegram.Services.LocationService(_clientService));
                case "Telegram.ViewModels.Settings.SettingsBackgroundsViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsBackgroundsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Settings.SettingsShortcutsViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsShortcutsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _shortcutsService ??= new Telegram.Services.ShortcutsService(
                            _clientService,
                            _settingsService,
                            _eventAggregator));
                case "Telegram.ViewModels.Settings.SettingsPowerSavingViewModel":
                    return (T)(object)new Telegram.ViewModels.Settings.SettingsPowerSavingViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.BackgroundViewModel":
                    return (T)(object)new Telegram.ViewModels.BackgroundViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.StickersViewModel":
                    return (T)(object)new Telegram.ViewModels.StickersViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Payments.PaymentAddressViewModel":
                    return (T)(object)new Telegram.ViewModels.Payments.PaymentAddressViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Payments.PaymentCredentialsViewModel":
                    return (T)(object)new Telegram.ViewModels.Payments.PaymentCredentialsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Payments.PaymentFormViewModel":
                    return (T)(object)new Telegram.ViewModels.Payments.PaymentFormViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.InteractionsViewModel":
                    return (T)(object)new Telegram.ViewModels.InteractionsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.StoryInteractionsViewModel":
                    return (T)(object)new Telegram.ViewModels.StoryInteractionsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Folders.FoldersViewModel":
                    return (T)(object)new Telegram.ViewModels.Folders.FoldersViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Folders.FolderViewModel":
                    return (T)(object)new Telegram.ViewModels.Folders.FolderViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Folders.ShareFolderViewModel":
                    return (T)(object)new Telegram.ViewModels.Folders.ShareFolderViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Folders.AddFolderViewModel":
                    return (T)(object)new Telegram.ViewModels.Folders.AddFolderViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Folders.RemoveFolderViewModel":
                    return (T)(object)new Telegram.ViewModels.Folders.RemoveFolderViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.DownloadsViewModel":
                    return (T)(object)new Telegram.ViewModels.DownloadsViewModel(
                        _clientService,
                        _settingsService,
                        _storageService ??= new Telegram.Services.StorageService(_clientService),
                        _eventAggregator);
                case "Telegram.ViewModels.ChooseSoundViewModel":
                    return (T)(object)new Telegram.ViewModels.ChooseSoundViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.ChatNotificationsViewModel":
                    return (T)(object)new Telegram.ViewModels.ChatNotificationsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Premium.PromoViewModel":
                    return (T)(object)new Telegram.ViewModels.Premium.PromoViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Stars.StarsViewModel":
                    return (T)(object)new Telegram.ViewModels.Stars.StarsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Stars.BuyViewModel":
                    return (T)(object)new Telegram.ViewModels.Stars.BuyViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Stars.PayViewModel":
                    return (T)(object)new Telegram.ViewModels.Stars.PayViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Business.BusinessViewModel":
                    return (T)(object)new Telegram.ViewModels.Business.BusinessViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Business.BusinessLocationViewModel":
                    return (T)(object)new Telegram.ViewModels.Business.BusinessLocationViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Business.BusinessHoursViewModel":
                    return (T)(object)new Telegram.ViewModels.Business.BusinessHoursViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Business.BusinessRepliesViewModel":
                    return (T)(object)new Telegram.ViewModels.Business.BusinessRepliesViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Business.BusinessGreetViewModel":
                    return (T)(object)new Telegram.ViewModels.Business.BusinessGreetViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Business.BusinessAwayViewModel":
                    return (T)(object)new Telegram.ViewModels.Business.BusinessAwayViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Business.BusinessBotsViewModel":
                    return (T)(object)new Telegram.ViewModels.Business.BusinessBotsViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Business.BusinessIntroViewModel":
                    return (T)(object)new Telegram.ViewModels.Business.BusinessIntroViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.Business.BusinessChatLinksViewModel":
                    return (T)(object)new Telegram.ViewModels.Business.BusinessChatLinksViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.ViewModels.RevenueViewModel":
                    return (T)(object)new Telegram.ViewModels.RevenueViewModel(
                        _clientService,
                        _settingsService,
                        _eventAggregator);
                case "Telegram.Services.IDeviceInfoService":
                    return (T)_deviceInfoService;
                case "Telegram.Services.ISettingsService":
                    return (T)_settingsService;
                case "Telegram.Services.IEventAggregator":
                    return (T)_eventAggregator;
                case "Telegram.Services.IClientService":
                    return (T)_clientService;
                case "Telegram.Services.INetworkService":
                    return (T)_networkService;
                case "Telegram.Services.IGenerationService":
                    return (T)_generationService;
                case "Telegram.Services.ISessionService":
                    return (T)_sessionService;
                case "Telegram.Services.INotificationsService":
                    return (T)_notificationsService;
                case "Telegram.Services.ViewService.IViewService":
                    return (T)_viewService;
                case "Telegram.Services.IVoipService":
                    return (T)_voipService;
                case "Telegram.Services.ISettingsSearchService":
                    return (T)(_settingsSearchService ??= new Telegram.Services.SettingsSearchService(_clientService));
                case "Telegram.Services.ICloudUpdateService":
                    return (T)(_cloudUpdateService ??= new Telegram.Services.CloudUpdateService(
                        _clientService,
                        _networkService,
                        _eventAggregator));
                case "Telegram.Services.IShortcutsService":
                    return (T)(_shortcutsService ??= new Telegram.Services.ShortcutsService(
                        _clientService,
                        _settingsService,
                        _eventAggregator));
                case "Telegram.Services.IVoipGroupService":
                    return (T)(_voipGroupService ??= new Telegram.Services.VoipGroupService(
                        _clientService,
                        _settingsService,
                        _eventAggregator,
                        _viewService));
                case "Telegram.Services.IContactsService":
                    return (T)(_contactsService ??= new Telegram.Services.ContactsService(
                        _clientService,
                        _settingsService,
                        _eventAggregator));
                case "Telegram.Services.ILocationService":
                    return (T)(_locationService ??= new Telegram.Services.LocationService(_clientService));
                case "Telegram.Services.IThemeService":
                    return (T)(_themeService ??= new Telegram.Services.ThemeService(
                        _clientService,
                        _settingsService,
                        _eventAggregator));
                case "Telegram.Services.Factories.IMessageFactory":
                    return (T)(_messageFactory ??= new Telegram.Services.Factories.MessageFactory(
                        _clientService,
                        _playbackService));
                case "Telegram.Services.IStorageService":
                    return (T)(_storageService ??= new Telegram.Services.StorageService(_clientService));
                case "Telegram.Services.ITranslateService":
                    return (T)(_translateService ??= new Telegram.Services.TranslateService(
                        _clientService,
                        _settingsService,
                        _eventAggregator));
                case "Telegram.Services.IProfilePhotoService":
                    return (T)(_profilePhotoService ??= new Telegram.Services.ProfilePhotoService(_clientService));
                default:
                    return default;

            }
        }
    }
}
