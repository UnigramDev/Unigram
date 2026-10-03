// FxCompile takes one entry point per file, and the translated shaders carry both stages in
// one - as the GLSL they came from does. So this is a wrapper: the vertex entry point of
// Composite.hlsl, which stays exactly as the generator wrote it.
//
// Do not hand-edit Composite.hlsl. See Tools/build_shaders.py in the diamond spike.

#include "Composite.hlsl"
