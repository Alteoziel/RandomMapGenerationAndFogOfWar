# Map Generator

Map Generator is a plugin for the Windows Steam version of Dungeon Alchemist. Press **F9** on an open map and it can build a whole map in one pass: buildings, player tokens, enemy tokens, and an editor fog of war. You can also turn fog on or off later without generating again, so a map you built by hand can use the same fog.

This release works with **Dungeon Alchemist 1.8.10** on Windows through Steam. That is the patch notes version the game reports. It was made for that build, which runs on Unity 2022.3.62f2.

It needs [BepInEx 5.4.23.5 for Windows x64](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip). The installer copy includes that loader and skips it when BepInEx is already in the game folder. Workshop Procgen can stay installed next to this mod.

## What it does

Open a map and press **F9**. The window can be dragged by its title bar. Escape closes it.

**Generate** builds the map from the settings in the window.

- **Buildings** are rooms from the themes you pick. Leave the list alone and a theme is chosen at random from the ones the game allows in a fresh map. Each building is one room, with floors, walls, doors, windows, and props. Map width, map height, building count, and building size are sliders. If the map is smaller than the size you asked for, it grows. **Remove existing rooms first** clears normal rooms before placing new ones. Leave that off to fill only open ground.
- **Player tokens** are placed at random, or only inside a rectangle you drag on the map. You can use every token, or only the ones you check.
- **Enemy tokens** are placed on the floors of the buildings. They do not clear fog.
- **Fog** is a dark cover drawn in the editor. It sits just over open ground and higher inside a building. Drag a player token near it and that area stays clear. Vision distance is how far a move reveals, and changing it only affects the next move. **Reset revealed fog** covers those cleared cells again. Fog is not written into the map file, and it does not appear in a virtual tabletop export.
- **Cover this map** and **Hide fog** change fog on the map you already have open. They do not move or delete what you built. Choose player tokens first if only those should clear fog. If none are chosen, any token does.

**Cover the map with fog when generating** only affects the next Generate. Use **Cover this map** when the map is already built.

If [Workshop Procgen](https://github.com/Alteoziel/Dungeon-Alchemist-Workshop-Procgen) is installed, **Replace matching assets with Workshop assets** uses a matching Workshop asset for props, floors, walls, doors, and windows during that run. Roofs stay as the game built them. Your F6 chances are put back when generation finishes.

The menu hotkey can be changed in `BepInEx/config/com.aedin.mapgenerator.cfg`.

## Install with the installer

1. Close Dungeon Alchemist.
2. Download [MapGenerator-0.1.6-Installer.zip](https://github.com/Alteoziel/RandomMapGenerationAndFogOfWar/releases/download/v0.1.6/MapGenerator-0.1.6-Installer.zip).
3. Open your Downloads folder. Right-click the zip, choose **Extract All**, then **Extract**.
4. Open the new folder and double-click **Install Map Generator**.
5. If Windows asks you to confirm, choose **Run**, then **Yes**.
6. When the installer shows your game folder, choose **Yes**.

Start Dungeon Alchemist, open a map, and press **F9**.

The installer copies files into the game folder. It does not change your maps. If BepInEx is already there, it leaves that loader in place and only adds this plugin. If it cannot find the game, it asks you to select the folder yourself. In Steam, open the game page, choose the gear icon, **Manage**, then **Browse local files**, and select that folder.

## Install by hand

Use this when you do not want the installer. Download [MapGenerator-0.1.6-Manual.zip](https://github.com/Alteoziel/RandomMapGenerationAndFogOfWar/releases/download/v0.1.6/MapGenerator-0.1.6-Manual.zip).

1. Close Dungeon Alchemist.
2. If the game folder does not already contain `BepInEx\core\BepInEx.dll` and `winhttp.dll`, install BepInEx first. Download [BepInEx 5.4.23.5 for Windows x64](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip), extract it, and copy its files into the folder that contains `Dungeon Alchemist.exe`. Start the game once, then close it. The `BepInEx` folder should now exist.
3. Extract the manual zip. Copy the `MapGenerator` folder from `BepInEx\plugins\MapGenerator` into `BepInEx\plugins` in the game folder. `MapGenerator.dll` should end up at `BepInEx\plugins\MapGenerator\MapGenerator.dll`.
4. Start Dungeon Alchemist, open a map, and press **F9**.

In Steam, the game folder is under the gear icon, **Manage**, then **Browse local files**.

## Remove

Close the game. Delete the folder `BepInEx\plugins\MapGenerator` inside the Dungeon Alchemist folder. The game then runs without this mod. Fog notes for old maps, if you want those gone too, are under `BepInEx\config\MapGenerator`. Deleting the plugin does not change your maps.

AI was utilized in the development of this mod.
