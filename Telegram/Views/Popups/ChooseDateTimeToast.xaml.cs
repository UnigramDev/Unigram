//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseDateTimeToast : ModalPopup
    {
        public ChooseDateTimeToast()
        {
            InitializeComponent();

            var date = DateTime.Now.AddMinutes(10);
            Date.Date = date.Date;
            Time.Time = date.TimeOfDay;

            Date.CalendarIdentifier = GlobalizationPreferences.Calendars.FirstOrDefault();
            Time.ClockIdentifier = GlobalizationPreferences.Clocks.FirstOrDefault();

            Date.Language = NativeUtils.GetCurrentCulture();
            Time.Language = NativeUtils.GetCurrentCulture();

            Date.MinDate = DateTime.Today;
            Date.MaxDate = DateTime.Today.AddYears(1);
        }

        public UIElement Footer
        {
            get => FooterRoot.Child;
            set => FooterRoot.Child = value;
        }

        public DateTime Value
        {
            get
            {
                if (Date.Date is DateTimeOffset date)
                {
                    return date.Add(Time.Time).UtcDateTime;
                }

                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Asks for the date that text in a message is formatted as, and returns the link the
        /// text gets, or null if nothing was chosen.
        /// </summary>
        public static async Task<string> ChooseDateUrlAsync(XamlRoot xamlRoot)
        {
            var popup = new ChooseDateTimeToast
            {
                Title = Strings.RelativeDateAddDate,
                PrimaryButtonContent = Strings.OK,
                PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                SecondaryButtonContent = Strings.Cancel,
                IsLightDismissEnabled = true,
            };

            CheckBox relative = null;
            CheckBox short_time = null;
            CheckBox long_time = null;
            CheckBox short_date = null;
            CheckBox long_date = null;
            CheckBox day_of_week = null;

            if (ApiInfo.IsPackagedRelease)
            {
                relative = new CheckBox
                {
                    Content = "r (relative)"
                };

                short_time = new CheckBox
                {
                    Content = "t (short time)"
                };

                long_time = new CheckBox
                {
                    Content = "T (long time)"
                };

                short_date = new CheckBox
                {
                    Content = "d (short date)"
                };

                long_date = new CheckBox
                {
                    Content = "D (long date)"
                };

                day_of_week = new CheckBox
                {
                    Content = "w (day of week)"
                };

                var panel = new StackPanel();
                panel.Children.Add(relative);
                panel.Children.Add(day_of_week);
                panel.Children.Add(short_date);
                panel.Children.Add(long_date);
                panel.Children.Add(short_time);
                panel.Children.Add(long_time);

                popup.Footer = panel;
            }

            var confirm = await popup.ShowAsync(xamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                return null;
            }

            // The bot format string: r|w?[dD]?[tT]?. 'r' can't be combined with anything, and
            // a part can only be short OR long, so the boxes are read in that priority.
            var format = string.Empty;

            if (ApiInfo.IsPackagedRelease)
            {
                if (relative.IsChecked is true)
                {
                    format = "r";
                }
                else
                {
                    if (day_of_week.IsChecked is true)
                    {
                        format += "w";
                    }

                    if (short_date.IsChecked is true)
                    {
                        format += "d";
                    }
                    else if (long_date.IsChecked is true)
                    {
                        format += "D";
                    }

                    if (short_time.IsChecked is true)
                    {
                        format += "t";
                    }
                    else if (long_time.IsChecked is true)
                    {
                        format += "T";
                    }
                }
            }

            return TdExtensions.ToDateUrl(popup.Value.ToUnixTimeSeconds(), format);
        }
    }
}
