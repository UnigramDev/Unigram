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

            if (args.Item is DateTime dateHeader)
            {
                var border = args.ItemContainer.ContentTemplateRoot as Border;
                var label = border.Child as TextBlock;

                label.Text = BindConvert.DayGrouping(dateHeader);

                return;
            }

            var item = args.Item as RawTransaction;
            var root = args.ItemContainer.ContentTemplateRoot as Grid;
            var content = root.Children[0] as Grid;

            var headline = content.Children[0] as TextBlock;
            var address = content.Children[1] as TextBlock;
            var message = content.Children[2] as TextBlock;
            var fees = content.Children[3] as TextBlock;
            var timestamp = content.Children[4] as TextBlock;

            headline.Inlines.Clear();

            long amount = 0;
            IList<byte> comment = null;
            if (item.InMsg != null)
            {
                amount = item.InMsg.Value;
                comment = item.InMsg.Message;
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
                headline.Inlines.Add(new Run { Text = ConvertAmount(amount), Foreground = new SolidColorBrush(Windows.UI.Colors.Red), FontWeight = FontWeights.SemiBold });

                if (item.OutMsgs.IsEmpty())
                {
                    address.Text = Strings.Resources.WalletTransactionFee;
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

            if (item.StorageFee != 0 || item.OtherFee != 0)
            {
                fees.Text = string.Format(Strings.Resources.WalletBlockchainFees, BindConvert.Grams(-item.StorageFee - item.OtherFee, false));
                fees.Visibility = Visibility.Visible;
            }
            else
            {
                fees.Visibility = Visibility.Collapsed;
            }

            timestamp.Text = BindConvert.Current.Date((int)item.Utime);
        }

        #region Binding

        private string ConvertSyncState(SyncState state, long ciccio)
        {
            //if (state is SyncStateDone done)
            //{
            //    return $"100%";
            //}
            //else if (state is SyncStateInProgress progress)
            //{
            //    var value = (int)((progress.CurrentSeqno - progress.FromSeqno) / (double)(progress.ToSeqno - progress.FromSeqno) * 100);
            //    return string.Format(Strings.Resources.WalletUpdatingProgress, value);
            //}

            var last = Utils.UnixTimestampToDateTime(ciccio);

            if (state is SyncStateInProgress inProgress)
            {
                int progress = (int)((inProgress.CurrentSeqno - inProgress.FromSeqno) / (double)(inProgress.ToSeqno - inProgress.FromSeqno) * 100);
                if (progress != 0 && progress != 100)
                {
                    return string.Format(Strings.Resources.WalletUpdatingProgress, progress);
                }
                else
                {
                    return Strings.Resources.WalletUpdating;
                }
            }
            else
            {
                var newTime = DateTime.Now;
                var dt = newTime - last;
                if (dt.TotalSeconds < 60)
                {
                    return Strings.Resources.WalletUpdatedFewSecondsAgo;
                }
                else
                {
                    String time;
                    if (dt.TotalSeconds < 60 * 60)
                    {
                        time = Locale.Declension("Minutes", (int)(dt.TotalSeconds / 60));
                    }
                    else if (dt.TotalSeconds < 60 * 60 * 24)
                    {
                        time = Locale.Declension("Hours", (int)(dt.TotalSeconds / 60 / 60));
                    }
                    else
                    {
                        time = Locale.Declension("Days", (int)(dt.TotalSeconds / 60 / 60 / 24));
                    }

                    return string.Format(Strings.Resources.WalletUpdatedTimeAgo, time);
                }
            }

            return null;
        }

        private string ConvertAmount(long value)
        {
            return BindConvert.Grams(value, true);
        }

        private string ConvertAmount(long value, bool integer)
        {
            var grams = BindConvert.Grams(value, true);
            var split = grams.Split(' ');
            var gem = split[0] == "\uD83D\uDC8E";

            var amount = split[gem ? 1 : 0].Split('.');

            if (integer)
            {
                if (gem)
                {
                    return $"\uD83D\uDC8E {amount[0]}";
                }

                return amount[0];
            }

            if (!gem)
            {
                return $".{amount[1]} \uD83D\uDC8E";
            }

            return $".{amount[1]}";
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
            if (CreatedPanel != null || LoadingPanel != null)
            {
                HeaderPanel.Height = ScrollingHost.ActualHeight;
            }
            else
            {
                HeaderPanel.Height = double.NaN;
            }
        }

        private void CreatedPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            if (CreatedPanel != null || LoadingPanel != null)
            {
                HeaderPanel.Height = ScrollingHost.ActualHeight;
            }
            else
            {
                HeaderPanel.Height = double.NaN;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CreatedPanel != null || LoadingPanel != null)
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
