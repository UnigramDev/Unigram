using System;
using Unigram.Navigation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Services.Navigation
{
    // DOCS: https://github.com/Windows-XAML/Template10/wiki/Docs-%7C-NavigationService
    public class NavigatingEventArgs : NavigatedEventArgs
    {
        DeferralManager Manager;
        public Deferral GetDeferral() => Manager.GetDeferral();

        public NavigatingEventArgs(DeferralManager manager) : base()
        {
            Manager = manager;
        }

        public NavigatingEventArgs(DeferralManager manager, NavigatingCancelEventArgs e, Page page, Type targetPageType, object parameter, object targetPageParameter) : this(manager)
        {
            NavigationMode = e.NavigationMode;
            PageType = e.SourcePageType;
            Page = page;
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