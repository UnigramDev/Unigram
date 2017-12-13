using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Unigram.Common;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class DeleteChannelMessageDialog : ContentDialog
    {
        public DeleteChannelMessageDialog(int count, string fullName)
        {
            this.InitializeComponent();

            Title = Strings.Android.Message;
            PrimaryButtonText = Strings.Android.OK;
            SecondaryButtonText = Strings.Android.Cancel;

            Message.Text = string.Format(Strings.Android.AreYouSureDeleteMessages, LocaleHelper.Declension("Messages", count));
            DeleteAllCheck.Content = string.Format(Strings.Android.DeleteAllFrom, fullName ?? string.Empty);
        }

        public bool BanUser
        {
            get
            {
                return BanUserCheck.IsChecked ?? false;
            }
            set
            {
                BanUserCheck.IsChecked = value;
            }
        }

        public bool ReportSpam
        {
            get
            {
                return ReportSpamCheck.IsChecked ?? false;
            }
            set
            {
                ReportSpamCheck.IsChecked = value;
            }
        }

        public bool DeleteAll
        {
            get
            {
                return DeleteAllCheck.IsChecked ?? false;
            }
            set
            {
                DeleteAllCheck.IsChecked = value;
            }
        }
    }
}
