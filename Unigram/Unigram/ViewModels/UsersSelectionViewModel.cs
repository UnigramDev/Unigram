using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Core.Common;
using Unigram.Services;
using Unigram.ViewModels.Supergroups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public abstract class UsersSelectionViewModel : TLViewModelBase
    {
        public UsersSelectionViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator) 
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new SortedObservableCollection<User>(new UserComparer(false));
            SelectedItems = new MvxObservableCollection<User>();
            SelectedItems.CollectionChanged += OnCollectionChanged;

            SendCommand = new RelayCommand(SendExecute, () => Minimum <= SelectedItems.Count && Maximum >= SelectedItems.Count);
            SingleCommand = new RelayCommand<User>(SendExecute);
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

        protected virtual Func<User, bool> Filter => null;

        #endregion

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Items.Clear();

            ProtoService.Send(new SearchContacts(string.Empty, int.MaxValue), result =>
            {
                if (result is Telegram.Td.Api.Users users)
                {
                    BeginOnUIThread(() =>
                    {
                        foreach (var id in users.UserIds)
                        {
                            var user = ProtoService.GetUser(id);
                            if (user != null && Filter != null && Filter(user))
                            {
                                Items.Add(user);
                            }
                            else if (user != null)
                            {
                                Items.Add(user);
                            }
                        }
                    });
                }
            });

            return Task.CompletedTask;
        }

        public ObservableCollection<User> Items { get; protected set; }

        public MvxObservableCollection<User> SelectedItems { get; private set; }

        public RelayCommand SendCommand { get; }
        protected virtual void SendExecute()
        {

        }

        public RelayCommand<User> SingleCommand { get; }
        protected virtual void SendExecute(User user)
        {

        }

        private SearchUsersCollection _search;
        public SearchUsersCollection Search
        {
            get
            {
                return _search;
            }
            set
            {
                Set(ref _search, value);
            }
        }
    }
}
