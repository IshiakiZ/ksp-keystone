# Keystone

The base that a family of Kerbal Space Program 1 mods stands on, and one window for all of them: every mod installed
is listed there, with switches and settings for the mods made for it.

![Keystone's window beside a rocket on the pad: the list of mods, then each mod's settings, then what each mod costs](src/Keystone/img/keystone.gif)

For Kerbal Space Program 1.12.x. It needs no other mod.

## Install

Copy the `Keystone` folder from the download into your KSP's `GameData`. One copy serves every mod that needs it.

## Use

* **The window** opens from the keystone button on the game's toolbar, or with Alt-K (Option-K on a Mac), in every
  scene. Click a mod for its settings.
* **Settings are kept** in `GameData/Keystone/PluginData/<mod>.cfg`. Deleting a file there puts that mod back as
  installed.
* **What each mod costs:** a page that times every mod's work (it needs the Harmony mod).
* **Newer versions** can be looked for online. That is off until you switch it on, and nothing is downloaded or
  installed.

## For mod authors

A mod registers with one line and gets a page in the window, settings that are kept and keys a player can change.
Keystone also has a lens and a film that any mod can use.
[Everything a mod can use](https://github.com/IshiakiZ/ksp-keystone/wiki/For-mod-authors).

## Works on

Windows 11 (Direct3D 11 and OpenGL) and Mac: tried. Linux: never run.

**More:** [the long description](https://github.com/IshiakiZ/ksp-keystone/wiki/The-long-description) (everything in full, and what was
tried on each system) and the rest of the [wiki](https://github.com/IshiakiZ/ksp-keystone/wiki).

## Licence

[CC BY 4.0](LICENSE): use it, change it and pass it on as you like, as long as you credit Ishiaki and link back here.
