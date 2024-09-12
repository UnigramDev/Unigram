using System;
using System.Runtime.InteropServices;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;

namespace Telegram.Common
{
    // Copyright (c) Microsoft Corporation. All rights reserved.
    // Licensed under the MIT License. See LICENSE in the project root for license information.

    // Source file: dxaml\xcp\dxaml\lib\AutomaticDragHelper.cpp
    // Note: this is needed because just enabling CanDrag on a Button doesn't seem to work.
    // This is supposedly the source code of what happens when you set CanDrag to true.
    public partial class AutomaticDragHelper
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXDRAG = 68;
        private const int SM_CYDRAG = 69;

        // The standard Windows mouse drag box size is defined by SM_CXDRAG and SM_CYDRAG.
        // UIElement uses the standard box size with dimensions multiplied by this constant.
        // This arrangement is in place as accidentally triggering a drag was deemed too easy while
        // selecting several items with the mouse in quick succession.
        private const double UIELEMENT_MOUSE_DRAG_THRESHOLD_MULTIPLIER = 2.0;

        private readonly UIElement m_pOwnerNoRef;
        private readonly bool m_shouldAddInputHandlers;

        private bool m_isCheckingForMouseDrag;
        private Point m_lastMouseLeftButtonDownPosition;

        private bool m_isLeftButtonPressed;

        private PointerEventHandler m_dragDropPointerPressedToken;
        private PointerEventHandler m_dragDropPointerMovedToken;
        private PointerEventHandler m_dragDropPointerReleasedToken;
        private PointerEventHandler m_dragDropPointerCaptureLostToken;

        public AutomaticDragHelper(UIElement pUIElement, bool shouldAddInputHandlers)
        {
            m_pOwnerNoRef = pUIElement;
            m_shouldAddInputHandlers = shouldAddInputHandlers;
        }

        // Begin tracking the mouse cursor in order to fire a drag start if the pointer
        // moves a certain distance away from m_lastMouseLeftButtonDownPosition.
        private void BeginCheckingForMouseDrag(Pointer pPointer)
        {
            m_isCheckingForMouseDrag = !!m_pOwnerNoRef.CapturePointer(pPointer);
        }


        // Stop tracking the mouse cursor.
        private void StopCheckingForMouseDrag(Pointer pPointer)
        {
            // Do not call ReleasePointerCapture() more times than we called CapturePointer()
            if (m_isCheckingForMouseDrag)
            {
                m_isCheckingForMouseDrag = false;

                m_pOwnerNoRef.ReleasePointerCapture(pPointer);
            }
        }

        // Return true if we're tracking the mouse and newMousePosition is outside the drag
        // rectangle centered at m_lastMouseLeftButtonDownPosition (see IsOutsideDragRectangle).
        private bool ShouldStartMouseDrag(Point newMousePosition)
        {
            return m_isCheckingForMouseDrag && IsOutsideDragRectangle(newMousePosition, m_lastMouseLeftButtonDownPosition);
        }

        // Returns true if testPoint is outside of the rectangle
        // defined by the SM_CXDRAG and SM_CYDRAG system metrics and
        // dragRectangleCenter.
        private bool IsOutsideDragRectangle(Point testPoint, Point dragRectangleCenter)
        {
            double dx = Math.Abs(testPoint.X - dragRectangleCenter.X);
            double dy = Math.Abs(testPoint.Y - dragRectangleCenter.Y);

            // TODO: GetSystemMetrics fails when compiling RELEASE
            double maxDx = 4; // GetSystemMetrics(SM_CXDRAG);
            double maxDy = 4; // GetSystemMetrics(SM_CYDRAG);

            maxDx *= UIELEMENT_MOUSE_DRAG_THRESHOLD_MULTIPLIER;
            maxDy *= UIELEMENT_MOUSE_DRAG_THRESHOLD_MULTIPLIER;

            return (dx > maxDx || dy > maxDy);
        }


        public void StartDetectingDrag()
        {
            if (m_shouldAddInputHandlers && m_dragDropPointerPressedToken == null)
            {
                m_dragDropPointerPressedToken = new PointerEventHandler(HandlePointerPressedEventArgs);
                m_pOwnerNoRef.AddHandler(UIElement.PointerPressedEvent, m_dragDropPointerPressedToken, true);
            }
        }

        public void StopDetectingDrag()
        {
            if (m_dragDropPointerPressedToken != null)
            {
                m_pOwnerNoRef.RemoveHandler(UIElement.PointerPressedEvent, m_dragDropPointerPressedToken);
                m_dragDropPointerPressedToken = null;
            }
        }

        private void RegisterDragPointerEvents()
        {
            if (m_shouldAddInputHandlers)
            {
                // Hookup pointer events so we can catch and handle it for drag and drop.
                if (m_dragDropPointerMovedToken == null)
                {
                    m_dragDropPointerMovedToken = new PointerEventHandler(HandlePointerMovedEventArgs);
                    m_pOwnerNoRef.AddHandler(UIElement.PointerMovedEvent, m_dragDropPointerMovedToken, true);
                }

                if (m_dragDropPointerReleasedToken == null)
                {
                    m_dragDropPointerReleasedToken = new PointerEventHandler(HandlePointerReleasedEventArgs);
                    m_pOwnerNoRef.AddHandler(UIElement.PointerReleasedEvent, m_dragDropPointerReleasedToken, true);
                }

                if (m_dragDropPointerCaptureLostToken == null)
                {
                    m_dragDropPointerCaptureLostToken = new PointerEventHandler(HandlePointerCaptureLostEventArgs);
                    m_pOwnerNoRef.AddHandler(UIElement.PointerCaptureLostEvent, m_dragDropPointerCaptureLostToken, true);
                }
            }
        }

        private void HandlePointerPressedEventArgs(object sender, PointerRoutedEventArgs args)
        {
            var spPointer = args.Pointer;
            var pointerDeviceType = spPointer.PointerDeviceType;

            var spPointerPoint = args.GetCurrentPoint(m_pOwnerNoRef);

            // Check if this is a mouse button down.
            if (pointerDeviceType == PointerDeviceType.Mouse || pointerDeviceType == PointerDeviceType.Pen)
            {
                // Mouse button down.
                var isLeftButtonPressed = spPointerPoint.Properties.IsLeftButtonPressed;

                // If the left mouse button was the one pressed...
                if (!m_isLeftButtonPressed && isLeftButtonPressed)
                {
                    m_isLeftButtonPressed = true;
                    // Start listening for a mouse drag gesture
                    m_lastMouseLeftButtonDownPosition = spPointerPoint.Position;
                    BeginCheckingForMouseDrag(spPointer);

                    RegisterDragPointerEvents();
                }
            }
        }

        private void HandlePointerMovedEventArgs(object sender, PointerRoutedEventArgs args)
        {
            var spPointer = args.Pointer;
            var pointerDeviceType = spPointer.PointerDeviceType;

            // Our behavior is different between mouse and touch.
            // It's up to us to detect mouse drag gestures - if we
            // detect one here, start a drag drop.
            if (pointerDeviceType == PointerDeviceType.Mouse || pointerDeviceType == PointerDeviceType.Pen)
            {
                var spPointerPoint = args.GetCurrentPoint(m_pOwnerNoRef);

                var newMousePosition = spPointerPoint.Position;
                if (ShouldStartMouseDrag(newMousePosition))
                {
                    StopCheckingForMouseDrag(spPointer);

                    _ = m_pOwnerNoRef.StartDragAsync(spPointerPoint);
                }
            }
        }


        private void HandlePointerReleasedEventArgs(object sender, PointerRoutedEventArgs args)
        {
            var spPointer = args.Pointer;
            var pointerDeviceType = spPointer.PointerDeviceType;

            // Check if this is a mouse button up
            if (pointerDeviceType == PointerDeviceType.Mouse || pointerDeviceType == PointerDeviceType.Pen)
            {
                var spPointerPoint = args.GetCurrentPoint(m_pOwnerNoRef);
                var spPointerProperties = spPointerPoint.Properties;
                var isLeftButtonPressed = spPointerProperties.IsLeftButtonPressed;

                // if the mouse left button was the one released...
                if (m_isLeftButtonPressed && !isLeftButtonPressed)
                {
                    m_isLeftButtonPressed = false;
                    UnregisterEvents();
                    // Terminate any mouse drag gesture tracking.
                    StopCheckingForMouseDrag(spPointer);
                }
            }
            else
            {
                UnregisterEvents();
            }
        }


        private void HandlePointerCaptureLostEventArgs(object sender, PointerRoutedEventArgs args)
        {
            var spPointer = args.Pointer;
            var pointerDeviceType = spPointer.PointerDeviceType;

            if (pointerDeviceType == PointerDeviceType.Mouse || pointerDeviceType == PointerDeviceType.Pen)
            {
                // We're not necessarily going to get a PointerReleased on capture lost, so reset this flag here.
                m_isLeftButtonPressed = false;
            }

            UnregisterEvents();
        }

        private void UnregisterEvents()
        {
            // Unregister events handlers
            if (m_dragDropPointerMovedToken != null)
            {
                m_pOwnerNoRef.RemoveHandler(UIElement.PointerMovedEvent, m_dragDropPointerMovedToken);
                m_dragDropPointerMovedToken = null;
            }

            if (m_dragDropPointerReleasedToken != null)
            {
                m_pOwnerNoRef.RemoveHandler(UIElement.PointerReleasedEvent, m_dragDropPointerReleasedToken);
                m_dragDropPointerReleasedToken = null;
            }

            if (m_dragDropPointerCaptureLostToken != null)
            {
                m_pOwnerNoRef.RemoveHandler(UIElement.PointerCaptureLostEvent, m_dragDropPointerCaptureLostToken);
                m_dragDropPointerCaptureLostToken = null;
            }
        }
    }
}
