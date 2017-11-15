using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Users
{
    public sealed partial class UserCreatePage : Page
    {
        public UserCreateViewModel ViewModel => DataContext as UserCreateViewModel;

        public UserCreatePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<UserCreateViewModel>();
        }
    }
}
