/*
 * Copyright (C) 2018 The Android Open Source Project
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#include "lang_id/common/file/mmap.h"

#include <errno.h>
#include <fcntl.h>
#include <stdint.h>
#include <string.h>
#ifdef _WIN32
#include <winbase.h>
#include <windows.h>
#else
#include <sys/mman.h>
#include <unistd.h>
#endif
#include <sys/stat.h>

#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_base/macros.h"

namespace libtextclassifier3 {
namespace mobile {

namespace {
inline MmapHandle GetErrorMmapHandle() { return MmapHandle(nullptr, 0); }
}  // anonymous namespace

#ifdef _WIN32

namespace {
inline std::string MBFromW(LPCWSTR pwsz) {
  int cch = WideCharToMultiByte(CP_UTF8, 0, pwsz, -1, 0, 0, NULL, NULL);

  char* psz = new char[cch];

  WideCharToMultiByte(CP_UTF8, 0, pwsz, -1, psz, cch, NULL, NULL);

  std::string st(psz);
  delete[] psz;

  return st;
}

inline std::wstring WFromMB(const std::string& s)
{
  int len;
  int slength = (int)s.length() + 1;
  len = MultiByteToWideChar(CP_ACP, 0, s.c_str(), slength, 0, 0);
  wchar_t* buf = new wchar_t[len];
  MultiByteToWideChar(CP_ACP, 0, s.c_str(), slength, buf, len);
  std::wstring r(buf);
  delete[] buf;
  return r;
}

inline std::string GetLastSystemError() {
  LPTSTR message_buffer;
  DWORD error_code = GetLastError();
  FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM |
                    FORMAT_MESSAGE_IGNORE_INSERTS,
                NULL, error_code, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
                (LPTSTR)&message_buffer, 0, NULL);
  std::string result = MBFromW(message_buffer);
  LocalFree(message_buffer);
  return result;
}

// Class for automatically closing a Win32 HANDLE on exit from a scope.
class Win32HandleCloser {
 public:
  explicit Win32HandleCloser(HANDLE handle) : handle_(handle) {}
  ~Win32HandleCloser() {
    bool result = CloseHandle(handle_);
    if (!result) {
      const DWORD last_error = GetLastError();
      SAFTM_LOG(ERROR) << "Error closing handle: " << last_error << ": "
                       << GetLastSystemError();
    }
  }

 private:
  const HANDLE handle_;

  SAFTM_DISALLOW_COPY_AND_ASSIGN(Win32HandleCloser);
};
}  // namespace

MmapHandle MmapFile(const std::string &filename) {
  HANDLE handle =
      CreateFileFromAppW(WFromMB(filename.c_str()).c_str(),  // File to open.
                 GENERIC_READ,      // Open for reading.
                 FILE_SHARE_READ,   // Share for reading.
                 NULL,              // Default security.
                 OPEN_EXISTING,     // Existing file only.
                 FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OVERLAPPED,  // Normal file.
                 NULL);  // No attr. template.
  if (handle == INVALID_HANDLE_VALUE) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Error opening " << filename << ": " << last_error;
    return GetErrorMmapHandle();
  }

  // Make sure we close handle no matter how we exit this function.
  Win32HandleCloser handle_closer(handle);

  return MmapFile(handle);
}

MmapHandle MmapFile(HANDLE file_handle) {
  // Get the file size.
  LARGE_INTEGER file_size;
  //DWORD file_size_high = 0;
  DWORD file_size_low = GetFileSizeEx(file_handle, &file_size);
  if (file_size_low != 0 && GetLastError() != NO_ERROR) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Unable to stat fd: " << last_error;
    return GetErrorMmapHandle();
  }
  size_t file_size_in_bytes = (static_cast<size_t>(file_size.HighPart) << 32) +
                              static_cast<size_t>(file_size.LowPart);

  // Create a file mapping object that refers to the file.
  HANDLE file_mapping_object =
      CreateFileMappingFromApp(file_handle, nullptr, PAGE_READONLY, 0, nullptr);
  if (file_mapping_object == NULL) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Error while mmapping: " << last_error;
    return GetErrorMmapHandle();
  }
  Win32HandleCloser handle_closer(file_mapping_object);

  // Map the file mapping object into memory.
  void *mmap_addr =
      MapViewOfFile(file_mapping_object, FILE_MAP_READ, 0, 0,  // File offset.
                    0  // Number of bytes to map; 0 means map the whole file.
      );
  if (mmap_addr == nullptr) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Error while mmapping: " << last_error;
    return GetErrorMmapHandle();
  }

  return MmapHandle(mmap_addr, file_size_in_bytes);
}

MmapHandle MmapFile(HANDLE file_handle, size_t offset_in_bytes, size_t size_in_bytes) {
  // Get the file size.
  LARGE_INTEGER file_size;
  //DWORD file_size_high = 0;
  DWORD file_size_low = GetFileSizeEx(file_handle, &file_size);
  if (file_size_low != 0 && GetLastError() != NO_ERROR) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Unable to stat fd: " << last_error;
    return GetErrorMmapHandle();
  }
  size_t file_size_in_bytes = (static_cast<size_t>(file_size.HighPart) << 32) +
                              static_cast<size_t>(file_size.LowPart);

  // Create a file mapping object that refers to the file.
  HANDLE file_mapping_object =
      CreateFileMappingFromApp(file_handle, nullptr, PAGE_READONLY, 0, nullptr);
  if (file_mapping_object == NULL) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Error while mmapping: " << last_error;
    return GetErrorMmapHandle();
  }
  Win32HandleCloser handle_closer(file_mapping_object);

  // Map the file mapping object into memory.
  void *mmap_addr =
      MapViewOfFile(file_mapping_object, FILE_MAP_READ, 0, 0,  // File offset.
                    0  // Number of bytes to map; 0 means map the whole file.
      );
  if (mmap_addr == nullptr) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Error while mmapping: " << last_error;
    return GetErrorMmapHandle();
  }

  return MmapHandle(mmap_addr, file_size_in_bytes);
}

bool Unmap(MmapHandle mmap_handle) {
  if (!mmap_handle.ok()) {
    // Unmapping something that hasn't been mapped is trivially successful.
    return true;
  }
  bool succeeded = UnmapViewOfFile(mmap_handle.start());
  if (!succeeded) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Error during Unmap / UnmapViewOfFile: " << last_error;
    return false;
  }
  return true;
}

#else

namespace {
inline std::string GetLastSystemError() { return std::string(strerror(errno)); }

class FileCloser {
 public:
  explicit FileCloser(int fd) : fd_(fd) {}
  ~FileCloser() {
    int result = close(fd_);
    if (result != 0) {
      const std::string last_error = GetLastSystemError();
      SAFTM_LOG(ERROR) << "Error closing file descriptor: " << last_error;
    }
  }

 private:
  const int fd_;

  SAFTM_DISALLOW_COPY_AND_ASSIGN(FileCloser);
};
}  // namespace

MmapHandle MmapFile(const std::string &filename) {
  int fd = open(filename.c_str(), O_RDONLY);

  if (fd < 0) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Error opening " << filename << ": " << last_error;
    return GetErrorMmapHandle();
  }

  // Make sure we close fd no matter how we exit this function.  As the man page
  // for mmap clearly states: "closing the file descriptor does not unmap the
  // region."  Hence, we can close fd as soon as we return from here.
  FileCloser file_closer(fd);

  return MmapFile(fd);
}

MmapHandle MmapFile(int fd) {
  // Get file stats to obtain file size.
  struct stat sb;
  if (fstat(fd, &sb) != 0) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Unable to stat fd: " << last_error;
    return GetErrorMmapHandle();
  }
  size_t file_size_in_bytes = static_cast<size_t>(sb.st_size);

  // Perform actual mmap.
  return MmapFile(fd, /*offset_in_bytes=*/0, file_size_in_bytes);
}

MmapHandle MmapFile(int fd, size_t offset_in_bytes, size_t size_in_bytes) {
  // Make sure the offset is a multiple of the page size, as returned by
  // sysconf(_SC_PAGE_SIZE); this is required by the man-page for mmap.
  static const size_t kPageSize = sysconf(_SC_PAGE_SIZE);
  const size_t aligned_offset = (offset_in_bytes / kPageSize) * kPageSize;
  const size_t alignment_shift = offset_in_bytes - aligned_offset;
  const size_t aligned_length = size_in_bytes + alignment_shift;

  void *mmap_addr = mmap(

      // Let system pick address for mmapp-ed data.
      nullptr,

      aligned_length,

      // One can read / write the mapped data (but see MAP_PRIVATE below).
      // Normally, we expect only to read it, but in the future, we may want to
      // write it, to fix e.g., endianness differences.
      PROT_READ | PROT_WRITE,

      // Updates to mmaped data are *not* propagated to actual file.
      // AFAIK(salcianu) that's anyway not possible on Android.
      MAP_PRIVATE,

      // Descriptor of file to mmap.
      fd,

      aligned_offset);
  if (mmap_addr == MAP_FAILED) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Error while mmapping: " << last_error;
    return GetErrorMmapHandle();
  }

  return MmapHandle(static_cast<char *>(mmap_addr) + alignment_shift,
                    size_in_bytes);
}

bool Unmap(MmapHandle mmap_handle) {
  if (!mmap_handle.ok()) {
    // Unmapping something that hasn't been mapped is trivially successful.
    return true;
  }
  if (munmap(mmap_handle.start(), mmap_handle.num_bytes()) != 0) {
    const std::string last_error = GetLastSystemError();
    SAFTM_LOG(ERROR) << "Error during Unmap / munmap: " << last_error;
    return false;
  }
  return true;
}

#endif

}  // namespace mobile
}  // namespace nlp_saft
