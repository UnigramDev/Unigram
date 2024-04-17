using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public abstract class BusinessFeatureViewModelBase : ViewModelBase
    {
        public BusinessFeatureViewModelBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var cached = ClientService.GetUserFull(ClientService.Options.MyId);
            if (cached == null)
            {
                var response = await ClientService.SendAsync(new GetUserFullInfo(ClientService.Options.MyId));
                if (response is not UserFullInfo)
                {
                    return;
                }

                cached = response as UserFullInfo;
            }

            await OnNavigatedToAsync(cached, mode, state);
        }

        protected abstract Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state);

        public override async void NavigatingFrom(NavigatingEventArgs args)
        {
            if (!_completed && HasChanged)
            {
                var message = this switch
                {
                    BusinessAwayViewModel => Strings.BusinessAwayUnsavedChanges,
                    BusinessGreetViewModel => Strings.BusinessGreetUnsavedChanges,
                    BusinessHoursViewModel => Strings.BusinessHoursUnsavedChanges,
                    BusinessLocationViewModel => Strings.BusinessLocationUnsavedChanges,
                    BusinessIntroViewModel => Strings.BusinessIntroUnsavedChanges,
                    BusinessBotsViewModel => Strings.BusinessBotUnsavedChanges,
                    _ => null
                };

                if (message == null)
                {
                    return;
                }

                args.Cancel = true;

                var confirm = await ShowPopupAsync(message, Strings.UnsavedChanges, Strings.ChatThemeSaveDialogApply, Strings.ChatThemeSaveDialogDiscard);
                if (confirm == ContentDialogResult.Primary)
                {
                    Continue();
                }
                else if (confirm == ContentDialogResult.Secondary)
                {
                    _completed = true;
                    NavigationService.GoBack();
                }
            }
        }

        protected bool _completed;
        public virtual bool HasChanged { get; }

        protected bool Invalidate<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Set(ref storage, value, propertyName))
            {
                RaisePropertyChanged(nameof(HasChanged));
                return true;
            }

            return false;
        }

        public abstract void Continue();
    }
}
