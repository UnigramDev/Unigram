using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.Views;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls.Chats
{
    public class ChatBackgroundPresenter : ContentControl, IHandle<UpdateWallpaper>
    {
        private int _session;
        private IEventAggregator _aggregator;

        private ChatBackground _defaultBackground;
        private Rectangle _imageBackground;
        private Rectangle _colorBackground;

        public ChatBackgroundPresenter()
        {
            //Update();
        }

        public void Handle(UpdateWallpaper update)
        {
            this.BeginOnUIThread(() => Update(_session, update.Background, update.Color));
        }

        public void Update(int session, ISettingsService settings, IEventAggregator aggregator)
        {
            _session = session;
            _aggregator = aggregator;

            aggregator.Subscribe(this);
            Update(session, settings.SelectedBackground, settings.SelectedColor);
        }

        private async void Update(int session, int background, int color)
        {
            try
            {
                if (color == 0)
                {
                    if (background != 1000001)
                    {
                        var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync($"{session}\\{Constants.WallpaperFileName}");
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
                        _defaultBackground = new ChatBackground();

                    Content = _defaultBackground;
                }
                else
                {
                    if (_colorBackground == null)
                        _colorBackground = new Rectangle();

                    _colorBackground.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF,
                        (byte)((color >> 16) & 0xFF),
                        (byte)((color >> 8) & 0xFF),
                        (byte)((color & 0xFF))));

                    Content = _colorBackground;
                }
            }
            catch
            {
                if (_defaultBackground == null)
                    _defaultBackground = new ChatBackground();

                Content = _defaultBackground;
            }
        }
    }
}
