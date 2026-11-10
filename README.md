# Melee Ruleset

A map package that implements the melee ruleset from the design documents: each player owns one hero, an identical Nexus, and a set of buildings that raise an autonomous army from their own charges. Terrain is not authored here; the package is composed over another map's terrain.

## Referenced maps

`references.json` names the maps this one takes content from. It is not a package format: every save writes the referenced content into this folder, so this stays an ordinary editable map and testing, exporting, publishing and playing all read one folder.

| Referenced map | What it contributes |
| :--- | :--- |
| Secluded Crest v1.0.0 (`cc2b97b2…`) | Everything this map does not carry itself: `terrain.json`, its height field, the terrain images, the props, and the `.rmesh` models and environment settings its `metadata.json` declares. |

`metadata.json` is always merged: objects merge key by key, arrays are combined with each entry kept once by its `TemplateID`, and a value both maps declare carries this map's own, because this map merges last. That keeps the ruleset's identity, its `GameBuildNumber` and its full-information shroud, while the terrain map's models, environment, skyboxes and props arrive beside them.

Every other file comes whole from exactly one map. A file only one map carries is that map's, and a file both carry is kept from this map unless `references.json` states a priority for it. The nine files both maps carry here are this map's own build and identity - `MapScript.cs`, `MapScript.csproj`, the generated templates, `Directory.Build.targets`, `NuGet.config`, `AGENTS.md`, `LICENSE.md`, `.gitignore` - so none of them needs a priority.

### Prerequisites

* On a save, a referenced map is read from the helper folder `references.json` names while that folder exists, and otherwise from the map store by name, version and pinned manifest hash. Its `FolderPath` here points at the sibling `ugc-SecludedCrest` checkout.
* Files taken from a referenced map are made read-only in this folder. Editing one is reverted by the next save, which reports every file it replaced.
* `metadata.partial.json` holds the keys this map overrides in the merge. It is regenerated on every save by diffing this map's metadata against the merge of the referenced maps, so upgrading Secluded Crest to a new version and re-saving drops the old version's data instead of carrying it forward.

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
