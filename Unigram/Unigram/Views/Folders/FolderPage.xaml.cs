using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
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
    public sealed partial class FolderPage : Page
    {
        public FolderViewModel ViewModel => DataContext as FolderViewModel;

        public FolderPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<FolderViewModel>();
        }

        private void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var content = button.Content as Grid;

            var element = sender.ItemsSourceView.GetAt(args.Index) as ChatListFilterElement;
            button.Tag = element;

            var title = content.Children[1] as TextBlock;
            var photo = content.Children[0] as ProfilePicture;

            if (element is FilterChat chat)
            {
                title.Text = ViewModel.ProtoService.GetTitle(chat.Chat);
                photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat.Chat, 36);
            }
            else if (element is FilterFlag flag)
            {
                switch (flag.Flag)
                {
                    case ChatListFilterFlags.IncludeContacts:
                        title.Text = Strings.Resources.FilterContacts;
                        break;
                    case ChatListFilterFlags.IncludeNonContacts:
                        title.Text = Strings.Resources.FilterNonContacts;
                        break;
                    case ChatListFilterFlags.IncludeGroups:
                        title.Text = Strings.Resources.FilterGroups;
                        break;
                    case ChatListFilterFlags.IncludeChannels:
                        title.Text = Strings.Resources.FilterChannels;
                        break;
                    case ChatListFilterFlags.IncludeBots:
                        title.Text = Strings.Resources.FilterBots;
                        break;

                    case ChatListFilterFlags.ExcludeMuted:
                        title.Text = Strings.Resources.FilterMuted;
                        break;
                    case ChatListFilterFlags.ExcludeRead:
                        title.Text = Strings.Resources.FilterRead;
                        break;
                    case ChatListFilterFlags.ExcludeArchived:
                        title.Text = Strings.Resources.FilterArchived;
                        break;
                }

                photo.Source = PlaceholderHelper.GetGlyph(MainPage.GetFilterIcon(flag.Flag), (int)flag.Flag, 36);
            }

            //button.Command = ViewModel.OpenChatCommand;
            //button.CommandParameter = nearby;
        }

        private void Include_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.Tag as ChatListFilterElement;

            flyout.CreateFlyoutItem(viewModel.RemoveIncludeCommand, chat, Strings.Resources.StickersRemove, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
        }

        private void Exclude_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var chat = element.Tag as ChatListFilterElement;

            flyout.CreateFlyoutItem(viewModel.RemoveExcludeCommand, chat, Strings.Resources.StickersRemove, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
        }
    }
}
