namespace Realm.Maps;

using System;
using System.Numerics;
using Realm.Ecs;
using Realm.MapAPI;

public partial class MapScript : IWasmModule, ILobbyScript
{
	private const string NexusTemplateId = "building/nexus";
	private const string GoldNodeTemplateId = "resource/melee_gold_mine";
	private const string WoodNodeTemplateId = "resource/melee_woodland";

	private const int DefaultMaximumBuildingsPerPlayer = 8;
	private const int DefaultMaximumBuildingCopiesPerPlayer = 1;
	private const int MaximumBuildingLimit = 24;
	private const string ConstructionAbilityId = "ability/begin_construction";
	private const string AbandonConstructionAbilityId = "ability/abandon_construction";
	private const string BlueprintTemplateId = "unit/blueprint";
	private const string WorkerTemplateId = "unit/worker";
	private const float BuildingWoodCost = 100f;
	private const int ManaPerProduction = 100;
	private const int DefaultDraftOptionCount = 3;
	private const int MaximumDraftOptions = 24;
	private const int DraftRow = 2;
	private const int DraftAbandonSlot = 3;
	private const int MaximumPopulation = 8;
	private const float StartingGold = 200f;
	private const float StartingWood = 100f;
	private const float UnderAttackAlertCooldownSeconds = 8f;
	private const float BaseRevivalSeconds = 12f;
	private const float RevivalSecondsPerDeath = 3f;
	private const float RevivalSecondsCap = 45f;
	// A worker gathers with its attack: it hits a node as it would hit a unit, and every point of post-mitigation damage
	// that lands is one piece of the node's item. How often it hits is its own attack cooldown, so a buff to attack raises
	// income and a buff to movement shortens the walk. Neither number is estimated by the ruleset.
	private const float WorkerCheckSeconds = 1f;
	private const float WorkerSearchRadius = 400f;
	private const float PermanentBuffSeconds = 10_000_000f;

	private const string WorkerHallBuffId = "buff/worker_hall";
	private const string WorkerYardBuffId = "buff/worker_yard";
	private const string WorkerLodgeBuffId = "buff/worker_lodge";
	private const string WorkerRangeBuffId = "buff/worker_range";
	private const string WorkerFoundryBuffId = "buff/worker_foundry";
	private const string WorkerSlingBuffId = "buff/worker_sling";
	private const string WorkerShrineBuffId = "buff/worker_shrine";
	private const string WorkerSignalBuffId = "buff/worker_signal";
	private const string WorkerBastionBuffId = "buff/worker_bastion";
	private const string DeepGranariesWorkerBuffId = "buff/deep_granaries_worker";
	private const float CinderMarkDamagePerSecond = 6f;
	private const float CinderfieldRadius = 5f;
	private const float CinderfieldDamagePerSecond = 25f;
	private const float CinderfieldTickSeconds = 0.5f;
	private const float CinderfieldBaseDurationSeconds = 4f;
	private const float RequisitionBaseCompletionFraction = 0.3f;
	private const float RequisitionMaximumCompletionFraction = 0.5f;
	private const float BulwarkRecalculateSeconds = 1f;
	private const float BulwarkSoldierRadius = 10f;
	private const float BulwarkAttackerRadius = 12f;
	private const float BulwarkArmorPerSoldier = 0.6f;
	private const float BulwarkMagicResistancePerSoldier = 0.6f;
	private const float BulwarkArmorPerBuilding = 1.5f;
	private const float BulwarkMagicResistancePerBuilding = 1.5f;
	private const float BulwarkPenetrationPerAttacker = 0.1f;
	private const float BulwarkNexusCoefficient = 1f;
	private const float HeroRepairHealthPercentPerSecond = 0.03f;
	private const string BulwarkBuffId = "buff/bulwark";
	private const string WorkerCrewBuffIcon = "res://Assets/UI/golden_hammers.webp";

	private const float RelicCost = 100f;
	private const int RelicSlotCount = 6;
	private const float FormationFrontDistance = 3.4f;
	private const float FormationFlankDistance = 3.0f;
	private const float FormationBackDistance = 3.4f;
	private const float FormationRowSpacing = 1.7f;
	private const float FormationColumnSpacing = 2.1f;
	private const float FormationSlotTolerance = 1.5f;
	private const float FormationLeashDistance = 24f;
	private const float FormationOrderIntervalSeconds = 0.45f;
	private const float EngageLeashDistance = 18f;
	private const float StandardTargetMemorySeconds = 4f;
	private const float TargetRetrySeconds = 0.6f;
	private const int FormationFrontSize = 3;
	private const int FormationFlankSize = 2;

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

	private enum SoldierFormation
	{
		Front,
		Flank,
		Back
	}

	private enum SoldierTargetingRule
	{
		Standard,
		TankShredder,
		Assassin
	}

	private readonly record struct SoldierProfile(
		string SoldierTemplateId,
		string BuildingTemplateId,
		int Capacity,
		int StoredProductions,
		float ProductionSeconds,
		float AcquisitionRange,
		SoldierFormation Formation,
		SoldierTargetingRule TargetingRule);

	private readonly record struct BuildingProfile(
		string TemplateId,
		string SoldierAbilityId,
		string WorkerAbilityId,
		float ProductionSeconds,
		int WorkerCapacity,
		int WorkerProductions,
		float WorkerProductionSeconds,
		string WorkerResourceType,
		string[] WorkerBuffIds,
		string WorkerBuffSummary);

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

	private static readonly string[] RelicNames =
	{
		"Sigil of the Wall",
		"Courier's Knot",
		"Spiked Barricades",
		"Deep Granaries",
		"Warden's Writ",
		"Muster Bell"
	};

	private static readonly string[] RelicEffects =
	{
		"Front-formation soldiers gain 10% maximum health.",
		"All recharge is 10% faster.",
		"Owned buildings return 25% of the damage they take.",
		"Workers gain 10% attack.",
		"All owned units suffer 10% shorter debuff durations.",
		"Soldier recharge is 10% faster."
	};

	private static readonly string[] RelicHotkeys = { "1", "2", "3", "4", "5", "6" };

	private static readonly SoldierProfile[] SoldierProfiles =
	{
		new("unit/shieldbearer", "building/bulwark_hall", 6, 3, 22f, 12f, SoldierFormation.Front, SoldierTargetingRule.Standard),
		new("unit/axeman", "building/warband_yard", 6, 3, 20f, 12f, SoldierFormation.Flank, SoldierTargetingRule.Standard),
		new("unit/spear_militia", "building/spear_lodge", 5, 3, 24f, 12f, SoldierFormation.Front, SoldierTargetingRule.TankShredder),
		new("unit/bowman", "building/archery_range", 8, 4, 18f, 12f, SoldierFormation.Back, SoldierTargetingRule.Assassin),
		new("unit/slinger", "building/sling_camp", 8, 4, 16f, 12f, SoldierFormation.Flank, SoldierTargetingRule.Standard),
		new("unit/bolt_thrower", "building/bolt_foundry", 2, 1, 30f, 14f, SoldierFormation.Back, SoldierTargetingRule.TankShredder),
		new("unit/mender", "building/mender_shrine", 4, 2, 26f, 12f, SoldierFormation.Back, SoldierTargetingRule.Standard),
		new("unit/standard_bearer", "building/signal_post", 3, 2, 28f, 12f, SoldierFormation.Flank, SoldierTargetingRule.Standard),
		new("unit/warder", "building/vigil_bastion", 3, 2, 26f, 12f, SoldierFormation.Front, SoldierTargetingRule.Standard)
	};

	private static readonly string[] HallWorkerBuffs = { WorkerHallBuffId };
	private static readonly string[] YardWorkerBuffs = { WorkerYardBuffId };
	private static readonly string[] LodgeWorkerBuffs = { WorkerLodgeBuffId };
	private static readonly string[] RangeWorkerBuffs = { WorkerRangeBuffId };
	private static readonly string[] FoundryWorkerBuffs = { WorkerFoundryBuffId };
	private static readonly string[] SlingWorkerBuffs = { WorkerSlingBuffId };
	private static readonly string[] ShrineWorkerBuffs = { WorkerShrineBuffId };
	private static readonly string[] SignalWorkerBuffs = { WorkerSignalBuffId };
	private static readonly string[] BastionWorkerBuffs = { WorkerBastionBuffId };

	private static readonly BuildingProfile[] BuildingProfiles =
	{
		new("building/bulwark_hall", "ability/raise_shieldbearer", "ability/raise_worker_bulwark_hall", 22f, 2, 1, 45f, "gold", HallWorkerBuffs, "+2 armor, +5% health"),
		new("building/warband_yard", "ability/raise_axeman", "ability/raise_worker_warband_yard", 20f, 2, 2, 30f, "gold", YardWorkerBuffs, "+20% attack"),
		new("building/spear_lodge", "ability/raise_spear_militia", "ability/raise_worker_spear_lodge", 24f, 2, 1, 35f, "gold", LodgeWorkerBuffs, "+10% move speed"),
		new("building/archery_range", "ability/raise_bowman", "ability/raise_worker_archery_range", 18f, 3, 2, 25f, "wood", RangeWorkerBuffs, "+15% attack"),
		new("building/bolt_foundry", "ability/raise_bolt_thrower", "ability/raise_worker_bolt_foundry", 30f, 1, 1, 50f, "wood", FoundryWorkerBuffs, "+40% attack, -10% move speed"),
		new("building/sling_camp", "ability/raise_slinger", "ability/raise_worker_sling_camp", 16f, 3, 3, 20f, "wood", SlingWorkerBuffs, "+10% attack"),
		new("building/mender_shrine", "ability/raise_mender", "ability/raise_worker_mender_shrine", 26f, 1, 1, 40f, "gold", ShrineWorkerBuffs, "+1 armor, +15% move speed"),
		new("building/signal_post", "ability/raise_standard_bearer", "ability/raise_worker_signal_post", 28f, 2, 1, 30f, "gold", SignalWorkerBuffs, "+25% attack"),
		new("building/vigil_bastion", "ability/raise_warder", "ability/raise_worker_vigil_bastion", 26f, 2, 1, 35f, "gold", BastionWorkerBuffs, "+2 armor, +10% health")
	};

	private readonly int[] _statedManaMax = new int[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _statedManaRegen = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly int[] _lastBadgeValues = new int[EcsSharedMemory.MAX_ENTITIES];
	private readonly int[] _soldierCounts = new int[SoldierProfiles.Length];
	private readonly int[] _capacityCounts = new int[SoldierProfiles.Length];

	private static readonly float[] ConstructionSecondsByProfile = { 30f, 30f, 30f, 30f, 30f, 30f, 35f, 35f, 35f };

	private readonly byte[] _draftOpen = new byte[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _draftOptionCounts = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _draftOptions = new int[EcsSharedMemory.MAX_PLAYERS * MaximumDraftOptions];
	private readonly int[] _draftSiteEntities = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _draftSiteRetirements = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _draftSitePositionsX = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _draftSitePositionsZ = new float[EcsSharedMemory.MAX_PLAYERS];
	private int _deckTakenBuildings;

	// The parameters the lobby's host set for this match. Each one starts at the value the ruleset itself states and is
	// replaced only when the host configured it, so a lobby that touched nothing plays the authored ruleset.
	private int _maximumBuildingsPerPlayer = DefaultMaximumBuildingsPerPlayer;
	private int _maximumBuildingCopiesPerPlayer = DefaultMaximumBuildingCopiesPerPlayer;
	private int _draftOptionsPerDraw = DefaultDraftOptionCount;
	private float _capacityMultiplier = 1f;
	private float _rechargeMultiplier = 1f;
	private bool _mirroredDraft;
	private bool _openPoolDrafting;

	private readonly int[] _heroEntities = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _nexusEntities = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _heroDeaths = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _heroTemplateIndices = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _reviveAtTimes = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _heroExperiences = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _heroLevelsBeforeDeath = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _heroFacing = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _heroLastPositionsX = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _heroLastPositionsZ = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _heroRecentTargets = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _heroRecentTargetTimes = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _assignedTargets = new int[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _nextTargetTimes = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _nextAttackRefreshTimes = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _nextFormationOrderTimes = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly byte[] _soldierControlled = new byte[EcsSharedMemory.MAX_ENTITIES];
	private readonly int[] _sourceBuildings = new int[EcsSharedMemory.MAX_ENTITIES];
	private readonly int[] _publishedMaintainedSoldiers = new int[EcsSharedMemory.MAX_ENTITIES];
	private readonly ulong[] _bulwarkStates = new ulong[EcsSharedMemory.MAX_PLAYERS];
	private readonly ulong[] _buildingBulwarkStates = new ulong[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _lastAlertTimes = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly byte[] _relicFlags = new byte[EcsSharedMemory.MAX_PLAYERS];
	private readonly byte[] _burningFromMark = new byte[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _cinderMarkDamage = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly byte[] _cinderfieldActive = new byte[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _cinderfieldCasters = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly int[] _cinderfieldOwners = new int[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _cinderfieldCentersX = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _cinderfieldCentersZ = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _cinderfieldRemainingSeconds = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _cinderfieldDamageTimers = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _mendTimers = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _wardingAegisTimers = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly float[] _standardAuraTimers = new float[EcsSharedMemory.MAX_ENTITIES];
	private readonly byte[] _eliminated = new byte[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _nexusPositionsX = new float[EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _nexusPositionsZ = new float[EcsSharedMemory.MAX_PLAYERS];

	private IGameAPI _api = null!;
	private readonly int[] _workerBuildingOf = new int[EcsSharedMemory.MAX_ENTITIES];
	private readonly int[] _workerNodeOf = new int[EcsSharedMemory.MAX_ENTITIES];
	private readonly byte[] _workerResourceOf = new byte[EcsSharedMemory.MAX_ENTITIES];
	private readonly byte[] _workerPausedOf = new byte[EcsSharedMemory.MAX_ENTITIES];
	private readonly int[] _buildingConstructionOrder = new int[EcsSharedMemory.MAX_ENTITIES];
	private readonly int[] _playerConstructionCount = new int[EcsSharedMemory.MAX_PLAYERS];
	private float _workerCheckTimer;
	private float _bulwarkTimer;

	public void Initialize(IGameAPI api)
	{
		_api = api;
		ReadMatchOptions(api);

		api.SetUnitBarVisibility(true, true, true);
		api.RegisterPrimaryBuilding(NexusTemplateId);
		api.RegisterResourceDropoffBuilding(NexusTemplateId);
		api.RegisterResourceNodeType(GoldNodeTemplateId, "gold");
		api.RegisterResourceNodeType(WoodNodeTemplateId, "wood");

		for (int index = 0; index < HeroTemplateIds.Length; index++)
		{
			api.RegisterResourceWorkerUnit(HeroTemplateIds[index]);
		}

		api.RegisterBuffModifier("buff/grounding_shout", "MovementSpeed", true, -0.25f);
		api.RegisterBuffModifier("buff/hold_fast", "Armor", false, 4f);
		api.RegisterBuffModifier("buff/forced_march", "MovementSpeed", true, 0.2f);
		api.RegisterBuffModifier("buff/standard_aura", "MovementSpeed", true, 0.1f);
		api.RegisterBuffModifier("buff/cinder_mark", "HpRegen", false, 0f);

		api.RegisterBuffModifier(WorkerHallBuffId, "Armor", false, 2f);
		api.RegisterBuffModifier(WorkerHallBuffId, "MaxHp", true, 0.05f);
		api.RegisterBuffModifier(WorkerYardBuffId, "Attack", true, 0.2f);
		api.RegisterBuffModifier(WorkerLodgeBuffId, "MovementSpeed", true, 0.1f);
		api.RegisterBuffModifier(WorkerRangeBuffId, "Attack", true, 0.15f);
		api.RegisterBuffModifier(WorkerFoundryBuffId, "Attack", true, 0.4f);
		api.RegisterBuffModifier(WorkerFoundryBuffId, "MovementSpeed", true, -0.1f);
		api.RegisterBuffModifier(WorkerSlingBuffId, "Attack", true, 0.1f);
		api.RegisterBuffModifier(WorkerShrineBuffId, "Armor", false, 1f);
		api.RegisterBuffModifier(WorkerShrineBuffId, "MovementSpeed", true, 0.15f);
		api.RegisterBuffModifier(WorkerSignalBuffId, "Attack", true, 0.25f);
		api.RegisterBuffModifier(WorkerBastionBuffId, "Armor", false, 2f);
		api.RegisterBuffModifier(WorkerBastionBuffId, "MaxHp", true, 0.1f);
		api.RegisterBuffModifier(DeepGranariesWorkerBuffId, "Attack", true, 0.1f);

		for (int index = 0; index < _lastAlertTimes.Length; index++)
		{
			_lastAlertTimes[index] = -UnderAttackAlertCooldownSeconds;
		}

		api.OnUnitDied += HandleUnitDied;
		api.OnUnitDamaged += HandleUnitDamaged;
		api.OnSpellCast += HandleSpellCast;
		api.OnConstructionFinished += HandleConstructionFinished;

		RegisterDraftAbilities(api);
		RegisterRelicVendor(api);
		InitializeDamageAffinity(api);
		RegisterInstructions(api);
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
		RetireDraftSites(api);
		ReadOnlySpan<PlayerId> players = api.GetActivePlayers();

		for (int index = 0; index < players.Length; index++)
		{
			PlayerId player = players[index];
			if (player.Value == 0 || !player.IsActive) continue;

			UpdateArmyLevel(api, player);
			UpdateRevival(api, player);
			UpdateProduction(api, player);
			UpdateHeroMemory(api, player);
			UpdateArmyBehaviour(api, player);
			ApplyAutoCasts(api, player, delta);
		}

		MaintainWorkers(api, players, delta);
		UpdateCinderfields(api, delta);
		UpdateDamageAffinity(api, delta);

		_bulwarkTimer += delta;
		bool recalculateBulwark = _bulwarkTimer >= BulwarkRecalculateSeconds;
		if (recalculateBulwark) _bulwarkTimer = 0f;

		for (int index = 0; index < players.Length; index++)
		{
			PlayerId player = players[index];
			if (player.Value == 0 || !player.IsActive) continue;

			RefreshRelics(api, player);
			if (recalculateBulwark) RefreshBulwark(api, player);
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
		api.SetPlayerBuildingLimit(playerIndex, _maximumBuildingsPerPlayer + 1);

		Vector3 spawnAnchor = api.GetSpawnAnchor(playerIndex);
		if (spawnAnchor == Vector3.Zero)
		{
			player.ShowFeedbackText("This board declares no spawn anchor for your slot.", new Vector3(1f, 0.4f, 0.3f));
			return;
		}

		var anchor = new Vector2(spawnAnchor.X, spawnAnchor.Z);
		_nexusPositionsX[playerIndex] = anchor.X;
		_nexusPositionsZ[playerIndex] = anchor.Y;

		// The hero's template decides the side's innate, and the innate applies to every owned unit, the Nexus
		// included. It is resolved before anything is spawned so the starting structure never misses it.
		int heroTemplateIndex = ResolveHeroTemplateIndex(api.GetPlayerFaction(playerIndex));
		_heroTemplateIndices[playerIndex] = heroTemplateIndex;

		UnitId nexus = player.SpawnUnit(NexusTemplateId, anchor, false, true);
		if (nexus.IsValid)
		{
			_nexusEntities[playerIndex] = nexus.UniqueId;
			ApplyInnateToUnit(api, playerIndex, nexus);
		}

		var heroPosition = new Vector2(anchor.X + 4f, anchor.Y + 4f);
		UnitId hero = player.SpawnUnit(HeroTemplateIds[heroTemplateIndex], heroPosition, true, true);
		if (!hero.IsValid) return;

		_heroEntities[playerIndex] = hero.UniqueId;
		_heroLastPositionsX[playerIndex] = hero.Position.X;
		_heroLastPositionsZ[playerIndex] = hero.Position.Z;
		_bulwarkStates[playerIndex] = ulong.MaxValue;
		api.SetUnitLevel(hero, 1);
		api.SetUnitRepairRate(hero, HeroRepairHealthPercentPerSecond);
		ApplyInnateToUnit(api, playerIndex, hero);
		ApplyDraftAbilityState(api, playerIndex);
		ResetUnitRuntimeState(hero.UniqueId);
		ApplyRelicVendorState(api, player);

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

			// A worker's level is the construction order of the building that raised it, so the army's growth never
			// reaches it: a later building is how a player raises a stronger worker.
			if (_workerBuildingOf[unit.UniqueId] > 0) continue;
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
		_heroLastPositionsX[playerIndex] = hero.Position.X;
		_heroLastPositionsZ[playerIndex] = hero.Position.Z;
		_bulwarkStates[playerIndex] = ulong.MaxValue;
		int retainedLevel = Math.Max(1, _heroLevelsBeforeDeath[playerIndex]);
		api.SetUnitLevel(hero, retainedLevel);
		api.SetUnitExperience(hero, _heroExperiences[playerIndex]);
		api.SetUnitRepairRate(hero, HeroRepairHealthPercentPerSecond);
		ApplyInnateToUnit(api, playerIndex, hero);
		RestoreRelicsToHero(api, playerIndex, hero);
		ApplyRelicsToUnit(api, playerIndex, hero);
		ApplyDraftAbilityState(api, playerIndex);
		RefillProductionForPlayer(api, player);
		ResetUnitRuntimeState(hero.UniqueId);
		ApplyRelicVendorState(api, player);

		player.PanCameraTo(hero.Position, 0.6f);
		player.ShowFeedbackText("Your hero returns to the field.", new Vector3(0.6f, 1f, 0.7f));
	}

	/// <summary>
	///     Keeps every building's production honest without raising anything itself: it states the building's mana pool
	///     from the profile the site was raised with, publishes the casts the building could pay for, and disables the
	///     two production abilities while what they raise has no room left. The engine's auto-cast is what casts them.
	/// </summary>
	private void UpdateProduction(IGameAPI api, PlayerId player)
	{
		int playerIndex = player.Value;
		bool heroAlive = _heroEntities[playerIndex] > 0 && !new UnitId(_heroEntities[playerIndex]).IsDead;

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
				if (grantedProfile >= 0) _capacityCounts[grantedProfile] += ScaledCapacity(grantedProfile);
				continue;
			}

			int soldierProfile = FindProfileIndexBySoldier(unit.TemplateID);
			if (soldierProfile >= 0) _soldierCounts[soldierProfile]++;
		}

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId building = owned[index];
			if (building.IsDead || !building.IsBuilding) continue;

			BuildingProfile profile = FindBuildingProfile(building.TemplateID);
			if (string.IsNullOrEmpty(profile.TemplateId)) continue;

			if (building.IsDormant || !IsBuildingOperational(api, building))
			{
				StopBuildingProduction(api, building, profile);
				continue;
			}

			StateBuildingMana(api, playerIndex, building, profile);
			PublishProductionBadge(api, player, building, AvailableProductions(building));

			int profileIndex = FindProfileIndexByBuilding(building.TemplateID);
			PublishMaintainedSoldiers(api, owned, building, profileIndex);
			bool soldierRoom = _soldierCounts[profileIndex] < _capacityCounts[profileIndex];
			bool workerRoom = CountWorkersOfBuilding(owned, building.UniqueId) < profile.WorkerCapacity;

			api.SetAbilityState(building, profile.SoldierAbilityId, disabled: !heroAlive || !soldierRoom, hidden: false);
			api.SetAbilityState(building, profile.WorkerAbilityId, disabled: !heroAlive || !workerRoom, hidden: false);
		}
	}

	/// <summary>
	///     Publishes the soldiers a building maintains against the capacity it contributes, so a player reads a building's
	///     share of the army cap on the building itself instead of counting units. The count is the living soldiers this
	///     building raised, which is what its construction order and its level buffs belong to. The reading is written only
	///     when it changes, because a panel refreshes every tick and building the same string every tick would be wasted work.
	/// </summary>
	private void PublishMaintainedSoldiers(IGameAPI api, ReadOnlySpan<UnitId> owned, UnitId building, int profileIndex)
	{
		int maintained = 0;
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead || unit.IsBuilding) continue;
			if (_sourceBuildings[unit.UniqueId] != building.UniqueId) continue;
			if (FindProfileIndexBySoldier(unit.TemplateID) != profileIndex) continue;
			maintained++;
		}

		int capacity = ScaledCapacity(profileIndex);
		int published = maintained * 1024 + capacity;
		if (_publishedMaintainedSoldiers[building.UniqueId] == published) return;

		_publishedMaintainedSoldiers[building.UniqueId] = published;
		api.SetUnitTooltipText(building, "production.capacity", $"{maintained} / {capacity}");
	}

	/// <summary>
	///     Switches a dormant or unfinished building's production off and stops its mana pool from regenerating. A
	///     destroyed building keeps the mana it already held, but nothing refills it until the building is whole again.
	/// </summary>
	/// <param name="api">The game API reference to change the building's state.</param>
	/// <param name="building">The building that cannot produce.</param>
	/// <param name="profile">The production profile the building was raised with.</param>
	private void StopBuildingProduction(IGameAPI api, UnitId building, BuildingProfile profile)
	{
		api.SetAbilityState(building, profile.SoldierAbilityId, true, false);
		api.SetAbilityState(building, profile.WorkerAbilityId, true, false);

		int entityId = building.UniqueId;
		if (_statedManaRegen[entityId] == 0f) return;

		api.SetUnitManaRegen(building, 0f);
		_statedManaRegen[entityId] = 0f;
	}

	/// <summary>
	///     States the mana pool a building's two production abilities draw from. The pool holds one production worth of mana
	///     for every production the building's soldier line and worker line would each have held, and it refills at the sum of
	///     the two lines' own recharge rates, so the same number of soldiers and workers arrive over a match as before.
	/// </summary>
	private void StateBuildingMana(IGameAPI api, int playerIndex, UnitId building, BuildingProfile profile)
	{
		int profileIndex = FindProfileIndexByBuilding(building.TemplateID);
		int maximum = MaximumProductionMana(playerIndex, profileIndex, profile);
		float soldierRecharge = profile.ProductionSeconds * ResolveRechargeMultiplier(playerIndex);
		float workerRecharge = profile.WorkerProductionSeconds * ResolveWorkerRechargeMultiplier(playerIndex);
		float regen = (ManaPerProduction / soldierRecharge) + (ManaPerProduction / workerRecharge);

		int entityId = building.UniqueId;
		if (_statedManaMax[entityId] != maximum || MathF.Abs(building.MaxMana - maximum) > 0.5f)
		{
			api.SetUnitMaxMana(building, maximum);
			_statedManaMax[entityId] = maximum;
		}

		if (MathF.Abs(_statedManaRegen[entityId] - regen) > 0.0001f)
		{
			api.SetUnitManaRegen(building, regen);
			_statedManaRegen[entityId] = regen;
		}
	}

	private int MaximumProductionMana(int playerIndex, int profileIndex, BuildingProfile profile)
	{
		int extraProductions = IsStandingLedgerHero(playerIndex) ? 1 : 0;
		return (ScaledStoredProductions(profileIndex) + extraProductions + profile.WorkerProductions) * ManaPerProduction;
	}

	private static int AvailableProductions(UnitId building) => (int)MathF.Floor(building.Mana / ManaPerProduction);

	private void RefillProductionForPlayer(IGameAPI api, PlayerId player)
	{
		int playerIndex = player.Value;
		ReadOnlySpan<UnitId> owned = player.GetUnits();
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId building = owned[index];
			if (building.IsDead || !building.IsBuilding) continue;

			BuildingProfile profile = FindBuildingProfile(building.TemplateID);
			if (string.IsNullOrEmpty(profile.TemplateId)) continue;

			StateBuildingMana(api, playerIndex, building, profile);
			api.SetUnitMana(building, MaximumProductionMana(playerIndex, FindProfileIndexByBuilding(building.TemplateID), profile));
			PublishProductionBadge(api, player, building, AvailableProductions(building));
		}
	}

	private void GrantOneProductionToBuildings(int playerIndex)
	{
		ReadOnlySpan<UnitId> owned = _api.GetUnitsOwnedByPlayer(playerIndex);
		PlayerId player = _api.GetPlayer(playerIndex);

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId building = owned[index];
			if (building.IsDead || !building.IsBuilding) continue;

			BuildingProfile profile = FindBuildingProfile(building.TemplateID);
			if (string.IsNullOrEmpty(profile.TemplateId)) continue;
			if (!IsBuildingOperational(_api, building)) continue;

			StateBuildingMana(_api, playerIndex, building, profile);
			_api.SetUnitMana(building, MathF.Min(building.MaxMana, building.Mana + ManaPerProduction));
			PublishProductionBadge(_api, player, building, AvailableProductions(building));
		}
	}

	private int CountWorkersOfBuilding(ReadOnlySpan<UnitId> owned, int buildingId)
	{
		int workers = 0;
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead) continue;
			if (_workerBuildingOf[unit.UniqueId] != buildingId) continue;

			workers++;
		}

		return workers;
	}

	private bool TryRaiseFromAbility(IGameAPI api, UnitId building, string abilityId)
	{
		if (!building.IsValid || !building.IsBuilding) return false;

		BuildingProfile profile = FindBuildingProfile(building.TemplateID);
		if (string.IsNullOrEmpty(profile.TemplateId)) return false;

		int playerIndex = building.Player;
		if (playerIndex <= 0) return false;

		PlayerId player = api.GetPlayer(playerIndex);
		if (!player.IsActive) return false;

		if (string.Equals(abilityId, profile.SoldierAbilityId, StringComparison.OrdinalIgnoreCase))
		{
			int profileIndex = FindProfileIndexByBuilding(building.TemplateID);
			if (profileIndex < 0) return true;

			SpawnSoldier(api, player, SoldierProfiles[profileIndex], building);
			return true;
		}

		if (string.Equals(abilityId, profile.WorkerAbilityId, StringComparison.OrdinalIgnoreCase))
		{
			SpawnWorker(api, player, building, profile);
			return true;
		}

		return false;
	}

	/// <summary>
	///     Fields one soldier at the building that raised it and sends it walking to the hero, which is the reading a
	///     player expects: the site is where the soldier comes from, and the army gathers around the hero. The army's own
	///     formation pass keeps it in its slot once it arrives.
	/// </summary>
	private void SpawnSoldier(IGameAPI api, PlayerId player, SoldierProfile profile, UnitId building)
	{
		Vector3 origin = building.Position;
		float angle = (building.UniqueId % 8) * (MathF.Tau / 8f);
		var position = new Vector2(origin.X + MathF.Cos(angle) * 2.5f, origin.Z + MathF.Sin(angle) * 2.5f);

		UnitId soldier = player.SpawnUnit(profile.SoldierTemplateId, position, true, true);
		if (!soldier.IsValid) return;

		ResetUnitRuntimeState(soldier.UniqueId);
		_sourceBuildings[soldier.UniqueId] = building.UniqueId;
		api.EnableUnitAutoCombat(soldier, false);
		api.SetUnitPlayerOrdersLocked(soldier, true);
		_soldierControlled[soldier.UniqueId] = 1;

		int heroEntityId = _heroEntities[player.Value];
		if (heroEntityId > 0)
		{
			UnitId hero = new(heroEntityId);
			if (!hero.IsDead)
			{
				if (hero.Level > 1) api.SetUnitLevel(soldier, hero.Level);

				Vector3 rallyPoint = ComputeFormationSlot(profile.Formation, 0, hero.Position, _heroFacing[player.Value]);
				api.IssueMoveOrder(soldier, rallyPoint);
			}
		}

		ApplyInnateToUnit(api, player.Value, soldier);
		ApplyRelicsToUnit(api, player.Value, soldier);
	}

	/// <summary>
	///     Raises one worker for a building. Every building raises the same worker template, at the level of the order the
	///     building itself was constructed in, and it carries that building's own permanent buffs, so two buildings with the
	///     same worker template gather at different rates without either of them stating one.
	/// </summary>
	private void SpawnWorker(IGameAPI api, PlayerId player, UnitId building, BuildingProfile profile)
	{
		byte resource = ResourceByteOf(profile.WorkerResourceType);
		if (resource == 0) return;

		Vector3 origin = building.Position;
		var position = new Vector2(origin.X, origin.Z);
		UnitId worker = player.SpawnUnit(WorkerTemplateId, position, true, true);
		if (!worker.IsValid) return;

		int workerId = worker.UniqueId;
		int buildingId = building.UniqueId;
		ResetUnitRuntimeState(workerId);
		_workerBuildingOf[workerId] = buildingId;
		_workerNodeOf[workerId] = 0;
		_workerPausedOf[workerId] = 0;

		api.SetUnitPlayerOrdersLocked(worker, true);
		api.EnableUnitAutoCombat(worker, false);
		api.SetUnitWorker(worker, true);
		api.SetUnitWorkerAssignment(worker, profile.WorkerResourceType);

		// The assignment the engine keeps on the worker is what the crew works, so the script reads it back once here
		// rather than asking for the string on every worker check.
		_workerResourceOf[workerId] = ResourceByteOf(api.GetUnitWorkerAssignment(worker));
		api.SetUnitLevel(worker, Math.Max(1, _buildingConstructionOrder[buildingId]));
		ApplyInnateToUnit(api, player.Value, worker);

		for (int index = 0; index < profile.WorkerBuffIds.Length; index++)
		{
			api.AddBuff(worker, profile.WorkerBuffIds[index], PermanentBuffSeconds);
		}

		PublishWorkerBuffText(api, worker, profile);

		if (HasRelic(player.Value, 3))
		{
			api.AddBuff(worker, DeepGranariesWorkerBuffId, PermanentBuffSeconds);
			PublishDeepGranariesWorkerText(api, worker);
		}

		AssignWorkerToNearestNode(api, workerId, building, resource);
	}

	private static void PublishWorkerBuffText(IGameAPI api, UnitId worker, BuildingProfile profile)
	{
		string buildingName = api.GetUnitName(profile.TemplateId);
		for (int index = 0; index < profile.WorkerBuffIds.Length; index++)
		{
			string core = BuffCore(profile.WorkerBuffIds[index]);
			api.SetUnitTooltipText(worker, $"buff.{core}.name", $"{buildingName} Crew");
			api.SetUnitTooltipText(worker, $"buff.{core}.description", profile.WorkerBuffSummary);
			api.SetUnitTooltipText(worker, $"buff.{core}.icon", WorkerCrewBuffIcon);
		}
	}

	private static void PublishDeepGranariesWorkerText(IGameAPI api, UnitId worker)
	{
		api.SetUnitTooltipText(worker, "buff.deep_granaries_worker.name", "Deep Granaries");
		api.SetUnitTooltipText(worker, "buff.deep_granaries_worker.description", "+10% attack.");
		api.SetUnitTooltipText(worker, "buff.deep_granaries_worker.icon", "res://Assets/UI/scroll_icon.webp");
	}

	private static void PublishCinderMarkText(IGameAPI api, UnitId unit, float burnPerSecond)
	{
		api.SetUnitTooltipText(unit, "buff.cinder_mark.name", "Cinder Mark");
		api.SetUnitTooltipText(unit, "buff.cinder_mark.description", $"Burns for {burnPerSecond:0.#} magic damage a second for 2 seconds.");
		api.SetUnitTooltipText(unit, "buff.cinder_mark.icon", "res://Assets/UI/fire_spell.webp");
	}

	private static float CinderMarkEffectiveBurnPerSecond(IGameAPI api, UnitId sable)
	{
		return CinderMarkDamagePerSecond * (1f + api.GetUnitSpellPower(sable));
	}

	private static string BuffCore(string buffId)
	{
		return buffId.StartsWith("buff/", StringComparison.OrdinalIgnoreCase) ? buffId.Substring("buff/".Length) : buffId;
	}

	private static byte ResourceByteOf(string resourceType)
	{
		if (string.Equals(resourceType, "gold", StringComparison.OrdinalIgnoreCase)) return 1;
		if (string.Equals(resourceType, "wood", StringComparison.OrdinalIgnoreCase)) return 2;

		return 0;
	}

	private void AssignWorkerToNearestNode(IGameAPI api, int workerId, UnitId building, byte resource)
	{
		UnitId worker = new(workerId);
		if (worker.IsDead) return;

		ResourceNodeId node = FindNearestNode(api, building.Position, resource);
		if (!node.IsValid)
		{
			_workerNodeOf[workerId] = 0;
			return;
		}

		api.IssueGatherOrder(worker, node);

		// A node index is zero-based, so the state names it one higher and keeps zero for "no node".
		_workerNodeOf[workerId] = node.Value + 1;
	}

	private static ResourceNodeId FindNearestNode(IGameAPI api, Vector3 origin, byte resource)
	{
		string wanted = resource == 2 ? "wood" : "gold";
		ReadOnlySpan<ResourceNodeId> nodes = api.GetResourceNodes();
		ResourceNodeId nearest = ResourceNodeId.Invalid;
		float nearestDistanceSquared = WorkerSearchRadius * WorkerSearchRadius;

		for (int index = 0; index < nodes.Length; index++)
		{
			ResourceNodeId node = nodes[index];
			if (!string.Equals(api.GetResourceNodeType(node), wanted, StringComparison.OrdinalIgnoreCase)) continue;
			if (api.GetResourceNodeAmount(node) <= 0.01f) continue;

			Vector3 nodePosition = api.GetResourceNodePosition(node);
			float offsetX = nodePosition.X - origin.X;
			float offsetZ = nodePosition.Z - origin.Z;
			float distanceSquared = offsetX * offsetX + offsetZ * offsetZ;
			if (distanceSquared >= nearestDistanceSquared) continue;

			nearestDistanceSquared = distanceSquared;
			nearest = node;
		}

		return nearest;
	}

	/// <summary>
	///     Keeps each building's worker on a node a few times a second: a worker whose building is gone is retired, and one
	///     whose node has run dry is sent to the next nearest node of the same kind. A worker with no node left anywhere,
	///     or the same player's other workers body-blocking it, is left where it stands rather than paid from nothing.
	/// </summary>
	private void MaintainWorkers(IGameAPI api, ReadOnlySpan<PlayerId> players, float delta)
	{
		_workerCheckTimer += delta;
		if (_workerCheckTimer < WorkerCheckSeconds) return;
		_workerCheckTimer = 0f;

		for (int index = 0; index < players.Length; index++)
		{
			PlayerId player = players[index];
			if (player.Value == 0 || !player.IsActive) continue;

			ReadOnlySpan<UnitId> owned = player.GetUnits();
			for (int unitIndex = 0; unitIndex < owned.Length; unitIndex++)
			{
				UnitId unit = owned[unitIndex];
				if (unit.IsDead) continue;

				int workerId = unit.UniqueId;
				int buildingId = _workerBuildingOf[workerId];
				if (buildingId == 0) continue;

				UnitId building = new(buildingId);
				if (building.IsDead)
				{
					_workerBuildingOf[workerId] = 0;
					_workerNodeOf[workerId] = 0;
					_workerResourceOf[workerId] = 0;
					api.DestroyUnit(unit, executeDespawnShader: false, playDeathAnimation: false);
					continue;
				}

				// A destroyed building pays nothing while it waits to be mended, so its worker stands down rather than
				// mining a node the building cannot be paid from. It walks back to work the moment the building stands.
				if (building.IsDormant)
				{
					if (_workerPausedOf[workerId] == 0)
					{
						_workerPausedOf[workerId] = 1;
						api.IssueStopOrder(unit);
					}

					continue;
				}

				if (_workerPausedOf[workerId] != 0)
				{
					_workerPausedOf[workerId] = 0;
					AssignWorkerToNearestNode(api, workerId, building, _workerResourceOf[workerId]);
					continue;
				}

				int nodeState = _workerNodeOf[workerId];
				if (nodeState > 0 && api.GetResourceNodeAmount(new ResourceNodeId(nodeState - 1)) > 0.01f) continue;

				AssignWorkerToNearestNode(api, workerId, building, _workerResourceOf[workerId]);
			}
		}
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

			// The damage resolver applies the caster's spell power to magic damage, so the banked tick is the authored
			// rate and the engine scales it once. Scaling here as well would apply spell power twice.
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

		EnsureBulwarkBuff(api, hero);

		ReadOnlySpan<UnitId> owned = player.GetUnits();
		int nearbySoldiers = 0;
		int functionalBuildings = CountAlliedOperationalBuildings(api, player);

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead) continue;
			if (unit.IsBuilding) continue;
			if (unit.IsHero) continue;
			if (_workerBuildingOf[unit.UniqueId] > 0) continue;
			if (Vector3.DistanceSquared(unit.Position, hero.Position) <= BulwarkSoldierRadius * BulwarkSoldierRadius) nearbySoldiers++;
		}

		float heroArmor = nearbySoldiers * BulwarkArmorPerSoldier;
		float heroResistance = nearbySoldiers * BulwarkMagicResistancePerSoldier;
		api.RemoveStatModifiers(hero, "bulwark/hero");
		api.AddStatModifier(hero, "Armor", false, heroArmor, "bulwark/hero");
		api.AddStatModifier(hero, "MagicResistance", false, heroResistance, "bulwark/hero");

		ulong heroState = (ulong)(uint)nearbySoldiers;
		if (_bulwarkStates[playerIndex] != heroState)
		{
			_bulwarkStates[playerIndex] = heroState;
			PublishBulwarkText(api, hero, "Nearby soldiers", nearbySoldiers, heroArmor, heroResistance);
		}

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId building = owned[index];
			if (building.IsDead || !building.IsBuilding) continue;
			if (!IsBuildingOperational(api, building)) continue;

			RefreshBuildingBulwark(api, player, building, functionalBuildings);
		}
	}

	private void RefreshBuildingBulwark(IGameAPI api, PlayerId player, UnitId building, int functionalBuildings)
	{
		EnsureBulwarkBuff(api, building);

		int attackingForce = LargestHostileTeamForceNear(api, player, building.Position);
		float penetration = Math.Clamp(attackingForce * BulwarkPenetrationPerAttacker, 0f, 1f);
		float coefficient = BulwarkCoefficientFor(building.TemplateID);
		float armor = functionalBuildings * BulwarkArmorPerBuilding * coefficient * (1f - penetration);
		float resistance = functionalBuildings * BulwarkMagicResistancePerBuilding * coefficient * (1f - penetration);

		api.RemoveStatModifiers(building, "bulwark/buildings");
		api.AddStatModifier(building, "Armor", false, armor, "bulwark/buildings");
		api.AddStatModifier(building, "MagicResistance", false, resistance, "bulwark/buildings");

		ulong state = ((ulong)(uint)functionalBuildings << 32) | ((ulong)(uint)attackingForce << 16) | (uint)BitConverter.SingleToInt32Bits(coefficient);
		int entityId = building.UniqueId;
		if (_buildingBulwarkStates[entityId] == state) return;

		_buildingBulwarkStates[entityId] = state;
		PublishBuildingBulwarkText(api, building, functionalBuildings, attackingForce, armor, resistance);
	}

	/// <summary>
	///     Counts every operational building its team holds, not only the defender's own. The rule groups by team, so an
	///     ally's base reinforces the same buildings the player's own base does.
	/// </summary>
	/// <param name="api">The game API reference to read the teams from.</param>
	/// <param name="player">The player whose team is counted.</param>
	/// <returns>The number of finished, living buildings held by the player and its declared allies.</returns>
	private int CountAlliedOperationalBuildings(IGameAPI api, PlayerId player)
	{
		ReadOnlySpan<PlayerId> players = api.GetActivePlayers();
		int functionalBuildings = 0;

		for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
		{
			PlayerId ally = players[playerIndex];
			if (ally.Value <= 0 || !ally.IsActive) continue;
			if (ally.Value != player.Value && !api.ArePlayersAllied(ally.Value, player.Value)) continue;

			ReadOnlySpan<UnitId> owned = ally.GetUnits();
			for (int unitIndex = 0; unitIndex < owned.Length; unitIndex++)
			{
				UnitId unit = owned[unitIndex];
				if (unit.IsDead || !unit.IsBuilding) continue;
				if (IsBuildingOperational(api, unit)) functionalBuildings++;
			}
		}

		return functionalBuildings;
	}

	private int LargestHostileTeamForceNear(IGameAPI api, PlayerId player, Vector3 position)
	{
		Span<int> teamCounts = stackalloc int[EcsSharedMemory.MAX_PLAYERS];
		ReadOnlySpan<UnitId> nearby = api.GetUnitsInRadius(position, BulwarkAttackerRadius);

		for (int index = 0; index < nearby.Length; index++)
		{
			UnitId unit = nearby[index];
			if (unit.IsDead || unit.IsBuilding) continue;
			if (unit.Player <= 0 || unit.Player == player.Value) continue;
			if (api.ArePlayersAllied(unit.Player, player.Value)) continue;

			int team = api.GetPlayer(unit.Player).Team;
			if (team <= 0 || team >= teamCounts.Length) continue;

			teamCounts[team]++;
		}

		int largest = 0;
		for (int team = 1; team < teamCounts.Length; team++)
		{
			if (teamCounts[team] > largest) largest = teamCounts[team];
		}

		return largest;
	}

	private static float BulwarkCoefficientFor(string templateId)
	{
		return templateId switch
		{
			NexusTemplateId => BulwarkNexusCoefficient,
			"building/bulwark_hall" => 0.9f,
			"building/warband_yard" => 0.7f,
			"building/spear_lodge" => 0.8f,
			"building/archery_range" => 0.6f,
			"building/bolt_foundry" => 0.85f,
			"building/sling_camp" => 0.5f,
			"building/mender_shrine" => 0.7f,
			"building/signal_post" => 0.6f,
			"building/vigil_bastion" => 0.75f,
			_ => 0.5f
		};
	}

	private static void EnsureBulwarkBuff(IGameAPI api, UnitId unit)
	{
		if (!unit.IsValid || unit.IsDead) return;
		if (api.UnitHasBuff(unit, BulwarkBuffId)) return;

		api.AddBuff(unit, BulwarkBuffId, PermanentBuffSeconds);
	}

	private static void PublishBulwarkText(IGameAPI api, UnitId unit, string countLabel, int count, float armor, float resistance)
	{
		api.SetUnitTooltipText(unit, "buff.bulwark.name", "Bulwark");
		api.SetUnitTooltipText(unit, "buff.bulwark.description", $"{countLabel}: {count}. Armor +{armor:0.#}, magic resistance +{resistance:0.#}.");
		api.SetUnitTooltipText(unit, "buff.bulwark.icon", "res://Assets/UI/battle_shield.webp");
	}

	private static void PublishBuildingBulwarkText(IGameAPI api, UnitId building, int functionalBuildings, int attackingForce, float armor, float resistance)
	{
		api.SetUnitTooltipText(building, "buff.bulwark.name", "Bulwark");
		api.SetUnitTooltipText(building, "buff.bulwark.description", $"Allied buildings: {functionalBuildings}. Nearby attackers: {attackingForce}. Armor +{armor:0.#}, magic resistance +{resistance:0.#}.");
		api.SetUnitTooltipText(building, "buff.bulwark.icon", "res://Assets/UI/battle_shield.webp");
	}

	private void HandleConstructionFinished(UnitId building)
	{
		if (!building.IsValid || !building.IsBuilding) return;

		int playerIndex = building.Player;
		if (playerIndex <= 0) return;

		int profileIndex = FindProfileIndexByBuilding(building.TemplateID);
		if (profileIndex < 0) return;

		// The worker every building raises matches the order the building itself was constructed in, so the first
		// building's workers are first level and the next building's are one level above them.
		int buildingId = building.UniqueId;
		int constructionOrder = _playerConstructionCount[playerIndex] + 1;
		_playerConstructionCount[playerIndex] = constructionOrder;
		_buildingConstructionOrder[buildingId] = constructionOrder;
		_lastBadgeValues[buildingId] = -1;
		_statedManaMax[buildingId] = -1;
		_statedManaRegen[buildingId] = -1f;

		PlayerId player = _api.GetPlayer(playerIndex);
		BuildingProfile profile = FindBuildingProfile(building.TemplateID);
		StateBuildingMana(_api, playerIndex, building, profile);
		ApplyInnateToUnit(_api, playerIndex, building);

		// A finished site opens with one production already paid for, which is the production the building opens with
		// with, and the rest of its pool fills at the two lines' recharge.
		_api.SetUnitMana(building, ManaPerProduction);
		PublishProductionBadge(_api, player, building, AvailableProductions(building));
		player.ShowFeedbackText($"{_api.GetUnitName(building.TemplateID)} is complete.", new Vector3(0.8f, 0.9f, 1f));
	}

	private void HandleUnitDamaged(UnitId victim, UnitId attacker, float damage)
	{
		if (!victim.IsValid || damage <= 0.01f) return;

		int playerIndex = victim.Player;
		if (playerIndex <= 0) return;

		if (attacker.IsValid && attacker.IsHero && attacker.Player > 0 && !attacker.IsDead)
		{
			int attackingPlayerIndex = attacker.Player;
			if (!_api.ArePlayersAllied(attackingPlayerIndex, playerIndex))
			{
				_heroRecentTargets[attackingPlayerIndex] = victim.UniqueId;
				_heroRecentTargetTimes[attackingPlayerIndex] = _api.GameElapsedTime;
			}
		}

		PlayerId player = _api.GetPlayer(playerIndex);
		if (!player.IsActive) return;
		if (attacker.IsValid && player.IsAlliedWith(attacker.Player)) return;

		if (IsCinderMarkHero(attacker.Player) && !victim.IsBuilding && _burningFromMark[victim.UniqueId] == 0)
		{
			victim.AddBuff("buff/cinder_mark", 2f);
			int sableEntityId = _heroEntities[attacker.Player];
			float burnPerSecond = sableEntityId > 0 ? CinderMarkEffectiveBurnPerSecond(_api, new UnitId(sableEntityId)) : CinderMarkDamagePerSecond;
			PublishCinderMarkText(_api, victim, burnPerSecond);
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

		_heroLevelsBeforeDeath[playerIndex] = Math.Max(1, hero.Level);
		_heroExperiences[playerIndex] = hero.Experience;
		_heroEntities[playerIndex] = 0;
		_heroRecentTargets[playerIndex] = 0;
		_bulwarkStates[playerIndex] = ulong.MaxValue;
		_heroDeaths[playerIndex]++;
		float revivalSeconds = MathF.Min(RevivalSecondsCap, BaseRevivalSeconds + Math.Max(0, _heroDeaths[playerIndex] - 1) * RevivalSecondsPerDeath);
		_reviveAtTimes[playerIndex] = _api.GameElapsedTime + revivalSeconds;
		AbandonOpenDraft(playerIndex);

		ReadOnlySpan<UnitId> owned = _api.GetUnitsOwnedByPlayer(playerIndex);
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead) continue;

			// Production stops while the hero is dead because the buildings' abilities are disabled, so the army
			// disperses without the base continuing to field soldiers nobody follows.
			if (unit.IsHero || unit.IsBuilding) continue;

			// Workers are the economy, not the army. They keep working their nodes, and their productions keep refilling, so
			// a hero's death costs the army it was leading rather than the base that supports it.
			if (_workerBuildingOf[unit.UniqueId] > 0) continue;

			unit.Destroy(false, false);
		}

		PlayerId player = _api.GetPlayer(playerIndex);
		ApplyRelicVendorState(_api, player);
		player.ShowFeedbackText($"Your hero has fallen. The army disperses. Your hero returns in {revivalSeconds:0} seconds.", new Vector3(1f, 0.35f, 0.3f));
		player.PlayWarningSound();
	}

	/// <summary>
	///     Ends a seat's game when its Nexus falls, unless an allied seat still holds one. Declared allies are one group
	///     for every team-level rule, and elimination is one of them: a seat whose base is gone keeps its army and keeps
	///     playing while its ally's Nexus stands, and only the loss of the team's last Nexus ends every seat on it.
	/// </summary>
	/// <param name="nexus">The Nexus that was destroyed.</param>
	private void HandleNexusDestroyed(UnitId nexus)
	{
		int playerIndex = nexus.Player;
		if (playerIndex <= 0 || _eliminated[playerIndex] != 0) return;

		_eliminated[playerIndex] = 1;
		AbandonOpenDraft(playerIndex);

		int team = _api.GetPlayer(playerIndex).Team;
		if (!TeamHoldsANexus(team, playerIndex))
		{
			ReadOnlySpan<PlayerId> players = _api.GetActivePlayers();
			for (int index = 0; index < players.Length; index++)
			{
				PlayerId member = players[index];
				if (member.Value <= 0 || !member.IsActive) continue;
				if (member.Team != team) continue;

				member.TriggerDefeat("Your Nexus has been destroyed.");
			}
		}

		CheckTeamVictory();
	}

	/// <summary>
	///     Answers whether any seat on a team still has its Nexus standing. The seat whose Nexus has just fallen is left
	///     out, because the death of a Nexus is reported while its unit is still standing and asking the world about it
	///     would say the team still holds the base that was just destroyed.
	/// </summary>
	/// <param name="team">The team to look at.</param>
	/// <param name="fallenSeat">The seat whose Nexus this is.</param>
	/// <returns>Whether another Nexus that belongs to that team is on the board.</returns>
	private bool TeamHoldsANexus(int team, int fallenSeat)
	{
		ReadOnlySpan<PlayerId> players = _api.GetActivePlayers();
		for (int index = 0; index < players.Length; index++)
		{
			PlayerId player = players[index];
			if (player.Value <= 0 || !player.IsActive) continue;
			if (player.Value == fallenSeat) continue;
			if (player.Team != team) continue;
			if (_nexusEntities[player.Value] <= 0) continue;
			if (!new UnitId(_nexusEntities[player.Value]).IsDead) return true;
		}

		return false;
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

		if (TryRaiseFromAbility(_api, caster, abilityId)) return;

		if (IsRelicAbility(abilityId))
		{
			BuyRelic(playerIndex, abilityId);
			return;
		}

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

		if (abilityId == "ability/cinderfield")
		{
			BeginCinderfield(_api, caster, position);
			return;
		}

		if (abilityId == "ability/emergency_levy")
		{
			GrantOneProductionToBuildings(playerIndex);
		}
	}

	private void AdvanceActiveConstruction(int playerIndex)
	{
		UnitId hero = new(_heroEntities[playerIndex]);
		int heroLevel = hero.IsDead ? 1 : hero.Level;

		ReadOnlySpan<UnitId> owned = _api.GetUnitsOwnedByPlayer(playerIndex);
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId building = owned[index];
			if (building.IsDead || !building.IsBuilding) continue;

			float progress = _api.GetUnitConstructionProgress(building);
			if (progress >= 1f) continue;

			float remainingSeconds = (1f - progress) * ResolveConstructionSeconds(building.TemplateID);
			_api.AdvanceUnitConstruction(building, remainingSeconds * RequisitionCompletionFractionForLevel(heroLevel));

			PlayerId player = _api.GetPlayer(playerIndex);
			player.ShowFeedbackText("Requisition speeds the work along.", new Vector3(0.9f, 0.85f, 0.5f));
			return;
		}
	}

	private static float RequisitionCompletionFractionForLevel(int level)
	{
		int clampedLevel = Math.Clamp(level, 1, 6);
		return MathF.Min(RequisitionMaximumCompletionFraction, RequisitionBaseCompletionFraction + ((clampedLevel - 1) * 0.05f));
	}

	private void BeginCinderfield(IGameAPI api, UnitId caster, Vector3 position)
	{
		int playerIndex = caster.Player;
		if (playerIndex <= 0) return;

		int slot = FindCinderfieldSlot();
		_cinderfieldActive[slot] = 1;
		_cinderfieldCasters[slot] = caster.UniqueId;
		_cinderfieldOwners[slot] = playerIndex;
		_cinderfieldCentersX[slot] = position.X;
		_cinderfieldCentersZ[slot] = position.Z;
		_cinderfieldRemainingSeconds[slot] = CinderfieldDurationForLevel(caster.Level);
		_cinderfieldDamageTimers[slot] = 0f;

		api.SpawnExpandingGroundRing(position, CinderfieldRadius, CinderfieldRadius, 1.1f, new Vector3(1f, 0.3f, 0.05f));
		api.GetPlayer(playerIndex).ShowFeedbackText("Cinderfield burns the ground.", new Vector3(1f, 0.4f, 0.15f));
	}

	private static float CinderfieldDurationForLevel(int level)
	{
		int clampedLevel = Math.Clamp(level, 1, 6);
		if (clampedLevel <= 3) return CinderfieldBaseDurationSeconds + ((clampedLevel - 1) * 0.25f);

		return 4.5f + ((clampedLevel - 3) * (0.5f / 3f));
	}

	private int FindCinderfieldSlot()
	{
		int oldestSlot = 0;
		float shortestRemaining = float.MaxValue;

		for (int index = 0; index < _cinderfieldActive.Length; index++)
		{
			if (_cinderfieldActive[index] == 0) return index;
			if (_cinderfieldRemainingSeconds[index] >= shortestRemaining) continue;

			shortestRemaining = _cinderfieldRemainingSeconds[index];
			oldestSlot = index;
		}

		return oldestSlot;
	}

	private void UpdateCinderfields(IGameAPI api, float delta)
	{
		if (delta <= 0f) return;

		for (int index = 0; index < _cinderfieldActive.Length; index++)
		{
			if (_cinderfieldActive[index] == 0) continue;

			_cinderfieldRemainingSeconds[index] -= delta;
			_cinderfieldDamageTimers[index] += delta;

			while (_cinderfieldDamageTimers[index] >= CinderfieldTickSeconds)
			{
				_cinderfieldDamageTimers[index] -= CinderfieldTickSeconds;
				DamageCinderfield(api, index);
			}

			if (_cinderfieldRemainingSeconds[index] <= 0f) _cinderfieldActive[index] = 0;
		}
	}

	private void DamageCinderfield(IGameAPI api, int slot)
	{
		int playerIndex = _cinderfieldOwners[slot];
		var center = new Vector3(_cinderfieldCentersX[slot], 0f, _cinderfieldCentersZ[slot]);

		UnitId caster = new(_cinderfieldCasters[slot]);
		if (caster.IsDead) caster = new(_heroEntities[playerIndex]);
		if (caster.IsDead) caster = UnitId.Invalid;

		api.SpawnExpandingGroundRing(center, CinderfieldRadius, CinderfieldRadius, CinderfieldTickSeconds + 0.1f, new Vector3(1f, 0.3f, 0.05f));

		ReadOnlySpan<UnitId> caught = api.GetUnitsInRadius(center, CinderfieldRadius);
		for (int index = 0; index < caught.Length; index++)
		{
			UnitId unit = caught[index];
			if (unit.IsDead) continue;
			if (api.ArePlayersAllied(unit.Player, playerIndex)) continue;

			api.DealTypedDamage(caster, unit, CinderfieldDamagePerSecond * CinderfieldTickSeconds, "magic");
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

		if (_api.GetPlayerBuildingCount(playerIndex) > _maximumBuildingsPerPlayer)
		{
			player.ShowFeedbackText($"All {_maximumBuildingsPerPlayer} building locations are taken.", new Vector3(1f, 0.7f, 0.3f));
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

		// The blueprints stands where the player cast, and it is the thing that carries the draw: the packages appear in
		// its command grid, so choosing one reads as choosing what to raise on that ground rather than as a second cast
		// from the hero that already paid for the site.
		UnitId blueprint = player.SpawnUnit(BlueprintTemplateId, new Vector2(position.X, position.Z), true, true);
		if (!blueprint.IsValid)
		{
			player.ShowFeedbackText("The site could not be marked there.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		_api.SetUnitInvulnerable(blueprint, true);
		_api.SetUnitCommandsFrozen(blueprint, true);
		player.AdjustWood(-BuildingWoodCost);
		_draftOpen[playerIndex] = 1;
		_draftOptionCounts[playerIndex] = optionCount;
		_draftSiteEntities[playerIndex] = blueprint.UniqueId;
		Vector3 sitePosition = blueprint.Position;
		_draftSitePositionsX[playerIndex] = sitePosition.X;
		_draftSitePositionsZ[playerIndex] = sitePosition.Z;

		for (int index = 0; index < optionCount; index++)
		{
			string templateId = SoldierProfiles[_draftOptions[playerIndex * MaximumDraftOptions + index]].BuildingTemplateId;
			_api.SetAbilityGridPosition(templateId, index, DraftRow);
		}

		ApplyDraftAbilityState(_api, playerIndex);

		player.ShowFeedbackText($"{optionCount} building(s) are offered. Pick one from the command grid, or abandon the construction.", new Vector3(0.85f, 0.9f, 0.6f));
		_api.SpawnExpandingGroundRing(new Vector3(position.X, 0f, position.Z), 3f, 6f, 0.6f, new Vector3(0.9f, 0.8f, 0.4f));
	}

	/// <summary>
	///     Reads the parameters the lobby's host configured for this match, so a host can seat more players, offer more or
	///     fewer building options per draw, allow more copies of one building, multiply capacity and stored productions, and switch on
	///     mirrored drafting or an open building pool. Every one of them falls back to the value this ruleset states, which
	///     is what a lobby that touched nothing plays.
	/// </summary>
	/// <param name="api">The game API the match runs through.</param>
	private void ReadMatchOptions(IGameAPI api)
	{
		int draftOptions = api.GetMatchOption(MatchOption.MaximumBuildingDraftCount);
		_draftOptionsPerDraw = draftOptions > 0 ? Math.Clamp(draftOptions, 1, MaximumDraftOptions) : DefaultDraftOptionCount;

		int buildingSupply = api.GetMatchOption(MatchOption.MaximumBuildingSupply);
		_maximumBuildingsPerPlayer = buildingSupply > 0 ? Math.Clamp(buildingSupply, 1, MaximumBuildingLimit) : DefaultMaximumBuildingsPerPlayer;

		int buildingCopies = api.GetMatchOption(MatchOption.MaximumUniqueBuildingInstances);
		_maximumBuildingCopiesPerPlayer = buildingCopies > 0 ? Math.Clamp(buildingCopies, 1, MaximumBuildingLimit) : DefaultMaximumBuildingCopiesPerPlayer;

		int capacityPercent = api.GetMatchOption(MatchOption.CapacityMultiplierPercent);
		_capacityMultiplier = capacityPercent > 0 ? capacityPercent / 100f : 1f;

		int productionPercent = api.GetMatchOption(MatchOption.ProductionMultiplierPercent);
		_rechargeMultiplier = productionPercent > 0 ? productionPercent / 100f : 1f;

		_mirroredDraft = api.GetMatchOption(MatchOption.MirroredAutoDraft) != 0;
		_openPoolDrafting = api.GetMatchOption(MatchOption.OpenPoolDrafting) != 0;
	}

	/// <summary>
	///     Answers how many soldiers of one profile a building of that profile supports, after the host's capacity
	///     multiplier has been applied.
	/// </summary>
	/// <param name="profileIndex">The index of the soldier profile in <see cref="SoldierProfiles" />.</param>
	/// <returns>The capacity one building of that profile grants.</returns>
	private int ScaledCapacity(int profileIndex)
	{
		int capacity = SoldierProfiles[profileIndex].Capacity;
		if (_capacityMultiplier == 1f) return capacity;

		return Math.Max(1, (int)MathF.Round(capacity * _capacityMultiplier));
	}

	/// <summary>
	///     Answers how many productions a building of one profile stores, after the host's production multiplier has been applied.
	/// </summary>
	/// <param name="profileIndex">The index of the soldier profile in <see cref="SoldierProfiles" />.</param>
	/// <returns>The productions one building of that profile stores.</returns>
	private int ScaledStoredProductions(int profileIndex)
	{
		int productions = SoldierProfiles[profileIndex].StoredProductions;
		if (_rechargeMultiplier == 1f) return productions;

		return Math.Max(1, (int)MathF.Round(productions * _rechargeMultiplier));
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

		int optionCount = Math.Min(_draftOptionsPerDraw, candidateCount);
		int optionStart = playerIndex * MaximumDraftOptions;
		for (int index = 0; index < optionCount; index++)
		{
			if (_mirroredDraft)
			{
				_draftOptions[optionStart + index] = candidates[index];
				continue;
			}

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
			if (CountOwnedBuildings(owned, SoldierProfiles[profileIndex].BuildingTemplateId) >= _maximumBuildingCopiesPerPlayer) continue;

			destination[count++] = profileIndex;
		}

		return count;
	}

	private static int CountOwnedBuildings(ReadOnlySpan<UnitId> owned, string templateId)
	{
		int count = 0;
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead || !unit.IsBuilding) continue;
			if (unit.TemplateID == templateId) count++;
		}

		return count;
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
			BuildingProfile workerProfile = FindBuildingProfile(profile.BuildingTemplateId);
			string tooltip = $"Draft {buildingName} for 100 wood. Raises {soldierName}: {ScaledCapacity(index)} capacity, one every {profile.ProductionSeconds:0}s. Workers gather {workerProfile.WorkerResourceType}: {workerProfile.WorkerCapacity} at {workerProfile.WorkerProductionSeconds:0}s each, gaining {workerProfile.WorkerBuffSummary}. Both draw from the building's own mana.";

			api.RegisterAbility(profile.BuildingTemplateId, buildingName, tooltip, string.Empty, true);
			AddDraftAbilityToBlueprint(api, profile.BuildingTemplateId);
			api.SetAbilityTooltip(profile.BuildingTemplateId, tooltip);
		}

		api.RegisterAbility(AbandonConstructionAbilityId, "Abandon Construction", "Gives back the 100 wood the open construction reserved.", string.Empty, true);
		api.SetAbilityGridPosition(AbandonConstructionAbilityId, DraftAbandonSlot, DraftRow);
		AddDraftAbilityToBlueprint(api, AbandonConstructionAbilityId);
	}

	private static void AddDraftAbilityToBlueprint(IGameAPI api, string abilityId)
	{
		api.AddUnitTypeAbility(BlueprintTemplateId, abilityId);
	}

	private void RetireDraftSite(int playerIndex)
	{
		int entityId = _draftSiteEntities[playerIndex];
		_draftSiteEntities[playerIndex] = 0;
		if (entityId <= 0) return;

		_draftSiteRetirements[playerIndex] = entityId;

		// The marker answers the draw, so its packages stop being offered at once even though the marker itself stands
		// for one more tick: a selection made in that tick must not still read the draw it has already answered.
		UnitId blueprint = new(entityId);
		if (blueprint.IsDead) return;

		for (int index = 0; index < SoldierProfiles.Length; index++)
		{
			_api.SetAbilityState(blueprint, SoldierProfiles[index].BuildingTemplateId, true, true);
		}

		_api.SetAbilityState(blueprint, AbandonConstructionAbilityId, true, true);
	}

	/// <summary>
	///     Removes the markers of sites that were answered last tick. The blueprint is often the caster of the command that
	///     answered it, so it is retired on the following update rather than while its own ability is still resolving.
	/// </summary>
	private void RetireDraftSites(IGameAPI api)
	{
		for (int playerIndex = 1; playerIndex < EcsSharedMemory.MAX_PLAYERS; playerIndex++)
		{
			int entityId = _draftSiteRetirements[playerIndex];
			if (entityId <= 0) continue;

			_draftSiteRetirements[playerIndex] = 0;
			UnitId blueprint = new(entityId);
			if (blueprint.IsDead) continue;

			api.DestroyUnit(blueprint, executeDespawnShader: false, playDeathAnimation: false);
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

		int blueprintEntityId = _draftSiteEntities[playerIndex];
		UnitId blueprint = new(blueprintEntityId);
		bool hasBlueprint = blueprintEntityId > 0 && !blueprint.IsDead;

		for (int index = 0; index < SoldierProfiles.Length; index++)
		{
			if (!hasBlueprint) break;

			string abilityId = SoldierProfiles[index].BuildingTemplateId;
			bool visible = IsDraftOption(playerIndex, index);
			api.SetAbilityState(blueprint, abilityId, !visible, !visible);
		}

		bool draftOpen = hasBlueprint && _draftOpen[playerIndex] != 0;
		if (hasBlueprint) api.SetAbilityState(blueprint, AbandonConstructionAbilityId, !draftOpen, !draftOpen);

		// Begin Construction refuses while a draw is open, so leaving its card on the grid spent one of the twelve slots
		// on a command the player could not use and pushed the last offered package onto a second page.
		int heroEntityId = _heroEntities[playerIndex];
		if (heroEntityId > 0) api.SetAbilityState(new UnitId(heroEntityId), ConstructionAbilityId, draftOpen, draftOpen);
	}

	private bool IsDraftOption(int playerIndex, int profileIndex)
	{
		if (playerIndex <= 0 || playerIndex >= EcsSharedMemory.MAX_PLAYERS) return false;
		if (_draftOpen[playerIndex] == 0) return false;

		int optionCount = _draftOptionCounts[playerIndex];
		for (int index = 0; index < optionCount; index++)
		{
			if (_draftOptions[playerIndex * MaximumDraftOptions + index] == profileIndex) return true;
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
			RetireDraftSite(playerIndex);
			player.AdjustWood(BuildingWoodCost);
			player.ShowFeedbackText("The site could not be placed there.", new Vector3(1f, 0.3f, 0.25f));
			ApplyDraftAbilityState(_api, playerIndex);
			return;
		}

		_api.SetUnitRepairable(site, true);
		_api.SetUnitMana(site, 0f);
		RetireDraftSite(playerIndex);

		// The package leaves the shared deck only once its building actually stands, so a placement the map had to
		// refuse does not cost the deck one of its nine buildings for the rest of the match.
		if (!_openPoolDrafting) _deckTakenBuildings |= 1 << profileIndex;

		ApplyDraftAbilityState(_api, playerIndex);

		// A site is raised by the hero working at it, never by the site existing near the hero. Nothing is ordered here, so
		// the player right-clicks the site to send the hero to raise it, and walking away stops the work where it stood.
		player.ShowFeedbackText(
			$"{_api.GetUnitName(buildingTemplateId)} is under construction. Right-click the site to raise it.",
			new Vector3(0.85f, 0.9f, 0.6f));
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
		RetireDraftSite(playerIndex);
		ApplyDraftAbilityState(_api, playerIndex);

		PlayerId player = _api.GetPlayer(playerIndex);
		if (!player.IsActive) return;

		player.AdjustWood(BuildingWoodCost);
		player.ShowFeedbackText("Construction cancelled. Its 100 wood is returned.", new Vector3(0.75f, 0.75f, 0.75f));
	}

	private void ResetUnitRuntimeState(int entityId)
	{
		if (entityId <= 0 || entityId >= EcsSharedMemory.MAX_ENTITIES) return;

		_assignedTargets[entityId] = 0;
		_nextTargetTimes[entityId] = 0f;
		_nextAttackRefreshTimes[entityId] = 0f;
		_nextFormationOrderTimes[entityId] = 0f;
		_soldierControlled[entityId] = 0;
		_sourceBuildings[entityId] = 0;
		_publishedMaintainedSoldiers[entityId] = 0;
		_workerBuildingOf[entityId] = 0;
		_workerNodeOf[entityId] = 0;
		_workerResourceOf[entityId] = 0;
		_workerPausedOf[entityId] = 0;
		_burningFromMark[entityId] = 0;
		_cinderMarkDamage[entityId] = 0f;
		_mendTimers[entityId] = 0f;
		_wardingAegisTimers[entityId] = 0f;
		_standardAuraTimers[entityId] = 0f;
		_lastBadgeValues[entityId] = -1;
		_statedManaMax[entityId] = -1;
		_statedManaRegen[entityId] = -1f;
	}

	/// <summary>
	///     Keeps the hero's facing, experience and level while it stands, so a revival returns the progression the hero
	///     earned rather than resetting it. Facing is the direction the hero last walked, which is what the formation
	///     slots are built around.
	/// </summary>
	private void UpdateHeroMemory(IGameAPI api, PlayerId player)
	{
		int playerIndex = player.Value;
		int heroEntityId = _heroEntities[playerIndex];
		if (heroEntityId <= 0) return;

		UnitId hero = new(heroEntityId);
		if (hero.IsDead) return;

		_heroLevelsBeforeDeath[playerIndex] = Math.Max(1, hero.Level);
		_heroExperiences[playerIndex] = hero.Experience;

		Vector3 position = hero.Position;
		float deltaX = position.X - _heroLastPositionsX[playerIndex];
		float deltaZ = position.Z - _heroLastPositionsZ[playerIndex];
		if (deltaX * deltaX + deltaZ * deltaZ > 0.0025f) _heroFacing[playerIndex] = MathF.Atan2(deltaX, deltaZ);

		_heroLastPositionsX[playerIndex] = position.X;
		_heroLastPositionsZ[playerIndex] = position.Z;
	}

	/// <summary>
	///     Keeps the autonomous army in the slots its authored formation states and points each soldier at the target its
	///     authored targeting rule selects. The engine still performs the pathing, collision and attack resolution; this
	///     only decides where a soldier belongs and what it should fight, which is the behaviour the design places in the
	///     ruleset rather than in a player order.
	/// </summary>
	private void UpdateArmyBehaviour(IGameAPI api, PlayerId player)
	{
		int playerIndex = player.Value;
		int heroEntityId = _heroEntities[playerIndex];
		if (heroEntityId <= 0) return;

		UnitId hero = new(heroEntityId);
		if (hero.IsDead) return;

		ReadOnlySpan<UnitId> owned = player.GetUnits();
		int frontCount = 0;
		int flankCount = 0;
		int backCount = 0;

		for (int index = 0; index < owned.Length; index++)
		{
			UnitId soldier = owned[index];
			if (soldier.IsDead || soldier.IsHero || soldier.IsBuilding) continue;

			int profileIndex = FindProfileIndexBySoldier(soldier.TemplateID);
			if (profileIndex < 0) continue;

			if (_soldierControlled[soldier.UniqueId] == 0)
			{
				api.EnableUnitAutoCombat(soldier, false);
				api.SetUnitPlayerOrdersLocked(soldier, true);
				_soldierControlled[soldier.UniqueId] = 1;
			}

			SoldierProfile profile = SoldierProfiles[profileIndex];
			int slotIndex;
			switch (profile.Formation)
			{
				case SoldierFormation.Front:
					slotIndex = frontCount++;
					break;
				case SoldierFormation.Flank:
					slotIndex = flankCount++;
					break;
				default:
					slotIndex = backCount++;
					break;
			}

			Vector3 slot = ComputeFormationSlot(profile.Formation, slotIndex, hero.Position, _heroFacing[playerIndex]);
			UpdateSoldierBehaviour(api, player, hero, soldier, profile, slot);
		}
	}

	private static Vector3 ComputeFormationSlot(SoldierFormation formation, int slotIndex, Vector3 heroPosition, float facing)
	{
		float forwardX = MathF.Sin(facing);
		float forwardZ = MathF.Cos(facing);
		float rightX = MathF.Cos(facing);
		float rightZ = -MathF.Sin(facing);

		switch (formation)
		{
			case SoldierFormation.Front:
			{
				int row = slotIndex / FormationFrontSize;
				int column = slotIndex % FormationFrontSize - 1;
				float forward = FormationFrontDistance + row * FormationRowSpacing;
				float lateral = column * FormationColumnSpacing;
				return new Vector3(
					heroPosition.X + forwardX * forward + rightX * lateral,
					heroPosition.Y,
					heroPosition.Z + forwardZ * forward + rightZ * lateral);
			}
			case SoldierFormation.Flank:
			{
				int row = slotIndex / FormationFlankSize;
				int side = slotIndex % FormationFlankSize == 0 ? -1 : 1;
				float lateral = side * (FormationFlankDistance + row * FormationRowSpacing);
				float forward = row % 2 == 0 ? 0.8f : -0.8f;
				return new Vector3(
					heroPosition.X + rightX * lateral + forwardX * forward,
					heroPosition.Y,
					heroPosition.Z + rightZ * lateral + forwardZ * forward);
			}
			default:
			{
				int row = slotIndex / FormationFrontSize;
				int column = slotIndex % FormationFrontSize - 1;
				float behind = FormationBackDistance + row * FormationRowSpacing;
				float lateral = column * FormationColumnSpacing;
				return new Vector3(
					heroPosition.X - forwardX * behind + rightX * lateral,
					heroPosition.Y,
					heroPosition.Z - forwardZ * behind + rightZ * lateral);
			}
		}
	}

	private void UpdateSoldierBehaviour(IGameAPI api, PlayerId player, UnitId hero, UnitId soldier, in SoldierProfile profile, Vector3 slot)
	{
		int entityId = soldier.UniqueId;
		float now = api.GameElapsedTime;

		int targetEntityId = _assignedTargets[entityId];
		bool targetUsable = targetEntityId > 0 && IsTargetStillReachable(api, player.Value, targetEntityId, hero.Position);
		if (!targetUsable && targetEntityId != 0)
		{
			_assignedTargets[entityId] = 0;
			targetEntityId = 0;
		}

		if (targetEntityId == 0 && now >= _nextTargetTimes[entityId])
		{
			UnitId selected = SelectTarget(api, soldier, profile, player);
			if (selected.IsValid)
			{
				api.IssueAttackOrder(soldier, selected);
				_assignedTargets[entityId] = selected.UniqueId;
				_nextTargetTimes[entityId] = now + TargetRetrySeconds;
				_nextAttackRefreshTimes[entityId] = now + 1f;
				targetEntityId = selected.UniqueId;
				targetUsable = true;
			}
		}

		if (targetEntityId != 0 && targetUsable)
		{
			if (now >= _nextAttackRefreshTimes[entityId])
			{
				api.IssueAttackOrder(soldier, new UnitId(targetEntityId));
				_nextAttackRefreshTimes[entityId] = now + 1f;
			}

			return;
		}

		if (now < _nextFormationOrderTimes[entityId]) return;

		float heroDistanceSquared = Vector3.DistanceSquared(soldier.Position, hero.Position);
		float slotDistanceSquared = Vector3.DistanceSquared(soldier.Position, slot);
		if (slotDistanceSquared <= FormationSlotTolerance * FormationSlotTolerance && heroDistanceSquared <= FormationLeashDistance * FormationLeashDistance) return;

		api.IssueMoveOrder(soldier, slot);
		_nextFormationOrderTimes[entityId] = now + FormationOrderIntervalSeconds;
	}

	private bool IsTargetStillReachable(IGameAPI api, int playerIndex, int targetEntityId, Vector3 heroPosition)
	{
		var target = new UnitId(targetEntityId);
		if (!target.IsValid || target.IsDead) return false;
		if (api.ArePlayersAllied(target.Player, playerIndex)) return false;
		if (target.Invulnerable) return false;

		return Vector3.DistanceSquared(target.Position, heroPosition) <= EngageLeashDistance * EngageLeashDistance;
	}

	private UnitId SelectTarget(IGameAPI api, UnitId soldier, in SoldierProfile profile, PlayerId player)
	{
		float acquisitionRange = profile.AcquisitionRange;
		float acquisitionRangeSquared = acquisitionRange * acquisitionRange;
		float now = api.GameElapsedTime;

		if (profile.TargetingRule == SoldierTargetingRule.Standard
		    && now - _heroRecentTargetTimes[player.Value] <= StandardTargetMemorySeconds)
		{
			var remembered = new UnitId(_heroRecentTargets[player.Value]);
			if (IsTargetCandidate(api, remembered, player.Value, soldier.Position, acquisitionRangeSquared)) return remembered;
		}

		UnitId best = UnitId.Invalid;
		float bestScore = profile.TargetingRule == SoldierTargetingRule.Standard ? float.MaxValue : 0f;
		ReadOnlySpan<UnitId> candidates = api.GetUnitsInRange(soldier.Position, acquisitionRange);

		for (int index = 0; index < candidates.Length; index++)
		{
			UnitId candidate = candidates[index];
			if (!IsTargetCandidate(api, candidate, player.Value, soldier.Position, acquisitionRangeSquared)) continue;

			switch (profile.TargetingRule)
			{
				case SoldierTargetingRule.TankShredder:
				{
					float health = candidate.Health;
					if (!best.IsValid || health > bestScore || (health == bestScore && candidate.UniqueId < best.UniqueId))
					{
						best = candidate;
						bestScore = health;
					}

					break;
				}
				case SoldierTargetingRule.Assassin:
				{
					float health = candidate.Health;
					if (!best.IsValid || health < bestScore || (health == bestScore && candidate.UniqueId < best.UniqueId))
					{
						best = candidate;
						bestScore = health;
					}

					break;
				}
				default:
				{
					float distanceSquared = Vector3.DistanceSquared(candidate.Position, soldier.Position);
					if (!best.IsValid || distanceSquared < bestScore || (distanceSquared == bestScore && candidate.UniqueId < best.UniqueId))
					{
						best = candidate;
						bestScore = distanceSquared;
					}

					break;
				}
			}
		}

		return best;
	}

	private static bool IsTargetCandidate(IGameAPI api, UnitId candidate, int playerIndex, Vector3 soldierPosition, float acquisitionRangeSquared)
	{
		if (!candidate.IsValid || candidate.IsDead) return false;
		if (candidate.Player == playerIndex) return false;
		if (api.ArePlayersAllied(candidate.Player, playerIndex)) return false;
		if (candidate.Invulnerable) return false;
		if (candidate.IsBuilding && api.GetUnitConstructionProgress(candidate) < 1f) return false;

		return Vector3.DistanceSquared(candidate.Position, soldierPosition) <= acquisitionRangeSquared;
	}

	private static bool IsRelicAbility(string abilityId)
	{
		for (int index = 0; index < RelicIds.Length; index++)
		{
			if (RelicIds[index] == abilityId) return true;
		}

		return false;
	}

	private static void RegisterRelicVendor(IGameAPI api)
	{
		for (int index = 0; index < RelicIds.Length; index++)
		{
			string relicId = RelicIds[index];
			string tooltip = $"{RelicEffects[index]} Costs {RelicCost:0} gold and occupies one of the hero's six relic slots.";

			api.RegisterAbility(relicId, RelicNames[index], tooltip, string.Empty, true);
			api.AddUnitTypeAbility(NexusTemplateId, relicId);
			api.SetAbilityTooltip(relicId, tooltip);

			// The command grid is four columns by three rows. Six relics therefore fill the first row and the first two
			// slots of the second; putting them all on one row would silently drop the last two from the card.
			api.SetAbilityGridPosition(relicId, index % 4, index / 4);
			api.SetAbilityHotkey(relicId, RelicHotkeys[index]);
			api.SetItemTooltip(relicId, tooltip);
		}
	}

	private void BuyRelic(int playerIndex, string relicId)
	{
		int relicIndex = Array.IndexOf(RelicIds, relicId);
		if (relicIndex < 0) return;

		PlayerId player = _api.GetPlayer(playerIndex);
		if (!player.IsActive) return;

		int heroEntityId = _heroEntities[playerIndex];
		if (heroEntityId <= 0 || new UnitId(heroEntityId).IsDead)
		{
			player.ShowFeedbackText("Your hero must be alive to buy a relic.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		UnitId hero = new(heroEntityId);
		if (hero.HasItem(relicId))
		{
			player.ShowFeedbackText($"Your hero already holds {RelicNames[relicIndex]}.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		int heldRelics = CountRelics(hero);
		if (heldRelics >= RelicSlotCount)
		{
			player.ShowFeedbackText("All six relic slots are full.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		if (player.Gold < RelicCost)
		{
			player.ShowFeedbackText($"A relic costs {RelicCost:0} gold.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		player.AdjustGold(-RelicCost);
		if (!_api.AddItemToUnit(hero, relicId))
		{
			player.AdjustGold(RelicCost);
			player.ShowFeedbackText("The relic could not be equipped.", new Vector3(1f, 0.7f, 0.3f));
			return;
		}

		RefreshRelics(_api, player);
		ApplyRelicVendorState(_api, player);
		player.ShowFeedbackText($"{RelicNames[relicIndex]} is yours.", new Vector3(0.85f, 0.95f, 0.75f));
		player.PlayClickSound();
	}

	private static int CountRelics(UnitId hero)
	{
		int held = 0;
		for (int index = 0; index < RelicIds.Length; index++)
		{
			if (hero.HasItem(RelicIds[index])) held++;
		}

		return held;
	}

	private void ApplyRelicVendorState(IGameAPI api, PlayerId player)
	{
		int playerIndex = player.Value;
		int nexusEntityId = _nexusEntities[playerIndex];
		if (nexusEntityId <= 0) return;

		var nexus = new UnitId(nexusEntityId);
		if (nexus.IsDead) return;

		int heroEntityId = _heroEntities[playerIndex];
		var hero = new UnitId(heroEntityId);
		bool heroAlive = heroEntityId > 0 && !hero.IsDead;

		for (int index = 0; index < RelicIds.Length; index++)
		{
			bool owned = heroAlive && hero.HasItem(RelicIds[index]);
			api.SetAbilityState(nexus, RelicIds[index], owned || !heroAlive, false);
		}
	}

	private void PublishProductionBadge(IGameAPI api, PlayerId player, UnitId building, int available)
	{
		int entityId = building.UniqueId;
		if (_lastBadgeValues[entityId] == available) return;

		_lastBadgeValues[entityId] = available;
		api.SetUnitOverheadBadge(building, available, player.Color);
	}

	private void RestoreRelicsToHero(IGameAPI api, int playerIndex, UnitId hero)
	{
		byte flags = _relicFlags[playerIndex];
		for (int index = 0; index < RelicIds.Length; index++)
		{
			if ((flags & (1 << index)) == 0) continue;

			api.AddItemToUnit(hero, RelicIds[index]);
		}
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

		ApplyRelicVendorState(api, player);

		if ((flags & (1 << 0)) != 0 && (previousFlags & (1 << 0)) == 0)
		{
			ApplySigilOfTheWall(api, player);
		}

		if ((flags & (1 << 4)) != 0 && (previousFlags & (1 << 4)) == 0)
		{
			ApplyWardensWrit(api, player);
		}

		if ((flags & (1 << 3)) != 0 && (previousFlags & (1 << 3)) == 0)
		{
			ApplyDeepGranaries(api, player);
		}
	}

	private void ApplyDeepGranaries(IGameAPI api, PlayerId player)
	{
		ReadOnlySpan<UnitId> owned = player.GetUnits();
		for (int index = 0; index < owned.Length; index++)
		{
			UnitId unit = owned[index];
			if (unit.IsDead) continue;
			if (_workerBuildingOf[unit.UniqueId] <= 0) continue;

			api.AddBuff(unit, DeepGranariesWorkerBuffId, PermanentBuffSeconds);
			PublishDeepGranariesWorkerText(api, unit);
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

			api.AddStatModifier(unit, "Tenacity", false, 0.1f, "relic/wardens_writ");
		}
	}

	private void ApplyInnateToUnit(IGameAPI api, int playerIndex, UnitId unit)
	{
		if (!unit.IsValid) return;

		switch (_heroTemplateIndices[playerIndex])
		{
			case 0:
				// Unbroken is a player-wide rule, so it belongs on every owned unit, not only the ones that fight.
				api.AddStatModifier(unit, "Armor", false, 6f, "innate/unbroken");
				api.AddStatModifier(unit, "Tenacity", true, 0.1f, "innate/unbroken");
				break;
			case 1:
				// Cinder Mark is the army's on-hit rule. Workers cannot strike an enemy and buildings never attack, so
				// the marker stays on the combatants instead of describing a burn they cannot apply.
				if (unit.IsBuilding || _workerBuildingOf[unit.UniqueId] > 0) break;

				unit.AddBuff("buff/cinder_mark", 2f);
				int sableEntityId = _heroEntities[playerIndex];
				float burnPerSecond = sableEntityId > 0 ? CinderMarkEffectiveBurnPerSecond(api, new UnitId(sableEntityId)) : CinderMarkDamagePerSecond;
				PublishCinderMarkText(api, unit, burnPerSecond);
				break;
			case 2:
				// Standing Ledger is a production-room rule, not a combat stat. Putting its extra production on the
				// MaxMana stat lets the stat pipeline keep the larger pool instead of overwriting the map's number
				// with the template's smaller one on the next recalculation.
				if (!unit.IsBuilding || FindProfileIndexByBuilding(unit.TemplateID) < 0) break;

				api.AddStatModifier(unit, "MaxMana", false, ManaPerProduction, "innate/standing_ledger");
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
			api.AddStatModifier(unit, "Tenacity", false, 0.1f, "relic/wardens_writ");
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

	/// <summary>
	///     Answers how fast a building's worker productions come back. The Courier's Knot shortens every recharge, so it
	///     applies here as well as to the soldier productions; the Muster Bell states soldier recharge and therefore does not
	///     touch the worker crew's own clock.
	/// </summary>
	/// <param name="playerIndex">The seat the building belongs to.</param>
	/// <returns>The multiplier applied to the authored worker recharge.</returns>
	private float ResolveWorkerRechargeMultiplier(int playerIndex)
	{
		return HasRelic(playerIndex, 1) ? 0.9f : 1f;
	}

	private static float ResolveConstructionSeconds(string templateId)
	{
		int profileIndex = FindProfileIndexByBuilding(templateId);
		return profileIndex < 0 ? 30f : ConstructionSecondsByProfile[profileIndex];
	}

	private static bool IsBuildingOperational(IGameAPI api, UnitId building)
	{
		if (building.IsDormant || building.Health <= 0f) return false;

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
		return profileIndex >= 0 && SoldierProfiles[profileIndex].Formation == SoldierFormation.Front;
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
