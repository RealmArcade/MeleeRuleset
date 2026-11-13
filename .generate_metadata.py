import json

BUILDINGS = [
    ("building/nexus", "Nexus", 6000, 15, "fairytale_castle.rmesh", 2.2, 2.2, 1.8, []),
    ("building/bulwark_hall", "Bulwark Hall", 2000, 10, "fairytale_castle.rmesh", 1.8, 1.8015, 1.4, []),
    ("building/warband_yard", "Warband Yard", 1500, 7, "timber_framed_house.rmesh", 1.5, 1.5001, 1.25, []),
    ("building/spear_lodge", "Spear Lodge", 1600, 8, "watch_tower_building.rmesh", 1.5, 1.5005, 1.25, []),
    ("building/archery_range", "Archery Range", 1400, 6, "fantasy_tower_building_4.rmesh", 1.5, 1.4952, 1.25, []),
    ("building/bolt_foundry", "Bolt Foundry", 1800, 9, "dark_tower_keep.rmesh", 1.5, 1.5023, 1.25, []),
    ("building/sling_camp", "Sling Camp", 1200, 5, "scroll_and_scroll_holder.rmesh", 1.5, 1.5106, 1.25, []),
    ("building/mender_shrine", "Mender Shrine", 1500, 7, "dark_tower_keep_1.rmesh", 1.5, 1.5024, 1.25, []),
    ("building/signal_post", "Signal Post", 1300, 6, "watch_tower_building.rmesh", 1.35, 1.3505, 1.0, []),
    ("building/vigil_bastion", "Vigil Bastion", 1600, 8, "ice_castle_1.rmesh", 1.5, 1.5001, 1.25, []),
]

CONSTRUCTION_TIMES = {
    "building/bulwark_hall": 30,
    "building/warband_yard": 30,
    "building/spear_lodge": 30,
    "building/archery_range": 30,
    "building/bolt_foundry": 30,
    "building/sling_camp": 30,
    "building/mender_shrine": 35,
    "building/signal_post": 35,
    "building/vigil_bastion": 35,
}

# id, name, hp, armor, damage, interval, range, speed, gold bounty, xp bounty, model, abilities
SOLDIERS = [
    ("unit/shieldbearer", "Shieldbearer", 420, 7, 14, 1.4, 1.4, 4.2, 8, 6, "fantasy_warrior_unit_1.rmesh", []),
    ("unit/axeman", "Axeman", 300, 3, 26, 1.6, 1.6, 4.6, 10, 8, "orc_warrior_7.rmesh", []),
    ("unit/spear_militia", "Spear Militia", 330, 4, 20, 1.5, 2.4, 4.3, 9, 7, "fantasy_warrior_unit_1.rmesh", []),
    ("unit/bowman", "Bowman", 190, 1, 17, 1.3, 9.0, 4.8, 7, 5, "fantasy_warrior_unit_1.rmesh", []),
    ("unit/slinger", "Slinger", 160, 0, 12, 1.1, 7.0, 5.2, 5, 4, "fantasy_warrior_unit_1.rmesh", []),
    ("unit/bolt_thrower", "Bolt Thrower", 260, 2, 46, 3.2, 12.0, 2.8, 16, 12, "fantasy_warrior_unit_1.rmesh", []),
    ("unit/mender", "Mender", 200, 1, 8, 1.8, 8.0, 4.8, 6, 5, "fantasy_warrior_unit_1.rmesh", ["ability/mend"]),
    ("unit/standard_bearer", "Standard Bearer", 240, 3, 10, 1.8, 1.5, 4.9, 6, 5, "orc_warrior_7.rmesh", ["ability/standard_aura"]),
    ("unit/warder", "Warder", 380, 5, 12, 1.7, 1.6, 4.4, 8, 6, "fantasy_warrior_unit_1.rmesh", ["ability/warding_aegis"]),
]

HEROES = [
    ("hero/vale", "Vale, the Lineholder", 900, 8, 30, 1.25, 1.6, 5.2, 240, 2.2, 18, 10, 14, 12, 8, 4,
     "fantasy_warrior_unit_1.rmesh", 1.1,
     ["ability/grounding_shout", "ability/hold_fast", "ability/shield_bash", "ability/begin_construction"]),
    ("hero/sable", "Sable, the Ashcaller", 700, 3, 20, 1.5, 8.5, 5.0, 360, 4.0, 8, 12, 10, 26, 10, 8,
     "fantasy_warrior_unit_1.rmesh", 1.0,
     ["ability/ember_lance", "ability/cinderfield", "ability/backdraft", "ability/begin_construction"]),
    ("hero/corvus", "Corvus, the Quartermaster", 800, 5, 24, 1.3, 9.0, 5.0, 300, 3.2, 10, 12, 12, 20, 12, 14,
     "orc_warrior_7.rmesh", 1.05,
     ["ability/requisition", "ability/forced_march", "ability/emergency_levy", "ability/begin_construction"]),
]

HERO_DESCRIPTIONS = {
    "hero/vale": "Durable frontline commander. Innate Unbroken: Vale and every owned unit carry +6 armor and +10% tenacity, always active.",
    "hero/sable": "Ranged area-damage spellcaster. Innate Cinder Mark: Sable's abilities and every owned unit's attacks burn for 6 magic damage a second for 2 seconds.",
    "hero/corvus": "Economy and tempo strategist. Innate Standing Ledger: every owned soldier-granting building holds one more charge, always active.",
}

ABILITIES = [
    {
        "TemplateID": "ability/grounding_shout", "Name": "Grounding Shout", "AbilityType": "target_spell",
        "Description": "60 magic damage in radius 4 and a 25% movement slow for 2 seconds.",
        "ManaCost": 60, "Cooldown": 14, "TargetRange": 7, "AreaOfEffectRadius": 4, "GridX": 0, "GridY": 0, "Hotkey": "Q",
        "Effects": [
            {"Kind": "damage", "Delivery": "radius", "Targets": "enemies", "Radius": 4, "Amount": 60, "DamageType": "magic"},
            {"Kind": "debuff", "Delivery": "radius", "Targets": "enemies", "Radius": 4, "Duration": 2, "BuffId": "buff/grounding_shout"},
        ],
    },
    {
        "TemplateID": "ability/hold_fast", "Name": "Hold Fast", "AbilityType": "target_spell",
        "Description": "Allies in radius 6 gain +4 armor for 3 seconds.",
        "ManaCost": 50, "Cooldown": 18, "TargetRange": 6, "AreaOfEffectRadius": 6, "GridX": 1, "GridY": 0, "Hotkey": "W",
        "Effects": [
            {"Kind": "buff", "Delivery": "radius", "Targets": "allies", "Radius": 6, "Duration": 3, "BuffId": "buff/hold_fast"},
        ],
    },
    {
        "TemplateID": "ability/shield_bash", "Name": "Shield Bash", "AbilityType": "target_spell",
        "Description": "40 physical damage to a single enemy in reach.",
        "ManaCost": 60, "Cooldown": 16, "TargetRange": 4, "GridX": 2, "GridY": 0, "Hotkey": "E",
        "Effects": [
            {"Kind": "damage", "Delivery": "single", "Targets": "enemies", "Amount": 40, "DamageType": "physical"},
        ],
    },
    {
        "TemplateID": "ability/ember_lance", "Name": "Ember Lance", "AbilityType": "target_spell",
        "Description": "90 magic damage along a line of length 12 and 35 degrees, up to 5 targets.",
        "ManaCost": 80, "Cooldown": 10, "TargetRange": 12, "GridX": 0, "GridY": 0, "Hotkey": "Q",
        "Effects": [
            {"Kind": "damage", "Delivery": "line", "Targets": "enemies", "Length": 12, "AngleDegrees": 35, "MaxTargets": 5, "Amount": 90, "DamageType": "magic"},
        ],
    },
    {
        "TemplateID": "ability/cinderfield", "Name": "Cinderfield", "AbilityType": "target_spell",
        "Description": "100 magic damage to every enemy in radius 5.",
        "ManaCost": 100, "Cooldown": 20, "TargetRange": 11, "AreaOfEffectRadius": 5, "GridX": 1, "GridY": 0, "Hotkey": "W",
        "Effects": [
            {"Kind": "damage", "Delivery": "radius", "Targets": "enemies", "Radius": 5, "Amount": 100, "DamageType": "magic"},
        ],
    },
    {
        "TemplateID": "ability/backdraft", "Name": "Backdraft", "AbilityType": "target_spell",
        "Description": "70 magic damage and 3 units of knockback in radius 4.",
        "ManaCost": 80, "Cooldown": 22, "TargetRange": 4, "AreaOfEffectRadius": 4, "GridX": 2, "GridY": 0, "Hotkey": "E",
        "Effects": [
            {"Kind": "damage", "Delivery": "radius", "Targets": "enemies", "Radius": 4, "Amount": 70, "DamageType": "magic"},
            {"Kind": "knockback", "Delivery": "radius", "Targets": "enemies", "Radius": 4, "Amount": 3},
        ],
    },
    {
        "TemplateID": "ability/requisition", "Name": "Requisition", "AbilityType": "instant_spell",
        "Description": "Completes 30% of the hero's active construction's remaining time.",
        "ManaCost": 70, "Cooldown": 30, "TargetRange": 0, "GridX": 0, "GridY": 0, "Hotkey": "Q",
    },
    {
        "TemplateID": "ability/forced_march", "Name": "Forced March", "AbilityType": "target_spell",
        "Description": "Allies in radius 8 gain 20% movement speed for 5 seconds.",
        "ManaCost": 60, "Cooldown": 22, "TargetRange": 8, "AreaOfEffectRadius": 8, "GridX": 1, "GridY": 0, "Hotkey": "W",
        "Effects": [
            {"Kind": "buff", "Delivery": "radius", "Targets": "allies", "Radius": 8, "Duration": 5, "BuffId": "buff/forced_march"},
        ],
    },
    {
        "TemplateID": "ability/emergency_levy", "Name": "Emergency Levy", "AbilityType": "instant_spell",
        "Description": "Every owned soldier-granting building gains one charge, up to its maximum.",
        "ManaCost": 90, "Cooldown": 50, "TargetRange": 0, "GridX": 2, "GridY": 0, "Hotkey": "E",
    },
    {
        "TemplateID": "ability/mend", "Name": "Mend", "AbilityType": "target_spell",
        "Description": "Auto-cast: heals the most wounded nearby ally for 22.",
        "ManaCost": 0, "Cooldown": 2.2, "TargetRange": 8, "AreaOfEffectRadius": 0,
        "AutoCast": {"Enabled": True, "RequiresEnemyTarget": False, "AllyHealthPercentBelow": 0.9},
        "Effects": [
            {"Kind": "heal", "Delivery": "single", "Targets": "allies", "Amount": 22},
        ],
    },
    {
        "TemplateID": "ability/warding_aegis", "Name": "Warding Aegis", "AbilityType": "target_spell",
        "Description": "Auto-cast: shields the lowest-health nearby ally for 60 points for 8 seconds.",
        "ManaCost": 0, "Cooldown": 12, "TargetRange": 8, "AreaOfEffectRadius": 0,
        "AutoCast": {"Enabled": True, "RequiresEnemyTarget": False, "AllyHealthPercentBelow": 1.0},
        "Effects": [
            {"Kind": "shield", "Delivery": "single", "Targets": "allies", "Amount": 60, "Duration": 8},
        ],
    },
    {
        "TemplateID": "ability/standard_aura", "Name": "Standard Aura", "AbilityType": "target_spell",
        "Description": "Every nearby ally carries 10% more movement speed.",
        "ManaCost": 0, "Cooldown": 5, "TargetRange": 7, "AreaOfEffectRadius": 7,
        "AutoCast": {"Enabled": True, "RequiresEnemyTarget": False, "AllyHealthPercentBelow": 1.0},
        "Effects": [
            {"Kind": "buff", "Delivery": "radius", "Targets": "allies", "Radius": 7, "Duration": 6, "BuffId": "buff/standard_aura"},
        ],
    },
    {
        "TemplateID": "ability/begin_construction", "Name": "Begin Construction", "AbilityType": "target_spell",
        "Description": "Costs 100 wood. Draws up to three buildings from the shared deck; pick one to raise its site here, then right-click the site to build it.",
        "ManaCost": 0, "Cooldown": 0, "TargetRange": 8, "GridX": 3, "GridY": 0, "Hotkey": "B",
    },
]

RELICS = [
    ("item/sigil_of_the_wall", "Sigil of the Wall", "Front-formation soldiers gain 10% maximum health."),
    ("item/couriers_knot", "Courier's Knot", "All recharge is 10% faster."),
    ("item/spiked_barricades", "Spiked Barricades", "All owned buildings return damage to their attackers."),
    ("item/deep_granaries", "Deep Granaries", "Workers gain 10% attack, so their gathering swings harder."),
    ("item/wardens_writ", "Warden's Writ", "All owned units suffer 10% shorter debuff durations."),
    ("item/muster_bell", "Muster Bell", "Charge recharge is 10% faster."),
]

def growth(vitality=0, strength=0, agility=0, intelligence=0, wisdom=0, fortune=0):
    return {
        "StrengthPerLevel": strength,
        "AgilityPerLevel": agility,
        "VitalityPerLevel": vitality,
        "IntelligencePerLevel": intelligence,
        "WisdomPerLevel": wisdom,
        "FortunePerLevel": fortune,
    }

def soldier(entry):
    template_id, name, hp, armor, damage, interval, attack_range, speed, bounty_gold, bounty_xp, model, abilities = entry
    return {
        "TemplateID": template_id,
        "Name": name,
        "Description": "",
        "MaxHp": hp,
        "Damage": damage,
        "Range": attack_range,
        "Armor": armor,
        "Speed": speed,
        "AttackCooldown": interval,
        "ScanRadius": max(12, attack_range),
        "CostGold": 0,
        "CostWood": 0,
        "CostStone": 0,
        "PopCost": 1,
        "ProductionTime": 0,
        "HpRegen": 0.6,
        "MaxMana": 0,
        "ManaRegen": 0,
        "DamageType": "physical",
        "MovementType": "Ground",
        "SightRange": 16,
        "AcquisitionRange": max(12, attack_range + 2),
        "GoldBounty": bounty_gold,
        "XpBounty": bounty_xp,
        "LevelGrowth": growth(vitality=2.5, strength=1.2, agility=0.8, intelligence=0.3),
        "ModelPath": model,
        "VisualMode": "Mesh",
        "Scale": 1.0,
        "YOffset": 0.8,
        "CollisionCircle": 0.5,
        "PathingType": 9,
        "Abilities": abilities or None,
        "IsHero": False,
        "IncludeInDefaultHotkeySelection": False,
    }

def hero(entry):
    (template_id, name, hp, armor, damage, interval, attack_range, speed, mana, mana_regen,
     strength, agility, vitality, intelligence, wisdom, fortune, model, scale, abilities) = entry
    return {
        "TemplateID": template_id,
        "Name": name,
        "Description": HERO_DESCRIPTIONS[template_id],
        "MaxHp": hp,
        "Damage": damage,
        "Range": attack_range,
        "Armor": armor,
        "Speed": speed,
        "AttackCooldown": interval,
        "ScanRadius": max(14, attack_range),
        "CostGold": 0,
        "CostWood": 0,
        "CostStone": 0,
        "PopCost": 0,
        "ProductionTime": 0,
        "Strength": strength,
        "Agility": agility,
        "Vitality": vitality,
        "Intelligence": intelligence,
        "Wisdom": wisdom,
        "Fortune": fortune,
        "HpRegen": 2.0,
        "MaxMana": mana,
        "ManaRegen": mana_regen,
        "DamageType": "physical",
        "MovementType": "Ground",
        "SightRange": 18,
        "AcquisitionRange": max(14, attack_range),
        "GoldBounty": 100,
        "XpBounty": 120,
        "ExperiencePerLevel": 200,
        "LevelGrowth": growth(vitality=14, strength=3.2, agility=2.4, intelligence=3.0, wisdom=1.2, fortune=1.0),
        "ModelPath": model,
        "VisualMode": "Mesh",
        "Scale": scale,
        "YOffset": 0.8,
        "CollisionCircle": 0.6,
        "PathingType": 9,
        "BuildOptions": None,
        "Abilities": abilities,
        "IsHero": True,
        "IncludeInDefaultHotkeySelection": True,
    }

def building(entry):
    template_id, name, hp, armor, model, scale, y_offset, collision, abilities = entry
    construction = CONSTRUCTION_TIMES.get(template_id, 0)
    return {
        "TemplateID": template_id,
        "Name": name,
        "Description": "",
        "MaxHp": hp,
        "Armor": armor,
        "CostGold": 0,
        "CostWood": 100 if construction > 0 else 0,
        "CostStone": 0,
        "PopCost": 0,
        "ProductionTime": construction,
        "HpRegen": 0,
        "DamageType": "physical",
        "MovementType": "Ground",
        "SightRange": 20,
        "AcquisitionRange": 0,
        "GoldBounty": 50,
        "XpBounty": 40,
        "ModelPath": model,
        "VisualMode": "Mesh",
        "Scale": scale,
        "YOffset": y_offset,
        "CollisionCircle": collision,
        "PathingType": 32,
        "Abilities": abilities or None,
        "IsHero": False,
        "ConstructionRange": 4.0,
    }

def relic(entry):
    template_id, name, description = entry
    return {
        "TemplateID": template_id,
        "Name": name,
        "Description": description,
        "ItemClass": "relic",
        "CostGold": 100,
        "ChargeCount": 1,
        "ItemLevel": 0,
        "CanDrop": False,
        "GrantedUpgrades": None,
        "GrantedAbilities": None,
    }

resource_gold = {
    "TemplateID": "resource/melee_gold_mine",
    "Name": "Gold Mine",
    "Description": "",
    "ModelPath": "gold_mine.rmesh",
    "VisualMode": "Mesh",
    "MaxCapacity": 5000,
    "HarvestRate": 8,
    "GrowthRate": 0,
    "MaxWorkers": 0,
    "Scale": 1.6,
    "YOffset": 1.6,
    "CollisionCircle": 1.5,
    "Brightness": 0.5,
    "Tint": "#ffffff",
    "NormalizeLuminance": True,
    "IgnorePlayerColor": True,
    "PathingType": 255,
}

resource_wood = {
    "TemplateID": "resource/melee_woodland",
    "Name": "Woodland",
    "Description": "",
    "ModelPath": "coniferous_tree_1.rmesh",
    "VisualMode": "Mesh",
    "MaxCapacity": 4000,
    "HarvestRate": 8,
    "GrowthRate": 0,
    "MaxWorkers": 0,
    "Scale": 2.4,
    "YOffset": 2.4,
    "CollisionCircle": 1.0,
    "Brightness": 0.5,
    "Tint": "#ffffff",
    "NormalizeLuminance": True,
    "IgnorePlayerColor": True,
    "PathingType": 255,
}

nexus = building(BUILDINGS[0])

metadata = {
    "license": "https://www.realm-game.com/RealmPlatform_UGC_License_v1.txt",
    "GameBuildNumber": "v0.0.6",
    "MapProperties": {
        "MapName": "Melee Ruleset",
        "MapDescription": "The melee ruleset: one hero, buildings that raise an army from their charges, relics bought at the Nexus, and a Nexus to break. Terrain and models are composed from a sub-archive.",
        "Author": "Realm",
        "SuggestedPlayers": "2-4",
        "ShroudType": "Revealed",
        "MapWidth": 128,
        "MapHeight": 128,
        "CameraBoundsLeft": -95,
        "CameraBoundsRight": 95,
        "CameraBoundsTop": -95,
        "CameraBoundsBottom": 125,
        "Version": "1.0.0",
        "Tags": ["Melee", "Ruleset"],
        "MapType": "Melee",
        "PlayablePlayerSlots": 4,
        "ShowUnitHealthBar": True,
        "ShowUnitManaBar": True,
        "ShowUnitLevel": True,
        "HowToPlayObjective": "Destroy the enemy Nexus.",
        "HowToPlayInstructions": "Your hero is the only unit you command. Begin Construction draws three buildings from the shared deck and shows them in the command grid of your hero and Nexus: pick one for 100 wood and it produces soldiers automatically from its charges, with every soldier matching your hero's level. Spend gold at your Nexus on relics.",
    },
    "Dependencies": [],
    "Templates": {
        "Units": [hero(entry) for entry in HEROES] + [soldier(entry) for entry in SOLDIERS],
        "Buildings": [nexus] + [building(entry) for entry in BUILDINGS[1:]],
        "Resources": [resource_gold, resource_wood],
        "Props": [],
        "Weapons": [],
        "Abilities": ABILITIES,
        "Upgrades": [],
        "Items": [relic(entry) for entry in RELICS],
        "Attachments": [],
        "Vfx": [],
    },
    "CustomProceduralAnimations": [],
    "CustomWaterProfiles": [],
    "CustomEnvironmentPresets": [],
    "DefaultEnvironmentPreset": None,
    "textures": {},
    "decals": {},
    "vfx_spritesheets": {},
    "noise_textures": {},
    "icons": {},
    "skyboxes": {},
    "ribbons": {},
    "SpawnShader": {},
    "gdshader": {},
}

with open("metadata.json", "w", encoding="utf-8", newline="\n") as handle:
    json.dump(metadata, handle, indent=2)
    handle.write("\n")

print("templates:", {key: len(value) for key, value in metadata["Templates"].items()})
