using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;

namespace Zenkai.Dev;

/// <summary>
/// Dev-only chat command for testing skills before trainers exist (phase 5).
///   /zskill list                 every skill, learned ones marked
///   /zskill grant &lt;name|all&gt;     learn a skill (also fills an empty slot)
///   /zskill revoke &lt;name|all&gt;    forget a skill
///   /zskill slot &lt;1-8&gt; &lt;name|none&gt; put a learned skill in a bar slot
///   /zskill secondary &lt;name|none&gt; put a learned skill on the right mouse button
///   /zskill ki &lt;amount&gt;          set current ki
///   /zskill dummy                place a target dummy a few tiles in front of you
/// Phase 10 gates this behind a config option.
/// </summary>
public class SkillCommand : ModCommand
{
	public override CommandType Type => CommandType.Chat;
	public override string Command => "zskill";
	public override string Usage => "/zskill list | grant <name|all> | revoke <name|all> | slot <1-8> <name|none> | secondary <name|none> | ki <amount> | dummy";
	public override string Description => "Zenkai dev tools for skills";

	public override void Action(CommandCaller caller, string input, string[] args)
	{
		Player player = caller.Player;
		SkillPlayer sp = player.GetModPlayer<SkillPlayer>();
		if (args.Length == 0) {
			caller.Reply(Usage, Color.Orange);
			return;
		}

		switch (args[0].ToLowerInvariant()) {
			case "list":
				foreach (ModSkill s in SkillLoader.Ordered)
					caller.Reply($"{(sp.Knows(s) ? "[x]" : "[ ]")} {s.Name} ({s.Kind}, {s.KiCost} ki)");
				caller.Reply("Slots: " + string.Join(", ", sp.Loadout.Select((n, i) => $"{i + 1}={sp.SkillInSlot(i)?.Name ?? "-"}"))
					+ $", RMB={sp.SecondarySkill?.Name ?? "-"}");
				break;

			case "grant":
			case "revoke":
				if (args.Length < 2)
					throw new UsageException();
				bool grant = args[0].ToLowerInvariant() == "grant";
				foreach (ModSkill s in Match(args[1])) {
					if (grant)
						sp.Learn(s);
					else
						sp.Forget(s);
					caller.Reply($"{(grant ? "Learned" : "Forgot")} {s.Name}");
				}
				break;

			case "slot":
				if (args.Length < 3 || !int.TryParse(args[1], out int slot) || slot < 1 || slot > SkillPlayer.SlotCount)
					throw new UsageException();
				if (args[2].ToLowerInvariant() == "none") {
					sp.SetSlot(slot - 1, null);
				}
				else {
					ModSkill s = Match(args[2]).FirstOrDefault();
					if (s == null || !sp.Knows(s)) {
						caller.Reply("Unknown or unlearned skill", Color.Red);
						return;
					}
					sp.SetSlot(slot - 1, s);
				}
				caller.Reply($"Slot {slot} = {sp.SkillInSlot(slot - 1)?.Name ?? "-"}");
				break;

			case "secondary":
				if (args.Length < 2)
					throw new UsageException();
				if (args[1].ToLowerInvariant() == "none") {
					sp.SetSecondary(null);
				}
				else {
					ModSkill s = Match(args[1]).FirstOrDefault();
					if (s == null || !sp.Knows(s)) {
						caller.Reply("Unknown or unlearned skill", Color.Red);
						return;
					}
					sp.SetSecondary(s);
				}
				caller.Reply($"Right button = {sp.SecondarySkill?.Name ?? "-"}");
				break;

			case "ki":
				if (args.Length < 2 || !float.TryParse(args[1], out float amount))
					throw new UsageException();
				KiPlayer ki = player.GetModPlayer<KiPlayer>();
				ki.Ki = MathHelper.Clamp(amount, 0f, ki.MaxKi);
				break;

			case "dummy":
				PlaceDummy(player, 6);
				break;

			default:
				throw new UsageException();
		}
	}

	static ModSkill[] Match(string name) =>
		name.ToLowerInvariant() == "all"
			? SkillLoader.Skills.ToArray()
			: SkillLoader.Skills.Where(s => s.Name.ToLowerInvariant() == name.ToLowerInvariant()).ToArray();

	/// <summary>Places a vanilla Target Dummy on the ground a few tiles in front of the player.
	/// Shared with the shot runner. Returns false if no spot was found.</summary>
	public static bool PlaceDummy(Player player, int tilesAhead)
	{
		int x = (int)(player.Center.X / 16f) + player.direction * tilesAhead;
		int y = (int)(player.Bottom.Y / 16f) - 1;
		// Find the floor: climb out of any hill, then walk down to the first solid tile and stand on it.
		for (int i = 0; i < 30 && WorldGen.SolidTile(x, y); i++)
			y--;
		for (int i = 0; i < 30 && !WorldGen.SolidTile(x, y + 1); i++)
			y++;
		// Clear grass, flowers and other non-solid clutter where the 2x3 dummy goes.
		for (int tx = x - 1; tx <= x + 1; tx++) {
			for (int ty = y - 2; ty <= y; ty++) {
				if (Main.tile[tx, ty].HasTile && !WorldGen.SolidTile(tx, ty))
					WorldGen.KillTile(tx, ty, noItem: true);
			}
		}
		bool ok = WorldGen.PlaceObject(x, y, TileID.TargetDummy, false, 0, 0, -1, player.direction);
		if (!ok)
			return false;
		// PlaceObject sets the tiles only; the tile entity that spawns the dummy NPC is added by the
		// placing player's hook in vanilla, so add it here at the dummy's top-left tile.
		for (int tx = x - 2; tx <= x + 2; tx++) {
			for (int ty = y - 4; ty <= y; ty++) {
				Tile tile = Main.tile[tx, ty];
				if (tile.HasTile && tile.TileType == TileID.TargetDummy && tile.TileFrameY == 0 && tile.TileFrameX % 36 == 0) {
					Terraria.GameContent.Tile_Entities.TETrainingDummy.Place(tx, ty);
					return true;
				}
			}
		}
		return true;
	}
}
