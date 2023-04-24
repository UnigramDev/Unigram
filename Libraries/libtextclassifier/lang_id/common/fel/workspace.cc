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

#include "lang_id/common/fel/workspace.h"

#include <atomic>
#include <string>

namespace libtextclassifier3 {
namespace mobile {

// static
int GetFreshTypeId() {
  // Static local below is initialized the first time this method is run.
  static std::atomic<int> counter(0);
  return counter++;
}

std::string WorkspaceRegistry::DebugString() const {
  std::string str;
  for (auto &it : workspace_names_) {
    const std::string &type_name = workspace_types_.at(it.first);
    for (size_t index = 0; index < it.second.size(); ++index) {
      const std::string &workspace_name = it.second[index];
      str.append("\n  ");
      str.append(type_name);
      str.append(" :: ");
      str.append(workspace_name);
    }
  }
  return str;
}

VectorIntWorkspace::VectorIntWorkspace(int size) : elements_(size) {}

VectorIntWorkspace::VectorIntWorkspace(int size, int value)
    : elements_(size, value) {}

VectorIntWorkspace::VectorIntWorkspace(const std::vector<int> &elements)
    : elements_(elements) {}

std::string VectorIntWorkspace::TypeName() { return "Vector"; }

}  // namespace mobile
}  // namespace nlp_saft
