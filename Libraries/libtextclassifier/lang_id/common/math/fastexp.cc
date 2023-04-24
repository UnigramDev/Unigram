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

#include "lang_id/common/math/fastexp.h"

namespace libtextclassifier3 {
namespace mobile {

const int FastMathClass::kBits;
const int FastMathClass::kMask1;
const int FastMathClass::kMask2;
constexpr float FastMathClass::kLogBase2OfE;

FastMathClass FastMathInstance;

// Data taken from util/math/fastmath.cc
const FastMathClass::Table FastMathClass::cache_ = {
    {0, 45549, 91345, 137391, 183686, 230233, 277032, 324086, 371395, 418961,
     466785, 514869, 563214, 611822, 660693, 709830, 759233, 808905, 858847,
     909060, 959545, 1010305, 1061340, 1112652, 1164243, 1216114, 1268267,
     1320703, 1373423, 1426430, 1479725, 1533309, 1587184, 1641351, 1695813,
     1750570, 1805625, 1860979, 1916633, 1972590, 2028850, 2085416, 2142289,
     2199470, 2256963, 2314767, 2372885, 2431319, 2490070, 2549140, 2608531,
     2668245, 2728282, 2788646, 2849337, 2910358, 2971710, 3033396, 3095416,
     3157773, 3220469, 3283505, 3346884, 3410606, 3474675, 3539091, 3603857,
     3668975, 3734447, 3800274, 3866458, 3933002, 3999907, 4067176, 4134809,
     4202810, 4271180, 4339922, 4409036, 4478526, 4548394, 4618640, 4689268,
     4760280, 4831677, 4903462, 4975636, 5048203, 5121164, 5194520, 5268275,
     5342431, 5416989, 5491952, 5567322, 5643101, 5719292, 5795897, 5872917,
     5950356, 6028215, 6106497, 6185204, 6264338, 6343902, 6423898, 6504329,
     6585196, 6666502, 6748250, 6830442, 6913080, 6996166, 7079704, 7163696,
     7248143, 7333049, 7418416, 7504247, 7590543, 7677309, 7764545, 7852255,
     7940441, 8029106, 8118253, 8207884, 8298001}
};

}  // namespace mobile
}  // namespace nlp_saft
