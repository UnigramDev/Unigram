using System;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Business.Popups;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public partial class BusinessChatLinksViewModel : ViewModelBase, IDelegable<IBusinessChatLinksDelegate>
    {
        public IBusinessChatLinksDelegate Delegate { get; set; }

        public BusinessChatLinksViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<BusinessChatLink>();
            Items.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(CanCreateMore));
        }

        public MvxObservableCollection<BusinessChatLink> Items { get; }

        public bool CanCreateMore => Items.Count < ClientService.Options.BusinessChatLinkCountMax;

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetBusinessChatLinks());
            if (response is BusinessChatLinks links)
            {
                Items.AddRange(links.Links);
            }
        }

        public async void Create()
        {
            var response = await ClientService.SendAsync(new CreateBusinessChatLink(new InputBusinessChatLink(new FormattedText(string.Empty, Array.Empty<TextEntity>()), string.Empty)));
            if (response is BusinessChatLink chatLink)
            {
                Items.Add(chatLink);
                Open(chatLink);
            }
        }

        public void Copy(BusinessChatLink chatLink)
        {
            MessageHelper.CopyLink(XamlRoot, chatLink.Link);
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
                Text = chatLink.Title,
                MaxLength = 32,
                MinLength = 1,
            };

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                chatLink.Title = popup.Text;

                Delegate?.UpdateBusinessChatLink(chatLink);

                var response = await ClientService.SendAsync(new EditBusinessChatLink(chatLink.Link, new InputBusinessChatLink(chatLink.Text, popup.Text)));
                if (response is BusinessChatLink updated)
                {
                    chatLink.Title = updated.Title;
                    chatLink.Text = updated.Text;

                    Delegate?.UpdateBusinessChatLink(chatLink);
                }
            }
        }

        public async void Delete(BusinessChatLink chatLink)
        {
            var confirm = await ShowPopupAsync(Strings.BusinessLinksDeleteMessage, Strings.BusinessLinksDeleteTitle, Strings.Remove, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeleteBusinessChatLink(chatLink.Link));
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
