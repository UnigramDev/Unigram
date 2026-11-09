//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.ApplicationModel;
#if UNPACKAGED
using System.Runtime.InteropServices;
#endif

namespace Telegram.Common
{
    // partial because the source-generated P/Invokes below require it.
    static partial class FeatureTokenGenerator
    {
        static readonly Dictionary<string, string> LimitedAccessFeaturesMap = new()
        {
            { "com.microsoft.windows.windowdecorations", "425261a8-7f73-4319-8a53-fc13f87e1717" },
            { "com.microsoft.windows.composition.engine", "26ef12c7-bf7e-4fa7-ac71-9665b27be6f7" },
            { "com.microsoft.windows.updateorchestrator.1", "20C662033A4007A55375BF00D986C280B41A418F" },
            { "com.microsoft.windows.update.1", "01B2AEA8-7DD5-4066-A081-1E4CD1479CCA" },
            { "com.microsoft.windows.taskbar.requestPinSecondaryTile", "04c19204-10d9-450a-95c4-2910c8f72be3" },
            { "com.microsoft.windows.richeditmath", "RDZCQjY2M0YtQkFDMi00NkIwLUI3NzEtODg4NjMxMEVENkFF" },
            { "com.microsoft.windows.holographic.xrruntime.2", "58AA36EF-7C1A-4A56-9308-FC882F56465A" },
            { "com.microsoft.windows.holographic.xrruntime.1", "036EFF74-8BF2-4249-82AF-92235C6E1A10" },
            { "com.microsoft.windows.holographic.shell", "527f4968-f193-419a-b91f-46b9106e1129" },
            { "com.microsoft.windows.holographic.keyboardcursor_v1", "FE676B8B-E396-4A80-9573-B67542840E5C" },
            { "com.microsoft.windows.callcontrolpublicapi_v1", "6e7e52aa-cddb-4e57-9f1c-7dd511ad7d01" },
            { "com.microsoft.windows.applicationwindow", "e5a85131-319b-4a56-9577-1c1d9c781218" },
            { "com.microsoft.windows.applicationmodel.phonelinetransportdevice_v1", "cb9WIvVfhp+8lFhaSrB6V6zUBGqctteKi/f/9AIeoZ4" },
            { "com.microsoft.windows.applicationmodel.conversationalagent_v1", "hhrovbOc/z8TgeoWheL4RF5vLLJrKNAQpdyvhlTee6I" },
            { "com.microsoft.services.cortana.cortanaactionableinsights_v1", "nEVyyzytE6ankNk1CIAu6sZsh8vKLw3Q7glTOHB11po=" }
        };

#if UNPACKAGED
        // Without a package, Windows unlocks for the name in the exe's Identity resource, which the
        // build writes (LimitedAccessFeatureIdentity in Telegram.WinUI.csproj). Null if it is absent.
        private static string FamilyName { get; } = ApiInfo.HasPackageIdentity
            ? AppInfo.Current.PackageFamilyName
            : ReadIdentityResource();

        [LibraryImport("kernel32.dll", EntryPoint = "FindResourceW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr FindResource(IntPtr module, string name, string type);

        [LibraryImport("kernel32.dll")]
        private static partial IntPtr LoadResource(IntPtr module, IntPtr resource);

        [LibraryImport("kernel32.dll")]
        private static partial IntPtr LockResource(IntPtr data);

        [LibraryImport("kernel32.dll")]
        private static partial uint SizeofResource(IntPtr module, IntPtr resource);

        private static string ReadIdentityResource()
        {
            var resource = FindResource(IntPtr.Zero, "Identity", "LimitedAccessFeature");
            if (resource == IntPtr.Zero)
            {
                return null;
            }

            var data = LockResource(LoadResource(IntPtr.Zero, resource));
            if (data == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(data, (int)SizeofResource(IntPtr.Zero, resource) / sizeof(char));
        }
#else
        private static string FamilyName => AppInfo.Current.PackageFamilyName;
#endif

        private static string GenerateTokenFromFeatureId(string featureId)
            => GenerateFeatureToken(featureId, LimitedAccessFeaturesMap[featureId], FamilyName);

        private static string GenerateAttestation(string featureId)
            => $"{FamilyName.Split('_').Last()} has registered their use of {featureId} with Microsoft and agrees to the terms of use.";

        private static string GenerateFeatureToken(string featureId, string featureKey, string packageIdentity)
        {
            var fullBytes = Encoding.UTF8.GetBytes($"{featureId}!{featureKey}!{packageIdentity}");
            var tokenBytes = new byte[16];
            using (var shaCsp = new System.Security.Cryptography.SHA256CryptoServiceProvider())
            {
                shaCsp.TransformFinalBlock(fullBytes, 0, fullBytes.Length);
                Array.Copy(shaCsp.Hash, tokenBytes, tokenBytes.Length);
            }

            return Convert.ToBase64String(tokenBytes);
        }

        public static bool TryUnlockFeature(string featureId)
        {
            try
            {
                if (string.IsNullOrEmpty(FamilyName))
                {
                    return false;
                }

                var token = GenerateTokenFromFeatureId(featureId);
                var attestation = GenerateAttestation(featureId);
                var accessResult = LimitedAccessFeatures.TryUnlockFeature(featureId, token, attestation);

                return accessResult.Status == LimitedAccessFeatureStatus.Available
                    || accessResult.Status == LimitedAccessFeatureStatus.AvailableWithoutToken;
            }
            catch
            {
                return false;
            }
        }
    }
}
