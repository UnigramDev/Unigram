using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Navigation.Services
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-NavigationService
    public interface INavigable
    {
        Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state);
        Task OnNavigatedFromAsync(IDictionary<string, object> suspensionState, bool suspending);
        void OnNavigatingFrom(NavigatingEventArgs args);
        INavigationService NavigationService { get; set; }
        IDispatcherContext Dispatcher { get; set; }
        IDictionary<string, object> SessionState { get; set; }
    }
}
