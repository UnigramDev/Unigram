using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.SecretChats;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.SecretChats
{
    public sealed partial class SecretChatCreatePage : Page
    {
        public SecretChatCreateViewModel ViewModel => DataContext as SecretChatCreateViewModel;

        public SecretChatCreatePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SecretChatCreateViewModel>();
            View.Attach();
        }
    }
}
