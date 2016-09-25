using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Core.Dependency;
using Unigram.ViewModels;
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

    public sealed partial class ChatInfoPage : Page
    {
        public UserInfoViewModel ViewModel => DataContext as UserInfoViewModel;
        public ChatInfoPage()
        {
            this.InitializeComponent();
            DataContext = UnigramContainer.Instance.ResolverType<UserInfoViewModel>();
        }
    }
}
