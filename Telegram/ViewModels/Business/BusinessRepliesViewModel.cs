using Rg.DiffUtils;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public partial class BusinessRepliesViewModel : ViewModelBase, IDelegable<IBusinessRepliesDelegate>, IHandle
    {
        public IBusinessRepliesDelegate Delegate { get; set; }

        public BusinessRepliesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new DiffObservableCollection<QuickReplyShortcut>(new QuickReplyShortcutDiffHandler(this), Constants.DiffOptions);
        }

        class QuickReplyShortcutDiffHandler : IDiffHandler<QuickReplyShortcut>
        {
            private readonly BusinessRepliesViewModel _viewModel;

            public QuickReplyShortcutDiffHandler(BusinessRepliesViewModel viewModel)
            {
                _viewModel = viewModel;
            }

            public bool CompareItems(QuickReplyShortcut oldItem, QuickReplyShortcut newItem)
            {
                return oldItem.Id == newItem.Id;
            }

            public void UpdateItem(QuickReplyShortcut oldItem, QuickReplyShortcut newItem)
            {
                oldItem.Name = newItem.Name;
                oldItem.FirstMessage = newItem.FirstMessage;
                oldItem.MessageCount = newItem.MessageCount;

                _viewModel.Delegate?.UpdateQuickReplyShortcut(oldItem);
            }
        }

        public DiffObservableCollection<QuickReplyShortcut> Items { get; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            Items.ReplaceDiff(ClientService.GetQuickReplyShortcuts());

            ClientService.Send(new LoadQuickReplyShortcuts());
            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateQuickReplyShortcuts>(this, Handle)
                .Subscribe<UpdateQuickReplyShortcut>(Handle);
        }

        private void Handle(UpdateQuickReplyShortcuts update)
        {
            BeginOnUIThread(() => Items.ReplaceDiff(ClientService.GetQuickReplyShortcuts()));
        }

        private void Handle(UpdateQuickReplyShortcut update)
        {
            BeginOnUIThread(() => Items.ReplaceDiff(ClientService.GetQuickReplyShortcuts()));
        }

        public async void Create()
        {
            var popup = new InputPopup
            {
                Title = Strings.BusinessRepliesNewTitle,
                Header = Strings.BusinessRepliesNewMessage,
                PrimaryButtonText = Strings.Done,
                SecondaryButtonText = Strings.Cancel,
                PlaceholderText = Strings.BusinessRepliesNamePlaceholder,
                MaxLength = 32,
                MinLength = 1,
            };

            popup.Validating += (s, args) =>
            {
                if (!ClientService.CheckQuickReplyShortcutName(args.Text))
                {
                    ShowToast(Strings.BusinessRepliesNameBusy, ToastPopupIcon.Error);
                    args.Cancel = true;
                }
            };

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                NavigationService.Navigate(typeof(ChatBusinessRepliesPage), new ChatBusinessRepliesIdNavigationArgs(popup.Text));
            }
        }

        public async void Rename(QuickReplyShortcut shortcut)
        {
            var popup = new InputPopup
            {
                Title = Strings.BusinessRepliesEditTitle,
                Header = Strings.BusinessRepliesEditMessage,
                PrimaryButtonText = Strings.Done,
                SecondaryButtonText = Strings.Cancel,
                PlaceholderText = Strings.BusinessRepliesNamePlaceholder,
                Text = shortcut.Name,
                MaxLength = 32,
                MinLength = 1,
            };

            popup.Validating += (s, args) =>
            {
                if (!ClientService.CheckQuickReplyShortcutName(args.Text))
                {
                    ShowToast(Strings.BusinessRepliesNameBusy, ToastPopupIcon.Error);
                    args.Cancel = true;
                }
            };

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new SetQuickReplyShortcutName(shortcut.Id, popup.Text));
            }
        }

        public async void Delete(QuickReplyShortcut shortcut)
        {
            var title = Locale.Declension(Strings.R.BusinessRepliesDeleteTitle, 1);
            var message = Locale.Declension(Strings.R.BusinessRepliesDeleteMessage, 1);

            var confirm = await ShowPopupAsync(message, title, Strings.Remove, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                ClientService.Send(new DeleteQuickReplyShortcut(shortcut.Id));
            }
        }

        public void Open(QuickReplyShortcut shortcut)
        {
            NavigationService.Navigate(typeof(ChatBusinessRepliesPage), new ChatBusinessRepliesIdNavigationArgs(shortcut.Name));
        }
    }
}
