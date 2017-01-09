using System;
using System.Text;
using System.IO;

namespace Unigram.Core.Rtf.Write
{
    /// <summary>
    /// Summary description for RtfImage
    /// </summary>
    public class RtfImage : RtfBlock
    {
        private string _imgFname;
        private ImageFileType _imgType;
        private Byte[] _imgByte;
        private Align _alignment;
        private Margins _margins;
        private float _width;
        private float _height;
        private bool _keepAspectRatio;
        private string _blockHead;
        private string _blockTail;
        private bool _startNewPage;
        private bool _startNewPara;

        internal RtfImage(string fileName, ImageFileType type)
        {
            _imgFname = fileName;
            _imgType = type;
            _alignment = Align.None;
            _margins = new Margins();
            _keepAspectRatio = true;
            _blockHead = @"{\pard";
            _blockTail = @"}";
            _startNewPage = false;
            _startNewPara = false;
            
            // TODO:
            //Image image = Image.FromFile(fileName);
            //_width = (image.Width / image.HorizontalResolution) * 72;
            //_height = (image.Height / image.VerticalResolution) * 72;

            //using (MemoryStream mStream = new MemoryStream())
            //{
            //    image.Save(mStream, image.RawFormat);
            //    _imgByte = mStream.ToArray();
            //}
        }


        internal RtfImage(System.IO.MemoryStream imageStream)
        {
            _alignment = Align.Left;
            _margins = new Margins();
            _keepAspectRatio = true;
            _blockHead = @"{\pard";
            _blockTail = @"}";
            _startNewPage = false;
            _startNewPara = false;

            _imgByte = imageStream.ToArray();

            // TODO:
            //Image image = Image.FromStream(imageStream);
            //_width = (image.Width / image.HorizontalResolution) * 72;
            //_height = (image.Height / image.VerticalResolution) * 72;

            //if(image.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Png)) _imgType = ImageFileType.Png;
            //else if (image.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Jpeg)) _imgType = ImageFileType.Jpg;
            //else if (image.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Gif)) _imgType = ImageFileType.Gif;
            //else throw new Exception("Image format is not supported: " + image.RawFormat.ToString());
        }


        public override Align Alignment
        {
            get
            {
                return _alignment;
            }
            set
            {
                _alignment = value;
            }
        }

        public override Margins Margins
        {
            get
            {
                return _margins;
            }
        }

        public override bool StartNewPage
        {
            get
            {
                return _startNewPage;
            }
            set
            {
                _startNewPage = value;
            }
        }

        public bool StartNewPara
        {
            get
            {
                return _startNewPara;
            }
            set
            {
                _startNewPara = value;
            }
        }

        public float Width
        {
            get
            {
                return _width;
            }
            set
            {
                if (_keepAspectRatio && _width > 0) {
                    float ratio = _height / _width;
                    _height = value * ratio;
                }
                _width = value;
            }
        }

        public float Heigth
        {
            get
            {
                return _height;
            }
            set
            {
                if (_keepAspectRatio && _height > 0) {
                    float ratio = _width / _height;
                    _width = value * ratio;
                }
                _height = value;
            }
        }
        
        public bool KeepAspectRatio
        {
            get
            {
                return _keepAspectRatio;
            }
            set
            {
                _keepAspectRatio = value;
            }
        }

        public override RtfCharFormat DefaultCharFormat
        {
            // DefaultCharFormat is meaningless for RtfImage.
            get
            {
                return null;
            }
        }

        private string extractImage()
        {
            StringBuilder result = new StringBuilder();

            for (int i = 0; i < _imgByte.Length; i++)
            {
                if (i != 0 && i % 60 == 0)
                {
                    result.AppendLine();
                }
                result.AppendFormat("{0:x2}", _imgByte[i]);
            }
            
            return result.ToString();
        }

        internal override string BlockHead
        {
            set
            {
                _blockHead = value;
            }
        }

        internal override string BlockTail
        {
            set
            {
                _blockTail = value;
            }
        }

        public override string Render()
        {
            StringBuilder result = new StringBuilder(_blockHead);

            if (_startNewPage) {
                result.Append(@"\pagebb");
            }

            if (_margins[Direction.Top] >= 0) {
                result.Append(@"\sb" + RtfUtility.pt2Twip(_margins[Direction.Top]));
            }
            if (_margins[Direction.Bottom] >= 0) {
                result.Append(@"\sa" + RtfUtility.pt2Twip(_margins[Direction.Bottom]));
            }
            if (_margins[Direction.Left] >= 0) {
                result.Append(@"\li" + RtfUtility.pt2Twip(_margins[Direction.Left]));
            }
            if (_margins[Direction.Right] >= 0) {
                result.Append(@"\ri" + RtfUtility.pt2Twip(_margins[Direction.Right]));
            }
            switch (_alignment) {
                case Align.Left:
                    result.Append(@"\ql");
                    break;
                case Align.Right:
                    result.Append(@"\qr");
                    break;
                case Align.Center:
                    result.Append(@"\qc");
                    break;
            }
            result.AppendLine();

            result.Append(@"{\*\shppict{\pict");
            if (_imgType == ImageFileType.Jpg) {
                result.Append(@"\jpegblip");
            } else if (_imgType == ImageFileType.Png || _imgType == ImageFileType.Gif) {
                result.Append(@"\pngblip");
            } else {
                throw new Exception("Image type not supported.");
            }
            if (_height > 0) {
                result.Append(@"\pichgoal" + RtfUtility.pt2Twip(_height));
            }
            if (_width > 0) {
                result.Append(@"\picwgoal" + RtfUtility.pt2Twip(_width));
            }
            result.AppendLine();
            
            result.AppendLine(extractImage());
            result.AppendLine("}}");
            if (_startNewPara) result.Append(@"\par");
            result.AppendLine(_blockTail);
            return result.ToString();
        }
    }
}
