﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels;
using Unigram.ViewModels.Dialogs;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls
{
    public sealed partial class DialogSearchBar : UserControl
    {
        public DialogSearchViewModel ViewModel => DataContext as DialogSearchViewModel;

        public DialogSearchBar()
        {
            this.InitializeComponent();
        }

        #region Binding

        private string ConvertOf(int index, int count)
        {
            return string.Format(Strings.Android.Of, index + 1, count);
        }

        #endregion
    }
}
