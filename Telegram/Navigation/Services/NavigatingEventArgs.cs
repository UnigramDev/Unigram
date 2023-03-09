//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Navigation.Services
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

        public NavigatingEventArgs(Type targetPageType, object parameter, object state, object targetPageParameter)
        {
            NavigationMode = NavigationMode.New;
            SourcePageType = null;
            Content = null;
            Parameter = parameter;
            TargetPageType = targetPageType;
            TargetPageParameter = state;
        }

        public bool Cancel { get; set; } = false;
        public bool Suspending { get; set; } = false;
        public Type TargetPageType { get; set; }
        public object TargetPageParameter { get; set; }
    }
}