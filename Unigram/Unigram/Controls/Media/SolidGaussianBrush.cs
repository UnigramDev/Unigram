using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.System;
using Windows.System.Power;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Media
{
    public class SolidGaussianBrush : XamlCompositionBrushBase
    {
        private bool m_isConnected;
        private bool m_isDisabledByPolicy;
        private CompositionBrush m_brush;

        private readonly bool m_energySaverStatusChangedRevokerValid;
        private readonly CompositionCapabilities m_compositionCapabilities;
        private readonly UISettings m_uiSettings;

        private readonly DispatcherQueue m_dispatcher;

        public SolidGaussianBrush()
        {
            m_dispatcher = DispatcherQueue.GetForCurrentThread();

            try
            {
                PowerManager.EnergySaverStatusChanged += PowerManager_EnergySaverStatusChanged;
                m_energySaverStatusChangedRevokerValid = true;
            }
            catch
            {

            }

            m_compositionCapabilities = CompositionCapabilities.GetForCurrentView();
            m_compositionCapabilities.Changed += CompositionCapabilities_Changed;

            m_uiSettings = new UISettings();
            m_uiSettings.AdvancedEffectsEnabledChanged += UISettings_AdvancedEffectsEnabledChanged;

            UpdatePolicy();
        }

        private void PowerManager_EnergySaverStatusChanged(object sender, object e)
        {
            UpdatePolicyByDispatcher();
        }

        private void CompositionCapabilities_Changed(CompositionCapabilities sender, object args)
        {
            UpdatePolicyByDispatcher();
        }

        private void UISettings_AdvancedEffectsEnabledChanged(UISettings sender, object args)
        {
            UpdatePolicyByDispatcher();
        }

        private void UpdatePolicyByDispatcher()
        {
            if (m_dispatcher.HasThreadAccess)
            {
                UpdatePolicy();
            }
            else
            {
                m_dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, UpdatePolicy);
            }
        }

        // Internal MUX logic: https://github.com/microsoft/microsoft-ui-xaml/blob/main/dev/Lights/MaterialHelper.cpp
        private void UpdatePolicy()
        {
            var isEnergySaverMode = m_energySaverStatusChangedRevokerValid ? PowerManager.EnergySaverStatus == EnergySaverStatus.On : true;
            var areEffectsFast = m_compositionCapabilities != null && m_compositionCapabilities.AreEffectsFast();
            var advancedEffectsEnabled = m_uiSettings == null || m_uiSettings.AdvancedEffectsEnabled;

            var isDisabledByPolicy = isEnergySaverMode || !areEffectsFast || !advancedEffectsEnabled;

            if (m_isConnected && m_isDisabledByPolicy != isDisabledByPolicy)
            {
                m_isDisabledByPolicy = isDisabledByPolicy;
                UpdateBrush();
            }
            else
            {
                m_isDisabledByPolicy = isDisabledByPolicy;
            }
        }

        private void UpdateBrush()
        {
            if (m_isDisabledByPolicy && m_brush is CompositionEffectBrush)
            {
                m_brush.Dispose();
                m_brush = null;
            }
            else if (m_brush is CompositionColorBrush && !m_isDisabledByPolicy)
            {
                m_brush.Dispose();
                m_brush = null;
            }

            if (m_brush == null)
            {
                if (m_isDisabledByPolicy)
                {
                    m_brush = Window.Current.Compositor.CreateColorBrush(FallbackColor);
                    CompositionBrush = m_brush;
                }
                else
                {
                    var gaussianBlur = new GaussianBlurEffect
                    {
                        Name = "Blur",
                        BlurAmount = 30,
                        Optimization = EffectOptimization.Speed,
                        BorderMode = EffectBorderMode.Hard,
                        Source = new CompositionEffectSourceParameter("Backdrop")
                    };

                    var saturationEffect = new SaturationEffect
                    {
                        Name = "Saturation",
                        Saturation = 1.7f,
                        Source = gaussianBlur
                    };

                    var tintColorEffect = new ColorSourceEffect
                    {
                        Name = "TintColor",
                        Color = Color.FromArgb(52, 0, 0, 0)
                    };

                    var compositeEffect = new CompositeEffect();
                    compositeEffect.Mode = CanvasComposite.SourceOver;
                    compositeEffect.Sources.Add(saturationEffect);
                    compositeEffect.Sources.Add(tintColorEffect);

                    var effectFactory = Window.Current.Compositor.CreateEffectFactory(compositeEffect);
                    var backdrop = Window.Current.Compositor.CreateBackdropBrush();

                    var brush = effectFactory.CreateBrush();
                    brush.SetSourceParameter("Backdrop", backdrop);

                    m_brush = brush;
                    CompositionBrush = m_brush;
                }
            }
        }

        protected override void OnConnected()
        {
            UpdateBrush();

            m_isConnected = true;
            base.OnConnected();
        }

        protected override void OnDisconnected()
        {
            m_isConnected = false;

            if (m_brush != null)
            {
                m_brush.Dispose();
                m_brush = null;
            }

            CompositionBrush = null;

            base.OnDisconnected();
        }
    }
}
