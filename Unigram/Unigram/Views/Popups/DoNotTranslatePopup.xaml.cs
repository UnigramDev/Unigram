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
