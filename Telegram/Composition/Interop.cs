//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
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
