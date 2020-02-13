using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Filters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Filters
{
    public sealed partial class FilterPage : Page
    {
        public FilterViewModel ViewModel => DataContext as FilterViewModel;

        public FilterPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<FilterViewModel>();
        }

        private void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var content = button.Content as Grid;

            var chat = sender.ItemsSourceView.GetAt(args.Index) as Chat;

            var title = content.Children[1] as TextBlock;
            title.Text = ViewModel.ProtoService.GetTitle(chat);

            //if (ViewModel.CacheService.TryGetSupergroup(chat, out Supergroup supergroup))
            //{
            //    var subtitle = content.Children[2] as TextBlock;
            //    subtitle.Text = string.Format("{0}, {1}", BindConvert.Distance(nearby.Distance), Locale.Declension("Members", supergroup.MemberCount));
            //}
            //else
            //{
            //    var subtitle = content.Children[2] as TextBlock;
            //    subtitle.Text = BindConvert.Distance(nearby.Distance);
            //}

            var photo = content.Children[0] as ProfilePicture;
            photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);

            //button.Command = ViewModel.OpenChatCommand;
            //button.CommandParameter = nearby;
        }
    }
}
