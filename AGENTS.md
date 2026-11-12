# Realm Custom Map Agents Guide
Realm is an RTS Game Engine with a custom map editor for UGC arcade maps.

## Skills
Separate skill files are located in the `.agents/skills` folder.
- `skill-assign-asset`: Automatically queries storage and assigns random matching assets to templates. **Always call `skill-assign-asset` (`AssignRandomAssetsToTemplates(false)` via IPC) after creating a new template in `metadata.json`** to populate missing asset references.
- `skill-translate-map`: Extracts, translates, verifies and prunes this map's own `locale/*.json` catalogs so template names and tooltips render in each player's language. Map text only — it never touches the Realm client's catalogs.

## Map Scripting & Editor API
- Implements `IMapScript`.
- `Initialize(IGameAPI api)` is called when the map starts.
- `Update(IGameAPI api, float delta)` is called every simulation tick (30Hz).
- Full `IGameAPI` reference: `lib/Realm.MapAPI.xml`
- Full `IEditorAPI` reference: `lib/Realm.EditorAPI.xml`

### Accessing IEditorAPI via HTTP IPC Bridge
VSCodium agent extensions can programmatically invoke editor operations by issuing HTTP `POST` JSON requests to `http://127.0.0.1:8092/api/` (or port `8093` if 8092 is occupied).

#### IPC JSON Request Schema:
```json
{
  "action": "invokeEditorApi",
  "method": "raiseTerrain",
  "requestId": "req-12345",
  "x": 0.0,
  "y": 0.0,
  "z": 0.0,
  "radius": 5.0,
  "strength": 1.0,
  "isSquare": false
}
```

#### IPC JSON Response Schema:
```json
{
  "action": "invokeEditorApiResult",
  "method": "raiseTerrain",
  "requestId": "req-12345",
  "success": true,
  "error": ""
}
```

## JSON Map Files

### metadata.json
The primary map configuration file. Schema at `.vscode/metadata.schema.json` 

### manifest.json
Tracks map asset registrations Schema at `.vscode/manifest.schema.json`.

### terrain.json
Defines the terrain layout: prop/unit/decal placements, map dimensions (`Width`, `Depth`), camera bounds, `Coordinates` (named map regions), `SpawnAnchors` (the point each player slot starts at), and skybox path. Schema at `.vscode/terrain.schema.json`.

A ruleset holds no terrain of its own: the board that composes it places its props, its resource nodes and its spawn anchors, and the script reads them through `IGameAPI.GetSpawnAnchor` and `IGameAPI.GetResourceNodes`.

## Localization

A map is read in the language each player picks in Settings → Game → Language. The map folder carries its own catalogs:

- **`locale/{code}.json`** — flat `{"English text": "translated text"}`, one file per supported language (`en`, `es`, `fr`, `de`, `pt`, `ru`, `zh`, `ja`, `ar`, `hi`). The client merges the map's catalog over the game's own when the map loads, so every player reads the same map in their own language and nothing has to run in WASM. A missing entry reads as the English text, and English itself needs no file.
- **Keys are the text `metadata.json` already holds**, so a translation needs no key naming scheme:
  - `Name` fields are keyed by the plain name, without its BBCode: `"[color=#FF0000]Seismic Rupture[/color]"` is translated by an entry keyed `"Seismic Rupture"`, and the colour markup stays as written.
  - `Description` fields and tooltips are keyed by the whole string, `{{...}}` tokens included. A translation may move the tokens and keep its own BBCode; the client fills the tokens in afterwards, so `{{Ability.Effect0.Amount}}` and `[color]`/`[b]` survive the lookup.
  - An ability tooltip that opens with a role tag, `[Damage] Deals ...`, can be translated as two entries (`"Damage"` and the sentence) instead of one.
- **Anything the script shows one player** — feedback lines, alerts, a refusal — is translated on the authority with `api.Translate(key, playerIndex)` where it is shown, so one host serves players in different languages and the client never sees the English. `MapScript` keeps that to one call per message through its `Say` helper, and a line that states a number passes a `{0}` placeholder so the translation can move it. `api.GetPlayerLanguage(playerIndex)` reports what each player asked for.
- **Text the script publishes that every player reads at once** — an ability's name and tooltip, a page in the instructions menu, a buff's name and description — is keyed by the whole English string and translated by the client against the same catalog. A tooltip that states numbers is published as a template whose values the card fills in, `{{Ability.Key}}` published with `SetAbilityTooltipValue`, so the sentence stays one translatable key.
- **`verify_keys.py` reads the script too**, so `list`, `verify` and `prune` cover what the map publishes and not only `metadata.json`: `api.Translate`/`Say` keys, `AddInstructionsSection` sections, `RegisterAbility` names and tooltips, `Set*Tooltip` text, a `string[]` named `*Tooltips`, a `const string` whose name says tooltip, and the phrases in a `new(...)` record table.

## Terrain Binary Files

### `terrain_heights.exr`
- **Format:** 32-bit single-channel (R32F) OpenEXR.
- **Size:** `(MapWidth + 1) × (MapDepth + 1)` pixels — one sample per terrain vertex.
- **Encoding:** The red channel float is the world-space Y height of that vertex. `0.0` = sea level. Origin (pixel 0,0) = world corner `(minX, minZ)`. Pixels scan left-to-right, top-to-bottom in terrain grid space.

### `terrain_water.exr`
- **Format:** 32-bit single-channel (R32F) OpenEXR.
- **Size:** Same as `terrain_heights.exr`.
- **Encoding:** Red channel = water surface height at that vertex. Values `≤ 0` indicate no water. The engine uses this to render the water plane and determine shallow/deep zone thresholds.

### `terrain_splat_indices.exr`
- **Format:** 4-channel (RGBA) 16-bit half-float OpenEXR.
- **Size:** `MapWidth × MapDepth` pixels — one pixel per terrain cell.
- **Encoding:** Each channel (R, G, B, A) holds the **integer index** (stored as a float) of one of the four ground texture layers blended at that cell. Index values refer to the ordered texture list in `metadata.json` → `textures`.

### `terrain_splat_weights.exr`
- **Format:** 4-channel (RGBA) 16-bit half-float OpenEXR.
- **Size:** Same as `terrain_splat_indices.exr`.
- **Encoding:** Each channel holds the blend weight `[0.0 – 1.0]` for the corresponding texture index in `terrain_splat_indices.exr`. Channel R weight = weight for the texture at `splat_indices.R`. Weights sum to ~1.0 per pixel.

### `terrain_cliff_splat_indices.exr`
- **Format / Size / Encoding:** Identical schema to `terrain_splat_indices.exr`, but applies to **cliff face geometry** rather than the flat terrain surface.

### `terrain_cliff_splat_weights.exr`
- **Format / Size / Encoding:** Identical schema to `terrain_splat_weights.exr`, applied to cliff face geometry.

### `terrain_pathing.png`
- **Format:** 8-bit single-channel (grayscale) PNG.
- **Size:** `MapWidth × MapDepth` pixels — one pixel per terrain cell.
- **Encoding:** Each pixel is a bitmask of allowed movement types:
  - `1` = Shallow water passable
  - `2` = Deep water passable
  - `4` = Flying units passable
  - `8` = Ground units passable
  - `32` = Buildable (structures can be placed)
  - `255` = All movement types allowed
  - `0` = Impassable