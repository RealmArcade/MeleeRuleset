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
			"You command one hero and the army that hero's buildings raise. The army fights on its own: you never select a soldier or give it an order.\n\n"
			+ "Destroy every enemy Nexus to win. Your own Nexus is what keeps you in the match, and it is also where your hero revives.");

		api.AddInstructionsSection(
			"Gold, Wood and Workers",
			"Gold and wood come from the nodes on the map. Each building raises its own worker crew, and every worker walks to the nearest node of the kind its building works.\n\n"
			+ "Wood pays for buildings. Gold pays for relics and is also carried home by gold crews. A worker that cannot carry another piece takes what it has to the Nexus before returning.\n\n"
			+ "Your hero gathers too: pick Gather from the command card and right-click a tree or a mine. The hero never wanders off to gather on its own, so a hero is only gathering when you told it to.");

		api.AddInstructionsSection(
			"Drafting Buildings",
			$"Begin Construction costs {BuildingWoodCost:0} wood and draws up to {_draftOptionsPerDraw} buildings from the shared deck. Pick one to draw its site where you stand, then right-click the site with your hero to raise it.\n\n"
			+ "A building that is taken leaves the deck for everyone until the deck is replenished. Abandon Construction gives the wood back while the site is still open.");

		api.AddInstructionsSection(
			"Building Supply",
			$"Each building you raise costs one supply, and you may hold {_maximumBuildingsPerPlayer} buildings at a time. Your Nexus is free and does not count against that limit.\n\n"
			+ "Buildings are also your army's only source of soldiers and workers, so the supply cap is what keeps a wide base from becoming an unlimited army.");

		api.AddInstructionsSection(
			"Production Mana",
			"Every building stores mana and spends it on what it raises. Two abilities sit on the building: one raises its soldier, one raises its worker crew. Both draw from the same pool, so a building that is busy raising workers has less to spend on soldiers.\n\n"
			+ "Auto-cast is on when a building is raised. Right-click either card to switch that ability off or back on, and a marching frame shows which cards are auto-casting. The frame follows the building, not the click, so it always states what the building will do next.\n\n"
			+ "A card can also be pressed by hand when its ability is off. Cooldown reduction on the building speeds both lines up, and a relic or innate that shortens cooldown applies to every building you own.");

		api.AddInstructionsSection(
			"Soldiers Fight For You",
			"Soldiers are raised beside the building that fields them and walk to your hero. From there they hold their formation: Front soldiers ahead of the hero, Flank soldiers to either side, Back soldiers behind.\n\n"
			+ "Each soldier type also carries a targeting rule. Standard soldiers focus whatever the hero is fighting, Tank-Shredders go for the healthiest enemy, and Assassins finish the weakest one. Army composition is therefore a choice about coverage and about what your army will prioritise.");

		api.AddInstructionsSection(
			"Relics",
			$"Relics are bought at the Nexus for {RelicCost:0} gold and equipped in one of your hero's {RelicSlotCount} slots. They are player-wide upgrades: every soldier and every building you own benefits from the rule a relic changes.\n\n"
			+ "Your hero's innate is always on and cannot be toggled. Together with relics it is the whole upgrade path; there is no research tree.");

		api.AddInstructionsSection(
			"Veterancy",
			"Every enemy unit, building and neutral monster pays a bounty of experience and gold when it dies. Experience raises your hero's level, and every soldier you own always matches that level, so fighting is how the whole army gets stronger.");

		api.AddInstructionsSection(
			"Victory",
			"When a Nexus falls its seat is out of the match, unless an ally on the same team still holds one. The last team with a Nexus standing wins.");
	}
}
