# Keystone

The base that a family of small Kerbal Space Program 1 mods stands on: Smooth Portraits, Kerbal Skins and
Cinema Camera so far. On its own it does nothing you can see except offer a settings window, in which each
mod that uses it gets a page. It also has a lens that any of them can put in front of the game's camera.

For Kerbal Space Program 1.12.x.

## For players

* **Install:** copy the `Keystone` folder into `GameData`, next to the mod that needs it. One copy serves
  every mod that uses it.
* **The window** opens from the keystone button on the game's toolbar, or with Option-K on a Mac and Alt-K
  elsewhere, in every scene. Drag a slider or type a number into the box beside it; click a colour box to
  choose a colour on a gradient. Hold the pointer over a setting to read what it does. `Defaults` puts a
  page back as installed.
* **Settings are kept** in `GameData/Keystone/PluginData/<mod>.cfg`. Deleting a file there puts that mod
  back as installed.

## For a mod

Reference `Keystone.dll`, say that you need it, and register:

```csharp
[assembly: KSPAssemblyDependency("Keystone", 0, 3)]

Mod mod = Kit.Register("Smooth Portraits", "0.1.0", "A line about what the mod does.");
Toggle on = mod.Toggle("on", "Smooth portraits", true, "What the pointer shows when held over it.");
Slider most = mod.Slider("most", "At most", 60f, 10f, 144f, " a second", 0);
```

A setting takes what was saved for it the moment it is made, and is read ever after as its `Value`.
[Everything a mod can use](docs/For-mod-authors.md).

## The lens

`Keystone.Lens` does to a camera's finished picture what a lens, a shutter and a film do: depth of field,
motion blur, grain, darker corners, colour fringes and the bend of a wide lens, from one shader of its own
and no other mod. A mod fills in a `Look` and hands it over every frame; Cinema Camera is the mod that
uses it. [How it works, and where](docs/The-lens.md).

## Which systems it works on

Made and tried on a Mac; **not yet run on Windows or Linux**. The window, the settings and the tools are
plain C# with nothing in them that belongs to one system. The lens ships as an OpenGL shader (Mac and
Linux); on Windows' Direct3D it does not work yet, and a mod falls back to TUFX where that is installed.

## Building

`./build.sh` builds Keystone and installs it into the game; it needs the .NET SDK and a copy of the game.
[More](docs/Building.md).

## Licence

[MIT](LICENSE).
