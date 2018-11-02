using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.Channels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Telegram.Td.Api;
using Unigram.Converters;
using Unigram.Controls;
using Unigram.Common;
using System.Reactive.Linq;

namespace Unigram.Views.Channels
{
    public sealed partial class ChannelCreateStep2Page : Page
    {
        public ChannelCreateStep2ViewModel ViewModel => DataContext as ChannelCreateStep2ViewModel;

        public ChannelCreateStep2Page()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ChannelCreateStep2ViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(Username, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                if (ViewModel.UpdateIsValid(Username.Text))
                {
                    ViewModel.CheckAvailability(Username.Text);
                }
            });
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.RevokeLinkCommand.Execute(e.ClickedItem);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var chat = args.Item as Chat;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = ViewModel.ProtoService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {
                if (chat.Type is ChatTypeSupergroup super)
                {
                    var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                    if (supergroup != null)
                    {
                        var subtitle = content.Children[2] as TextBlock;
                        subtitle.Text = MeUrlPrefixConverter.Convert(ViewModel.CacheService, supergroup.Username, true);
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }
    }
}
