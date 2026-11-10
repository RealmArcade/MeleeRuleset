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
	private const string ConstructionAbilityId = "ability/begin_construction";
	private const string AbandonConstructionAbilityId = "ability/abandon_construction";
	private const float BuildingWoodCost = 100f;
	private const int DraftOptionCount = 3;
	private const int DraftRow = 1;
	private const int DraftAbandonSlot = 3;
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

	private const string MendAbilityId = "ability/mend";
	private const string WardingAegisAbilityId = "ability/warding_aegis";
	private const string StandardAuraAbilityId = "ability/standard_aura";
	private const string StandardAuraBuffId = "buff/standard_aura";
	private const string MenderTemplateId = "unit/mender";
	private const string WarderTemplateId = "unit/warder";
	private const string StandardBearerTemplateId = "unit/standard_bearer";
	private const float MendRadius = 8f;
	private const float MendCooldownSeconds = 2.2f;
	private const float MendHealthFractionBelow = 0.9f;
	private const float WardingAegisRadius = 8f;
	private const float WardingAegisCooldownSeconds = 12f;
	private const float StandardAuraRadius = 7f;
	private const float StandardAuraCooldownSeconds = 5f;

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

	private static readonly float[] ConstructionSecondsByProfile = { 30f, 30f, 30f, 30f, 30f, 30f, 35f, 35f, 35f };

	private static readonly string[] DraftHotkeys = { "Z", "X", "C" };

	private readonly byte[] _draftOpen = new byte[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _draftOptionCounts = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _draftOptions = new int[EcsSharedMemory.MAX_PLAYERS * DraftOptionCount];
	private readonly float[] _draftSitePositionsX = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _draftSitePositionsZ = new float[EcsSharedMemory.MAX_PLAYERS];
	private int _deckTakenBuildings;

	private readonly int[] _heroEntities = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _nexusEntities = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _heroDeaths = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _heroTemplateIndices = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _reviveAtTimes = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _lastAlertTimes = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly byte[] _relicFlags = new byte[EcsSharedMemory.MAX_PLAYERS];
	private readonly byte[] _burningFromMark = new byte[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _cinderMarkDamage = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _mendTimers = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _wardingAegisTimers = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _standardAuraTimers = new float[EcsSharedMemory.MAX_ENTITIES];
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

		for (int index = 0; index < _lastAlertTimes.Length; index++)
		{
			_lastAlertTimes[index] = -UnderAttackAlertCooldownSeconds;
		}

		api.OnUnitDied += HandleUnitDied;
		api.OnUnitDamaged += HandleUnitDamaged;
		api.OnSpellCast += HandleSpellCast;
		api.OnConstructionFinished += HandleConstructionFinished;

		RegisterDraftAbilities(api);
		WarnWhenTheBoardIsNotAuthoredForMelee(api);

		ReadOnlySpan<PlayerId> players = api.GetActivePlayers();
		for (int index = 0; index < players.Length; index++)
		{
			if (players[index].Value == 0) continue;
			ConfigurePlayer(api, players[index]);
		}
	}

	private static void WarnWhenTheBoardIsNotAuthoredForMelee(IGameAPI api)
	{
		bool hasAnchors = api.GetSpawnAnchorCount() > 0;
		bool hasResourceNodes = api.GetResourceNodes().Length > 0;
		if (hasAnchors && hasResourceNodes) return;

		api.ShowFeedbackText(
			"This board does not seat Melee: it declares no spawn anchors or places no resource nodes.",
			new Vector3(1f, 0.4f, 0.3f));
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
			ApplyAutoCasts(api, player, delta);
			AdvanceOwnConstruction(api, player, delta);
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
			ApplyCinderMarks(api, player, delta);
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
		api.SetPlayerBuildingLimit(playerIndex, MaximumBuildingsPerPlayer + 1);

		Vector3 spawnAnchor = api.GetSpawnAnchor(playerIndex);
		if (spawnAnchor == Vector3.Zero)
		{
			player.ShowFeedbackText("This board declares no spawn anchor for your slot.", new Vector3(1f, 0.4f, 0.3f));
			return;
		}

		var anchor = new Vector2(spawnAnchor.X, spawnAnchor.Z);
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
		ApplyDraftAbilityState(api, playerIndex);

		player.PanCameraTo(new Vector3(anchor.X, 0f, anchor.Y), 0f);
		player.ShowFeedbackText($"{HeroFactionNames[heroTemplateIndex]} answers the call.", new Vector3(0.85f, 0.95f, 1f));
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
		ApplyDraftAbilityState(api, playerIndex);
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

	/// <summary>
	///     Burns every enemy the army has marked, for the burn's own rate rather than the tick's damage. Each tick's burn is
	///     smaller than a whole point of damage, and one hit is never worth less than the combat resolver's floor, so the
	///     burn is banked per victim and the whole points are dealt as they fill: a tick's worth paid on its own would be
	///     raised to the floor and the authored rate would arrive several times over. The damage is flagged so re-entering
	///     the damage callback does not re-mark its victim: a burn that refreshed itself each tick would never end, and the
	///     2 s burn is meant to run out once the hits stop.
	/// </summary>
	private void ApplyCinderMarks(IGameAPI api, PlayerId player, float delta)
	{
		if (delta <= 0f) return;
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

			int entityId = unit.UniqueId;
			float banked = _cinderMarkDamage[entityId] + CinderMarkDamagePerSecond * delta;
			float whole = MathF.Floor(banked);
			_cinderMarkDamage[entityId] = banked - whole;
			if (whole <= 0f) continue;

			_burningFromMark[entityId] = 1;
			api.DealTypedDamage(hero, unit, whole, "magic");
			_burningFromMark[entityId] = 0;
		}
	}

	/// <summary>
	///     Casts the soldier abilities the content set gives to an auto-cast rule and no player order: the Mender heals the
	///     most wounded ally beside it, the Warder shields the weakest one, and the Standard Bearer keeps its aura on the
	///     soldiers around it. The map keeps each caster's own clock, because these abilities are never triggered by a
	///     player and the authority has no order to spend a cooldown on.
	/// </summary>
	private void ApplyAutoCasts(IGameAPI api, PlayerId player, float delta)
	{
		if (delta <= 0f) return;

		ReadOnlySpan<UnitId> owned = player.GetUnits();
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId caster = owned[index];
			if (caster.IsDead || caster.IsBuilding || caster.IsHero) continue;

			string templateId = caster.TemplateID;
			if (templateId == MenderTemplateId)
			{
				UpdateMend(api, owned, caster, delta);
				continue;
			}

			if (templateId == WarderTemplateId)
			{
				UpdateWardingAegis(api, owned, caster, delta);
				continue;
			}

			if (templateId == StandardBearerTemplateId) UpdateStandardAura(api, owned, caster, delta);
		}
	}

	private void UpdateMend(IGameAPI api, ReadOnlySpan<UnitId> owned, UnitId caster, float delta)
	{
		int entityId = caster.UniqueId;
		_mendTimers[entityId] -= delta;
		if (_mendTimers[entityId] > 0f) return;

		_mendTimers[entityId] = 0f;
		if (!TryFindMostWoundedAlly(owned, caster, MendRadius, MendHealthFractionBelow, out UnitId patient)) return;

		_mendTimers[entityId] = MendCooldownSeconds;
		api.CastAbility(caster, MendAbilityId, patient.Position);
	}

	private void UpdateWardingAegis(IGameAPI api, ReadOnlySpan<UnitId> owned, UnitId caster, float delta)
	{
		int entityId = caster.UniqueId;
		_wardingAegisTimers[entityId] -= delta;
		if (_wardingAegisTimers[entityId] > 0f) return;

		_wardingAegisTimers[entityId] = 0f;
		if (!TryFindMostWoundedAlly(owned, caster, WardingAegisRadius, 1f, out UnitId ward)) return;

		_wardingAegisTimers[entityId] = WardingAegisCooldownSeconds;
		api.CastAbility(caster, WardingAegisAbilityId, ward.Position);
	}

	private void UpdateStandardAura(IGameAPI api, ReadOnlySpan<UnitId> owned, UnitId caster, float delta)
	{
		int entityId = caster.UniqueId;
		_standardAuraTimers[entityId] -= delta;
		if (_standardAuraTimers[entityId] > 0f) return;

		_standardAuraTimers[entityId] = 0f;
		if (!HasAllyWithoutAura(api, owned, caster)) return;

		_standardAuraTimers[entityId] = StandardAuraCooldownSeconds;
		api.CastAbility(caster, StandardAuraAbilityId, caster.Position);
	}

	private static bool TryFindMostWoundedAlly(ReadOnlySpan<UnitId> owned, UnitId caster, float radius, float healthFractionBelow, out UnitId worstWounded)
	{
		worstWounded = UnitId.Invalid;
		Vector3 casterPosition = caster.Position;
		float radiusSquared = radius * radius;
		float lowestFraction = healthFractionBelow;

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId ally = owned[index];
			if (ally.IsDead || ally.IsBuilding) continue;
			if (ally.UniqueId == caster.UniqueId) continue;
			if (Vector3.DistanceSquared(ally.Position, casterPosition) > radiusSquared) continue;

			float maximumHealth = ally.MaxHealth;
			if (maximumHealth <= 0f) continue;

			float fraction = ally.Health / maximumHealth;
			if (fraction >= lowestFraction) continue;

			lowestFraction = fraction;
			worstWounded = ally;
		}

		return worstWounded.IsValid;
	}

	private static bool HasAllyWithoutAura(IGameAPI api, ReadOnlySpan<UnitId> owned, UnitId caster)
	{
		Vector3 casterPosition = caster.Position;
		float radiusSquared = StandardAuraRadius * StandardAuraRadius;

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId ally = owned[index];
			if (ally.IsDead || ally.IsBuilding) continue;
			if (Vector3.DistanceSquared(ally.Position, casterPosition) > radiusSquared) continue;
			if (api.UnitHasBuff(ally, StandardAuraBuffId)) continue;

			return true;
		}

		return false;
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

		// The bulwark is restated every second and a modifier is appended rather than replaced, so the previous
		// statement is withdrawn first. Without that the hero would keep last second's soldiers as well as this one's.
		api.RemoveStatModifiers(hero, "bulwark/hero");
		api.AddStatModifier(hero, "Armor", false, nearbySoldiers * BulwarkArmorPerSoldier, "bulwark/hero");
		api.AddStatModifier(hero, "MagicResistance", false, nearbySoldiers * BulwarkMagicResistancePerSoldier, "bulwark/hero");

		int nexusEntityId = _nexusEntities[playerIndex];
		if (nexusEntityId <= 0) return;

		UnitId nexus = new(nexusEntityId);
		if (nexus.IsDead) return;

		api.RemoveStatModifiers(nexus, "bulwark/buildings");
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

		if (IsCinderMarkHero(attacker.Player) && !victim.IsBuilding && _burningFromMark[victim.UniqueId] == 0)
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
		AbandonOpenDraft(playerIndex);

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

		if (abilityId == ConstructionAbilityId)
		{
			BeginConstruction(playerIndex, position);
			return;
		}

		if (abilityId == AbandonConstructionAbilityId)
		{
			AbandonOpenDraft(playerIndex);
			return;
		}

		if (FindProfileIndexByBuilding(abilityId) >= 0)
		{
			LockDraftOption(playerIndex, abilityId);
			return;
		}

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

	private void BeginConstruction(int playerIndex, Vector3 position)
	{
		if (playerIndex <= 0 || playerIndex >= EcsSharedMemory.MAX_PLAYERS) return;

		PlayerId player = _api.GetPlayer(playerIndex);
		if (!player.IsActive) return;

		if (_draftOpen[playerIndex] != 0)
		{
			player.ShowFeedbackText("Choose from the buildings already offered.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		if (_api.GetPlayerBuildingCount(playerIndex) > MaximumBuildingsPerPlayer)
		{
			player.ShowFeedbackText($"All {MaximumBuildingsPerPlayer} building locations are taken.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		if (player.Wood < BuildingWoodCost)
		{
			player.ShowFeedbackText("A construction costs 100 wood.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		int optionCount = DrawDraftOptions(playerIndex);
		if (optionCount == 0)
		{
			player.ShowFeedbackText("No building is left to draw.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		player.AdjustWood(-BuildingWoodCost);
		_draftOpen[playerIndex] = 1;
		_draftOptionCounts[playerIndex] = optionCount;
		_draftSitePositionsX[playerIndex] = position.X;
		_draftSitePositionsZ[playerIndex] = position.Z;

		for (int index = 0; index < optionCount; index++)
		{
			string templateId = SoldierProfiles[_draftOptions[playerIndex * DraftOptionCount + index]].BuildingTemplateId;
			_api.SetAbilityGridPosition(templateId, index, DraftRow);
			_api.SetAbilityHotkey(templateId, DraftHotkeys[index]);
		}

		ApplyDraftAbilityState(_api, playerIndex);

		player.ShowFeedbackText("Three buildings are offered. Pick one from the command grid, or abandon the construction.", new Vector3(0.85f, 0.9f, 0.6f));
		_api.SpawnExpandingGroundRing(new Vector3(position.X, 0f, position.Z), 3f, 6f, 0.6f, new Vector3(0.9f, 0.8f, 0.4f));
	}

	/// <summary>
	///     Draws the buildings this player may be offered: the shared deck minus what another player already took, minus
	///     what this player already stands on, which is the unique-copy rule the content set is balanced against. A deck
	///     with nothing left is restored first, so a long match never stalls for lack of options.
	/// </summary>
	private int DrawDraftOptions(int playerIndex)
	{
		Span<int> candidates = stackalloc int[SoldierProfiles.Length];
		int candidateCount = CollectDraftCandidates(playerIndex, candidates);
		if (candidateCount == 0)
		{
			_deckTakenBuildings = 0;
			candidateCount = CollectDraftCandidates(playerIndex, candidates);
		}

		int optionCount = Math.Min(DraftOptionCount, candidateCount);
		int optionStart = playerIndex * DraftOptionCount;
		for (int index = 0; index < optionCount; index++)
		{
			int pick = _api.RandomInt(0, candidateCount - 1);
			_draftOptions[optionStart + index] = candidates[pick];
			candidates[pick] = candidates[candidateCount - 1];
			candidateCount--;
		}

		return optionCount;
	}

	private int CollectDraftCandidates(int playerIndex, Span<int> destination)
	{
		ReadOnlySpan<UnitId> owned = _api.GetUnitsOwnedByPlayer(playerIndex);
		int count = 0;
		for (int profileIndex = 0; profileIndex < SoldierProfiles.Length; profileIndex++)
		{
			if ((_deckTakenBuildings & (1 << profileIndex)) != 0) continue;
			if (PlayerOwnsBuilding(owned, SoldierProfiles[profileIndex].BuildingTemplateId)) continue;

			destination[count++] = profileIndex;
		}

		return count;
	}

	private static bool PlayerOwnsBuilding(ReadOnlySpan<UnitId> owned, string templateId)
	{
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead || !unit.IsBuilding) continue;
			if (unit.TemplateID == templateId) return true;
		}

		return false;
	}

	/// <summary>
	///     Declares the abilities the draft is played through. Each drafted building is an ability whose identifier is the
	///     building template itself, so a cast names the package the player picked without a second table to keep in step,
	///     and every hero and Nexus can offer them in its command grid. All of them are hidden until a draw offers them.
	/// </summary>
	private void RegisterDraftAbilities(IGameAPI api)
	{
		for (int index = 0; index < SoldierProfiles.Length; index++)
		{
			SoldierProfile profile = SoldierProfiles[index];
			string buildingName = api.GetUnitName(profile.BuildingTemplateId);
			string soldierName = api.GetUnitName(profile.SoldierTemplateId);
			string tooltip = $"Draft {buildingName} for 100 wood. Raises {soldierName}: {profile.Capacity} capacity, {profile.MaximumCharges} charges every {profile.RechargeSeconds:0}s. Income {DescribeBuildingIncome(profile.BuildingTemplateId)}.";

			api.RegisterAbility(profile.BuildingTemplateId, buildingName, tooltip, string.Empty, true);
			AddDraftAbilityToCommandUnits(api, profile.BuildingTemplateId);
			api.SetAbilityTooltip(profile.BuildingTemplateId, tooltip);
		}

		api.RegisterAbility(AbandonConstructionAbilityId, "Abandon Construction", "Gives back the 100 wood the open construction reserved.", string.Empty, true);
		api.SetAbilityGridPosition(AbandonConstructionAbilityId, DraftAbandonSlot, DraftRow);
		api.SetAbilityHotkey(AbandonConstructionAbilityId, "V");
		AddDraftAbilityToCommandUnits(api, AbandonConstructionAbilityId);
	}

	private static void AddDraftAbilityToCommandUnits(IGameAPI api, string abilityId)
	{
		api.AddUnitTypeAbility(NexusTemplateId, abilityId);

		for (int index = 0; index < HeroTemplateIds.Length; index++)
		{
			api.AddUnitTypeAbility(HeroTemplateIds[index], abilityId);
		}
	}

	/// <summary>
	///     Shows the draft on the units that carry the command grid: the hero that began the construction and the Nexus it
	///     was begun from. Only the drawn packages and the abandon command are visible, so a player sees the three options
	///     the deck offered and nothing else.
	/// </summary>
	private void ApplyDraftAbilityState(IGameAPI api, int playerIndex)
	{
		if (playerIndex <= 0 || playerIndex >= EcsSharedMemory.MAX_PLAYERS) return;

		for (int index = 0; index < SoldierProfiles.Length; index++)
		{
			string abilityId = SoldierProfiles[index].BuildingTemplateId;
			bool visible = IsDraftOption(playerIndex, index);
			SetDraftAbilityVisible(api, playerIndex, abilityId, visible);
		}

		SetDraftAbilityVisible(api, playerIndex, AbandonConstructionAbilityId, _draftOpen[playerIndex] != 0);
	}

	private void SetDraftAbilityVisible(IGameAPI api, int playerIndex, string abilityId, bool visible)
	{
		int heroEntityId = _heroEntities[playerIndex];
		if (heroEntityId > 0) api.SetAbilityState(new UnitId(heroEntityId), abilityId, !visible, !visible);

		int nexusEntityId = _nexusEntities[playerIndex];
		if (nexusEntityId > 0) api.SetAbilityState(new UnitId(nexusEntityId), abilityId, !visible, !visible);
	}

	private bool IsDraftOption(int playerIndex, int profileIndex)
	{
		if (playerIndex <= 0 || playerIndex >= EcsSharedMemory.MAX_PLAYERS) return false;
		if (_draftOpen[playerIndex] == 0) return false;

		int optionCount = _draftOptionCounts[playerIndex];
		for (int index = 0; index < optionCount; index++)
		{
			if (_draftOptions[playerIndex * DraftOptionCount + index] == profileIndex) return true;
		}

		return false;
	}

	/// <summary>
	///     Locks the building a player cast from its command grid. The deck entry leaves the shared deck here rather than
	///     when it was drawn, so an option the player declined stays available to everybody else.
	/// </summary>
	private void LockDraftOption(int playerIndex, string buildingTemplateId)
	{
		if (playerIndex <= 0 || playerIndex >= EcsSharedMemory.MAX_PLAYERS) return;

		PlayerId player = _api.GetPlayer(playerIndex);
		int profileIndex = FindProfileIndexByBuilding(buildingTemplateId);

		// A building the draw did not offer is not a pick: the command only means something while that package is one of
		// the three this player is being offered, so a cast of any other building is refused without touching the draft.
		if (_draftOpen[playerIndex] == 0 || !IsDraftOption(playerIndex, profileIndex))
		{
			player.ShowFeedbackText("That building is not part of this construction.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		_draftOpen[playerIndex] = 0;
		_draftOptionCounts[playerIndex] = 0;

		var position = new Vector3(_draftSitePositionsX[playerIndex], 0f, _draftSitePositionsZ[playerIndex]);
		UnitId site = _api.SpawnBuildingUnderConstruction(buildingTemplateId, position, playerIndex, ConstructionSecondsByProfile[profileIndex]);
		if (!site.IsValid)
		{
			player.AdjustWood(BuildingWoodCost);
			player.ShowFeedbackText("The site could not be placed there.", new Vector3(1f, 0.3f, 0.25f));
			ApplyDraftAbilityState(_api, playerIndex);
			return;
		}

		// The package leaves the shared deck only once its building actually stands, so a placement the map had to
		// refuse does not cost the deck one of its nine buildings for the rest of the match.
		_deckTakenBuildings |= 1 << profileIndex;

		ApplyDraftAbilityState(_api, playerIndex);

		player.ShowFeedbackText($"{_api.GetUnitName(buildingTemplateId)} is under construction.", new Vector3(0.85f, 0.9f, 0.6f));
		player.PlayClickSound();
	}

	/// <summary>
	///     Gives back what an unanswered draft reserved: the deck entries return because nothing was ever taken from the
	///     deck, and the 100 wood returns because it was charged when the draw was made.
	/// </summary>
	private void AbandonOpenDraft(int playerIndex)
	{
		if (playerIndex <= 0 || playerIndex >= EcsSharedMemory.MAX_PLAYERS) return;
		if (_draftOpen[playerIndex] == 0) return;

		_draftOpen[playerIndex] = 0;
		_draftOptionCounts[playerIndex] = 0;
		ApplyDraftAbilityState(_api, playerIndex);

		PlayerId player = _api.GetPlayer(playerIndex);
		if (!player.IsActive) return;

		player.AdjustWood(BuildingWoodCost);
		player.ShowFeedbackText("Construction cancelled. Its 100 wood is returned.", new Vector3(0.75f, 0.75f, 0.75f));
	}

	/// <summary>
	///     Advances the sites this match started through the draft. Nothing is working them, so the map is the clock that
	///     builds them; the engine still announces each completion through OnConstructionFinished, where the charges start.
	/// </summary>
	private void AdvanceOwnConstruction(IGameAPI api, PlayerId player, float delta)
	{
		if (delta <= 0f) return;

		ReadOnlySpan<UnitId> owned = player.GetUnits();
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId building = owned[index];
			if (building.IsDead || !building.IsBuilding) continue;
			if (api.GetUnitConstructionProgress(building) >= 1f) continue;

			api.AdvanceUnitConstruction(building, delta);
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
		int profileIndex = FindProfileIndexByBuilding(templateId);
		return profileIndex < 0 ? 30f : ConstructionSecondsByProfile[profileIndex];
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

	private static string DescribeBuildingIncome(string buildingTemplateId)
	{
		BuildingProfile building = FindBuildingProfile(buildingTemplateId);
		if (building.GoldPerMinute > 0f) return $"{building.GoldPerMinute:0.#} gold/min";
		if (building.WoodPerMinute > 0f) return $"{building.WoodPerMinute:0.#} wood/min";

		return "none";
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
