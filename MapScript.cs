namespace Realm.Maps;

using System;
using System.Numerics;
using Realm.Ecs;
using Realm.MapAPI;

public class MapScript : IWasmModule, ILobbyScript
{
	private const string NexusTemplateId = "building/nexus";
	private const string GoldNodeTemplateId = "resource/melee_gold_mine";
	private const string WoodNodeTemplateId = "resource/melee_woodland";

	private const int MaximumBuildingsPerPlayer = 5;
	private const int MaximumPopulation = 80;
	private const float StartingGold = 200f;
	private const float StartingWood = 100f;
	private const float UnderAttackAlertCooldownSeconds = 8f;
	private const float BaseRevivalSeconds = 6f;
	private const float RevivalSecondsPerDeath = 3f;
	private const float IncomeTickSeconds = 1f;
	private const float IncomeTaxPerBuilding = 0.15f;
	private const float CinderMarkDamagePerSecond = 6f;
	private const float RequisitionCompletionFraction = 0.3f;
	private const float BulwarkRecalculateSeconds = 1f;
	private const float BulwarkSoldierRadius = 10f;
	private const float BulwarkArmorPerSoldier = 0.6f;
	private const float BulwarkMagicResistancePerSoldier = 0.6f;
	private const float BulwarkArmorPerBuilding = 1.5f;
	private const float BulwarkMagicResistancePerBuilding = 1.5f;

	private readonly record struct SoldierProfile(
		string SoldierTemplateId,
		string BuildingTemplateId,
		int Capacity,
		int MaximumCharges,
		float RechargeSeconds,
		float ProductionSeconds,
		bool FrontFormation);

	private readonly record struct BuildingProfile(
		string TemplateId,
		float GoldPerMinute,
		float WoodPerMinute);

	private static readonly string[] HeroTemplateIds = { "hero/vale", "hero/sable", "hero/corvus" };
	private static readonly string[] HeroFactionNames = { "Vale", "Sable", "Corvus" };
	private static readonly string[] RelicIds =
	{
		"item/sigil_of_the_wall",
		"item/couriers_knot",
		"item/spiked_barricades",
		"item/deep_granaries",
		"item/wardens_writ",
		"item/muster_bell"
	};

	private static readonly Vector2[] SpawnAnchors =
	{
		new(-58f, -58f),
		new(58f, -58f),
		new(-58f, 92f),
		new(58f, 92f)
	};

	private static readonly Vector2[] GoldNodePositions =
	{
		new(-44f, -58f), new(44f, -58f), new(-44f, 92f), new(44f, 92f),
		new(-20f, 15f), new(20f, 15f)
	};

	private static readonly Vector2[] WoodNodePositions =
	{
		new(-58f, -44f), new(58f, -44f), new(-58f, 106f), new(58f, 106f),
		new(0f, -40f), new(0f, 70f)
	};

	private static readonly SoldierProfile[] SoldierProfiles =
	{
		new("unit/shieldbearer", "building/bulwark_hall", 6, 3, 22f, 5f, true),
		new("unit/axeman", "building/warband_yard", 6, 3, 20f, 4f, false),
		new("unit/spear_militia", "building/spear_lodge", 5, 3, 24f, 5f, true),
		new("unit/bowman", "building/archery_range", 8, 4, 18f, 4f, false),
		new("unit/slinger", "building/sling_camp", 8, 4, 16f, 3f, false),
		new("unit/bolt_thrower", "building/bolt_foundry", 2, 1, 30f, 8f, false),
		new("unit/mender", "building/mender_shrine", 4, 2, 26f, 5f, false),
		new("unit/standard_bearer", "building/signal_post", 3, 2, 28f, 5f, false),
		new("unit/warder", "building/vigil_bastion", 3, 2, 26f, 6f, true)
	};

	private static readonly BuildingProfile[] BuildingProfiles =
	{
		new("building/bulwark_hall", 30f, 0f),
		new("building/warband_yard", 16f, 0f),
		new("building/spear_lodge", 10f, 0f),
		new("building/archery_range", 0f, 22f),
		new("building/bolt_foundry", 0f, 8f),
		new("building/sling_camp", 0f, 16f),
		new("building/mender_shrine", 14f, 0f),
		new("building/signal_post", 6f, 0f),
		new("building/vigil_bastion", 10f, 0f)
	};

	private readonly int[] _charges = new int[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _rechargeTimers = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _productionTimers = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly int[] _lastBadgeValues = new int[EcsSharedMemory.MAX_ENTITIES];
	private readonly byte[] _producing = new byte[EcsSharedMemory.MAX_ENTITIES];
	private readonly int[] _soldierCounts = new int[SoldierProfiles.Length];
	private readonly int[] _capacityCounts = new int[SoldierProfiles.Length];

	private readonly int[] _heroEntities = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _nexusEntities = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _heroDeaths = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _heroTemplateIndices = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _reviveAtTimes = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _lastAlertTimes = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly byte[] _relicFlags = new byte[EcsSharedMemory.MAX_PLAYERS];
	private readonly byte[] _eliminated = new byte[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _nexusPositionsX = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _nexusPositionsZ = new float[EcsSharedMemory.MAX_PLAYERS];

	private IGameAPI _api = null!;
	private float _incomeTimer;
	private float _bulwarkTimer;

	public void Initialize(IGameAPI api)
	{
		_api = api;

		api.SetUnitBarVisibility(true, true, true);
		api.RegisterPrimaryBuilding(NexusTemplateId);
		api.RegisterResourceDropoffBuilding(NexusTemplateId);
		api.RegisterResourceNodeType(GoldNodeTemplateId, "gold");
		api.RegisterResourceNodeType(WoodNodeTemplateId, "wood");

		for (int index = 0; index < HeroTemplateIds.Length; index++)
		{
			api.RegisterResourceWorkerUnit(HeroTemplateIds[index]);
		}

		api.RegisterBuffModifier("buff/grounding_shout", "MovementSpeed", true, -25f);
		api.RegisterBuffModifier("buff/hold_fast", "Armor", false, 4f);
		api.RegisterBuffModifier("buff/forced_march", "MovementSpeed", true, 20f);
		api.RegisterBuffModifier("buff/standard_aura", "MovementSpeed", true, 10f);
		api.RegisterBuffModifier("buff/cinder_mark", "HpRegen", false, 0f);

		api.OnUnitDied += HandleUnitDied;
		api.OnUnitDamaged += HandleUnitDamaged;
		api.OnSpellCast += HandleSpellCast;
		api.OnConstructionFinished += HandleConstructionFinished;

		ReadOnlySpan<PlayerId> players = api.GetActivePlayers();
		for (int index = 0; index < players.Length; index++)
		{
			if (players[index].Value == 0) continue;
			ConfigurePlayer(api, players[index]);
		}

		SpawnResourceNodes(api);
	}

	public void Update(IGameAPI api, float delta)
	{
		ReadOnlySpan<PlayerId> players = api.GetActivePlayers();

		for (int index = 0; index < players.Length; index++)
		{
			PlayerId player = players[index];
			if (player.Value == 0 || !player.IsActive) continue;

			UpdateArmyLevel(api, player);
			UpdateRevival(api, player);
			UpdateProduction(api, player, delta);
		}

		_bulwarkTimer += delta;
		bool recalculateBulwark = _bulwarkTimer >= BulwarkRecalculateSeconds;
		if (recalculateBulwark) _bulwarkTimer = 0f;

		_incomeTimer += delta;
		bool applyIncome = _incomeTimer >= IncomeTickSeconds;
		float incomeDelta = _incomeTimer;
		if (applyIncome) _incomeTimer = 0f;

		for (int index = 0; index < players.Length; index++)
		{
			PlayerId player = players[index];
			if (player.Value == 0 || !player.IsActive) continue;

			RefreshRelics(api, player);
			if (recalculateBulwark) RefreshBulwark(api, player);
			if (applyIncome) ApplyIncome(api, player, incomeDelta);
			ApplyCinderMarks(api, player);
		}
	}

	public void LobbyInitialize(ILobbyAPI api)
	{
		for (int index = 0; index < HeroFactionNames.Length; index++)
		{
			api.AddFaction(HeroFactionNames[index]);
		}

		api.SetTeamCount(2);

		for (int slot = 1; slot <= api.SlotCount; slot++)
		{
			if (!api.IsOccupied(slot)) continue;
			if (api.GetPlayerFactionIndex(slot) >= 0) continue;

			api.SetPlayerFaction(slot, HeroFactionNames[0]);
		}
	}

	public void LobbyUpdate(ILobbyAPI api, float deltaSeconds)
	{
	}

	public bool TryGetStartWarning(ILobbyAPI api, out string message)
	{
		message = string.Empty;
		return false;
	}

	private void ConfigurePlayer(IGameAPI api, PlayerId player)
	{
		int playerIndex = player.Value;
		player.Gold = StartingGold;
		player.Wood = StartingWood;
		player.Stone = 0f;
		player.MaxPopulation = MaximumPopulation;
		api.SetPlayerBuildingLimit(playerIndex, MaximumBuildingsPerPlayer);

		Vector2 anchor = SpawnAnchors[Math.Clamp(playerIndex - 1, 0, SpawnAnchors.Length - 1)];
		_nexusPositionsX[playerIndex] = anchor.X;
		_nexusPositionsZ[playerIndex] = anchor.Y;

		UnitId nexus = player.SpawnUnit(NexusTemplateId, anchor, false, true);
		if (nexus.IsValid) _nexusEntities[playerIndex] = nexus.UniqueId;

		int heroTemplateIndex = ResolveHeroTemplateIndex(api.GetPlayerFaction(playerIndex));
		_heroTemplateIndices[playerIndex] = heroTemplateIndex;

		var heroPosition = new Vector2(anchor.X + 4f, anchor.Y + 4f);
		UnitId hero = player.SpawnUnit(HeroTemplateIds[heroTemplateIndex], heroPosition, true, true);
		if (!hero.IsValid) return;

		_heroEntities[playerIndex] = hero.UniqueId;
		api.SetUnitLevel(hero, 1);
		ApplyInnateToUnit(api, playerIndex, hero);

		player.PanCameraTo(new Vector3(anchor.X, 0f, anchor.Y), 0f);
		player.ShowFeedbackText($"{HeroFactionNames[heroTemplateIndex]} answers the call.", new Vector3(0.85f, 0.95f, 1f));
	}

	private void SpawnResourceNodes(IGameAPI api)
	{
		for (int index = 0; index < GoldNodePositions.Length; index++)
		{
			Vector2 position = GoldNodePositions[index];
			api.SpawnResourceNode(GoldNodeTemplateId, new Vector3(position.X, 0f, position.Y), 5000f);
		}

		for (int index = 0; index < WoodNodePositions.Length; index++)
		{
			Vector2 position = WoodNodePositions[index];
			api.SpawnResourceNode(WoodNodeTemplateId, new Vector3(position.X, 0f, position.Y), 4000f);
		}
	}

	private void UpdateArmyLevel(IGameAPI api, PlayerId player)
	{
		int heroEntityId = _heroEntities[player.Value];
		if (heroEntityId <= 0) return;

		UnitId hero = new(heroEntityId);
		if (hero.IsDead) return;

		int heroLevel = hero.Level;
		if (heroLevel <= 1) return;

		ReadOnlySpan<UnitId> owned = player.GetUnits();
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead || unit.IsHero || unit.IsBuilding) continue;
			if (unit.Level == heroLevel) continue;

			api.SetUnitLevel(unit, heroLevel);
		}
	}

	private void UpdateRevival(IGameAPI api, PlayerId player)
	{
		int playerIndex = player.Value;
		int heroEntityId = _heroEntities[playerIndex];
		if (heroEntityId > 0 && !new UnitId(heroEntityId).IsDead) return;
		if (_reviveAtTimes[playerIndex] <= 0f) return;
		if (api.GameElapsedTime < _reviveAtTimes[playerIndex]) return;

		_reviveAtTimes[playerIndex] = 0f;

		var position = new Vector2(_nexusPositionsX[playerIndex] + 4f, _nexusPositionsZ[playerIndex] + 4f);
		UnitId hero = player.SpawnUnit(HeroTemplateIds[_heroTemplateIndices[playerIndex]], position, true, true);
		if (!hero.IsValid) return;

		_heroEntities[playerIndex] = hero.UniqueId;
		api.SetUnitLevel(hero, 1);
		ApplyInnateToUnit(api, playerIndex, hero);
		ApplyRelicsToUnit(api, playerIndex, hero);
		RefillChargesForPlayer(api, player);

		player.PanCameraTo(hero.Position, 0.6f);
		player.ShowFeedbackText("Your hero returns to the field.", new Vector3(0.6f, 1f, 0.7f));
	}

	private void UpdateProduction(IGameAPI api, PlayerId player, float delta)
	{
		int playerIndex = player.Value;
		ReadOnlySpan<UnitId> owned = player.GetUnits();

		for (int index = 0; index < SoldierProfiles.Length; index++)
		{
			_soldierCounts[index] = 0;
			_capacityCounts[index] = 0;
		}

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead) continue;

			if (unit.IsBuilding)
			{
				int grantedProfile = FindProfileIndexByBuilding(unit.TemplateID);
				if (grantedProfile >= 0) _capacityCounts[grantedProfile] += SoldierProfiles[grantedProfile].Capacity;
				continue;
			}

			int soldierProfile = FindProfileIndexBySoldier(unit.TemplateID);
			if (soldierProfile >= 0) _soldierCounts[soldierProfile]++;
		}

		float rechargeMultiplier = ResolveRechargeMultiplier(playerIndex);
		int extraCharges = IsStandingLedgerHero(playerIndex) ? 1 : 0;

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId building = owned[index];
			if (building.IsDead || !building.IsBuilding) continue;
			if (!IsBuildingOperational(api, building)) continue;

			int profileIndex = FindProfileIndexByBuilding(building.TemplateID);
			if (profileIndex < 0) continue;

			SoldierProfile profile = SoldierProfiles[profileIndex];
			int entityId = building.UniqueId;
			int maximumCharges = profile.MaximumCharges + extraCharges;

			if (_charges[entityId] < maximumCharges)
			{
				_rechargeTimers[entityId] += delta;
				float rechargeSeconds = profile.RechargeSeconds * rechargeMultiplier;
				if (_rechargeTimers[entityId] >= rechargeSeconds)
				{
					_rechargeTimers[entityId] -= rechargeSeconds;
					_charges[entityId]++;
					PublishChargeBadge(api, player, building, _charges[entityId]);
				}
			}

			int reservedCapacity = _soldierCounts[profileIndex] + (_producing[entityId] != 0 ? 1 : 0);
			if (_producing[entityId] == 0 && _charges[entityId] > 0 && reservedCapacity < _capacityCounts[profileIndex])
			{
				_producing[entityId] = 1;
				_productionTimers[entityId] = profile.ProductionSeconds;
			}

			if (_producing[entityId] == 0) continue;

			_productionTimers[entityId] -= delta;
			if (_productionTimers[entityId] > 0f) continue;

			_producing[entityId] = 0;
			SpawnSoldier(api, player, profile, building);
			_charges[entityId] = Math.Max(0, _charges[entityId] - 1);
			PublishChargeBadge(api, player, building, _charges[entityId]);
		}
	}

	private void SpawnSoldier(IGameAPI api, PlayerId player, SoldierProfile profile, UnitId building)
	{
		Vector3 origin = building.Position;
		float angle = (building.UniqueId % 8) * (MathF.Tau / 8f);
		var position = new Vector2(origin.X + MathF.Cos(angle) * 2.5f, origin.Z + MathF.Sin(angle) * 2.5f);

		UnitId soldier = player.SpawnUnit(profile.SoldierTemplateId, position, true, true);
		if (!soldier.IsValid) return;

		int heroEntityId = _heroEntities[player.Value];
		if (heroEntityId > 0)
		{
			UnitId hero = new(heroEntityId);
			if (!hero.IsDead && hero.Level > 1) api.SetUnitLevel(soldier, hero.Level);
		}

		ApplyInnateToUnit(api, player.Value, soldier);
		ApplyRelicsToUnit(api, player.Value, soldier);
	}

	private void ApplyIncome(IGameAPI api, PlayerId player, float delta)
	{
		int playerIndex = player.Value;
		ReadOnlySpan<UnitId> owned = player.GetUnits();
		float goldPerMinute = 0f;
		float woodPerMinute = 0f;
		int functionalBuildings = 0;

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead || !unit.IsBuilding) continue;
			if (!IsBuildingOperational(api, unit)) continue;

			BuildingProfile profile = FindBuildingProfile(unit.TemplateID);
			if (string.IsNullOrEmpty(profile.TemplateId)) continue;

			float taxDivisor = 1f + functionalBuildings * IncomeTaxPerBuilding;
			goldPerMinute += profile.GoldPerMinute / taxDivisor;
			woodPerMinute += profile.WoodPerMinute / taxDivisor;
			functionalBuildings++;
		}

		if (goldPerMinute <= 0f && woodPerMinute <= 0f) return;

		float incomeMultiplier = HasRelic(playerIndex, 3) ? 1.1f : 1f;
		float secondsFraction = delta / 60f;
		player.AdjustGold(goldPerMinute * incomeMultiplier * secondsFraction);
		player.AdjustWood(woodPerMinute * secondsFraction);
	}

	private void ApplyCinderMarks(IGameAPI api, PlayerId player)
	{
		if (!IsCinderMarkHero(player.Value)) return;

		int heroEntityId = _heroEntities[player.Value];
		if (heroEntityId <= 0) return;

		UnitId hero = new(heroEntityId);
		if (hero.IsDead) return;

		ReadOnlySpan<UnitId> allUnits = api.GetAllUnits();
		for (int index = 0; index < allUnits.Length; index++)
		{
			UnitId unit = allUnits[index];
			if (unit.IsDead || unit.IsHero) continue;
			if (api.ArePlayersAllied(unit.Player, player.Value)) continue;
			if (!unit.HasBuff("buff/cinder_mark")) continue;

			api.DealTypedDamage(hero, unit, CinderMarkDamagePerSecond, "magic");
		}
	}

	private void RefreshBulwark(IGameAPI api, PlayerId player)
	{
		int playerIndex = player.Value;
		int heroEntityId = _heroEntities[playerIndex];
		if (heroEntityId <= 0) return;

		UnitId hero = new(heroEntityId);
		if (hero.IsDead) return;

		ReadOnlySpan<UnitId> owned = player.GetUnits();
		int nearbySoldiers = 0;
		int functionalBuildings = 0;

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead) continue;

			if (unit.IsBuilding)
			{
				if (IsBuildingOperational(api, unit)) functionalBuildings++;
				continue;
			}

			if (unit.IsHero) continue;
			if (Vector3.DistanceSquared(unit.Position, hero.Position) <= BulwarkSoldierRadius * BulwarkSoldierRadius) nearbySoldiers++;
		}

		api.AddStatModifier(hero, "Armor", false, nearbySoldiers * BulwarkArmorPerSoldier, "bulwark/hero");
		api.AddStatModifier(hero, "MagicResistance", false, nearbySoldiers * BulwarkMagicResistancePerSoldier, "bulwark/hero");

		int nexusEntityId = _nexusEntities[playerIndex];
		if (nexusEntityId <= 0) return;

		UnitId nexus = new(nexusEntityId);
		if (nexus.IsDead) return;

		api.AddStatModifier(nexus, "Armor", false, functionalBuildings * BulwarkArmorPerBuilding, "bulwark/buildings");
		api.AddStatModifier(nexus, "MagicResistance", false, functionalBuildings * BulwarkMagicResistancePerBuilding, "bulwark/buildings");
	}

	private void HandleConstructionFinished(UnitId building)
	{
		if (!building.IsValid || !building.IsBuilding) return;

		int playerIndex = building.Player;
		if (playerIndex <= 0) return;

		int profileIndex = FindProfileIndexByBuilding(building.TemplateID);
		if (profileIndex < 0) return;

		int entityId = building.UniqueId;
		int extraCharges = IsStandingLedgerHero(playerIndex) ? 1 : 0;
		_charges[entityId] = SoldierProfiles[profileIndex].MaximumCharges > 0 ? 1 : 0;
		_rechargeTimers[entityId] = 0f;

		PlayerId player = _api.GetPlayer(playerIndex);
		PublishChargeBadge(_api, player, building, _charges[entityId]);
		player.ShowFeedbackText($"{_api.GetUnitName(building.TemplateID)} is complete.", new Vector3(0.8f, 0.9f, 1f));

		if (extraCharges <= 0) return;

		_charges[entityId] = Math.Min(_charges[entityId], SoldierProfiles[profileIndex].MaximumCharges + extraCharges);
	}

	private void HandleUnitDamaged(UnitId victim, UnitId attacker, float damage)
	{
		if (!victim.IsValid || damage <= 0.01f) return;

		int playerIndex = victim.Player;
		if (playerIndex <= 0) return;

		PlayerId player = _api.GetPlayer(playerIndex);
		if (!player.IsActive) return;
		if (attacker.IsValid && player.IsAlliedWith(attacker.Player)) return;

		if (IsCinderMarkHero(attacker.Player) && attacker.IsHero && !victim.IsBuilding)
		{
			victim.AddBuff("buff/cinder_mark", 2f);
		}

		if (victim.IsBuilding && HasRelic(playerIndex, 2) && attacker.IsValid)
		{
			_api.DealTypedDamage(victim, attacker, damage * 0.25f, "physical");
		}

		float elapsed = _api.GameElapsedTime;
		if (elapsed - _lastAlertTimes[playerIndex] < UnderAttackAlertCooldownSeconds) return;

		_lastAlertTimes[playerIndex] = elapsed;
		string alert = victim.TemplateID == NexusTemplateId
			? "Your Nexus is under attack!"
			: $"{_api.GetUnitName(victim.TemplateID)} is under attack.";

		player.ShowFeedbackText(alert, new Vector3(1f, 0.25f, 0.15f));
		player.PingMinimap(victim.Position);
		player.PlayWarningSound();
	}

	private void HandleUnitDied(UnitId unit, UnitId killer)
	{
		if (!unit.IsValid) return;

		if (unit.IsHero)
		{
			HandleHeroDied(unit);
			return;
		}

		if (unit.IsBuilding && unit.TemplateID == NexusTemplateId)
		{
			HandleNexusDestroyed(unit);
		}
	}

	private void HandleHeroDied(UnitId hero)
	{
		int playerIndex = hero.Player;
		if (playerIndex <= 0) return;

		_heroDeaths[playerIndex]++;
		float revivalSeconds = BaseRevivalSeconds + Math.Max(0, _heroDeaths[playerIndex] - 1) * RevivalSecondsPerDeath;
		_reviveAtTimes[playerIndex] = _api.GameElapsedTime + revivalSeconds;

		ReadOnlySpan<UnitId> owned = _api.GetUnitsOwnedByPlayer(playerIndex);
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead || unit.IsHero || unit.IsBuilding) continue;

			unit.Destroy(false, false);
			_producing[unit.UniqueId] = 0;
			_productionTimers[unit.UniqueId] = 0f;
		}

		PlayerId player = _api.GetPlayer(playerIndex);
		player.ShowFeedbackText("Your hero has fallen. The army disperses.", new Vector3(1f, 0.35f, 0.3f));
		player.PlayWarningSound();
	}

	private void HandleNexusDestroyed(UnitId nexus)
	{
		int playerIndex = nexus.Player;
		if (playerIndex <= 0 || _eliminated[playerIndex] != 0) return;

		_eliminated[playerIndex] = 1;

		PlayerId player = _api.GetPlayer(playerIndex);
		player.TriggerDefeat("Your Nexus has been destroyed.");
		CheckTeamVictory();
	}

	private void CheckTeamVictory()
	{
		int survivingTeam = 0;
		bool contested = false;

		ReadOnlySpan<PlayerId> players = _api.GetActivePlayers();
		for (int index = 0; index < players.Length; index++)
		{
			PlayerId player = players[index];
			if (player.Value == 0 || !player.IsActive || _eliminated[player.Value] != 0) continue;
			if (_nexusEntities[player.Value] > 0 && new UnitId(_nexusEntities[player.Value]).IsDead) continue;

			int team = player.Team;
			if (survivingTeam == 0) survivingTeam = team;
			else if (survivingTeam != team)
			{
				contested = true;
				break;
			}
		}

		if (contested || survivingTeam == 0) return;

		for (int index = 0; index < players.Length; index++)
		{
			PlayerId player = players[index];
			if (player.Value == 0 || !player.IsActive || _eliminated[player.Value] != 0) continue;
			if (player.Team != survivingTeam) continue;

			player.TriggerVictory();
		}
	}

	private void HandleSpellCast(UnitId caster, string abilityId, Vector3 position)
	{
		if (!caster.IsValid) return;

		int playerIndex = caster.Player;
		if (playerIndex <= 0) return;

		if (abilityId == "ability/requisition")
		{
			AdvanceActiveConstruction(playerIndex);
			return;
		}

		if (abilityId == "ability/emergency_levy")
		{
			GrantOneChargeToBuildings(playerIndex);
		}
	}

	private void AdvanceActiveConstruction(int playerIndex)
	{
		ReadOnlySpan<UnitId> owned = _api.GetUnitsOwnedByPlayer(playerIndex);
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId building = owned[index];
			if (building.IsDead || !building.IsBuilding) continue;

			float progress = _api.GetUnitConstructionProgress(building);
			if (progress >= 1f) continue;

			float remainingSeconds = (1f - progress) * ResolveConstructionSeconds(building.TemplateID);
			_api.AdvanceUnitConstruction(building, remainingSeconds * RequisitionCompletionFraction);

			PlayerId player = _api.GetPlayer(playerIndex);
			player.ShowFeedbackText("Requisition speeds the work along.", new Vector3(0.9f, 0.85f, 0.5f));
			return;
		}
	}

	private void GrantOneChargeToBuildings(int playerIndex)
	{
		ReadOnlySpan<UnitId> owned = _api.GetUnitsOwnedByPlayer(playerIndex);
		PlayerId player = _api.GetPlayer(playerIndex);
		int extraCharges = IsStandingLedgerHero(playerIndex) ? 1 : 0;

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId building = owned[index];
			if (building.IsDead || !building.IsBuilding) continue;

			int profileIndex = FindProfileIndexByBuilding(building.TemplateID);
			if (profileIndex < 0) continue;
			if (!IsBuildingOperational(_api, building)) continue;

			int entityId = building.UniqueId;
			int maximumCharges = SoldierProfiles[profileIndex].MaximumCharges + extraCharges;
			_charges[entityId] = Math.Min(maximumCharges, _charges[entityId] + 1);
			PublishChargeBadge(_api, player, building, _charges[entityId]);
		}
	}

	private void RefillChargesForPlayer(IGameAPI api, PlayerId player)
	{
		int playerIndex = player.Value;
		int extraCharges = IsStandingLedgerHero(playerIndex) ? 1 : 0;

		ReadOnlySpan<UnitId> owned = player.GetUnits();
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId building = owned[index];
			if (building.IsDead || !building.IsBuilding) continue;

			int profileIndex = FindProfileIndexByBuilding(building.TemplateID);
			if (profileIndex < 0) continue;

			int entityId = building.UniqueId;
			_charges[entityId] = SoldierProfiles[profileIndex].MaximumCharges + extraCharges;
			_rechargeTimers[entityId] = 0f;
			PublishChargeBadge(api, player, building, _charges[entityId]);
		}
	}

	private void PublishChargeBadge(IGameAPI api, PlayerId player, UnitId building, int charges)
	{
		int entityId = building.UniqueId;
		if (_lastBadgeValues[entityId] == charges + 1) return;

		_lastBadgeValues[entityId] = charges + 1;
		api.SetUnitOverheadBadge(building, charges, player.Color);
	}

	private void RefreshRelics(IGameAPI api, PlayerId player)
	{
		int playerIndex = player.Value;
		int heroEntityId = _heroEntities[playerIndex];
		if (heroEntityId <= 0) return;

		UnitId hero = new(heroEntityId);
		if (hero.IsDead) return;

		byte previousFlags = _relicFlags[playerIndex];
		byte flags = 0;
		for (int index = 0; index < RelicIds.Length; index++)
		{
			if (hero.HasItem(RelicIds[index])) flags |= (byte)(1 << index);
		}

		_relicFlags[playerIndex] = flags;
		if (flags == previousFlags) return;

		if ((flags & (1 << 0)) != 0 && (previousFlags & (1 << 0)) == 0)
		{
			ApplySigilOfTheWall(api, player);
		}

		if ((flags & (1 << 4)) != 0 && (previousFlags & (1 << 4)) == 0)
		{
			ApplyWardensWrit(api, player);
		}
	}

	private void ApplySigilOfTheWall(IGameAPI api, PlayerId player)
	{
		ReadOnlySpan<UnitId> owned = player.GetUnits();
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead || unit.IsHero || unit.IsBuilding) continue;
			if (!IsFrontFormationSoldier(unit.TemplateID)) continue;

			api.SetUnitMaxHealth(unit, unit.MaxHealth * 1.1f);
		}
	}

	private void ApplyWardensWrit(IGameAPI api, PlayerId player)
	{
		ReadOnlySpan<UnitId> owned = player.GetUnits();
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead) continue;

			api.AddStatModifier(unit, "Tenacity", true, 10f, "relic/wardens_writ");
		}
	}

	private void ApplyInnateToUnit(IGameAPI api, int playerIndex, UnitId unit)
	{
		if (!unit.IsValid) return;

		switch (_heroTemplateIndices[playerIndex])
		{
			case 0:
				api.AddStatModifier(unit, "Armor", false, 6f, "innate/unbroken");
				api.AddStatModifier(unit, "Tenacity", true, 10f, "innate/unbroken");
				break;
			case 1:
				unit.AddBuff("buff/cinder_mark", 2f);
				break;
		}
	}

	private void ApplyRelicsToUnit(IGameAPI api, int playerIndex, UnitId unit)
	{
		if (!unit.IsValid) return;

		byte flags = _relicFlags[playerIndex];
		if ((flags & (1 << 0)) != 0 && IsFrontFormationSoldier(unit.TemplateID))
		{
			api.SetUnitMaxHealth(unit, unit.MaxHealth * 1.1f);
		}

		if ((flags & (1 << 4)) != 0)
		{
			api.AddStatModifier(unit, "Tenacity", true, 10f, "relic/wardens_writ");
		}
	}

	private float ResolveRechargeMultiplier(int playerIndex)
	{
		byte flags = _relicFlags[playerIndex];
		float multiplier = 1f;
		if ((flags & (1 << 1)) != 0) multiplier *= 0.9f;
		if ((flags & (1 << 5)) != 0) multiplier *= 0.9f;

		return multiplier;
	}

	private static float ResolveConstructionSeconds(string templateId)
	{
		BuildingProfile profile = FindBuildingProfile(templateId);
		return string.IsNullOrEmpty(profile.TemplateId) ? 30f : 30f;
	}

	private static bool IsBuildingOperational(IGameAPI api, UnitId building)
	{
		if (building.Health <= 0f) return false;

		return api.GetUnitConstructionProgress(building) >= 1f;
	}

	private bool HasRelic(int playerIndex, int relicIndex)
	{
		return (_relicFlags[playerIndex] & (1 << relicIndex)) != 0;
	}

	private bool IsStandingLedgerHero(int playerIndex)
	{
		return _heroTemplateIndices[playerIndex] == 2;
	}

	private bool IsCinderMarkHero(int playerIndex)
	{
		return playerIndex > 0 && playerIndex < EcsSharedMemory.MAX_PLAYERS && _heroTemplateIndices[playerIndex] == 1;
	}

	private static bool IsFrontFormationSoldier(string templateId)
	{
		int profileIndex = FindProfileIndexBySoldier(templateId);
		return profileIndex >= 0 && SoldierProfiles[profileIndex].FrontFormation;
	}

	private static int FindProfileIndexBySoldier(string templateId)
	{
		if (string.IsNullOrEmpty(templateId)) return -1;

		for (int index = 0; index < SoldierProfiles.Length; index++)
		{
			if (SoldierProfiles[index].SoldierTemplateId == templateId) return index;
		}

		return -1;
	}

	private static int FindProfileIndexByBuilding(string templateId)
	{
		if (string.IsNullOrEmpty(templateId)) return -1;

		for (int index = 0; index < SoldierProfiles.Length; index++)
		{
			if (SoldierProfiles[index].BuildingTemplateId == templateId) return index;
		}

		return -1;
	}

	private static BuildingProfile FindBuildingProfile(string templateId)
	{
		if (string.IsNullOrEmpty(templateId)) return default;

		for (int index = 0; index < BuildingProfiles.Length; index++)
		{
			if (BuildingProfiles[index].TemplateId == templateId) return BuildingProfiles[index];
		}

		return default;
	}

	private static int ResolveHeroTemplateIndex(string faction)
	{
		if (!string.IsNullOrEmpty(faction))
		{
			for (int index = 0; index < HeroFactionNames.Length; index++)
			{
				if (string.Equals(HeroFactionNames[index], faction, StringComparison.OrdinalIgnoreCase)) return index;
			}
		}

		return 0;
	}
}
