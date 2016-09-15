using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Common
{
    public static class LazyBitmapImage
    {
        public static async void SetUriSource(this BitmapImage bitmap, Uri uri)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            using (var stream = await file.OpenReadAsync())
            {
                await bitmap.SetSourceAsync(stream);
            }
        }
    }
}
