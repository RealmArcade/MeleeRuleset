# Melee Ruleset

A map package that implements the melee ruleset from the design documents: each player owns one hero, an identical Nexus, and a set of buildings that raise an autonomous army from their own charges. Terrain is not authored here; the package is composed over another map's terrain.

## Composition

`composite.composition.json` is the composition this map is exported with. It is a recipe, not the export-only descriptor, so this folder stays an ordinary editable map.

| Sub-archive | Rule | Why |
| :--- | :--- | :--- |
| Secluded Crest v1.0.0 (`cc2b97b2…`) | `Overwrite`, with `metadata.json` set to `Union` | The ruleset authors no terrain and no models, so the sub-archive's `terrain.json`, terrain images and `.rmesh` models become this package's. The two maps' `metadata.json` merge instead of one replacing the other, and the parent keeps its own name, version, build number and licence. |

Composition is flat and one layer deep: Secluded Crest is a primitive map, and nothing composes a composition.

### Prerequisites

* Secluded Crest v1.0.0 must be present in the local map store before the package can be planned or played, because sub-archives are resolved by name, version and pinned manifest hash rather than by folder path.
* The editor's **Composite Map** menu plans the composition, validates the union, writes `composite.manifest.json` for the export only, and exports the `.rmap`. The recipe is rewritten from whatever the dialog holds when an export succeeds.
* The exported package carries this map's own files and the descriptor. A client that plays it downloads the sub-archive and hard-links the union of both packages into the composite's folder before the match can start.

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

## Rules the engine cannot express yet

* **Deck drafting.** The three-option draw, its reservation, the 99% construction gate and deck competition need a generic "offer this player N choices and report the answer" API plus the client card that renders it. Until then a player builds any package from the hero's build options, and building supply is a fixed cap of five.
* **Formations and targeting rules.** Front, Flank and Back formation slots and the Standard, Tank-Shredder and Assassin targeting rules belong in the engine's autonomous-army behaviour, not in a map script; soldiers currently use the engine's default engagement.
* **Gathering workers and repair.** Passive income is a per-building rate rather than worker travel time, and building repair is the engine's existing behaviour rather than a hero action this script drives.
