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

#include "lang_id/fb_model/lang-id-from-fb.h"

#include <string>

#include "lang_id/fb_model/model-provider-from-fb.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

std::unique_ptr<LangId> GetLangIdFromFlatbufferFile(
    const std::string &filename) {
  std::unique_ptr<ModelProvider> model_provider(
      new ModelProviderFromFlatbuffer(filename));

  // NOTE: we avoid absl (including absl::make_unique), due to b/113350902
  return std::unique_ptr<LangId>(  // NOLINT
      new LangId(std::move(model_provider)));
}

std::unique_ptr<LangId> GetLangIdFromFlatbufferFileDescriptor(
    FileDescriptorOrHandle fd) {
  std::unique_ptr<ModelProvider> model_provider(
      new ModelProviderFromFlatbuffer(fd));

  // NOTE: we avoid absl (including absl::make_unique), due to b/113350902
  return std::unique_ptr<LangId>(  // NOLINT
      new LangId(std::move(model_provider)));
}

std::unique_ptr<LangId> GetLangIdFromFlatbufferFileDescriptor(
    FileDescriptorOrHandle fd, size_t offset, size_t num_bytes) {
  std::unique_ptr<ModelProvider> model_provider(
      new ModelProviderFromFlatbuffer(fd, offset, num_bytes));

  // NOTE: we avoid absl (including absl::make_unique), due to b/113350902
  return std::unique_ptr<LangId>(  // NOLINT
      new LangId(std::move(model_provider)));
}

std::unique_ptr<LangId> GetLangIdFromFlatbufferBytes(const char *data,
                                                     size_t num_bytes) {
  std::unique_ptr<ModelProvider> model_provider(
      new ModelProviderFromFlatbuffer(data, num_bytes));

  // NOTE: we avoid absl (including absl::make_unique), due to b/113350902
  return std::unique_ptr<LangId>(  // NOLINT
      new LangId(std::move(model_provider)));
}

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft
