using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditTypeViewModel : SupergroupEditViewModelBase
    {
        public SupergroupEditTypeViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        protected override async void SendExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var username = _isPublic ? (_username?.Trim() ?? string.Empty) : string.Empty;

            // If we're editing a basic group and the user wants to set an username to it,
            // then we need to upgrade it to a supergroup first.
            if (chat.Type is ChatTypeBasicGroup && !string.IsNullOrEmpty(username))
            {
                var response = await ProtoService.SendAsync(new UpgradeBasicGroupChatToSupergroupChat(chat.Id));
                if (response is Chat result && result.Type is ChatTypeSupergroup supergroup)
                {
                    chat = result;
                    await ProtoService.SendAsync(new GetSupergroupFullInfo(supergroup.SupergroupId));
                }
            }

            if (chat.Type is ChatTypeSupergroup)
            {
                var item = ProtoService.GetSupergroup(chat);
                var cache = ProtoService.GetSupergroupFull(chat);

                if (item == null || cache == null)
                {
                    return;
                }

                if (!string.Equals(username, item.Username))
                {
                    var response = await ProtoService.SendAsync(new SetSupergroupUsername(item.Id, username));
                    if (response is Error error)
                    {
                        if (error.TypeEquals(ErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
                        {
                            HasTooMuchUsernames = true;
                            LoadAdminedPublicChannels();
                        }
                        // TODO:

                        return;
                    }
                }

                NavigationService.GoBack();
            }
        }
    }
}
