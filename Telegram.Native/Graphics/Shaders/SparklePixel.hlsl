// FxCompile takes one entry point per file, and the translated shaders carry both stages in
// one - as the GLSL they came from does. So this is a wrapper: the pixel entry point of
// Sparkle.hlsl, which stays exactly as the generator wrote it.
//
// Do not hand-edit Sparkle.hlsl. See Tools/build_shaders.py in the diamond spike.

#include "Sparkle.hlsl"
