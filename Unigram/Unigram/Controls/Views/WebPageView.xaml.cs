using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class WebPageView : BottomSheet
    {
        private WebPageView()
        {
            this.InitializeComponent();
        }

        private static WebPageView _current;
        public static WebPageView Current
        {
            get
            {
                if (_current == null)
                    _current = new WebPageView();

                return _current;
            }
        }

        public IAsyncOperation<ContentDialogBaseResult> ShowAsync(TLWebPage webPage)
        {
            if (webPage.HasEmbedUrl)
            {
                Image.Constraint = webPage;
                View.Navigate(new Uri(webPage.EmbedUrl));
            }

            return base.ShowAsync();
        }

        private void Join_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.OK);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogBaseResult.Cancel);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            View.NavigateToString(string.Empty);
        }
    }
}
