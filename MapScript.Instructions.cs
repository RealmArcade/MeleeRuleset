namespace Realm.Maps;

using Realm.MapAPI;

public partial class MapScript
{
	/// <summary>
	///     Publishes the rules a player reads in the instructions panel. Every section is written from the same numbers
	///     the script plays by, so the panel cannot drift from the match it describes.
	/// </summary>
	private void RegisterInstructions(IGameAPI api)
	{
		api.AddInstructionsSection(
			"How a Match Works",
			"Use your hero to gather resources, construct buildings, kill monsters for bounties and experience, and destroy the enemy's base\n\nEach hero has different abilities, strengths, and weaknesses.\n\n");

		api.AddInstructionsSection(
			"Resources",
			"You can gather Gold and Wood with your hero from the nodes spread across the map. Each constructed building also automatically sends workers to gather at the nearest node.\n\nWood pays for buildings. Gold pays for relics.\n\n");

		api.AddInstructionsSection(
			"Constructing Buildings",
			"Buildings simultaneously grant protection, army, and economy.\n\nEach additional building increases the global armor of your entire base.\n\nBuildings automatically send soldiers to fight with your hero.\n\nBuildings automatically send workers to gather resources.\n\nBuildings are drafted from a shared deck, changing your strategy each game.\n\nEach building (and the soldiers and workers they spawn) have their own strengths and weaknesses.\n\nDestroy your enemies buildings to shrink their army, defenses, and economy.");

		api.AddInstructionsSection(
			"Nexus",
			"This is your main starting building and your hero's respawn point. Defend it at all costs. Destroy the enemy Nexus to win.\n\nRelics can be purchased from the Nexus which grant you extra bonuses\n\n");
	}
}
