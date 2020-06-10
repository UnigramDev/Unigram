using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Controls;
using Unigram.Services;
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

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsShortcutsPage : HostedPage
    {
        public SettingsShortcutsViewModel ViewModel => DataContext as SettingsShortcutsViewModel;

        public SettingsShortcutsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsShortcutsViewModel>();
        }

        private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as BadgeButton;
            var info = sender.ItemsSourceView.GetAt(args.Index) as ShortcutInfo;
            //var info = button.DataContext as ShortcutInfo;

            button.Content = info.Command;
            //button.Badge = info.Shortcut;
            button.Command = ViewModel.EditCommand;
            button.CommandParameter = info;
        }
    }
}
