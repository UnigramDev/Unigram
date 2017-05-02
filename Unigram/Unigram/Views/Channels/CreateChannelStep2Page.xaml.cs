﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.Channels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Channels
{
    public sealed partial class CreateChannelStep2Page : Page
    {
        public CreateChannelStep2ViewModel ViewModel => DataContext as CreateChannelStep2ViewModel;

        public CreateChannelStep2Page()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<CreateChannelStep2ViewModel>();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {

        }
    }
}
