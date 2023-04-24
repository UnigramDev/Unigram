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

#include "lang_id/lang-id-wrapper.h"

#include <fcntl.h>

#include "lang_id/fb_model/lang-id-from-fb.h"
#include "lang_id/lang-id.h"

namespace libtextclassifier3 {

namespace langid {

std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> LoadFromPath(
    const std::string& langid_model_path) {
  std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> langid_model =
      libtextclassifier3::mobile::lang_id::GetLangIdFromFlatbufferFile(langid_model_path);
  return langid_model;
}

//std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> LoadFromDescriptor(
//    const int langid_fd) {
//  std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> langid_model =
//      libtextclassifier3::mobile::lang_id::GetLangIdFromFlatbufferFileDescriptor(
//          langid_fd);
//  return langid_model;
//}

std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> LoadFromUnownedBuffer(
    const char* buffer, int size) {
  std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> langid_model =
      libtextclassifier3::mobile::lang_id::GetLangIdFromFlatbufferBytes(buffer, size);
  return langid_model;
}

std::vector<std::pair<std::string, float>> GetPredictions(
    const libtextclassifier3::mobile::lang_id::LangId* model, const std::string& text) {
  return GetPredictions(model, text.data(), text.size());
}

std::vector<std::pair<std::string, float>> GetPredictions(
    const libtextclassifier3::mobile::lang_id::LangId* model, const char* text,
    int text_size) {
  std::vector<std::pair<std::string, float>> prediction_results;
  if (model == nullptr) {
    return prediction_results;
  }

  const float noise_threshold =
      model->GetFloatProperty("text_classifier_langid_noise_threshold", -1.0f);

  // Speed up the things by specifying the max results we want. For example, if
  // the noise threshold is 0.1, we don't need more than 10 results.
  const int max_results =
      noise_threshold < 0.01
          ? -1  // -1 means FindLanguages returns all predictions
          : static_cast<int>(1 / noise_threshold) + 1;

  libtextclassifier3::mobile::lang_id::LangIdResult langid_result;
  model->FindLanguages(text, text_size, &langid_result, max_results);
  for (int i = 0; i < langid_result.predictions.size(); i++) {
    const auto& prediction = langid_result.predictions[i];
    if (prediction.second >= noise_threshold && prediction.first != "und") {
      prediction_results.push_back({prediction.first, prediction.second});
    }
  }
  return prediction_results;
}

std::string GetLanguageTags(const libtextclassifier3::mobile::lang_id::LangId* model,
                            const std::string& text) {
  const std::vector<std::pair<std::string, float>>& predictions =
      GetPredictions(model, text);
  const float threshold =
      model->GetFloatProperty("text_classifier_langid_threshold", -1.0f);
  std::string detected_language_tags = "";
  bool first_accepted_language = true;
  for (int i = 0; i < predictions.size(); i++) {
    const auto& prediction = predictions[i];
    if (threshold >= 0.f && prediction.second < threshold) {
      continue;
    }
    if (first_accepted_language) {
      first_accepted_language = false;
    } else {
      detected_language_tags += ",";
    }
    detected_language_tags += prediction.first;
  }
  return detected_language_tags;
}

}  // namespace langid

}  // namespace libtextclassifier3
