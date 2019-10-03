using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Ton.Tonlib.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Wallet;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Wallet
{
    public sealed partial class WalletTransactionPage : Page, IWalletTransactionDelegate
    {
        public WalletTransactionViewModel ViewModel => DataContext as WalletTransactionViewModel;

        public WalletTransactionPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletTransactionViewModel, IWalletTransactionDelegate>(this);
        }

        #region Delegate

        public void UpdateTransaction(RawTransaction item)
        {
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

            var date = BindConvert.Current.DateTime((int)item.Utime);

            var brush = new SolidColorBrush(amount > 0 ? Windows.UI.Colors.Green : Windows.UI.Colors.Black);

            if (amount > 0)
            {
                Amount.Text = ConvertAmount(amount);
                Amount.Foreground = new SolidColorBrush(Windows.UI.Colors.Green);

                Recipient.Text = Strings.Resources.WalletTransactionSender;
                Address.Text = ConvertAddress(item.InMsg.Source);
            }
            else
            {
                Amount.Text = ConvertAmount(amount);
                //Amount.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);

                if (item.OutMsgs.IsEmpty())
                {
                }
                else
                {
                    Recipient.Text = Strings.Resources.WalletTransactionRecipient;
                    Address.Text = ConvertAddress(item.OutMsgs[0].Destination);
                }
            }

            if (comment != null && comment.Count > 0)
            {
                CommentPanel.Visibility = Visibility.Visible;
                Comment.Text = Encoding.UTF8.GetString(comment.ToArray());
            }
            else
            {
                CommentPanel.Visibility = Visibility.Collapsed;
            }

            Timestamp.Text = string.Format(Strings.Resources.FormatDateAtTime, BindConvert.Current.ShortDate.Format(date), BindConvert.Current.ShortTime.Format(date));
        }

        #endregion

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

        #endregion

    }
}
