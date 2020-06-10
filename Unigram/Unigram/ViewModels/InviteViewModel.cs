using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class InviteViewModel : TLViewModelBase
    {
        private IContactsService _contactsService;

        public InviteViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IContactsService contactsService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _contactsService = contactsService;

            Items = new MvxObservableCollection<object>();
        }

        public MvxObservableCollection<object> Items { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await _contactsService.ImportAsync();
            if (response is ImportedContacts importedContacts)
            {
                var result = new List<ImportedContact>();

                for (int i = 0; i < importedContacts.UserIds.Count; i++)
                {
                    result.Add(new ImportedContact { UserId = importedContacts.UserIds[i], ImporterCount = importedContacts.ImporterCount[i] });
                }

                Items.ReplaceWith(result.OrderByDescending(x => x.ImporterCount));
            }
        }
    }

    public class ImportedContact
    {
        public int UserId { get; set; }
        public int ImporterCount { get; set; }
    }
}
