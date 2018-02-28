using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.SecretChats
{
    public class SecretChatCreateViewModel : UsersSelectionViewModel
    {
        public SecretChatCreateViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public override string Title => Strings.Android.NewSecretChat;

        public override int Maximum => 1;

        protected override async void SendExecute(User user)
        {
            if (user == null)
            {
                return;
            }

            var confirm = await TLMessageDialog.ShowAsync(Strings.Android.AreYouSureSecretChat, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            Function request;

            var existing = ProtoService.GetSecretChatForUser(user.Id);
            if (existing != null)
            {
                request = new CreateSecretChat(existing.Id);
            }
            else
            {
                request = new CreateNewSecretChat(user.Id);
            }

            var response = await ProtoService.SendAsync(request);
            if (response is Chat chat)
            {
                NavigationService.NavigateToChat(chat);
                NavigationService.RemoveLast();
            }
        }
    }
}
