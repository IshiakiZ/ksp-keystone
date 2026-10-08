# The lens

`Keystone.Lens` does to a camera's finished picture what a lens, a shutter and a film do: depth of field
(what is nearer or further than the focus is drawn as discs, as a real lens draws it, the nearer things
spreading over what lies behind them), the smear of whatever moved while the shutter was open, grain,
darker corners, colours parting towards the corners, and the bend of a wide lens. A mod fills in a `Look`
(focal length, sensor height, f-number, focus distance, how wide the blur may get, the part of a frame
the shutter is open for, and how much of each of the rest) and hands it over every frame. Cinema Camera
is the mod that uses it.

It is one shader of Keystone's own (`tools/shaderpack/lens.glsl`), run twice on the picture the game has
just finished: once at half size for what is out of focus, once for everything else. It needs no other mod.

* **Mac and Linux** (OpenGL): the shader ships with Keystone as `PluginData/lens.bundle`. Made and tried
  on a Mac; not tried on Linux.
* **Windows** (Direct3D 11): that shader cannot be used, and one built for Direct3D does not ship yet
  (it has to be built in the Unity editor; Keystone looks for it as `PluginData/lens-windows.bundle`).
  Until then `Lens.Best` is TUFX's post-processing where the TUFX mod is installed, by way of
  `KeystoneTUFX.dll` in this folder (the game loads it only where TUFX is; where TUFX is not, the game's
  log has a line saying it was left out, which is as it should be), and nothing otherwise. A mod should
  say so to the player when `Lens.Best` is null: `Lens.WhyNot` has the words. With `-force-glcore` the game
  runs on OpenGL on Windows too and the lens should work as on a Mac. **Nothing here has been run on
  Windows.**

The game's log says which it is: `[Keystone] has a lens of its own`, or `no lens of its own:` and why.

It is small on purpose and will grow with what the mods turn out to share (key bindings and saving things
with a save game are the likely next ones).
