using System;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class EditToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var replyInfo = value as MessageComposerHeader;
            if (replyInfo != null)
            {
                if (replyInfo.EditingMessage != null)
                {
                    return parameter != null ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            return parameter == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
