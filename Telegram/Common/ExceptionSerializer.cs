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
                    return "Not enough memory resources are available to complete this operation.";

                case "Le serveur RPC n’est pas disponible.":
                case "O servidor RPC não está disponível.":
                case "Der RPC-Server ist nicht verfügbar.":
                case "Serwer RPC jest niedostępny.":
                case "Сервер RPC недоступен.":
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
                    return "Operation aborted";

                case "Défaillance irrémédiable":
                case "Errore irreparabile":
                case "Error catastrófico":
                case "Falha catastrófica":
                case "Çok zararlı hata":
                case "Разрушительный сбой":
                case "灾难性故障":
                case "오류입니다.":
                    return "Catastrophic failure";

                case "Асинхронная операция не запущена должным образом.":
                    return "An async operation was not properly started.";

                case "Попытка произвести недопустимую операцию над параметром реестра, отмеченным для удаления.":
                    return "Illegal operation attempted on a registry key that has been marked for deletion.";

                case "Acceso denegado.":
                case "Acesso negado.":
                case "Accès refusé.":
                case "Отказано в доступе.":
                case "拒绝访问。":
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
                    return "The message filter indicated that the application is busy.";

                case "%1 не является приложением Win32.":
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
                    return "An event was unable to invoke any of the subscribers";

                case "Le package n'a pas de répertoire mutable.":
                case "Das Paket hat kein variables Verzeichnis.":
                case "Пакет не имеет изменяемого каталога.":
                    return "The package does not have a mutable directory.";

                case "Risorsa realizzata sulla destinazione di rendering errata.":
                case "Die Ressource wurde auf dem falschen Renderziel erkannt.":
                case "Kaynak yanlış işleme hedefinde gerçekleştirildi.":
                    return "The resource was realized on the wrong render target.";

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
