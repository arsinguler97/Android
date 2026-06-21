# AR Animal Scanner - Project Notes

## Current Flow

The app has three main scenes:

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/ScanScene.unity`
- `Assets/Scenes/AnimalScene.unity`

Main flow:

1. App opens in `MainMenu`.
2. User taps/clicks anywhere.
3. `TapToLoadScene` loads `ScanScene` directly with `SceneManager.LoadScene`.
4. `ScanScene` scans the AR camera feed for supported animals.
5. If a supported animal is detected at or above 75 percent confidence, `AnimalScanSceneController` stores the recognized animal and loads `AnimalScene`.
6. In `AnimalScene`, user taps a valid horizontal AR surface to place the recognized animal prefab.
7. Back button returns to `ScanScene`.

Important: MainMenu -> ScanScene intentionally bypasses `AppSceneLoader`. This keeps XR Simulation startup behavior closer to opening ScanScene directly in the Editor.

## Scene Responsibilities

### MainMenu

- Shows start/menu UI.
- `TapToLoadScene` should use direct `SceneManager.LoadScene(sceneName)`.
- Do not route this transition through `AppSceneLoader`.

### ScanScene

- Scans only.
- Should not place animals.
- Contains:
  - AR Session / XR Origin / AR Camera.
  - `AIAnimalRecognition` with `ARCameraClassifier`.
  - `Animal Scan Scene Controller`.
  - Scan UI Canvas.
  - `AITestImageBoard` for Editor/XR Simulation testing only.

Scan UI:

- Default status: `Scan an animal`.
- Animals List button opens supported animal list.
- When detection reaches threshold, detected animal is saved and `AnimalScene` loads.

### AnimalScene

- Places only the recognized animal.
- Does not need `AITestImageBoard`.
- Does not need ScanScene scan UI.
- Uses ScanScene AR/XR objects kept alive underneath while in Editor testing.
- User taps/clicks a horizontal AR surface to place the animal.
- Tapping the spawned animal plays its SFX if mapped.

## Supported Animals

User-facing animals:

- Fox
- Lion
- Elephant
- Bear
- Alligator

Current label mappings:

- Lion: `lion`
- Alligator: `alligator`, `American alligator`
- Bear: `bear`, `brown bear`, `American black bear`, `ice bear`, `sloth bear`
- Fox: `fox`, `red fox`, `kit fox`, `Arctic fox`, `grey fox`
- Elephant: `elephant`, `Indian elephant`, `African elephant`

## Key Scripts

- `Assets/Scripts/TapToLoadScene.cs`
  - Used by MainMenu.
  - Directly loads ScanScene with `SceneManager.LoadScene`.

- `Assets/Scripts/AppSceneLoader.cs`
  - Used for ScanScene <-> AnimalScene flow.
  - Keeps ScanScene loaded while AnimalScene is active.
  - Hides Scan UI, test image board, and classifier during AnimalScene.
  - Restores Scan UI and resets scan state when returning to ScanScene.

- `Assets/Scripts/AnimalScanSceneController.cs`
  - Watches classifier predictions.
  - Requires confidence >= `0.75`.
  - Saves recognized animal to runtime state.
  - Loads AnimalScene.
  - Has `ResetScanState()` for returning from AnimalScene.

- `Assets/Scripts/ARCameraClassifier.cs`
  - Runs model inference on AR camera frames.
  - In Editor can classify rendered camera view for XR Simulation testing.
  - Important fix: classification coroutine restarts on `OnEnable`, because `AIAnimalRecognition` is disabled during AnimalScene.

- `Assets/Scripts/RecognizedAnimalSpawner.cs`
  - Places the recognized prefab on valid horizontal surfaces only.
  - Uses per-animal prefab mappings, spawn offsets, rotation offsets, scale multipliers, and SFX clips.

## Editor Testing Notes

- XR Simulation right-click movement is only an Editor testing control.
- Phone builds do not use right-click movement.
- On phone, AR camera motion comes from the actual device camera and tracking.
- `AITestImageBoard` is for Editor testing only and should not be required on device.
- If ScanScene is opened directly, XR Simulation is usually more reliable.
- MainMenu -> ScanScene now uses direct scene loading to avoid AppSceneLoader interfering with XR Simulation startup.

## Device Build Notes

For iPhone testing:

- Use macOS.
- Install Unity with iOS Build Support.
- Install Xcode.
- Add scenes to Build Settings in this order:
  1. `MainMenu`
  2. `ScanScene`
  3. `AnimalScene`
- Switch platform to iOS in Unity Build Settings.
- Check Player Settings:
  - Camera usage description must be set.
  - ARKit support/settings must be enabled.
  - Bundle Identifier should be valid.
  - Signing team will be configured in Xcode if needed.
- Build from Unity to an Xcode project.
- Open generated Xcode project on Mac.
- Select connected iPhone as target.
- Configure signing/team.
- Build and run from Xcode.

Expected device behavior:

- ScanScene uses the real phone camera to classify animal images.
- AnimalScene uses the phone AR camera and detected real-world horizontal planes.
- User taps a real floor/table surface to place the recognized animal.
- The placed animal should stay in world space while the user walks around it.

## Common Issues

- If ScanScene returns from AnimalScene but no prediction/confidence appears, check that `ARCameraClassifier` restarts on enable.
- If ScanScene is stuck on the previous detected animal, call `AnimalScanSceneController.ResetScanState()` when restoring ScanScene.
- If all animals appear at startup, keep `AR_Objects` children inactive and spawn from prefabs.
- If a spawned model is invisible, check prefab root active state and renderer state.
- If a model floats/sinks, tune `positionOffset.y` in `RecognizedAnimalSpawner`.
- If a model faces wrong direction, tune `rotationOffset.y`.
- If a model is too large/small, tune `scaleMultiplier`.
- If animals spawn on walls, verify horizontal surface filtering is active.
