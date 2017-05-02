using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.Chats;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Chats
{
    public sealed partial class CreateChatStep1Page : Page
    {
        public CreateChatStep1ViewModel ViewModel => DataContext as CreateChatStep1ViewModel;

        public CreateChatStep1Page()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<CreateChatStep1ViewModel>();
        }
    }
}
