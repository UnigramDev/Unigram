using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Wallet;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Wallet
{
    public sealed partial class WalletExportPage : Page, IWalletExportDelegate
    {
        public WalletExportViewModel ViewModel => DataContext as WalletExportViewModel;

        public WalletExportPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletExportViewModel, IWalletExportDelegate>(this);

            Transitions = ApiInfo.CreateSlideTransition();
        }

        public void UpdateWordList(IList<WalletWordViewModel> wordList)
        {
            WordListPanel.Children.Clear();

            TextBlock CreateTextBox(WalletWordViewModel item, bool right)
            {
                var text = new TextBlock();
                text.Style = App.Current.Resources["BodyTextBlockStyle"] as Style;
                text.Inlines.Add(new Run { Text = $"{item.Index}. ", Foreground = App.Current.Resources["SystemControlDisabledChromeDisabledLowBrush"] as SolidColorBrush });
                text.Inlines.Add(new Run { Text = item.Text, FontWeight = FontWeights.SemiBold });
                text.HorizontalAlignment = HorizontalAlignment.Stretch;
                text.Margin = new Thickness(0, 0, right ? 0 : 12, 8);

                return text;
            }

            for (int i = 0; i < 24; i++)
            {
                WordListPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

                var text1 = CreateTextBox(wordList[i], false);
                var text2 = CreateTextBox(wordList[++i], true);

                Grid.SetRow(text1, (i + 1) / 2 - 1);
                Grid.SetRow(text2, (i + 1) / 2 - 1);
                Grid.SetColumn(text2, 1);

                WordListPanel.Children.Add(text1);
                WordListPanel.Children.Add(text2);
            }
        }
    }
}
