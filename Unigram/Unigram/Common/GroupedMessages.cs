using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Core.Common;

namespace Unigram.Common
{
    public class GroupedMessagePosition
    {
        public float AspectRatio { get; set; }
        public bool Bottom { get; set; }
        public bool Edge { get; set; }
        public bool Last { get; set; }
        public byte MaxX { get; set; }
        public byte MaxY { get; set; }
        public byte MinX { get; set; }
        public byte MinY { get; set; }
        public float ph { get; set; }
        public int pw { get; set; }
        public int SpanSize { get; set; }

        public void Set(int minX, int maxX, int minY, int maxY, int w, float h)
        {
            MinX = (byte)minX;
            MaxX = (byte)maxX;
            MinY = (byte)minY;
            MaxY = (byte)maxY;
            pw = w;
            SpanSize = w;
            ph = h;
        }
    }

    public class GroupedMessages
    {
        private List<MessageGroupedLayoutAttempt> _attempts;
        private List<GroupedMessagePosition> _posArray = new List<GroupedMessagePosition>();

        public long GroupedId { get; set; }

        public IList<TLMessage> Messages { get; } = new UniqueList<long, TLMessage>(x => x.RandomId ?? x.Id);
        public Dictionary<TLMessage, GroupedMessagePosition> Positions { get; } = new Dictionary<TLMessage, GroupedMessagePosition>();

        private class MessageGroupedLayoutAttempt
        {
            public float[] Heights { get; private set; }
            public int[] LineCounts { get; private set; }

            public MessageGroupedLayoutAttempt(int i1, int i2, float f1, float f2)
            {
                LineCounts = new int[] { i1, i2 };
                Heights = new float[] { f1, f2 };
            }

            public MessageGroupedLayoutAttempt(int i1, int i2, int i3, float f1, float f2, float f3)
            {
                LineCounts = new int[] { i1, i2, i3 };
                Heights = new float[] { f1, f2, f3 };
            }
        }

        private float MultiHeight(float[] array, int start, int end)
        {
            float sum = 0.0f;
            for (int a = start; a < end; a++)
            {
                sum += array[a];
            }

            return 700.0f / sum;
        }

        public void Calculate()
        {
            _posArray.Clear();
            Positions.Clear();

            int count = Messages.Count;
            if (count > 1)
            {
                StringBuilder proportions = new StringBuilder();
                float averageAspectRatio = 1.0f;
                bool isOut = false;
                int a = 0;
                while (a < count)
                {
                    TLMessage messageObject = Messages[a];
                    TLVector<TLPhotoSizeBase> photoThumbs = null;
                    if (a == 0)
                    {
                        isOut = messageObject.IsOut && !messageObject.IsSaved();
                        isOut = false;
                    }
                    if (messageObject.Media is TLMessageMediaPhoto photoMedia && photoMedia.Photo is TLPhoto photo)
                    {
                        photoThumbs = photo.Sizes;
                    }
                    else if (messageObject.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
                    {
                        photoThumbs = new TLVector<TLPhotoSizeBase> { document.Thumb };
                    }
                    TLPhotoSizeBase photoSize = GetClosestPhotoSizeWithSize(photoThumbs, 1280);
                    int w = 0;
                    int h = 0;
                    if (photoSize is TLPhotoSize size)
                    {
                        w = size.W;
                        h = size.H;
                    }
                    else if (photoSize is TLPhotoCachedSize cachedSize)
                    {
                        w = cachedSize.W;
                        h = cachedSize.H;
                    }

                    GroupedMessagePosition position = new GroupedMessagePosition();
                    position.Last = a == count + -1;
                    position.AspectRatio = w / (float)h;
                    if (position.AspectRatio > 1.2f)
                    {
                        proportions.Append("w");
                    }
                    else if (position.AspectRatio < 0.8f)
                    {
                        proportions.Append("n");
                    }
                    else
                    {
                        proportions.Append("q");
                    }
                    averageAspectRatio += position.AspectRatio;
                    Positions[messageObject] = position;
                    _posArray.Add(position);
                    a++;
                }
                float maxAspectRatio = 700.0f / 814.0f;
                averageAspectRatio /= count;
                float height;
                GroupedMessagePosition pos;
                if (count == 2 || count == 3 || count == 4)
                {
                    byte maxX = 0;
                    byte maxY = 0;
                    GroupedMessagePosition position1;
                    GroupedMessagePosition position2;
                    GroupedMessagePosition position3;
                    if (count == 2)
                    {
                        position1 = _posArray[0];
                        position2 = _posArray[1];
                        String pString = proportions.ToString();
                        if (!pString.Equals("ww") || averageAspectRatio <= 1.4d * maxAspectRatio || position1.AspectRatio - position2.AspectRatio >= 0.2d)
                        {
                            if (!pString.Equals("ww"))
                            {
                                if (!pString.Equals("qq"))
                                {
                                    int firstWidth = (int)Math.Round((700.0f / position2.AspectRatio) / ((1.0f / position1.AspectRatio) + (1.0f / position2.AspectRatio)));
                                    int secondWidth = 700 - firstWidth;
                                    height = Math.Min(814.0f, (float)Math.Round(Math.Min(firstWidth / position1.AspectRatio, secondWidth / position2.AspectRatio))) / 814.0f;
                                    position1.Set(0, 0, 0, 0, firstWidth, height);
                                    position2.Set(1, 1, 0, 0, secondWidth, height);
                                    maxX = 1;
                                }
                            }
                            height = ((float)Math.Round(Math.Min(350 / position1.AspectRatio, Math.Min(350 / position2.AspectRatio, 814.0f)))) / 814.0f;
                            position1.Set(0, 0, 0, 0, 350, height);
                            position2.Set(1, 1, 0, 0, 350, height);
                            maxX = 1;
                        }
                        else
                        {
                            height = ((float)Math.Round(Math.Min(700.0f / position1.AspectRatio, Math.Min(700.0f / position2.AspectRatio, 814.0f / 2.0f)))) / 814.0f;
                            position1.Set(0, 0, 0, 0, 700, height);
                            position2.Set(0, 0, 1, 1, 700, height);
                            maxY = 1;
                        }
                    }
                    else if (count == 3)
                    {
                        position1 = _posArray[0];
                        position2 = _posArray[1];
                        position3 = _posArray[2];
                        float secondHeight;
                        if (proportions.ToString().Equals("www"))
                        {
                            float firstHeight = ((float)Math.Round(Math.Min(700.0f / position1.AspectRatio, 0.66f * 814.0f))) / 814.0f;
                            position1.Set(0, 1, 0, 0, 700, firstHeight);
                            secondHeight = Math.Min(814.0f - firstHeight, (float)Math.Round(Math.Min(350 / position2.AspectRatio, 350 / position3.AspectRatio))) / 814.0f;
                            position2.Set(0, 0, 1, 1, 350, secondHeight);
                            position3.Set(1, 1, 1, 1, 350, secondHeight);
                            maxX = 1;
                            maxY = 1;
                        }
                        else
                        {
                            int leftWidth = (int)Math.Round(Math.Min(position1.AspectRatio * 814.0f, 525.0f));
                            position1.Set(0, 0, 0, 1, leftWidth, 1.0f);
                            float thirdHeight = (float)Math.Round((position2.AspectRatio * 700.0f) / (position3.AspectRatio + position2.AspectRatio));
                            secondHeight = 814.0f - thirdHeight;
                            int rightWidth = (int)Math.Min(700 - leftWidth, Math.Round(Math.Min(position3.AspectRatio * thirdHeight, position2.AspectRatio * secondHeight)));
                            position2.Set(1, 1, 0, 0, rightWidth, secondHeight / 814.0f);
                            position3.Set(0, 1, 1, 1, rightWidth, thirdHeight / 814.0f);
                            position3.SpanSize = 700;
                            maxX = 1;
                            maxY = 1;
                        }
                    }
                    else if (count == 4)
                    {
                        position1 = _posArray[0];
                        position2 = _posArray[1];
                        position3 = _posArray[2];
                        GroupedMessagePosition position4 = _posArray[3];
                        float h0;
                        int w0;
                        if (proportions.ToString().Equals("wwww"))
                        {
                            h0 = ((float)Math.Round(Math.Min(700.0f / position1.AspectRatio, 0.66f * 814.0f))) / 814.0f;
                            position1.Set(0, 2, 0, 0, 700, h0);
                            float h = (float)Math.Round(700.0f / ((position2.AspectRatio + position3.AspectRatio) + position4.AspectRatio));
                            w0 = (int)(position2.AspectRatio * h);
                            int w1 = (int)(position3.AspectRatio * h);
                            int w2 = (700 - w0) - w1;
                            h = Math.Min(814.0f - h0, h) / 814.0f;
                            position2.Set(0, 0, 1, 1, w0, h);
                            position3.Set(1, 1, 1, 1, w1, h);
                            position4.Set(2, 2, 1, 1, w2, h);
                            maxX = 2;
                            maxY = 1;
                        }
                        else
                        {
                            w0 = (int)Math.Round(Math.Min(position1.AspectRatio * 814.0f, 462.00003f));
                            int w = (int)Math.Round(814.0f / ((1.0f / _posArray[3].AspectRatio) + ((1.0f / position3.AspectRatio) + (1.0f / position2.AspectRatio))));
                            h0 = w / position2.AspectRatio / 814.0f;
                            float h1 = w / position3.AspectRatio / 814.0f;
                            float h2 = w / _posArray[3].AspectRatio / 814.0f;
                            position1.Set(0, 0, 0, 2, w0, (h0 + h1) + h2);
                            w = Math.Min(700 - w0, w);
                            position2.Set(1, 1, 0, 0, w, h0);
                            position3.Set(0, 1, 1, 1, w, h1);
                            position3.SpanSize = 700;
                            position4.Set(0, 1, 2, 2, w, h2);
                            position4.SpanSize = 700;
                            maxX = 1;
                            maxY = 2;
                        }
                    }
                    for (a = 0; a < count; a++)
                    {
                        pos = _posArray[a];
                        if (pos.MaxY == maxY)
                        {
                            pos.Bottom = true;
                        }
                        if (isOut)
                        {
                            if (pos.MinX == 0)
                            {
                                pos.SpanSize += 300;
                            }
                            if (pos.MaxX == maxX)
                            {
                                pos.Edge = true;
                            }
                        }
                        else
                        {
                            if (pos.MaxX == maxX)
                            {
                                pos.SpanSize += 300;
                            }
                            if (pos.MinX == 0)
                            {
                                pos.Edge = true;
                            }
                        }
                    }
                    return;
                }
                int firstLine;
                int secondLine;
                float[] croppedRatios = new float[_posArray.Count];
                for (a = 0; a < count; a++)
                {
                    if (averageAspectRatio > 1.1f)
                    {
                        croppedRatios[a] = Math.Max(1.0f, _posArray[a].AspectRatio);
                    }
                    else
                    {
                        croppedRatios[a] = Math.Min(1.0f, _posArray[a].AspectRatio);
                    }
                }
                _attempts = new List<MessageGroupedLayoutAttempt>();
                for (firstLine = 1; firstLine < croppedRatios.Length; firstLine++)
                {
                    secondLine = croppedRatios.Length - firstLine;
                    if (firstLine <= 4 && secondLine <= 4)
                    {
                        _attempts.Add(new MessageGroupedLayoutAttempt(firstLine, secondLine, MultiHeight(croppedRatios, 0, firstLine), MultiHeight(croppedRatios, firstLine, croppedRatios.Length)));
                    }
                }
                for (firstLine = 1; firstLine < croppedRatios.Length - 1; firstLine++)
                {
                    secondLine = 1;
                    while (secondLine < croppedRatios.Length - firstLine)
                    {
                        int thirdLine = (croppedRatios.Length - firstLine) - secondLine;
                        if (firstLine <= 4 && secondLine <= 4 && thirdLine <= 4)
                        {
                            _attempts.Add(new MessageGroupedLayoutAttempt(firstLine, secondLine, thirdLine, MultiHeight(croppedRatios, 0, firstLine), MultiHeight(croppedRatios, firstLine, firstLine + secondLine), MultiHeight(croppedRatios, firstLine + secondLine, croppedRatios.Length)));
                        }
                        secondLine++;
                    }
                }
                MessageGroupedLayoutAttempt optimal = null;
                float optimalDiff = 0.0f;
                for (a = 0; a < _attempts.Count; a++)
                {
                    MessageGroupedLayoutAttempt attempt = _attempts[a];
                    height = 0.0f;
                    foreach (float f in attempt.Heights)
                    {
                        height += f;
                    }
                    float diff = Math.Abs(height - 814.0f);
                    if (attempt.LineCounts.Length > 1 && (attempt.LineCounts[0] > attempt.LineCounts[1] || (attempt.LineCounts.Length > 2 && attempt.LineCounts[1] > attempt.LineCounts[2])))
                    {
                        diff *= 1.1f;
                    }
                    if (optimal == null || diff < optimalDiff)
                    {
                        optimal = attempt;
                        optimalDiff = diff;
                    }
                }
                int index = 0;
                float y = 0.0f;
                for (int i = 0; i < optimal.LineCounts.Length; i++)
                {
                    int c = optimal.LineCounts[i];
                    float lineHeight = optimal.Heights[i];
                    for (int k = 0; k < c; k++)
                    {
                        pos = _posArray[index];
                        pos.Set(k, k, i, i, (int)(croppedRatios[index] * lineHeight), lineHeight / 814.0f);
                        if (i == optimal.LineCounts.Length)
                        {
                            pos.Bottom = true;
                        }
                        if (isOut)
                        {
                            if (k == 0)
                            {
                                pos.SpanSize += 300;
                            }
                            if (k == c - 1)
                            {
                                pos.Edge = true;
                            }
                        }
                        else
                        {
                            if (k == c - 1)
                            {
                                pos.SpanSize += 300;
                            }
                            if (k == 0)
                            {
                                pos.Edge = true;
                            }
                        }
                        index++;
                    }
                    y += lineHeight;
                }
            }
        }

        public static TLPhotoSizeBase GetClosestPhotoSizeWithSize(TLVector<TLPhotoSizeBase> sizes, int side)
        {
            return GetClosestPhotoSizeWithSize(sizes, side, false);
        }

        public static TLPhotoSizeBase GetClosestPhotoSizeWithSize(TLVector<TLPhotoSizeBase> sizes, int side, bool byMinSide)
        {
            if (sizes == null || sizes.IsEmpty())
            {
                return null;
            }
            int lastSide = 0;
            TLPhotoSizeBase closestObject = null;
            for (int a = 0; a < sizes.Count; a++)
            {
                TLPhotoSizeBase obj = sizes[a];
                if (obj == null)
                {
                    continue;
                }

                int w = 0;
                int h = 0;
                if (obj is TLPhotoSize size)
                {
                    w = size.W;
                    h = size.H;
                }
                else if (obj is TLPhotoCachedSize cachedSize)
                {
                    w = cachedSize.W;
                    h = cachedSize.H;
                }

                if (byMinSide)
                {
                    int currentSide = h >= w ? w : h;
                    if (closestObject == null || side > 100 && closestObject is TLPhotoSize closestSize && closestSize.Location is TLFileLocation location && location.DCId == int.MinValue || obj is TLPhotoCachedSize || side > lastSide && lastSide < currentSide)
                    {
                        closestObject = obj;
                        lastSide = currentSide;
                    }
                }
                else
                {
                    int currentSide = w >= h ? w : h;
                    if (closestObject == null || side > 100 && closestObject is TLPhotoSize closestSize && closestSize.Location is TLFileLocation location && location.DCId == int.MinValue || obj is TLPhotoCachedSize || currentSide <= side && lastSide < currentSide)
                    {
                        closestObject = obj;
                        lastSide = currentSide;
                    }
                }
            }
            return closestObject;
        }
    }
}
