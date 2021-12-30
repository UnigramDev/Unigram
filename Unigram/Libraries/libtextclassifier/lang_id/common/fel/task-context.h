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

#ifndef TC3_STD_STRING_IMPORT
#define TC3_STD_STRING_IMPORT
#include <string>

namespace libtextclassifier3 {
using string = std::string;
template <class CharT, class Traits = std::char_traits<CharT>,
          class Allocator = std::allocator<CharT> >
using basic_string = std::basic_string<CharT, Traits, Allocator>;
}  // namespace libtextclassifier3
#endif
#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_TASK_CONTEXT_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_TASK_CONTEXT_H_

#include <map>
#include <string>

namespace libtextclassifier3 {
namespace mobile {

// Class that provides access to model parameter and inputs.
//
// Note: This class is related to the servers-side nlp_saft::TaskContext, but it
// has been simplified to reduce code dependencies.
class TaskContext {
 public:
  // Returns path for the input named |name|.  Returns empty string ("") if
  // there is no input with that name.  Note: this can be a standard file path,
  // or a path in a more special file system.
  std::string GetInputPath(const std::string &name) const;

  // Sets path for input |name|.  Previous path, if any, is overwritten.
  void SetInputPath(const std::string &name, const std::string &path);

  // Returns parameter value.  If the parameter is not specified in this
  // context, the default value is returned.
  std::string Get(const std::string &name, const char *defval) const;
  int Get(const std::string &name, int defval) const;
  float Get(const std::string &name, float defval) const;
  bool Get(const std::string &name, bool defval) const;

  // Sets value of parameter |name| to |value|.
  void SetParameter(const std::string &name, const std::string &value);

 private:
  // Maps input name -> path.
  std::map<std::string, std::string> inputs_;

  // Maps parameter name -> value.
  std::map<std::string, std::string> parameters_;
};

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_TASK_CONTEXT_H_
