using System;
using System.Threading.Tasks;
using Telegram;

public static class CanvasDrawingSession_stub
{
    public static Microsoft.Graphics.Canvas.CanvasActiveLayer CreateLayer_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, float opacity, Windows.Foundation.Rect clipRectangle)
    {
        try
        {
            return sender.CreateLayer(opacity, clipRectangle);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DrawImage_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, Microsoft.Graphics.Canvas.ICanvasImage image, float x, float y)
    {
        try
        {
            sender.DrawImage(image, x, y);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void FillRectangle_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, Windows.Foundation.Rect rect, Windows.UI.Color color)
    {
        try
        {
            sender.FillRectangle(rect, color);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void FillGeometry_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, Microsoft.Graphics.Canvas.Geometry.CanvasGeometry geometry, Windows.UI.Color color)
    {
        try
        {
            sender.FillGeometry(geometry, color);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void FillCircle_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, float x, float y, float radius, Windows.UI.Color color)
    {
        try
        {
            sender.FillCircle(x, y, radius, color);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DrawText_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, string text, float x, float y, Windows.UI.Color color, Microsoft.Graphics.Canvas.Text.CanvasTextFormat format)
    {
        try
        {
            sender.DrawText(text, x, y, color, format);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DrawLine_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, float x0, float y0, float x1, float y1, Windows.UI.Color color, float strokeWidth, Microsoft.Graphics.Canvas.Geometry.CanvasStrokeStyle strokeStyle)
    {
        try
        {
            sender.DrawLine(x0, y0, x1, y1, color, strokeWidth, strokeStyle);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DrawGeometry_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, Microsoft.Graphics.Canvas.Geometry.CanvasGeometry geometry, Windows.UI.Color color, float strokeWidth, Microsoft.Graphics.Canvas.Geometry.CanvasStrokeStyle strokeStyle)
    {
        try
        {
            sender.DrawGeometry(geometry, color, strokeWidth, strokeStyle);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DrawLine_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, float x0, float y0, float x1, float y1, Windows.UI.Color color, float strokeWidth)
    {
        try
        {
            sender.DrawLine(x0, y0, x1, y1, color, strokeWidth);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DrawText_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, string text, float x, float y, Windows.UI.Color color)
    {
        try
        {
            sender.DrawText(text, x, y, color);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.Graphics.Canvas.CanvasActiveLayer CreateLayer_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, float opacity, Microsoft.Graphics.Canvas.Geometry.CanvasGeometry clipGeometry)
    {
        try
        {
            return sender.CreateLayer(opacity, clipGeometry);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DrawImage_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, Microsoft.Graphics.Canvas.ICanvasImage image)
    {
        try
        {
            sender.DrawImage(image);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void FillRoundedRectangle_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, Windows.Foundation.Rect rect, float radiusX, float radiusY, Windows.UI.Color color)
    {
        try
        {
            sender.FillRoundedRectangle(rect, radiusX, radiusY, color);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DrawImage_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, Microsoft.Graphics.Canvas.CanvasBitmap bitmap, Windows.Foundation.Rect destinationRectangle)
    {
        try
        {
            sender.DrawImage(bitmap, destinationRectangle);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Clear_stub(this Microsoft.Graphics.Canvas.CanvasDrawingSession sender, Windows.UI.Color color)
    {
        try
        {
            sender.Clear(color);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasActiveLayer_stub
{
    public static void Dispose_stub(this Microsoft.Graphics.Canvas.CanvasActiveLayer sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasRenderTarget_stub
{
    public static Microsoft.Graphics.Canvas.CanvasDrawingSession CreateDrawingSession_stub(this Microsoft.Graphics.Canvas.CanvasRenderTarget sender)
    {
        try
        {
            return sender.CreateDrawingSession();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasDevice_stub
{
    public static Microsoft.Graphics.Canvas.CanvasDevice GetSharedDevice_stub()
    {
        try
        {
            return Microsoft.Graphics.Canvas.CanvasDevice.GetSharedDevice();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasBitmap_stub
{
    public static Microsoft.Graphics.Canvas.CanvasBitmap CreateFromSoftwareBitmap_stub(Microsoft.Graphics.Canvas.ICanvasResourceCreator resourceCreator, Windows.Graphics.Imaging.SoftwareBitmap sourceBitmap)
    {
        try
        {
            return Microsoft.Graphics.Canvas.CanvasBitmap.CreateFromSoftwareBitmap(resourceCreator, sourceBitmap);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Microsoft.Graphics.Canvas.CanvasBitmap sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task SaveAsync_stub(this Microsoft.Graphics.Canvas.CanvasBitmap sender, Windows.Storage.Streams.IRandomAccessStream stream, Microsoft.Graphics.Canvas.CanvasBitmapFileFormat fileFormat)
    {
        try
        {
            await sender.SaveAsync(stream, fileFormat);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Microsoft.Graphics.Canvas.CanvasBitmap> LoadAsync_stub(Microsoft.Graphics.Canvas.ICanvasResourceCreator resourceCreator, string fileName)
    {
        try
        {
            return await Microsoft.Graphics.Canvas.CanvasBitmap.LoadAsync(resourceCreator, fileName);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.Graphics.Canvas.CanvasBitmap CreateFromBytes_stub(Microsoft.Graphics.Canvas.ICanvasResourceCreator resourceCreator, byte[] bytes, int widthInPixels, int heightInPixels, Windows.Graphics.DirectX.DirectXPixelFormat format)
    {
        try
        {
            return Microsoft.Graphics.Canvas.CanvasBitmap.CreateFromBytes(resourceCreator, bytes, widthInPixels, heightInPixels, format);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasPathBuilder_stub
{
    public static void SetFilledRegionDetermination_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, Microsoft.Graphics.Canvas.Geometry.CanvasFilledRegionDetermination filledRegionDetermination)
    {
        try
        {
            sender.SetFilledRegionDetermination(filledRegionDetermination);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void BeginFigure_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, System.Numerics.Vector2 startPoint)
    {
        try
        {
            sender.BeginFigure(startPoint);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddLine_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, System.Numerics.Vector2 endPoint)
    {
        try
        {
            sender.AddLine(endPoint);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddCubicBezier_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, System.Numerics.Vector2 controlPoint1, System.Numerics.Vector2 controlPoint2, System.Numerics.Vector2 endPoint)
    {
        try
        {
            sender.AddCubicBezier(controlPoint1, controlPoint2, endPoint);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void EndFigure_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, Microsoft.Graphics.Canvas.Geometry.CanvasFigureLoop figureLoop)
    {
        try
        {
            sender.EndFigure(figureLoop);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddQuadraticBezier_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, System.Numerics.Vector2 controlPoint, System.Numerics.Vector2 endPoint)
    {
        try
        {
            sender.AddQuadraticBezier(controlPoint, endPoint);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void BeginFigure_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, float startX, float startY)
    {
        try
        {
            sender.BeginFigure(startX, startY);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddLine_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, float x, float y)
    {
        try
        {
            sender.AddLine(x, y);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddArc_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, System.Numerics.Vector2 endPoint, float radiusX, float radiusY, float rotationAngle, Microsoft.Graphics.Canvas.Geometry.CanvasSweepDirection sweepDirection, Microsoft.Graphics.Canvas.Geometry.CanvasArcSize arcSize)
    {
        try
        {
            sender.AddArc(endPoint, radiusX, radiusY, rotationAngle, sweepDirection, arcSize);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddArc_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, System.Numerics.Vector2 centerPoint, float radiusX, float radiusY, float startAngle, float sweepAngle)
    {
        try
        {
            sender.AddArc(centerPoint, radiusX, radiusY, startAngle, sweepAngle);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddGeometry_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder sender, Microsoft.Graphics.Canvas.Geometry.CanvasGeometry geometry)
    {
        try
        {
            sender.AddGeometry(geometry);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasGeometry_stub
{
    public static Microsoft.Graphics.Canvas.Geometry.CanvasGeometry CreatePath_stub(Microsoft.Graphics.Canvas.Geometry.CanvasPathBuilder pathBuilder)
    {
        try
        {
            return Microsoft.Graphics.Canvas.Geometry.CanvasGeometry.CreatePath(pathBuilder);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.Graphics.Canvas.Geometry.CanvasGeometry CreateGroup_stub(Microsoft.Graphics.Canvas.ICanvasResourceCreator resourceCreator, Microsoft.Graphics.Canvas.Geometry.CanvasGeometry[] geometries, Microsoft.Graphics.Canvas.Geometry.CanvasFilledRegionDetermination filledRegionDetermination)
    {
        try
        {
            return Microsoft.Graphics.Canvas.Geometry.CanvasGeometry.CreateGroup(resourceCreator, geometries, filledRegionDetermination);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.Graphics.Canvas.Geometry.CanvasGeometry CreateCircle_stub(Microsoft.Graphics.Canvas.ICanvasResourceCreator resourceCreator, float x, float y, float radius)
    {
        try
        {
            return Microsoft.Graphics.Canvas.Geometry.CanvasGeometry.CreateCircle(resourceCreator, x, y, radius);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.Graphics.Canvas.Geometry.CanvasGeometry CreateRoundedRectangle_stub(Microsoft.Graphics.Canvas.ICanvasResourceCreator resourceCreator, Windows.Foundation.Rect rect, float radiusX, float radiusY)
    {
        try
        {
            return Microsoft.Graphics.Canvas.Geometry.CanvasGeometry.CreateRoundedRectangle(resourceCreator, rect, radiusX, radiusY);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.Graphics.Canvas.Geometry.CanvasGeometry CreateRectangle_stub(Microsoft.Graphics.Canvas.ICanvasResourceCreator resourceCreator, float x, float y, float w, float h)
    {
        try
        {
            return Microsoft.Graphics.Canvas.Geometry.CanvasGeometry.CreateRectangle(resourceCreator, x, y, w, h);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.Graphics.Canvas.Geometry.CanvasGeometry CreateEllipse_stub(Microsoft.Graphics.Canvas.ICanvasResourceCreator resourceCreator, float x, float y, float radiusX, float radiusY)
    {
        try
        {
            return Microsoft.Graphics.Canvas.Geometry.CanvasGeometry.CreateEllipse(resourceCreator, x, y, radiusX, radiusY);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.Graphics.Canvas.Geometry.CanvasGeometry CreateRoundedRectangle_stub(Microsoft.Graphics.Canvas.ICanvasResourceCreator resourceCreator, float x, float y, float w, float h, float radiusX, float radiusY)
    {
        try
        {
            return Microsoft.Graphics.Canvas.Geometry.CanvasGeometry.CreateRoundedRectangle(resourceCreator, x, y, w, h, radiusX, radiusY);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.Graphics.Canvas.Geometry.CanvasGeometry CombineWith_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasGeometry sender, Microsoft.Graphics.Canvas.Geometry.CanvasGeometry otherGeometry, System.Numerics.Matrix3x2 otherGeometryTransform, Microsoft.Graphics.Canvas.Geometry.CanvasGeometryCombine combine)
    {
        try
        {
            return sender.CombineWith(otherGeometry, otherGeometryTransform, combine);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasStrokeStyle_stub
{
    public static void Dispose_stub(this Microsoft.Graphics.Canvas.Geometry.CanvasStrokeStyle sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasTextFormat_stub
{
    public static void Dispose_stub(this Microsoft.Graphics.Canvas.Text.CanvasTextFormat sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasTextLayout_stub
{
    public static void Dispose_stub(this Microsoft.Graphics.Canvas.Text.CanvasTextLayout sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.Graphics.Canvas.Text.CanvasTextLayoutRegion[] GetCharacterRegions_stub(this Microsoft.Graphics.Canvas.Text.CanvasTextLayout sender, int characterIndex, int characterCount)
    {
        try
        {
            return sender.GetCharacterRegions(characterIndex, characterCount);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasControl_stub
{
    public static void Invalidate_stub(this Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender)
    {
        try
        {
            sender.Invalidate();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RemoveFromVisualTree_stub(this Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender)
    {
        try
        {
            sender.RemoveFromVisualTree();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasAnimatedControl_stub
{
    public static void RemoveFromVisualTree_stub(this Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedControl sender)
    {
        try
        {
            sender.RemoveFromVisualTree();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class IAnimatedVisualSource2_stub
{
    public static void SetColorProperty_stub(this Microsoft.UI.Xaml.Controls.IAnimatedVisualSource2 sender, string propertyName, Windows.UI.Color value)
    {
        try
        {
            sender.SetColorProperty(propertyName, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class IAnimatedVisualSource_stub
{
    public static Microsoft.UI.Xaml.Controls.IAnimatedVisual TryCreateAnimatedVisual_stub(this Microsoft.UI.Xaml.Controls.IAnimatedVisualSource sender, Microsoft.UI.Composition.Compositor compositor, out object diagnostics)
    {
        try
        {
            return sender.TryCreateAnimatedVisual(compositor, out diagnostics);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class WebView2_stub
{
    public static async Task EnsureCoreWebView2Async_stub(this Microsoft.UI.Xaml.Controls.WebView2 sender)
    {
        try
        {
            await sender.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Reload_stub(this Microsoft.UI.Xaml.Controls.WebView2 sender)
    {
        try
        {
            sender.Reload();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Close_stub(this Microsoft.UI.Xaml.Controls.WebView2 sender)
    {
        try
        {
            sender.Close();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ItemsSourceView_stub
{
    public static object GetAt_stub(this Microsoft.UI.Xaml.Controls.ItemsSourceView sender, int index)
    {
        try
        {
            return sender.GetAt(index);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CoreWebView2Environment_stub
{
    public static string GetAvailableBrowserVersionString_stub()
    {
        try
        {
            return Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CoreWebView2_stub
{
    public static async Task<string> AddScriptToExecuteOnDocumentCreatedAsync_stub(this Microsoft.Web.WebView2.Core.CoreWebView2 sender, string javaScript)
    {
        try
        {
            return await sender.AddScriptToExecuteOnDocumentCreatedAsync(javaScript);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Navigate_stub(this Microsoft.Web.WebView2.Core.CoreWebView2 sender, string uri)
    {
        try
        {
            sender.Navigate(uri);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void NavigateToString_stub(this Microsoft.Web.WebView2.Core.CoreWebView2 sender, string htmlContent)
    {
        try
        {
            sender.NavigateToString(htmlContent);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<string> ExecuteScriptAsync_stub(this Microsoft.Web.WebView2.Core.CoreWebView2 sender, string javaScript)
    {
        try
        {
            return await sender.ExecuteScriptAsync(javaScript);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CoreWebView2WebMessageReceivedEventArgs_stub
{
    public static string TryGetWebMessageAsString_stub(this Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs sender)
    {
        try
        {
            return sender.TryGetWebMessageAsString();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class LottieAnimation_stub
{
    public static void Cache_stub(this RLottie.LottieAnimation sender)
    {
        try
        {
            sender.Cache();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RenderSync_stub(this RLottie.LottieAnimation sender, Windows.Storage.Streams.IBuffer bitmap, int frame)
    {
        try
        {
            sender.RenderSync(bitmap, frame);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static RLottie.LottieAnimation LoadFromFile_stub(string filePath, int pixelWidth, int pixelHeight, bool precache, System.Collections.Generic.IReadOnlyDictionary<int, int> colorReplacement, RLottie.FitzModifier modifier)
    {
        try
        {
            return RLottie.LottieAnimation.LoadFromFile(filePath, pixelWidth, pixelHeight, precache, colorReplacement, modifier);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this RLottie.LottieAnimation sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static RLottie.LottieAnimation LoadFromFile_stub(string filePath, int pixelWidth, int pixelHeight, bool precache, System.Collections.Generic.IReadOnlyDictionary<int, int> colorReplacement)
    {
        try
        {
            return RLottie.LottieAnimation.LoadFromFile(filePath, pixelWidth, pixelHeight, precache, colorReplacement);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RenderSync_stub(this RLottie.LottieAnimation sender, Microsoft.Graphics.Canvas.CanvasBitmap bitmap, int frame)
    {
        try
        {
            sender.RenderSync(bitmap, frame);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static RLottie.LottieAnimation LoadFromData_stub(string jsonData, int pixelWidth, int pixelHeight, string cacheKey, bool precache, System.Collections.Generic.IReadOnlyDictionary<int, int> colorReplacement)
    {
        try
        {
            return RLottie.LottieAnimation.LoadFromData(jsonData, pixelWidth, pixelHeight, cacheKey, precache, colorReplacement);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RenderSync_stub(this RLottie.LottieAnimation sender, string filePath, int frame)
    {
        try
        {
            sender.RenderSync(filePath, frame);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class PlaceholderImageHelper_stub
{
    public static void WriteBytes_stub(System.Collections.Generic.IList<byte> hash, Windows.Storage.Streams.IRandomAccessStream randomAccessStream)
    {
        try
        {
            Telegram.Native.PlaceholderImageHelper.WriteBytes(hash, randomAccessStream);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IList<Windows.Foundation.Rect> LineMetrics_stub(this Telegram.Native.PlaceholderImageHelper sender, string text, System.Collections.Generic.IList<Telegram.Td.Api.TextEntity> entities, double fontSize, double width, bool rtl)
    {
        try
        {
            return sender.LineMetrics(text, entities, fontSize, width, rtl);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.IBuffer DrawWebP_stub(string fileName, int maxWidth, out int pixelWidth, out int pixelHeight)
    {
        try
        {
            return Telegram.Native.PlaceholderImageHelper.DrawWebP(fileName, maxWidth, out pixelWidth, out pixelHeight);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Encode_stub(this Telegram.Native.PlaceholderImageHelper sender, Windows.Storage.Streams.IBuffer source, Windows.Storage.Streams.IRandomAccessStream destination, int width, int height)
    {
        try
        {
            sender.Encode(source, destination, width, height);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task DrawSvgAsync_stub(this Telegram.Native.PlaceholderImageHelper sender, string path, Windows.UI.Color foreground, Windows.Storage.Streams.IRandomAccessStream randomAccessStream, double dpi)
    {
        try
        {
            await sender.DrawSvgAsync(path, foreground, randomAccessStream, dpi);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DrawThumbnailPlaceholder_stub(this Telegram.Native.PlaceholderImageHelper sender, string fileName, float blurAmount, Windows.Storage.Streams.IRandomAccessStream randomAccessStream)
    {
        try
        {
            sender.DrawThumbnailPlaceholder(fileName, blurAmount, randomAccessStream);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DrawThumbnailPlaceholder_stub(this Telegram.Native.PlaceholderImageHelper sender, System.Collections.Generic.IList<byte> bytes, float blurAmount, Windows.Storage.Streams.IRandomAccessStream randomAccessStream)
    {
        try
        {
            sender.DrawThumbnailPlaceholder(bytes, blurAmount, randomAccessStream);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IList<Windows.Foundation.Rect> RangeMetrics_stub(this Telegram.Native.PlaceholderImageHelper sender, string text, int offset, int length, System.Collections.Generic.IList<Telegram.Td.Api.TextEntity> entities, double fontSize, double width, bool rtl)
    {
        try
        {
            return sender.RangeMetrics(text, offset, length, entities, fontSize, width, rtl);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Numerics.Vector2 ContentEnd_stub(this Telegram.Native.PlaceholderImageHelper sender, string text, System.Collections.Generic.IList<Telegram.Td.Api.TextEntity> entities, double fontSize, double width)
    {
        try
        {
            return sender.ContentEnd(text, entities, fontSize, width);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CachedVideoAnimation_stub
{
    public static void Cache_stub(this Telegram.Native.CachedVideoAnimation sender)
    {
        try
        {
            sender.Cache();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RenderSync_stub(this Telegram.Native.CachedVideoAnimation sender, Windows.Storage.Streams.IBuffer bitmap, out int seconds, out bool completed)
    {
        try
        {
            sender.RenderSync(bitmap, out seconds, out completed);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Telegram.Native.CachedVideoAnimation LoadFromFile_stub(Telegram.Native.IVideoAnimationSource file, int width, int height, bool precache)
    {
        try
        {
            return Telegram.Native.CachedVideoAnimation.LoadFromFile(file, width, height, precache);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Telegram.Native.CachedVideoAnimation sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class BufferSurface_stub
{
    public static void Copy_stub(Windows.Storage.Streams.IBuffer source, Windows.Storage.Streams.IBuffer destination)
    {
        try
        {
            Telegram.Native.BufferSurface.Copy(source, destination);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.IBuffer Create_stub(byte[] data)
    {
        try
        {
            return Telegram.Native.BufferSurface.Create(data);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.IBuffer Create_stub(uint size)
    {
        try
        {
            return Telegram.Native.BufferSurface.Create(size);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class LanguageIdentification_stub
{
    public static string IdentifyLanguage_stub(string text)
    {
        try
        {
            return Telegram.Native.LanguageIdentification.IdentifyLanguage(text);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class NativeUtils_stub
{
    public static string GetKeyboardCulture_stub()
    {
        try
        {
            return Telegram.Native.NativeUtils.GetKeyboardCulture();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool IsMediaSupported_stub()
    {
        try
        {
            return Telegram.Native.NativeUtils.IsMediaSupported();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetCurrentCulture_stub()
    {
        try
        {
            return Telegram.Native.NativeUtils.GetCurrentCulture();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Telegram.Native.TextDirectionality GetDirectionality_stub(string value)
    {
        try
        {
            return Telegram.Native.NativeUtils.GetDirectionality(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int GetLastInputTime_stub()
    {
        try
        {
            return Telegram.Native.NativeUtils.GetLastInputTime();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool FileExists_stub(string path)
    {
        try
        {
            return Telegram.Native.NativeUtils.FileExists(path);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetFatalErrorCallback_stub(Telegram.Native.FatalErrorCallback action)
    {
        try
        {
            Telegram.Native.NativeUtils.SetFatalErrorCallback(action);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Telegram.Native.FatalError GetFatalError_stub(bool onlyNative)
    {
        try
        {
            return Telegram.Native.NativeUtils.GetFatalError(onlyNative);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void OverrideScaleForCurrentView_stub(int value)
    {
        try
        {
            Telegram.Native.NativeUtils.OverrideScaleForCurrentView(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int GetScaleForCurrentView_stub()
    {
        try
        {
            return Telegram.Native.NativeUtils.GetScaleForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool IsFileReadable_stub(string path, out long fileSize, out long fileTime)
    {
        try
        {
            return Telegram.Native.NativeUtils.IsFileReadable(path, out fileSize, out fileTime);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string FormatTime_stub(int value)
    {
        try
        {
            return Telegram.Native.NativeUtils.FormatTime(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VideoAnimation_stub
{
    public static Telegram.Native.VideoAnimation LoadFromFile_stub(Telegram.Native.IVideoAnimationSource file, bool preview, bool limitFps, bool probe)
    {
        try
        {
            return Telegram.Native.VideoAnimation.LoadFromFile(file, preview, limitFps, probe);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int RenderSync_stub(this Telegram.Native.VideoAnimation sender, Windows.Storage.Streams.IBuffer bitmap, int width, int height, bool preview, out int seconds)
    {
        try
        {
            return sender.RenderSync(bitmap, width, height, preview, out seconds);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SeekToMilliseconds_stub(this Telegram.Native.VideoAnimation sender, long ms, bool precise)
    {
        try
        {
            sender.SeekToMilliseconds(ms, precise);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FileStreamFromApp_stub
{
    public static void Close_stub(this Telegram.Native.FileStreamFromApp sender)
    {
        try
        {
            sender.Close();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool Seek_stub(this Telegram.Native.FileStreamFromApp sender, long offset)
    {
        try
        {
            return sender.Seek(offset);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int Read_stub(this Telegram.Native.FileStreamFromApp sender, long pointer, uint length)
    {
        try
        {
            return sender.Read(pointer, length);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class LokiRng_stub
{
    public static float Next_stub(this Telegram.Native.LokiRng sender)
    {
        try
        {
            return sender.Next();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class QrBuffer_stub
{
    public static Telegram.Native.QrBuffer FromString_stub(string text)
    {
        try
        {
            return Telegram.Native.QrBuffer.FromString(text);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VoipVideoCapture_stub
{
    public static void SwitchToDevice_stub(this Telegram.Native.Calls.VoipVideoCapture sender, string deviceId)
    {
        try
        {
            sender.SwitchToDevice(deviceId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Telegram.Native.Calls.VoipVideoCapture sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VoipGroupManager_stub
{
    public static void SetAudioInputDevice_stub(this Telegram.Native.Calls.VoipGroupManager sender, string id)
    {
        try
        {
            sender.SetAudioInputDevice(id);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetAudioOutputDevice_stub(this Telegram.Native.Calls.VoipGroupManager sender, string id)
    {
        try
        {
            sender.SetAudioOutputDevice(id);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetConnectionMode_stub(this Telegram.Native.Calls.VoipGroupManager sender, Telegram.Native.Calls.VoipGroupConnectionMode connectionMode, bool keepBroadcastIfWasEnabled, bool isUnifiedBroadcast)
    {
        try
        {
            sender.SetConnectionMode(connectionMode, keepBroadcastIfWasEnabled, isUnifiedBroadcast);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void EmitJoinPayload_stub(this Telegram.Native.Calls.VoipGroupManager sender, Telegram.Native.Calls.EmitJsonPayloadDelegate completion)
    {
        try
        {
            sender.EmitJoinPayload(completion);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetJoinResponsePayload_stub(this Telegram.Native.Calls.VoipGroupManager sender, string payload)
    {
        try
        {
            sender.SetJoinResponsePayload(payload);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetVideoCapture_stub(this Telegram.Native.Calls.VoipGroupManager sender, Telegram.Native.Calls.VoipCaptureBase videoCapture)
    {
        try
        {
            sender.SetVideoCapture(videoCapture);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Telegram.Native.Calls.VoipGroupManager sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetVolume_stub(this Telegram.Native.Calls.VoipGroupManager sender, int ssrc, double volume)
    {
        try
        {
            sender.SetVolume(ssrc, volume);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetRequestedVideoChannels_stub(this Telegram.Native.Calls.VoipGroupManager sender, System.Collections.Generic.IList<Telegram.Native.Calls.VoipVideoChannelInfo> descriptions)
    {
        try
        {
            sender.SetRequestedVideoChannels(descriptions);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Telegram.Native.Calls.VoipVideoRendererToken AddIncomingVideoOutput_stub(this Telegram.Native.Calls.VoipGroupManager sender, int audioSource, Telegram.Td.Api.GroupCallParticipantVideoInfo videoInfo, Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl canvas)
    {
        try
        {
            return sender.AddIncomingVideoOutput(audioSource, videoInfo, canvas);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddUnifiedVideoOutput_stub(this Telegram.Native.Calls.VoipGroupManager sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl canvas)
    {
        try
        {
            sender.AddUnifiedVideoOutput(canvas);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VoipCaptureBase_stub
{
    public static Telegram.Native.Calls.VoipVideoRendererToken SetOutput_stub(this Telegram.Native.Calls.VoipCaptureBase sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl canvas)
    {
        try
        {
            return sender.SetOutput(canvas);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Telegram.Native.Calls.VoipCaptureBase sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Telegram.Native.Calls.VoipVideoRendererToken SetOutput_stub(this Telegram.Native.Calls.VoipCaptureBase sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl canvas, bool enableBlur)
    {
        try
        {
            return sender.SetOutput(canvas, enableBlur);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VoipScreenCapture_stub
{
    public static bool IsSupported_stub()
    {
        try
        {
            return Telegram.Native.Calls.VoipScreenCapture.IsSupported();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Telegram.Native.Calls.VoipScreenCapture sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VoipManager_stub
{
    public static void SetAudioInputDevice_stub(this Telegram.Native.Calls.VoipManager sender, string id)
    {
        try
        {
            sender.SetAudioInputDevice(id);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetAudioOutputDevice_stub(this Telegram.Native.Calls.VoipManager sender, string id)
    {
        try
        {
            sender.SetAudioOutputDevice(id);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetVideoCapture_stub(this Telegram.Native.Calls.VoipManager sender, Telegram.Native.Calls.VoipCaptureBase videoCapture)
    {
        try
        {
            sender.SetVideoCapture(videoCapture);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ReceiveSignalingData_stub(this Telegram.Native.Calls.VoipManager sender, System.Collections.Generic.IList<byte> data)
    {
        try
        {
            sender.ReceiveSignalingData(data);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Start_stub(this Telegram.Native.Calls.VoipManager sender)
    {
        try
        {
            sender.Start();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Telegram.Native.Calls.VoipVideoRendererToken SetIncomingVideoOutput_stub(this Telegram.Native.Calls.VoipManager sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl canvas)
    {
        try
        {
            return sender.SetIncomingVideoOutput(canvas);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Telegram.Native.Calls.VoipManager sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static long GetPreferredRelayId_stub(this Telegram.Native.Calls.VoipManager sender)
    {
        try
        {
            return sender.GetPreferredRelayId();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetDebugInfo_stub(this Telegram.Native.Calls.VoipManager sender)
    {
        try
        {
            return sender.GetDebugInfo();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VoipVideoRendererToken_stub
{
    public static void Stop_stub(this Telegram.Native.Calls.VoipVideoRendererToken sender)
    {
        try
        {
            sender.Stop();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool IsMatch_stub(this Telegram.Native.Calls.VoipVideoRendererToken sender, string endpointId, Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl canvasControl)
    {
        try
        {
            return sender.IsMatch(endpointId, canvasControl);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CompositionDevice_stub
{
    public static Telegram.Native.Composition.DirectRectangleClip CreateRectangleClip_stub(Microsoft.UI.Xaml.UIElement element)
    {
        try
        {
            return Telegram.Native.Composition.CompositionDevice.CreateRectangleClip(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DirectRectangleClip_stub
{
    public static void Set_stub(this Telegram.Native.Composition.DirectRectangleClip sender, float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        try
        {
            sender.Set(topLeft, topRight, bottomRight, bottomLeft);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AnimateBottomLeft_stub(this Telegram.Native.Composition.DirectRectangleClip sender, Microsoft.UI.Composition.Compositor compositor, float from, float to, double duration)
    {
        try
        {
            sender.AnimateBottomLeft(compositor, from, to, duration);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AnimateBottomRight_stub(this Telegram.Native.Composition.DirectRectangleClip sender, Microsoft.UI.Composition.Compositor compositor, float from, float to, double duration)
    {
        try
        {
            sender.AnimateBottomRight(compositor, from, to, duration);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetInset_stub(this Telegram.Native.Composition.DirectRectangleClip sender, float left, float top, float right, float bottom)
    {
        try
        {
            sender.SetInset(left, top, right, bottom);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AnimateBottom_stub(this Telegram.Native.Composition.DirectRectangleClip sender, Microsoft.UI.Composition.Compositor compositor, float from, float to, double duration)
    {
        try
        {
            sender.AnimateBottom(compositor, from, to, duration);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AnimateTop_stub(this Telegram.Native.Composition.DirectRectangleClip sender, Microsoft.UI.Composition.Compositor compositor, float from, float to, double duration)
    {
        try
        {
            sender.AnimateTop(compositor, from, to, duration);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class SyntaxToken_stub
{
    public static async Task<Telegram.Native.Highlight.SyntaxToken> TokenizeAsync_stub(string language, string coddiri)
    {
        try
        {
            return await Telegram.Native.Highlight.SyntaxToken.TokenizeAsync(language, coddiri);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class OpusOutput_stub
{
    public static void Transcode_stub(this Telegram.Native.Opus.OpusOutput sender, string fileName)
    {
        try
        {
            sender.Transcode(fileName);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Client_stub
{
    public static void Send_stub(this Telegram.Td.Client sender, Telegram.Td.Api.Function function, Telegram.Td.ClientResultHandler handler)
    {
        try
        {
            sender.Send(function, handler);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Telegram.Td.Api.BaseObject Execute_stub(Telegram.Td.Api.Function function)
    {
        try
        {
            return Telegram.Td.Client.Execute(function);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetLogMessageCallback_stub(int max_verbosity_level, Telegram.Td.LogMessageCallback callback)
    {
        try
        {
            Telegram.Td.Client.SetLogMessageCallback(max_verbosity_level, callback);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Telegram.Td.Client Create_stub(Telegram.Td.ClientResultHandler updateHandler)
    {
        try
        {
            return Telegram.Td.Client.Create(updateHandler);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FormattedText_stub
{
    public static string ToString_stub(this Telegram.Td.Api.FormattedText sender)
    {
        try
        {
            return sender.ToString();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Message_stub
{
    public static string ToString_stub(this Telegram.Td.Api.Message sender)
    {
        try
        {
            return sender.ToString();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FullTrustProcessLauncher_stub
{
    public static async Task LaunchFullTrustProcessForCurrentAppAsync_stub()
    {
        try
        {
            await Windows.ApplicationModel.FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class StartupTask_stub
{
    public static async Task<Windows.ApplicationModel.StartupTaskState> RequestEnableAsync_stub(this Windows.ApplicationModel.StartupTask sender)
    {
        try
        {
            return await sender.RequestEnableAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Disable_stub(this Windows.ApplicationModel.StartupTask sender)
    {
        try
        {
            sender.Disable();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.StartupTask> GetAsync_stub(string taskId)
    {
        try
        {
            return await Windows.ApplicationModel.StartupTask.GetAsync(taskId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class SuspendingOperation_stub
{
    public static Windows.ApplicationModel.SuspendingDeferral GetDeferral_stub(this Windows.ApplicationModel.SuspendingOperation sender)
    {
        try
        {
            return sender.GetDeferral();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class SuspendingDeferral_stub
{
    public static void Complete_stub(this Windows.ApplicationModel.SuspendingDeferral sender)
    {
        try
        {
            sender.Complete();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class AppServiceConnection_stub
{
    public static async Task<Windows.ApplicationModel.AppService.AppServiceResponse> SendMessageAsync_stub(this Windows.ApplicationModel.AppService.AppServiceConnection sender, Windows.Foundation.Collections.ValueSet message)
    {
        try
        {
            return await sender.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class IBackgroundTaskInstance_stub
{
    public static Windows.ApplicationModel.Background.BackgroundTaskDeferral GetDeferral_stub(this Windows.ApplicationModel.Background.IBackgroundTaskInstance sender)
    {
        try
        {
            return sender.GetDeferral();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class BackgroundTaskDeferral_stub
{
    public static void Complete_stub(this Windows.ApplicationModel.Background.BackgroundTaskDeferral sender)
    {
        try
        {
            sender.Complete();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class IBackgroundTaskRegistration_stub
{
    public static void Unregister_stub(this Windows.ApplicationModel.Background.IBackgroundTaskRegistration sender, bool cancelTask)
    {
        try
        {
            sender.Unregister(cancelTask);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class BackgroundExecutionManager_stub
{
    public static void RemoveAccess_stub()
    {
        try
        {
            Windows.ApplicationModel.Background.BackgroundExecutionManager.RemoveAccess();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.Background.BackgroundAccessStatus> RequestAccessAsync_stub()
    {
        try
        {
            return await Windows.ApplicationModel.Background.BackgroundExecutionManager.RequestAccessAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class BackgroundTaskBuilder_stub
{
    public static void SetTrigger_stub(this Windows.ApplicationModel.Background.BackgroundTaskBuilder sender, Windows.ApplicationModel.Background.IBackgroundTrigger trigger)
    {
        try
        {
            sender.SetTrigger(trigger);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.ApplicationModel.Background.BackgroundTaskRegistration Register_stub(this Windows.ApplicationModel.Background.BackgroundTaskBuilder sender)
    {
        try
        {
            return sender.Register();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VoipCallCoordinator_stub
{
    public static Windows.ApplicationModel.Calls.VoipCallCoordinator GetDefault_stub()
    {
        try
        {
            return Windows.ApplicationModel.Calls.VoipCallCoordinator.GetDefault();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.Calls.VoipPhoneCallResourceReservationStatus> ReserveCallResourcesAsync_stub(this Windows.ApplicationModel.Calls.VoipCallCoordinator sender)
    {
        try
        {
            return await sender.ReserveCallResourcesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.ApplicationModel.Calls.VoipPhoneCall RequestNewOutgoingCall_stub(this Windows.ApplicationModel.Calls.VoipCallCoordinator sender, string context, string contactName, string serviceName, Windows.ApplicationModel.Calls.VoipPhoneCallMedia media)
    {
        try
        {
            return sender.RequestNewOutgoingCall(context, contactName, serviceName, media);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.ApplicationModel.Calls.VoipPhoneCall RequestNewIncomingCall_stub(this Windows.ApplicationModel.Calls.VoipCallCoordinator sender, string context, string contactName, string contactNumber, System.Uri contactImage, string serviceName, System.Uri brandingImage, string callDetails, System.Uri ringtone, Windows.ApplicationModel.Calls.VoipPhoneCallMedia media, System.TimeSpan ringTimeout)
    {
        try
        {
            return sender.RequestNewIncomingCall(context, contactName, contactNumber, contactImage, serviceName, brandingImage, callDetails, ringtone, media, ringTimeout);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void NotifyMuted_stub(this Windows.ApplicationModel.Calls.VoipCallCoordinator sender)
    {
        try
        {
            sender.NotifyMuted();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void NotifyUnmuted_stub(this Windows.ApplicationModel.Calls.VoipCallCoordinator sender)
    {
        try
        {
            sender.NotifyUnmuted();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VoipPhoneCall_stub
{
    public static void TryShowAppUI_stub(this Windows.ApplicationModel.Calls.VoipPhoneCall sender)
    {
        try
        {
            sender.TryShowAppUI();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void NotifyCallActive_stub(this Windows.ApplicationModel.Calls.VoipPhoneCall sender)
    {
        try
        {
            sender.NotifyCallActive();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void NotifyCallEnded_stub(this Windows.ApplicationModel.Calls.VoipPhoneCall sender)
    {
        try
        {
            sender.NotifyCallEnded();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContactManager_stub
{
    public static async Task<Windows.ApplicationModel.Contacts.Contact> ConvertVCardToContactAsync_stub(Windows.Storage.Streams.IRandomAccessStreamReference vCard)
    {
        try
        {
            return await Windows.ApplicationModel.Contacts.ContactManager.ConvertVCardToContactAsync(vCard);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ShowContactCard_stub(Windows.ApplicationModel.Contacts.Contact contact, Windows.Foundation.Rect selection)
    {
        try
        {
            Windows.ApplicationModel.Contacts.ContactManager.ShowContactCard(contact, selection);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.Contacts.ContactStore> RequestStoreAsync_stub(Windows.ApplicationModel.Contacts.ContactStoreAccessType accessType)
    {
        try
        {
            return await Windows.ApplicationModel.Contacts.ContactManager.RequestStoreAsync(accessType);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.Contacts.ContactAnnotationStore> RequestAnnotationStoreAsync_stub(Windows.ApplicationModel.Contacts.ContactAnnotationStoreAccessType accessType)
    {
        try
        {
            return await Windows.ApplicationModel.Contacts.ContactManager.RequestAnnotationStoreAsync(accessType);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContactStore_stub
{
    public static async Task<System.Collections.Generic.IReadOnlyList<Windows.ApplicationModel.Contacts.Contact>> FindContactsAsync_stub(this Windows.ApplicationModel.Contacts.ContactStore sender)
    {
        try
        {
            return await sender.FindContactsAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.Contacts.ContactList> GetContactListAsync_stub(this Windows.ApplicationModel.Contacts.ContactStore sender, string contactListId)
    {
        try
        {
            return await sender.GetContactListAsync(contactListId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.Contacts.ContactList> CreateContactListAsync_stub(this Windows.ApplicationModel.Contacts.ContactStore sender, string displayName, string userDataAccountId)
    {
        try
        {
            return await sender.CreateContactListAsync(displayName, userDataAccountId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.Contacts.Contact> GetContactAsync_stub(this Windows.ApplicationModel.Contacts.ContactStore sender, string contactId)
    {
        try
        {
            return await sender.GetContactAsync(contactId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContactList_stub
{
    public static async Task<Windows.ApplicationModel.Contacts.Contact> GetContactFromRemoteIdAsync_stub(this Windows.ApplicationModel.Contacts.ContactList sender, string remoteId)
    {
        try
        {
            return await sender.GetContactFromRemoteIdAsync(remoteId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task DeleteContactAsync_stub(this Windows.ApplicationModel.Contacts.ContactList sender, Windows.ApplicationModel.Contacts.Contact contact)
    {
        try
        {
            await sender.DeleteContactAsync(contact);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task SaveContactAsync_stub(this Windows.ApplicationModel.Contacts.ContactList sender, Windows.ApplicationModel.Contacts.Contact contact)
    {
        try
        {
            await sender.SaveContactAsync(contact);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task SaveAsync_stub(this Windows.ApplicationModel.Contacts.ContactList sender)
    {
        try
        {
            await sender.SaveAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContactAnnotationList_stub
{
    public static async Task<System.Collections.Generic.IReadOnlyList<Windows.ApplicationModel.Contacts.ContactAnnotation>> FindAnnotationsByRemoteIdAsync_stub(this Windows.ApplicationModel.Contacts.ContactAnnotationList sender, string remoteId)
    {
        try
        {
            return await sender.FindAnnotationsByRemoteIdAsync(remoteId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<bool> TrySaveAnnotationAsync_stub(this Windows.ApplicationModel.Contacts.ContactAnnotationList sender, Windows.ApplicationModel.Contacts.ContactAnnotation annotation)
    {
        try
        {
            return await sender.TrySaveAnnotationAsync(annotation);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContactAnnotationStore_stub
{
    public static async Task<Windows.ApplicationModel.Contacts.ContactAnnotationList> GetAnnotationListAsync_stub(this Windows.ApplicationModel.Contacts.ContactAnnotationStore sender, string annotationListId)
    {
        try
        {
            return await sender.GetAnnotationListAsync(annotationListId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.Contacts.ContactAnnotationList> CreateAnnotationListAsync_stub(this Windows.ApplicationModel.Contacts.ContactAnnotationStore sender, string userDataAccountId)
    {
        try
        {
            return await sender.CreateAnnotationListAsync(userDataAccountId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<System.Collections.Generic.IReadOnlyList<Windows.ApplicationModel.Contacts.ContactAnnotation>> FindAnnotationsForContactAsync_stub(this Windows.ApplicationModel.Contacts.ContactAnnotationStore sender, Windows.ApplicationModel.Contacts.Contact contact)
    {
        try
        {
            return await sender.FindAnnotationsForContactAsync(contact);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContactPanel_stub
{
    public static void ClosePanel_stub(this Windows.ApplicationModel.Contacts.ContactPanel sender)
    {
        try
        {
            sender.ClosePanel();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CoreApplication_stub
{
    public static Windows.ApplicationModel.Core.CoreApplicationView GetCurrentView_stub()
    {
        try
        {
            return Windows.ApplicationModel.Core.CoreApplication.GetCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.ApplicationModel.Core.CoreApplicationView CreateNewView_stub()
    {
        try
        {
            return Windows.ApplicationModel.Core.CoreApplication.CreateNewView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.Core.AppRestartFailureReason> RequestRestartAsync_stub(string launchArguments)
    {
        try
        {
            return await Windows.ApplicationModel.Core.CoreApplication.RequestRestartAsync(launchArguments);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void EnablePrelaunch_stub(bool value)
    {
        try
        {
            Windows.ApplicationModel.Core.CoreApplication.EnablePrelaunch(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DataTransferManager_stub
{
    public static bool IsSupported_stub()
    {
        try
        {
            return Windows.ApplicationModel.DataTransfer.DataTransferManager.IsSupported();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.ApplicationModel.DataTransfer.DataTransferManager GetForCurrentView_stub()
    {
        try
        {
            return Windows.ApplicationModel.DataTransfer.DataTransferManager.GetForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ShowShareUI_stub()
    {
        try
        {
            Windows.ApplicationModel.DataTransfer.DataTransferManager.ShowShareUI();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Clipboard_stub
{
    public static Windows.ApplicationModel.DataTransfer.DataPackageView GetContent_stub()
    {
        try
        {
            return Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetContent_stub(Windows.ApplicationModel.DataTransfer.DataPackage content)
    {
        try
        {
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(content);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Flush_stub()
    {
        try
        {
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DataPackageView_stub
{
    public static async Task<string> GetTextAsync_stub(this Windows.ApplicationModel.DataTransfer.DataPackageView sender)
    {
        try
        {
            return await sender.GetTextAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<System.Uri> GetApplicationLinkAsync_stub(this Windows.ApplicationModel.DataTransfer.DataPackageView sender)
    {
        try
        {
            return await sender.GetApplicationLinkAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.Streams.RandomAccessStreamReference> GetBitmapAsync_stub(this Windows.ApplicationModel.DataTransfer.DataPackageView sender)
    {
        try
        {
            return await sender.GetBitmapAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<System.Collections.Generic.IReadOnlyList<Windows.Storage.IStorageItem>> GetStorageItemsAsync_stub(this Windows.ApplicationModel.DataTransfer.DataPackageView sender)
    {
        try
        {
            return await sender.GetStorageItemsAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<System.Uri> GetWebLinkAsync_stub(this Windows.ApplicationModel.DataTransfer.DataPackageView sender)
    {
        try
        {
            return await sender.GetWebLinkAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<object> GetDataAsync_stub(this Windows.ApplicationModel.DataTransfer.DataPackageView sender, string formatId)
    {
        try
        {
            return await sender.GetDataAsync(formatId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool Contains_stub(this Windows.ApplicationModel.DataTransfer.DataPackageView sender, string formatId)
    {
        try
        {
            return sender.Contains(formatId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DataPackage_stub
{
    public static void SetApplicationLink_stub(this Windows.ApplicationModel.DataTransfer.DataPackage sender, System.Uri value)
    {
        try
        {
            sender.SetApplicationLink(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetBitmap_stub(this Windows.ApplicationModel.DataTransfer.DataPackage sender, Windows.Storage.Streams.RandomAccessStreamReference value)
    {
        try
        {
            sender.SetBitmap(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetStorageItems_stub(this Windows.ApplicationModel.DataTransfer.DataPackage sender, System.Collections.Generic.IEnumerable<Windows.Storage.IStorageItem> value)
    {
        try
        {
            sender.SetStorageItems(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetText_stub(this Windows.ApplicationModel.DataTransfer.DataPackage sender, string value)
    {
        try
        {
            sender.SetText(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetWebLink_stub(this Windows.ApplicationModel.DataTransfer.DataPackage sender, System.Uri value)
    {
        try
        {
            sender.SetWebLink(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.ApplicationModel.DataTransfer.DataPackageView GetView_stub(this Windows.ApplicationModel.DataTransfer.DataPackage sender)
    {
        try
        {
            return sender.GetView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetData_stub(this Windows.ApplicationModel.DataTransfer.DataPackage sender, string formatId, object value)
    {
        try
        {
            sender.SetData(formatId, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ShareOperation_stub
{
    public static void ReportCompleted_stub(this Windows.ApplicationModel.DataTransfer.ShareTarget.ShareOperation sender)
    {
        try
        {
            sender.ReportCompleted();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ExtendedExecutionSession_stub
{
    public static async Task<Windows.ApplicationModel.ExtendedExecution.ExtendedExecutionResult> RequestExtensionAsync_stub(this Windows.ApplicationModel.ExtendedExecution.ExtendedExecutionSession sender)
    {
        try
        {
            return await sender.RequestExtensionAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Windows.ApplicationModel.ExtendedExecution.ExtendedExecutionSession sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ResourceLoader_stub
{
    public static Windows.ApplicationModel.Resources.ResourceLoader GetForViewIndependentUse_stub(string name)
    {
        try
        {
            return Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetString_stub(this Windows.ApplicationModel.Resources.ResourceLoader sender, string resource)
    {
        try
        {
            return sender.GetString(resource);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ResourceContext_stub
{
    public static void Reset_stub(this Windows.ApplicationModel.Resources.Core.ResourceContext sender)
    {
        try
        {
            sender.Reset();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.ApplicationModel.Resources.Core.ResourceContext GetForCurrentView_stub()
    {
        try
        {
            return Windows.ApplicationModel.Resources.Core.ResourceContext.GetForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.ApplicationModel.Resources.Core.ResourceContext GetForViewIndependentUse_stub()
    {
        try
        {
            return Windows.ApplicationModel.Resources.Core.ResourceContext.GetForViewIndependentUse();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class UserDataAccountManager_stub
{
    public static async Task<Windows.ApplicationModel.UserDataAccounts.UserDataAccountStore> RequestStoreAsync_stub(Windows.ApplicationModel.UserDataAccounts.UserDataAccountStoreAccessType storeAccessType)
    {
        try
        {
            return await Windows.ApplicationModel.UserDataAccounts.UserDataAccountManager.RequestStoreAsync(storeAccessType);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class UserDataAccountStore_stub
{
    public static async Task<Windows.ApplicationModel.UserDataAccounts.UserDataAccount> GetAccountAsync_stub(this Windows.ApplicationModel.UserDataAccounts.UserDataAccountStore sender, string id)
    {
        try
        {
            return await sender.GetAccountAsync(id);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.UserDataAccounts.UserDataAccount> CreateAccountAsync_stub(this Windows.ApplicationModel.UserDataAccounts.UserDataAccountStore sender, string userDisplayName)
    {
        try
        {
            return await sender.CreateAccountAsync(userDisplayName);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class UserDataAccount_stub
{
    public static async Task DeleteAsync_stub(this Windows.ApplicationModel.UserDataAccounts.UserDataAccount sender)
    {
        try
        {
            await sender.DeleteAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class JsonObject_stub
{
    public static Windows.Data.Json.JsonArray GetNamedArray_stub(this Windows.Data.Json.JsonObject sender, string name)
    {
        try
        {
            return sender.GetNamedArray(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Data.Json.JsonObject GetNamedObject_stub(this Windows.Data.Json.JsonObject sender, string name)
    {
        try
        {
            return sender.GetNamedObject(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetNamedString_stub(this Windows.Data.Json.JsonObject sender, string name)
    {
        try
        {
            return sender.GetNamedString(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool TryParse_stub(string input, out Windows.Data.Json.JsonObject result)
    {
        try
        {
            return Windows.Data.Json.JsonObject.TryParse(input, out result);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool GetNamedBoolean_stub(this Windows.Data.Json.JsonObject sender, string name, bool defaultValue)
    {
        try
        {
            return sender.GetNamedBoolean(name, defaultValue);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Data.Json.JsonObject Parse_stub(string input)
    {
        try
        {
            return Windows.Data.Json.JsonObject.Parse(input);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetNamedValue_stub(this Windows.Data.Json.JsonObject sender, string name, Windows.Data.Json.IJsonValue value)
    {
        try
        {
            sender.SetNamedValue(name, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string ToString_stub(this Windows.Data.Json.JsonObject sender)
    {
        try
        {
            return sender.ToString();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Data.Json.JsonObject GetNamedObject_stub(this Windows.Data.Json.JsonObject sender, string name, Windows.Data.Json.JsonObject defaultValue)
    {
        try
        {
            return sender.GetNamedObject(name, defaultValue);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetNamedString_stub(this Windows.Data.Json.JsonObject sender, string name, string defaultValue)
    {
        try
        {
            return sender.GetNamedString(name, defaultValue);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Data.Json.JsonValue GetNamedValue_stub(this Windows.Data.Json.JsonObject sender, string name)
    {
        try
        {
            return sender.GetNamedValue(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string Stringify_stub(this Windows.Data.Json.JsonObject sender)
    {
        try
        {
            return sender.Stringify();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Data.Json.JsonArray GetNamedArray_stub(this Windows.Data.Json.JsonObject sender, string name, Windows.Data.Json.JsonArray defaultValue)
    {
        try
        {
            return sender.GetNamedArray(name, defaultValue);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class JsonArray_stub
{
    public static Windows.Data.Json.JsonArray GetArrayAt_stub(this Windows.Data.Json.JsonArray sender, uint index)
    {
        try
        {
            return sender.GetArrayAt(index);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetStringAt_stub(this Windows.Data.Json.JsonArray sender, uint index)
    {
        try
        {
            return sender.GetStringAt(index);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static double GetNumberAt_stub(this Windows.Data.Json.JsonArray sender, uint index)
    {
        try
        {
            return sender.GetNumberAt(index);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool TryParse_stub(string input, out Windows.Data.Json.JsonArray result)
    {
        try
        {
            return Windows.Data.Json.JsonArray.TryParse(input, out result);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class IJsonValue_stub
{
    public static Windows.Data.Json.JsonObject GetObject_stub(this Windows.Data.Json.IJsonValue sender)
    {
        try
        {
            return sender.GetObject();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetString_stub(this Windows.Data.Json.IJsonValue sender)
    {
        try
        {
            return sender.GetString();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class JsonValue_stub
{
    public static Windows.Data.Json.JsonValue CreateStringValue_stub(string input)
    {
        try
        {
            return Windows.Data.Json.JsonValue.CreateStringValue(input);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string Stringify_stub(this Windows.Data.Json.JsonValue sender)
    {
        try
        {
            return sender.Stringify();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class XmlDocument_stub
{
    public static Windows.Data.Xml.Dom.IXmlNode SelectSingleNode_stub(this Windows.Data.Xml.Dom.XmlDocument sender, string xpath)
    {
        try
        {
            return sender.SelectSingleNode(xpath);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void LoadXml_stub(this Windows.Data.Xml.Dom.XmlDocument sender, string xml)
    {
        try
        {
            sender.LoadXml(xml);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class XmlElement_stub
{
    public static void SetAttribute_stub(this Windows.Data.Xml.Dom.XmlElement sender, string attributeName, string attributeValue)
    {
        try
        {
            sender.SetAttribute(attributeName, attributeValue);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DeviceInformation_stub
{
    public static Windows.Devices.Enumeration.DeviceWatcher CreateWatcher_stub(Windows.Devices.Enumeration.DeviceClass deviceClass)
    {
        try
        {
            return Windows.Devices.Enumeration.DeviceInformation.CreateWatcher(deviceClass);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Devices.Enumeration.DeviceInformation> CreateFromIdAsync_stub(string deviceId)
    {
        try
        {
            return await Windows.Devices.Enumeration.DeviceInformation.CreateFromIdAsync(deviceId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Devices.Enumeration.DeviceInformationCollection> FindAllAsync_stub(Windows.Devices.Enumeration.DeviceClass deviceClass)
    {
        try
        {
            return await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(deviceClass);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DeviceWatcher_stub
{
    public static void Start_stub(this Windows.Devices.Enumeration.DeviceWatcher sender)
    {
        try
        {
            sender.Start();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DeviceAccessInformation_stub
{
    public static Windows.Devices.Enumeration.DeviceAccessInformation CreateFromDeviceClass_stub(Windows.Devices.Enumeration.DeviceClass deviceClass)
    {
        try
        {
            return Windows.Devices.Enumeration.DeviceAccessInformation.CreateFromDeviceClass(deviceClass);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Geolocator_stub
{
    public static async Task<Windows.Devices.Geolocation.GeolocationAccessStatus> RequestAccessAsync_stub()
    {
        try
        {
            return await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Devices.Geolocation.Geoposition> GetGeopositionAsync_stub(this Windows.Devices.Geolocation.Geolocator sender)
    {
        try
        {
            return await sender.GetGeopositionAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ApiInformation_stub
{
    public static bool IsApiContractPresent_stub(string contractName, ushort majorVersion)
    {
        try
        {
            return Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent(contractName, majorVersion);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool IsTypePresent_stub(string typeName)
    {
        try
        {
            return Windows.Foundation.Metadata.ApiInformation.IsTypePresent(typeName);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool IsEnumNamedValuePresent_stub(string enumTypeName, string valueName)
    {
        try
        {
            return Windows.Foundation.Metadata.ApiInformation.IsEnumNamedValuePresent(enumTypeName, valueName);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool IsPropertyPresent_stub(string typeName, string propertyName)
    {
        try
        {
            return Windows.Foundation.Metadata.ApiInformation.IsPropertyPresent(typeName, propertyName);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class GeographicRegion_stub
{
    public static bool IsSupported_stub(string geographicRegionCode)
    {
        try
        {
            return Windows.Globalization.GeographicRegion.IsSupported(geographicRegionCode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Language_stub
{
    public static bool IsWellFormed_stub(string languageTag)
    {
        try
        {
            return Windows.Globalization.Language.IsWellFormed(languageTag);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DateTimeFormatter_stub
{
    public static string Format_stub(this Windows.Globalization.DateTimeFormatting.DateTimeFormatter sender, System.DateTimeOffset value)
    {
        try
        {
            return sender.Format(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CurrencyFormatter_stub
{
    public static string Format_stub(this Windows.Globalization.NumberFormatting.CurrencyFormatter sender, long value)
    {
        try
        {
            return sender.Format(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string Format_stub(this Windows.Globalization.NumberFormatting.CurrencyFormatter sender, double value)
    {
        try
        {
            return sender.Format(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string FormatInt_stub(this Windows.Globalization.NumberFormatting.CurrencyFormatter sender, long value)
    {
        try
        {
            return sender.FormatInt(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string FormatUInt_stub(this Windows.Globalization.NumberFormatting.CurrencyFormatter sender, ulong value)
    {
        try
        {
            return sender.FormatUInt(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string FormatDouble_stub(this Windows.Globalization.NumberFormatting.CurrencyFormatter sender, double value)
    {
        try
        {
            return sender.FormatDouble(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static long? ParseInt_stub(this Windows.Globalization.NumberFormatting.CurrencyFormatter sender, string text)
    {
        try
        {
            return sender.ParseInt(text);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static ulong? ParseUInt_stub(this Windows.Globalization.NumberFormatting.CurrencyFormatter sender, string text)
    {
        try
        {
            return sender.ParseUInt(text);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static double? ParseDouble_stub(this Windows.Globalization.NumberFormatting.CurrencyFormatter sender, string text)
    {
        try
        {
            return sender.ParseDouble(text);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class GraphicsCapturePicker_stub
{
    public static async Task<Windows.Graphics.Capture.GraphicsCaptureItem> PickSingleItemAsync_stub(this Windows.Graphics.Capture.GraphicsCapturePicker sender)
    {
        try
        {
            return await sender.PickSingleItemAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class BitmapDecoder_stub
{
    public static async Task<Windows.Graphics.Imaging.BitmapDecoder> CreateAsync_stub(Windows.Storage.Streams.IRandomAccessStream stream)
    {
        try
        {
            return await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Graphics.Imaging.PixelDataProvider> GetPixelDataAsync_stub(this Windows.Graphics.Imaging.BitmapDecoder sender, Windows.Graphics.Imaging.BitmapPixelFormat pixelFormat, Windows.Graphics.Imaging.BitmapAlphaMode alphaMode, Windows.Graphics.Imaging.BitmapTransform transform, Windows.Graphics.Imaging.ExifOrientationMode exifOrientationMode, Windows.Graphics.Imaging.ColorManagementMode colorManagementMode)
    {
        try
        {
            return await sender.GetPixelDataAsync(pixelFormat, alphaMode, transform, exifOrientationMode, colorManagementMode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Graphics.Imaging.SoftwareBitmap> GetSoftwareBitmapAsync_stub(this Windows.Graphics.Imaging.BitmapDecoder sender, Windows.Graphics.Imaging.BitmapPixelFormat pixelFormat, Windows.Graphics.Imaging.BitmapAlphaMode alphaMode, Windows.Graphics.Imaging.BitmapTransform transform, Windows.Graphics.Imaging.ExifOrientationMode exifOrientationMode, Windows.Graphics.Imaging.ColorManagementMode colorManagementMode)
    {
        try
        {
            return await sender.GetSoftwareBitmapAsync(pixelFormat, alphaMode, transform, exifOrientationMode, colorManagementMode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class PixelDataProvider_stub
{
    public static byte[] DetachPixelData_stub(this Windows.Graphics.Imaging.PixelDataProvider sender)
    {
        try
        {
            return sender.DetachPixelData();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class BitmapEncoder_stub
{
    public static async Task<Windows.Graphics.Imaging.BitmapEncoder> CreateAsync_stub(System.Guid encoderId, Windows.Storage.Streams.IRandomAccessStream stream)
    {
        try
        {
            return await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(encoderId, stream);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetSoftwareBitmap_stub(this Windows.Graphics.Imaging.BitmapEncoder sender, Windows.Graphics.Imaging.SoftwareBitmap bitmap)
    {
        try
        {
            sender.SetSoftwareBitmap(bitmap);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task FlushAsync_stub(this Windows.Graphics.Imaging.BitmapEncoder sender)
    {
        try
        {
            await sender.FlushAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class PackageManager_stub
{
    public static Windows.Foundation.IAsyncOperationWithProgress<Windows.Management.Deployment.DeploymentResult, Windows.Management.Deployment.DeploymentProgress> AddPackageAsync_stub(this Windows.Management.Deployment.PackageManager sender, System.Uri packageUri, System.Collections.Generic.IEnumerable<System.Uri> dependencyPackageUris, Windows.Management.Deployment.DeploymentOptions deploymentOptions)
    {
        try
        {
            return sender.AddPackageAsync(packageUri, dependencyPackageUris, deploymentOptions);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class AudioFrame_stub
{
    public static Windows.Media.AudioBuffer LockBuffer_stub(this Windows.Media.AudioFrame sender, Windows.Media.AudioBufferAccessMode mode)
    {
        try
        {
            return sender.LockBuffer(mode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class AudioBuffer_stub
{
    public static Windows.Foundation.IMemoryBufferReference CreateReference_stub(this Windows.Media.AudioBuffer sender)
    {
        try
        {
            return sender.CreateReference();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class SystemMediaTransportControlsDisplayUpdater_stub
{
    public static void ClearAll_stub(this Windows.Media.SystemMediaTransportControlsDisplayUpdater sender)
    {
        try
        {
            sender.ClearAll();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Update_stub(this Windows.Media.SystemMediaTransportControlsDisplayUpdater sender)
    {
        try
        {
            sender.Update();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class SystemMediaTransportControls_stub
{
    public static Windows.Media.SystemMediaTransportControls GetForCurrentView_stub()
    {
        try
        {
            return Windows.Media.SystemMediaTransportControls.GetForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class AudioGraph_stub
{
    public static void Stop_stub(this Windows.Media.Audio.AudioGraph sender)
    {
        try
        {
            sender.Stop();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Windows.Media.Audio.AudioGraph sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.Audio.CreateAudioGraphResult> CreateAsync_stub(Windows.Media.Audio.AudioGraphSettings settings)
    {
        try
        {
            return await Windows.Media.Audio.AudioGraph.CreateAsync(settings);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.Audio.CreateAudioFileInputNodeResult> CreateFileInputNodeAsync_stub(this Windows.Media.Audio.AudioGraph sender, Windows.Storage.IStorageFile file)
    {
        try
        {
            return await sender.CreateFileInputNodeAsync(file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.Audio.CreateAudioDeviceOutputNodeResult> CreateDeviceOutputNodeAsync_stub(this Windows.Media.Audio.AudioGraph sender)
    {
        try
        {
            return await sender.CreateDeviceOutputNodeAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Start_stub(this Windows.Media.Audio.AudioGraph sender)
    {
        try
        {
            sender.Start();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class AudioFileInputNode_stub
{
    public static void AddOutgoingConnection_stub(this Windows.Media.Audio.AudioFileInputNode sender, Windows.Media.Audio.IAudioNode destination)
    {
        try
        {
            sender.AddOutgoingConnection(destination);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CameraCaptureUI_stub
{
    public static async Task<Windows.Storage.StorageFile> CaptureFileAsync_stub(this Windows.Media.Capture.CameraCaptureUI sender, Windows.Media.Capture.CameraCaptureUIMode mode)
    {
        try
        {
            return await sender.CaptureFileAsync(mode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class MediaCapture_stub
{
    public static async Task InitializeAsync_stub(this Windows.Media.Capture.MediaCapture sender, Windows.Media.Capture.MediaCaptureInitializationSettings mediaCaptureInitializationSettings)
    {
        try
        {
            await sender.InitializeAsync(mediaCaptureInitializationSettings);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Windows.Media.Capture.MediaCapture sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.Capture.Frames.MediaFrameReader> CreateFrameReaderAsync_stub(this Windows.Media.Capture.MediaCapture sender, Windows.Media.Capture.Frames.MediaFrameSource inputSource)
    {
        try
        {
            return await sender.CreateFrameReaderAsync(inputSource);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.Capture.LowLagMediaRecording> PrepareLowLagRecordToStorageFileAsync_stub(this Windows.Media.Capture.MediaCapture sender, Windows.Media.MediaProperties.MediaEncodingProfile encodingProfile, Windows.Storage.IStorageFile file)
    {
        try
        {
            return await sender.PrepareLowLagRecordToStorageFileAsync(encodingProfile, file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.Capture.MediaCaptureStopResult> StopRecordWithResultAsync_stub(this Windows.Media.Capture.MediaCapture sender)
    {
        try
        {
            return await sender.StopRecordWithResultAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IReadOnlyList<Windows.Media.Capture.MediaCaptureVideoProfile> FindAllVideoProfiles_stub(string videoDeviceId)
    {
        try
        {
            return Windows.Media.Capture.MediaCapture.FindAllVideoProfiles(videoDeviceId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class LowLagMediaRecording_stub
{
    public static async Task StartAsync_stub(this Windows.Media.Capture.LowLagMediaRecording sender)
    {
        try
        {
            await sender.StartAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task ResumeAsync_stub(this Windows.Media.Capture.LowLagMediaRecording sender)
    {
        try
        {
            await sender.ResumeAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.Capture.MediaCapturePauseResult> PauseWithResultAsync_stub(this Windows.Media.Capture.LowLagMediaRecording sender, Windows.Media.Devices.MediaCapturePauseBehavior behavior)
    {
        try
        {
            return await sender.PauseWithResultAsync(behavior);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.Capture.MediaCaptureStopResult> StopWithResultAsync_stub(this Windows.Media.Capture.LowLagMediaRecording sender)
    {
        try
        {
            return await sender.StopWithResultAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task FinishAsync_stub(this Windows.Media.Capture.LowLagMediaRecording sender)
    {
        try
        {
            await sender.FinishAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class MediaFrameReader_stub
{
    public static void Dispose_stub(this Windows.Media.Capture.Frames.MediaFrameReader sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.Capture.Frames.MediaFrameReaderStartStatus> StartAsync_stub(this Windows.Media.Capture.Frames.MediaFrameReader sender)
    {
        try
        {
            return await sender.StartAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Media.Capture.Frames.MediaFrameReference TryAcquireLatestFrame_stub(this Windows.Media.Capture.Frames.MediaFrameReader sender)
    {
        try
        {
            return sender.TryAcquireLatestFrame();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task StopAsync_stub(this Windows.Media.Capture.Frames.MediaFrameReader sender)
    {
        try
        {
            await sender.StopAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class AudioMediaFrame_stub
{
    public static Windows.Media.AudioFrame GetAudioFrame_stub(this Windows.Media.Capture.Frames.AudioMediaFrame sender)
    {
        try
        {
            return sender.GetAudioFrame();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class MediaSource_stub
{
    public static Windows.Media.Core.MediaSource CreateFromStorageFile_stub(Windows.Storage.IStorageFile file)
    {
        try
        {
            return Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class MediaDevice_stub
{
    public static string GetDefaultAudioCaptureId_stub(Windows.Media.Devices.AudioDeviceRole role)
    {
        try
        {
            return Windows.Media.Devices.MediaDevice.GetDefaultAudioCaptureId(role);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetDefaultAudioRenderId_stub(Windows.Media.Devices.AudioDeviceRole role)
    {
        try
        {
            return Windows.Media.Devices.MediaDevice.GetDefaultAudioRenderId(role);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VideoDeviceController_stub
{
    public static System.Collections.Generic.IReadOnlyList<Windows.Media.MediaProperties.IMediaEncodingProperties> GetAvailableMediaStreamProperties_stub(this Windows.Media.Devices.VideoDeviceController sender, Windows.Media.Capture.MediaStreamType mediaStreamType)
    {
        try
        {
            return sender.GetAvailableMediaStreamProperties(mediaStreamType);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class MediaEncodingProfile_stub
{
    public static Windows.Media.MediaProperties.MediaEncodingProfile CreateMp4_stub(Windows.Media.MediaProperties.VideoEncodingQuality quality)
    {
        try
        {
            return Windows.Media.MediaProperties.MediaEncodingProfile.CreateMp4(quality);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Media.MediaProperties.MediaEncodingProfile CreateWav_stub(Windows.Media.MediaProperties.AudioEncodingQuality quality)
    {
        try
        {
            return Windows.Media.MediaProperties.MediaEncodingProfile.CreateWav(quality);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.MediaProperties.MediaEncodingProfile> CreateFromFileAsync_stub(Windows.Storage.IStorageFile file)
    {
        try
        {
            return await Windows.Media.MediaProperties.MediaEncodingProfile.CreateFromFileAsync(file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class MediaPlayer_stub
{
    public static void Pause_stub(this Windows.Media.Playback.MediaPlayer sender)
    {
        try
        {
            sender.Pause();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Play_stub(this Windows.Media.Playback.MediaPlayer sender)
    {
        try
        {
            sender.Play();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class MediaTranscoder_stub
{
    public static void AddVideoEffect_stub(this Windows.Media.Transcoding.MediaTranscoder sender, string activatableClassId, bool effectRequired, Windows.Foundation.Collections.IPropertySet configuration)
    {
        try
        {
            sender.AddVideoEffect(activatableClassId, effectRequired, configuration);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Media.Transcoding.PrepareTranscodeResult> PrepareFileTranscodeAsync_stub(this Windows.Media.Transcoding.MediaTranscoder sender, Windows.Storage.IStorageFile source, Windows.Storage.IStorageFile destination, Windows.Media.MediaProperties.MediaEncodingProfile profile)
    {
        try
        {
            return await sender.PrepareFileTranscodeAsync(source, destination, profile);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class PrepareTranscodeResult_stub
{
    public static Windows.Foundation.IAsyncActionWithProgress<double> TranscodeAsync_stub(this Windows.Media.Transcoding.PrepareTranscodeResult sender)
    {
        try
        {
            return sender.TranscodeAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class NetworkInformation_stub
{
    public static Windows.Networking.Connectivity.ConnectionProfile GetInternetConnectionProfile_stub()
    {
        try
        {
            return Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ConnectionProfile_stub
{
    public static Windows.Networking.Connectivity.ConnectionCost GetConnectionCost_stub(this Windows.Networking.Connectivity.ConnectionProfile sender)
    {
        try
        {
            return sender.GetConnectionCost();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Networking.Connectivity.NetworkConnectivityLevel GetNetworkConnectivityLevel_stub(this Windows.Networking.Connectivity.ConnectionProfile sender)
    {
        try
        {
            return sender.GetNetworkConnectivityLevel();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class PushNotificationChannelManager_stub
{
    public static async Task<Windows.Networking.PushNotifications.PushNotificationChannel> CreatePushNotificationChannelForApplicationAsync_stub()
    {
        try
        {
            return await Windows.Networking.PushNotifications.PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class PushNotificationChannel_stub
{
    public static void Close_stub(this Windows.Networking.PushNotifications.PushNotificationChannel sender)
    {
        try
        {
            sender.Close();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class KeyCredentialManager_stub
{
    public static async Task<bool> IsSupportedAsync_stub()
    {
        try
        {
            return await Windows.Security.Credentials.KeyCredentialManager.IsSupportedAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Security.Credentials.KeyCredentialRetrievalResult> OpenAsync_stub(string name)
    {
        try
        {
            return await Windows.Security.Credentials.KeyCredentialManager.OpenAsync(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Security.Credentials.KeyCredentialRetrievalResult> RequestCreateAsync_stub(string name, Windows.Security.Credentials.KeyCredentialCreationOption option)
    {
        try
        {
            return await Windows.Security.Credentials.KeyCredentialManager.RequestCreateAsync(name, option);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class KeyCredential_stub
{
    public static async Task<Windows.Security.Credentials.KeyCredentialOperationResult> RequestSignAsync_stub(this Windows.Security.Credentials.KeyCredential sender, Windows.Storage.Streams.IBuffer data)
    {
        try
        {
            return await sender.RequestSignAsync(data);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CryptographicBuffer_stub
{
    public static Windows.Storage.Streams.IBuffer CreateFromByteArray_stub(byte[] value)
    {
        try
        {
            return Windows.Security.Cryptography.CryptographicBuffer.CreateFromByteArray(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void CopyToByteArray_stub(Windows.Storage.Streams.IBuffer buffer, out byte[] value)
    {
        try
        {
            Windows.Security.Cryptography.CryptographicBuffer.CopyToByteArray(buffer, out value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.IBuffer GenerateRandom_stub(uint length)
    {
        try
        {
            return Windows.Security.Cryptography.CryptographicBuffer.GenerateRandom(length);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.IBuffer ConvertStringToBinary_stub(string value, Windows.Security.Cryptography.BinaryStringEncoding encoding)
    {
        try
        {
            return Windows.Security.Cryptography.CryptographicBuffer.ConvertStringToBinary(value, encoding);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class HashAlgorithmProvider_stub
{
    public static Windows.Security.Cryptography.Core.HashAlgorithmProvider OpenAlgorithm_stub(string algorithm)
    {
        try
        {
            return Windows.Security.Cryptography.Core.HashAlgorithmProvider.OpenAlgorithm(algorithm);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.IBuffer HashData_stub(this Windows.Security.Cryptography.Core.HashAlgorithmProvider sender, Windows.Storage.Streams.IBuffer data)
    {
        try
        {
            return sender.HashData(data);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class MapLocationFinder_stub
{
    public static async Task<Windows.Services.Maps.MapLocationFinderResult> FindLocationsAtAsync_stub(Windows.Devices.Geolocation.Geopoint queryPoint, Windows.Services.Maps.MapLocationDesiredAccuracy accuracy)
    {
        try
        {
            return await Windows.Services.Maps.MapLocationFinder.FindLocationsAtAsync(queryPoint, accuracy);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class StoreContext_stub
{
    public static Windows.Services.Store.StoreContext GetDefault_stub()
    {
        try
        {
            return Windows.Services.Store.StoreContext.GetDefault();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<System.Collections.Generic.IReadOnlyList<Windows.Services.Store.StorePackageUpdate>> GetAppAndOptionalStorePackageUpdatesAsync_stub(this Windows.Services.Store.StoreContext sender)
    {
        try
        {
            return await sender.GetAppAndOptionalStorePackageUpdatesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ApplicationDataContainer_stub
{
    public static Windows.Storage.ApplicationDataContainer CreateContainer_stub(this Windows.Storage.ApplicationDataContainer sender, string name, Windows.Storage.ApplicationDataCreateDisposition disposition)
    {
        try
        {
            return sender.CreateContainer(name, disposition);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DeleteContainer_stub(this Windows.Storage.ApplicationDataContainer sender, string name)
    {
        try
        {
            sender.DeleteContainer(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class StorageFile_stub
{
    public static async Task<Windows.Storage.StorageFile> GetFileFromPathAsync_stub(string path)
    {
        try
        {
            return await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.Streams.IRandomAccessStreamWithContentType> OpenReadAsync_stub(this Windows.Storage.StorageFile sender)
    {
        try
        {
            return await sender.OpenReadAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.Streams.IRandomAccessStream> OpenAsync_stub(this Windows.Storage.StorageFile sender, Windows.Storage.FileAccessMode accessMode)
    {
        try
        {
            return await sender.OpenAsync(accessMode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFile> GetFileFromApplicationUriAsync_stub(System.Uri uri)
    {
        try
        {
            return await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(uri);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task DeleteAsync_stub(this Windows.Storage.StorageFile sender)
    {
        try
        {
            await sender.DeleteAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task CopyAndReplaceAsync_stub(this Windows.Storage.StorageFile sender, Windows.Storage.IStorageFile fileToReplace)
    {
        try
        {
            await sender.CopyAndReplaceAsync(fileToReplace);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task RenameAsync_stub(this Windows.Storage.StorageFile sender, string desiredName, Windows.Storage.NameCollisionOption option)
    {
        try
        {
            await sender.RenameAsync(desiredName, option);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task DeleteAsync_stub(this Windows.Storage.StorageFile sender, Windows.Storage.StorageDeleteOption option)
    {
        try
        {
            await sender.DeleteAsync(option);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFolder> GetParentAsync_stub(this Windows.Storage.StorageFile sender)
    {
        try
        {
            return await sender.GetParentAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.FileProperties.BasicProperties> GetBasicPropertiesAsync_stub(this Windows.Storage.StorageFile sender)
    {
        try
        {
            return await sender.GetBasicPropertiesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.FileProperties.StorageItemThumbnail> GetThumbnailAsync_stub(this Windows.Storage.StorageFile sender, Windows.Storage.FileProperties.ThumbnailMode mode, uint requestedSize, Windows.Storage.FileProperties.ThumbnailOptions options)
    {
        try
        {
            return await sender.GetThumbnailAsync(mode, requestedSize, options);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task MoveAndReplaceAsync_stub(this Windows.Storage.StorageFile sender, Windows.Storage.IStorageFile fileToReplace)
    {
        try
        {
            await sender.MoveAndReplaceAsync(fileToReplace);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.FileProperties.StorageItemThumbnail> GetThumbnailAsync_stub(this Windows.Storage.StorageFile sender, Windows.Storage.FileProperties.ThumbnailMode mode, uint requestedSize)
    {
        try
        {
            return await sender.GetThumbnailAsync(mode, requestedSize);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFile> CopyAsync_stub(this Windows.Storage.StorageFile sender, Windows.Storage.IStorageFolder destinationFolder, string desiredNewName, Windows.Storage.NameCollisionOption option)
    {
        try
        {
            return await sender.CopyAsync(destinationFolder, desiredNewName, option);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class StorageFolder_stub
{
    public static async Task<Windows.Storage.IStorageItem> TryGetItemAsync_stub(this Windows.Storage.StorageFolder sender, string name)
    {
        try
        {
            return await sender.TryGetItemAsync(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFile> CreateFileAsync_stub(this Windows.Storage.StorageFolder sender, string desiredName, Windows.Storage.CreationCollisionOption options)
    {
        try
        {
            return await sender.CreateFileAsync(desiredName, options);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFile> CreateFileAsync_stub(this Windows.Storage.StorageFolder sender, string desiredName)
    {
        try
        {
            return await sender.CreateFileAsync(desiredName);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFolder> CreateFolderAsync_stub(this Windows.Storage.StorageFolder sender, string desiredName, Windows.Storage.CreationCollisionOption options)
    {
        try
        {
            return await sender.CreateFolderAsync(desiredName, options);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<System.Collections.Generic.IReadOnlyList<Windows.Storage.StorageFile>> GetFilesAsync_stub(this Windows.Storage.StorageFolder sender)
    {
        try
        {
            return await sender.GetFilesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFolder> GetFolderAsync_stub(this Windows.Storage.StorageFolder sender, string name)
    {
        try
        {
            return await sender.GetFolderAsync(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFile> GetFileAsync_stub(this Windows.Storage.StorageFolder sender, string name)
    {
        try
        {
            return await sender.GetFileAsync(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class KnownFolders_stub
{
    public static async Task<Windows.Storage.StorageFolder> GetFolderAsync_stub(Windows.Storage.KnownFolderId folderId)
    {
        try
        {
            return await Windows.Storage.KnownFolders.GetFolderAsync(folderId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DownloadsFolder_stub
{
    public static async Task<Windows.Storage.StorageFile> CreateFileAsync_stub(string desiredName, Windows.Storage.CreationCollisionOption option)
    {
        try
        {
            return await Windows.Storage.DownloadsFolder.CreateFileAsync(desiredName, option);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FileIO_stub
{
    public static async Task<System.Collections.Generic.IList<string>> ReadLinesAsync_stub(Windows.Storage.IStorageFile file)
    {
        try
        {
            return await Windows.Storage.FileIO.ReadLinesAsync(file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task WriteBytesAsync_stub(Windows.Storage.IStorageFile file, byte[] buffer)
    {
        try
        {
            await Windows.Storage.FileIO.WriteBytesAsync(file, buffer);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<string> ReadTextAsync_stub(Windows.Storage.IStorageFile file)
    {
        try
        {
            return await Windows.Storage.FileIO.ReadTextAsync(file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task WriteTextAsync_stub(Windows.Storage.IStorageFile file, string contents)
    {
        try
        {
            await Windows.Storage.FileIO.WriteTextAsync(file, contents);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class StorageItemAccessList_stub
{
    public static bool ContainsItem_stub(this Windows.Storage.AccessCache.StorageItemAccessList sender, string token)
    {
        try
        {
            return sender.ContainsItem(token);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Remove_stub(this Windows.Storage.AccessCache.StorageItemAccessList sender, string token)
    {
        try
        {
            sender.Remove(token);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool CheckAccess_stub(this Windows.Storage.AccessCache.StorageItemAccessList sender, Windows.Storage.IStorageItem file)
    {
        try
        {
            return sender.CheckAccess(file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFile> GetFileAsync_stub(this Windows.Storage.AccessCache.StorageItemAccessList sender, string token)
    {
        try
        {
            return await sender.GetFileAsync(token);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFolder> GetFolderAsync_stub(this Windows.Storage.AccessCache.StorageItemAccessList sender, string token)
    {
        try
        {
            return await sender.GetFolderAsync(token);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddOrReplace_stub(this Windows.Storage.AccessCache.StorageItemAccessList sender, string token, Windows.Storage.IStorageItem file)
    {
        try
        {
            sender.AddOrReplace(token, file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string Add_stub(this Windows.Storage.AccessCache.StorageItemAccessList sender, Windows.Storage.IStorageItem file)
    {
        try
        {
            return sender.Add(file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Clear_stub(this Windows.Storage.AccessCache.StorageItemAccessList sender)
    {
        try
        {
            sender.Clear();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class StorageItemMostRecentlyUsedList_stub
{
    public static bool ContainsItem_stub(this Windows.Storage.AccessCache.StorageItemMostRecentlyUsedList sender, string token)
    {
        try
        {
            return sender.ContainsItem(token);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFolder> GetFolderAsync_stub(this Windows.Storage.AccessCache.StorageItemMostRecentlyUsedList sender, string token)
    {
        try
        {
            return await sender.GetFolderAsync(token);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Remove_stub(this Windows.Storage.AccessCache.StorageItemMostRecentlyUsedList sender, string token)
    {
        try
        {
            sender.Remove(token);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class StorageItemContentProperties_stub
{
    public static async Task<Windows.Storage.FileProperties.VideoProperties> GetVideoPropertiesAsync_stub(this Windows.Storage.FileProperties.StorageItemContentProperties sender)
    {
        try
        {
            return await sender.GetVideoPropertiesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.FileProperties.MusicProperties> GetMusicPropertiesAsync_stub(this Windows.Storage.FileProperties.StorageItemContentProperties sender)
    {
        try
        {
            return await sender.GetMusicPropertiesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FileOpenPicker_stub
{
    public static async Task<System.Collections.Generic.IReadOnlyList<Windows.Storage.StorageFile>> PickMultipleFilesAsync_stub(this Windows.Storage.Pickers.FileOpenPicker sender)
    {
        try
        {
            return await sender.PickMultipleFilesAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.StorageFile> PickSingleFileAsync_stub(this Windows.Storage.Pickers.FileOpenPicker sender)
    {
        try
        {
            return await sender.PickSingleFileAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FileSavePicker_stub
{
    public static async Task<Windows.Storage.StorageFile> PickSaveFileAsync_stub(this Windows.Storage.Pickers.FileSavePicker sender)
    {
        try
        {
            return await sender.PickSaveFileAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FolderPicker_stub
{
    public static async Task<Windows.Storage.StorageFolder> PickSingleFolderAsync_stub(this Windows.Storage.Pickers.FolderPicker sender)
    {
        try
        {
            return await sender.PickSingleFolderAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class InMemoryRandomAccessStream_stub
{
    public static void Seek_stub(this Windows.Storage.Streams.InMemoryRandomAccessStream sender, ulong position)
    {
        try
        {
            sender.Seek(position);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.IOutputStream GetOutputStreamAt_stub(this Windows.Storage.Streams.InMemoryRandomAccessStream sender, ulong position)
    {
        try
        {
            return sender.GetOutputStreamAt(position);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.IRandomAccessStream CloneStream_stub(this Windows.Storage.Streams.InMemoryRandomAccessStream sender)
    {
        try
        {
            return sender.CloneStream();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DataWriter_stub
{
    public static uint WriteString_stub(this Windows.Storage.Streams.DataWriter sender, string value)
    {
        try
        {
            return sender.WriteString(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.DataWriterStoreOperation StoreAsync_stub(this Windows.Storage.Streams.DataWriter sender)
    {
        try
        {
            return sender.StoreAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void WriteInt32_stub(this Windows.Storage.Streams.DataWriter sender, int value)
    {
        try
        {
            sender.WriteInt32(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void WriteByte_stub(this Windows.Storage.Streams.DataWriter sender, byte value)
    {
        try
        {
            sender.WriteByte(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void WriteInt64_stub(this Windows.Storage.Streams.DataWriter sender, long value)
    {
        try
        {
            sender.WriteInt64(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void WriteUInt32_stub(this Windows.Storage.Streams.DataWriter sender, uint value)
    {
        try
        {
            sender.WriteUInt32(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static uint MeasureString_stub(this Windows.Storage.Streams.DataWriter sender, string value)
    {
        try
        {
            return sender.MeasureString(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<bool> FlushAsync_stub(this Windows.Storage.Streams.DataWriter sender)
    {
        try
        {
            return await sender.FlushAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class RandomAccessStreamReference_stub
{
    public static Windows.Storage.Streams.RandomAccessStreamReference CreateFromStream_stub(Windows.Storage.Streams.IRandomAccessStream stream)
    {
        try
        {
            return Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(stream);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.Storage.Streams.IRandomAccessStreamWithContentType> OpenReadAsync_stub(this Windows.Storage.Streams.RandomAccessStreamReference sender)
    {
        try
        {
            return await sender.OpenReadAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.RandomAccessStreamReference CreateFromFile_stub(Windows.Storage.IStorageFile file)
    {
        try
        {
            return Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class IInputStream_stub
{
    public static Windows.Foundation.IAsyncOperationWithProgress<Windows.Storage.Streams.IBuffer, uint> ReadAsync_stub(this Windows.Storage.Streams.IInputStream sender, Windows.Storage.Streams.IBuffer buffer, uint count, Windows.Storage.Streams.InputStreamOptions options)
    {
        try
        {
            return sender.ReadAsync(buffer, count, options);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class IOutputStream_stub
{
    public static Windows.Foundation.IAsyncOperationWithProgress<uint, uint> WriteAsync_stub(this Windows.Storage.Streams.IOutputStream sender, Windows.Storage.Streams.IBuffer buffer)
    {
        try
        {
            return sender.WriteAsync(buffer);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DataReader_stub
{
    public static Windows.Storage.Streams.DataReaderLoadOperation LoadAsync_stub(this Windows.Storage.Streams.DataReader sender, uint count)
    {
        try
        {
            return sender.LoadAsync(count);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ReadBytes_stub(this Windows.Storage.Streams.DataReader sender, byte[] value)
    {
        try
        {
            sender.ReadBytes(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static long ReadInt64_stub(this Windows.Storage.Streams.DataReader sender)
    {
        try
        {
            return sender.ReadInt64();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int ReadInt32_stub(this Windows.Storage.Streams.DataReader sender)
    {
        try
        {
            return sender.ReadInt32();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static byte ReadByte_stub(this Windows.Storage.Streams.DataReader sender)
    {
        try
        {
            return sender.ReadByte();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string ReadString_stub(this Windows.Storage.Streams.DataReader sender, uint codeUnitCount)
    {
        try
        {
            return sender.ReadString(codeUnitCount);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static uint ReadUInt32_stub(this Windows.Storage.Streams.DataReader sender)
    {
        try
        {
            return sender.ReadUInt32();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class IRandomAccessStream_stub
{
    public static Windows.Storage.Streams.IInputStream GetInputStreamAt_stub(this Windows.Storage.Streams.IRandomAccessStream sender, ulong position)
    {
        try
        {
            return sender.GetInputStreamAt(position);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Storage.Streams.IOutputStream GetOutputStreamAt_stub(this Windows.Storage.Streams.IRandomAccessStream sender, ulong position)
    {
        try
        {
            return sender.GetOutputStreamAt(position);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class RandomAccessStream_stub
{
    public static Windows.Foundation.IAsyncOperationWithProgress<ulong, ulong> CopyAsync_stub(Windows.Storage.Streams.IInputStream source, Windows.Storage.Streams.IOutputStream destination)
    {
        try
        {
            return Windows.Storage.Streams.RandomAccessStream.CopyAsync(source, destination);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DispatcherQueue_stub
{
    public static bool TryEnqueue_stub(this Windows.System.DispatcherQueue sender, Windows.System.DispatcherQueueHandler callback)
    {
        try
        {
            return sender.TryEnqueue(callback);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.System.DispatcherQueue GetForCurrentThread_stub()
    {
        try
        {
            return Windows.System.DispatcherQueue.GetForCurrentThread();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool TryEnqueue_stub(this Windows.System.DispatcherQueue sender, Windows.System.DispatcherQueuePriority priority, Windows.System.DispatcherQueueHandler callback)
    {
        try
        {
            return sender.TryEnqueue(priority, callback);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Launcher_stub
{
    public static async Task<bool> LaunchUriAsync_stub(System.Uri uri)
    {
        try
        {
            return await Windows.System.Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<bool> LaunchFileAsync_stub(Windows.Storage.IStorageFile file)
    {
        try
        {
            return await Windows.System.Launcher.LaunchFileAsync(file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<bool> LaunchFileAsync_stub(Windows.Storage.IStorageFile file, Windows.System.LauncherOptions options)
    {
        try
        {
            return await Windows.System.Launcher.LaunchFileAsync(file, options);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<bool> LaunchFolderAsync_stub(Windows.Storage.IStorageFolder folder, Windows.System.FolderLauncherOptions options)
    {
        try
        {
            return await Windows.System.Launcher.LaunchFolderAsync(folder, options);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<bool> LaunchUriAsync_stub(System.Uri uri, Windows.System.LauncherOptions options)
    {
        try
        {
            return await Windows.System.Launcher.LaunchUriAsync(uri, options);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.System.LaunchQuerySupportStatus> QueryFileSupportAsync_stub(Windows.Storage.StorageFile file)
    {
        try
        {
            return await Windows.System.Launcher.QueryFileSupportAsync(file);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DisplayRequest_stub
{
    public static void RequestActive_stub(this Windows.System.Display.DisplayRequest sender)
    {
        try
        {
            sender.RequestActive();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RequestRelease_stub(this Windows.System.Display.DisplayRequest sender)
    {
        try
        {
            sender.RequestRelease();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CompositionAnimation_stub
{
    public static void ClearAllParameters_stub(this Microsoft.UI.Composition.CompositionAnimation sender)
    {
        try
        {
            sender.ClearAllParameters();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetReferenceParameter_stub(this Microsoft.UI.Composition.CompositionAnimation sender, string key, Microsoft.UI.Composition.CompositionObject compositionObject)
    {
        try
        {
            sender.SetReferenceParameter(key, compositionObject);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetScalarParameter_stub(this Microsoft.UI.Composition.CompositionAnimation sender, string key, float value)
    {
        try
        {
            sender.SetScalarParameter(key, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CompositionObject_stub
{
    public static void StartAnimation_stub(this Microsoft.UI.Composition.CompositionObject sender, string propertyName, Microsoft.UI.Composition.CompositionAnimation animation)
    {
        try
        {
            sender.StartAnimation(propertyName, animation);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.AnimationController TryGetAnimationController_stub(this Microsoft.UI.Composition.CompositionObject sender, string propertyName)
    {
        try
        {
            return sender.TryGetAnimationController(propertyName);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Microsoft.UI.Composition.CompositionObject sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void StopAnimation_stub(this Microsoft.UI.Composition.CompositionObject sender, string propertyName)
    {
        try
        {
            sender.StopAnimation(propertyName);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void StartAnimation_stub(this Microsoft.UI.Composition.CompositionObject sender, string propertyName, Microsoft.UI.Composition.CompositionAnimation animation, Microsoft.UI.Composition.AnimationController animationController)
    {
        try
        {
            sender.StartAnimation(propertyName, animation, animationController);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Compositor_stub
{
    public static Microsoft.UI.Composition.Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateVector2KeyFrameAnimation();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionSpriteShape CreateSpriteShape_stub(this Microsoft.UI.Composition.Compositor sender, Microsoft.UI.Composition.CompositionGeometry geometry)
    {
        try
        {
            return sender.CreateSpriteShape(geometry);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionColorBrush CreateColorBrush_stub(this Microsoft.UI.Composition.Compositor sender, Windows.UI.Color color)
    {
        try
        {
            return sender.CreateColorBrush(color);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionEffectFactory CreateEffectFactory_stub(this Microsoft.UI.Composition.Compositor sender, Windows.Graphics.Effects.IGraphicsEffect graphicsEffect)
    {
        try
        {
            return sender.CreateEffectFactory(graphicsEffect);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionPathGeometry CreatePathGeometry_stub(this Microsoft.UI.Composition.Compositor sender, Microsoft.UI.Composition.CompositionPath path)
    {
        try
        {
            return sender.CreatePathGeometry(path);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionSurfaceBrush CreateSurfaceBrush_stub(this Microsoft.UI.Composition.Compositor sender, Microsoft.UI.Composition.ICompositionSurface surface)
    {
        try
        {
            return sender.CreateSurfaceBrush(surface);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionVisualSurface CreateVisualSurface_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateVisualSurface();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.ContainerVisual CreateContainerVisual_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateContainerVisual();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.ShapeVisual CreateShapeVisual_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateShapeVisual();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.SpriteVisual CreateSpriteVisual_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateSpriteVisual();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.StepEasingFunction CreateStepEasingFunction_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateStepEasingFunction();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CubicBezierEasingFunction CreateCubicBezierEasingFunction_stub(this Microsoft.UI.Composition.Compositor sender, System.Numerics.Vector2 controlPoint1, System.Numerics.Vector2 controlPoint2)
    {
        try
        {
            return sender.CreateCubicBezierEasingFunction(controlPoint1, controlPoint2);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.ExpressionAnimation CreateExpressionAnimation_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateExpressionAnimation();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.ColorKeyFrameAnimation CreateColorKeyFrameAnimation_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateColorKeyFrameAnimation();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateScalarKeyFrameAnimation();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionColorBrush CreateColorBrush_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateColorBrush();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.ExpressionAnimation CreateExpressionAnimation_stub(this Microsoft.UI.Composition.Compositor sender, string expression)
    {
        try
        {
            return sender.CreateExpressionAnimation(expression);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.PathKeyFrameAnimation CreatePathKeyFrameAnimation_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreatePathKeyFrameAnimation();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionPathGeometry CreatePathGeometry_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreatePathGeometry();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionPropertySet CreatePropertySet_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreatePropertySet();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionContainerShape CreateContainerShape_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateContainerShape();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionColorGradientStop CreateColorGradientStop_stub(this Microsoft.UI.Composition.Compositor sender, float offset, Windows.UI.Color color)
    {
        try
        {
            return sender.CreateColorGradientStop(offset, color);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionLinearGradientBrush CreateLinearGradientBrush_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateLinearGradientBrush();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.AnimationController CreateAnimationController_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateAnimationController();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.InsetClip CreateInsetClip_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateInsetClip();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.BooleanKeyFrameAnimation CreateBooleanKeyFrameAnimation_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateBooleanKeyFrameAnimation();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionEllipseGeometry CreateEllipseGeometry_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateEllipseGeometry();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionRadialGradientBrush CreateRadialGradientBrush_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateRadialGradientBrush();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionRoundedRectangleGeometry CreateRoundedRectangleGeometry_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateRoundedRectangleGeometry();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionScopedBatch CreateScopedBatch_stub(this Microsoft.UI.Composition.Compositor sender, Microsoft.UI.Composition.CompositionBatchTypes batchType)
    {
        try
        {
            return sender.CreateScopedBatch(batchType);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.Vector3KeyFrameAnimation CreateVector3KeyFrameAnimation_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateVector3KeyFrameAnimation();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.DropShadow CreateDropShadow_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateDropShadow();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionGeometricClip CreateGeometricClip_stub(this Microsoft.UI.Composition.Compositor sender, Microsoft.UI.Composition.CompositionGeometry geometry)
    {
        try
        {
            return sender.CreateGeometricClip(geometry);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionGeometricClip CreateGeometricClip_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateGeometricClip();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionRectangleGeometry CreateRectangleGeometry_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateRectangleGeometry();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionMaskBrush CreateMaskBrush_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateMaskBrush();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionSurfaceBrush CreateSurfaceBrush_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateSurfaceBrush();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionSpriteShape CreateSpriteShape_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateSpriteShape();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.LinearEasingFunction CreateLinearEasingFunction_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateLinearEasingFunction();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionEffectFactory CreateEffectFactory_stub(this Microsoft.UI.Composition.Compositor sender, Windows.Graphics.Effects.IGraphicsEffect graphicsEffect, System.Collections.Generic.IEnumerable<string> animatableProperties)
    {
        try
        {
            return sender.CreateEffectFactory(graphicsEffect, animatableProperties);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.SpringVector3NaturalMotionAnimation CreateSpringVector3Animation_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateSpringVector3Animation();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.SpringScalarNaturalMotionAnimation CreateSpringScalarAnimation_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateSpringScalarAnimation();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionViewBox CreateViewBox_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateViewBox();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.InsetClip CreateInsetClip_stub(this Microsoft.UI.Composition.Compositor sender, float leftInset, float topInset, float rightInset, float bottomInset)
    {
        try
        {
            return sender.CreateInsetClip(leftInset, topInset, rightInset, bottomInset);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionLineGeometry CreateLineGeometry_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateLineGeometry();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionBackdropBrush CreateBackdropBrush_stub(this Microsoft.UI.Composition.Compositor sender)
    {
        try
        {
            return sender.CreateBackdropBrush();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.RectangleClip CreateRectangleClip_stub(this Microsoft.UI.Composition.Compositor sender, float left, float top, float right, float bottom, System.Numerics.Vector2 topLeftRadius, System.Numerics.Vector2 topRightRadius, System.Numerics.Vector2 bottomRightRadius, System.Numerics.Vector2 bottomLeftRadius)
    {
        try
        {
            return sender.CreateRectangleClip(left, top, right, bottom, topLeftRadius, topRightRadius, bottomRightRadius, bottomLeftRadius);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Vector2KeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.Vector2KeyFrameAnimation sender, float normalizedProgressKey, System.Numerics.Vector2 value, Microsoft.UI.Composition.CompositionEasingFunction easingFunction)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, value, easingFunction);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.Vector2KeyFrameAnimation sender, float normalizedProgressKey, System.Numerics.Vector2 value)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CompositionEffectFactory_stub
{
    public static Microsoft.UI.Composition.CompositionEffectBrush CreateBrush_stub(this Microsoft.UI.Composition.CompositionEffectFactory sender)
    {
        try
        {
            return sender.CreateBrush();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CompositionEffectBrush_stub
{
    public static void SetSourceParameter_stub(this Microsoft.UI.Composition.CompositionEffectBrush sender, string name, Microsoft.UI.Composition.CompositionBrush source)
    {
        try
        {
            sender.SetSourceParameter(name, source);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class AnimationController_stub
{
    public static void Pause_stub(this Microsoft.UI.Composition.AnimationController sender)
    {
        try
        {
            sender.Pause();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Resume_stub(this Microsoft.UI.Composition.AnimationController sender)
    {
        try
        {
            sender.Resume();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VisualCollection_stub
{
    public static void InsertAtTop_stub(this Microsoft.UI.Composition.VisualCollection sender, Microsoft.UI.Composition.Visual newChild)
    {
        try
        {
            sender.InsertAtTop(newChild);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InsertAtBottom_stub(this Microsoft.UI.Composition.VisualCollection sender, Microsoft.UI.Composition.Visual newChild)
    {
        try
        {
            sender.InsertAtBottom(newChild);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RemoveAll_stub(this Microsoft.UI.Composition.VisualCollection sender)
    {
        try
        {
            sender.RemoveAll();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CompositionPropertySet_stub
{
    public static void InsertScalar_stub(this Microsoft.UI.Composition.CompositionPropertySet sender, string propertyName, float value)
    {
        try
        {
            sender.InsertScalar(propertyName, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InsertVector4_stub(this Microsoft.UI.Composition.CompositionPropertySet sender, string propertyName, System.Numerics.Vector4 value)
    {
        try
        {
            sender.InsertVector4(propertyName, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InsertVector3_stub(this Microsoft.UI.Composition.CompositionPropertySet sender, string propertyName, System.Numerics.Vector3 value)
    {
        try
        {
            sender.InsertVector3(propertyName, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InsertColor_stub(this Microsoft.UI.Composition.CompositionPropertySet sender, string propertyName, Windows.UI.Color value)
    {
        try
        {
            sender.InsertColor(propertyName, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionGetValueStatus TryGetVector3_stub(this Microsoft.UI.Composition.CompositionPropertySet sender, string propertyName, out System.Numerics.Vector3 value)
    {
        try
        {
            return sender.TryGetVector3(propertyName, out value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InsertBoolean_stub(this Microsoft.UI.Composition.CompositionPropertySet sender, string propertyName, bool value)
    {
        try
        {
            sender.InsertBoolean(propertyName, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ColorKeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.ColorKeyFrameAnimation sender, float normalizedProgressKey, Windows.UI.Color value, Microsoft.UI.Composition.CompositionEasingFunction easingFunction)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, value, easingFunction);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.ColorKeyFrameAnimation sender, float normalizedProgressKey, Windows.UI.Color value)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ScalarKeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.ScalarKeyFrameAnimation sender, float normalizedProgressKey, float value, Microsoft.UI.Composition.CompositionEasingFunction easingFunction)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, value, easingFunction);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.ScalarKeyFrameAnimation sender, float normalizedProgressKey, float value)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class PathKeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.PathKeyFrameAnimation sender, float normalizedProgressKey, Microsoft.UI.Composition.CompositionPath path, Microsoft.UI.Composition.CompositionEasingFunction easingFunction)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, path, easingFunction);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.PathKeyFrameAnimation sender, float normalizedProgressKey, Microsoft.UI.Composition.CompositionPath path)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, path);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class KeyFrameAnimation_stub
{
    public static void InsertExpressionKeyFrame_stub(this Microsoft.UI.Composition.KeyFrameAnimation sender, float normalizedProgressKey, string value, Microsoft.UI.Composition.CompositionEasingFunction easingFunction)
    {
        try
        {
            sender.InsertExpressionKeyFrame(normalizedProgressKey, value, easingFunction);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class BooleanKeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.BooleanKeyFrameAnimation sender, float normalizedProgressKey, bool value)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Vector3KeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.Vector3KeyFrameAnimation sender, float normalizedProgressKey, System.Numerics.Vector3 value)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InsertKeyFrame_stub(this Microsoft.UI.Composition.Vector3KeyFrameAnimation sender, float normalizedProgressKey, System.Numerics.Vector3 value, Microsoft.UI.Composition.CompositionEasingFunction easingFunction)
    {
        try
        {
            sender.InsertKeyFrame(normalizedProgressKey, value, easingFunction);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CompositionScopedBatch_stub
{
    public static void End_stub(this Microsoft.UI.Composition.CompositionScopedBatch sender)
    {
        try
        {
            sender.End();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CompositionCapabilities_stub
{
    public static Microsoft.UI.Composition.CompositionCapabilities GetForCurrentView_stub()
    {
        try
        {
            return Microsoft.UI.Composition.CompositionCapabilities.GetForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool AreEffectsFast_stub(this Microsoft.UI.Composition.CompositionCapabilities sender)
    {
        try
        {
            return sender.AreEffectsFast();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class InteractionTracker_stub
{
    public static int TryUpdatePosition_stub(this Microsoft.UI.Composition.Interactions.InteractionTracker sender, System.Numerics.Vector3 value)
    {
        try
        {
            return sender.TryUpdatePosition(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int TryUpdatePositionWithAnimation_stub(this Microsoft.UI.Composition.Interactions.InteractionTracker sender, Microsoft.UI.Composition.CompositionAnimation animation)
    {
        try
        {
            return sender.TryUpdatePositionWithAnimation(animation);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.Interactions.InteractionTracker CreateWithOwner_stub(Microsoft.UI.Composition.Compositor compositor, Microsoft.UI.Composition.Interactions.IInteractionTrackerOwner owner)
    {
        try
        {
            return Microsoft.UI.Composition.Interactions.InteractionTracker.CreateWithOwner(compositor, owner);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ConfigurePositionXInertiaModifiers_stub(this Microsoft.UI.Composition.Interactions.InteractionTracker sender, System.Collections.Generic.IEnumerable<Microsoft.UI.Composition.Interactions.InteractionTrackerInertiaModifier> modifiers)
    {
        try
        {
            sender.ConfigurePositionXInertiaModifiers(modifiers);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VisualInteractionSource_stub
{
    public static Microsoft.UI.Composition.Interactions.VisualInteractionSource Create_stub(Microsoft.UI.Composition.Visual source)
    {
        try
        {
            return Microsoft.UI.Composition.Interactions.VisualInteractionSource.Create(source);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void TryRedirectForManipulation_stub(this Microsoft.UI.Composition.Interactions.VisualInteractionSource sender, Windows.UI.Input.PointerPoint pointerPoint)
    {
        try
        {
            sender.TryRedirectForManipulation(pointerPoint);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CompositionInteractionSourceCollection_stub
{
    public static void Add_stub(this Microsoft.UI.Composition.Interactions.CompositionInteractionSourceCollection sender, Microsoft.UI.Composition.Interactions.ICompositionInteractionSource value)
    {
        try
        {
            sender.Add(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class InteractionTrackerInertiaRestingValue_stub
{
    public static Microsoft.UI.Composition.Interactions.InteractionTrackerInertiaRestingValue Create_stub(Microsoft.UI.Composition.Compositor compositor)
    {
        try
        {
            return Microsoft.UI.Composition.Interactions.InteractionTrackerInertiaRestingValue.Create(compositor);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CoreDispatcher_stub
{
    public static async Task RunAsync_stub(this Windows.UI.Core.CoreDispatcher sender, Windows.UI.Core.CoreDispatcherPriority priority, Windows.UI.Core.DispatchedHandler agileCallback)
    {
        try
        {
            await sender.RunAsync(priority, agileCallback);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task RunIdleAsync_stub(this Windows.UI.Core.CoreDispatcher sender, Windows.UI.Core.IdleDispatchedHandler agileCallback)
    {
        try
        {
            await sender.RunIdleAsync(agileCallback);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CoreWindow_stub
{
    public static Windows.UI.Core.CoreVirtualKeyStates GetKeyState_stub(this Windows.UI.Core.CoreWindow sender, Windows.System.VirtualKey virtualKey)
    {
        try
        {
            return sender.GetKeyState(virtualKey);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Core.CoreVirtualKeyStates GetAsyncKeyState_stub(this Windows.UI.Core.CoreWindow sender, Windows.System.VirtualKey virtualKey)
    {
        try
        {
            return sender.GetAsyncKeyState(virtualKey);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class SystemNavigationManager_stub
{
    public static Windows.UI.Core.SystemNavigationManager GetForCurrentView_stub()
    {
        try
        {
            return Windows.UI.Core.SystemNavigationManager.GetForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class SystemNavigationManagerPreview_stub
{
    public static Windows.UI.Core.Preview.SystemNavigationManagerPreview GetForCurrentView_stub()
    {
        try
        {
            return Windows.UI.Core.Preview.SystemNavigationManagerPreview.GetForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class GestureRecognizer_stub
{
    public static void CompleteGesture_stub(this Windows.UI.Input.GestureRecognizer sender)
    {
        try
        {
            sender.CompleteGesture();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ProcessDownEvent_stub(this Windows.UI.Input.GestureRecognizer sender, Windows.UI.Input.PointerPoint value)
    {
        try
        {
            sender.ProcessDownEvent(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ProcessUpEvent_stub(this Windows.UI.Input.GestureRecognizer sender, Windows.UI.Input.PointerPoint value)
    {
        try
        {
            sender.ProcessUpEvent(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class BadgeUpdateManager_stub
{
    public static Windows.UI.Notifications.BadgeUpdater CreateBadgeUpdaterForApplication_stub(string applicationId)
    {
        try
        {
            return Windows.UI.Notifications.BadgeUpdateManager.CreateBadgeUpdaterForApplication(applicationId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.Data.Xml.Dom.XmlDocument GetTemplateContent_stub(Windows.UI.Notifications.BadgeTemplateType type)
    {
        try
        {
            return Windows.UI.Notifications.BadgeUpdateManager.GetTemplateContent(type);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class BadgeUpdater_stub
{
    public static void Clear_stub(this Windows.UI.Notifications.BadgeUpdater sender)
    {
        try
        {
            sender.Clear();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Update_stub(this Windows.UI.Notifications.BadgeUpdater sender, Windows.UI.Notifications.BadgeNotification notification)
    {
        try
        {
            sender.Update(notification);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ToastNotificationHistory_stub
{
    public static void RemoveGroup_stub(this Windows.UI.Notifications.ToastNotificationHistory sender, string group)
    {
        try
        {
            sender.RemoveGroup(group);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IReadOnlyList<Windows.UI.Notifications.ToastNotification> GetHistory_stub(this Windows.UI.Notifications.ToastNotificationHistory sender)
    {
        try
        {
            return sender.GetHistory();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Remove_stub(this Windows.UI.Notifications.ToastNotificationHistory sender, string tag, string group)
    {
        try
        {
            sender.Remove(tag, group);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Clear_stub(this Windows.UI.Notifications.ToastNotificationHistory sender, string applicationId)
    {
        try
        {
            sender.Clear(applicationId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ToastNotificationManagerForUser_stub
{
    public static async Task<Windows.UI.Notifications.ToastNotifier> GetToastNotifierForToastCollectionIdAsync_stub(this Windows.UI.Notifications.ToastNotificationManagerForUser sender, string collectionId)
    {
        try
        {
            return await sender.GetToastNotifierForToastCollectionIdAsync(collectionId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Notifications.ToastCollectionManager GetToastCollectionManager_stub(this Windows.UI.Notifications.ToastNotificationManagerForUser sender)
    {
        try
        {
            return sender.GetToastCollectionManager();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.UI.Notifications.ToastNotificationHistory> GetHistoryForToastCollectionIdAsync_stub(this Windows.UI.Notifications.ToastNotificationManagerForUser sender, string collectionId)
    {
        try
        {
            return await sender.GetHistoryForToastCollectionIdAsync(collectionId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ToastNotificationManager_stub
{
    public static Windows.UI.Notifications.ToastNotificationManagerForUser GetDefault_stub()
    {
        try
        {
            return Windows.UI.Notifications.ToastNotificationManager.GetDefault();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Notifications.ToastNotifier CreateToastNotifier_stub(string applicationId)
    {
        try
        {
            return Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier(applicationId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ToastNotifier_stub
{
    public static void Show_stub(this Windows.UI.Notifications.ToastNotifier sender, Windows.UI.Notifications.ToastNotification notification)
    {
        try
        {
            sender.Show(notification);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ToastCollectionManager_stub
{
    public static async Task SaveToastCollectionAsync_stub(this Windows.UI.Notifications.ToastCollectionManager sender, Windows.UI.Notifications.ToastCollection collection)
    {
        try
        {
            await sender.SaveToastCollectionAsync(collection);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class TileUpdater_stub
{
    public static void Clear_stub(this Windows.UI.Notifications.TileUpdater sender)
    {
        try
        {
            sender.Clear();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class TileUpdateManager_stub
{
    public static Windows.UI.Notifications.TileUpdater CreateTileUpdaterForApplication_stub(string applicationId)
    {
        try
        {
            return Windows.UI.Notifications.TileUpdateManager.CreateTileUpdaterForApplication(applicationId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class JumpList_stub
{
    public static bool IsSupported_stub()
    {
        try
        {
            return Windows.UI.StartScreen.JumpList.IsSupported();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.UI.StartScreen.JumpList> LoadCurrentAsync_stub()
    {
        try
        {
            return await Windows.UI.StartScreen.JumpList.LoadCurrentAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task SaveAsync_stub(this Windows.UI.StartScreen.JumpList sender)
    {
        try
        {
            await sender.SaveAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class JumpListItem_stub
{
    public static Windows.UI.StartScreen.JumpListItem CreateWithArguments_stub(string arguments, string displayName)
    {
        try
        {
            return Windows.UI.StartScreen.JumpListItem.CreateWithArguments(arguments, displayName);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ITextRange_stub
{
    public static void SetRange_stub(this Windows.UI.Text.ITextRange sender, int startPosition, int endPosition)
    {
        try
        {
            sender.SetRange(startPosition, endPosition);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void GetText_stub(this Windows.UI.Text.ITextRange sender, Windows.UI.Text.TextGetOptions options, out string value)
    {
        try
        {
            sender.GetText(options, out value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Text.ITextRange GetClone_stub(this Windows.UI.Text.ITextRange sender)
    {
        try
        {
            return sender.GetClone();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void GetRect_stub(this Windows.UI.Text.ITextRange sender, Windows.UI.Text.PointOptions options, out Windows.Foundation.Rect rect, out int hit)
    {
        try
        {
            sender.GetRect(options, out rect, out hit);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetText_stub(this Windows.UI.Text.ITextRange sender, Windows.UI.Text.TextSetOptions options, string value)
    {
        try
        {
            sender.SetText(options, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int StartOf_stub(this Windows.UI.Text.ITextRange sender, Windows.UI.Text.TextRangeUnit unit, bool extend)
    {
        try
        {
            return sender.StartOf(unit, extend);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Collapse_stub(this Windows.UI.Text.ITextRange sender, bool value)
    {
        try
        {
            sender.Collapse(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int Expand_stub(this Windows.UI.Text.ITextRange sender, Windows.UI.Text.TextRangeUnit unit)
    {
        try
        {
            return sender.Expand(unit);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int MoveEnd_stub(this Windows.UI.Text.ITextRange sender, Windows.UI.Text.TextRangeUnit unit, int count)
    {
        try
        {
            return sender.MoveEnd(unit, count);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Cut_stub(this Windows.UI.Text.ITextRange sender)
    {
        try
        {
            sender.Cut();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Copy_stub(this Windows.UI.Text.ITextRange sender)
    {
        try
        {
            sender.Copy();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int Move_stub(this Windows.UI.Text.ITextRange sender, Windows.UI.Text.TextRangeUnit unit, int count)
    {
        try
        {
            return sender.Move(unit, count);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool CanPaste_stub(this Windows.UI.Text.ITextRange sender, int format)
    {
        try
        {
            return sender.CanPaste(format);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Paste_stub(this Windows.UI.Text.ITextRange sender, int format)
    {
        try
        {
            sender.Paste(format);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int Delete_stub(this Windows.UI.Text.ITextRange sender, Windows.UI.Text.TextRangeUnit unit, int count)
    {
        try
        {
            return sender.Delete(unit, count);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int EndOf_stub(this Windows.UI.Text.ITextRange sender, Windows.UI.Text.TextRangeUnit unit, bool extend)
    {
        try
        {
            return sender.EndOf(unit, extend);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int FindText_stub(this Windows.UI.Text.ITextRange sender, string value, int scanLength, Windows.UI.Text.FindOptions options)
    {
        try
        {
            return sender.FindText(value, scanLength, options);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int MoveStart_stub(this Windows.UI.Text.ITextRange sender, Windows.UI.Text.TextRangeUnit unit, int count)
    {
        try
        {
            return sender.MoveStart(unit, count);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ITextDocument_stub
{
    public static void GetText_stub(this Windows.UI.Text.ITextDocument sender, Windows.UI.Text.TextGetOptions options, out string value)
    {
        try
        {
            sender.GetText(options, out value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Text.ITextCharacterFormat GetDefaultCharacterFormat_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            return sender.GetDefaultCharacterFormat();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Text.ITextRange GetRange_stub(this Windows.UI.Text.ITextDocument sender, int startPosition, int endPosition)
    {
        try
        {
            return sender.GetRange(startPosition, endPosition);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool CanRedo_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            return sender.CanRedo();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Redo_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            sender.Redo();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool CanUndo_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            return sender.CanUndo();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool CanCopy_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            return sender.CanCopy();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool CanPaste_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            return sender.CanPaste();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int BatchDisplayUpdates_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            return sender.BatchDisplayUpdates();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int ApplyDisplayUpdates_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            return sender.ApplyDisplayUpdates();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Undo_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            sender.Undo();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void BeginUndoGroup_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            sender.BeginUndoGroup();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void EndUndoGroup_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            sender.EndUndoGroup();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetText_stub(this Windows.UI.Text.ITextDocument sender, Windows.UI.Text.TextSetOptions options, string value)
    {
        try
        {
            sender.SetText(options, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Text.ITextParagraphFormat GetDefaultParagraphFormat_stub(this Windows.UI.Text.ITextDocument sender)
    {
        try
        {
            return sender.GetDefaultParagraphFormat();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void LoadFromStream_stub(this Windows.UI.Text.ITextDocument sender, Windows.UI.Text.TextSetOptions options, Windows.Storage.Streams.IRandomAccessStream value)
    {
        try
        {
            sender.LoadFromStream(options, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ITextParagraphFormat_stub
{
    public static void SetIndents_stub(this Windows.UI.Text.ITextParagraphFormat sender, float start, float left, float right)
    {
        try
        {
            sender.SetIndents(start, left, right);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ApplicationView_stub
{
    public static Windows.UI.ViewManagement.ApplicationView GetForCurrentView_stub()
    {
        try
        {
            return Windows.UI.ViewManagement.ApplicationView.GetForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool IsViewModeSupported_stub(this Windows.UI.ViewManagement.ApplicationView sender, Windows.UI.ViewManagement.ApplicationViewMode viewMode)
    {
        try
        {
            return sender.IsViewModeSupported(viewMode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool TryEnterFullScreenMode_stub(this Windows.UI.ViewManagement.ApplicationView sender)
    {
        try
        {
            return sender.TryEnterFullScreenMode();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ExitFullScreenMode_stub(this Windows.UI.ViewManagement.ApplicationView sender)
    {
        try
        {
            sender.ExitFullScreenMode();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool TryResizeView_stub(this Windows.UI.ViewManagement.ApplicationView sender, Windows.Foundation.Size value)
    {
        try
        {
            return sender.TryResizeView(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int GetApplicationViewIdForWindow_stub(Windows.UI.Core.ICoreWindow window)
    {
        try
        {
            return Windows.UI.ViewManagement.ApplicationView.GetApplicationViewIdForWindow(window);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetPreferredMinSize_stub(this Windows.UI.ViewManagement.ApplicationView sender, Windows.Foundation.Size minSize)
    {
        try
        {
            sender.SetPreferredMinSize(minSize);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<bool> TryConsolidateAsync_stub(this Windows.UI.ViewManagement.ApplicationView sender)
    {
        try
        {
            return await sender.TryConsolidateAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ApplicationViewSwitcher_stub
{
    public static async Task SwitchAsync_stub(int toViewId, int fromViewId, Windows.UI.ViewManagement.ApplicationViewSwitchingOptions options)
    {
        try
        {
            await Windows.UI.ViewManagement.ApplicationViewSwitcher.SwitchAsync(toViewId, fromViewId, options);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task SwitchAsync_stub(int viewId)
    {
        try
        {
            await Windows.UI.ViewManagement.ApplicationViewSwitcher.SwitchAsync(viewId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<bool> TryShowAsStandaloneAsync_stub(int viewId)
    {
        try
        {
            return await Windows.UI.ViewManagement.ApplicationViewSwitcher.TryShowAsStandaloneAsync(viewId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<bool> TryShowAsViewModeAsync_stub(int viewId, Windows.UI.ViewManagement.ApplicationViewMode viewMode, Windows.UI.ViewManagement.ViewModePreferences viewModePreferences)
    {
        try
        {
            return await Windows.UI.ViewManagement.ApplicationViewSwitcher.TryShowAsViewModeAsync(viewId, viewMode, viewModePreferences);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class UIViewSettings_stub
{
    public static Windows.UI.ViewManagement.UIViewSettings GetForCurrentView_stub()
    {
        try
        {
            return Windows.UI.ViewManagement.UIViewSettings.GetForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class UISettings_stub
{
    public static Windows.UI.Color GetColorValue_stub(this Windows.UI.ViewManagement.UISettings sender, Windows.UI.ViewManagement.UIColorType desiredColor)
    {
        try
        {
            return sender.GetColorValue(desiredColor);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ViewModePreferences_stub
{
    public static Windows.UI.ViewManagement.ViewModePreferences CreateDefault_stub(Windows.UI.ViewManagement.ApplicationViewMode mode)
    {
        try
        {
            return Windows.UI.ViewManagement.ViewModePreferences.CreateDefault(mode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class UIElement_stub
{
    public static bool CapturePointer_stub(this Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.Input.Pointer value)
    {
        try
        {
            return sender.CapturePointer(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ReleasePointerCapture_stub(this Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.Input.Pointer value)
    {
        try
        {
            sender.ReleasePointerCapture(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Measure_stub(this Microsoft.UI.Xaml.UIElement sender, Windows.Foundation.Size availableSize)
    {
        try
        {
            sender.Measure(availableSize);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddHandler_stub(this Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.RoutedEvent routedEvent, object handler, bool handledEventsToo)
    {
        try
        {
            sender.AddHandler(routedEvent, handler, handledEventsToo);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RemoveHandler_stub(this Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.RoutedEvent routedEvent, object handler)
    {
        try
        {
            sender.RemoveHandler(routedEvent, handler);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<Windows.ApplicationModel.DataTransfer.DataPackageOperation> StartDragAsync_stub(this Microsoft.UI.Xaml.UIElement sender, Windows.UI.Input.PointerPoint pointerPoint)
    {
        try
        {
            return await sender.StartDragAsync(pointerPoint);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Arrange_stub(this Microsoft.UI.Xaml.UIElement sender, Windows.Foundation.Rect finalRect)
    {
        try
        {
            sender.Arrange(finalRect);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Media.GeneralTransform TransformToVisual_stub(this Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.UIElement visual)
    {
        try
        {
            return sender.TransformToVisual(visual);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void UpdateLayout_stub(this Microsoft.UI.Xaml.UIElement sender)
    {
        try
        {
            sender.UpdateLayout();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InvalidateArrange_stub(this Microsoft.UI.Xaml.UIElement sender)
    {
        try
        {
            sender.InvalidateArrange();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool CancelDirectManipulations_stub(this Microsoft.UI.Xaml.UIElement sender)
    {
        try
        {
            return sender.CancelDirectManipulations();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void InvalidateMeasure_stub(this Microsoft.UI.Xaml.UIElement sender)
    {
        try
        {
            sender.InvalidateMeasure();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ReleasePointerCaptures_stub(this Microsoft.UI.Xaml.UIElement sender)
    {
        try
        {
            sender.ReleasePointerCaptures();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void StartBringIntoView_stub(this Microsoft.UI.Xaml.UIElement sender)
    {
        try
        {
            sender.StartBringIntoView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FrameworkElement_stub
{
    public static object FindName_stub(this Microsoft.UI.Xaml.FrameworkElement sender, string name)
    {
        try
        {
            return sender.FindName(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Data.BindingExpression GetBindingExpression_stub(this Microsoft.UI.Xaml.FrameworkElement sender, Microsoft.UI.Xaml.DependencyProperty dp)
    {
        try
        {
            return sender.GetBindingExpression(dp);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetBinding_stub(this Microsoft.UI.Xaml.FrameworkElement sender, Microsoft.UI.Xaml.DependencyProperty dp, Microsoft.UI.Xaml.Data.BindingBase binding)
    {
        try
        {
            sender.SetBinding(dp, binding);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DependencyObject_stub
{
    public static void ClearValue_stub(this Microsoft.UI.Xaml.DependencyObject sender, Microsoft.UI.Xaml.DependencyProperty dp)
    {
        try
        {
            sender.ClearValue(dp);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static object GetValue_stub(this Microsoft.UI.Xaml.DependencyObject sender, Microsoft.UI.Xaml.DependencyProperty dp)
    {
        try
        {
            return sender.GetValue(dp);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetValue_stub(this Microsoft.UI.Xaml.DependencyObject sender, Microsoft.UI.Xaml.DependencyProperty dp, object value)
    {
        try
        {
            sender.SetValue(dp, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static long RegisterPropertyChangedCallback_stub(this Microsoft.UI.Xaml.DependencyObject sender, Microsoft.UI.Xaml.DependencyProperty dp, Microsoft.UI.Xaml.DependencyPropertyChangedCallback callback)
    {
        try
        {
            return sender.RegisterPropertyChangedCallback(dp, callback);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void UnregisterPropertyChangedCallback_stub(this Microsoft.UI.Xaml.DependencyObject sender, Microsoft.UI.Xaml.DependencyProperty dp, long token)
    {
        try
        {
            sender.UnregisterPropertyChangedCallback(dp, token);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DependencyProperty_stub
{
    public static Microsoft.UI.Xaml.DependencyProperty Register_stub(string name, System.Type propertyType, System.Type ownerType, Microsoft.UI.Xaml.PropertyMetadata typeMetadata)
    {
        try
        {
            return Microsoft.UI.Xaml.DependencyProperty.Register(name, propertyType, ownerType, typeMetadata);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.DependencyProperty RegisterAttached_stub(string name, System.Type propertyType, System.Type ownerType, Microsoft.UI.Xaml.PropertyMetadata defaultMetadata)
    {
        try
        {
            return Microsoft.UI.Xaml.DependencyProperty.RegisterAttached(name, propertyType, ownerType, defaultMetadata);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VisualStateManager_stub
{
    public static bool GoToState_stub(Microsoft.UI.Xaml.Controls.Control control, string stateName, bool useTransitions)
    {
        try
        {
            return Microsoft.UI.Xaml.VisualStateManager.GoToState(control, stateName, useTransitions);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DispatcherTimer_stub
{
    public static void Stop_stub(this Microsoft.UI.Xaml.DispatcherTimer sender)
    {
        try
        {
            sender.Stop();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Start_stub(this Microsoft.UI.Xaml.DispatcherTimer sender)
    {
        try
        {
            sender.Start();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Window_stub
{
    public static void SetTitleBar_stub(this Microsoft.UI.Xaml.Window sender, Microsoft.UI.Xaml.UIElement value)
    {
        try
        {
            sender.SetTitleBar(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Activate_stub(this Microsoft.UI.Xaml.Window sender)
    {
        try
        {
            sender.Activate();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Close_stub(this Microsoft.UI.Xaml.Window sender)
    {
        try
        {
            sender.Close();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DragStartingEventArgs_stub
{
    public static Microsoft.UI.Xaml.DragOperationDeferral GetDeferral_stub(this Microsoft.UI.Xaml.DragStartingEventArgs sender)
    {
        try
        {
            return sender.GetDeferral();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DragUI_stub
{
    public static void SetContentFromDataPackage_stub(this Microsoft.UI.Xaml.DragUI sender)
    {
        try
        {
            sender.SetContentFromDataPackage();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DragOperationDeferral_stub
{
    public static void Complete_stub(this Microsoft.UI.Xaml.DragOperationDeferral sender)
    {
        try
        {
            sender.Complete();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class AutomationProperties_stub
{
    public static void SetName_stub(Microsoft.UI.Xaml.DependencyObject element, string value)
    {
        try
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetName_stub(Microsoft.UI.Xaml.DependencyObject element)
    {
        try
        {
            return Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetAutomationControlType_stub(Microsoft.UI.Xaml.UIElement element, Microsoft.UI.Xaml.Automation.Peers.AutomationControlType value)
    {
        try
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationControlType(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetPositionInSet_stub(Microsoft.UI.Xaml.DependencyObject element, int value)
    {
        try
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetPositionInSet(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetSizeOfSet_stub(Microsoft.UI.Xaml.DependencyObject element, int value)
    {
        try
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetSizeOfSet(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IList<Microsoft.UI.Xaml.UIElement> GetControlledPeers_stub(Microsoft.UI.Xaml.DependencyObject element)
    {
        try
        {
            return Microsoft.UI.Xaml.Automation.AutomationProperties.GetControlledPeers(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Automation.Peers.AccessibilityView GetAccessibilityView_stub(Microsoft.UI.Xaml.DependencyObject element)
    {
        try
        {
            return Microsoft.UI.Xaml.Automation.AutomationProperties.GetAccessibilityView(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class AutomationPeer_stub
{
    public static bool ListenerExists_stub(Microsoft.UI.Xaml.Automation.Peers.AutomationEvents eventId)
    {
        try
        {
            return Microsoft.UI.Xaml.Automation.Peers.AutomationPeer.ListenerExists(eventId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static object GetPattern_stub(this Microsoft.UI.Xaml.Automation.Peers.AutomationPeer sender, Microsoft.UI.Xaml.Automation.Peers.PatternInterface patternInterface)
    {
        try
        {
            return sender.GetPattern(patternInterface);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RaiseAutomationEvent_stub(this Microsoft.UI.Xaml.Automation.Peers.AutomationPeer sender, Microsoft.UI.Xaml.Automation.Peers.AutomationEvents eventId)
    {
        try
        {
            sender.RaiseAutomationEvent(eventId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RaisePropertyChangedEvent_stub(this Microsoft.UI.Xaml.Automation.Peers.AutomationPeer sender, Microsoft.UI.Xaml.Automation.AutomationProperty automationProperty, object oldValue, object newValue)
    {
        try
        {
            sender.RaisePropertyChangedEvent(automationProperty, oldValue, newValue);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FrameworkElementAutomationPeer_stub
{
    public static Microsoft.UI.Xaml.Automation.Peers.AutomationPeer CreatePeerForElement_stub(Microsoft.UI.Xaml.UIElement element)
    {
        try
        {
            return Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Automation.Peers.AutomationPeer FromElement_stub(Microsoft.UI.Xaml.UIElement element)
    {
        try
        {
            return Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class IInvokeProvider_stub
{
    public static void Invoke_stub(this Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider sender)
    {
        try
        {
            sender.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Grid_stub
{
    public static void SetColumn_stub(Microsoft.UI.Xaml.FrameworkElement element, int value)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.Grid.SetColumn(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetColumnSpan_stub(Microsoft.UI.Xaml.FrameworkElement element, int value)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetRow_stub(Microsoft.UI.Xaml.FrameworkElement element, int value)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.Grid.SetRow(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetRowSpan_stub(Microsoft.UI.Xaml.FrameworkElement element, int value)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.Grid.SetRowSpan(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int GetRow_stub(Microsoft.UI.Xaml.FrameworkElement element)
    {
        try
        {
            return Microsoft.UI.Xaml.Controls.Grid.GetRow(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ListViewBase_stub
{
    public static void ScrollIntoView_stub(this Microsoft.UI.Xaml.Controls.ListViewBase sender, object item, Microsoft.UI.Xaml.Controls.ScrollIntoViewAlignment alignment)
    {
        try
        {
            sender.ScrollIntoView(item, alignment);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ScrollIntoView_stub(this Microsoft.UI.Xaml.Controls.ListViewBase sender, object item)
    {
        try
        {
            sender.ScrollIntoView(item);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SelectAll_stub(this Microsoft.UI.Xaml.Controls.ListViewBase sender)
    {
        try
        {
            sender.SelectAll();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ItemsControl_stub
{
    public static Microsoft.UI.Xaml.DependencyObject ContainerFromItem_stub(this Microsoft.UI.Xaml.Controls.ItemsControl sender, object item)
    {
        try
        {
            return sender.ContainerFromItem(item);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.DependencyObject ContainerFromIndex_stub(this Microsoft.UI.Xaml.Controls.ItemsControl sender, int index)
    {
        try
        {
            return sender.ContainerFromIndex(index);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static object ItemFromContainer_stub(this Microsoft.UI.Xaml.Controls.ItemsControl sender, Microsoft.UI.Xaml.DependencyObject container)
    {
        try
        {
            return sender.ItemFromContainer(container);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.DependencyObject GroupHeaderContainerFromItemContainer_stub(this Microsoft.UI.Xaml.Controls.ItemsControl sender, Microsoft.UI.Xaml.DependencyObject itemContainer)
    {
        try
        {
            return sender.GroupHeaderContainerFromItemContainer(itemContainer);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int IndexFromContainer_stub(this Microsoft.UI.Xaml.Controls.ItemsControl sender, Microsoft.UI.Xaml.DependencyObject container)
    {
        try
        {
            return sender.IndexFromContainer(container);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ScrollViewer_stub
{
    public static bool ChangeView_stub(this Microsoft.UI.Xaml.Controls.ScrollViewer sender, double? horizontalOffset, double? verticalOffset, float? zoomFactor, bool disableAnimation)
    {
        try
        {
            return sender.ChangeView(horizontalOffset, verticalOffset, zoomFactor, disableAnimation);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool ChangeView_stub(this Microsoft.UI.Xaml.Controls.ScrollViewer sender, double? horizontalOffset, double? verticalOffset, float? zoomFactor)
    {
        try
        {
            return sender.ChangeView(horizontalOffset, verticalOffset, zoomFactor);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetVerticalScrollBarVisibility_stub(Microsoft.UI.Xaml.DependencyObject element, Microsoft.UI.Xaml.Controls.ScrollBarVisibility verticalScrollBarVisibility)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.ScrollViewer.SetVerticalScrollBarVisibility(element, verticalScrollBarVisibility);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetVerticalScrollMode_stub(Microsoft.UI.Xaml.DependencyObject element, Microsoft.UI.Xaml.Controls.ScrollMode verticalScrollMode)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.ScrollViewer.SetVerticalScrollMode(element, verticalScrollMode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetIsVerticalRailEnabled_stub(Microsoft.UI.Xaml.DependencyObject element, bool isVerticalRailEnabled)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.ScrollViewer.SetIsVerticalRailEnabled(element, isVerticalRailEnabled);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetHorizontalScrollBarVisibility_stub(Microsoft.UI.Xaml.DependencyObject element, Microsoft.UI.Xaml.Controls.ScrollBarVisibility horizontalScrollBarVisibility)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.ScrollViewer.SetHorizontalScrollBarVisibility(element, horizontalScrollBarVisibility);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetHorizontalScrollMode_stub(Microsoft.UI.Xaml.DependencyObject element, Microsoft.UI.Xaml.Controls.ScrollMode horizontalScrollMode)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.ScrollViewer.SetHorizontalScrollMode(element, horizontalScrollMode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetIsHorizontalRailEnabled_stub(Microsoft.UI.Xaml.DependencyObject element, bool isHorizontalRailEnabled)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.ScrollViewer.SetIsHorizontalRailEnabled(element, isHorizontalRailEnabled);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContainerContentChangingEventArgs_stub
{
    public static void RegisterUpdateCallback_stub(this Microsoft.UI.Xaml.Controls.ContainerContentChangingEventArgs sender, Windows.Foundation.TypedEventHandler<Microsoft.UI.Xaml.Controls.ListViewBase, Microsoft.UI.Xaml.Controls.ContainerContentChangingEventArgs> callback)
    {
        try
        {
            sender.RegisterUpdateCallback(callback);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RegisterUpdateCallback_stub(this Microsoft.UI.Xaml.Controls.ContainerContentChangingEventArgs sender, uint callbackPhase, Windows.Foundation.TypedEventHandler<Microsoft.UI.Xaml.Controls.ListViewBase, Microsoft.UI.Xaml.Controls.ContainerContentChangingEventArgs> callback)
    {
        try
        {
            sender.RegisterUpdateCallback(callbackPhase, callback);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Control_stub
{
    public static bool Focus_stub(this Microsoft.UI.Xaml.Controls.Control sender, Microsoft.UI.Xaml.FocusState value)
    {
        try
        {
            return sender.Focus(value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class UIElementCollection_stub
{
    public static void Move_stub(this Microsoft.UI.Xaml.Controls.UIElementCollection sender, uint oldIndex, uint newIndex)
    {
        try
        {
            sender.Move(oldIndex, newIndex);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Canvas_stub
{
    public static void SetZIndex_stub(Microsoft.UI.Xaml.UIElement element, int value)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.Canvas.SetZIndex(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetLeft_stub(Microsoft.UI.Xaml.UIElement element, double length)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(element, length);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetTop_stub(Microsoft.UI.Xaml.UIElement element, double length)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.Canvas.SetTop(element, length);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ToolTipService_stub
{
    public static void SetToolTip_stub(Microsoft.UI.Xaml.DependencyObject element, object value)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static object GetToolTip_stub(Microsoft.UI.Xaml.DependencyObject element)
    {
        try
        {
            return Microsoft.UI.Xaml.Controls.ToolTipService.GetToolTip(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class DataTemplateSelector_stub
{
    public static Microsoft.UI.Xaml.DataTemplate SelectTemplate_stub(this Microsoft.UI.Xaml.Controls.DataTemplateSelector sender, object item, Microsoft.UI.Xaml.DependencyObject container)
    {
        try
        {
            return sender.SelectTemplate(item, container);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Image_stub
{
    public static Microsoft.UI.Composition.CompositionBrush GetAlphaMask_stub(this Microsoft.UI.Xaml.Controls.Image sender)
    {
        try
        {
            return sender.GetAlphaMask();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class TextBlock_stub
{
    public static Microsoft.UI.Composition.CompositionBrush GetAlphaMask_stub(this Microsoft.UI.Xaml.Controls.TextBlock sender)
    {
        try
        {
            return sender.GetAlphaMask();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class MenuFlyout_stub
{
    public static void ShowAt_stub(this Microsoft.UI.Xaml.Controls.MenuFlyout sender, Microsoft.UI.Xaml.UIElement targetElement, Windows.Foundation.Point point)
    {
        try
        {
            sender.ShowAt(targetElement, point);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContentDialog_stub
{
    public static async Task<Microsoft.UI.Xaml.Controls.ContentDialogResult> ShowAsync_stub(this Microsoft.UI.Xaml.Controls.ContentDialog sender)
    {
        try
        {
            return await sender.ShowAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Hide_stub(this Microsoft.UI.Xaml.Controls.ContentDialog sender)
    {
        try
        {
            sender.Hide();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Frame_stub
{
    public static void GoBack_stub(this Microsoft.UI.Xaml.Controls.Frame sender)
    {
        try
        {
            sender.GoBack();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool Navigate_stub(this Microsoft.UI.Xaml.Controls.Frame sender, System.Type sourcePageType)
    {
        try
        {
            return sender.Navigate(sourcePageType);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool Navigate_stub(this Microsoft.UI.Xaml.Controls.Frame sender, System.Type sourcePageType, object parameter, Microsoft.UI.Xaml.Media.Animation.NavigationTransitionInfo infoOverride)
    {
        try
        {
            return sender.Navigate(sourcePageType, parameter, infoOverride);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void GoBack_stub(this Microsoft.UI.Xaml.Controls.Frame sender, Microsoft.UI.Xaml.Media.Animation.NavigationTransitionInfo transitionInfoOverride)
    {
        try
        {
            sender.GoBack(transitionInfoOverride);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetNavigationState_stub(this Microsoft.UI.Xaml.Controls.Frame sender)
    {
        try
        {
            return sender.GetNavigationState();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetNavigationState_stub(this Microsoft.UI.Xaml.Controls.Frame sender, string navigationState)
    {
        try
        {
            sender.SetNavigationState(navigationState);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void GoForward_stub(this Microsoft.UI.Xaml.Controls.Frame sender)
    {
        try
        {
            sender.GoForward();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class TextBox_stub
{
    public static void SelectAll_stub(this Microsoft.UI.Xaml.Controls.TextBox sender)
    {
        try
        {
            sender.SelectAll();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class RichTextBlock_stub
{
    public static void Select_stub(this Microsoft.UI.Xaml.Controls.RichTextBlock sender, Microsoft.UI.Xaml.Documents.TextPointer start, Microsoft.UI.Xaml.Documents.TextPointer end)
    {
        try
        {
            sender.Select(start, end);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Documents.TextPointer GetPositionFromPoint_stub(this Microsoft.UI.Xaml.Controls.RichTextBlock sender, Windows.Foundation.Point point)
    {
        try
        {
            return sender.GetPositionFromPoint(point);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class HandwritingView_stub
{
    public static bool TryClose_stub(this Microsoft.UI.Xaml.Controls.HandwritingView sender)
    {
        try
        {
            return sender.TryClose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class WebView_stub
{
    public static void AddWebAllowedObject_stub(this Microsoft.UI.Xaml.Controls.WebView sender, string name, object pObject)
    {
        try
        {
            sender.AddWebAllowedObject(name, pObject);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Navigate_stub(this Microsoft.UI.Xaml.Controls.WebView sender, System.Uri source)
    {
        try
        {
            sender.Navigate(source);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void NavigateToString_stub(this Microsoft.UI.Xaml.Controls.WebView sender, string text)
    {
        try
        {
            sender.NavigateToString(text);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static async Task<string> InvokeScriptAsync_stub(this Microsoft.UI.Xaml.Controls.WebView sender, string scriptName, System.Collections.Generic.IEnumerable<string> arguments)
    {
        try
        {
            return await sender.InvokeScriptAsync(scriptName, arguments);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Refresh_stub(this Microsoft.UI.Xaml.Controls.WebView sender)
    {
        try
        {
            sender.Refresh();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContentDialogButtonClickEventArgs_stub
{
    public static Microsoft.UI.Xaml.Controls.ContentDialogButtonClickDeferral GetDeferral_stub(this Microsoft.UI.Xaml.Controls.ContentDialogButtonClickEventArgs sender)
    {
        try
        {
            return sender.GetDeferral();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContentDialogButtonClickDeferral_stub
{
    public static void Complete_stub(this Microsoft.UI.Xaml.Controls.ContentDialogButtonClickDeferral sender)
    {
        try
        {
            sender.Complete();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class PasswordBox_stub
{
    public static void SelectAll_stub(this Microsoft.UI.Xaml.Controls.PasswordBox sender)
    {
        try
        {
            sender.SelectAll();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CalendarView_stub
{
    public static void SetDisplayDate_stub(this Microsoft.UI.Xaml.Controls.CalendarView sender, System.DateTimeOffset date)
    {
        try
        {
            sender.SetDisplayDate(date);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FlyoutBase_stub
{
    public static void Hide_stub(this Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase sender)
    {
        try
        {
            sender.Hide();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ShowAt_stub(this Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase sender, Microsoft.UI.Xaml.DependencyObject placementTarget, Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions showOptions)
    {
        try
        {
            sender.ShowAt(placementTarget, showOptions);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ShowAt_stub(this Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase sender, Microsoft.UI.Xaml.FrameworkElement placementTarget)
    {
        try
        {
            sender.ShowAt(placementTarget);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase GetAttachedFlyout_stub(Microsoft.UI.Xaml.FrameworkElement element)
    {
        try
        {
            return Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase.GetAttachedFlyout(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ShowAttachedFlyout_stub(Microsoft.UI.Xaml.FrameworkElement flyoutOwner)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase.ShowAttachedFlyout(flyoutOwner);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class XamlDirect_stub
{
    public static Microsoft.UI.Xaml.Core.Direct.XamlDirect GetDefault_stub()
    {
        try
        {
            return Microsoft.UI.Xaml.Core.Direct.XamlDirect.GetDefault();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject GetXamlDirectObject_stub(this Microsoft.UI.Xaml.Core.Direct.XamlDirect sender, object @object)
    {
        try
        {
            return sender.GetXamlDirectObject(@object);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject GetXamlDirectObjectProperty_stub(this Microsoft.UI.Xaml.Core.Direct.XamlDirect sender, Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Microsoft.UI.Xaml.Core.Direct.XamlPropertyIndex propertyIndex)
    {
        try
        {
            return sender.GetXamlDirectObjectProperty(xamlDirectObject, propertyIndex);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void ClearCollection_stub(this Microsoft.UI.Xaml.Core.Direct.XamlDirect sender, Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject)
    {
        try
        {
            sender.ClearCollection(xamlDirectObject);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject CreateInstance_stub(this Microsoft.UI.Xaml.Core.Direct.XamlDirect sender, Microsoft.UI.Xaml.Core.Direct.XamlTypeIndex typeIndex)
    {
        try
        {
            return sender.CreateInstance(typeIndex);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetDoubleProperty_stub(this Microsoft.UI.Xaml.Core.Direct.XamlDirect sender, Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Microsoft.UI.Xaml.Core.Direct.XamlPropertyIndex propertyIndex, double value)
    {
        try
        {
            sender.SetDoubleProperty(xamlDirectObject, propertyIndex, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetEnumProperty_stub(this Microsoft.UI.Xaml.Core.Direct.XamlDirect sender, Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Microsoft.UI.Xaml.Core.Direct.XamlPropertyIndex propertyIndex, uint value)
    {
        try
        {
            sender.SetEnumProperty(xamlDirectObject, propertyIndex, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static object GetObject_stub(this Microsoft.UI.Xaml.Core.Direct.XamlDirect sender, Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject)
    {
        try
        {
            return sender.GetObject(xamlDirectObject);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void AddToCollection_stub(this Microsoft.UI.Xaml.Core.Direct.XamlDirect sender, Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject value)
    {
        try
        {
            sender.AddToCollection(xamlDirectObject, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetObjectProperty_stub(this Microsoft.UI.Xaml.Core.Direct.XamlDirect sender, Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Microsoft.UI.Xaml.Core.Direct.XamlPropertyIndex propertyIndex, object value)
    {
        try
        {
            sender.SetObjectProperty(xamlDirectObject, propertyIndex, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetStringProperty_stub(this Microsoft.UI.Xaml.Core.Direct.XamlDirect sender, Microsoft.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Microsoft.UI.Xaml.Core.Direct.XamlPropertyIndex propertyIndex, string value)
    {
        try
        {
            sender.SetStringProperty(xamlDirectObject, propertyIndex, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ISupportIncrementalLoading_stub
{
    public static async Task<Microsoft.UI.Xaml.Data.LoadMoreItemsResult> LoadMoreItemsAsync_stub(this Microsoft.UI.Xaml.Data.ISupportIncrementalLoading sender, uint count)
    {
        try
        {
            return await sender.LoadMoreItemsAsync(count);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class TextPointer_stub
{
    public static Windows.Foundation.Rect GetCharacterRect_stub(this Microsoft.UI.Xaml.Documents.TextPointer sender, Microsoft.UI.Xaml.Documents.LogicalDirection direction)
    {
        try
        {
            return sender.GetCharacterRect(direction);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Typography_stub
{
    public static void SetVariants_stub(Microsoft.UI.Xaml.DependencyObject element, Microsoft.UI.Xaml.FontVariants value)
    {
        try
        {
            Microsoft.UI.Xaml.Documents.Typography.SetVariants(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ElementCompositionPreview_stub
{
    public static void SetIsTranslationEnabled_stub(Microsoft.UI.Xaml.UIElement element, bool value)
    {
        try
        {
            Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.SetIsTranslationEnabled(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetElementChildVisual_stub(Microsoft.UI.Xaml.UIElement element, Microsoft.UI.Composition.Visual visual)
    {
        try
        {
            Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.SetElementChildVisual(element, visual);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.Visual GetElementVisual_stub(Microsoft.UI.Xaml.UIElement element)
    {
        try
        {
            return Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.CompositionPropertySet GetScrollViewerManipulationPropertySet_stub(Microsoft.UI.Xaml.Controls.ScrollViewer scrollViewer)
    {
        try
        {
            return Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Composition.Visual GetElementChildVisual_stub(Microsoft.UI.Xaml.UIElement element)
    {
        try
        {
            return Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementChildVisual(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class PointerRoutedEventArgs_stub
{
    public static Windows.UI.Input.PointerPoint GetCurrentPoint_stub(this Microsoft.UI.Xaml.Input.PointerRoutedEventArgs sender, Microsoft.UI.Xaml.UIElement relativeTo)
    {
        try
        {
            return sender.GetCurrentPoint(relativeTo);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class FocusManager_stub
{
    public static bool TryMoveFocus_stub(Microsoft.UI.Xaml.Input.FocusNavigationDirection focusNavigationDirection, Microsoft.UI.Xaml.Input.FindNextElementOptions focusNavigationOptions)
    {
        try
        {
            return Microsoft.UI.Xaml.Input.FocusManager.TryMoveFocus(focusNavigationDirection, focusNavigationOptions);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.UIElement FindNextFocusableElement_stub(Microsoft.UI.Xaml.Input.FocusNavigationDirection focusNavigationDirection)
    {
        try
        {
            return Microsoft.UI.Xaml.Input.FocusManager.FindNextFocusableElement(focusNavigationDirection);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.DependencyObject FindFirstFocusableElement_stub(Microsoft.UI.Xaml.DependencyObject searchScope)
    {
        try
        {
            return Microsoft.UI.Xaml.Input.FocusManager.FindFirstFocusableElement(searchScope);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static object GetFocusedElement_stub()
    {
        try
        {
            return Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ManipulationDeltaRoutedEventArgs_stub
{
    public static void Complete_stub(this Microsoft.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs sender)
    {
        try
        {
            sender.Complete();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ContextRequestedEventArgs_stub
{
    public static bool TryGetPosition_stub(this Microsoft.UI.Xaml.Input.ContextRequestedEventArgs sender, Microsoft.UI.Xaml.UIElement relativeTo, out Windows.Foundation.Point point)
    {
        try
        {
            return sender.TryGetPosition(relativeTo, out point);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class GettingFocusEventArgs_stub
{
    public static bool TryCancel_stub(this Microsoft.UI.Xaml.Input.GettingFocusEventArgs sender)
    {
        try
        {
            return sender.TryCancel();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool TrySetNewFocusedElement_stub(this Microsoft.UI.Xaml.Input.GettingFocusEventArgs sender, Microsoft.UI.Xaml.DependencyObject element)
    {
        try
        {
            return sender.TrySetNewFocusedElement(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class LosingFocusEventArgs_stub
{
    public static bool TrySetNewFocusedElement_stub(this Microsoft.UI.Xaml.Input.LosingFocusEventArgs sender, Microsoft.UI.Xaml.DependencyObject element)
    {
        try
        {
            return sender.TrySetNewFocusedElement(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool TryCancel_stub(this Microsoft.UI.Xaml.Input.LosingFocusEventArgs sender)
    {
        try
        {
            return sender.TryCancel();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class XamlMarkupHelper_stub
{
    public static void UnloadObject_stub(Microsoft.UI.Xaml.DependencyObject element)
    {
        try
        {
            Microsoft.UI.Xaml.Markup.XamlMarkupHelper.UnloadObject(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class GeneralTransform_stub
{
    public static Windows.Foundation.Point TransformPoint_stub(this Microsoft.UI.Xaml.Media.GeneralTransform sender, Windows.Foundation.Point point)
    {
        try
        {
            return sender.TransformPoint(point);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class VisualTreeHelper_stub
{
    public static Microsoft.UI.Xaml.DependencyObject GetParent_stub(Microsoft.UI.Xaml.DependencyObject reference)
    {
        try
        {
            return Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(reference);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IReadOnlyList<Microsoft.UI.Xaml.Controls.Primitives.Popup> GetOpenPopups_stub(Microsoft.UI.Xaml.Window window)
    {
        try
        {
            return Microsoft.UI.Xaml.Media.VisualTreeHelper.GetOpenPopups(window);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IEnumerable<Microsoft.UI.Xaml.UIElement> FindElementsInHostCoordinates_stub(Windows.Foundation.Point intersectingPoint, Microsoft.UI.Xaml.UIElement subtree)
    {
        try
        {
            return Microsoft.UI.Xaml.Media.VisualTreeHelper.FindElementsInHostCoordinates(intersectingPoint, subtree);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.DependencyObject GetChild_stub(Microsoft.UI.Xaml.DependencyObject reference, int childIndex)
    {
        try
        {
            return Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(reference, childIndex);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int GetChildrenCount_stub(Microsoft.UI.Xaml.DependencyObject reference)
    {
        try
        {
            return Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(reference);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void DisconnectChildrenRecursive_stub(Microsoft.UI.Xaml.UIElement element)
    {
        try
        {
            Microsoft.UI.Xaml.Media.VisualTreeHelper.DisconnectChildrenRecursive(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class LoadedImageSurface_stub
{
    public static Microsoft.UI.Xaml.Media.LoadedImageSurface StartLoadFromStream_stub(Windows.Storage.Streams.IRandomAccessStream stream)
    {
        try
        {
            return Microsoft.UI.Xaml.Media.LoadedImageSurface.StartLoadFromStream(stream);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Media.LoadedImageSurface StartLoadFromStream_stub(Windows.Storage.Streams.IRandomAccessStream stream, Windows.Foundation.Size desiredMaxSize)
    {
        try
        {
            return Microsoft.UI.Xaml.Media.LoadedImageSurface.StartLoadFromStream(stream, desiredMaxSize);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Media.LoadedImageSurface StartLoadFromUri_stub(System.Uri uri)
    {
        try
        {
            return Microsoft.UI.Xaml.Media.LoadedImageSurface.StartLoadFromUri(uri);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Dispose_stub(this Microsoft.UI.Xaml.Media.LoadedImageSurface sender)
    {
        try
        {
            sender.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ConnectedAnimationService_stub
{
    public static Microsoft.UI.Xaml.Media.Animation.ConnectedAnimation PrepareToAnimate_stub(this Microsoft.UI.Xaml.Media.Animation.ConnectedAnimationService sender, string key, Microsoft.UI.Xaml.UIElement source)
    {
        try
        {
            return sender.PrepareToAnimate(key, source);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Media.Animation.ConnectedAnimationService GetForCurrentView_stub()
    {
        try
        {
            return Microsoft.UI.Xaml.Media.Animation.ConnectedAnimationService.GetForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Microsoft.UI.Xaml.Media.Animation.ConnectedAnimation GetAnimation_stub(this Microsoft.UI.Xaml.Media.Animation.ConnectedAnimationService sender, string key)
    {
        try
        {
            return sender.GetAnimation(key);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class ConnectedAnimation_stub
{
    public static bool TryStart_stub(this Microsoft.UI.Xaml.Media.Animation.ConnectedAnimation sender, Microsoft.UI.Xaml.UIElement destination)
    {
        try
        {
            return sender.TryStart(destination);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class BitmapSource_stub
{
    public static async Task SetSourceAsync_stub(this Microsoft.UI.Xaml.Media.Imaging.BitmapSource sender, Windows.Storage.Streams.IRandomAccessStream streamSource)
    {
        try
        {
            await sender.SetSourceAsync(streamSource);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetSource_stub(this Microsoft.UI.Xaml.Media.Imaging.BitmapSource sender, Windows.Storage.Streams.IRandomAccessStream streamSource)
    {
        try
        {
            sender.SetSource(streamSource);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class WriteableBitmap_stub
{
    public static void Invalidate_stub(this Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap sender)
    {
        try
        {
            sender.Invalidate();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class SoftwareBitmapSource_stub
{
    public static async Task SetBitmapAsync_stub(this Microsoft.UI.Xaml.Media.Imaging.SoftwareBitmapSource sender, Windows.Graphics.Imaging.SoftwareBitmap softwareBitmap)
    {
        try
        {
            await sender.SetBitmapAsync(softwareBitmap);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class Shape_stub
{
    public static Microsoft.UI.Composition.CompositionBrush GetAlphaMask_stub(this Microsoft.UI.Xaml.Shapes.Shape sender)
    {
        try
        {
            return sender.GetAlphaMask();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
