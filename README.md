# Melee Ruleset

The rules package of the melee game from the design documents: each player owns one hero, an identical Nexus, and a set of buildings that raise an autonomous army from their own charges. It authors rules, not a board.

## Referenced by the board

The `ugc-SecludedCrest` repository is the composing map: its `references.json` names this map, and every save of it takes this map's files into its own folder, so the board becomes one ordinary playable map. Nothing has to be composed here, and this folder is never the map that is played.

| Into | What this map contributes |
| :--- | :--- |
| Secluded Crest v1.0.0 | `MapScript.cs`, the rules script itself, and the melee templates this map's `metadata.json` declares - units, buildings, abilities, relics and the two harvestable resource node types - plus the factions, deck and economy the script runs. |

## The board supplies the placement

This map declares no `terrain.json` and no coordinates of its own. Where the twelve resource nodes stand and where each seat begins are the board's statements, so any board authored for Melee plays by these rules unchanged:

* The four starting points are `SpawnAnchors` in the board's `terrain.json`, one per player slot. The script reads slot *n*'s point with `IGameAPI.GetSpawnAnchor(n)`, places that player's Nexus on it, and puts the hero beside it. A board with no anchor for a slot seats nobody there and says so in that player's feedback rather than inventing a position.
* The twelve resource nodes are props the board places with the template ids this map declares (`resource/melee_gold_mine` and `resource/melee_woodland`). The engine attaches the node's capacity from the template when the prop loads, so the script spawns nothing and only reads the layout, through `IGameAPI.GetResourceNodes`.
* A board that declares no anchors or places no nodes is not a Melee board. The script warns the table once at the start of a match instead of seating players somewhere arbitrary.

`metadata.json` here is merged into the board's: objects merge key by key, arrays are combined with each entry kept once by its `TemplateID`, and the board merges last, so the board's own identity, build number, shroud and camera stand while the rules, units, abilities and relics arrive from here.

Every other file comes whole from exactly one map. `MapScript.cs` is the one file the board's `references.json` states comes from this map, because the ruleset is defined here; `MapScript.csproj`, the generated templates, `Directory.Build.targets`, `NuGet.config`, `AGENTS.md`, `LICENSE.md` and `.gitignore` are kept by the board, which builds and maintains its own copies. `MapTemplates.g.cs` and `WasmEntryPoint.g.cs` are never taken from a referenced map at all.

### Prerequisites

* The board reads this map from `FolderPath` in its `references.json` while that folder exists, and otherwise from the map store by name, version and this map's pinned manifest hash.
* `manifest.json` here is the package manifest that pin names. Regenerate it with `MapManifest.CreateFromDirectory` whenever this map's content changes, then repin the board's reference to the new `ComputeManifestBlake3()`.
* `lib/` and `bin/` are this map's own workspace. The board compiles its script in its own folder from the files it takes, so the `.wasm` the game runs is never the one built here.

## What the script implements

* **One hero per player.** The lobby's faction dropdown selects Vale, Sable or Corvus, and the hero spawns beside an identical Nexus at the board's anchor for that slot. Every player starts with 200 gold and 100 wood.
* **Army level.** Every living soldier is set to the hero's level, which is the only source of army stat growth.
* **Formations and targeting.** Each soldier type holds its authored slot around the hero, rotating with the hero's facing - Front ahead, Flank to either side, Back behind - and fights by its authored rule: Standard concentrates on the target the hero last hit, Tank-Shredder takes the healthiest enemy in range, and Assassin the weakest. Slot and rule are stated in the soldier's own readings.
* **Charges and automatic production.** Each soldier-granting building holds a charge count and recharges on its own timer, spends a charge when its soldier type has an unreserved capacity slot, and produces the soldier at the building. A finished building starts with one charge. The count is published as the building's overhead badge, including the zero it reads when the last charge has been spent, and the building's readings - the soldier it unlocks, its charges against its maximum, the seconds until its next charge, the income it produces and the soldiers it maintains against its capacity - are published to the inspector through `IGameAPI.SetUnitReadoutRow`, so an opponent that selects the building reads the same numbers the owner does.
* **Hero death.** Losing the hero destroys the army, clears production, and schedules a revival at the Nexus, with the timer growing by three seconds per earlier death. Revival refills every building's charges.
* **Economy.** Every finished gathering building carries an invulnerable worker that walks from the building to the nearest matching node and back, taking the building's advertised swing out of the node and paying it into the player's stockpile on delivery. A node beside the building therefore pays more than one across the board without the script computing a rate. The hero can also gather by hand from the two node types the board places.
* **Relics.** Six relics are sold at the Nexus for 100 gold and applied player-wide while held: front-formation health, recharge rate, damage reflection, income, tenacity and charge recharge.
* **Innates.** Vale's army carries flat armor and tenacity, Sable's carries Cinder Mark, and Corvus's buildings hold one extra charge.
* **Bulwark.** Heroes gain armor and magic resistance from their own nearby soldiers, and the Nexus from the player's functional buildings.
* **Elimination.** Destroying a Nexus eliminates its player; the last team holding a Nexus wins.
* **Under-attack alerts** are throttled per player, name the victim, ping the minimap and play a warning.
* **Instant abilities.** Requisition advances the active construction and Emergency Levy adds a charge to every soldier-granting building.
* **The shared building deck.** Begin Construction costs 100 wood and draws up to three buildings from one deck shared by every player. The draw excludes what another player already took and what the player already stands on, and a deck with nothing left is restored before it is drawn again. The three packages are offered as commands in the hero's and Nexus's command grid; the chosen one leaves the deck, the others return, and a player who abandons the draw gets the 100 wood back. The site then builds on its own for the building's authored construction time and starts its charges when the engine announces the completion.

## Rules the engine cannot express yet

* **The silhouette and its 99% gate.** The design draws the options only after an unidentified site already stands there, and holds that site at 99% until the choice is locked. The engine cannot swap a unit's template, so the draw is answered from the command grid first and the chosen building's site appears the moment a package is cast; construction time therefore runs after the decision instead of doubling as the decision window. The reservation is published on the hero that holds the draft open, which is a unit every player can select, rather than on a site, because no site exists until the choice is locked.
* **Income tax.** The design scales building income down marginally as a player's functional building count rises. The engine pays a worker's swing in full, so this script pays the full advertised amount; the marginal curve is left for the balance pass that also fixes the exact curve the design defers.
* **Repair and the destroyed state.** The engine destroys a building whose health reaches zero and offers no way to restore a damaged one, so a destroyed building is gone for the rest of the match instead of staying dormant until repaired. Restoring a building needs a repair action on the hero and a dormant building state in the engine; neither exists yet, and this script does not invent them.
