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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_FLATBUFFERS_MODEL_UTILS_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_FLATBUFFERS_MODEL_UTILS_H_

#include <stddef.h>

#include <string>

#include "lang_id/common/fel/task-context.h"
#include "lang_id/common/flatbuffers/model_generated.h"
#include "lang_id/common/lite_base/integral-types.h"
#include "lang_id/common/lite_strings/stringpiece.h"

namespace libtextclassifier3 {
namespace saft_fbs {

// Verifies that the |num_bytes| bytes that start at |data| represent a valid
// Model flatbuffer.  If so, returns that Model.  Otherwise, returns nullptr.
//
// Note: if the Model has the crc32 field, this method checks that the Model
// checksum matches that field; if they don't match, the Model is considered
// invalid, and this function returns nullptr.  The checksum test is in addition
// to the standard flatbuffer validity checking.
const Model *GetVerifiedModelFromBytes(const char *data, size_t num_bytes);

// Convenience StringPiece version of GetVerifiedModelFromBytes.
inline const Model *GetVerifiedModelFromBytes(mobile::StringPiece bytes) {
  return GetVerifiedModelFromBytes(bytes.data(), bytes.size());
}

// Returns the |model| input with specified |name|.  Returns nullptr if no such
// input exists.  If |model| contains multiple inputs with that |name|, returns
// the first one (model builders should avoid building such models).
const ModelInput *GetInputByName(const Model *model, const std::string &name);

// Returns a StringPiece pointing to the bytes for the content of |input|.  In
// case of errors, returns StringPiece(nullptr, 0).
mobile::StringPiece GetInputBytes(const ModelInput *input);

// Fills parameters from |context|, based on the parameters from |model|.
// Returns false if any error is encountered, true otherwise.  In the case of an
// error, some parameters may have been added to |context| (e.g., if we find a
// problem with the 3rd parameter, the first 2 have been added).
bool FillParameters(const Model &model, mobile::TaskContext *context);

// Returns the CRC32 checksum of |model|.  This checksum is computed over the
// entire information from the model (including the bytes of the inputs),
// *except* the crc32 field.  Hence, when a model is build, one can store the
// result of this function into that field; on the user side, one can check that
// the result of this function matches the crc32 field, to guard against model
// corruption.  GetVerifiedModelFromBytes performs this check.
mobile::uint32 ComputeCrc2Checksum(const Model *model);

// Returns true if we have clear evidence that |model| fails its checksum.
//
// E.g., if |model| has the crc32 field, and the value of that field does not
// match the checksum, then this function returns true.  If there is no crc32
// field, then we don't know what the original (at build time) checksum was, so
// we don't know anything clear and this function returns false.
bool ClearlyFailsChecksum(const Model &model);

}  // namespace saft_fbs
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_FLATBUFFERS_MODEL_UTILS_H_
