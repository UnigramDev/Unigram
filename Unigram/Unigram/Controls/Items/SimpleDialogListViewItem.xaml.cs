using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Items
{
    public sealed partial class SimpleDialogListViewItem : UserControl
    {
        public TLDialog ViewModel => DataContext as TLDialog;
        private TLDialog _oldViewModel;

        public SimpleDialogListViewItem()
        {
            this.InitializeComponent();

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (_oldViewModel != null)
            {
                _oldViewModel.PropertyChanged -= OnPropertyChanged;
                _oldViewModel = null;
            }

            if (ViewModel != null)
            {
                _oldViewModel = ViewModel;
                ViewModel.PropertyChanged += OnPropertyChanged;

                UpdatePicture();
            }
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Self")
            {

            }
            else if (e.PropertyName == "TopMessageItem")
            {

            }
            else if (e.PropertyName == "UnreadCount")
            {

            }
            else if (e.PropertyName == "With")
            {
                UpdatePicture();
            }
        }

        private void UpdatePicture()
        {
            switch (GetColorIndex(ViewModel.WithId))
            {
                case 0:
                    Placeholder.Fill = Application.Current.Resources["PlaceholderRedBrush"] as SolidColorBrush;
                    break;
                case 1:
                    Placeholder.Fill = Application.Current.Resources["PlaceholderGreenBrush"] as SolidColorBrush;
                    break;
                case 2:
                    Placeholder.Fill = Application.Current.Resources["PlaceholderYellowBrush"] as SolidColorBrush;
                    break;
                case 3:
                    Placeholder.Fill = Application.Current.Resources["PlaceholderBlueBrush"] as SolidColorBrush;
                    break;
                case 4:
                    Placeholder.Fill = Application.Current.Resources["PlaceholderPurpleBrush"] as SolidColorBrush;
                    break;
                case 5:
                    Placeholder.Fill = Application.Current.Resources["PlaceholderPinkBrush"] as SolidColorBrush;
                    break;
                case 6:
                    Placeholder.Fill = Application.Current.Resources["PlaceholderCyanBrush"] as SolidColorBrush;
                    break;
                case 7:
                    Placeholder.Fill = Application.Current.Resources["PlaceholderOrangeBrush"] as SolidColorBrush;
                    break;
                default:
                    Placeholder.Fill = Application.Current.Resources["PlaceholderListViewItemPlaceholderBackgroundThemeBrush"] as SolidColorBrush;
                    break;
            }
        }

        private int GetColorIndex(int id)
        {
            if (id < 0)
            {
                id += 256;
            }

            try
            {
                var str = string.Format("{0}{1}", id, MTProtoService.Current.CurrentUserId);
                if (str.Length > 15)
                {
                    str = str.Substring(0, 15);
                }

                var input = CryptographicBuffer.ConvertStringToBinary(str, BinaryStringEncoding.Utf8);
                var hasher = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
                var hashed = hasher.HashData(input);
                CryptographicBuffer.CopyToByteArray(hashed, out byte[] digest);

                var boh = ((id & 0x300000000) == 0x300000000);

                return digest[id % 0x0F] & ((ViewModel.With is TLPeerUser) ? 0x07 : 0x03);
            }
            catch { }

            return id % 8;
        }

        private void UpdateTimeLabel()
        {
            var topMessage = ViewModel?.TopMessageItem as TLMessageBase;
            if (topMessage != null)
            {
                var clientDelta = MTProtoService.Current.ClientTicksDelta;
                var utc0SecsLong = topMessage.Date * 4294967296 - clientDelta;
                var utc0SecsInt = utc0SecsLong / 4294967296.0;
                var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

                var cultureInfo = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
                var shortTimePattern = Utils.GetShortTimePattern(ref cultureInfo);         
            }
        }

        private bool IsOut(TLDialog dialog)
        {
            var topMessage = dialog.TopMessageItem as TLMessage;
            if (topMessage != null /*&& topMessage.ShowFrom*/)
            {
                var from = topMessage.FromId;
                if (from != null)
                {
                    int currentUserId = MTProtoService.Current.CurrentUserId;
                    if (currentUserId == from.Value)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
