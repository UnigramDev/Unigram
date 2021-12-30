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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FB_MODEL_MODEL_PROVIDER_FROM_FB_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FB_MODEL_MODEL_PROVIDER_FROM_FB_H_

#include <cstddef>
#include <memory>
#include <string>
#include <vector>

#include "lang_id/common/fel/task-context.h"
#include "lang_id/common/file/mmap.h"
#include "lang_id/common/flatbuffers/model_generated.h"
#include "lang_id/common/lite_strings/stringpiece.h"
#include "lang_id/model-provider.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

// ModelProvider for LangId, based on a SAFT model in flatbuffer format.
class ModelProviderFromFlatbuffer : public ModelProvider {
 public:
  // Constructs a model provider based on a flatbuffer-format SAFT model from
  // |filename|.
  explicit ModelProviderFromFlatbuffer(const std::string &filename);

  // Constructs a model provider based on a flatbuffer-format SAFT model from
  // file descriptor |fd|.
  explicit ModelProviderFromFlatbuffer(FileDescriptorOrHandle fd);

  // Constructs a model provider based on a flatbuffer-format SAFT model from
  // file descriptor |fd|.
  ModelProviderFromFlatbuffer(FileDescriptorOrHandle fd, std::size_t offset,
                              std::size_t size);

  // Constructs a model provider from a flatbuffer-format SAFT model the bytes
  // of which are already in RAM (size bytes starting from address data).
  // Useful if you "transport" these bytes otherwise than via a normal file
  // (e.g., if you embed them somehow in your binary).
  //
  // IMPORTANT: |data| should be alive during the lifetime of the
  // newly-constructed ModelProviderFromFlatbuffer.  This is trivial to ensure
  // for data that's statically embedded in your binary, but more complex in
  // other cases.  To avoid overhead (e.g., heap allocation), this method does
  // not make a private copy of the data.  In general, the ownership of the
  // newly-constructed ModelProviderFromFlatbuffer is immediately passed to a
  // LangId object (which doesn't pass it further); hence, one needs to make
  // sure |data| is alive during the lifetime of that LangId object.
  ModelProviderFromFlatbuffer(const char *data, std::size_t size) {
    StringPiece model_bytes(data, size);
    Initialize(model_bytes);
  }

  ~ModelProviderFromFlatbuffer() override = default;

  const TaskContext *GetTaskContext() const override {
    return &context_;
  }

  const EmbeddingNetworkParams *GetNnParams() const override {
    return nn_params_.get();
  }

  std::vector<std::string> GetLanguages() const override { return languages_; }

 private:
  // Initializes the fields of this class based on the flatbuffer from
  // |model_bytes|.  These bytes are supposed to be the representation of a
  // Model flatbuffer and should be alive during the lifetime of this object.
  void Initialize(StringPiece model_bytes);

  // Initializes nn_params_ based on model_.
  bool InitNetworkParams();

  // If filename-based constructor is used, scoped_mmap_ keeps the file mmapped
  // during the lifetime of this object, such that references inside the Model
  // flatbuffer from those bytes remain valid.
  const std::unique_ptr<ScopedMmap> scoped_mmap_;

  // Pointer to the flatbuffer from
  //
  // (a) [if filename constructor was used:] the bytes mmapped by scoped_mmap_
  // (for safety considerations, see comment for that field), or
  //
  // (b) [of (data, size) constructor was used:] the bytes from [data,
  // data+size).  Please read carefully the doc for that constructor.
  const saft_fbs::Model *model_;

  // Context returned by this model provider.  We set its parameters based on
  // model_, at construction time.
  TaskContext context_;

  // List of supported languages, see GetLanguages().  We expect this list to be
  // specified by the ModelParameter named "supported_languages" from model_.
  std::vector<std::string> languages_;

  // EmbeddingNetworkParams, see GetNnParams().  Set based on the ModelInput
  // named "language-identifier-network" from model_.
  std::unique_ptr<EmbeddingNetworkParams> nn_params_;
};

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FB_MODEL_MODEL_PROVIDER_FROM_FB_H_
