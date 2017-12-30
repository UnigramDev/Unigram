using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Services.Locale;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Common;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsLanguagePage : Page
    {
        public SettingsLanguageViewModel ViewModel => DataContext as SettingsLanguageViewModel;

        public SettingsLanguagePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsLanguageViewModel>();
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            var lang = e.ClickedItem as TLLangPackLanguage;
            if (lang == null)
            {
                return;
            }

            ApplicationLanguages.PrimaryLanguageOverride = lang.LangCode;
            ResourceContext.GetForCurrentView().Reset();
            ResourceContext.GetForViewIndependentUse().Reset();

            WindowWrapper.Current().NavigationServices.Remove(ViewModel.NavigationService);
            BootStrapper.Current.NavigationService.Reset();

            //new LocaleService(ViewModel.ProtoService).applyRemoteLanguage(e.ClickedItem as TLLangPackLanguage, true);
        }
    }
}
