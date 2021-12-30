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

#include "lang_id/common/fel/task-context.h"

#include <string>

#include "lang_id/common/lite_strings/numbers.h"

namespace libtextclassifier3 {
namespace mobile {

std::string TaskContext::GetInputPath(const std::string &name) const {
  auto it = inputs_.find(name);
  if (it != inputs_.end()) {
    return it->second;
  }
  return "";
}

void TaskContext::SetInputPath(const std::string &name,
                               const std::string &path) {
  inputs_[name] = path;
}

std::string TaskContext::Get(const std::string &name,
                             const char *defval) const {
  auto it = parameters_.find(name);
  if (it != parameters_.end()) {
    return it->second;
  }
  return defval;
}

int TaskContext::Get(const std::string &name, int defval) const {
  const std::string s = Get(name, "");
  int value = defval;
  if (LiteAtoi(s, &value)) {
    return value;
  }
  return defval;
}

float TaskContext::Get(const std::string &name, float defval) const {
  const std::string s = Get(name, "");
  float value = defval;
  if (LiteAtof(s, &value)) {
    return value;
  }
  return defval;
}

bool TaskContext::Get(const std::string &name, bool defval) const {
  std::string value = Get(name, "");
  return value.empty() ? defval : value == "true";
}

void TaskContext::SetParameter(const std::string &name,
                               const std::string &value) {
  parameters_[name] = value;
}

}  // namespace mobile
}  // namespace nlp_saft
