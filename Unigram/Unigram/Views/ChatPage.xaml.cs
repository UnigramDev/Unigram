using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class ChatPage : Page
    {
        public DialogViewModel ViewModel
        {
            get
            {
                if (Content is ChatView view)
                {
                    return view.DataContext as DialogViewModel;
                }

                return null;
            }
        }

        public ChatPage()
        {
            InitializeComponent();

            Content = new ChatView(deleg => (DataContext = TLContainer.Current.Resolve<DialogViewModel, IDialogDelegate>(deleg)) as DialogViewModel);
            NavigationCacheMode = NavigationCacheMode.Required;
        }
    }
}
