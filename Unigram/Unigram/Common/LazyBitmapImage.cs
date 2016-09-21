using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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

        public static async void SetByteSource(this BitmapImage bitmap, byte[] data)
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(data);
                    await writer.StoreAsync();
                }
                await bitmap.SetSourceAsync(stream);
            }
        }

        #region Source

        public static object GetSource(DependencyObject obj)
        {
            return (object)obj.GetValue(SourceProperty);
        }

        public static void SetSource(DependencyObject obj, object value)
        {
            obj.SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.RegisterAttached("Source", typeof(object), typeof(LazyBitmapImage), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as Image;
            var photo = e.NewValue as TLPhoto;
            if (photo != null)
            {
                var photoSize = photo.Sizes.OrderByDescending(x => Math.Abs(x.W * x.H)).FirstOrDefault();
                if (photoSize != null)
                {
                    sender.MaxWidth = photoSize.W;
                    sender.MaxHeight = photoSize.H;
                }
            }
        }

        #endregion

    }
}
