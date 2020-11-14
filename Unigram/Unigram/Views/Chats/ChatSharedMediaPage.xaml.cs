using System.ComponentModel;
using Unigram.Navigation;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatSharedMediaPage : Page, INavigablePage
    {
        public ChatSharedMediaPage()
        {
            InitializeComponent();
            DataContext = View.DataContext = TLContainer.Current.Resolve<ChatSharedMediaViewModel, IFileDelegate>(View);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            View.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            View.OnNavigatedFrom(e);
        }

        public void OnBackRequested(HandledEventArgs args)
        {
            View.OnBackRequested(args);
        }

        public void UpdateFile(Telegram.Td.Api.File file)
        {
            View.UpdateFile(file);
        }
    }
}
