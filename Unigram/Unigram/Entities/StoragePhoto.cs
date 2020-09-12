using System;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Unigram.Entities
{
    public class StoragePhoto : StorageMedia
    {
        private BasicProperties _basic;

        public StoragePhoto(StorageFile file, BasicProperties basic, ImageProperties props)
            : base(file, basic)
        {
            _basic = basic;

            Properties = props;
        }

        public override uint Width => Properties.GetWidth();
        public override uint Height => Properties.GetHeight();

        //private bool? _isAnimatable;
        //public override bool IsAnimatable
        //{
        //    get
        //    {
        //        if (_isAnimatable == null)
        //        {
        //            try
        //            {
        //                using (var archive = ZipFile.OpenRead(File.Path))
        //                {
        //                    _isAnimatable = true;
        //                }
        //            }
        //            catch
        //            {
        //                _isAnimatable = false;
        //            }
        //        }

        //        return _isAnimatable ?? false;
        //    }
        //}

        public ImageProperties Properties { get; private set; }

        public new static async Task<StoragePhoto> CreateAsync(StorageFile file)
        {
            try
            {
                if (!file.IsAvailable)
                {
                    return null;
                }

                var basic = await file.GetBasicPropertiesAsync();
                var image = await file.Properties.GetImagePropertiesAsync();

                if (image.Width >= 20 * image.Height || image.Height >= 20 * image.Width)
                {
                    return null;
                }

                if (image.Width > 0 && image.Height > 0)
                {
                    return new StoragePhoto(file, basic, image);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
