using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsLanguagePage : Page
    {
        public SettingsLanguageViewModel ViewModel => DataContext as SettingsLanguageViewModel;

        public SettingsLanguagePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsLanguageViewModel>();
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is LanguagePackInfo)
            {
                ViewModel.ChangeCommand.Execute(e.ClickedItem);
            }
        }

        #region Context menu

        private void Language_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var info = element.Tag as LanguagePackInfo;

            if (!info.IsInstalled)
            {
                return;
            }

            flyout.CreateFlyoutItem(ViewModel.DeleteCommand, info, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
        }

        #endregion
    }

    public class SettingsLanguageSelector : DataTemplateSelector
    {
        public DataTemplate LanguageTemplate { get; set; }
        public DataTemplate SeparatorTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is LanguagePackInfo)
            {
                return LanguageTemplate;
            }

            return SeparatorTemplate;
        }
    }
}
