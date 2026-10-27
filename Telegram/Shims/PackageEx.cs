//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Telegram.Common;
// Two aliases, for two different collisions. The runtime one cannot be spelled out, because
// inside this namespace a qualified System.Runtime.InteropServices binds to Windows.System first,
// which has no Runtime under it - and it cannot be called Architecture either, because a bare
// Architecture would then bind to the property below before the type. The WinRT one cannot be
// imported as a namespace, because System.Reflection has a ProcessorArchitecture of its own.
using ProcessorArchitecture = Windows.System.ProcessorArchitecture;
using RuntimeArchitecture = System.Runtime.InteropServices.Architecture;

namespace Windows.ApplicationModel
{
    /// <summary>
    /// The five things the app asks <see cref="Package.Current"/> for, answered without it. The
    /// property that throws is <c>Package.Current</c> itself, so every one of them is a crash on
    /// an unpackaged launch rather than a feature that quietly does nothing.
    ///
    /// The types are the ones the projection uses, so the call sites read the same either way.
    /// </summary>
    public static class PackageEx
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

        /// <summary>
        /// Where the payload is. <c>AppContext.BaseDirectory</c> ends in a separator and
        /// <c>InstalledLocation.Path</c> does not, and the difference reaches string
        /// concatenations, so it is trimmed here rather than at nine call sites.
        /// </summary>
        private static string _installedPath;
        public static string InstalledPath => _installedPath ??= _packaged
            ? Package.Current.InstalledLocation.Path
            : AppContext.BaseDirectory.TrimEnd('\\');
#else
        public static PackageVersion Version => Package.Current.Id.Version;

        public static ProcessorArchitecture Architecture => Package.Current.Id.Architecture;

        public static string FamilyName => Package.Current.Id.FamilyName;

        public static PackageSignatureKind SignatureKind => Package.Current.SignatureKind;

        public static string InstalledPath => Package.Current.InstalledLocation.Path;
#endif

        /// <summary>
        /// A path to something in the payload, as <c>InstalledLocation.GetFileAsync</c> takes one:
        /// relative, and with backslashes.
        /// </summary>
        public static string GetInstalledPath(string path)
        {
            return Path.Combine(InstalledPath, path);
        }

#if UNPACKAGED
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
#endif
    }
}
