# Bal Hanuman - The Journey to the Forgotten Temple

A colourful 2D side-scrolling platformer for Android (landscape). You play young Bal Hanuman.

**Level 1: The Missing Fruit Offering.** A peaceful exploration level (no combat): help the sage find the missing mango basket, collect mangoes, cross a stream, climb vines, rescue a baby monkey, lower an ancient bridge, and return the basket.

## Requirements
- Unity **6000.6.5f1** (see `ProjectSettings/ProjectVersion.txt`)
- Git LFS (`git lfs install`) - images and audio are stored with LFS

## Open and play
1. Clone the repo (with Git LFS installed), then open the folder in Unity Hub.
2. Open `Assets/Scenes/MainMenu.unity` and press Play, or open `Assets/Scenes/Level01_MissingFruitOffering.unity` to start the level directly.

## Controls
| Action | Keyboard | Touch |
|---|---|---|
| Move | A / D or arrow keys | left / right buttons |
| Jump | Space, W or Up arrow | up button (right side) |
| Roll | Shift or Z | dash button |
| Bend (crouch) | S or Down arrow | down button (left side) |
| Climb vine | W / S (or Up / Down) near a vine | up button / down button |
| Interact | E | star button |
| Pause | Esc | pause button |

## Rebuilding the level scenes
The scenes, prefabs and animations are generated from the art by an editor script.
In Unity use the menu **Bal Hanuman > Build Level 1 (HQ)**. This regenerates `Level01_MissingFruitOffering` and `MainMenu`, so make manual scene edits only if you do not plan to rebuild.

## Project layout
- `Assets/Scripts` - gameplay code (`Player`, `Camera`, `Interactions`, `Collectibles`, `Quests`, `UI`, `Level`, `Core`)
- `Assets/Editor` - builders and importers
- `Assets/ArtHQ` - sprites (character, environment, items, VFX, ground set)
- `Assets/Prefabs`, `Assets/Animations`, `Assets/Scenes`
- `Assets/Scripts/Dev/PlaytestBot.cs` - automated play-test used during development

## Notes
- No audio files yet: sound effects are synthesised at runtime (`Assets/Scripts/Core/Sfx.cs`).
- Placeholders: signpost and floating log sprites were drawn as stand-ins.
