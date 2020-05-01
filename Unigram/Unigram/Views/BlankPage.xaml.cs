using System.Linq;
using Telegram.Td.Api;
using Unigram.Services;
using Unigram.Views.SignIn;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class BlankPage : Page
    {
        public BlankPage()
        {
            InitializeComponent();
            DataContext = new object();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back && Frame.ForwardStack.Any(x => x.SourcePageType == typeof(SignInPage)))
            {
                TLContainer.Current.Resolve<IProtoService>().Send(new Destroy());
            }
        }
    }
}
