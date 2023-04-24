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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_MACROS_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_MACROS_H_

#define SAFTM_DISALLOW_COPY_AND_ASSIGN(TypeName) \
  TypeName(const TypeName &) = delete;         \
  TypeName &operator=(const TypeName &) = delete

// The SAFTM_FALLTHROUGH_INTENDED macro can be used to annotate implicit
// fall-through between switch labels:
//
//  switch (x) {
//    case 40:
//    case 41:
//      if (truth_is_out_there) {
//        ++x;
//        SAFTM_FALLTHROUGH_INTENDED;  // Use instead of/along with annotations
//                                     // in comments.
//      } else {
//        return x;
//      }
//    case 42:
//      ...
//
//  As shown in the example above, the SAFTM_FALLTHROUGH_INTENDED macro should
//  be followed by a semicolon. It is designed to mimic control-flow statements
//  like 'break;', so it can be placed in most places where 'break;' can, but
//  only if there are no statements on the execution path between it and the
//  next switch label.
//
//  When compiled with clang, the SAFTM_FALLTHROUGH_INTENDED macro is expanded
//  to [[clang::fallthrough]] attribute, which is analysed when performing
//  switch labels fall-through diagnostic ('-Wimplicit-fallthrough').  See clang
//  documentation on language extensions for details:
//  http://clang.llvm.org/docs/AttributeReference.html#fallthrough-clang-fallthrough
//
//  When used with unsupported compilers, the SAFTM_FALLTHROUGH_INTENDED macro
//  has no effect on diagnostics.
//
//  In either case this macro has no effect on runtime behavior and performance
//  of code.
#if defined(__clang__) && defined(__has_warning)
#if __has_feature(cxx_attributes) && __has_warning("-Wimplicit-fallthrough")
#define SAFTM_FALLTHROUGH_INTENDED [[clang::fallthrough]]  // NOLINT
#endif
#endif

#ifndef SAFTM_FALLTHROUGH_INTENDED
#define SAFTM_FALLTHROUGH_INTENDED \
  do {                           \
  } while (0)
#endif

// SAFTM_UNIQUE_ID(prefix) expands to a unique id that starts with prefix.
//
// The current implementation expands to prefix_<line_number>; hence, multiple
// uses of this macro with the same prefix and on the same line will result in
// the same identifier name.  In those cases, if you need different ids, we
// suggest you use different prefixes.
//
// Implementation is tricky; for more info, see
// https://stackoverflow.com/questions/1597007/creating-c-macro-with-and-line-token-concatenation-with-positioning-macr
#define SAFTM_UNIQUE_ID_INTERNAL2(x, y)  x ## y
#define SAFTM_UNIQUE_ID_INTERNAL(x, y)   SAFTM_UNIQUE_ID_INTERNAL2(x, y)
#define SAFTM_UNIQUE_ID(prefix)  SAFTM_UNIQUE_ID_INTERNAL(prefix ## _, __LINE__)

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_MACROS_H_
