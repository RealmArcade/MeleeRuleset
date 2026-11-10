# Melee Ruleset

The rules package of the melee game from the design documents: each player owns one hero, an identical Nexus, and a set of buildings that raise an autonomous army from their own charges. It authors rules, not a board.

## Referenced by the board

The `ugc-SecludedCrest` repository is the composing map: its `references.json` names this map, and every save of it takes this map's files into its own folder, so the board becomes one ordinary playable map. Nothing has to be composed here, and this folder is never the map that is played.

| Into | What this map contributes |
| :--- | :--- |
| Secluded Crest v1.0.0 | `MapScript.cs`, the rules script itself, and the melee templates this map's `metadata.json` declares - units, buildings, abilities and relics - plus the factions, deck and economy the script runs. |

`metadata.json` here is merged into the board's: objects merge key by key, arrays are combined with each entry kept once by its `TemplateID`, and the board merges last, so the board's own identity, build number, shroud and camera stand while the rules, units, abilities and relics arrive from here.

Every other file comes whole from exactly one map. `MapScript.cs` is the one file the board's `references.json` states comes from this map, because the ruleset is defined here; `MapScript.csproj`, the generated templates, `Directory.Build.targets`, `NuGet.config`, `AGENTS.md`, `LICENSE.md` and `.gitignore` are kept by the board, which builds and maintains its own copies. `MapTemplates.g.cs` and `WasmEntryPoint.g.cs` are never taken from a referenced map at all.

### Prerequisites

* The board reads this map from `FolderPath` in its `references.json` while that folder exists, and otherwise from the map store by name, version and this map's pinned manifest hash.
* `manifest.json` here is the package manifest that pin names. Regenerate it with `MapManifest.CreateFromDirectory` whenever this map's content changes, then repin the board's reference to the new `ComputeManifestBlake3()`.
* `lib/` and `bin/` are this map's own workspace. The board compiles its script in its own folder from the files it takes, so the `.wasm` the game runs is never the one built here.

## What the script implements

* **One hero per player.** The lobby's faction dropdown selects Vale, Sable or Corvus, and the hero spawns beside an identical Nexus. Every player starts with 200 gold and 100 wood.
* **Army level.** Every living soldier is set to the hero's level, which is the only source of army stat growth.
* **Charges and automatic production.** Each soldier-granting building holds a charge count and recharges on its own timer, spends a charge when its soldier type has an unreserved capacity slot, and produces the soldier at the building. A finished building starts with one charge. The count is published as the building's overhead badge.
* **Hero death.** Losing the hero destroys the army, clears production, and schedules a revival at the Nexus, with the timer growing by three seconds per earlier death. Revival refills every building's charges.
* **Economy.** Buildings generate gold and wood per minute with a progressive income tax that is marginal per functional building. The hero can gather by hand from the two node types.
* **Relics.** Six relics are sold at the Nexus for 100 gold and applied player-wide while held: front-formation health, recharge rate, damage reflection, income, tenacity and charge recharge.
* **Innates.** Vale's army carries flat armor and tenacity, Sable's carries Cinder Mark, and Corvus's buildings hold one extra charge.
* **Bulwark.** Heroes gain armor and magic resistance from their own nearby soldiers, and the Nexus from the player's functional buildings.
* **Elimination.** Destroying a Nexus eliminates its player; the last team holding a Nexus wins.
* **Under-attack alerts** are throttled per player, name the victim, ping the minimap and play a warning.
* **Instant abilities.** Requisition advances the active construction and Emergency Levy adds a charge to every soldier-granting building.
* **The shared building deck.** Begin Construction costs 100 wood and draws up to three buildings from one deck shared by every player. The draw excludes what another player already took and what the player already stands on, and a deck with nothing left is restored before it is drawn again. The three packages are offered as commands in the hero's and Nexus's command grid; the chosen one leaves the deck, the others return, and a player who abandons the draw gets the 100 wood back. The site then builds on its own for the building's authored construction time and starts its charges when the engine announces the completion.

## Rules the engine cannot express yet

* **The silhouette and its 99% gate.** The design draws the options only after an unidentified site already stands there, and holds that site at 99% until the choice is locked. The engine cannot swap a unit's template, so the draw is answered from the command grid first and the chosen building's site appears the moment a package is cast; construction time therefore runs after the decision instead of doubling as the decision window. The reserved options are also not shown on a site to an opponent who inspects it.
* **Formations and targeting rules.** Front, Flank and Back formation slots and the Standard, Tank-Shredder and Assassin targeting rules belong in the engine's autonomous-army behaviour, not in a map script; soldiers currently use the engine's default engagement.
* **Gathering workers and repair.** Passive income is a per-building rate rather than worker travel time, and building repair is the engine's existing behaviour rather than a hero action this script drives.
