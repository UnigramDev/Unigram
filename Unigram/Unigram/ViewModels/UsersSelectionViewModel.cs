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
using Telegram.Api.TL.Contacts;
using Template10.Services.NavigationService;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Views;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public abstract class UsersSelectionViewModel : UnigramViewModelBase
    {
        public UsersSelectionViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            Items = new SortedObservableCollection<TLUser>(new TLUserComparer(false));
            Search = new ObservableCollection<KeyedList<string, TLObject>>();
            SelectedItems = new ObservableCollection<TLUser>();
            SelectedItems.CollectionChanged += OnCollectionChanged;

            SendCommand = new RelayCommand(SendExecute, () => Minimum <= SelectedItems.Count && Maximum >= SelectedItems.Count);
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SendCommand.RaiseCanExecuteChanged();
        }

        #region Overrides

        public virtual string Title => "Title";

        public virtual int Maximum => 5000;
        public virtual int Minimum => 0;

        public ListViewSelectionMode SelectionMode => Maximum > 1 ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;

        public virtual bool AllowGlobalSearch => true;

        protected virtual Func<TLUser, bool> Filter => null;

        #endregion

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Items.Clear();

            var contacts = CacheService.GetContacts();
            foreach (var item in contacts.OfType<TLUser>())
            {
                var user = item as TLUser;
                if (user.IsSelf)
                {
                    continue;
                }

                if (Filter != null)
                {
                    if (Filter(user))
                    {
                        Items.Add(user);
                    }
                }
                else
                {
                    Items.Add(user);
                }
            }

            return Task.CompletedTask;
        }

        public SortedObservableCollection<TLUser> Items { get; private set; }

        public ObservableCollection<TLUser> SelectedItems { get; private set; }

        public RelayCommand SendCommand { get; }
        protected virtual void SendExecute()
        {

        }

        #region Search

        public ObservableCollection<KeyedList<string, TLObject>> Search { get; private set; }

        private string _searchQuery;
        public string SearchQuery
        {
            get
            {
                return _searchQuery;
            }
            set
            {
                Set(ref _searchQuery, value);
                SearchSync(value);
            }
        }

        public async void SearchSync(string query)
        {
            var local = await SearchLocalAsync(query.TrimStart('@'));

            if (string.Equals(query, _searchQuery))
            {
                Search.Clear();
                if (local != null) Search.Insert(0, local);
            }
        }

        public async Task SearchAsync(string query)
        {
            var global = await SearchGlobalAsync(query);

            if (string.Equals(query, _searchQuery))
            {
                if (Search.Count > 1) Search.RemoveAt(1);
                if (global != null) Search.Add(global);
            }

            //SearchQuery = query;
        }

        private async Task<KeyedList<string, TLObject>> SearchLocalAsync(string query1)
        {
            var query = LocaleHelper.GetQuery(query1);

            var dialogs = await Task.Run(() => CacheService.GetDialogs());
            var contacts = await Task.Run(() => CacheService.GetContacts());

            if (dialogs != null && contacts != null)
            {
                var simple = new List<TLUser>();

                var contactsResults = contacts.OfType<TLUser>().Where(x =>
                    SelectedItems.All(selectedUser => selectedUser.Id != x.Id) &&
                    x.IsLike(query, StringComparison.OrdinalIgnoreCase));

                foreach (var result in contactsResults)
                {
                    simple.Add(result);
                }

                if (simple.Count > 0)
                {
                    return new KeyedList<string, TLObject>(null, simple);
                }
            }

            return null;
        }

        private async Task<KeyedList<string, TLObject>> SearchGlobalAsync(string query)
        {
            if (query.Length < 5)
            {
                return null;
            }

            var result = await ProtoService.SearchAsync(query, 100);
            if (result.IsSucceeded)
            {
                if (result.Result.Results.Count > 0)
                {
                    var parent = new KeyedList<string, TLObject>("Global search results");

                    CacheService.SyncUsersAndChats(result.Result.Users, result.Result.Chats,
                        tuple =>
                        {
                            result.Result.Users = tuple.Item1;
                            result.Result.Chats = tuple.Item2;

                            foreach (var peer in result.Result.Results)
                            {
                                var item = result.Result.Users.FirstOrDefault(x => x.Id == peer.Id);
                                if (item != null && SelectedItems.All(selectedUser => selectedUser.Id != item.Id))
                                {
                                    parent.Add(item);
                                }
                            }
                        });

                    return parent;
                }
            }

            return null;
        }

        #endregion
    }
}
