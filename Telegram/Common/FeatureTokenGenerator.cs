using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.ApplicationModel;

namespace Telegram.Common
{
    static class FeatureTokenGenerator
    {
        static readonly Dictionary<string, string> LimitedAccessFeaturesMap = new Dictionary<string, string>()
        {
            { "com.microsoft.windows.windowdecorations", "425261a8-7f73-4319-8a53-fc13f87e1717" },
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

        private static string GenerateTokenFromFeatureId(string featureId)
            => GenerateFeatureToken(featureId, LimitedAccessFeaturesMap[featureId], AppInfo.Current.PackageFamilyName);

        private static string GenerateAttestation(string featureId)
            => $"{AppInfo.Current.PackageFamilyName.Split('_').Last()} has registered their use of {featureId} with Microsoft and agrees to the terms of use.";

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
