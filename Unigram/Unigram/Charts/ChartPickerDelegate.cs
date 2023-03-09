//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Windows.Foundation;
using Windows.UI.Input;

namespace Telegram.Charts
{
    public class ChartPickerDelegate
    {
        public bool disabled;
        readonly IListener view;

        public ChartPickerDelegate(IListener view)
        {
            this.view = view;
        }

        private const int CAPTURE_NONE = 0;
        private const int CAPTURE_LEFT = 1;
        private const int CAPTURE_RIGHT = 1 << 1;
        private const int CAPTURE_MIDDLE = 1 << 2;
        public int pickerWidth;
        public bool tryMoveTo;
        public float moveToX;
        public float moveToY;
        public long startTapTime;
        ValueAnimator moveToAnimator;

        public Rect leftPickerArea = new Rect();
        public Rect rightPickerArea = new Rect();
        public Rect middlePickerArea = new Rect();


        public float pickerStart = 0.7f;
        public float pickerEnd = 1f;

        public float minDistance = 0.1f;


        public CapturesData GetMiddleCaptured()
        {
            if (capturedStates[0] != null && capturedStates[0].state == CAPTURE_MIDDLE)
            {
                return capturedStates[0];
            }

            if (capturedStates[1] != null && capturedStates[1].state == CAPTURE_MIDDLE)
            {
                return capturedStates[1];
            }

            return null;
        }

        public CapturesData GetLeftCaptured()
        {
            if (capturedStates[0] != null && capturedStates[0].state == CAPTURE_LEFT)
            {
                return capturedStates[0];
            }

            if (capturedStates[1] != null && capturedStates[1].state == CAPTURE_LEFT)
            {
                return capturedStates[1];
            }

            return null;
        }

        public CapturesData GetRightCaptured()
        {
            if (capturedStates[0] != null && capturedStates[0].state == CAPTURE_RIGHT)
            {
                return capturedStates[0];
            }

            if (capturedStates[1] != null && capturedStates[1].state == CAPTURE_RIGHT)
            {
                return capturedStates[1];
            }

            return null;
        }


        public class CapturesData
        {
            public readonly IListener view;
            public readonly int state;
            public int capturedX;
            public int lastMovingX;
            public float start;
            public float end;

            ValueAnimator a;
            readonly ValueAnimator jumpToAnimator;
            public float aValue = 0f;

            public CapturesData(IListener view, int state)
            {
                this.view = view;
                this.state = state;
            }

            public void captured()
            {
                a = ValueAnimator.OfFloat(0, 1f);
                a.SetDuration(600);
                a.setInterpolator(BaseChartView.INTERPOLATOR);
                a.AddUpdateListener(new AnimatorUpdateListener(animation =>
                {
                    aValue = (float)animation.GetAnimatedValue();
                    view.Invalidate();
                }));
                a.Start();
            }

            public void uncapture()
            {
                if (a != null)
                {
                    a.Cancel();
                }

                if (jumpToAnimator != null)
                {
                    jumpToAnimator.Cancel();
                }
            }
        }

        readonly CapturesData[] capturedStates = { null, null };

        public bool Capture(int x, int y, int pointerIndex)
        {
            if (disabled)
            {
                return false;
            }
            if (pointerIndex == 0)
            {
                if (leftPickerArea.Contains(new Point(x, y)))
                {
                    if (capturedStates[0] != null)
                    {
                        capturedStates[1] = capturedStates[0];
                    }

                    capturedStates[0] = new CapturesData(view, CAPTURE_LEFT);
                    capturedStates[0].start = pickerStart;
                    capturedStates[0].capturedX = x;
                    capturedStates[0].lastMovingX = x;
                    capturedStates[0].captured();

                    if (moveToAnimator != null)
                    {
                        moveToAnimator.Cancel();
                    }
                    return true;
                }

                if (rightPickerArea.Contains(new Point(x, y)))
                {
                    if (capturedStates[0] != null)
                    {
                        capturedStates[1] = capturedStates[0];
                    }

                    capturedStates[0] = new CapturesData(view, CAPTURE_RIGHT);
                    capturedStates[0].end = pickerEnd;
                    capturedStates[0].capturedX = x;
                    capturedStates[0].lastMovingX = x;
                    capturedStates[0].captured();

                    if (moveToAnimator != null)
                    {
                        moveToAnimator.Cancel();
                    }

                    return true;
                }


                if (middlePickerArea.Contains(new Point(x, y)))
                {
                    capturedStates[0] = new CapturesData(view, CAPTURE_MIDDLE);
                    capturedStates[0].end = pickerEnd;
                    capturedStates[0].start = pickerStart;
                    capturedStates[0].capturedX = x;
                    capturedStates[0].lastMovingX = x;
                    capturedStates[0].captured();
                    if (moveToAnimator != null)
                    {
                        moveToAnimator.Cancel();
                    }

                    return true;
                }


                if (y < leftPickerArea.Bottom && y > leftPickerArea.Top)
                {
                    tryMoveTo = true;
                    moveToX = x;
                    moveToY = y;
                    startTapTime = DateTime.Now.ToTimestamp() * 1000;
                    if (moveToAnimator != null)
                    {
                        if (moveToAnimator.IsRunning())
                        {
                            view.OnPickerJumpTo(pickerStart, pickerEnd, true);
                        }
                        moveToAnimator.Cancel();
                    }
                    return true;
                }
            }
            else if (pointerIndex == 1)
            {
                if (capturedStates[0] == null)
                {
                    return false;
                }

                if (capturedStates[0].state == CAPTURE_MIDDLE)
                {
                    return false;
                }

                if (leftPickerArea.Contains(new Point(x, y)) && capturedStates[0].state != CAPTURE_LEFT)
                {
                    capturedStates[1] = new CapturesData(view, CAPTURE_LEFT);
                    capturedStates[1].start = pickerStart;
                    capturedStates[1].capturedX = x;
                    capturedStates[1].lastMovingX = x;
                    capturedStates[1].captured();
                    if (moveToAnimator != null)
                    {
                        moveToAnimator.Cancel();
                    }

                    return true;
                }

                if (rightPickerArea.Contains(new Point(x, y)))
                {
                    if (capturedStates[0].state == CAPTURE_RIGHT)
                    {
                        return false;
                    }

                    capturedStates[1] = new CapturesData(view, CAPTURE_RIGHT);
                    capturedStates[1].end = pickerEnd;
                    capturedStates[1].capturedX = x;
                    capturedStates[1].lastMovingX = x;
                    capturedStates[1].captured();
                    if (moveToAnimator != null)
                    {
                        moveToAnimator.Cancel();
                    }

                    return true;
                }
            }
            return false;
        }

        public bool Captured()
        {
            return capturedStates[0] != null || tryMoveTo;
        }

        public bool Move(int x, int y, int pointer)
        {
            if (tryMoveTo)
            {
                return false;
            }
            CapturesData d = capturedStates[pointer];
            if (d == null)
            {
                return false;
            }

            int capturedState = d.state;
            float capturedStart = d.start;
            float capturedEnd = d.end;
            int capturedX = d.capturedX;
            d.lastMovingX = x;

            bool notifyPicker = false;
            if (capturedState == CAPTURE_LEFT)
            {
                pickerStart = capturedStart - (capturedX - x) / (float)pickerWidth;
                if (pickerStart < 0f)
                {
                    pickerStart = 0f;
                }

                if (pickerEnd - pickerStart < minDistance)
                {
                    pickerStart = pickerEnd - minDistance;
                }

                notifyPicker = true;
            }

            if (capturedState == CAPTURE_RIGHT)
            {
                pickerEnd = capturedEnd - (capturedX - x) / (float)pickerWidth;
                if (pickerEnd > 1f)
                {
                    pickerEnd = 1f;
                }

                if (pickerEnd - pickerStart < minDistance)
                {
                    pickerEnd = pickerStart + minDistance;
                }

                notifyPicker = true;
            }

            if (capturedState == CAPTURE_MIDDLE)
            {
                pickerStart = capturedStart - (capturedX - x) / (float)pickerWidth;
                pickerEnd = capturedEnd - (capturedX - x) / (float)pickerWidth;
                if (pickerStart < 0f)
                {
                    pickerStart = 0f;
                    pickerEnd = capturedEnd - capturedStart;
                }

                if (pickerEnd > 1f)
                {
                    pickerEnd = 1f;
                    pickerStart = 1f - (capturedEnd - capturedStart);
                }

                notifyPicker = true;
            }
            if (notifyPicker)
            {
                view.OnPickerDataChanged();
            }

            return true;
        }

        public bool Uncapture(PointerPoint point, int pointerIndex)
        {
            if (pointerIndex == 0)
            {
                if (tryMoveTo)
                {
                    tryMoveTo = false;
                    float dx = moveToX - (int)point.Position.X;
                    float dy = moveToY - (int)point.Position.Y;
                    if (/*@event.getAction() == MotionEvent.ACTION_UP &&*/ DateTime.Now.ToTimestamp() * 1000 - startTapTime < 300 && Math.Sqrt(dx * dx + dy * dy) < 10)
                    {

                        float moveToX = (this.moveToX - BaseChartView.HORIZONTAL_PADDING) / pickerWidth;
                        float w = pickerEnd - pickerStart;
                        float moveToLeft = moveToX - w / 2f;
                        float moveToRight = moveToX + w / 2f;
                        if (moveToLeft < 0f)
                        {
                            moveToLeft = 0;
                            moveToRight = w;
                        }
                        else if (moveToRight > 1f)
                        {
                            moveToLeft = 1f - w;
                            moveToRight = 1f;

                        }
                        float moveFromLeft = pickerStart;
                        float moveFromRight = pickerEnd;

                        moveToAnimator = ValueAnimator.OfFloat(0f, 1f);
                        float finalMoveToLeft = moveToLeft;
                        float finalMoveToRight = moveToRight;
                        view.OnPickerJumpTo(finalMoveToLeft, finalMoveToRight, true);
                        moveToAnimator.AddUpdateListener(new AnimatorUpdateListener(animation =>
                        {
                            float v = (float)animation.GetAnimatedValue();
                            pickerStart = moveFromLeft + (finalMoveToLeft - moveFromLeft) * v;
                            pickerEnd = moveFromRight + (finalMoveToRight - moveFromRight) * v;
                            view.OnPickerJumpTo(finalMoveToLeft, finalMoveToRight, false);
                        }));
                        moveToAnimator.setInterpolator(BaseChartView.INTERPOLATOR);
                        moveToAnimator.Start();
                    }
                    return true;
                }

                if (capturedStates[0] != null)
                {
                    capturedStates[0].uncapture();
                }

                capturedStates[0] = null;
                if (capturedStates[1] != null)
                {
                    capturedStates[0] = capturedStates[1];
                    capturedStates[1] = null;
                }
            }
            else
            {
                if (capturedStates[1] != null)
                {
                    capturedStates[1].uncapture();
                }

                capturedStates[1] = null;
            }
            return false;
        }

        public void Uncapture()
        {
            if (capturedStates[0] != null)
            {
                capturedStates[0].uncapture();
            }

            if (capturedStates[1] != null)
            {
                capturedStates[1].uncapture();
            }

            capturedStates[0] = null;
            capturedStates[1] = null;
        }

        public interface IListener
        {
            void OnPickerDataChanged();
            void OnPickerJumpTo(float start, float end, bool force);
            void Invalidate();
        }
    }
}
