//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages.Service
{
    public sealed partial class MessageSuggestBirthdateContent : MessageService
    {
        public MessageSuggestBirthdateContent()
        {
            InitializeComponent();
        }

        protected override void UpdateContent(MessageViewModel message)
        {
            if (message.Content is not MessageSuggestBirthdate suggestBirthdate)
            {
                return;
            }

            LocaleService.Current.GetDatePositions(out int dayPosition, out int monthPosition, out int yearPosition);

            Grid.SetColumn(DateDayTitle, dayPosition);
            Grid.SetColumn(DateDayValue, dayPosition);

            Grid.SetColumn(DateMonthTitle, monthPosition);
            Grid.SetColumn(DateMonthValue, monthPosition);

            Grid.SetColumn(DateYearTitle, yearPosition);
            Grid.SetColumn(DateYearValue, yearPosition);

            DateDayValue.Text = suggestBirthdate.Birthdate.Day.ToString();
            DateMonthValue.Text = LocaleService.Current.CurrentCulture.DateTimeFormat.GetMonthName(suggestBirthdate.Birthdate.Month);
            DateYearValue.Text = suggestBirthdate.Birthdate.Year.ToString();

            if (suggestBirthdate.Birthdate.Year == 0)
            {
                DateRoot.ColumnDefinitions[yearPosition].Width = new GridLength(0, GridUnitType.Pixel);
            }
            else
            {
                DateRoot.ColumnDefinitions[yearPosition].Width = new GridLength(1, GridUnitType.Star);
            }

            View.Visibility = message.IsOutgoing
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void Service_Click(object sender, RoutedEventArgs e)
        {
            if (Message?.Delegate != null)
            {
                Message.Delegate.ExecuteServiceMessage(Message);
            }
        }
    }
}
