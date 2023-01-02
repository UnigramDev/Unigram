//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Controls;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class DoNotTranslatePopup : ContentPopup
    {
        public DoNotTranslatePopup(IList<LanguagePackInfo> languages, IList<string> selected)
        {
            InitializeComponent();

            Title = Strings.Resources.DoNotTranslate;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            ScrollingHost.ItemsSource = languages;

            foreach (var item in languages)
            {
                if (selected.Contains(item.Id))
                {
                    ScrollingHost.SelectedItems.Add(item);
                }
            }
        }

        public IList<string> SelectedItems { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SelectedItems = ScrollingHost.SelectedItems.Cast<LanguagePackInfo>().Select(x => x.Id).ToArray();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }
    }
}
