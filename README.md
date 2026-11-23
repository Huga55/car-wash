# Little Car Wash

A gentle, colourful car-washing game for 5–7 year olds. Works on **Android tablets** (finger) and **Windows** (mouse or touch screen).

The child washes a dirty car in three easy steps:

1. **Make it wet** — swipe a finger/mouse over the car.
2. **Add foam** — swipe again to cover it in bubbles.
3. **Rinse it off** — swipe to reveal a sparkling, shiny car.

There is no timer pressure and no way to fail: once about 60% of the car is swiped, the game finishes the last few spots for the child, cheers, and moves on.

## Features

- **30 levels** with small cars, big trucks, buses, a fire truck, tractor, ice-cream van, limo, monster truck and more.
- Every car is **drawn procedurally** from simple 2D shapes — no 3D models, textures, fonts, shaders or audio files in the repository.
- Bright backgrounds, stars, sparkles, progress bar and generated sound effects.
- Big friendly buttons, huge text, one-step instructions and an animated finger hint.
- Stars are saved per level in `PlayerPrefs`; all levels are unlocked from the start.
- Designed for **weak Android tablets**: a single 256×256 painting texture, one upload per stroke, unlit UI, no real-time lights or physics.

## Playing locally

1. Install **Unity 2022.3.61f1** (LTS) with Unity Hub. For Android, install the Android Build Support module (SDK + NDK) in the Hub.
2. Open this folder as a project in Unity Hub.
3. Press **Play** in the editor (mouse drag = finger swipe).

`Assets/Editor/ProjectSetup.cs` applies the player settings on open: product name, Android package id, landscape orientation, 1280×800 desktop window, IL2CPP with ARM64 + ARMv7 and minimum Android API 22.

## Automatic GitHub builds

`.github/workflows/build.yml` builds **Android** and **StandaloneWindows64** on every `push` and can also be started manually from the Actions tab. Artifacts are uploaded as `LittleCarWash-Android` and `LittleCarWash-StandaloneWindows64`.

Unity requires an activated licence in CI. The first build fails with **`Missing Unity License File and no Serial was found`** until you add one of these secret sets (**Settings → Secrets and variables → Actions → New repository secret**).

### Free Unity Personal licence (most people)

1. Install **Unity Hub** (free) and sign in with your Unity account. Hub is only needed to create the licence file — you still do not need Unity/Hub to *play* the built game.
2. In Hub open **Preferences → Licenses → Add → Get a free personal license** and complete activation.
3. Copy the licence file from:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
4. Open `Unity_lic.ulf` in Notepad, select all, and paste the **whole XML contents** into the `UNITY_LICENSE` secret. Add your Unity account as `UNITY_EMAIL` and `UNITY_PASSWORD` too.
5. **Sign out of Unity Hub** after copying the file. On recent Hub versions signing out returns the Personal seat so the CI build can use it.
6. Re-run the workflow (**Actions → Build… → Re-run all jobs**).

If GitHub has trouble storing the multi-line XML, use base64 instead — in PowerShell:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\ProgramData\Unity\Unity_lic.ulf")) | Set-Clipboard
```

### Unity Plus / Pro licence

Add `UNITY_SERIAL` (your serial, e.g. `XX-XXXX-XXXX-XXXX-XXXX-XXXX`), `UNITY_EMAIL` and `UNITY_PASSWORD`.

The workflow uses [`game-ci/unity-builder`](https://github.com/game-ci/unity-builder). A log line like **`Warning: Library folder does not exist`** is normal on the first run — Unity creates `Library/` during the build, and the cache step reuses it on later runs.

Artifacts:

- **Android:** `LittleCarWash.apk` — debug-signed, install with `adb install -r LittleCarWash.apk`. For Play Store publishing, add a keystore in Unity Player Settings / CI secrets.
- **Windows:** `LittleCarWash.exe` plus its `_Data` folder — keep them together.

## Project layout

```
Assets/
  Scenes/Game.unity          Minimal scene with the Bootstrap component
  Scripts/
    Bootstrap.cs             Camera, EventSystem, audio and controller setup
    GameController.cs        Screens, input, stage flow, stars
    GameData.cs              30 level definitions (car kinds, colours, sizes)
    CarArt.cs                Procedural 2D car drawing (17 car families)
    WashSurface.cs           Wet / foam / clean masks and compositing
    GameArt.cs               Background themes
    UiFactory.cs             Runtime UI widgets (rounded panels, stars, text)
    AudioHelper.cs           Synthesised sound effects
  Editor/ProjectSetup.cs     Player settings + build scene registration
.github/workflows/build.yml  Android + Windows CI
```

## Adding content

- **New level:** add a name in `GameData.Names` and tweak the arrays in `GameData.cs`. `LevelCount` controls how many levels exist.
- **New car shape:** add an enum value in `CarKind`, add a drawing method in `CarArt.Painter`, and hook it into `CarArt.Render`'s switch.
- **New background:** add a palette to `GameArt` and increase the sprite cache size in `GameController`.

Everything visible is generated at runtime, so adding a car costs a few dozen lines of shape drawing and zero downloaded assets.
