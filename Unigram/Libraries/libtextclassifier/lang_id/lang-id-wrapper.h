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

#ifndef LIBTEXTCLASSIFIER_LANG_ID_LANG_ID_WRAPPER_H_
#define LIBTEXTCLASSIFIER_LANG_ID_LANG_ID_WRAPPER_H_

#include <string>
#include <utility>
#include <vector>

#include "lang_id/lang-id.h"

namespace libtextclassifier3 {

namespace langid {

// Loads the LangId model from a given path.
std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> LoadFromPath(
    const std::string& path);

// Loads the LangId model from a file descriptor.
//std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> LoadFromDescriptor(
//    const int fd);

// Loads the LangId model from a buffer. The buffer needs to outlive the LangId
// instance.
std::unique_ptr<libtextclassifier3::mobile::lang_id::LangId> LoadFromUnownedBuffer(
    const char* buffer, int size);

// Returns the LangId predictions (locale, confidence) from the given LangId
// model. The maximum number of predictions returned will be computed internally
// relatively to the noise threshold.
std::vector<std::pair<std::string, float>> GetPredictions(
    const libtextclassifier3::mobile::lang_id::LangId* model, const std::string& text);

// Same as above but takes a char pointer and byte length.
std::vector<std::pair<std::string, float>> GetPredictions(
    const libtextclassifier3::mobile::lang_id::LangId* model, const char* text,
    int text_size);

// Returns the language tags string from the given LangId model. The language
// tags will be filtered internally by the LangId threshold.
std::string GetLanguageTags(const libtextclassifier3::mobile::lang_id::LangId* model,
                            const std::string& text);

}  // namespace langid

}  // namespace libtextclassifier3

#endif  // LIBTEXTCLASSIFIER_LANG_ID_LANG_ID_WRAPPER_H_
