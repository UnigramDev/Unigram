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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FB_MODEL_LANG_ID_FROM_FB_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FB_MODEL_LANG_ID_FROM_FB_H_

#include <stddef.h>

#include <memory>
#include <string>

#include "lang_id/common/file/mmap.h"
#include "lang_id/lang-id.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

// Returns a LangId built using the SAFT model in flatbuffer format from
// |filename|.
std::unique_ptr<LangId> GetLangIdFromFlatbufferFile(
    const std::string &filename);

// Returns a LangId built using the SAFT model in flatbuffer format from
// given file descriptor.
std::unique_ptr<LangId> GetLangIdFromFlatbufferFileDescriptor(
    FileDescriptorOrHandle fd);

// Returns a LangId built using the SAFT model in flatbuffer format from
// given file descriptor, staring at |offset| and of size |num_bytes|.
std::unique_ptr<LangId> GetLangIdFromFlatbufferFileDescriptor(
    FileDescriptorOrHandle fd, size_t offset, size_t num_bytes);

// Returns a LangId built using the SAFT model in flatbuffer format from
// the |num_bytes| bytes that start at address |data|.
//
// IMPORTANT: the model bytes must be alive during the lifetime of the returned
// LangId.  To avoid overhead (e.g., heap allocation), this method does not make
// a private copy of the model bytes.  Avoiding overhead is the main reason we
// use flatbuffers.
std::unique_ptr<LangId> GetLangIdFromFlatbufferBytes(const char *data,
                                                     size_t num_bytes);

// Convenience string-based version of GetLangIdFromFlatbufferBytes.
//
// IMPORTANT: |bytes| must be alive during the lifetime of the returned LangId.
inline std::unique_ptr<LangId> GetLangIdFromFlatbufferBytes(
    const std::string &bytes) {
  return GetLangIdFromFlatbufferBytes(bytes.data(), bytes.size());
}

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FB_MODEL_LANG_ID_FROM_FB_H_
