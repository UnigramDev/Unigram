using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Navigation.Services
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-NavigationService
    public class NavigatingEventArgs : NavigatedEventArgs
    {
        public NavigatingEventArgs()
        {

        }

        public NavigatingEventArgs(NavigatingCancelEventArgs e, Page page, Type targetPageType, object parameter, object targetPageParameter)
        {
            NavigationMode = e.NavigationMode;
            SourcePageType = e.SourcePageType;
            Content = page;
            Parameter = parameter;
            TargetPageType = targetPageType;
            TargetPageParameter = targetPageParameter;
        }

        public bool Cancel { get; set; } = false;
        public bool Suspending { get; set; } = false;
        public Type TargetPageType { get; set; }
        public object TargetPageParameter { get; set; }
    }
}