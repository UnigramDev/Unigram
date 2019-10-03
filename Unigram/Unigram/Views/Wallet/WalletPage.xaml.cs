using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Ton.Tonlib.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Wallet;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Wallet
{
    public sealed partial class WalletPage : Page
    {
        public WalletViewModel ViewModel => DataContext as WalletViewModel;

        public WalletPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame.BackStack.Clear();
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RawTransaction item)
            {
                // No details for processing fees
                if (item.InMsg == null && item.OutMsgs.IsEmpty())
                {
                    return;
                }

                var state = new Dictionary<string, object>
                {
                    { "transaction", e.ClickedItem }
                };

                ViewModel.NavigationService.Navigate(typeof(WalletTransactionPage), state: state);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var item = args.Item as RawTransaction;
            var root = args.ItemContainer.ContentTemplateRoot as Grid;
            var content = root.Children[0] as Grid;

            var headline = content.Children[0] as TextBlock;
            var address = content.Children[1] as TextBlock;
            var message = content.Children[2] as TextBlock;
            var timestamp = content.Children[3] as TextBlock;

            headline.Inlines.Clear();

            long amount;
            IList<byte> comment;
            if (item.InMsg != null)
            {
                amount = item.InMsg.Value;
                comment = item.InMsg.Message;
            }
            else
            {
                amount = 0;
                comment = null;
            }

            foreach (var msg in item.OutMsgs)
            {
                amount -= msg.Value;

                if (comment == null || comment.IsEmpty())
                {
                    comment = msg.Message;
                }
            }

            amount -= item.Fee;

            if (amount > 0)
            {
                headline.Inlines.Add(new Run { Text = ConvertAmount(amount), Foreground = new SolidColorBrush(Windows.UI.Colors.Green), FontWeight = FontWeights.SemiBold });
                headline.Inlines.Add(new Run { Text = $" {Strings.Resources.WalletFrom}" });
                address.Text = ConvertAddress(item.InMsg.Source);
            }
            else
            {
                headline.Inlines.Add(new Run { Text = ConvertAmount(amount), FontWeight = FontWeights.SemiBold });

                if (item.OutMsgs.IsEmpty())
                {
                    address.Text = Strings.Resources.WalletProcessingFee;
                }
                else
                {
                    headline.Inlines.Add(new Run { Text = $" {Strings.Resources.WalletTo}" });
                    address.Text = ConvertAddress(item.OutMsgs[0].Destination);
                }
            }

            if (comment != null && comment.Count > 0)
            {
                message.Text = Encoding.UTF8.GetString(comment.ToArray());
                message.Visibility = Visibility.Visible;
            }
            else
            {
                message.Visibility = Visibility.Collapsed;
            }

            timestamp.Text = BindConvert.Current.DateExtended((int)item.Utime);
        }

        #region Binding

        private string ConvertAmount(long value)
        {
            return string.Format("{0:0.000000000} \uD83D\uDC8E", value / 1000000000d);
        }

        private string ConvertAddress(string address)
        {
            if (address == null)
            {
                return string.Empty;
            }

            return address.Substring(0, address.Length / 2) + Environment.NewLine + address.Substring(address.Length / 2);
        }

        private Visibility ConvertSendVisibility(long value)
        {
            SendColumn.Width = new GridLength(value > 0 ? 1 : 0, value > 0 ? GridUnitType.Star : GridUnitType.Pixel);
            return value > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        private void CreatedPanel_Loaded(object sender, RoutedEventArgs e)
        {
            HeaderPanel.Height = ScrollingHost.ActualHeight;
        }

        private void CreatedPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            HeaderPanel.Height = double.NaN;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CreatedPanel != null)
            {
                HeaderPanel.Height = ScrollingHost.ActualHeight;
            }
            else
            {
                HeaderPanel.Height = double.NaN;
            }
        }
    }
}
