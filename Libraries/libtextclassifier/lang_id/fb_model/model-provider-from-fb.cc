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

#include "lang_id/fb_model/model-provider-from-fb.h"

#include <string>

#include "lang_id/common/file/file-utils.h"
#include "lang_id/common/file/mmap.h"
#include "lang_id/common/flatbuffers/embedding-network-params-from-flatbuffer.h"
#include "lang_id/common/flatbuffers/model-utils.h"
#include "lang_id/common/lite_strings/str-split.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

ModelProviderFromFlatbuffer::ModelProviderFromFlatbuffer(
    const std::string &filename)

    // Using mmap as a fast way to read the model bytes.  As the file is
    // unmapped only when the field scoped_mmap_ is destructed, the model bytes
    // stay alive for the entire lifetime of this object.
    : scoped_mmap_(new ScopedMmap(filename)) {
  Initialize(scoped_mmap_->handle().to_stringpiece());
}

ModelProviderFromFlatbuffer::ModelProviderFromFlatbuffer(
    FileDescriptorOrHandle fd)

    // Using mmap as a fast way to read the model bytes.  As the file is
    // unmapped only when the field scoped_mmap_ is destructed, the model bytes
    // stay alive for the entire lifetime of this object.
    : scoped_mmap_(new ScopedMmap(fd)) {
  Initialize(scoped_mmap_->handle().to_stringpiece());
}

ModelProviderFromFlatbuffer::ModelProviderFromFlatbuffer(
    FileDescriptorOrHandle fd, std::size_t offset, std::size_t size)

    // Using mmap as a fast way to read the model bytes.  As the file is
    // unmapped only when the field scoped_mmap_ is destructed, the model bytes
    // stay alive for the entire lifetime of this object.
    : scoped_mmap_(new ScopedMmap(fd, offset, size)) {
  Initialize(scoped_mmap_->handle().to_stringpiece());
}

void ModelProviderFromFlatbuffer::Initialize(StringPiece model_bytes) {
  // Note: valid_ was initialized to false.  In the code below, we set valid_ to
  // true only if all initialization steps completed successfully.  Otherwise,
  // we return early, leaving valid_ to its default value false.
  model_ = saft_fbs::GetVerifiedModelFromBytes(model_bytes);
  if (model_ == nullptr) {
    SAFTM_LOG(ERROR) << "Unable to initialize ModelProviderFromFlatbuffer";
    return;
  }

  // Initialize context_ parameters.
  if (!saft_fbs::FillParameters(*model_, &context_)) {
    // FillParameters already performs error logging.
    return;
  }

  // Init languages_.
  const std::string known_languages_str =
      context_.Get("supported_languages", "");
  for (StringPiece sp : LiteStrSplit(known_languages_str, ',')) {
    languages_.emplace_back(sp);
  }
  if (languages_.empty()) {
    SAFTM_LOG(ERROR) << "Unable to find list of supported_languages";
    return;
  }

  // Init nn_params_.
  if (!InitNetworkParams()) {
    // InitNetworkParams already performs error logging.
    return;
  }

  // Everything looks fine.
  valid_ = true;
}

bool ModelProviderFromFlatbuffer::InitNetworkParams() {
  const std::string kInputName = "language-identifier-network";
  StringPiece bytes =
      saft_fbs::GetInputBytes(saft_fbs::GetInputByName(model_, kInputName));
  if ((bytes.data() == nullptr) || bytes.empty()) {
    SAFTM_LOG(ERROR) << "Unable to get bytes for model input " << kInputName;
    return false;
  }
  std::unique_ptr<EmbeddingNetworkParamsFromFlatbuffer> nn_params_from_fb(
      new EmbeddingNetworkParamsFromFlatbuffer(bytes));
  if (!nn_params_from_fb->is_valid()) {
    SAFTM_LOG(ERROR) << "EmbeddingNetworkParamsFromFlatbuffer not valid";
    return false;
  }
  nn_params_ = std::move(nn_params_from_fb);
  return true;
}

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft
