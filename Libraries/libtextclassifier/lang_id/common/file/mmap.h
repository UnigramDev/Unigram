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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_FILE_MMAP_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_FILE_MMAP_H_

#include <stddef.h>

#include <cstddef>
#include <string>

#include "lang_id/common/lite_strings/stringpiece.h"

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#endif

namespace libtextclassifier3 {
namespace mobile {

// Handle for a memory area where a file has been mmapped.
//
// Similar to a pointer: you "allocate" it using MmapFile(filename) and "delete"
// it using Unmap().  Just like a pointer, it is passed around by value (see
// signature of MmapFile and Unmap; fortunately, it's a small class, so there
// shouldn't be any significant performance penalty) and its usage is not
// necessarily scoped (that's why the destructor is not performing the unmap).
//
// Note: on program termination, each still unmapped file is automatically
// unmapped.  Hence, it is not an error if you don't call Unmap() (provided you
// are ok keeping that file in memory the whole time).
class MmapHandle {
 public:
  MmapHandle(void *start, size_t num_bytes)
      : start_(start), num_bytes_(num_bytes) {}

  // Returns start address for the memory area where a file has been mmapped.
  void *start() const { return start_; }

  // Returns number of bytes of the memory area from start().
  size_t num_bytes() const { return num_bytes_; }

  // Shortcut to simplify checking success of MmapFile().  See usage example
  // from the doc of that function.
  bool ok() const { return start() != nullptr; }

  // Returns a StringPiece pointing to the same underlying bytes.
  StringPiece to_stringpiece() const {
    return StringPiece(reinterpret_cast<char *>(start_), num_bytes_);
  }

 private:
  // See doc for start().  Not owned.
  void *const start_;

  // See doc for num_bytes().
  const size_t num_bytes_;
};

// Maps the full content of a file in memory (using mmap).
//
// When done using the file content, one can unmap using Unmap().  Otherwise,
// all mapped files are unmapped when the program terminates.
//
// Sample usage:
//
// MmapHandle mmap_handle = MmapFile(filename);
// CHECK(mmap_handle.ok()) << "Unable to mmap " << filename;
//
// ... use data from addresses
// ... [mmap_handle.start, mmap_handle.start + mmap_handle.num_bytes)
//
// Unmap(mmap_handle);  // Unmap logs errors internally.
//
// Note: one can read *and* write the num_bytes bytes from start, but those
// writes are not propagated to the underlying file, nor to other processes that
// may have mmapped that file (all changes are local to current process).
MmapHandle MmapFile(const std::string &filename);

#ifdef _WIN32
using FileDescriptorOrHandle = HANDLE;
#else
using FileDescriptorOrHandle = int;
#endif

// Like MmapFile(const std::string &filename), but uses a file descriptor.
// This function maps the entire file content.
MmapHandle MmapFile(FileDescriptorOrHandle fd);

// Like MmapFile(const std::string &filename), but uses a file descriptor,
// with an offset relative to the file start and a specified size, such that we
// consider only a range of the file content.
MmapHandle MmapFile(FileDescriptorOrHandle fd, size_t offset_in_bytes,
                    size_t size_in_bytes);

// Unmaps a file mapped using MmapFile.  Returns true on success, false
// otherwise.
bool Unmap(MmapHandle mmap_handle);

// Scoped mmapping of a file.  Mmaps a file on construction, unmaps it on
// destruction.
class ScopedMmap {
 public:
  explicit ScopedMmap(const std::string &filename)
      : handle_(MmapFile(filename)) {}

  explicit ScopedMmap(FileDescriptorOrHandle fd) : handle_(MmapFile(fd)) {}

  explicit ScopedMmap(FileDescriptorOrHandle fd, size_t offset_in_bytes,
                      size_t size_in_bytes)
      : handle_(MmapFile(fd, offset_in_bytes, size_in_bytes)) {}

  ~ScopedMmap() {
    if (handle_.ok()) {
      Unmap(handle_);
    }
  }

  const MmapHandle &handle() { return handle_; }

 private:
  MmapHandle handle_;
};

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_FILE_MMAP_H_
