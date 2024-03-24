using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Td.Api
{
    public interface BusinessFeature
    {

    }

    public class BusinessFeatureAwayMessage : BusinessFeature
    {

    }

    public class BusinessFeatureGreetingMessage : BusinessFeature
    {

    }

    public class BusinessFeatureQuickReplies : BusinessFeature
    {

    }

    public class BusinessFeatureOpeningHours : BusinessFeature
    {

    }

    public class BusinessFeatureLocation : BusinessFeature
    {

    }

    public class BusinessFeatureConnectedBots : BusinessFeature
    {

    }

    public class BusinessFeatureIntro : BusinessFeature
    {

    }
}

namespace Telegram.ViewModels.Business
{
    public class BusinessViewModel : ViewModelBase
    {
        public BusinessViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new ObservableCollection<BusinessFeature>();

            var order = ClientService.Config.GetNamedArray("business_promo_order");
            if (order == null)
            {
                return;
            }

            foreach (var item in order.Values)
            {
                if (item is not JsonValueString value)
                {
                    continue;
                }

                switch (value.Value)
                {
                    case "business_location":
                        Items.Add(new BusinessFeatureLocation());
                        break;
                    case "business_hours":
                        Items.Add(new BusinessFeatureOpeningHours());
                        break;
                    case "quick_replies":
                        Items.Add(new BusinessFeatureQuickReplies());
                        break;
                    case "greeting_message":
                        Items.Add(new BusinessFeatureGreetingMessage());
                        break;
                    case "away_message":
                        Items.Add(new BusinessFeatureAwayMessage());
                        break;
                }
            }

            if (Constants.DEBUG)
            {
                Items.Add(new BusinessFeatureConnectedBots());
                Items.Add(new BusinessFeatureIntro());
            }
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            ClientService.Send(new GetUserFullInfo(ClientService.Options.MyId));
            return Task.CompletedTask;
        }

        public ObservableCollection<BusinessFeature> Items { get; private set; }
    }
}
