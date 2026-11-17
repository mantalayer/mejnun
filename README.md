# Mejnun

A rummy-style card game played with two decks for mobile (iOS/Android) and PC (Steam). Unity 6 + Wwise.

- Design & implementation plan: [`Docs/Mejnun-Plan.md`](Docs/Mejnun-Plan.md)

## Requirements
- Unity **6000.4.0f1**
- Wwise **2025.1.12** (only needed to edit audio; generated SoundBanks are committed)
- Git LFS (`git lfs install`)

## Repository layout
```
Assets/
  _Project/            all Mejnun-owned content
    Art/               Cards/Decks/<Skin>/, Table, UI, Fonts, VFX
    Audio/             Unity-side audio assets (Wwise events/banks live in Wwise)
    Data/              ScriptableObject data: Decks, Rules, AI
    Materials/ Shaders/ Prefabs/ Scenes/ UI/
    Scripts/           one folder per asmdef (Docs/Mejnun-Plan.md §3.1)
      Core/            Mejnun.Core: pure C# rules engine, no UnityEngine references (M1)
    Settings/          URP assets, Input/ (Input System actions)
    Tests/             EditMode/ (Core/ = Mejnun.Core.Tests), PlayMode/
  Wwise/               Wwise Unity integration (vendor, do not edit)
  StreamingAssets/     Wwise copies SoundBanks here at build time (ignored)
mejnun_WwiseProject/   Wwise authoring project (GeneratedSoundBanks committed)
Docs/                  design docs and plans
Packages/ ProjectSettings/
```
Third-party Asset Store packages go in `Assets/ThirdParty/`.

## Rules engine (`Mejnun.Core`)
Every mode (hot-seat, AI, online) drives the same engine through a single entry point:

```csharp
var game = new MejnunGame(new GameSetup(seatCount: 4, seed: 1234UL)); // no dealer given => ChoosingDealer
game.Apply(GameCommand.ChooseRandomDealer());                      // deals 11 each, assigns two deck faces
var seat = game.State.CurrentSeat;
var result = game.Apply(GameCommand.DrawOrPass(seat));              // CommandResult { Ok, Error }
```

- Illegal commands never throw. They return a `RuleError` and leave the state unchanged.
- Presentation, audio and networking subscribe to `game.Events` (`GameEvents`).
- `SnapshotSerializer.Write/Read` produce versioned binary snapshots (including the in-progress turn and RNG state). Replaying the same seed with the same commands gives identical bytes.

## Running the tests
Use the Test Runner window (EditMode ▸ `Mejnun.Core.Tests`), or run headless with the project closed in the editor:
```sh
/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . \
  -runTests -testPlatform EditMode -assemblyNames Mejnun.Core.Tests -testResults Library/results.xml \
  -enableCodeCoverage -coverageResultsPath Library/Coverage \
  -coverageOptions "generateHtmlReport;assemblyFilters:+Mejnun.Core"
```
The coverage report is written to `Library/Coverage/Report/`. The M1 target is ≥ 95% line coverage for `Mejnun.Core`.

## Git setup (one-time, per clone)
```sh
git lfs install
# Smart merge for scenes/prefabs (adjust the Unity path to your install):
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/Tools/UnityYAMLMerge' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```
