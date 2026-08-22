//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.CodeAnalysis;

namespace Telegram.Generators
{
    internal static class WinRTProjection
    {
        // What CsWinRT stamps on a projected type. Matching on these rather than on the namespace,
        // because the projection for a referenced WinRT component is generated into the consuming
        // assembly: Telegram.Native.Direct2DDevice is a type of this compilation and looks
        // managed by every other measure.
        private static readonly string[] Attributes =
        {
            "WindowsRuntimeTypeAttribute",
            "ProjectedRuntimeClassAttribute",
            "WindowsRuntimeHelperTypeAttribute",
        };

        public static bool IsProjected(ITypeSymbol type)
        {
            if (type == null)
            {
                return false;
            }

            foreach (var attribute in type.OriginalDefinition.GetAttributes())
            {
                var declaring = attribute.AttributeClass;
                if (declaring?.ContainingNamespace?.Name != "WinRT")
                {
                    continue;
                }

                foreach (var name in Attributes)
                {
                    if (declaring.Name == name)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
