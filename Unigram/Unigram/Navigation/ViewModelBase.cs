using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unigram.Navigation.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Navigation
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-MVVM
    public abstract class ViewModelBase : BindableBase, INavigable
    {
        public virtual Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            return Task.CompletedTask;
        }

        public virtual void OnNavigatingFrom(NavigatingEventArgs args)
        {

        }

        [JsonIgnore]
        public virtual INavigationService NavigationService { get; set; }

        [JsonIgnore]
        public virtual IDispatcherContext Dispatcher { get; set; }

        [JsonIgnore]
        public virtual IDictionary<string, object> SessionState { get; set; }
    }
}