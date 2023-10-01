//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Popups
{
    public sealed partial class IdenticonPopup : ContentPopup
    {
        public IdenticonPopup(int sessionId, Chat chat)
        {
            InitializeComponent();
            Title = Strings.EncryptionKey;

            PrimaryButtonText = Strings.Close;

            if (chat.Type is ChatTypeSecret secret)
            {
                var service = TLContainer.Current.Resolve<IClientService>(sessionId);

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

                Layout(secretChat.KeyHash);
                Hash.Text = Stringify(secretChat.KeyHash);

                TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.EncryptionKeyDescription, user.FirstName, user.FirstName));
            }
        }

        private void Layout(IList<byte> hash)
        {
            float width = 192;
            float height = 192;

            float length = hash.Count == 16 ? 8 : 12;

            var layers = new GeometryGroup[]
            {
                new GeometryGroup(),
                new GeometryGroup(),
                new GeometryGroup(),
                new GeometryGroup()
            };

            int bitPointer = 0;
            float rectSize = MathF.Floor(MathF.Min(width, height) / length);
            float xOffset = MathF.Max(0.0f, (width - rectSize * length) / 2);
            float yOffset = MathF.Max(0.0f, (height - rectSize * length) / 2);

            for (int iy = 0; iy < length; iy++)
            {
                for (int ix = 0; ix < length; ix++)
                {
                    int byteValue = (hash[bitPointer / 8] >> (bitPointer % 8)) & 0x3;
                    int colorIndex = Math.Abs(byteValue) % 4;
                    bitPointer += 2;

                    layers[colorIndex].Children.Add(new RectangleGeometry
                    {
                        Rect = new(xOffset + ix * rectSize, iy * rectSize + yOffset, rectSize, rectSize)
                    });
                }
            }

            Layer0.Data = layers[0];
            Layer1.Data = layers[1];
            Layer2.Data = layers[2];
            Layer3.Data = layers[3];
        }

        private string Stringify(IList<byte> hash)
        {
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

            return builder.ToString();
        }
    }
}
