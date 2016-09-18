using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    class UpdateProfilePictureColor
    {
        static int GetColorIndex(int id)
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
                byte[] digest;
                CryptographicBuffer.CopyToByteArray(hashed, out digest);
                var boh = ((id & 0x300000000) == 0x300000000);
                TLPeerUser x = new TLPeerUser { Id = id };
                return digest[id % 0x0F] & ((x is TLPeerUser) ?   0x03 : 0x07);
            }
            catch { }

            return id % 8;
        }

        public static Brush UpdatePicture(int id)
        {
            var x = GetColorIndex(id);
            switch (x)
            {
                case 0:
                    return Application.Current.Resources["RedBrush"] as SolidColorBrush;
                case 1:
                    return Application.Current.Resources["GreenBrush"] as SolidColorBrush;
                case 2:
                    return Application.Current.Resources["YellowBrush"] as SolidColorBrush;
                case 3:
                    return Application.Current.Resources["BlueBrush"] as SolidColorBrush;
                case 4:
                    return Application.Current.Resources["PurpleBrush"] as SolidColorBrush;
                case 5:
                    return Application.Current.Resources["PinkBrush"] as SolidColorBrush;
                case 6:
                    return Application.Current.Resources["CyanBrush"] as SolidColorBrush;
                case 7:
                    return Application.Current.Resources["OrangeBrush"] as SolidColorBrush;
                default:
                    return Application.Current.Resources["ListViewItemPlaceholderBackgroundThemeBrush"] as SolidColorBrush;
            }
        }
    }
}
