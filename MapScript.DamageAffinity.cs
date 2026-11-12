namespace Realm.Maps;

using System;
using System.Numerics;
using Realm.Ecs;
using Realm.MapAPI;

/// <summary>
///     The free-for-all anti-kingmaker rule: the damage a team deals to another is scaled by how much of that other
///     team's current fight the attacking team is actually a part of. A duel stays at full effectiveness, a team that
///     joins an existing fight starts out nearly harmless and grows into full strength as it contributes, and every
///     contribution fades over half a minute so a retreating or repairing enemy is not immediately worth full damage
///     again.
/// </summary>
public partial class MapScript
{
	private const float AffinityHalfLifeSeconds = 30f;
	private const float AffinityRecalculateSeconds = 1f;
	private const float AffinityMinimumBlowDamage = 5f;
	private const float AffinityMinimumEffectiveness = 0.05f;
	private const float AffinityFullEffectiveness = 1f;
	private const float AffinityShieldFlashCooldownSeconds = 0.6f;
	private const float AffinityShieldFlashRadius = 2.6f;
	private const float AffinityThreeWayExponent = 0.38f;

	private readonly float[] _affinityEnergy = new float[EcsSharedMemory.MAX_PLAYERS * EcsSharedMemory.MAX_PLAYERS];
	private readonly double[] _affinityTouchedAt = new double[EcsSharedMemory.MAX_PLAYERS * EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _affinityApplied = new float[EcsSharedMemory.MAX_PLAYERS * EcsSharedMemory.MAX_PLAYERS];
	private readonly float[] _affinityShieldFlashedAt = new float[EcsSharedMemory.MAX_ENTITIES];
	private double _affinityClock;
	private float _affinityRecalculateTimer;

	/// <summary>
	///     Starts the rule and states that every pair begins at full effectiveness, so a board that never sees a third
	///     party fights exactly as the engine would have it fight.
	/// </summary>
	/// <param name="api">The game API reference to manipulate the game world.</param>
	private void InitializeDamageAffinity(IGameAPI api)
	{
		_affinityClock = api.GameElapsedTime;
		_affinityRecalculateTimer = 0f;

		for (int index = 0; index < _affinityApplied.Length; index++)
		{
			_affinityApplied[index] = -1f;
		}

		for (int index = 0; index < _affinityShieldFlashedAt.Length; index++)
		{
			_affinityShieldFlashedAt[index] = -AffinityShieldFlashCooldownSeconds;
		}

		api.OnUnitDamagedBeforeMitigation += HandleUnitDamagedBeforeMitigation;
	}

	/// <summary>
	///     Decays every pair's participation and restates the damage effectiveness the engine applies, once per
	///     recalculation interval so the cost does not follow the tick rate.
	/// </summary>
	/// <param name="api">The game API reference to manipulate the game world.</param>
	/// <param name="delta">The time elapsed since the last physics tick, in seconds.</param>
	private void UpdateDamageAffinity(IGameAPI api, float delta)
	{
		_affinityClock += delta;
		_affinityRecalculateTimer += delta;
		if (_affinityRecalculateTimer < AffinityRecalculateSeconds) return;

		float elapsed = _affinityRecalculateTimer;
		_affinityRecalculateTimer = 0f;
		DecayParticipation(elapsed);
		ApplyDamageEffectiveness(api);
	}

	/// <summary>
	///     Weighs one blow as participation between the attacker's team and the victim's team. Only damage carried before
	///     the victim's resistances count, so hitting an armoured target does not move a fight's standing less than hitting
	///     a soft one, and a blow too small to matter is ignored entirely so a nuisance cannot steer the rule for free.
	/// </summary>
	/// <param name="victim">The unit that was hit.</param>
	/// <param name="attacker">The unit that landed the blow, or an invalid identifier when nothing did.</param>
	/// <param name="damageBeforeMitigation">The damage the blow carried before the victim's resistances reduced it.</param>
	private void HandleUnitDamagedBeforeMitigation(UnitId victim, UnitId attacker, float damageBeforeMitigation)
	{
		if (!victim.IsValid || !attacker.IsValid || damageBeforeMitigation < AffinityMinimumBlowDamage) return;

		int victimPlayerIndex = victim.Player;
		int attackerPlayerIndex = attacker.Player;
		if (victimPlayerIndex <= 0 || attackerPlayerIndex <= 0) return;

		PlayerId victimPlayer = _api.GetPlayer(victimPlayerIndex);
		PlayerId attackerPlayer = _api.GetPlayer(attackerPlayerIndex);
		if (!victimPlayer.IsActive || !attackerPlayer.IsActive) return;

		int victimTeam = victimPlayer.Team;
		int attackerTeam = attackerPlayer.Team;
		if (victimTeam <= 0 || attackerTeam <= 0 || victimTeam == attackerTeam) return;

		AddParticipation(attackerTeam, victimTeam, damageBeforeMitigation);
		ShowDamageAffinityShield(victim, attackerTeam, victimTeam);
	}

	/// <summary>Adds damage to a pair's participation, after fading what the pair had already accumulated.</summary>
	/// <param name="firstTeam">One team of the pair.</param>
	/// <param name="secondTeam">The other team of the pair.</param>
	/// <param name="damage">The pre-mitigation damage exchanged.</param>
	private void AddParticipation(int firstTeam, int secondTeam, float damage)
	{
		int forward = PairIndex(firstTeam, secondTeam);
		if (forward < 0) return;

		DecayParticipationAt(forward, _affinityClock);
		DecayParticipationAt(PairIndex(secondTeam, firstTeam), _affinityClock);

		_affinityEnergy[forward] += damage;
		_affinityEnergy[PairIndex(secondTeam, firstTeam)] += damage;
	}

	/// <summary>Fades every pair's participation by the time that passed, keeping the half life the rule states.</summary>
	/// <param name="elapsedSeconds">The seconds since the participation was last faded.</param>
	private void DecayParticipation(float elapsedSeconds)
	{
		for (int index = 0; index < _affinityEnergy.Length; index++)
		{
			if (_affinityEnergy[index] <= 0f) continue;
			DecayParticipationAt(index, _affinityClock);
		}
	}

	private void DecayParticipationAt(int pairIndex, double now)
	{
		float energy = _affinityEnergy[pairIndex];
		if (energy <= 0f) return;

		double touchedAt = _affinityTouchedAt[pairIndex];
		double elapsed = now - touchedAt;
		if (touchedAt <= 0d || elapsed <= 0d)
		{
			_affinityTouchedAt[pairIndex] = now;
			return;
		}

		float faded = energy * MathF.Pow(0.5f, (float)(elapsed / AffinityHalfLifeSeconds));
		_affinityEnergy[pairIndex] = faded < 0.01f ? 0f : faded;
		_affinityTouchedAt[pairIndex] = now;
	}

	/// <summary>
	///     Answers the multiplier a team's damage carries against another, from the share of that team's exposure the pair
	///     accounts for. A duel answers full effectiveness; a team joining a fight answers almost nothing and climbs as it
	///     contributes, while the teams already fighting each lose ground to the newcomer. The exact curve is a tuning
	///     value, so it is stated here rather than in the engine.
	/// </summary>
	/// <param name="attackingTeam">The team whose damage is being scaled.</param>
	/// <param name="defendingTeam">The team the damage is dealt to.</param>
	/// <returns>The multiplier, between the floor and full effectiveness.</returns>
	private float ResolveAffinityEffectiveness(int attackingTeam, int defendingTeam)
	{
		int pair = PairIndex(attackingTeam, defendingTeam);
		if (pair < 0) return AffinityFullEffectiveness;

		float pairEnergy = _affinityEnergy[pair];

		float exposure = 0f;
		bool attackersFightEachOther = false;
		for (int otherTeam = 1; otherTeam < EcsSharedMemory.MAX_PLAYERS; otherTeam++)
		{
			if (otherTeam == defendingTeam) continue;
			if (otherTeam == attackingTeam) continue;

			exposure += _affinityEnergy[PairIndex(otherTeam, defendingTeam)];
			if (!attackersFightEachOther && (_affinityEnergy[PairIndex(attackingTeam, otherTeam)] > 0f || _affinityEnergy[PairIndex(otherTeam, attackingTeam)] > 0f))
			{
				attackersFightEachOther = true;
			}
		}

		// A pair that has never exchanged a blow against a defender nobody else is fighting is an opening duel, and an
		// opening duel is fought at full effectiveness. Only a team that arrives on a fight already in progress counts as
		// joining one, and it starts out nearly harmless rather than being handed a share it never earned.
		if (pairEnergy <= 0f) return exposure > 0f ? AffinityMinimumEffectiveness : AffinityFullEffectiveness;

		if (exposure <= 0f) return AffinityFullEffectiveness;

		float exposureTotal = pairEnergy + exposure;
		float share = Math.Clamp(pairEnergy / exposureTotal, 0f, 1f);

		// Two teams that have both joined one defender are each fighting half the fight, so their damage is halved. When
		// those two attackers are also fighting each other, the engagement is a three-way one and every participant keeps
		// more of its strength, which is what makes a true three-way fight less punishing than a two-on-one.
		float exponent = attackersFightEachOther ? AffinityThreeWayExponent : 1f;
		float effectiveness = MathF.Pow(share, exponent);

		return Math.Clamp(effectiveness, AffinityMinimumEffectiveness, AffinityFullEffectiveness);
	}

	/// <summary>
	///     Restates the effectiveness every seat's damage carries against every other seat it is at war with. A pair is
	///     only written when its answer changed enough to be worth a host command, because the table is applied to every
	///     blow the engine resolves and the rule recalculates once a second.
	/// </summary>
	/// <param name="api">The game API reference to manipulate the game world.</param>
	private void ApplyDamageEffectiveness(IGameAPI api)
	{
		ReadOnlySpan<PlayerId> players = api.GetActivePlayers();

		for (int attackerIndex = 0; attackerIndex < players.Length; attackerIndex++)
		{
			PlayerId attacker = players[attackerIndex];
			if (attacker.Value <= 0 || !attacker.IsActive) continue;

			for (int defenderIndex = 0; defenderIndex < players.Length; defenderIndex++)
			{
				PlayerId defender = players[defenderIndex];
				if (defender.Value <= 0 || !defender.IsActive) continue;
				if (defender.Value == attacker.Value) continue;
				if (attacker.Team <= 0 || attacker.Team == defender.Team) continue;

				int pair = PairIndex(attacker.Value, defender.Value);
				if (pair < 0) continue;

				float effectiveness = ResolveAffinityEffectiveness(attacker.Team, defender.Team);
				if (MathF.Abs(_affinityApplied[pair] - effectiveness) < 0.001f) continue;

				_affinityApplied[pair] = effectiveness;
				api.SetPlayerDamageEffectiveness(attacker.Value, defender.Value, effectiveness);
			}
		}
	}

	/// <summary>
	///     Shows the reduction a team's blows are carrying as a gradient shield around the victim, tinted from pale at full
	///     effectiveness to deep blue as the reduction deepens, so a player can read roughly how much a fight is costing.
	/// </summary>
	/// <param name="victim">The unit that was hit.</param>
	/// <param name="attackingTeam">The team that landed the blow.</param>
	/// <param name="defendingTeam">The team the victim plays for.</param>
	private void ShowDamageAffinityShield(UnitId victim, int attackingTeam, int defendingTeam)
	{
		float effectiveness = ResolveAffinityEffectiveness(attackingTeam, defendingTeam);
		if (effectiveness >= 0.999f) return;

		int entityId = victim.UniqueId;
		if (entityId < 0 || entityId >= _affinityShieldFlashedAt.Length) return;
		if (_affinityClock - _affinityShieldFlashedAt[entityId] < AffinityShieldFlashCooldownSeconds) return;

		_affinityShieldFlashedAt[entityId] = (float)_affinityClock;

		float deepness = 1f - effectiveness;
		var color = new Vector3(
			0.35f + 0.65f * (1f - deepness),
			0.55f + 0.45f * (1f - deepness),
			0.85f + 0.15f * (1f - deepness));

		_api.SpawnExpandingBurstSphere(victim.Position, AffinityShieldFlashRadius * (0.7f + 0.6f * deepness), 9f, 0.55f, color);
	}

	private static int PairIndex(int firstPlayerIndex, int secondPlayerIndex)
	{
		if (firstPlayerIndex <= 0 || secondPlayerIndex <= 0) return -1;
		if (firstPlayerIndex >= EcsSharedMemory.MAX_PLAYERS || secondPlayerIndex >= EcsSharedMemory.MAX_PLAYERS) return -1;

		return firstPlayerIndex * EcsSharedMemory.MAX_PLAYERS + secondPlayerIndex;
	}
}
