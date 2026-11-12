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
			"Use your hero to gather resources, construct buildings, kill monsters for bounties and experience, and destroy the enemy's base\n\n"
			+ "Each hero has different abilities, strengths, and weaknesses.\n\n"
			);

		api.AddInstructionsSection(
			"Resources",
			"You can gather Gold and Wood with your hero from the nodes spread across the map. Each constructed building also automatically sends workers to gather at the nearest node.\n\n"
			+ "Wood pays for buildings. Gold pays for relics.\n\n");

		api.AddInstructionsSection(
			"Constructing Buildings",
			"Buildings simultaneously grant protection, army, and economy.\n\n"
			+ "Each additional building increases the global armor of your entire base. Kill enemy outer buildings before the Nexus to make it more vulnerable.\n\n"
			+ "Buildings automatically send soldiers to fight with your hero.\n\n"
			+ "Buildings automatically send workers to gather resources.\n\n"
			+ "Buildings are drafted from a shared deck, changing your strategy each game.\n\n"
			+ "Each building (and the soldiers and workers they spawn) have their own strengths and weaknesses.\n\n"
			+ "Destroy your enemies buildings to shrink their army, defenses, and economy.");

		api.AddInstructionsSection(
			"Combat",
			"Soldiers follow the hero and fight autonomously, each with its own stats, abilities, and positioning.\n\n"
			+ "Each additional soldier increases the defenses of your hero. Kill the enemy soldiers first to make their hero more vulnerable.\n\n");

		api.AddInstructionsSection(
			"Nexus",
			"This is your main starting building and your hero's respawn point. Defend it at all costs. Destroy the enemy Nexus to win.\n\n"
			+ "Relics can be purchased from the Nexus which grant you extra bonuses\n\n");
	}
}
