using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Core.Services;
using Unigram.Views;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
   public class ChatPageHostViewModel : UnigramViewModelBase
    {

        public ObservableCollection<object> parameterList = new ObservableCollection<object>();
        public ObservableCollection<PivotItem> chatWindows = new ObservableCollection<PivotItem>();
        public ChatPageHostViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) : base(protoService, cacheService, aggregator)
        {
            parameterList.CollectionChanged += ParameterList_CollectionChanged;
        }


        private void ParameterList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    var x = getChatWindow(parameterList.Last());
                    chatWindows.Add(x);
                    selectedIndex = chatWindows.IndexOf(x);
                    break;

                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    chatWindows.RemoveAt(e.OldStartingIndex);
                    break;
            }
        }

        public PivotItem getChatWindow(object parameter)
        {
            Frame frame = new Frame();
            var service = BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, BootStrapper.ExistingContent.Exclude) as NavigationService;
            service.SerializationService = TLSerializationService.Current;
            service.Navigate(typeof(DialogPage), parameter);
            PivotItem item = new PivotItem();
            item.Header = "Hello";
            item.Content = service.Frame;
            return item;
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (!parameterList.Contains(parameter))
                parameterList.Add(parameter);
            else
                selectedIndex = parameterList.IndexOf(parameter);
        }

        public int _selectedIndex;
        public int selectedIndex
        {
            get { return _selectedIndex; }
            set { _selectedIndex = value; }
        }
    
    }

}
