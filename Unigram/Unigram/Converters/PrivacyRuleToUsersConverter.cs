﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Strings;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class PrivacyRuleToUsersConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var count = System.Convert.ToInt32(value);
            if (count > 0)
            {
                return Language.Declension(count, Strings.Resources.UserNominativeSingular, Strings.Resources.UserNominativePlural, Strings.Resources.UserGenitiveSingular, Strings.Resources.UserGenitivePlural, null, null);
            }

            return "Add Users";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
