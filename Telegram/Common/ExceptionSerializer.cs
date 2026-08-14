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

        public static string Serialize(System.Exception exception, string id, string userId, bool captureAllThreads, string logs)
        {
            var hashBuilder = new StringBuilder();
            var binaries = new Dictionary<long, ExceptionBinary>();
            var modelThreads = default(List<ThreadModel>);
            var modelException = ProcessException(exception, null, binaries, hashBuilder);

            if (captureAllThreads)
            {
                var stack = StackCapture.CaptureAllThreadStacks();
                if (stack.Threads.Count > 0)
                {
                    modelThreads = new List<ThreadModel>(stack.Threads.Count);

                    foreach (var module in stack.Modules)
                    {
                        var baseAddress = module.BaseAddress.ToInt64();
                        if (binaries.ContainsKey(baseAddress))
                        {
                            continue;
                        }

                        var binary = ImageToBinary(module.BaseAddress);
                        if (binary != null)
                        {
                            binaries[baseAddress] = binary;
                        }
                    }

                    foreach (var thread in stack.Threads)
                    {
                        modelThreads.Add(new ThreadModel
                        {
                            Id = thread.ThreadId,
                            Frames = thread.Frames.Select(x => new ExceptionStackFrame
                            {
                                Address = string.Format(CultureInfo.InvariantCulture, AddressFormat, x.ToInt64()),
                            }).ToList()
                        });
                    }
                }
            }

            var error = new ErrorExceptionAndBinaries
            {
                Binaries = binaries.Count > 0 ? binaries.Values.ToList() : null,
                Exception = modelException,
                Threads = modelThreads,
            };

            foreach (var binary in binaries.Values.OrderBy(x => x.Name))
            {
                hashBuilder.Append(binary.Name.ToLowerInvariant());
            }

            return Serialize(error, id, userId, logs, hashBuilder);
        }

        public static string Serialize(FatalError exception, string id, string userId, bool captureAllThreads, string logs)
        {
            var hashBuilder = new StringBuilder();
            var binaries = new Dictionary<long, ExceptionBinary>();
            var modelThreads = default(List<ThreadModel>);
            var modelException = ProcessException(exception, null, binaries, hashBuilder);

            if (captureAllThreads)
            {
                var stack = StackCapture.CaptureAllThreadStacks();
                if (stack.Threads.Count > 0)
                {
                    modelThreads = new List<ThreadModel>(stack.Threads.Count);

                    foreach (var module in stack.Modules)
                    {
                        var baseAddress = module.BaseAddress.ToInt64();
                        if (binaries.ContainsKey(baseAddress))
                        {
                            continue;
                        }

                        var binary = ImageToBinary(module.BaseAddress);
                        if (binary != null)
                        {
                            binaries[baseAddress] = binary;
                        }
                    }

                    foreach (var thread in stack.Threads)
                    {
                        modelThreads.Add(new ThreadModel
                        {
                            Id = thread.ThreadId,
                            Frames = thread.Frames.Select(x => new ExceptionStackFrame
                            {
                                Address = string.Format(CultureInfo.InvariantCulture, AddressFormat, x.ToInt64()),
                            }).ToList()
                        });
                    }
                }
            }

            var error = new ErrorExceptionAndBinaries
            {
                Binaries = binaries.Count > 0 ? binaries.Values.ToList() : null,
                Exception = modelException,
                Threads = modelThreads
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
                LogTail = logs,
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

        private static ExceptionModel ProcessException(System.Exception exception, ExceptionModel outerException, Dictionary<long, ExceptionBinary> seenBinaries, StringBuilder hashBuilder)
        {
            var modelException = new ExceptionModel
            {
                Type = exception.GetType().Name,
                Message = TranslateMessage(exception.Message.Replace("\r\n", "\n")),
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
                Message = TranslateMessage(exception.Message.Replace("\r\n", "\n")),
                StackTrace = exception.StackTrace?.Replace("\r\n", "\n")
            };

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

        private static string TranslateMessage(string message)
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

                var index = part.IndexOf('(');
                if (index > 0)
                {
                    builder.Append(TranslateText(part.Substring(0, index - 1)));
                    builder.Append(part.Substring(index - 1));
                }
                else
                {
                    builder.Append(TranslateText(part));
                }
            }

            return builder.ToString();
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
                    return "The text associated with this error code could not be found.";

                case "L’objet invoqué s’est déconnecté de ses clients.":
                case "El objeto invocado ha desconectado de sus clientes.":
                case "O objeto invocado foi desligado dos respetivos clientes.":
                case "L'oggetto invocato si è disconnesso dai client corrispondenti.":
                case "Das aufgerufene Objekt wurde von den Clients getrennt.":
                case "Wywołany obiekt odłączył się od swoich klientów.":
                case "Вызванный объект был отключен от клиентов.":
                case "起動されたオブジェクトはクライアントから切断されました。":
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
                case "La aplicación llamó a una interfaz que se aplanó para un diferente subproceso.":
                case "L'applicazione ha chiamato un'interfaccia su cui era stato eseguito il marshalling per un thread differente.":
                case "Eine Schnittstelle, die für einen anderen Thread marshalled war, wurde von der Anwendung aufgerufen.":
                case "Aplikacja wywołała interfejs, który został skierowany na inny wątek.":
                case "Приложение обратилось к интерфейсу, относящемуся к другому потоку.":
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
                    return "Not enough memory resources are available to complete this operation.";

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
                    return "An async operation was not properly started.";

                case "Попытка произвести недопустимую операцию над параметром реестра, отмеченным для удаления.":
                    return "Illegal operation attempted on a registry key that has been marked for deletion.";

                case "Acceso denegado.":
                case "Acesso negado.":
                case "Accès refusé.":
                case "Отказано в доступе.":
                case "拒绝访问。":
                case "Erişim engellendi.":
                    return "Access is denied.";

                case "Échec de l’exécution du serveur":
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
                    return "A font file could not be opened because the file, directory, network location, drive, or other storage location does not exist or is unavailable.";

                case "Un fichier de polices existe mais n’a pas pu être ouvert en raison d’un refus d’accès, d’une violation de partage ou d’une erreur similaire.":
                case "Файл шрифта существует, но его не удалось открыть из-за отказа в доступе, нарушения общего доступа или аналогичной ошибки.":
                case "El archivo de fuentes existe, pero no se pudo abrir debido a que se denegó el acceso, a una infracción de uso compartido o a error similar.":
                case "Um arquivo de fonte existe porém não foi possível abri-lo devido a acesso negado, violação de compartilhamento ou erro semelhante.":
                case "글꼴 파일은 있지만 액세스 거부, 공유 위반 또는 유사한 오류로 인해 열 수 없습니다.":
                    return "A font file exists but could not be opened due to access denied, sharing violation, or similar error.";

                case "El sistema no puede encontrar el archivo especificado.":
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
                    return "The paging file is too small for this operation to complete.";

                case "Ressources système insuffisantes pour terminer le service demandé.":
                case "Não existem recursos de sistema suficientes para concluir o serviço pedido.":
                case "Недостаточно системных ресурсов для завершения операции.":
                    return "Insufficient system resources exist to complete the requested service.";

                case "Указанная служба не может быть запущена, так как отключена либо она сама, либо все связанные с ней устройства.":
                    return "The service cannot be started, either because it is disabled or because it has no enabled devices associated with it.";

                case "La zone de données passée à un appel système est insuffisante.":
                    return "The data area passed to a system call is too small.";

                case "L’identificateur d’opération n’est pas valide.":
                case "Неверный идентификатор операции.":
                    return "The operation identifier is not valid.";

                case "La operación intentó tener acceso a datos fuera del rango válido":
                    return "The operation attempted to access data outside the valid range";

                case "Интерфейс не зарегистрирован":
                    return "Interface not registered";

                case "valor no válido para el Registro":
                    return "Invalid value for registry";

                case "Символ Юникода не имеет сопоставления в конечной многобайтовой кодовой странице.":
                    return "No mapping for the Unicode character exists in the target multi-byte code page.";

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

        [JsonPropertyName("threads")]
        public List<ThreadModel> Threads { get; set; }
    }

    public partial class ThreadModel
    {
        [JsonPropertyName("id")]
        public uint Id { get; set; }

        public List<ExceptionStackFrame> Frames { get; set; }
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
