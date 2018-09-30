using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.Graphics.Imaging;

namespace Unigram.Common
{
    public static class ApiInfo
    {
        private static bool? _canRenderWebP;
        public static bool CanDecodeWebp => (_canRenderWebP = _canRenderWebP ?? ApiInformation.IsReadOnlyPropertyPresent("Windows.Graphics.Imaging.BitmapDecoder", "WebpDecoderId")) ?? false;
    }
}
