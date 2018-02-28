using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Views;
using Unigram.Native;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml;
using Windows.UI.ViewManagement;
using TdWindows;

namespace Unigram.Converters
{
    // TEMP
    public class DefaultPhotoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        public static object Convert(object value)
        {
            return null;
        }

        public static object Convert(object value, bool thumbnail)
        {
            return null;
        }
    }
}
