using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.ViewModels.Folders;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Folders
{
    public sealed partial class FoldersPage : Page
    {
        public FoldersViewModel ViewModel => DataContext as FoldersViewModel;

        public FoldersPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<FoldersViewModel>();
        }

        private void Items_ElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var filter = sender.ItemsSourceView.GetAt(args.Index) as ChatListFilter;

            button.Content = filter.Title;
            button.Command = ViewModel.EditCommand;
            button.CommandParameter = filter;
        }

        private void Suggestions_ElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as BadgeButton;
            var suggestion = sender.ItemsSourceView.GetAt(args.Index) as ChatListFilterSuggestion;

            button.Content = suggestion.Filter.Title;
            button.Badge = suggestion.Description;
        }
    }
}
