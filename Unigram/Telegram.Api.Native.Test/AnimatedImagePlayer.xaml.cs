using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unigram.Native;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Il modello di elemento Pagina vuota è documentato all'indirizzo https://go.microsoft.com/fwlink/?LinkId=234238

namespace Telegram.Api.Native.Test
{
    /// <summary>
    /// Pagina vuota che può essere usata autonomamente oppure per l'esplorazione all'interno di un frame.
    /// </summary>
    public sealed partial class AnimatedImagePlayer : Page
    {
        private AnimatedImageSourceRendererFactory m_factory;

        public AnimatedImagePlayer()
        {
            this.InitializeComponent();

            m_factory = new AnimatedImageSourceRendererFactory();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            var assetsFolder = await Package.Current.InstalledLocation.GetFolderAsync("Assets");
            var gifsFolder = await assetsFolder.GetFolderAsync("GIFs");
            var gifFiles = await gifsFolder.GetFilesAsync();

            GIFListView.ItemsSource = await Task.WhenAll(gifFiles.Select(async (g) =>
            {
                try
                {
                    var imageProperties = await g.Properties.GetVideoPropertiesAsync();
                    if (imageProperties == null || imageProperties.Width * imageProperties.Height == 0)
                    {
                        return null;
                    }

                    var renderer = m_factory.CreateRenderer((int)imageProperties.Width, (int)imageProperties.Height);
                    var imageSourceStream = await g.OpenReadAsync();

                    await renderer.SetSourceAsync(imageSourceStream);
                    return renderer;
                }
                catch (Exception)
                {
                    return null;
                }
            }).Where(r => r != null));
        }
    }
}
