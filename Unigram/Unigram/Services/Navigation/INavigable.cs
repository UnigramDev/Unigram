using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;
using Unigram.Navigation;

namespace Unigram.Services.Navigation
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-NavigationService
    public interface INavigable
    {
        Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state);
        Task OnNavigatedFromAsync(IDictionary<string, object> suspensionState, bool suspending);
        Task OnNavigatingFromAsync(NavigatingEventArgs args);
        INavigationService NavigationService { get; set; }
        IDispatcherWrapper Dispatcher { get; set; }
        IDictionary<string, object> SessionState { get; set; }
    }
}
