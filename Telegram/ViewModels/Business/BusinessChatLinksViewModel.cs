using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.Views.Business.Popups;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Td.Api
{
    public class BusinessChatLink
    {
        public string Name { get; set; }

        public string Url { get; set; }

        public FormattedText Message { get; set; }

        public int ViewCount { get; set; }
    }
}

namespace Telegram.ViewModels.Business
{
    public class BusinessChatLinksViewModel : ViewModelBase
    {
        public BusinessChatLinksViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new ObservableCollection<BusinessChatLink>();
        }

        public ObservableCollection<BusinessChatLink> Items { get; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            Items.Add(new BusinessChatLink
            {
                Name = "Due fritture",
                Url = "https://t.me/OMo8UwyFsjo3YTA8",
                Message = ClientEx.ParseMarkdown("Si possono avere **due** fritture?"),
                ViewCount = new Random().Next(1000)
            });

            Items.Add(new BusinessChatLink
            {
                Name = string.Empty,
                Url = "https://t.me/OMo8UwyFsjo3YTA8",
                Message = ClientEx.ParseMarkdown("Si __possono__ avere ||due|| fritture?"),
                ViewCount = new Random().Next(1000)
            });

            Items.Add(new BusinessChatLink
            {
                Name = string.Empty,
                Url = "https://t.me/OMo8UwyFsjo3YTA8",
                Message = ClientEx.ParseMarkdown(string.Empty),
                ViewCount = new Random().Next(1000)
            });

            return Task.CompletedTask;
        }

        public async void Create()
        {
            var chatLink = new BusinessChatLink
            {
                Name = string.Empty,
                Url = "https://t.me/OMo8UwyFsjo3YTA8",
                Message = ClientEx.ParseMarkdown(string.Empty),
                ViewCount = new Random().Next(1000)
            };

            Items.Add(chatLink);
            Open(chatLink);
        }

        public void Copy(BusinessChatLink chatLink)
        {
            MessageHelper.CopyLink(chatLink.Url);
        }

        public void Share(BusinessChatLink chatLink)
        {

        }

        public async void Rename(BusinessChatLink chatLink)
        {
            var popup = new InputPopup
            {
                Title = Strings.BusinessLinksRenameTitle,
                Header = Strings.BusinessLinksRenameMessage,
                PrimaryButtonText = Strings.Done,
                SecondaryButtonText = Strings.Cancel,
                PlaceholderText = Strings.BusinessLinksNamePlaceholder,
                Text = chatLink.Name,
                MaxLength = 32,
                MinLength = 1,
            };

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {

            }
        }

        public async void Delete(BusinessChatLink chatLink)
        {
            var confirm = await ShowPopupAsync(Strings.BusinessLinksDeleteMessage, Strings.BusinessLinksDeleteTitle, Strings.Remove, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                Items.Remove(chatLink);
            }
        }

        public async void Open(BusinessChatLink chatLink)
        {
            var popup = new BusinessChatLinkPopup(this, chatLink);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {

            }
        }
    }
}
