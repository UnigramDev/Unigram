using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Core.Services;
using Unigram.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Users
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class UserKeyHashPage : Page
    {
        public UserKeyHashPage()
        {
            this.InitializeComponent();
        }

        private Color[] colors = new Color[] {
            Color.FromArgb(0xff, 0xff, 0xff, 0xff),
            Color.FromArgb(0xff, 0xd5, 0xe6, 0xf3),
            Color.FromArgb(0xff, 0x2d, 0x57, 0x75),
            Color.FromArgb(0xff, 0x2f, 0x99, 0xc9)
    };

        private int getBits(IList<byte> data, int bitOffset)
        {
            return (data[bitOffset / 8] >> (bitOffset % 8)) & 0x3;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var service = UnigramContainer.Current.ResolveType<IProtoService>();
            var data = TLSerializationService.Current.Deserialize<long>((string)e.Parameter);

            var chat = service.GetChat(data);
            if (chat.Type is ChatTypeSecret secret)
            {
                var secretChat = service.GetSecretChat(secret.SecretChatId);
                if (secretChat == null)
                {
                    return;
                }

                var user = service.GetUser(secret.UserId);
                if (user == null)
                {
                    return;
                }

                //var width = 160;
                //var height = 160;

                //var bitmap = BitmapFactory.New(width, height);

                var hash = secretChat.KeyHash;
                //if (hash.Count == 16)
                //{
                //    int bitPointer = 0;
                //    float rectSize = (float)Math.Floor(Math.Min(width, height) / 8.0f);
                //    float xOffset = Math.Max(0, (width - rectSize * 8) / 2);
                //    float yOffset = Math.Max(0, (height - rectSize * 8) / 2);
                //    for (int iy = 0; iy < 8; iy++)
                //    {
                //        for (int ix = 0; ix < 8; ix++)
                //        {
                //            int byteValue = getBits(hash, bitPointer);
                //            bitPointer += 2;
                //            int colorIndex = Math.Abs(byteValue) % 4;
                //            bitmap.FillRectangle((int)(xOffset + ix * rectSize), (int)(iy * rectSize + yOffset), (int)(xOffset + ix * rectSize + rectSize), (int)(iy * rectSize + rectSize + yOffset), colors[colorIndex]);
                //        }
                //    }
                //}
                //else
                //{
                //    int bitPointer = 0;
                //    float rectSize = (float)Math.Floor(Math.Min(width, height) / 12.0f);
                //    float xOffset = Math.Max(0, (width - rectSize * 12) / 2);
                //    float yOffset = Math.Max(0, (height - rectSize * 12) / 2);
                //    for (int iy = 0; iy < 12; iy++)
                //    {
                //        for (int ix = 0; ix < 12; ix++)
                //        {
                //            int byteValue = getBits(hash, bitPointer);
                //            int colorIndex = Math.Abs(byteValue) % 4;
                //            bitmap.FillRectangle((int)(xOffset + ix * rectSize), (int)(iy * rectSize + yOffset), (int)(xOffset + ix * rectSize + rectSize), (int)(iy * rectSize + rectSize + yOffset), colors[colorIndex]);
                //            bitPointer += 2;
                //        }
                //    }
                //}

                var bitmap = PlaceholderHelper.GetIdenticon(hash);


                var builder = new StringBuilder();

                if (hash.Count > 16)
                {
                    var hex = BitConverter.ToString(hash.ToArray()).Replace("-", string.Empty);
                    for (int a = 0; a < 32; a++)
                    {
                        if (a != 0)
                        {
                            if (a % 8 == 0)
                            {
                                builder.Append('\n');
                            }
                            else if (a % 4 == 0)
                            {
                                builder.Append(' ');
                            }
                        }

                        builder.Append(hex.Substring(a * 2, 2));
                        builder.Append(' ');
                    }

                    builder.Append("\n");
                }

                Texture.Source = bitmap;
                Hash.Text = builder.ToString();

                TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.EncryptionKeyDescription, user.FirstName, user.FirstName));
            }
        }
    }
}
