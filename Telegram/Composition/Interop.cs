using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Windows.UI.Composition;

namespace Telegram.Composition
{
    [Guid("F26DA89E-683D-4C67-AEA7-BA29B2217A70")]
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    interface ICompositionVisualSurfacePartner
    {
        Vector2 RealizationSize { get; set; }
        CompositionStretch Stretch { get; set; }
        void Freeze();
    }
}
