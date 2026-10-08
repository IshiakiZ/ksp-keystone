# Building

`./build.sh` builds Keystone and installs it into the game; `./build.sh check` only compiles; `./build.sh dist`
builds into `GameData/` here without touching the game; `./build.sh shaders` packs the lens shader again after
`tools/shaderpack/lens.glsl` has been changed. It needs the .NET SDK and a copy of the game (`KSP_DIR=/path` if
it is not where Steam puts it). `KeystoneTUFX.dll` is built only where the TUFX mod is installed in that game.

This repository is made from a workspace that holds several mods side by side; changes arrive here from there.

The lens shader is OpenGL text packed into a bundle without the Unity editor (`tools/shaderpack`). Packing
needs a copy of the game with both expansions, because the packer takes the layout of a shader file from
one of the game's own bundles. What it writes holds Keystone's own shader text and Unity's description of
that layout (which every bundle made by Unity carries); nothing of the game's own shader is left in it.
