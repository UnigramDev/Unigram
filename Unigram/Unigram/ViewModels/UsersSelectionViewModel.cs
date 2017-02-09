using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class UsersSelectionViewModel : UnigramViewModelBase
    {
        public UsersSelectionViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Items = new SortedObservableCollection<TLUser>(new TLUserComparer(false));
            SelectedItems = new ObservableCollection<TLUser>();
            SelectedItems.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SendCommand.RaiseCanExecuteChanged();
        }

        #region Overrides

        public virtual string Title
        {
            get
            {
                return "Title";
            }
        }

        public virtual int Maximum
        {
            get
            {
                return 5000;
            }
        }

        public virtual int Minimum
        {
            get
            {
                return 0;
            }
        }

        protected virtual void SendExecute()
        {

        }

        #endregion

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var contacts = CacheService.GetContacts();
            foreach (var item in contacts.OfType<TLUser>())
            {
                var user = item as TLUser;
                if (user.IsSelf)
                {
                    continue;
                }

                Items.Add(user);
            }

            var input = string.Join(",", contacts.Select(x => x.Id).Union(new[] { SettingsHelper.UserId }).OrderBy(x => x));
            var hash = Utils.ComputeMD5(input);
            var hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();

            var response = await ProtoService.GetContactsAsync(hex);
            if (response.IsSucceeded && response.Result is TLContactsContacts)
            {
                var result = response.Result as TLContactsContacts;
                if (result != null)
                {
                    foreach (var item in result.Users.OfType<TLUser>())
                    {
                        var user = item as TLUser;
                        if (user.IsSelf)
                        {
                            continue;
                        }

                        Items.Add(user);
                    }
                }
            }
        }

        public SortedObservableCollection<TLUser> Items { get; private set; }

        public ObservableCollection<TLUser> SelectedItems { get; private set; }

        private RelayCommand _sendCommand;
        public RelayCommand SendCommand => _sendCommand = _sendCommand ?? new RelayCommand(SendExecute, () => Minimum <= SelectedItems.Count && Maximum >= SelectedItems.Count);
    }
}
