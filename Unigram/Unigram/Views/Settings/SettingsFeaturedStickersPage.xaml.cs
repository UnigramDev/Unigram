﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Core.Dependency;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsFeaturedStickersPage : Page
    {
        public SettingsFeaturedStickersViewModel ViewModel => DataContext as SettingsFeaturedStickersViewModel;

        public SettingsFeaturedStickersPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Instance.ResolveType<SettingsFeaturedStickersViewModel>();
        }
    }
}
