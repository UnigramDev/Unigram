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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_MODEL_PROVIDER_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_MODEL_PROVIDER_H_

#include <string>
#include <vector>

#include "lang_id/common/embedding-network-params.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

// Interface for accessing parameters for the LangId model.
//
// Note: some clients prefer to include the model parameters in the binary,
// others prefer loading them from a separate file.  This file provides a common
// interface for these alternative mechanisms.
class ModelProvider {
 public:
  virtual ~ModelProvider() = default;

  // Returns true if this ModelProvider has been succesfully constructed (e.g.,
  // can return false if an underlying model file could not be read).  Clients
  // should not use invalid ModelProviders.
  bool is_valid() { return valid_; }

  // Returns the TaskContext with parameters for the LangId model.  E.g., one
  // important parameter specifies the features to use.
  virtual const TaskContext *GetTaskContext() const = 0;

  // Returns parameters for the underlying Neurosis feed-forward neural network.
  virtual const EmbeddingNetworkParams *GetNnParams() const = 0;

  // Returns list of languages recognized by the model.  Each element of the
  // returned vector should be a BCP-47 language code (e.g., "en", "ro", etc).
  // Language at index i from the returned vector corresponds to softmax label
  // i.
  virtual std::vector<std::string> GetLanguages() const = 0;

 protected:
  bool valid_ = false;
};

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_MODEL_PROVIDER_H_
