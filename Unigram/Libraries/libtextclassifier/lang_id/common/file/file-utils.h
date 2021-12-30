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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_FILE_FILE_UTILS_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_FILE_FILE_UTILS_H_

#include <stddef.h>

#include <string>

#include "lang_id/common/file/mmap.h"
#include "lang_id/common/lite_strings/stringpiece.h"

namespace libtextclassifier3 {
namespace mobile {

namespace file_utils {

// Reads the entire content of a file into a string.  Returns true on success,
// false on error.
bool GetFileContent(const std::string &filename, std::string *content);

// Parses a proto from its serialized representation in memory.  That
// representation starts at address |data| and should contain exactly
// |num_bytes| bytes.  Returns true on success, false otherwise.
template <class Proto>
bool ParseProtoFromMemory(const char *data, size_t num_bytes, Proto *proto) {
  if (data == nullptr) {
    // Avoid passing a nullptr to ParseFromArray below.
    return false;
  }
  return proto->ParseFromArray(data, num_bytes);
}

// Convenience StringPiece-based version of ParseProtoFromMemory.
template <class Proto>
inline bool ParseProtoFromMemory(StringPiece sp, Proto *proto) {
  return ParseProtoFromMemory(sp.data(), sp.size(), proto);
}

// Parses a proto from a file.  Returns true on success, false otherwise.
//
// Note: the entire content of the file should be the binary (not
// human-readable) serialization of a protocol buffer.
//
// Note: when we compile for Android, the proto parsing methods need to know the
// type of the message they are parsing.  We use template polymorphism for that.
template <class Proto>
bool ReadProtoFromFile(const std::string &filename, Proto *proto) {
  ScopedMmap scoped_mmap(filename);
  const MmapHandle &handle = scoped_mmap.handle();
  if (!handle.ok()) {
    return false;
  }
  return ParseProtoFromMemory(handle.to_stringpiece(), proto);
}

// Returns true if filename is the name of an existing file, and false
// otherwise.
bool FileExists(const std::string &filename);

// Returns true if dirpath is the path to an existing directory, and false
// otherwise.
bool DirectoryExists(const std::string &dirpath);

}  // namespace file_utils

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_FILE_FILE_UTILS_H_
