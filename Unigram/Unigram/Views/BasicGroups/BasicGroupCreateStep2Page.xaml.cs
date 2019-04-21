using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.ViewModels.BasicGroups;
using Unigram.ViewModels.Chats;
using Unigram.Views;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.BasicGroups
{
    public sealed partial class BasicGroupCreateStep2Page : Page
    {
        public BasicGroupCreateStep2Page()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<BasicGroupCreateStep2ViewModel>();
            View.Attach();

            Transitions = ApiInfo.CreateSlideTransition();
        }
    }
}
