//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Telegram.Native;
using Telegram.Services;
using Windows.ApplicationModel;

namespace Telegram.Common
{
    public static class ExceptionSerializer
    {
        private static readonly IDeviceInfoService _service = new DeviceInfoService();

        public static string Serialize(System.Exception exception, string id, string userId, string logs)
        {
            var hashBuilder = new StringBuilder();
            var binaries = new Dictionary<long, ExceptionBinary>();
            var modelException = ProcessException(exception, null, binaries, hashBuilder);

            var error = new ErrorExceptionAndBinaries
            {
                Binaries = binaries.Count > 0 ? binaries.Values.ToList() : null,
                Exception = modelException,
            };

            foreach (var binary in binaries.Values.OrderBy(x => x.Name))
            {
                hashBuilder.Append(binary.Name.ToLowerInvariant());
            }

            return Serialize(error, id, userId, logs, hashBuilder);
        }

        public static string Serialize(FatalError exception, string id, string userId, string logs)
        {
            var hashBuilder = new StringBuilder();
            var binaries = new Dictionary<long, ExceptionBinary>();
            var modelException = ProcessException(exception, null, binaries, hashBuilder);

            var error = new ErrorExceptionAndBinaries
            {
                Binaries = binaries.Count > 0 ? binaries.Values.ToList() : null,
                Exception = modelException,
            };

            foreach (var binary in binaries.Values.OrderBy(x => x.Name))
            {
                hashBuilder.Append(binary.Name.ToLowerInvariant());
            }

            return Serialize(error, id, userId, logs, hashBuilder);
        }

        private static string Serialize(ErrorExceptionAndBinaries error, string id, string userId, string logs, StringBuilder hashBuilder)
        {
            var report = new ErrorReport
            {
                Id = id,
                UserId = userId,
                ApplicationVersion = _service.ApplicationVersion2,
                ApplicationArchitecture = Package.Current.Id.Architecture.ToString(),
                SystemVersion = _service.SystemVersion2,
                DeviceModel = _service.DeviceModel,
                Type = error.Exception.Type,
                Message = error.Exception.Message,
                ExitPoint = error.Exception.StackTrace,
                StackTrace = error,
                // The backend's unescaping drops the backslash off \r and leaves the letter behind,
                // so every CR has to come off before the report goes up - the same normalisation
                // ProcessException already does to the message and the stack trace.
                LogTail = logs?.Replace("\r\n", "\n"),
                Time = MonotonicUnixTime.Now,
                LaunchTime = WatchDog.LaunchTime
            };

            hashBuilder.Append(report.ApplicationVersion);
            hashBuilder.Append(report.Type.ToLowerInvariant());

            var lineBreak = report.Message.IndexOf('\n');
            if (lineBreak != -1)
            {
                hashBuilder.Append(report.Message[..lineBreak].ToLowerInvariant());
            }
            else
            {
                hashBuilder.Append(report.Message.ToLowerInvariant());
            }

            report.GroupHash = ComputeHash(hashBuilder.ToString());

            return JsonSerializer.Serialize(report, ErrorJsonContext.Default.ErrorReport);
        }

        private static string ComputeHash(string input)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert byte array to hexadecimal string
                StringBuilder sb = new();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2")); // "x2" for lowercase hex
                }
                return sb.ToString();
            }
        }

        // Every asynchronous fault that reaches UnhandledErrorDetected carries the same message
        // pump frames, so for those reports this text is the only thing that names the origin.
        // Only frames with a method signature: the description above them arrives in the user's
        // language, a native backtrace renders as "at module.dll+0x..." and is already covered by
        // the binary names, and the offsets are per-build noise.
        private static void AppendStackTraceHash(StringBuilder hashBuilder, string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return;
            }

            foreach (var line in stackTrace.Split('\n'))
            {
                var frame = line.Trim();

                if (!frame.StartsWith("at ", StringComparison.Ordinal) || !frame.Contains('('))
                {
                    continue;
                }

                var plus = frame.LastIndexOf('+');
                if (plus > 0 && frame.IndexOf("0x", plus, StringComparison.Ordinal) > 0)
                {
                    frame = frame.Substring(0, plus).TrimEnd();
                }

                hashBuilder.Append(frame);
            }
        }

        private static ExceptionModel ProcessException(System.Exception exception, ExceptionModel outerException, Dictionary<long, ExceptionBinary> seenBinaries, StringBuilder hashBuilder)
        {
            var type = exception.GetType().Name;
            var modelException = new ExceptionModel
            {
                Type = type,
                Message = TranslateMessage(exception.Message.Replace("\r\n", "\n"), type, exception.HResult),
                StackTrace = exception.StackTrace?.Replace("\r\n", "\n")
            };
            if (exception is AggregateException aggregateException)
            {
                if (aggregateException.InnerExceptions.Count != 0)
                {
                    modelException.InnerExceptions = new List<ExceptionModel>();
                    foreach (var innerException in aggregateException.InnerExceptions)
                    {
                        ProcessException(innerException, modelException, seenBinaries, hashBuilder);
                    }
                }
            }
            if (exception.InnerException != null)
            {
                modelException.InnerExceptions = modelException.InnerExceptions ?? new List<ExceptionModel>();
                ProcessException(exception.InnerException, modelException, seenBinaries, hashBuilder);
            }

            var stackTrace = new StackTrace(exception, true);
            var frames = stackTrace.GetFrames();

            // If there are native frames available, process them to extract image information and frame addresses.
            // The check looks odd, but there is a possibility of frames being null or empty both.
            if (frames != null && frames.Length > 0 && frames[0].HasNativeImage())
            {
                foreach (var frame in frames)
                {
                    // Get stack frame address.
                    var nativeIP = frame.GetNativeIP().ToInt64();
                    var crashFrame = new ExceptionStackFrame
                    {
                        Address = string.Format(CultureInfo.InvariantCulture, AddressFormat, nativeIP),
                    };

                    modelException.Frames ??= new();
                    modelException.Frames.Add(crashFrame);

                    // Process binary.
                    var nativeImageBase = frame.GetNativeImageBase().ToInt64();
                    if (nativeImageBase == 0)
                    {
                        continue;
                    }

                    void AppendHash(ExceptionBinary binary)
                    {
                        if (_builtinBinaries.Contains(binary.Name))
                        {
                            hashBuilder.Append(binary.Name.ToLowerInvariant());
                            hashBuilder.Append(nativeIP - nativeImageBase);
                        }
                    }

                    if (seenBinaries.TryGetValue(nativeImageBase, out ExceptionBinary binary))
                    {
                        AppendHash(binary);
                    }
                    else
                    {
                        binary = ImageToBinary(frame.GetNativeImageBase());

                        if (binary != null)
                        {
                            seenBinaries[nativeImageBase] = binary;
                            AppendHash(binary);
                        }
                    }
                }
            }
            else
            {
                hashBuilder.Append(exception.StackTrace);
            }

            outerException?.InnerExceptions.Add(modelException);
            return modelException;
        }

        private static ExceptionModel ProcessException(FatalError exception, ExceptionModel outerException, Dictionary<long, ExceptionBinary> seenBinaries, StringBuilder hashBuilder)
        {
            var modelException = new ExceptionModel
            {
                Type = exception.Type,
                // FatalError has no HRESULT of its own, so the lookup falls back to the one .NET
                // printed into the message, when it printed one.
                Message = TranslateMessage(exception.Message.Replace("\r\n", "\n"), exception.Type, 0),
                StackTrace = exception.StackTrace?.Replace("\r\n", "\n")
            };

            // The frames below are the pump for anything that arrived through the stowed path, so
            // without this two unrelated faults sharing a generic HRESULT message group as one.
            AppendStackTraceHash(hashBuilder, exception.StackTrace);

            if (exception.InnerException != null)
            {
                modelException.InnerExceptions ??= new List<ExceptionModel>();
                ProcessException(exception.InnerException, modelException, seenBinaries, hashBuilder);
            }

            foreach (var frame in exception.Frames)
            {
                // Get stack frame address.
                var nativeIP = frame.NativeIP;
                var crashFrame = new ExceptionStackFrame
                {
                    Address = string.Format(CultureInfo.InvariantCulture, AddressFormat, frame.NativeIP),
                };

                modelException.Frames ??= new();
                modelException.Frames.Add(crashFrame);

                // Process binary.
                var nativeImageBase = frame.NativeImageBase;
                if (nativeImageBase == 0)
                {
                    continue;
                }

                void AppendHash(ExceptionBinary binary)
                {
                    if (_builtinBinaries.Contains(binary.Name))
                    {
                        hashBuilder.Append(binary.Name.ToLowerInvariant());
                        hashBuilder.Append(nativeIP - nativeImageBase);
                    }
                }

                if (seenBinaries.TryGetValue(nativeImageBase, out ExceptionBinary binary))
                {
                    AppendHash(binary);
                }
                else
                {
                    binary = ImageToBinary((IntPtr)frame.NativeImageBase);

                    if (binary != null)
                    {
                        seenBinaries[nativeImageBase] = binary;
                        AppendHash(binary);
                    }
                }
            }

            outerException?.InnerExceptions.Add(modelException);
            return modelException;
        }

        private const string AddressFormat = "0x{0:x16}";

        // A dword, which is short for "double word," is a data type definition that is specific to Microsoft Windows. As defined in the file windows.h, a dword is an unsigned, 32-bit unit of data.
        private const int DWordSize = 4;

        // These constants come from the PE format described in documentation: https://docs.microsoft.com/en-us/windows/win32/debug/pe-format.

        // Optional Header Windows-Specific field: SizeOfImage is located at the offset 56.
        private const int SizeOfImageOffset = 56;

        // At location 0x3c, the stub has the file offset to the PE signature. This information enables Windows to properly execute the image file.
        private const int SignatureOffsetLocation = 0x3C;

        // At the beginning of an object file, or immediately after the signature of an image file, is a standard COFF file header of 20 bytes.
        private const int COFFFileHeaderSize = 20;

        // Size in bytes of the address that is relative to the image base of the beginning-of-code section when it is loaded into memory.
        private const int BaseOfDataSize = 4;

        private static unsafe ExceptionBinary ImageToBinary(IntPtr imageBase)
        {
            var imageSize = GetImageSize(imageBase);
            using (var reader = new PEReader((byte*)imageBase.ToPointer(), imageSize, true))
            {
                var debugDir = reader.ReadDebugDirectory();

                // In some cases debugDir can be empty even though frame.GetNativeImageBase() returns a value.
                if (debugDir.IsEmpty)
                {
                    return null;
                }
                var codeViewEntry = debugDir.First(entry => entry.Type == DebugDirectoryEntryType.CodeView);

                // When attaching a debugger in release, it will break into MissingRuntimeArtifactException, just click continue as it is actually caught and recovered by the lib.
                var codeView = reader.ReadCodeViewDebugDirectoryData(codeViewEntry);
                var pdbPath = Path.GetFileName(codeView.Path);
                var endAddress = imageBase + reader.PEHeaders.PEHeader.SizeOfImage;
                return new ExceptionBinary
                {
                    StartAddress = string.Format(CultureInfo.InvariantCulture, AddressFormat, imageBase.ToInt64()),
                    EndAddress = string.Format(CultureInfo.InvariantCulture, AddressFormat, endAddress.ToInt64()),
                    Path = pdbPath,
                    Name = string.IsNullOrEmpty(pdbPath) == false ? Path.GetFileNameWithoutExtension(pdbPath) : null,
                    Id = string.Format(CultureInfo.InvariantCulture, "{0:N}-{1}", codeView.Guid, codeView.Age)
                };
            }
        }

        private static int GetImageSize(IntPtr imageBase)
        {
            var peHeaderBytes = new byte[DWordSize];
            Marshal.Copy(imageBase + SignatureOffsetLocation, peHeaderBytes, 0, peHeaderBytes.Length);
            var peHeaderOffset = BitConverter.ToInt32(peHeaderBytes, 0);
            var peOptionalHeaderOffset = peHeaderOffset + BaseOfDataSize + COFFFileHeaderSize;
            var peOptionalHeaderBytes = new byte[DWordSize];
            Marshal.Copy(imageBase + peOptionalHeaderOffset + SizeOfImageOffset, peOptionalHeaderBytes, 0, peOptionalHeaderBytes.Length);
            return BitConverter.ToInt32(peOptionalHeaderBytes, 0);
        }

        private static string[] _builtinBinaries = new[]
        {
            "avcodec-61",
            "avformat-61",
            "avutil-59",
            "clrcompression",
            "dav1d",
            "jpeg62",
            "libaudio_format_plugin",
            "libavcodec_plugin",
            "libcache_block_plugin",
            "libcache_read_plugin",
            "libcrypto-3-x64",
            "libd3d11va_plugin",
            "libdav1d_plugin",
            "libdirect3d11_plugin",
            "libes_plugin",
            "libfaad_plugin",
            "libflac_plugin",
            "libflacsys_plugin",
            "libfloat_mixer_plugin",
            "libhttp_plugin",
            "libhttps_plugin",
            "libimem_plugin",
            "libmemory_keystore_plugin",
            "libmp4_plugin",
            "libmpg123_plugin",
            "libogg_plugin",
            "libopus_plugin",
            "libpacketizer_flac_plugin",
            "libpacketizer_h264_plugin",
            "libpacketizer_mpegaudio_plugin",
            "libpacketizer_mpegvideo_plugin",
            "libps_plugin",
            "librecord_plugin",
            "libsamplerate_plugin",
            "libscaletempo_plugin",
            "libskiptags_plugin",
            "libssl-3-x64",
            "libswscale_plugin",
            "libtdummy_plugin",
            "libtrivial_channel_mixer_plugin",
            "libugly_resampler_plugin",
            "libvlc",
            "libvlccore",
            "libwasapi_plugin",
            "libwinstore_plugin",
            "libyuv",
            "libyuvp_plugin",
            "lz4",
            "Microsoft.Graphics.Canvas",
            "Microsoft.Web.WebView2.Core",
            "ogg",
            "opus",
            "RLottie",
            "swresample-5",
            "swscale-8",
            "tdjson",
            "Telegram",
            "Telegram.Native.Calls",
            "Telegram.Native",
            "WebView2Loader",
            "zlib1",
        };

        private static string TranslateMessage(string message, string type, int hresult)
        {
            var parts = message.Split('\n');
            var builder = new StringBuilder();

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                var part = parts[i];

                if (TryTranslateAstaCall(part, out string asta))
                {
                    builder.Append(asta);
                    continue;
                }

                if (TryTranslateTdlibLog(part, out string tdlib))
                {
                    builder.Append(tdlib);
                    continue;
                }

                // Only the sentence is localised, so the HRESULT .NET appends has to come off
                // before it can be matched and go back on afterwards.
                var suffix = _hresultSuffix.Match(part);
                var sentence = suffix.Success ? part.Substring(0, suffix.Index) : part;

                // Only the first line can be the system text: when a WinRT error carries an
                // originating description, .NET puts it on a line of its own after it, and that
                // description is the part that says which call failed.
                if (i == 0 && TryTranslateHResult(type, hresult, suffix, out string canonical))
                {
                    builder.Append(canonical);
                }
                else
                {
                    builder.Append(TranslateText(sentence));
                }

                if (suffix.Success)
                {
                    builder.Append(suffix.Value);
                }
            }

            return builder.ToString();
        }

        // Anchored at the end and matched in full, because the sentence in front can contain
        // parentheses of its own - the Portuguese RPC_E_WRONG_THREAD text says "(marshall)".
        //
        // Two spellings: .NET Native writes "(Exception from HRESULT: 0x80004005)", CsWinRT writes
        // "(0x80004005)". Matching only the first left every message on the AOT build with its
        // suffix still attached, so no sentence ever matched TranslateText and every language
        // fragmented into its own group.
        private static readonly Regex _hresultSuffix = new(@"\s*\((?:Exception from HRESULT: )?0x([0-9A-Fa-f]{8})\)$", RegexOptions.Compiled);

        /// <summary>
        /// Rebuilds a message from its HRESULT, which is the same number in every language, rather
        /// than matching the sentence Windows produced in the user's own.
        /// </summary>
        private static bool TryTranslateHResult(string type, int hresult, Match suffix, out string translated)
        {
            // The message of a managed exception is .NET's own text and only starts with the system
            // wording, if at all: NullReferenceException carries E_POINTER, ArgumentException carries
            // E_INVALIDARG, InvalidCastException carries E_NOINTERFACE. Rewriting those from the code
            // would turn "Object reference not set to an instance of an object." into "Invalid
            // pointer" and merge every one of them into a single group. Exception and COMException
            // are the two whose message is the system text and nothing else.
            if (type != "Exception" && type != "COMException")
            {
                translated = null;
                return false;
            }

            // The FatalError path has no HRESULT to pass, so recover it from the message when .NET
            // appended one there.
            if (hresult == 0 && suffix.Success
                && uint.TryParse(suffix.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed))
            {
                hresult = (int)parsed;
            }

            translated = TranslateHResult((uint)hresult);
            return translated != null;
        }

        /// <summary>
        /// The English text Windows would have produced for a system error code.
        /// </summary>
        /// <remarks>
        /// Only genuine system codes belong here. COR_E_* (0x8013xxxx) must never be added: it is
        /// what an exception the app threw itself carries, and its message is one we wrote.
        ///
        /// E_FAIL is deliberately absent, and so are the DirectWrite and Direct2D codes.
        /// CoreApplication.UnhandledErrorDetected flattens the propagated exception to E_FAIL while
        /// the message keeps the wording of the original failure, so those reports arrive with a font
        /// or render-target sentence and an HRESULT of 0x80004005 - and taking the code at its word
        /// would replace the one meaningful part with "Unspecified error". Their sentences stay in
        /// <see cref="TranslateText"/>, which is where those two families are actually handled.
        /// </remarks>
        private static string TranslateHResult(uint hresult)
        {
            switch (hresult)
            {
                // The old-style OLE codes, still returned by parts of the shell and of XAML.
                case 0x80000005: return "Invalid pointer";
                case 0x80000007: return "Operation aborted";
                case 0x8000000B: return "The operation attempted to access data outside the valid range";
                case 0x80000013: return "The object has been closed.";
                case 0x80000016: return "The text associated with this error code could not be found.";
                case 0x80000019: return "An async operation was not properly started.";

                case 0x80004003: return "Invalid pointer";
                case 0x80004004: return "Operation aborted";
                case 0x8000FFFF: return "Catastrophic failure";

                case 0x80010001: return "Call was rejected by callee.";
                case 0x80010108: return "The object invoked has disconnected from its clients.";
                case 0x8001010A: return "The message filter indicated that the application is busy.";
                case 0x8001010E: return "The application called an interface that was marshalled for a different thread.";
                case 0x8001011B: return "Access is denied.";
                case 0x8002000A: return "Out of present range.";
                case 0x8002802B: return "Element not found.";
                case 0x80040153: return "Invalid value for registry";
                case 0x80040154: return "Class not registered";
                case 0x80040155: return "Interface not registered";
                case 0x800401D4: return "CloseClipboard Failed";
                case 0x80040201: return "An event was unable to invoke any of the subscribers";
                case 0x80080005: return "Server execution failed";
                case 0x80090027: return "The parameter is incorrect.";

                case 0x80070002: return "The system cannot find the file specified.";
                case 0x80070005: return "Access is denied.";
                case 0x80070008: return "Not enough memory resources are available to process this command.";
                case 0x8007000E: return "Not enough memory resources are available to complete this operation.";
                case 0x80070020: return "The process cannot access the file because it is being used by another process.";
                case 0x80070057: return "The parameter is incorrect.";
                case 0x80070070: return "There is not enough space on the disk.";
                case 0x8007007A: return "The data area passed to a system call is too small.";
                case 0x8007007E: return "The specified module could not be found.";
                case 0x800700C1: return "%1 is not a valid Win32 application.";
                case 0x800703FA: return "Illegal operation attempted on a registry key that has been marked for deletion.";
                case 0x80070422: return "The service cannot be started, either because it is disabled or because it has no enabled devices associated with it.";
                case 0x80070459: return "No mapping for the Unicode character exists in the target multi-byte code page.";
                case 0x80070490: return "Element not found.";
                case 0x800705AA:
                case 0x800705AB:
                case 0x800705AC: return "Insufficient system resources exist to complete the requested service.";
                case 0x800705AF: return "The paging file is too small for this operation to complete.";
                case 0x800706BA: return "The RPC server is unavailable.";
                case 0x800706BE: return "The remote procedure call failed.";
                case 0x800710DD: return "The operation identifier is not valid.";
                case 0x8007139F: return "The group or resource is not in the correct state to perform the requested operation.";
                case 0x80073D5B: return "The package does not have a mutable directory.";

                // Not in the system message table: XAML returns this for a failure raised while
                // parsing or applying markup, and the wording is the one those reports arrive with.
                case 0x800F1000: return "No installed components were detected.";

                case 0x83750008: return "Invalid JSON number.";
                case 0x887A0004: return "The specified device interface or feature level is not supported on this system.";
                case 0x887A0005: return "The GPU device instance has been suspended. Use GetDeviceRemovedReason to determine the appropriate action.";
                case 0xC00D3E82: return "A media source cannot go from the stopped state to the paused state.";

                default: return null;
            }
        }

        // The labels around the IID and the method index are localised, but the GUID and the number
        // that follows it are not, so the two are matched structurally rather than by their wording.
        private static readonly Regex _astaCall = new(@"(\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\})[^)0-9]{0,64}([0-9]+)", RegexOptions.Compiled);

        // RPC_E_SERVERCALL_RETRYLATER carries a detail sentence naming the ASTA thread, and that id is
        // different on every hang, so each report would otherwise land in a group of its own. Only the
        // IID and the method index say anything about which call hung, so they're all that's kept.
        private static bool TryTranslateAstaCall(string text, out string translated)
        {
            // "ASTA" is left untranslated in every locale seen, and restricts the match to this message.
            if (text.Contains("ASTA"))
            {
                var match = _astaCall.Match(text);
                if (match.Success)
                {
                    translated = string.Format(CultureInfo.InvariantCulture,
                        "The message filter indicated that the application is busy. A COM call (IID: {0}, method index: {1}) to an ASTA appears deadlocked and was timed out.",
                        match.Groups[1].Value.ToUpperInvariant(),
                        match.Groups[2].Value);
                    return true;
                }
            }

            translated = null;
            return false;
        }

        // TDLib prefixes a log line with the level, the thread, the source location and any number of
        // context tags: "[ 0][t 5][crypto.cpp:420][#1][!Session:4:main][&res != 1]". Matched by that
        // shape rather than by file name, so a new assertion normalises without a change here.
        private static readonly Regex _tdlibLog = new(@"^(\[\s*\d+\])\[t\s*\d+\](\[[^\]\\/]+\.[A-Za-z0-9]+:\d+\])((?:\[[^\]]*\])*)", RegexOptions.Compiled);

        private static readonly Regex _tdlibLogContext = new(@"\[#\d+\]|\[![^\]]*\]", RegexOptions.Compiled);

        private static readonly Regex _tdlibActorIndex = new(@"[:#]\d+(?=[:#\]]|$)", RegexOptions.Compiled);

        // Alternation order is the rule: a stringified condition and a source location are identity and
        // are kept whole, and only what is left over is read as a value the crash happened to print.
        private static readonly Regex _tdlibLogValue = new(@"`[^`]*`|[A-Za-z_][\w.]*\.(?:cpp|cxx|cc|h|hpp)(?:\s+at\s+line)?[:\s]\s*\d+|""[^""]*""|-?\d[\d.]*", RegexOptions.Compiled);

        // A LOG(FATAL) line reaches the report as the message, and the thread, the scheduler, the actor
        // index and the values in the trailing text all differ per crash, so one assertion would
        // otherwise be hashed into a group per report.
        private static bool TryTranslateTdlibLog(string text, out string translated)
        {
            var match = _tdlibLog.Match(text);
            if (!match.Success)
            {
                translated = null;
                return false;
            }

            translated = match.Groups[1].Value
                + match.Groups[2].Value
                + _tdlibLogContext.Replace(match.Groups[3].Value, TranslateTdlibLogContext)
                + _tdlibLogValue.Replace(text.Substring(match.Length), TranslateTdlibLogValue);
            return true;
        }

        // "[#1]" is the scheduler the actor happened to run on, and "Session:4:download#0" names the
        // datacenter and the connection slot - but "download" is a different code path from "main".
        private static string TranslateTdlibLogContext(Match match)
        {
            return match.Value[1] == '#'
                ? string.Empty
                : _tdlibActorIndex.Replace(match.Value, string.Empty);
        }

        private static string TranslateTdlibLogValue(Match match)
        {
            var first = match.Value[0];
            if (first == '"')
            {
                return "\"...\"";
            }
            else if (first == '-' || (first >= '0' && first <= '9'))
            {
                return "<...>";
            }

            return match.Value;
        }

        private static string TranslateText(string text)
        {
            switch (text)
            {
                case "L’interface de périphérique ou niveau de fonctionnalité spécifié n’est pas pris en charge sur ce système.":
                case "Este sistema no admite la interfaz de dispositivo o el nivel de característica especificados.":
                case "A interface de dispositivo ou nível de recurso especificado não tem suporte neste sistema.":
                case "Belirtilen aygıt arabirimi veya özellik düzeyi bu sistemde desteklenmiyor.":
                case "Указанный интерфейс устройства или уровень компонента не поддерживается в данной системе.":
                case "此系統不支援指定的裝置介面或功能層級。":
                    return "The specified device interface or feature level is not supported on this system.";

                case "Le texte associé à ce code d’erreur est introuvable.":
                case "Der Text zu diesem Fehlercode wurde nicht gefunden.":
                case "O texto associado a este código de erro não foi localizado.":
                case "No se pudo encontrar el texto asociado a este código de error.":
                case "Não foi possível encontrar o texto associado a este código de erro.":
                case "Bu hata koduyla ilişkili metin bulunamadı.":
                case "Impossibile trovare il testo associato a questo codice di errore.":
                case "De tekst die bij deze foutcode hoort, kan niet worden gevonden.":
                case "Nie można znaleźć tekstu skojarzonego z tym kodem błędu.":
                case "Не удалось найти текст, связанный с этим кодом ошибки.":
                case "이 오류 코드와 연결된 텍스트를 찾을 수 없습니다.":
                case "无法找到与此错误代码关联的文本。":
                case "找不到與此錯誤碼關聯的文字。":
                case "A hibakódhoz tartozó szöveg nem található.":
                case "Tähän virhekoodiin liittyvää tekstiä ei löytynyt.":
                case "Text přiřazený k tomuto kódu chyby nebyl nalezen.":
                case "Det gick inte att hitta texten som associeras med den här felkoden.":
                    return "The text associated with this error code could not be found.";

                case "L’objet invoqué s’est déconnecté de ses clients.":
                case "El objeto invocado ha desconectado de sus clientes.":
                case "O objeto invocado foi desligado dos respetivos clientes.":
                case "L'oggetto invocato si è disconnesso dai client corrispondenti.":
                case "Das aufgerufene Objekt wurde von den Clients getrennt.":
                case "Wywołany obiekt odłączył się od swoich klientów.":
                case "Вызванный объект был отключен от клиентов.":
                case "起動されたオブジェクトはクライアントから切断されました。":
                case "çağrılan nesne istemcilerinden ayrılmış.":
                    return "The object invoked has disconnected from its clients.";

                case "Unbekannter Fehler":
                case "Niet nader omschreven fout":
                case "Erreur non spécifiée":
                case "Error no especificado":
                case "Erro não especificado":
                case "Belirtilmemiş hata":
                case "Errore non specificato.":
                case "Nieokreślony błąd.":
                case "Nespecifikovaná chyba":
                case "Odefinierat fel":
                case "Uspesifisert feil":
                case "Määrittämätön virhe.":
                case "Meghatározatlan hiba":
                case "Неопознанная ошибка":
                case "未指定的错误":
                case "無法指出的錯誤":
                case "지정되지 않은 오류입니다.":
                case "エラーを特定できません":
                    return "Unspecified error";

                case "L’instance de périphérique GPU a été suspendue. Utilisez GetDeviceRemovedReason pour déterminer l’action appropriée.":
                case "La instancia de dispositivo de GPU se ha suspendido. Use GetDeviceRemovedReason para averiguar cuál es la acción adecuada.":
                case "Istanza del dispositivo GPU sospesa. Utilizzare GetDeviceRemovedReason per determinare l'azione appropriata.":
                case "Die GPU-Geräteinstanz wurde angehalten. Verwenden Sie GetDeviceRemovedReason, um die erforderliche Aktion zu bestimmen.":
                case "GPU aygıt örneği askıya alınmış. Uygun eylemi belirlemek için GetDeviceRemovedReason komutunu kullanın.":
                case "Wystąpienie urządzenia GPU zostało zawieszone. Użyj obiektu GetDeviceRemovedReason, aby określić odpowiednią akcję.":
                case "Экземпляр устройства GPU приостановлен. Для определения соответствующего действия используйте GetDeviceRemovedReason.":
                case "A instância de dispositivo GPU foi suspensa. Use GetDeviceRemovedReason para determinar a ação apropriada.":
                    return "The GPU device instance has been suspended. Use GetDeviceRemovedReason to determine the appropriate action.";

                case "Élément introuvable.":
                case "No se ha encontrado el elemento.":
                case "Elemento não encontrado.":
                case "Kan element niet vinden.":
                case "Impossibile trovare elemento.":
                case "Eleman bulunamadı.":
                case "Элемент не найден.":
                case "Nie można odnaleźć elementu.":
                case "元素找不到。":
                    return "Element not found.";

                case "Falscher Parameter.":
                case "Paramètre incorrect.":
                case "El parámetro no es correcto.":
                case "Parametro non corretto.":
                case "Parâmetro incorreto.":
                case "Parametre hatalı.":
                case "Параметр задан неверно.":
                case "Parametri ei kelpaa":
                case "De parameter is onjuist.":
                case "Parametr jest niepoprawny.":
                case "Felaktig parameter.":
                case "매개 변수가 틀립니다.":
                case "パラメーターが間違っています。":
                case "参数错误。":
                case "參數錯誤。":
                    return "The parameter is incorrect.";

                case "Geçersiz işaretçi":
                case "Pointeur non valide":
                case "Puntero no válido":
                case "Puntatore non valido.":
                case "Ungültiger Zeiger":
                case "Неправильный указатель":
                case "잘못된 포인터입니다.":
                case "Ponteiro inválido":
                case "无效指针":
                    return "Invalid pointer";

                case "Se cerró el objeto.":
                case "L’objet a été fermé.":
                case "Het object is gesloten.":
                case "L'oggetto è stato chiuso.":
                case "O objeto foi fechado.":
                case "Nesne kapatıldı.":
                case "Obiekt został zamknięty.":
                case "Объект закрыт.":
                case "개체가 닫혔습니다.":
                    return "The object has been closed.";

                case "Fuera del intervalo actual.":
                case "Fora do intervalo presente.":
                case "En dehors de la plage actuelle.":
                case "Non compreso nell'intervallo presente.":
                case "Выход за пределы диапазона.":
                    return "Out of present range.";

                case "Nie wykryto żadnych zainstalowanych składników.":
                    return "No installed components were detected.";

                case "No se puede encontrar el módulo especificado.":
                    return "The specified module could not be found.";

                case "L’application a appelé une interface qui était maintenue en ordre pour un thread différent.":
                case "O aplicativo chamou uma interface marshalled para um outro thread.":
                case "A aplicação chamou uma interface que estava empacotada (marshall) para outro módulo.":
                case "La aplicación llamó a una interfaz que se aplanó para un diferente subproceso.":
                case "L'applicazione ha chiamato un'interfaccia su cui era stato eseguito il marshalling per un thread differente.":
                case "Eine Schnittstelle, die für einen anderen Thread marshalled war, wurde von der Anwendung aufgerufen.":
                case "Aplikacja wywołała interfejs, który został skierowany na inny wątek.":
                case "Uygulama, farklı bir iş parçacığı için sıraya konan bir arabirimi çağırdı.":
                case "Programmet kaldte en grænseflade, der var arrangeret for en anden tråd.":
                case "Приложение обратилось к интерфейсу, относящемуся к другому потоку.":
                case "应用程序调用一个已为另一线程整理的接口。":
                    return "The application called an interface that was marshalled for a different thread.";

                case "Les ressources mémoire disponibles sont insuffisantes pour exécuter cette opération.":
                case "Le risorse di memoria disponibili insufficienti per completare l'operazione.":
                case "No hay suficientes recursos de memoria disponibles para completar esta operación.":
                case "Recursos de memória insuficientes disponíveis para concluir a operação.":
                case "Não existem recursos de memória suficientes para concluir esta operação.":
                case "Für diesen Vorgang sind nicht genügend Speicherressourcen verfügbar.":
                case "Otillräckligt med ledigt minne för att slutföra den här åtgärden.":
                case "Ikke nok minneressurser tilgjengelig for å fullføre denne operasjonen.":
                case "Bu işlemi tamamlamak için yeterli bellek kaynağı yok.":
                case "Недостаточно ресурсов памяти для завершения операции.":
                case "Недостаточно ресурсов памяти для обработки этой команды.":
                case "メモリ リソースが不足しているため、この操作を完了できません。":
                case "記憶體資源不足，無法完成此作業。":
                case "系统资源不足，无法完成请求的服务。":
                case "Zur Verarbeitung dieses Befehls sind nicht genügend Speicherressourcen verfügbar.":
                    return "Not enough memory resources are available to process this command.";

                case "Le serveur RPC n’est pas disponible.":
                case "O servidor RPC não está disponível.":
                case "Der RPC-Server ist nicht verfügbar.":
                case "Serwer RPC jest niedostępny.":
                case "Сервер RPC недоступен.":
                case "El servidor RPC no está disponible.":
                case "RPC sunucusu kullanılamıyor.":
                    return "The RPC server is unavailable.";

                case "Zdalne wywołanie procedury nie powiodło się.":
                case "Сбой при удаленном вызове процедуры.":

                // TODO: sligthly different case for async but we use the same english string
                case "Сбой при удаленном вызове процедуры. Вызов не произведен.":
                    return "The remote procedure call failed.";

                case "Aucun composant installé n’a été détecté.":
                case "No se han detectado componentes instalados.":
                case "Nenhum componente instalado foi detectado.":
                case "Keine installierten Komponenten gefunden.":
                case "Non è stato rilevato alcun componente installato.":
                case "Yüklü bileşen algılanamadı.":
                case "Не обнаружено установленных компонентов.":
                case "並未偵測出安裝元件。":
                    return "No installed components were detected.";

                case "Opération abandonnée":
                case "Operação anulada":
                case "Operación anulada":
                case "Операция прервана":
                case "İşlem iptal edildi":
                case "작업이 중단되었습니다.":
                case "Vorgang abgebrochen":
                case "Operacja przerwana.":
                    return "Operation aborted";

                case "Défaillance irrémédiable":
                case "Errore irreparabile":
                case "Error catastrófico":
                case "Falha catastrófica":
                case "Çok zararlı hata":
                case "Разрушительный сбой":
                case "灾难性故障":
                case "오류입니다.":
                case "災難性的失敗":
                    return "Catastrophic failure";

                case "Асинхронная операция не запущена должным образом.":
                case "Une opération asynchrone n’a pas démarré correctement.":
                case "某个异步操作没有正常启动。":
                case "Una operación asincrónica no se inició correctamente.":
                case "Uma operação assíncrona não foi iniciada corretamente.":
                    return "An async operation was not properly started.";

                case "Попытка произвести недопустимую операцию над параметром реестра, отмеченным для удаления.":
                    return "Illegal operation attempted on a registry key that has been marked for deletion.";

                case "Acceso denegado.":
                case "Acesso negado.":
                case "Accès refusé.":
                case "Отказано в доступе.":
                case "拒绝访问。":
                case "Erişim engellendi.":
                case "액세스가 거부되었습니다.":
                    return "Access is denied.";

                case "Échec de l’exécution du serveur":
                case "Ошибка при выполнении приложения-сервера":
                    return "Server execution failed";

                case "Le filtre de messages indiquait que l’application était occupée.":
                case "O filtro de mensagens indicou que o aplicativo está ocupado.":
                case "El filtro de mensaje indicó que la aplicación está ocupada.":
                case "Het berichtenfilter heeft aangegeven dat de toepassing bezet is.":
                case "Filtr wiadomości wykazał, że aplikacja jest zajęta.":
                case "Il filtro messaggi ha indicato che l'applicazione è impegnata.":
                case "İleti filtresi uygulamanın kullanımda olduğunu belirledi.":
                case "Фильтр сообщений выдал диагностику о занятости приложения.":
                case "O filtro de mensagens indicou que a aplicação está ocupada.":
                case "消息筛选器显示应用程序正在使用中。":
                    return "The message filter indicated that the application is busy.";

                case "%1 не является приложением Win32.":
                case "%1 n’est pas une application Win32 valide.":
                    return "%1 is not a valid Win32 application.";

                case "Il gruppo o la risorsa non si trova nello stato appropriato per eseguire l'operazione richiesta.":
                case "Le groupe ou la ressource n’est pas dans l’état correct pour effectuer l’opération requise.":
                case "El grupo o recurso no está en el estado correcto para realizar la operación solicitada.":
                case "Grup veya kaynak istenen işlemi gerçekleştirmek için doğru durumda değil.":
                case "Группа или ресурс не находятся в нужном состоянии для выполнения требуемой операции.":
                case "グループまたはリソースは要求した操作の実行に適切な状態ではありません。":
                    return "The group or resource is not in the correct state to perform the requested operation.";

                case "Un'origine multimediale non può passare dallo stato di interruzione allo stato di pausa.":
                case "Источник мультимедиа не может перейти из остановленного состояния в приостановленное.":
                    return "A media source cannot go from the stopped state to the paused state.";

                case "Un événement n’a pu invoquer aucun des abonnés.":
                case "Событие не смогло вызвать ни одного из абонентов":
                case "Ein Ereignis konnte keinen Abonnenten aufrufen.":
                    return "An event was unable to invoke any of the subscribers";

                case "Le package n'a pas de répertoire mutable.":
                case "Das Paket hat kein variables Verzeichnis.":
                case "Пакет не имеет изменяемого каталога.":
                    return "The package does not have a mutable directory.";

                case "Risorsa realizzata sulla destinazione di rendering errata.":
                case "Die Ressource wurde auf dem falschen Renderziel erkannt.":
                case "Kaynak yanlış işleme hedefinde gerçekleştirildi.":
                case "La ressource a été réalisée sur la cible de rendu incorrecte.":
                case "El recurso se produjo en el destino de representación incorrecto.":
                case "Ресурс был реализован с использованием неправильной однобуферной прорисовки.":
                case "O recurso foi realizado no destino de processamento errado.":
                case "Zasób został zrealizowany na nieprawidłowym obiekcie docelowym renderowania.":
                case "De bron is gerealiseerd op het verkeerde renderdoel.":
                case "リソースが誤ったレンダー ターゲットで認識されました。":
                case "在错误的呈现器目标上实现资源。":
                    return "The resource was realized on the wrong render target.";

                case "Un fichier de polices n’a pas pu être ouvert car le fichier, répertoire, remplacement réseau, lecteur ou autre emplacement de stockage n’existe pas ou n’est pas disponible.":
                case "Eine Schriftartdatei konnte nicht geöffnet werden, da die Datei, das Verzeichnis, die Netzwerkadresse, das Laufwerk oder ein anderer Speicherort nicht vorhanden bzw. verfügbar ist.":
                case "No se pudo abrir un archivo de fuentes porque el archivo, directorio, ubicación de red, unidad u otra ubicación de almacenamiento no existe o no está disponible.":
                case "Não foi possível abrir um arquivo de fonte porque o arquivo, o diretório, o local de rede, a unidade ou outro local de armazenamento não existe ou não está disponível.":
                case "Impossibile aprire un file di tipi di carattere. Il file, la directory, il percorso di rete, l'unità o un'altra posizione di archiviazione non esiste o non è disponibile.":
                case "Dosya, dizin, ağ konumu, sürücü veya başka bir depolama konumu olmadığından veya kullanılamıyor olduğundan, yazı tipi dosyası açılamadı.":
                case "Не удалось открыть файл шрифта, так как файл, каталог, сетевое расположение, диск или другое место хранения не существует или недоступно.":
                case "无法打开字体文件，原因是文件、目录、网络位置、驱动器或其他存储文字不存在或不可用。":
                case "Não foi possível abrir um ficheiro de tipos de letra, porque o ficheiro, diretório, localização de rede, unidade ou outra localização de armazenamento não existe ou não está disponível.":
                case "Een lettertypebestand kan niet worden geopend omdat het bestand, de map, de netwerklocatie, het station of een andere opslaglocatie niet bestaat of niet beschikbaar is.":
                case "Nie można otworzyć pliku czcionki, ponieważ plik, katalog, lokalizacja sieciowa, dysk lub inne miejsce przechowywania nie istnieje lub jest niedostępne.":
                case "フォント ファイルを開くことができませんでした。ファイル、ディレクトリ、ネットワークの場所、またはドライブなどの記憶域の場所が存在しないか、利用できません。":
                case "파일, 디렉터리, 네트워크 위치, 드라이브 또는 기타 저장소 위치가 존재하지 않거나 사용할 수 없으므로 글꼴 파일을 열 수 없습니다.":
                case "無法開啟字型檔案，因為檔案、目錄、網路位置、磁碟機或其他存放裝置不存在或無法使用。":
                case "Soubor s písmem nelze otevřít, protože soubor, adresář, síťové umístění, jednotka nebo jiné úložné umístění neexistují nebo nejsou k dispozici.":
                case "En skriftfil kunne ikke åpnes fordi filen, mappen, nettverksplasseringen, stasjonen eller en annen lagringsplassering ikke finnes eller ikke er tilgjengelig.":
                    return "A font file could not be opened because the file, directory, network location, drive, or other storage location does not exist or is unavailable.";

                case "Un fichier de polices existe mais n’a pas pu être ouvert en raison d’un refus d’accès, d’une violation de partage ou d’une erreur similaire.":
                case "Файл шрифта существует, но его не удалось открыть из-за отказа в доступе, нарушения общего доступа или аналогичной ошибки.":
                case "El archivo de fuentes existe, pero no se pudo abrir debido a que se denegó el acceso, a una infracción de uso compartido o a error similar.":
                case "Um arquivo de fonte existe porém não foi possível abri-lo devido a acesso negado, violação de compartilhamento ou erro semelhante.":
                case "글꼴 파일은 있지만 액세스 거부, 공유 위반 또는 유사한 오류로 인해 열 수 없습니다.":
                case "字体文件存在，但是由于访问被拒绝、共享违规或类似错误而无法打开。":
                    return "A font file exists but could not be opened due to access denied, sharing violation, or similar error.";

                case "El sistema no puede encontrar el archivo especificado.":
                case "Не удается найти указанный файл.":
                    return "The system cannot find the file specified.";

                case "Le processus ne peut pas accéder au fichier car ce fichier est utilisé par un autre processus.":
                case "Proces nie może uzyskać dostępu do pliku, ponieważ jest on używany przez inny proces.":
                    return "The process cannot access the file because it is being used by another process.";

                case "Le fichier est en cours d’utilisation. Fermez le fichier avant de continuer.":
                case "Plik jest używany. Zamknij go przed kontynuowaniem.":
                    return "The file is in use. Please close the file before continuing.";

                case "Espace insuffisant sur le disque.":
                    return "There is not enough space on the disk.";

                case "Файл подкачки слишком мал для завершения операции.":
                case "Le fichier de pagination est insuffisant pour terminer cette opération.":
                    return "The paging file is too small for this operation to complete.";

                case "Ressources système insuffisantes pour terminer le service demandé.":
                case "Não existem recursos de sistema suficientes para concluir o serviço pedido.":
                case "Недостаточно системных ресурсов для завершения операции.":
                    return "Insufficient system resources exist to complete the requested service.";

                case "Указанная служба не может быть запущена, так как отключена либо она сама, либо все связанные с ней устройства.":
                case "No se puede iniciar el servicio, porque está deshabilitado o porque no tiene dispositivos habilitados asociados a él.":
                case "Der angegebene Dienst kann nicht gestartet werden. Er ist deaktiviert oder nicht mit aktivierten Geräten verbunden.":
                    return "The service cannot be started, either because it is disabled or because it has no enabled devices associated with it.";

                case "La zone de données passée à un appel système est insuffisante.":
                case "Область данных, переданная по системному вызову, слишком мала.":
                    return "The data area passed to a system call is too small.";

                case "L’identificateur d’opération n’est pas valide.":
                case "Неверный идентификатор операции.":
                    return "The operation identifier is not valid.";

                case "La operación intentó tener acceso a datos fuera del rango válido":
                    return "The operation attempted to access data outside the valid range";

                case "Cette interface n’est pas prise en charge":
                case "Interfaz no compatible":
                case "Não há suporte para esta interface":
                case "Böyle bir arabirim desteklenmiyor":
                case "Интерфейс не поддерживается":
                    return "No such interface supported";

                case "Интерфейс не зарегистрирован":
                    return "Interface not registered";

                case "valor no válido para el Registro":
                    return "Invalid value for registry";

                case "Символ Юникода не имеет сопоставления в конечной многобайтовой кодовой странице.":
                    return "No mapping for the Unicode character exists in the target multi-byte code page.";

                case "Недопустимый дескриптор окна.":
                case "Handle de fenêtre non valide.":
                    return "Invalid window handle.";

                case "메모리 리소스가 부족하기 때문에 이 작업을 완료할 수 없습니다.":
                case "内存资源不足，无法完成此操作。":
                case "K dokončení této operace není dost paměťových prostředků.":
                    return "Not enough memory resources are available to complete this operation.";

                case "{A composição de Área de Trabalho está desabilitada} Não foi possível concluir a operação porque essa composição está desabilitada.":
                    return "{Desktop composition is disabled} The operation could not be completed because desktop composition is disabled.";

                default:
                    return text;
            }
        }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    [JsonSerializable(typeof(ErrorReport))]
    [JsonSerializable(typeof(ErrorExceptionAndBinaries))]
    [JsonSerializable(typeof(ExceptionModel))]
    [JsonSerializable(typeof(ExceptionStackFrame))]
    [JsonSerializable(typeof(ExceptionBinary))]
    [JsonSerializable(typeof(List<ExceptionBinary>))]
    [JsonSerializable(typeof(List<ExceptionModel>))]
    public partial class ErrorJsonContext : JsonSerializerContext
    {
    }

    public partial class ErrorReport
    {
        [JsonPropertyName("dedup_id")]
        public string Id { get; set; }

        [JsonPropertyName("ver_str")]
        public string ApplicationVersion { get; set; }

        [JsonPropertyName("arch")]
        public string ApplicationArchitecture { get; set; }

        [JsonPropertyName("os")]
        public string SystemVersion { get; set; }

        [JsonPropertyName("device")]
        public string DeviceModel { get; set; }

        [JsonPropertyName("error_type")]
        public string Type { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("exit_point")]
        public string ExitPoint { get; set; }

        [JsonPropertyName("stack_trace")]
        public ErrorExceptionAndBinaries StackTrace { get; set; }

        [JsonPropertyName("log_tail")]
        public string LogTail { get; set; }

        [JsonPropertyName("group_hash")]
        public string GroupHash { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("flags")]
        public int Flags { get; set; }

        [JsonPropertyName("cl_time")]
        public long Time { get; set; }

        [JsonPropertyName("cl_launch_time")]
        public long LaunchTime { get; set; }
    }

    public partial class ErrorExceptionAndBinaries
    {
        [JsonPropertyName("binaries")]
        public List<ExceptionBinary> Binaries { get; set; }

        [JsonPropertyName("exception")]
        public ExceptionModel Exception { get; set; }
    }

    public partial class ExceptionModel
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("stackTrace")]
        public string StackTrace { get; set; }

        public List<ExceptionStackFrame> Frames { get; set; }

        [JsonPropertyName("innerExceptions")]
        public List<ExceptionModel> InnerExceptions { get; set; }
    }

    public partial class ExceptionStackFrame
    {
        /// <summary>
        /// Gets or sets frame address.
        /// </summary>
        [JsonPropertyName("address")]
        public string Address { get; set; }
    }

    public partial class ExceptionBinary
    {
        /// <summary>
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// </summary>
        [JsonPropertyName("startAddress")]
        public string StartAddress { get; set; }

        /// <summary>
        /// </summary>
        [JsonPropertyName("endAddress")]
        public string EndAddress { get; set; }

        /// <summary>
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonIgnore]
        public string Name { get; set; }
    }
}
