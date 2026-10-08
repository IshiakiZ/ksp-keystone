# For mod authors

Reference `Keystone.dll`, say that you need it, and register:

```csharp
[assembly: KSPAssemblyDependency("Keystone", 0, 3)]

Mod mod = Kit.Register("Smooth Portraits", "0.1.0", "A line about what the mod does.");
Toggle on = mod.Toggle("on", "Smooth portraits", true, "What the pointer shows when held over it.");
Slider most = mod.Slider("most", "At most", 60f, 10f, 144f, " a second", 0);
```

A setting takes what was saved for it the moment it is made, and can be read ever after as its `Value`
(or used directly: a `Toggle` is a `bool`, a `Slider` a `float`). `Changed` on a setting, or on the mod,
tells you when the player moves one. There are five kinds: `Toggle`, `Slider` (dragged, or a number typed
into the box beside it), `Choice` (one of a few named things), `Tint` (a colour: a box that opens a
gradient to click or drag on, with the colour's six hex digits to type) and `Heading` (a line of words
between settings).

The number after `"Keystone"` in the dependency is the least version the mod can do with: `0, 1` for the
window and the settings, `0, 2` for the lens, `0, 3` for boxes that can be typed in and the gradient.

| | |
| --- | --- |
| `Kit.Register(name, version, about)` | a mod says it is here; gives back its `Mod` |
| `mod.Toggle / Slider / Choice / Tint / Heading` | a setting on its page |
| `mod.Panel = () => { ... }` | more for the page, drawn by the mod itself with the game's `GUILayout` |
| `mod.Kept(key)`, `mod.Keep(key, text)` | other text worth keeping in the same file (a list the mod draws itself) |
| `mod.Changed`, `setting.Changed` | the player changed something |
| `Host.Swatch`, `Host.Pick`, `Host.Mixer`, `Host.Row`, `Host.Rich`, `Host.Small`, `Host.Plain` | what the window is drawn with, for a `Panel`: a patch of colour, one that can be clicked, the gradient a colour is chosen on, a labelled slider, and the window's ways of writing |
| `Host.Typed(name, text, width, out entered, out mine)` | a box to type in. Give it a name that begins with `Host.TypedName`, and the game's own keys are held off while it has the keyboard |
| `Reach.Field / Method / Call<T>` | the game's non-public members, looked up once; missing ones are reported in the log once and come back as nothing, so a mod can go without instead of failing every frame |
| `Kit.Log(who, text)` | a line in the game's log with the mod's name in front |
| `Lens.Best`, `Look` | the lens (see [The lens](The-lens.md)): `Lens.Best?.Use(camera, look)` every frame while it is wanted, `Off()` when it is not |
