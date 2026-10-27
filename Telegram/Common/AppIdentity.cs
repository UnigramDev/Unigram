//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Telegram.Common;
using Windows.ApplicationModel;
// ProcessorArchitecture is aliased because System.Reflection has one of its own, and the runtime
// Architecture because a bare Architecture binds to the property below before the type.
using ProcessorArchitecture = Windows.System.ProcessorArchitecture;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;

namespace Telegram
{
    /// <summary>
    /// What the app asks <see cref="Package.Current"/> about itself, answered without it. The
    /// property that throws is <c>Package.Current</c> itself, so every one of them is a crash on
    /// an unpackaged launch rather than a feature that quietly does nothing.
    ///
    /// The types are the ones the projection uses, so the call sites read the same either way.
    /// </summary>
    public static class AppIdentity
    {
#if UNPACKAGED
        private static readonly bool _packaged = ApiInfo.HasPackageIdentity;

        /// <summary>
        /// Unpackaged this comes from the assembly, which the build stamps from the manifest the
        /// packaged flavours are signed with - so the two agree rather than merely resembling
        /// each other.
        /// </summary>
        private static PackageVersion? _version;
        public static PackageVersion Version => _version ??= _packaged
            ? Package.Current.Id.Version
            : FromAssembly(Assembly.GetExecutingAssembly().GetName().Version);

        private static ProcessorArchitecture? _architecture;
        public static ProcessorArchitecture Architecture => _architecture ??= _packaged
            ? Package.Current.Id.Architecture
            : RuntimeInformation.ProcessArchitecture switch
            {
                RuntimeArchitecture.X64 => ProcessorArchitecture.X64,
                RuntimeArchitecture.Arm64 => ProcessorArchitecture.Arm64,
                RuntimeArchitecture.X86 => ProcessorArchitecture.X86,
                _ => ProcessorArchitecture.Unknown
            };

        /// <summary>
        /// Empty without an identity, and every caller has to treat that as "there is nobody to
        /// name": a family name addresses the shell's idea of this app, and unpackaged the shell
        /// has none.
        /// </summary>
        private static string _familyName;
        public static string FamilyName => _familyName ??= _packaged
            ? Package.Current.Id.FamilyName
            : string.Empty;

        private static PackageSignatureKind? _signatureKind;
        public static PackageSignatureKind SignatureKind => _signatureKind ??= _packaged
            ? Package.Current.SignatureKind
            : PackageSignatureKind.None;

        // Build and Revision are -1 when the assembly version has fewer than four parts, and every
        // field here is a ushort where System.Version's are ints.
        private static PackageVersion FromAssembly(Version version)
        {
            return new PackageVersion
            {
                Major = Clamp(version.Major),
                Minor = Clamp(version.Minor),
                Build = Clamp(version.Build),
                Revision = Clamp(version.Revision)
            };
        }

        private static ushort Clamp(int value)
        {
            return value < 0 ? (ushort)0 : value > ushort.MaxValue ? ushort.MaxValue : (ushort)value;
        }
#else
        public static PackageVersion Version => Package.Current.Id.Version;

        public static ProcessorArchitecture Architecture => Package.Current.Id.Architecture;

        public static string FamilyName => Package.Current.Id.FamilyName;

        public static PackageSignatureKind SignatureKind => Package.Current.SignatureKind;
#endif
    }
}
