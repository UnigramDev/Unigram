using System;
using System.Globalization;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Windows.Globalization.DateTimeFormatting;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class TLIntToDateTimeConverter : DependencyObject, IValueConverter
    {
        #region TodayFormat
        public static readonly DependencyProperty TodayFormatProperty =
            DependencyProperty.Register("TodayFormat", 
                                        typeof (string), typeof (TLIntToDateTimeConverter),
                                        new PropertyMetadata(default(string)));

        public string TodayFormat
        {
            get { return (string) GetValue(TodayFormatProperty); }
            set { SetValue(TodayFormatProperty, value); }
        }
        #endregion

        #region YesterdayString
        public static readonly DependencyProperty YesterdayStringProperty =
            DependencyProperty.Register("YesterdayString", typeof (string), typeof (TLIntToDateTimeConverter), new PropertyMetadata(default(string)));

        public string YesterdayString
        {
            get { return (string) GetValue(YesterdayStringProperty); }
            set { SetValue(YesterdayStringProperty, value); }
        }
        #endregion

        #region YesterdayFormat
        public static readonly DependencyProperty YesterdayFormatProperty =
            DependencyProperty.Register("YesterdayFormat", 
                                        typeof (string), typeof (TLIntToDateTimeConverter),
                                        new PropertyMetadata(default(string)));

        public string YesterdayFormat
        {
            get { return (string) GetValue(YesterdayFormatProperty); }
            set { SetValue(YesterdayFormatProperty, value); }
        }
        #endregion

        #region WeekFormat
        public string WeekFormat
        {
            get { return (string)GetValue(WeekFormatProperty); }
            set { SetValue(WeekFormatProperty, value); }
        }

        public static readonly DependencyProperty WeekFormatProperty =
            DependencyProperty.Register("WeekFormat", typeof(string), typeof(TLIntToDateTimeConverter), new PropertyMetadata(default(string)));
        #endregion

        #region RegularFormat
        public static readonly DependencyProperty RegularFormatProperty =
            DependencyProperty.Register("RegularFormat", 
                                        typeof (string), typeof (TLIntToDateTimeConverter),
                                        new PropertyMetadata(default(string)));

        public string RegularFormat
        {
            get { return (string) GetValue(RegularFormatProperty); }
            set { SetValue(RegularFormatProperty, value); }
        }
        #endregion

        public static readonly DependencyProperty LongRegularFormatProperty =
            DependencyProperty.Register("LongRegularFormat", 
                                        typeof (string), typeof (TLIntToDateTimeConverter), 
                                        new PropertyMetadata(default(string)));

        public string LongRegularFormat
        {
            get { return (string) GetValue(LongRegularFormatProperty); }
            set { SetValue(LongRegularFormatProperty, value); }
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var clientDelta = MTProtoService.Current.ClientTicksDelta;
            var utc0SecsLong = (int)value * 4294967296 - clientDelta;
            var utc0SecsInt = utc0SecsLong / 4294967296.0;
            var dateTime = Telegram.Api.Helpers.Utils.UnixTimestampToDateTime(utc0SecsInt);

            var cultureInfo = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
            var shortTimePattern = Utils.GetShortTimePattern(ref cultureInfo);

            if (WeekFormat == null)
                return BindConvert.Current.ShortTime.Format(dateTime);

            //Today
            if ((dateTime.Date == DateTime.Now.Date) && !string.IsNullOrEmpty(TodayFormat))
            {
                //return dateTime.ToString(string.Format(TodayFormat, shortTimePattern), cultureInfo);
                return BindConvert.Current.ShortTime.Format(dateTime);
            }

            //Yesterday
            if ((dateTime.Date.AddDays(1) == DateTime.Now.Date) && !string.IsNullOrEmpty(YesterdayString))
            {
                return YesterdayString;
            }

            if ((dateTime.Date.AddDays(1) == DateTime.Now.Date) && !string.IsNullOrEmpty(YesterdayFormat))
            {
                return dateTime.ToString(string.Format(YesterdayFormat, shortTimePattern), cultureInfo);
            }

            if (dateTime.Date.AddDays(7) >= DateTime.Now.Date && !string.IsNullOrEmpty(WeekFormat))
            {
                return dateTime.ToString(string.Format(WeekFormat, shortTimePattern), cultureInfo);
            }

            //Long time ago (no more than one year ago)
            if (dateTime.Date.AddDays(365) >= DateTime.Now.Date && !string.IsNullOrEmpty(RegularFormat))
            {
                return dateTime.ToString(string.Format(RegularFormat, shortTimePattern), cultureInfo);
            }

            //Long long time ago
            //return dateTime.ToString(string.Format(LongRegularFormat, shortTimePattern), cultureInfo);
            return BindConvert.Current.ShortDate.Format(dateTime);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
