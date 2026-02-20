//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Navigation;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Media
{
    public abstract partial class PowerSavingBrushBase : XamlCompositionBrushBase
    {
        protected bool m_isConnected;
        protected CompositionBrush m_brush;

        private void PowerSavingPolicy_Changed(object sender, EventArgs e)
        {
            if (m_isConnected)
            {
                try
                {
                    UpdateBrushByDispatcher();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex);
                }
            }
        }

        private void UpdateBrushByDispatcher()
        {
            if (Dispatcher.HasThreadAccess)
            {
                UpdateBrush();
            }
            else
            {
                _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, UpdateBrush);
            }
        }

        private void UpdateBrush()
        {
            if (m_brush is CompositionEffectBrush && !PowerSavingPolicy.AreMaterialsEnabled)
            {
                m_brush.Dispose();
                m_brush = null;
            }
            else if (m_brush is CompositionColorBrush && PowerSavingPolicy.AreMaterialsEnabled)
            {
                m_brush.Dispose();
                m_brush = null;
            }

            if (m_brush == null)
            {
                if (!PowerSavingPolicy.AreMaterialsEnabled)
                {
                    m_brush = BootStrapper.Current.Compositor.CreateColorBrush(FallbackColor);
                    CompositionBrush = m_brush;
                }
                else
                {
                    m_brush = OnUpdateBrush();
                    CompositionBrush = m_brush;
                }
            }
        }

        protected abstract CompositionBrush OnUpdateBrush();

        protected override void OnConnected()
        {
            PowerSavingPolicy.Changed += PowerSavingPolicy_Changed;

            try
            {
                UpdateBrush();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }

            m_isConnected = true;
            base.OnConnected();
        }

        protected override void OnDisconnected()
        {
            PowerSavingPolicy.Changed -= PowerSavingPolicy_Changed;

            m_isConnected = false;

            m_brush?.Dispose();
            m_brush = null;

            CompositionBrush = null;

            base.OnDisconnected();
        }
    }
}
