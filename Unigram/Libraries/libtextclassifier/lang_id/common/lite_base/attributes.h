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

// Various macros related to function inlining.

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_ATTRIBUTES_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_ATTRIBUTES_H_

// SAFTM_HAVE_ATTRIBUTE
//
// A function-like feature checking macro that is a wrapper around
// `__has_attribute`, which is defined by GCC 5+ and Clang and evaluates to a
// nonzero constant integer if the attribute is supported or 0 if not.
//
// It evaluates to zero if `__has_attribute` is not defined by the compiler.
//
// GCC: https://gcc.gnu.org/gcc-5/changes.html
// Clang: https://clang.llvm.org/docs/LanguageExtensions.html
#ifdef __has_attribute
#define SAFTM_HAVE_ATTRIBUTE(x) __has_attribute(x)
#else
#define SAFTM_HAVE_ATTRIBUTE(x) 0
#endif

// SAFTM_MUST_USE_RESULT
//
// Tells the compiler to warn about unused return values for functions declared
// with this macro. The macro must appear as the very first part of a function
// declaration or definition:
//
// Example:
//
//   SAFTM_MUST_USE_RESULT Sprocket* AllocateSprocket();
//
// This placement has the broadest compatibility with GCC, Clang, and MSVC, with
// both defs and decls, and with GCC-style attributes, MSVC declspec, C++11
// and C++17 attributes.
//
// SAFTM_MUST_USE_RESULT allows using cast-to-void to suppress the unused result
// warning. For that, warn_unused_result is used only for clang but not for gcc.
// https://gcc.gnu.org/bugzilla/show_bug.cgi?id=66425
#if SAFTM_HAVE_ATTRIBUTE(nodiscard)
#define SAFTM_MUST_USE_RESULT [[nodiscard]]
#elif defined(__clang__) && SAFTM_HAVE_ATTRIBUTE(warn_unused_result)
#define SAFTM_MUST_USE_RESULT __attribute__((warn_unused_result))
#else
#define SAFTM_MUST_USE_RESULT
#endif

#if defined(__GNUC__) && \
    (__GNUC__ > 3 || (__GNUC__ == 3 && __GNUC_MINOR__ >= 1))

// For functions we want to force inline.
// Introduced in gcc 3.1.
#define SAFTM_ATTRIBUTE_ALWAYS_INLINE __attribute__((always_inline))

// For functions we don't want to inline, e.g., to keep code size small.
#define SAFTM_ATTRIBUTE_NOINLINE __attribute__((noinline))

#elif defined(_MSC_VER)
#define SAFTM_ATTRIBUTE_ALWAYS_INLINE __forceinline

#define SAFTM_ATTRIBUTE_NOINLINE __declspec(noinline)
#else

// Other compilers will have to figure it out for themselves.
#define SAFTM_ATTRIBUTE_ALWAYS_INLINE
#define SAFTM_ATTRIBUTE_NOINLINE
#endif  // big condition on two lines.

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_ATTRIBUTES_H_
