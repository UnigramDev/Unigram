using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Unigram.Common;
using Unigram.Views;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls
{
    public class DialogBackgroundPresenter : ContentControl, IHandle<string>
    {
        private DialogBackground _defaultBackground;
        private Rectangle _imageBackground;
        private Rectangle _colorBackground;

        public DialogBackgroundPresenter()
        {
            Reload();
            UnigramContainer.Current.ResolveType<ITelegramEventAggregator>().Subscribe(this);
        }

        public void Handle(string message)
        {
            if (message.Equals("Wallpaper"))
            {
                Reload();
            }
        }

        private async void Reload()
        {
            var selectedBackground = ApplicationSettings.Current.SelectedBackground;
            var selectedColor = ApplicationSettings.Current.SelectedColor;

            if (selectedColor == 0)
            {
                if (selectedBackground != 1000001)
                {
                    var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(FileUtils.GetFilePath(Constants.WallpaperFileName));
                    if (item is StorageFile file)
                    {
                        if (_imageBackground == null)
                            _imageBackground = new Rectangle();

                        using (var stream = await file.OpenReadAsync())
                        {
                            var bitmap = new BitmapImage();
                            await bitmap.SetSourceAsync(stream);
                            _imageBackground.Fill = new ImageBrush { ImageSource = bitmap, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
                        }

                        Content = _imageBackground;
                        return;
                    }
                }

                if (_defaultBackground == null)
                    _defaultBackground = new DialogBackground();

                Content = _defaultBackground;
            }
            else
            {
                if (_colorBackground == null)
                    _colorBackground = new Rectangle();

                _colorBackground.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF,
                    (byte)((selectedColor >> 16) & 0xFF),
                    (byte)((selectedColor >> 8) & 0xFF),
                    (byte)((selectedColor & 0xFF))));

                Content = _colorBackground;
            }
        }
    }
}
