# For players

* **Install:** copy the `Keystone` folder into `GameData`, next to the mod that needs it. One copy serves
  every mod that uses it.
* **The window** opens from the keystone button on the game's toolbar, or with the modifier key and K
  (Option-K on a Mac, Alt-K elsewhere), in every scene. Each mod has a page with its settings. Hold the
  pointer over a setting to read what it does at the foot of the window. `Defaults` puts that page back
  as installed.
* **Numbers can be typed.** Click the box beside a slider, type, and press Enter (or click elsewhere). A
  number outside the slider's range is brought to its nearest end. While you are typing, the game's own
  keys are held off, so an M does not open the map.
* **Colours are chosen on a gradient.** Click a colour box: a square of every shade of one colour to click
  or drag in, a strip under it for which colour, and the colour's six hex digits to type.
* **Settings are kept** in `GameData/Keystone/PluginData/<mod>.cfg`, written a second after you change
  something. Deleting a file there puts that mod back as installed. (They are kept in `PluginData`
  because the game does not read that folder as part of its own data, so saving a setting never makes
  it, or ModuleManager, rebuild anything at the next start.)
