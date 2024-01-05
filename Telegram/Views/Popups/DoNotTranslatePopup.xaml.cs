//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Controls;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class DoNotTranslatePopup : ContentPopup
    {
        public DoNotTranslatePopup(IList<LanguagePackInfo> languages, IEnumerable<string> selected)
        {
            InitializeComponent();

            Title = Strings.DoNotTranslate;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            var items = new List<LanguagePackInfo>();

            foreach (var item in languages)
            {
                if (selected.Contains(item.Id))
                {
                    items.Add(item);
                }
            }

            foreach (var item in languages)
            {
                if (selected.Contains(item.Id))
                {
                    continue;
                }

                items.Add(item);
            }

            ScrollingHost.ItemsSource = items;

            foreach (var item in languages)
            {
                if (selected.Contains(item.Id))
                {
                    ScrollingHost.SelectedItems.Add(item);
                }
            }
        }

        public HashSet<string> SelectedItems { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SelectedItems = ScrollingHost.SelectedItems.Cast<LanguagePackInfo>().Select(x => x.Id).ToHashSet();
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
