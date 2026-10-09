# The film

`Keystone.Film` does to the game's finished picture what the film in a camera, and the way it is developed,
do: how bright the picture comes out, what happens to what is brighter than white, how strong its contrast
and its colours are, the glow round whatever is very bright, the darkening of creases, and (as for the
lens) darker corners and colour fringes. A mod fills in a `Grade` and asks for it every frame:

```csharp
Grade grade = Grade.None;
grade.exposure = 0.5f;          // stops: +1 is twice as bright
grade.curve = 2;                // 0 none (cut off at white, as the game does it), 1 gentle, 2 as film does it
grade.contrast = 0.1f;          // -1 to 1
grade.colour = 0.2f;            // -1 to 1
grade.glow = 0.8f;              // 0 none; glowFrom is how bright a thing must be (1 is white), glowWide 1 to 10
grade.nooks = 0.6f;             // creases, 0 to 1
Film.Ask("My mod", grade);      // every frame while it is wanted; stop asking and it goes
```

More than one mod may ask at once, one for a look and another for an exposure: exposures add up, contrast,
colour and warmth add up, tints multiply, and of everything else the stronger wins.

## What does it

The base has no way of its own yet. `Film.Other` is filled in by the base's companion for the TUFX mod
(`KeystoneTUFX.dll`, which the game loads only where TUFX is installed): Unity's own post-processing,
which TUFX brings, as a layer of settings of its own over whatever TUFX profile is in use. It touches only
what was asked for: a grade that asks for no glow leaves the profile's own glow alone. `Film.Best` is null
where nothing can do it, and `Film.WhyNot` says why in words for the player.

`IFilm.Wide` says whether the game is keeping more than white in its picture (TUFX's "hdr", which its own
profiles have). Without that there is nothing brighter than white to bring back: the film curve and the
exposure in stops are left out, and a rough brightness is done in their place.

The game draws the far scene and the planets with other cameras, and what they draw is cut off at white
before the film sees it: only what is near (within 400 metres in flight) keeps more than white.

## Where it works

Wherever TUFX does, which is every system the game runs on. It was made and tried on a Mac (the game's
OpenGL, where the better way of darkening creases is not to be had and the plainer one is used) and has
**not been run on Windows or Linux**.
