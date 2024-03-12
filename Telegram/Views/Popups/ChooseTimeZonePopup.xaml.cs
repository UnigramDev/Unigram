//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System.Collections.Generic;
using System.Linq;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseTimeZonePopup : ContentPopup
    {
        private readonly List<TimeZone> _languages;
        private readonly DiffObservableCollection<TimeZone> _diff;

        public ChooseTimeZonePopup(TimeZones zones, TimeZone selected)
        {
            InitializeComponent();

            Title = Strings.BusinessHoursTimezonePicker;

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            var items = new List<TimeZone>(zones.TimeZonesValue.OrderBy(x => x.UtcTimeOffset));
            var handler = new DiffHandler<TimeZone>((x, y) =>
            {
                return x.Id == y.Id;
            });

            _languages = items;
            _diff = new DiffObservableCollection<TimeZone>(items, handler, Constants.DiffOptions);

            ScrollingHost.ItemsSource = _diff;
            ScrollingHost.SelectedItem = selected;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ScrollingHost.SelectedItem is not TimeZone timeZone)
            {
                args.Cancel = true;
                return;
            }

            SelectedItem = timeZone;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchField.Text))
            {
                _diff.ReplaceDiff(_languages);
            }
            else
            {
                _diff.ReplaceDiff(_languages.Where(FilterByQuery));
            }

            ShowHideNoResult(_diff.Count == 0);
        }

        private bool _noResultCollapsed = true;

        private void ShowHideNoResult(bool show)
        {
            if (_noResultCollapsed != show)
            {
                return;
            }

            _noResultCollapsed = !show;
            NoResult.Visibility = Visibility.Visible;

            var visual = ElementComposition.GetElementVisual(NoResult);
            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0, show ? 0 : 1);
            animation.InsertKeyFrame(1, show ? 1 : 0);

            visual.StartAnimation("Opacity", animation);
        }

        private bool FilterByQuery(TimeZone language)
        {
            if (ClientEx.SearchByPrefix(language.Name, SearchField.Text))
            {
                return true;
            }

            return ClientEx.SearchByPrefix(Formatter.UtcTimeOffset(language.UtcTimeOffset), SearchField.Text);
        }

        public TimeZone SelectedItem { get; private set; }
    }
}
