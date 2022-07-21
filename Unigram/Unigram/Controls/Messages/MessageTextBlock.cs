using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public class MessageTextBlock : Control
    {
        private Vector2 _currentSize;
        private float _currentDpi;
        private bool _visible = true;

        protected CanvasImageSource _surface;
        protected CanvasBitmap _bitmap;

        private Grid _layoutRoot;
        protected Image _canvas;

        protected bool _unloaded;

        protected bool _isLoopingEnabled = true;

        private readonly object _recreateLock = new();
        private readonly object _drawFrameLock = new();

        public MessageTextBlock()
        {
            _currentDpi = DisplayInformation.GetForCurrentView().LogicalDpi;

            DefaultStyleKey = typeof(MessageTextBlock);
            SizeChanged += OnSizeChanged;
        }

        protected override void OnApplyTemplate()
        {
            var canvas = GetTemplateChild("Canvas") as Image;
            if (canvas == null)
            {
                return;
            }

            _canvas = canvas;

            _layoutRoot = GetTemplateChild("LayoutRoot") as Grid;
            _layoutRoot.Loaded += OnLoaded;
            _layoutRoot.Loading += OnLoading;
            _layoutRoot.Unloaded += OnUnloaded;

            SourceChanged();

            base.OnApplyTemplate();
        }

        public void Test(FormattedText text)
        {
            GetTextLayout(text, ActualSize.X);

            Changed();
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_prevText != null)
            {
                var layout = GetTextLayout(_prevText, availableSize.ToVector2().X);

                _layoutRoot.Measure(new Size(layout.LayoutBounds.Width, layout.LayoutBounds.Height));
                return new Size(layout.LayoutBounds.Width, layout.LayoutBounds.Height);
            }

            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _layoutRoot.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

            DrawFrame();
            return finalSize;
        }

        private CanvasTextFormat _format;
        private CanvasTextLayout _layout;

        private FormattedText _prevText;

        private Dictionary<Rect, TextEntity> _links = new();

        private void ProcessEntities(FormattedText caption, CanvasTextLayout layout)
        {
            _links.Clear();

            if (caption.Entities != null)
            {
                var device = CanvasDevice.GetSharedDevice();
                var list = new List<CanvasGeometry>();

                foreach (var entity in caption.Entities)
                {
                    if (entity.Type is TextEntityTypeBold)
                    {
                        _layout.SetFontWeight(entity.Offset, entity.Length, FontWeights.SemiBold);
                    }
                    else if (entity.Type is TextEntityTypeItalic)
                    {
                        _layout.SetFontStyle(entity.Offset, entity.Length, FontStyle.Italic);
                    }
                    else if (entity.Type is TextEntityTypeCode or TextEntityTypePre or TextEntityTypePreCode)
                    {
                        _layout.SetFontFamily(entity.Offset, entity.Length, "Consolas");
                    }
                    //else if (entity.Type is TextEntityTypeBankCardNumber or TextEntityTypeBotCommand or TextEntityTypeCashtag or TextEntityTypeEmailAddress or TextEntityTypeHashtag or TextEntityTypeMediaTimestamp or TextEntityTypeMention or TextEntityTypeMentionName or TextEntityTypePhoneNumber or TextEntityTypeTextUrl or TextEntityTypeUnderline or TextEntityTypeUrl)
                    //{

                    //}
                    else if (entity.Type is TextEntityTypeSpoiler)
                    {
#if DEBUG
                        var regions = _layout.GetCharacterRegions(entity.Offset, entity.Length).ToList();

                        foreach (var region in regions)
#else
                        foreach (var region in _layout.GetCharacterRegions(entity.Offset, entity.Length))
#endif
                        {
                            _links[region.LayoutBounds] = entity;
                            list.Add(CanvasGeometry.CreateRoundedRectangle(device, region.LayoutBounds, 4, 4));
                        }

                    }
                    else if (entity.Type is TextEntityTypeStrikethrough)
                    {
                        _layout.SetStrikethrough(entity.Offset, entity.Length, true);
                    }
                    else
                    {
                        if (entity.Type is not TextEntityTypeUnderline)
                        {
#if DEBUG
                            var regions = _layout.GetCharacterRegions(entity.Offset, entity.Length).ToList();

                            foreach (var region in regions)
#else
                            foreach (var region in _layout.GetCharacterRegions(entity.Offset, entity.Length))
#endif
                            {
                                _links[region.LayoutBounds] = entity;
                            }
                        }

                        _layout.SetUnderline(entity.Offset, entity.Length, true);
                    }
                }

                list.Add(CanvasGeometry.CreateRectangle(device, 0, 0, ActualSize.X, ActualSize.Y));

                var visual = ElementCompositionPreview.GetElementVisual(this);
                visual.Clip = Window.Current.Compositor.CreateGeometricClip(Window.Current.Compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreateGroup(device, list.ToArray(), CanvasFilledRegionDetermination.Alternate))));
            }
        }

        private CanvasTextLayout GetTextLayout(FormattedText caption, float width)
        {
            if (_prevText == caption && _layout != null)
            {
                _layout.RequestedSize = new Size(width, double.PositiveInfinity);
                ProcessEntities(caption, _layout);

                return _layout;
            }
            else if (caption == null)
            {
                _prevText = null;
                _layout = null;

                return null;
            }

            var fontSize = (float)(Theme.Current.MessageFontSize * BootStrapper.Current.UISettings.TextScaleFactor);

            _format ??= new CanvasTextFormat { FontFamily = "Assets\\Emoji\\apple.ttf#Segoe UI Emoji", FontSize = fontSize, Options = CanvasDrawTextOptions.EnableColorFont };
            _layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), caption.Text, _format, width, float.PositiveInfinity);
            _layout.Options = CanvasDrawTextOptions.EnableColorFont | CanvasDrawTextOptions.NoPixelSnap;

            ProcessEntities(caption, _layout);

            _prevText = caption;
            return _layout;
        }

        private TextEntity _lastEntity;

        private CanvasTextLayoutRegion? _selectionStart;
        private CanvasTextLayoutRegion? _selectionEnd;

        private CanvasTextLayoutRegion[] _selectionHighlight;

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            var layout = _layout;
            if (layout != null)
            {
                var point = e.GetCurrentPoint(this).Position;
                var redraw = false;

                var cursor = _selectionCursor;

                if (_selectionStart is CanvasTextLayoutRegion selectionStart)
                {
                    layout.HitTest(point.ToVector2(), out CanvasTextLayoutRegion region);
                    {
                        var start = Math.Min(selectionStart.CharacterIndex, region.CharacterIndex);
                        var end = Math.Max(selectionStart.CharacterIndex, region.CharacterIndex);

                        if (selectionStart.CharacterIndex > region.CharacterIndex)
                        {
                            end++;
                        }
                        else
                        {
                            end++;
                        }

                        _selectionHighlight = layout.GetCharacterRegions(start, end - start);
                        _selectionEnd = region;
                        redraw = true;
                    }
                }
                else
                {
                    var rect = _links.FirstOrDefault(x => x.Key.Contains(point));

                    if (_lastEntity != null && _lastEntity != rect.Value)
                    {
                        _lastEntity = null;
                        redraw = true;
                    }

                    if (rect.Value != null)
                    {
                        cursor = _handCursor;

                        if (rect.Value != _lastEntity)
                        {
                            _lastEntity = rect.Value;
                            redraw = true;
                        }
                    }
                }

                Window.Current.CoreWindow.PointerCursor = cursor;

                if (redraw)
                {
                    DrawFrame();
                }
            }

            base.OnPointerMoved(e);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            var layout = _layout;
            if (layout != null)
            {
                var point = e.GetCurrentPoint(this).Position;
                layout.HitTest(point.ToVector2(), out CanvasTextLayoutRegion region);

                _selectionStart = region;
                _selectionHighlight = null;
            }

            base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            _selectionStart = null;
            _selectionHighlight = null;

            var point = e.GetCurrentPoint(this).Position;

            var rect = _links.FirstOrDefault(x => x.Key.Contains(point));
            if (rect.Value != null)
            {
                System.Diagnostics.Debug.WriteLine("Entity clicked: {0}", rect.Value.Type);
            }

            base.OnPointerReleased(e);
        }

        private static readonly CoreCursor _defaultCursor = new CoreCursor(CoreCursorType.Arrow, 1);
        private static readonly CoreCursor _selectionCursor = new CoreCursor(CoreCursorType.IBeam, 1);
        private static readonly CoreCursor _handCursor = new CoreCursor(CoreCursorType.Hand, 1);

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = _selectionCursor;
            base.OnPointerEntered(e);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = _defaultCursor;
            base.OnPointerExited(e);
        }

        private void SourceChanged()
        {
            Test(_prevText);
        }

        private void RegisterEventHandlers()
        {
            DisplayInformation.GetForCurrentView().DpiChanged += OnDpiChanged;
            Windows.UI.Xaml.Media.CompositionTarget.SurfaceContentsLost += OnSurfaceContentsLost;
        }

        private void UnregisterEventHandlers()
        {
            DisplayInformation.GetForCurrentView().DpiChanged -= OnDpiChanged;
            Windows.UI.Xaml.Media.CompositionTarget.SurfaceContentsLost -= OnSurfaceContentsLost;
        }

        private void OnDpiChanged(DisplayInformation sender, object args)
        {
            lock (_drawFrameLock)
            {
                _currentDpi = sender.LogicalDpi;
                Changed();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            lock (_drawFrameLock)
            {
                _currentSize = e.NewSize.ToVector2();
                Changed();
            }
        }

        private void OnSurfaceContentsLost(object sender, object e)
        {
            lock (_drawFrameLock)
            {
                _surface = null;
                Changed();
            }
        }

        private void Changed(bool force = false)
        {
            if (_canvas == null)
            {
                // Load is going to invoke Changed again
                Load();
                return;
            }

            lock (_recreateLock)
            {
                var newDpi = _currentDpi;
                var newSize = _currentSize;

                bool needsCreate = (_surface == null);
                needsCreate |= (_surface?.Dpi != newDpi);
                needsCreate |= (_surface?.Size.Width < newSize.X || _surface?.Size.Height < newSize.Y);
                needsCreate |= force;

                if (needsCreate && newSize.X > 0 && newSize.Y > 0)
                {
                    try
                    {
                        _surface = new CanvasImageSource(CanvasDevice.GetSharedDevice(), 500, newSize.Y, newDpi, CanvasAlphaMode.Premultiplied);
                        _canvas.Source = _surface;

                        DrawFrame();
                    }
                    catch
                    {
                        Unload();
                    }
                }
            }
        }

        protected bool Load()
        {
            if (_unloaded && _layoutRoot != null && _layoutRoot.IsLoaded)
            {
                lock (_recreateLock)
                {
                    while (_layoutRoot.Children.Count > 0)
                    {
                        _layoutRoot.Children.Remove(_layoutRoot.Children[0]);
                    }

                    _canvas = new Image
                    {
                        Stretch = Stretch.UniformToFill,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };

                    AutomationProperties.SetAccessibilityView(_canvas, AccessibilityView.Raw);

                    _layoutRoot.Children.Add(_canvas);

                    _unloaded = false;
                    SourceChanged();
                }

                return true;
            }

            return false;
        }

        private void OnLoading(FrameworkElement sender, object args)
        {
            Load();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Load();
            RegisterEventHandlers();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_layoutRoot != null && _layoutRoot.IsLoaded)
            {
                return;
            }

            Unload();
            UnregisterEventHandlers();
        }

        public void Unload()
        {
            _unloaded = true;

            lock (_recreateLock)
            {
                //_canvas.Source = new BitmapImage();
                _layoutRoot.Children.Remove(_canvas);
                _canvas = null;

                _surface?.Device.Trim();
                _surface = null;
            }
        }

        public void DrawFrame()
        {
            if (_surface == null)
            {
                return;
            }

            try
            {
                using (var session = _surface.CreateDrawingSession(Colors.Transparent))
                {
                    if (_unloaded)
                    {
                        return;
                    }

                    DrawFrame(_surface, session);
                }
            }
            catch (Exception ex)
            {
                if (_surface != null && _surface.Device.IsDeviceLost(ex.HResult))
                {
                    Changed(true);
                }
                else
                {
                    Unload();
                }
            }
        }

        private void DrawFrame(CanvasImageSource sender, CanvasDrawingSession args)
        {
            var layout = _layout;
            if (layout == null)
            {
                return;
            }

            layout.SetColor(0, int.MaxValue, Colors.Red);

            foreach (var link in _links.Values)
            {
                if (link == _lastEntity)
                {
                    layout.SetColor(link.Offset, link.Length, Colors.Green);
                }
                else
                {
                    layout.SetColor(link.Offset, link.Length, Colors.Blue);
                }
            }

            var hightlight = _selectionHighlight;
            if (hightlight != null)
            {
                foreach (var rect in hightlight)
                {
                    args.FillRectangle(rect.LayoutBounds, Colors.Blue);
                    layout.SetColor(rect.CharacterIndex, rect.CharacterCount, Colors.White);
                }
            }

            args.DrawTextLayout(layout, 0, 0, Colors.Red);
        }
    }
}
