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

#include "lang_id/common/embedding-feature-extractor.h"

#include <stddef.h>

#include <string>
#include <vector>

#include "lang_id/common/fel/feature-extractor.h"
#include "lang_id/common/fel/feature-types.h"
#include "lang_id/common/fel/task-context.h"
#include "lang_id/common/lite_base/integral-types.h"
#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_strings/numbers.h"
#include "lang_id/common/lite_strings/str-split.h"
#include "lang_id/common/lite_strings/stringpiece.h"

namespace libtextclassifier3 {
namespace mobile {

bool GenericEmbeddingFeatureExtractor::Setup(TaskContext *context) {
  // Don't use version to determine how to get feature FML.
  const std::string features = context->Get(GetParamName("features"), "");
  const std::string embedding_names =
      context->Get(GetParamName("embedding_names"), "");
  const std::string embedding_dims =
      context->Get(GetParamName("embedding_dims"), "");

  // NOTE: unfortunately, LiteStrSplit returns a vector of StringPieces pointing
  // to the original string, in this case |features|, which is local to this
  // method.  We need to explicitly create new strings.
  for (StringPiece sp : LiteStrSplit(features, ';')) {
    embedding_fml_.emplace_back(sp);
  }

  // Same here.
  for (StringPiece sp : LiteStrSplit(embedding_names, ';')) {
    embedding_names_.emplace_back(sp);
  }

  std::vector<StringPiece> dim_strs = LiteStrSplit(embedding_dims, ';');
  for (const auto &dim_str : dim_strs) {
    int dim = 0;
    if (!LiteAtoi(dim_str, &dim)) {
      SAFTM_LOG(ERROR) << "Unable to parse " << dim_str;
      return false;
    }
    embedding_dims_.push_back(dim);
  }
  return true;
}

bool GenericEmbeddingFeatureExtractor::Init(TaskContext *context) {
  return true;
}

}  // namespace mobile
}  // namespace nlp_saft
