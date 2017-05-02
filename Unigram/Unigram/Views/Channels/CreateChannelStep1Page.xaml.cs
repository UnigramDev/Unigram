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
    public sealed partial class CreateChannelStep1Page : Page
    {
        public CreateChannelStep1ViewModel ViewModel => DataContext as CreateChannelStep1ViewModel;

        public CreateChannelStep1Page()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<CreateChannelStep1ViewModel>();
        }
    }
}
