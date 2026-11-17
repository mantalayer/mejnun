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
    Scripts/           Runtime/, Editor/ (asmdef split per Docs/Mejnun-Plan.md §3.1)
    Settings/          URP assets, Input/ (Input System actions)
    Tests/             EditMode/, PlayMode/
  Wwise/               Wwise Unity integration (vendor, do not edit)
  StreamingAssets/     Wwise copies SoundBanks here at build time (ignored)
mejnun_WwiseProject/   Wwise authoring project (GeneratedSoundBanks committed)
Docs/                  design docs and plans
Packages/ ProjectSettings/
```
Third-party Asset Store packages go in `Assets/ThirdParty/`.

## Git setup (one-time, per clone)
```sh
git lfs install
# Smart merge for scenes/prefabs (adjust the Unity path to your install):
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'/Applications/Unity/Hub/Editor/6000.4.0f1/Unity.app/Contents/Tools/UnityYAMLMerge' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```
