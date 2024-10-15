using System;
using Telegram;

public static class StorageItemAccessList_stub
{
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
public static class ConnectedAnimation_stub
{
    public static bool TryStart_stub(this Windows.UI.Xaml.Media.Animation.ConnectedAnimation sender, Windows.UI.Xaml.UIElement destination)
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
public static class ConnectedAnimationService_stub
{
    public static Windows.UI.Xaml.Media.Animation.ConnectedAnimation GetAnimation_stub(this Windows.UI.Xaml.Media.Animation.ConnectedAnimationService sender, string key)
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
    public static Windows.UI.Xaml.Media.Animation.ConnectedAnimationService GetForCurrentView_stub()
    {
        try
        {
            return Windows.UI.Xaml.Media.Animation.ConnectedAnimationService.GetForCurrentView();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Xaml.Media.Animation.ConnectedAnimation PrepareToAnimate_stub(this Windows.UI.Xaml.Media.Animation.ConnectedAnimationService sender, string key, Windows.UI.Xaml.UIElement source)
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
}
public static class Storyboard_stub
{
    public static void Begin_stub(this Windows.UI.Xaml.Media.Animation.Storyboard sender)
    {
        try
        {
            sender.Begin();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetTarget_stub(Windows.UI.Xaml.Media.Animation.Timeline timeline, Windows.UI.Xaml.DependencyObject target)
    {
        try
        {
            Windows.UI.Xaml.Media.Animation.Storyboard.SetTarget(timeline, target);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetTargetProperty_stub(Windows.UI.Xaml.Media.Animation.Timeline element, string path)
    {
        try
        {
            Windows.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(element, path);
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
public static class LimitedAccessFeatures_stub
{
    public static Windows.ApplicationModel.LimitedAccessFeatureRequestResult TryUnlockFeature_stub(string featureId, string token, string attestation)
    {
        try
        {
            return Windows.ApplicationModel.LimitedAccessFeatures.TryUnlockFeature(featureId, token, attestation);
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
public static class StartupTask_stub
{
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
public static class AudioGraph_stub
{
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
}
public static class AutomationProperties_stub
{
    public static Windows.UI.Xaml.Automation.Peers.AccessibilityView GetAccessibilityView_stub(Windows.UI.Xaml.DependencyObject element)
    {
        try
        {
            return Windows.UI.Xaml.Automation.AutomationProperties.GetAccessibilityView(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IList<Windows.UI.Xaml.UIElement> GetControlledPeers_stub(Windows.UI.Xaml.DependencyObject element)
    {
        try
        {
            return Windows.UI.Xaml.Automation.AutomationProperties.GetControlledPeers(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetName_stub(Windows.UI.Xaml.DependencyObject element)
    {
        try
        {
            return Windows.UI.Xaml.Automation.AutomationProperties.GetName(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetAutomationControlType_stub(Windows.UI.Xaml.UIElement element, Windows.UI.Xaml.Automation.Peers.AutomationControlType value)
    {
        try
        {
            Windows.UI.Xaml.Automation.AutomationProperties.SetAutomationControlType(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetName_stub(Windows.UI.Xaml.DependencyObject element, string value)
    {
        try
        {
            Windows.UI.Xaml.Automation.AutomationProperties.SetName(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetPositionInSet_stub(Windows.UI.Xaml.DependencyObject element, int value)
    {
        try
        {
            Windows.UI.Xaml.Automation.AutomationProperties.SetPositionInSet(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetSizeOfSet_stub(Windows.UI.Xaml.DependencyObject element, int value)
    {
        try
        {
            Windows.UI.Xaml.Automation.AutomationProperties.SetSizeOfSet(element, value);
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
}
public static class BackgroundTaskBuilder_stub
{
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
public static class VoipCaptureBase_stub
{
    public static void SetOutput_stub(this Telegram.Native.Calls.VoipCaptureBase sender, Telegram.Native.Calls.VoipVideoOutputSink sink)
    {
        try
        {
            sender.SetOutput(sink);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetState_stub(this Telegram.Native.Calls.VoipCaptureBase sender, Telegram.Native.Calls.VoipVideoState state)
    {
        try
        {
            sender.SetState(state);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Stop_stub(this Telegram.Native.Calls.VoipCaptureBase sender)
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
}
public static class VoipGroupManager_stub
{
    public static void AddIncomingVideoOutput_stub(this Telegram.Native.Calls.VoipGroupManager sender, string endpointId, Telegram.Native.Calls.VoipVideoOutputSink sink)
    {
        try
        {
            sender.AddIncomingVideoOutput(endpointId, sink);
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
    public static void Stop_stub(this Telegram.Native.Calls.VoipGroupManager sender)
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
}
public static class VoipManager_stub
{
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
    public static void SetIncomingVideoOutput_stub(this Telegram.Native.Calls.VoipManager sender, Telegram.Native.Calls.VoipVideoOutputSink sink)
    {
        try
        {
            sender.SetIncomingVideoOutput(sink);
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
    public static void Start_stub(this Telegram.Native.Calls.VoipManager sender, Telegram.Native.Calls.VoipDescriptor descriptor)
    {
        try
        {
            sender.Start(descriptor);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Stop_stub(this Telegram.Native.Calls.VoipManager sender)
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
}
public static class VoipVideoOutputSink_stub
{
    public static void Stop_stub(this Telegram.Native.Calls.VoipVideoOutputSink sender)
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
}
public static class VoipPhoneCall_stub
{
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
public static class CanvasBitmap_stub
{
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
    public static Microsoft.Graphics.Canvas.CanvasDevice GetSharedDevice_stub(bool forceSoftwareRenderer)
    {
        try
        {
            return Microsoft.Graphics.Canvas.CanvasDevice.GetSharedDevice(forceSoftwareRenderer);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class CanvasDrawingSession_stub
{
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
public static class MediaCapture_stub
{
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
public static class CompositionDevice_stub
{
    public static Telegram.Native.Composition.DirectRectangleClip CreateRectangleClip_stub(Windows.UI.Xaml.UIElement element)
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
    public static Telegram.Native.Composition.DirectRectangleClip2 CreateRectangleClip2_stub(Windows.UI.Xaml.UIElement element)
    {
        try
        {
            return Telegram.Native.Composition.CompositionDevice.CreateRectangleClip2(element);
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
    public static void AnimateBottom_stub(this Telegram.Native.Composition.DirectRectangleClip sender, Windows.UI.Composition.Compositor compositor, float from, float to, double duration)
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
    public static void AnimateBottomLeft_stub(this Telegram.Native.Composition.DirectRectangleClip sender, Windows.UI.Composition.Compositor compositor, float from, float to, double duration)
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
    public static void AnimateBottomRight_stub(this Telegram.Native.Composition.DirectRectangleClip sender, Windows.UI.Composition.Compositor compositor, float from, float to, double duration)
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
    public static void AnimateTop_stub(this Telegram.Native.Composition.DirectRectangleClip sender, Windows.UI.Composition.Compositor compositor, float from, float to, double duration)
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
}
public static class DirectRectangleClip2_stub
{
    public static void Set_stub(this Telegram.Native.Composition.DirectRectangleClip2 sender, System.Numerics.Vector2 uniform)
    {
        try
        {
            sender.Set(uniform);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetInset_stub(this Telegram.Native.Composition.DirectRectangleClip2 sender, float left, float top, float right, float bottom)
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
}
public static class AnimationController_stub
{
    public static void Pause_stub(this Windows.UI.Composition.AnimationController sender)
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
    public static void Resume_stub(this Windows.UI.Composition.AnimationController sender)
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
public static class BooleanKeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.BooleanKeyFrameAnimation sender, float normalizedProgressKey, bool value)
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
public static class ColorKeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.ColorKeyFrameAnimation sender, float normalizedProgressKey, Windows.UI.Color value, Windows.UI.Composition.CompositionEasingFunction easingFunction)
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
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.ColorKeyFrameAnimation sender, float normalizedProgressKey, Windows.UI.Color value)
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
public static class CompositionAnimation_stub
{
    public static void ClearAllParameters_stub(this Windows.UI.Composition.CompositionAnimation sender)
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
    public static void SetReferenceParameter_stub(this Windows.UI.Composition.CompositionAnimation sender, string key, Windows.UI.Composition.CompositionObject compositionObject)
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
    public static void SetScalarParameter_stub(this Windows.UI.Composition.CompositionAnimation sender, string key, float value)
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
public static class CompositionCapabilities_stub
{
    public static bool AreEffectsFast_stub(this Windows.UI.Composition.CompositionCapabilities sender)
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
    public static Windows.UI.Composition.CompositionCapabilities GetForCurrentView_stub()
    {
        try
        {
            return Windows.UI.Composition.CompositionCapabilities.GetForCurrentView();
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
    public static void SetSourceParameter_stub(this Windows.UI.Composition.CompositionEffectBrush sender, string name, Windows.UI.Composition.CompositionBrush source)
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
public static class CompositionEffectFactory_stub
{
    public static Windows.UI.Composition.CompositionEffectBrush CreateBrush_stub(this Windows.UI.Composition.CompositionEffectFactory sender)
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
public static class CompositionObject_stub
{
    public static void Dispose_stub(this Windows.UI.Composition.CompositionObject sender)
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
    public static void StartAnimation_stub(this Windows.UI.Composition.CompositionObject sender, string propertyName, Windows.UI.Composition.CompositionAnimation animation)
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
    public static void StartAnimation_stub(this Windows.UI.Composition.CompositionObject sender, string propertyName, Windows.UI.Composition.CompositionAnimation animation, Windows.UI.Composition.AnimationController animationController)
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
    public static void StopAnimation_stub(this Windows.UI.Composition.CompositionObject sender, string propertyName)
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
    public static Windows.UI.Composition.AnimationController TryGetAnimationController_stub(this Windows.UI.Composition.CompositionObject sender, string propertyName)
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
}
public static class CompositionPropertySet_stub
{
    public static void InsertBoolean_stub(this Windows.UI.Composition.CompositionPropertySet sender, string propertyName, bool value)
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
    public static void InsertColor_stub(this Windows.UI.Composition.CompositionPropertySet sender, string propertyName, Windows.UI.Color value)
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
    public static void InsertScalar_stub(this Windows.UI.Composition.CompositionPropertySet sender, string propertyName, float value)
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
    public static void InsertVector3_stub(this Windows.UI.Composition.CompositionPropertySet sender, string propertyName, System.Numerics.Vector3 value)
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
    public static void InsertVector4_stub(this Windows.UI.Composition.CompositionPropertySet sender, string propertyName, System.Numerics.Vector4 value)
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
    public static Windows.UI.Composition.CompositionGetValueStatus TryGetVector3_stub(this Windows.UI.Composition.CompositionPropertySet sender, string propertyName, out System.Numerics.Vector3 value)
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
}
public static class CompositionScopedBatch_stub
{
    public static void End_stub(this Windows.UI.Composition.CompositionScopedBatch sender)
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
public static class Compositor_stub
{
    public static Windows.UI.Composition.AnimationController CreateAnimationController_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionBackdropBrush CreateBackdropBrush_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.BooleanKeyFrameAnimation CreateBooleanKeyFrameAnimation_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionColorBrush CreateColorBrush_stub(this Windows.UI.Composition.Compositor sender, Windows.UI.Color color)
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
    public static Windows.UI.Composition.CompositionColorBrush CreateColorBrush_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionColorGradientStop CreateColorGradientStop_stub(this Windows.UI.Composition.Compositor sender, float offset, Windows.UI.Color color)
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
    public static Windows.UI.Composition.ColorKeyFrameAnimation CreateColorKeyFrameAnimation_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionContainerShape CreateContainerShape_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.ContainerVisual CreateContainerVisual_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CubicBezierEasingFunction CreateCubicBezierEasingFunction_stub(this Windows.UI.Composition.Compositor sender, System.Numerics.Vector2 controlPoint1, System.Numerics.Vector2 controlPoint2)
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
    public static Windows.UI.Composition.DropShadow CreateDropShadow_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionEffectFactory CreateEffectFactory_stub(this Windows.UI.Composition.Compositor sender, Windows.Graphics.Effects.IGraphicsEffect graphicsEffect)
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
    public static Windows.UI.Composition.CompositionEffectFactory CreateEffectFactory_stub(this Windows.UI.Composition.Compositor sender, Windows.Graphics.Effects.IGraphicsEffect graphicsEffect, System.Collections.Generic.IEnumerable<string> animatableProperties)
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
    public static Windows.UI.Composition.CompositionEllipseGeometry CreateEllipseGeometry_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.ExpressionAnimation CreateExpressionAnimation_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.ExpressionAnimation CreateExpressionAnimation_stub(this Windows.UI.Composition.Compositor sender, string expression)
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
    public static Windows.UI.Composition.CompositionGeometricClip CreateGeometricClip_stub(this Windows.UI.Composition.Compositor sender, Windows.UI.Composition.CompositionGeometry geometry)
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
    public static Windows.UI.Composition.CompositionGeometricClip CreateGeometricClip_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.InsetClip CreateInsetClip_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.InsetClip CreateInsetClip_stub(this Windows.UI.Composition.Compositor sender, float leftInset, float topInset, float rightInset, float bottomInset)
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
    public static Windows.UI.Composition.LinearEasingFunction CreateLinearEasingFunction_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionLinearGradientBrush CreateLinearGradientBrush_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionLineGeometry CreateLineGeometry_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionMaskBrush CreateMaskBrush_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionPathGeometry CreatePathGeometry_stub(this Windows.UI.Composition.Compositor sender, Windows.UI.Composition.CompositionPath path)
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
    public static Windows.UI.Composition.CompositionPathGeometry CreatePathGeometry_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.PathKeyFrameAnimation CreatePathKeyFrameAnimation_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionPropertySet CreatePropertySet_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionRadialGradientBrush CreateRadialGradientBrush_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.RectangleClip CreateRectangleClip_stub(this Windows.UI.Composition.Compositor sender, float left, float top, float right, float bottom, System.Numerics.Vector2 topLeftRadius, System.Numerics.Vector2 topRightRadius, System.Numerics.Vector2 bottomRightRadius, System.Numerics.Vector2 bottomLeftRadius)
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
    public static Windows.UI.Composition.CompositionRectangleGeometry CreateRectangleGeometry_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionRoundedRectangleGeometry CreateRoundedRectangleGeometry_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.ScalarKeyFrameAnimation CreateScalarKeyFrameAnimation_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionScopedBatch CreateScopedBatch_stub(this Windows.UI.Composition.Compositor sender, Windows.UI.Composition.CompositionBatchTypes batchType)
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
    public static Windows.UI.Composition.ShapeVisual CreateShapeVisual_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.SpringScalarNaturalMotionAnimation CreateSpringScalarAnimation_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.SpringVector3NaturalMotionAnimation CreateSpringVector3Animation_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionSpriteShape CreateSpriteShape_stub(this Windows.UI.Composition.Compositor sender, Windows.UI.Composition.CompositionGeometry geometry)
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
    public static Windows.UI.Composition.CompositionSpriteShape CreateSpriteShape_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.SpriteVisual CreateSpriteVisual_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.StepEasingFunction CreateStepEasingFunction_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionSurfaceBrush CreateSurfaceBrush_stub(this Windows.UI.Composition.Compositor sender, Windows.UI.Composition.ICompositionSurface surface)
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
    public static Windows.UI.Composition.CompositionSurfaceBrush CreateSurfaceBrush_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.Vector2KeyFrameAnimation CreateVector2KeyFrameAnimation_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.Vector3KeyFrameAnimation CreateVector3KeyFrameAnimation_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionViewBox CreateViewBox_stub(this Windows.UI.Composition.Compositor sender)
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
    public static Windows.UI.Composition.CompositionVisualSurface CreateVisualSurface_stub(this Windows.UI.Composition.Compositor sender)
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
}
public static class KeyFrameAnimation_stub
{
    public static void InsertExpressionKeyFrame_stub(this Windows.UI.Composition.KeyFrameAnimation sender, float normalizedProgressKey, string value, Windows.UI.Composition.CompositionEasingFunction easingFunction)
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
public static class PathKeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.PathKeyFrameAnimation sender, float normalizedProgressKey, Windows.UI.Composition.CompositionPath path, Windows.UI.Composition.CompositionEasingFunction easingFunction)
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
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.PathKeyFrameAnimation sender, float normalizedProgressKey, Windows.UI.Composition.CompositionPath path)
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
public static class ScalarKeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.ScalarKeyFrameAnimation sender, float normalizedProgressKey, float value, Windows.UI.Composition.CompositionEasingFunction easingFunction)
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
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.ScalarKeyFrameAnimation sender, float normalizedProgressKey, float value)
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
public static class Vector2KeyFrameAnimation_stub
{
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.Vector2KeyFrameAnimation sender, float normalizedProgressKey, System.Numerics.Vector2 value, Windows.UI.Composition.CompositionEasingFunction easingFunction)
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
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.Vector2KeyFrameAnimation sender, float normalizedProgressKey, System.Numerics.Vector2 value)
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
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.Vector3KeyFrameAnimation sender, float normalizedProgressKey, System.Numerics.Vector3 value)
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
    public static void InsertKeyFrame_stub(this Windows.UI.Composition.Vector3KeyFrameAnimation sender, float normalizedProgressKey, System.Numerics.Vector3 value, Windows.UI.Composition.CompositionEasingFunction easingFunction)
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
public static class VisualCollection_stub
{
    public static void InsertAtBottom_stub(this Windows.UI.Composition.VisualCollection sender, Windows.UI.Composition.Visual newChild)
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
    public static void InsertAtTop_stub(this Windows.UI.Composition.VisualCollection sender, Windows.UI.Composition.Visual newChild)
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
    public static void RemoveAll_stub(this Windows.UI.Composition.VisualCollection sender)
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
public static class ContactManager_stub
{
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
}
public static class BackdropMaterial_stub
{
    public static void SetApplyToRootOrPageBackground_stub(Windows.UI.Xaml.Controls.Control element, bool value)
    {
        try
        {
            Microsoft.UI.Xaml.Controls.BackdropMaterial.SetApplyToRootOrPageBackground(element, value);
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
    public static Microsoft.UI.Xaml.Controls.IAnimatedVisual TryCreateAnimatedVisual_stub(this Microsoft.UI.Xaml.Controls.IAnimatedVisualSource sender, Windows.UI.Composition.Compositor compositor, out object diagnostics)
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
public static class WebView2_stub
{
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
}
public static class CalendarView_stub
{
    public static void SetDisplayDate_stub(this Windows.UI.Xaml.Controls.CalendarView sender, System.DateTimeOffset date)
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
public static class Canvas_stub
{
    public static void SetLeft_stub(Windows.UI.Xaml.UIElement element, double length)
    {
        try
        {
            Windows.UI.Xaml.Controls.Canvas.SetLeft(element, length);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetTop_stub(Windows.UI.Xaml.UIElement element, double length)
    {
        try
        {
            Windows.UI.Xaml.Controls.Canvas.SetTop(element, length);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetZIndex_stub(Windows.UI.Xaml.UIElement element, int value)
    {
        try
        {
            Windows.UI.Xaml.Controls.Canvas.SetZIndex(element, value);
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
    public static void RegisterUpdateCallback_stub(this Windows.UI.Xaml.Controls.ContainerContentChangingEventArgs sender, Windows.Foundation.TypedEventHandler<Windows.UI.Xaml.Controls.ListViewBase, Windows.UI.Xaml.Controls.ContainerContentChangingEventArgs> callback)
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
    public static void RegisterUpdateCallback_stub(this Windows.UI.Xaml.Controls.ContainerContentChangingEventArgs sender, uint callbackPhase, Windows.Foundation.TypedEventHandler<Windows.UI.Xaml.Controls.ListViewBase, Windows.UI.Xaml.Controls.ContainerContentChangingEventArgs> callback)
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
public static class ContentDialog_stub
{
    public static void Hide_stub(this Windows.UI.Xaml.Controls.ContentDialog sender)
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
public static class ContentDialogButtonClickDeferral_stub
{
    public static void Complete_stub(this Windows.UI.Xaml.Controls.ContentDialogButtonClickDeferral sender)
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
public static class ContentDialogButtonClickEventArgs_stub
{
    public static Windows.UI.Xaml.Controls.ContentDialogButtonClickDeferral GetDeferral_stub(this Windows.UI.Xaml.Controls.ContentDialogButtonClickEventArgs sender)
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
public static class Control_stub
{
    public static bool Focus_stub(this Windows.UI.Xaml.Controls.Control sender, Windows.UI.Xaml.FocusState value)
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
public static class DataTemplateSelector_stub
{
    public static Windows.UI.Xaml.DataTemplate SelectTemplate_stub(this Windows.UI.Xaml.Controls.DataTemplateSelector sender, object item, Windows.UI.Xaml.DependencyObject container)
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
public static class Frame_stub
{
    public static string GetNavigationState_stub(this Windows.UI.Xaml.Controls.Frame sender)
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
    public static void GoBack_stub(this Windows.UI.Xaml.Controls.Frame sender)
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
    public static void GoBack_stub(this Windows.UI.Xaml.Controls.Frame sender, Windows.UI.Xaml.Media.Animation.NavigationTransitionInfo transitionInfoOverride)
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
    public static void GoForward_stub(this Windows.UI.Xaml.Controls.Frame sender)
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
    public static bool Navigate_stub(this Windows.UI.Xaml.Controls.Frame sender, System.Type sourcePageType)
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
    public static bool Navigate_stub(this Windows.UI.Xaml.Controls.Frame sender, System.Type sourcePageType, object parameter, Windows.UI.Xaml.Media.Animation.NavigationTransitionInfo infoOverride)
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
    public static void SetNavigationState_stub(this Windows.UI.Xaml.Controls.Frame sender, string navigationState)
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
}
public static class Grid_stub
{
    public static int GetRow_stub(Windows.UI.Xaml.FrameworkElement element)
    {
        try
        {
            return Windows.UI.Xaml.Controls.Grid.GetRow(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetColumn_stub(Windows.UI.Xaml.FrameworkElement element, int value)
    {
        try
        {
            Windows.UI.Xaml.Controls.Grid.SetColumn(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetColumnSpan_stub(Windows.UI.Xaml.FrameworkElement element, int value)
    {
        try
        {
            Windows.UI.Xaml.Controls.Grid.SetColumnSpan(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetRow_stub(Windows.UI.Xaml.FrameworkElement element, int value)
    {
        try
        {
            Windows.UI.Xaml.Controls.Grid.SetRow(element, value);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetRowSpan_stub(Windows.UI.Xaml.FrameworkElement element, int value)
    {
        try
        {
            Windows.UI.Xaml.Controls.Grid.SetRowSpan(element, value);
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
    public static bool TryClose_stub(this Windows.UI.Xaml.Controls.HandwritingView sender)
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
public static class Image_stub
{
    public static Windows.UI.Composition.CompositionBrush GetAlphaMask_stub(this Windows.UI.Xaml.Controls.Image sender)
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
public static class ItemsControl_stub
{
    public static Windows.UI.Xaml.DependencyObject ContainerFromIndex_stub(this Windows.UI.Xaml.Controls.ItemsControl sender, int index)
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
    public static Windows.UI.Xaml.DependencyObject ContainerFromItem_stub(this Windows.UI.Xaml.Controls.ItemsControl sender, object item)
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
    public static Windows.UI.Xaml.DependencyObject GroupHeaderContainerFromItemContainer_stub(this Windows.UI.Xaml.Controls.ItemsControl sender, Windows.UI.Xaml.DependencyObject itemContainer)
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
    public static int IndexFromContainer_stub(this Windows.UI.Xaml.Controls.ItemsControl sender, Windows.UI.Xaml.DependencyObject container)
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
    public static object ItemFromContainer_stub(this Windows.UI.Xaml.Controls.ItemsControl sender, Windows.UI.Xaml.DependencyObject container)
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
}
public static class ListViewBase_stub
{
    public static void ScrollIntoView_stub(this Windows.UI.Xaml.Controls.ListViewBase sender, object item, Windows.UI.Xaml.Controls.ScrollIntoViewAlignment alignment)
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
    public static void ScrollIntoView_stub(this Windows.UI.Xaml.Controls.ListViewBase sender, object item)
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
    public static void SelectAll_stub(this Windows.UI.Xaml.Controls.ListViewBase sender)
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
public static class MenuFlyout_stub
{
    public static void ShowAt_stub(this Windows.UI.Xaml.Controls.MenuFlyout sender, Windows.UI.Xaml.UIElement targetElement, Windows.Foundation.Point point)
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
public static class PasswordBox_stub
{
    public static void SelectAll_stub(this Windows.UI.Xaml.Controls.PasswordBox sender)
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
    public static Windows.UI.Xaml.Documents.TextPointer GetPositionFromPoint_stub(this Windows.UI.Xaml.Controls.RichTextBlock sender, Windows.Foundation.Point point)
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
    public static void Select_stub(this Windows.UI.Xaml.Controls.RichTextBlock sender, Windows.UI.Xaml.Documents.TextPointer start, Windows.UI.Xaml.Documents.TextPointer end)
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
}
public static class ScrollViewer_stub
{
    public static bool ChangeView_stub(this Windows.UI.Xaml.Controls.ScrollViewer sender, double? horizontalOffset, double? verticalOffset, float? zoomFactor, bool disableAnimation)
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
    public static bool ChangeView_stub(this Windows.UI.Xaml.Controls.ScrollViewer sender, double? horizontalOffset, double? verticalOffset, float? zoomFactor)
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
    public static void SetHorizontalScrollBarVisibility_stub(Windows.UI.Xaml.DependencyObject element, Windows.UI.Xaml.Controls.ScrollBarVisibility horizontalScrollBarVisibility)
    {
        try
        {
            Windows.UI.Xaml.Controls.ScrollViewer.SetHorizontalScrollBarVisibility(element, horizontalScrollBarVisibility);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetHorizontalScrollMode_stub(Windows.UI.Xaml.DependencyObject element, Windows.UI.Xaml.Controls.ScrollMode horizontalScrollMode)
    {
        try
        {
            Windows.UI.Xaml.Controls.ScrollViewer.SetHorizontalScrollMode(element, horizontalScrollMode);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetIsHorizontalRailEnabled_stub(Windows.UI.Xaml.DependencyObject element, bool isHorizontalRailEnabled)
    {
        try
        {
            Windows.UI.Xaml.Controls.ScrollViewer.SetIsHorizontalRailEnabled(element, isHorizontalRailEnabled);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetIsVerticalRailEnabled_stub(Windows.UI.Xaml.DependencyObject element, bool isVerticalRailEnabled)
    {
        try
        {
            Windows.UI.Xaml.Controls.ScrollViewer.SetIsVerticalRailEnabled(element, isVerticalRailEnabled);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetVerticalScrollBarVisibility_stub(Windows.UI.Xaml.DependencyObject element, Windows.UI.Xaml.Controls.ScrollBarVisibility verticalScrollBarVisibility)
    {
        try
        {
            Windows.UI.Xaml.Controls.ScrollViewer.SetVerticalScrollBarVisibility(element, verticalScrollBarVisibility);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetVerticalScrollMode_stub(Windows.UI.Xaml.DependencyObject element, Windows.UI.Xaml.Controls.ScrollMode verticalScrollMode)
    {
        try
        {
            Windows.UI.Xaml.Controls.ScrollViewer.SetVerticalScrollMode(element, verticalScrollMode);
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
    public static Windows.UI.Composition.CompositionBrush GetAlphaMask_stub(this Windows.UI.Xaml.Controls.TextBlock sender)
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
public static class TextBox_stub
{
    public static void SelectAll_stub(this Windows.UI.Xaml.Controls.TextBox sender)
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
public static class ToolTipService_stub
{
    public static object GetToolTip_stub(Windows.UI.Xaml.DependencyObject element)
    {
        try
        {
            return Windows.UI.Xaml.Controls.ToolTipService.GetToolTip(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetToolTip_stub(Windows.UI.Xaml.DependencyObject element, object value)
    {
        try
        {
            Windows.UI.Xaml.Controls.ToolTipService.SetToolTip(element, value);
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
    public static void Move_stub(this Windows.UI.Xaml.Controls.UIElementCollection sender, uint oldIndex, uint newIndex)
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
public static class WebView_stub
{
    public static void AddWebAllowedObject_stub(this Windows.UI.Xaml.Controls.WebView sender, string name, object pObject)
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
    public static void Navigate_stub(this Windows.UI.Xaml.Controls.WebView sender, System.Uri source)
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
    public static void NavigateToString_stub(this Windows.UI.Xaml.Controls.WebView sender, string text)
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
    public static void Refresh_stub(this Windows.UI.Xaml.Controls.WebView sender)
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
public static class CoreWebView2_stub
{
    public static void AddWebResourceRequestedFilter_stub(this Microsoft.Web.WebView2.Core.CoreWebView2 sender, string uri, Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext ResourceContext)
    {
        try
        {
            sender.AddWebResourceRequestedFilter(uri, ResourceContext);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void GoBack_stub(this Microsoft.Web.WebView2.Core.CoreWebView2 sender)
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
    public static void GoForward_stub(this Microsoft.Web.WebView2.Core.CoreWebView2 sender)
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
    public static void Stop_stub(this Microsoft.Web.WebView2.Core.CoreWebView2 sender)
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
}
public static class CoreWebView2Environment_stub
{
    public static Microsoft.Web.WebView2.Core.CoreWebView2WebResourceResponse CreateWebResourceResponse_stub(this Microsoft.Web.WebView2.Core.CoreWebView2Environment sender, Windows.Storage.Streams.IRandomAccessStream Content, int StatusCode, string ReasonPhrase, string Headers)
    {
        try
        {
            return sender.CreateWebResourceResponse(Content, StatusCode, ReasonPhrase, Headers);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
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
public static class CoreWebView2HttpRequestHeaders_stub
{
    public static bool Contains_stub(this Microsoft.Web.WebView2.Core.CoreWebView2HttpRequestHeaders sender, string name)
    {
        try
        {
            return sender.Contains(name);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string GetHeader_stub(this Microsoft.Web.WebView2.Core.CoreWebView2HttpRequestHeaders sender, string name)
    {
        try
        {
            return sender.GetHeader(name);
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
public static class CoreWebView2WebResourceRequestedEventArgs_stub
{
    public static Windows.Foundation.Deferral GetDeferral_stub(this Microsoft.Web.WebView2.Core.CoreWebView2WebResourceRequestedEventArgs sender)
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
public static class CoreApplication_stub
{
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
}
public static class ResourceContext_stub
{
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
public static class HashAlgorithmProvider_stub
{
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
}
public static class CoreWindow_stub
{
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
public static class CryptographicBuffer_stub
{
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
}
public static class Clipboard_stub
{
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
}
public static class DataPackage_stub
{
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
}
public static class DataPackageView_stub
{
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
public static class XamlDirect_stub
{
    public static void AddToCollection_stub(this Windows.UI.Xaml.Core.Direct.XamlDirect sender, Windows.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Windows.UI.Xaml.Core.Direct.IXamlDirectObject value)
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
    public static void ClearCollection_stub(this Windows.UI.Xaml.Core.Direct.XamlDirect sender, Windows.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject)
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
    public static Windows.UI.Xaml.Core.Direct.IXamlDirectObject CreateInstance_stub(this Windows.UI.Xaml.Core.Direct.XamlDirect sender, Windows.UI.Xaml.Core.Direct.XamlTypeIndex typeIndex)
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
    public static Windows.UI.Xaml.Core.Direct.XamlDirect GetDefault_stub()
    {
        try
        {
            return Windows.UI.Xaml.Core.Direct.XamlDirect.GetDefault();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static object GetObject_stub(this Windows.UI.Xaml.Core.Direct.XamlDirect sender, Windows.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject)
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
    public static Windows.UI.Xaml.Core.Direct.IXamlDirectObject GetXamlDirectObject_stub(this Windows.UI.Xaml.Core.Direct.XamlDirect sender, object @object)
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
    public static Windows.UI.Xaml.Core.Direct.IXamlDirectObject GetXamlDirectObjectProperty_stub(this Windows.UI.Xaml.Core.Direct.XamlDirect sender, Windows.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Windows.UI.Xaml.Core.Direct.XamlPropertyIndex propertyIndex)
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
    public static void SetDoubleProperty_stub(this Windows.UI.Xaml.Core.Direct.XamlDirect sender, Windows.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Windows.UI.Xaml.Core.Direct.XamlPropertyIndex propertyIndex, double value)
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
    public static void SetEnumProperty_stub(this Windows.UI.Xaml.Core.Direct.XamlDirect sender, Windows.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Windows.UI.Xaml.Core.Direct.XamlPropertyIndex propertyIndex, uint value)
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
    public static void SetObjectProperty_stub(this Windows.UI.Xaml.Core.Direct.XamlDirect sender, Windows.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Windows.UI.Xaml.Core.Direct.XamlPropertyIndex propertyIndex, object value)
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
    public static void SetStringProperty_stub(this Windows.UI.Xaml.Core.Direct.XamlDirect sender, Windows.UI.Xaml.Core.Direct.IXamlDirectObject xamlDirectObject, Windows.UI.Xaml.Core.Direct.XamlPropertyIndex propertyIndex, string value)
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
public static class TextPointer_stub
{
    public static Windows.Foundation.Rect GetCharacterRect_stub(this Windows.UI.Xaml.Documents.TextPointer sender, Windows.UI.Xaml.Documents.LogicalDirection direction)
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
    public static void SetVariants_stub(Windows.UI.Xaml.DependencyObject element, Windows.UI.Xaml.FontVariants value)
    {
        try
        {
            Windows.UI.Xaml.Documents.Typography.SetVariants(element, value);
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
    public static void Stop_stub(this Windows.Devices.Enumeration.DeviceWatcher sender)
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
}
public static class ExtendedExecutionSession_stub
{
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
public static class Deferral_stub
{
    public static void Complete_stub(this Windows.Foundation.Deferral sender)
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
}
public static class CanvasGeometry_stub
{
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
}
public static class CanvasPathBuilder_stub
{
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
public static class ElementCompositionPreview_stub
{
    public static Windows.UI.Composition.Visual GetElementChildVisual_stub(Windows.UI.Xaml.UIElement element)
    {
        try
        {
            return Windows.UI.Xaml.Hosting.ElementCompositionPreview.GetElementChildVisual(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Composition.Visual GetElementVisual_stub(Windows.UI.Xaml.UIElement element)
    {
        try
        {
            return Windows.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Composition.CompositionPropertySet GetScrollViewerManipulationPropertySet_stub(Windows.UI.Xaml.Controls.ScrollViewer scrollViewer)
    {
        try
        {
            return Windows.UI.Xaml.Hosting.ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollViewer);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetAppWindowContent_stub(Windows.UI.WindowManagement.AppWindow appWindow, Windows.UI.Xaml.UIElement xamlContent)
    {
        try
        {
            Windows.UI.Xaml.Hosting.ElementCompositionPreview.SetAppWindowContent(appWindow, xamlContent);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetElementChildVisual_stub(Windows.UI.Xaml.UIElement element, Windows.UI.Composition.Visual visual)
    {
        try
        {
            Windows.UI.Xaml.Hosting.ElementCompositionPreview.SetElementChildVisual(element, visual);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void SetIsTranslationEnabled_stub(Windows.UI.Xaml.UIElement element, bool value)
    {
        try
        {
            Windows.UI.Xaml.Hosting.ElementCompositionPreview.SetIsTranslationEnabled(element, value);
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
public static class BitmapSource_stub
{
    public static void SetSource_stub(this Windows.UI.Xaml.Media.Imaging.BitmapSource sender, Windows.Storage.Streams.IRandomAccessStream streamSource)
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
    public static void Invalidate_stub(this Windows.UI.Xaml.Media.Imaging.WriteableBitmap sender)
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
    public static void ProcessMoveEvents_stub(this Windows.UI.Input.GestureRecognizer sender, System.Collections.Generic.IList<Windows.UI.Input.PointerPoint> value)
    {
        try
        {
            sender.ProcessMoveEvents(value);
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
public static class ContextRequestedEventArgs_stub
{
    public static bool TryGetPosition_stub(this Windows.UI.Xaml.Input.ContextRequestedEventArgs sender, Windows.UI.Xaml.UIElement relativeTo, out Windows.Foundation.Point point)
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
public static class FocusManager_stub
{
    public static Windows.UI.Xaml.DependencyObject FindFirstFocusableElement_stub(Windows.UI.Xaml.DependencyObject searchScope)
    {
        try
        {
            return Windows.UI.Xaml.Input.FocusManager.FindFirstFocusableElement(searchScope);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Xaml.UIElement FindNextFocusableElement_stub(Windows.UI.Xaml.Input.FocusNavigationDirection focusNavigationDirection)
    {
        try
        {
            return Windows.UI.Xaml.Input.FocusManager.FindNextFocusableElement(focusNavigationDirection);
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
            return Windows.UI.Xaml.Input.FocusManager.GetFocusedElement();
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool TryMoveFocus_stub(Windows.UI.Xaml.Input.FocusNavigationDirection focusNavigationDirection, Windows.UI.Xaml.Input.FindNextElementOptions focusNavigationOptions)
    {
        try
        {
            return Windows.UI.Xaml.Input.FocusManager.TryMoveFocus(focusNavigationDirection, focusNavigationOptions);
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
    public static bool TryCancel_stub(this Windows.UI.Xaml.Input.GettingFocusEventArgs sender)
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
    public static bool TrySetNewFocusedElement_stub(this Windows.UI.Xaml.Input.GettingFocusEventArgs sender, Windows.UI.Xaml.DependencyObject element)
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
    public static bool TryCancel_stub(this Windows.UI.Xaml.Input.LosingFocusEventArgs sender)
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
    public static bool TrySetNewFocusedElement_stub(this Windows.UI.Xaml.Input.LosingFocusEventArgs sender, Windows.UI.Xaml.DependencyObject element)
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
public static class ManipulationDeltaRoutedEventArgs_stub
{
    public static void Complete_stub(this Windows.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs sender)
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
public static class PointerRoutedEventArgs_stub
{
    public static Windows.UI.Input.PointerPoint GetCurrentPoint_stub(this Windows.UI.Xaml.Input.PointerRoutedEventArgs sender, Windows.UI.Xaml.UIElement relativeTo)
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
    public static System.Collections.Generic.IList<Windows.UI.Input.PointerPoint> GetIntermediatePoints_stub(this Windows.UI.Xaml.Input.PointerRoutedEventArgs sender, Windows.UI.Xaml.UIElement relativeTo)
    {
        try
        {
            return sender.GetIntermediatePoints(relativeTo);
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
    public static void Add_stub(this Windows.UI.Composition.Interactions.CompositionInteractionSourceCollection sender, Windows.UI.Composition.Interactions.ICompositionInteractionSource value)
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
public static class InteractionTracker_stub
{
    public static void ConfigurePositionXInertiaModifiers_stub(this Windows.UI.Composition.Interactions.InteractionTracker sender, System.Collections.Generic.IEnumerable<Windows.UI.Composition.Interactions.InteractionTrackerInertiaModifier> modifiers)
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
    public static Windows.UI.Composition.Interactions.InteractionTracker CreateWithOwner_stub(Windows.UI.Composition.Compositor compositor, Windows.UI.Composition.Interactions.IInteractionTrackerOwner owner)
    {
        try
        {
            return Windows.UI.Composition.Interactions.InteractionTracker.CreateWithOwner(compositor, owner);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int TryUpdatePosition_stub(this Windows.UI.Composition.Interactions.InteractionTracker sender, System.Numerics.Vector3 value)
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
    public static int TryUpdatePositionWithAnimation_stub(this Windows.UI.Composition.Interactions.InteractionTracker sender, Windows.UI.Composition.CompositionAnimation animation)
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
}
public static class InteractionTrackerInertiaRestingValue_stub
{
    public static Windows.UI.Composition.Interactions.InteractionTrackerInertiaRestingValue Create_stub(Windows.UI.Composition.Compositor compositor)
    {
        try
        {
            return Windows.UI.Composition.Interactions.InteractionTrackerInertiaRestingValue.Create(compositor);
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
    public static Windows.UI.Composition.Interactions.VisualInteractionSource Create_stub(Windows.UI.Composition.Visual source)
    {
        try
        {
            return Windows.UI.Composition.Interactions.VisualInteractionSource.Create(source);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void TryRedirectForManipulation_stub(this Windows.UI.Composition.Interactions.VisualInteractionSource sender, Windows.UI.Input.PointerPoint pointerPoint)
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
    public static double GetNamedNumber_stub(this Windows.Data.Json.JsonObject sender, string name, double defaultValue)
    {
        try
        {
            return sender.GetNamedNumber(name, defaultValue);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static double GetNamedNumber_stub(this Windows.Data.Json.JsonObject sender, string name)
    {
        try
        {
            return sender.GetNamedNumber(name);
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
public static class XamlMarkupHelper_stub
{
    public static void UnloadObject_stub(Windows.UI.Xaml.DependencyObject element)
    {
        try
        {
            Windows.UI.Xaml.Markup.XamlMarkupHelper.UnloadObject(element);
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
public static class GeneralTransform_stub
{
    public static Windows.Foundation.Point TransformPoint_stub(this Windows.UI.Xaml.Media.GeneralTransform sender, Windows.Foundation.Point point)
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
public static class LoadedImageSurface_stub
{
    public static Windows.UI.Xaml.Media.LoadedImageSurface StartLoadFromStream_stub(Windows.Storage.Streams.IRandomAccessStream stream)
    {
        try
        {
            return Windows.UI.Xaml.Media.LoadedImageSurface.StartLoadFromStream(stream);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Xaml.Media.LoadedImageSurface StartLoadFromStream_stub(Windows.Storage.Streams.IRandomAccessStream stream, Windows.Foundation.Size desiredMaxSize)
    {
        try
        {
            return Windows.UI.Xaml.Media.LoadedImageSurface.StartLoadFromStream(stream, desiredMaxSize);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Xaml.Media.LoadedImageSurface StartLoadFromUri_stub(System.Uri uri)
    {
        try
        {
            return Windows.UI.Xaml.Media.LoadedImageSurface.StartLoadFromUri(uri);
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
    public static void DisconnectChildrenRecursive_stub(Windows.UI.Xaml.UIElement element)
    {
        try
        {
            Windows.UI.Xaml.Media.VisualTreeHelper.DisconnectChildrenRecursive(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IEnumerable<Windows.UI.Xaml.UIElement> FindElementsInHostCoordinates_stub(Windows.Foundation.Point intersectingPoint, Windows.UI.Xaml.UIElement subtree)
    {
        try
        {
            return Windows.UI.Xaml.Media.VisualTreeHelper.FindElementsInHostCoordinates(intersectingPoint, subtree);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IEnumerable<Windows.UI.Xaml.UIElement> FindElementsInHostCoordinates_stub(Windows.Foundation.Rect intersectingRect, Windows.UI.Xaml.UIElement subtree)
    {
        try
        {
            return Windows.UI.Xaml.Media.VisualTreeHelper.FindElementsInHostCoordinates(intersectingRect, subtree);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Xaml.DependencyObject GetChild_stub(Windows.UI.Xaml.DependencyObject reference, int childIndex)
    {
        try
        {
            return Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(reference, childIndex);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static int GetChildrenCount_stub(Windows.UI.Xaml.DependencyObject reference)
    {
        try
        {
            return Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(reference);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IReadOnlyList<Windows.UI.Xaml.Controls.Primitives.Popup> GetOpenPopups_stub(Windows.UI.Xaml.Window window)
    {
        try
        {
            return Windows.UI.Xaml.Media.VisualTreeHelper.GetOpenPopups(window);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static System.Collections.Generic.IReadOnlyList<Windows.UI.Xaml.Controls.Primitives.Popup> GetOpenPopupsForXamlRoot_stub(Windows.UI.Xaml.XamlRoot xamlRoot)
    {
        try
        {
            return Windows.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Xaml.DependencyObject GetParent_stub(Windows.UI.Xaml.DependencyObject reference)
    {
        try
        {
            return Windows.UI.Xaml.Media.VisualTreeHelper.GetParent(reference);
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
public static class NativeUtils_stub
{
    public static void Crash_stub()
    {
        try
        {
            Telegram.Native.NativeUtils.Crash();
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
    public static string FormatDate_stub(int value, string format)
    {
        try
        {
            return Telegram.Native.NativeUtils.FormatDate(value, format);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static string FormatDate_stub(int year, int month, int day, string format)
    {
        try
        {
            return Telegram.Native.NativeUtils.FormatDate(year, month, day, format);
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
    public static string FormatTime_stub(System.DateTimeOffset value)
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
}
public static class ParticlesAnimation_stub
{
    public static void RenderSync_stub(this Telegram.Native.ParticlesAnimation sender, Windows.Storage.Streams.IBuffer bitmap)
    {
        try
        {
            sender.RenderSync(bitmap);
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
public static class ToastNotificationHistory_stub
{
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
}
public static class ToastNotificationManager_stub
{
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
}
public static class ToastNotificationManagerForUser_stub
{
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
public static class AutomationPeer_stub
{
    public static object GetPattern_stub(this Windows.UI.Xaml.Automation.Peers.AutomationPeer sender, Windows.UI.Xaml.Automation.Peers.PatternInterface patternInterface)
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
    public static bool ListenerExists_stub(Windows.UI.Xaml.Automation.Peers.AutomationEvents eventId)
    {
        try
        {
            return Windows.UI.Xaml.Automation.Peers.AutomationPeer.ListenerExists(eventId);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void RaiseAutomationEvent_stub(this Windows.UI.Xaml.Automation.Peers.AutomationPeer sender, Windows.UI.Xaml.Automation.Peers.AutomationEvents eventId)
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
    public static void RaisePropertyChangedEvent_stub(this Windows.UI.Xaml.Automation.Peers.AutomationPeer sender, Windows.UI.Xaml.Automation.AutomationProperty automationProperty, object oldValue, object newValue)
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
    public static Windows.UI.Xaml.Automation.Peers.AutomationPeer CreatePeerForElement_stub(Windows.UI.Xaml.UIElement element)
    {
        try
        {
            return Windows.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Xaml.Automation.Peers.AutomationPeer FromElement_stub(Windows.UI.Xaml.UIElement element)
    {
        try
        {
            return Windows.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.FromElement(element);
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
public static class CoreAppWindowPreview_stub
{
    public static int GetIdFromWindow_stub(Windows.UI.WindowManagement.AppWindow window)
    {
        try
        {
            return Windows.UI.Core.Preview.CoreAppWindowPreview.GetIdFromWindow(window);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class SystemNavigationCloseRequestedPreviewEventArgs_stub
{
    public static Windows.Foundation.Deferral GetDeferral_stub(this Windows.UI.Core.Preview.SystemNavigationCloseRequestedPreviewEventArgs sender)
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
public static class FlyoutBase_stub
{
    public static Windows.UI.Xaml.Controls.Primitives.FlyoutBase GetAttachedFlyout_stub(Windows.UI.Xaml.FrameworkElement element)
    {
        try
        {
            return Windows.UI.Xaml.Controls.Primitives.FlyoutBase.GetAttachedFlyout(element);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static void Hide_stub(this Windows.UI.Xaml.Controls.Primitives.FlyoutBase sender)
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
    public static void ShowAt_stub(this Windows.UI.Xaml.Controls.Primitives.FlyoutBase sender, Windows.UI.Xaml.DependencyObject placementTarget, Windows.UI.Xaml.Controls.Primitives.FlyoutShowOptions showOptions)
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
    public static void ShowAt_stub(this Windows.UI.Xaml.Controls.Primitives.FlyoutBase sender, Windows.UI.Xaml.FrameworkElement placementTarget)
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
}
public static class IInvokeProvider_stub
{
    public static void Invoke_stub(this Windows.UI.Xaml.Automation.Provider.IInvokeProvider sender)
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
}
public static class Shape_stub
{
    public static Windows.UI.Composition.CompositionBrush GetAlphaMask_stub(this Windows.UI.Xaml.Shapes.Shape sender)
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
public static class DataWriter_stub
{
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
    public static void WriteBytes_stub(this Windows.Storage.Streams.DataWriter sender, byte[] value)
    {
        try
        {
            sender.WriteBytes(value);
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
public static class InMemoryRandomAccessStream_stub
{
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
public static class RandomAccessStreamReference_stub
{
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
}
public static class DispatcherQueue_stub
{
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
public static class Client_stub
{
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
public static class ITextDocument_stub
{
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
public static class ITextRange_stub
{
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
public static class ApplicationView_stub
{
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
public static class AppWindow_stub
{
    public static void ClearPersistedState_stub(string key)
    {
        try
        {
            Windows.UI.WindowManagement.AppWindow.ClearPersistedState(key);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
}
public static class AppWindowPresenter_stub
{
    public static bool IsPresentationSupported_stub(this Windows.UI.WindowManagement.AppWindowPresenter sender, Windows.UI.WindowManagement.AppWindowPresentationKind presentationKind)
    {
        try
        {
            return sender.IsPresentationSupported(presentationKind);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static bool RequestPresentation_stub(this Windows.UI.WindowManagement.AppWindowPresenter sender, Windows.UI.WindowManagement.AppWindowPresentationKind presentationKind)
    {
        try
        {
            return sender.RequestPresentation(presentationKind);
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
public static class DependencyObject_stub
{
    public static void ClearValue_stub(this Windows.UI.Xaml.DependencyObject sender, Windows.UI.Xaml.DependencyProperty dp)
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
    public static object GetValue_stub(this Windows.UI.Xaml.DependencyObject sender, Windows.UI.Xaml.DependencyProperty dp)
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
    public static long RegisterPropertyChangedCallback_stub(this Windows.UI.Xaml.DependencyObject sender, Windows.UI.Xaml.DependencyProperty dp, Windows.UI.Xaml.DependencyPropertyChangedCallback callback)
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
    public static void SetValue_stub(this Windows.UI.Xaml.DependencyObject sender, Windows.UI.Xaml.DependencyProperty dp, object value)
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
    public static void UnregisterPropertyChangedCallback_stub(this Windows.UI.Xaml.DependencyObject sender, Windows.UI.Xaml.DependencyProperty dp, long token)
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
    public static Windows.UI.Xaml.DependencyProperty Register_stub(string name, System.Type propertyType, System.Type ownerType, Windows.UI.Xaml.PropertyMetadata typeMetadata)
    {
        try
        {
            return Windows.UI.Xaml.DependencyProperty.Register(name, propertyType, ownerType, typeMetadata);
        }
        catch (Exception ex)
        {
            Logger.Error(Environment.StackTrace);
            throw new RuntimeException(ex);
        }
    }
    public static Windows.UI.Xaml.DependencyProperty RegisterAttached_stub(string name, System.Type propertyType, System.Type ownerType, Windows.UI.Xaml.PropertyMetadata defaultMetadata)
    {
        try
        {
            return Windows.UI.Xaml.DependencyProperty.RegisterAttached(name, propertyType, ownerType, defaultMetadata);
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
    public static void Start_stub(this Windows.UI.Xaml.DispatcherTimer sender)
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
    public static void Stop_stub(this Windows.UI.Xaml.DispatcherTimer sender)
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
}
public static class DragOperationDeferral_stub
{
    public static void Complete_stub(this Windows.UI.Xaml.DragOperationDeferral sender)
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
public static class DragStartingEventArgs_stub
{
    public static Windows.UI.Xaml.DragOperationDeferral GetDeferral_stub(this Windows.UI.Xaml.DragStartingEventArgs sender)
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
    public static void SetContentFromDataPackage_stub(this Windows.UI.Xaml.DragUI sender)
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
public static class FrameworkElement_stub
{
    public static object FindName_stub(this Windows.UI.Xaml.FrameworkElement sender, string name)
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
    public static Windows.UI.Xaml.Data.BindingExpression GetBindingExpression_stub(this Windows.UI.Xaml.FrameworkElement sender, Windows.UI.Xaml.DependencyProperty dp)
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
    public static void SetBinding_stub(this Windows.UI.Xaml.FrameworkElement sender, Windows.UI.Xaml.DependencyProperty dp, Windows.UI.Xaml.Data.BindingBase binding)
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
public static class UIElement_stub
{
    public static void AddHandler_stub(this Windows.UI.Xaml.UIElement sender, Windows.UI.Xaml.RoutedEvent routedEvent, object handler, bool handledEventsToo)
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
    public static void Arrange_stub(this Windows.UI.Xaml.UIElement sender, Windows.Foundation.Rect finalRect)
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
    public static bool CancelDirectManipulations_stub(this Windows.UI.Xaml.UIElement sender)
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
    public static bool CapturePointer_stub(this Windows.UI.Xaml.UIElement sender, Windows.UI.Xaml.Input.Pointer value)
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
    public static void InvalidateArrange_stub(this Windows.UI.Xaml.UIElement sender)
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
    public static void InvalidateMeasure_stub(this Windows.UI.Xaml.UIElement sender)
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
    public static void Measure_stub(this Windows.UI.Xaml.UIElement sender, Windows.Foundation.Size availableSize)
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
    public static void ReleasePointerCapture_stub(this Windows.UI.Xaml.UIElement sender, Windows.UI.Xaml.Input.Pointer value)
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
    public static void ReleasePointerCaptures_stub(this Windows.UI.Xaml.UIElement sender)
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
    public static void RemoveHandler_stub(this Windows.UI.Xaml.UIElement sender, Windows.UI.Xaml.RoutedEvent routedEvent, object handler)
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
    public static void StartBringIntoView_stub(this Windows.UI.Xaml.UIElement sender)
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
    public static Windows.UI.Xaml.Media.GeneralTransform TransformToVisual_stub(this Windows.UI.Xaml.UIElement sender, Windows.UI.Xaml.UIElement visual)
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
    public static void UpdateLayout_stub(this Windows.UI.Xaml.UIElement sender)
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
}
public static class VisualStateManager_stub
{
    public static bool GoToState_stub(Windows.UI.Xaml.Controls.Control control, string stateName, bool useTransitions)
    {
        try
        {
            return Windows.UI.Xaml.VisualStateManager.GoToState(control, stateName, useTransitions);
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
    public static void Activate_stub(this Windows.UI.Xaml.Window sender)
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
    public static void Close_stub(this Windows.UI.Xaml.Window sender)
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
    public static void SetTitleBar_stub(this Windows.UI.Xaml.Window sender, Windows.UI.Xaml.UIElement value)
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
}
