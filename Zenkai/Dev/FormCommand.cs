using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Zenkai.Common.Forms;
using Zenkai.Common.Players;

namespace Zenkai.Dev;

/// <summary>
/// Dev-only chat command for testing forms before their unlock triggers and trainers exist.
///   /zform list                       every form with unlock state and mastery
///   /zform unlock &lt;name|all&gt;         unlock quietly
///   /zform awaken &lt;name&gt;             unlock with the dramatic first-time sequence
///   /zform lock &lt;name|all&gt;           lock again
///   /zform mastery &lt;name&gt; &lt;0-100&gt;    set mastery in percent
///   /zform revert                     drop out of the current form
/// Phase 10 gates this behind a config option.
/// </summary>
public class FormCommand : ModCommand
{
	public override CommandType Type => CommandType.Chat;
	public override string Command => "zform";
	public override string Usage => "/zform list | unlock <name|all> | awaken <name> | lock <name|all> | mastery <name> <0-100> | revert";
	public override string Description => "Zenkai dev tools for transformations";

	public override void Action(CommandCaller caller, string input, string[] args)
	{
		FormPlayer fp = caller.Player.GetModPlayer<FormPlayer>();
		if (args.Length == 0)
			throw new UsageException();

		switch (args[0].ToLowerInvariant()) {
			case "list":
				foreach (ModForm f in FormLoader.Ordered)
					caller.Reply($"{(fp.IsUnlocked(f) ? "[x]" : "[ ]")} {f.Name} tier {f.Tier}, mastery {fp.Mastery(f) * 100f:0}%{(fp.Active == f ? " (active)" : "")}");
				break;

			case "unlock":
			case "lock":
				if (args.Length < 2)
					throw new UsageException();
				foreach (ModForm f in Match(args[1])) {
					if (args[0].ToLowerInvariant() == "unlock")
						fp.Unlock(f, dramatic: false);
					else
						fp.Lock(f);
					caller.Reply($"{args[0]}ed {f.Name}");
				}
				break;

			case "awaken":
				if (args.Length < 2 || Match(args[1]).FirstOrDefault() is not ModForm form)
					throw new UsageException();
				fp.Lock(form);
				fp.Unlock(form, dramatic: true);
				break;

			case "mastery":
				if (args.Length < 3 || Match(args[1]).FirstOrDefault() is not ModForm target || !float.TryParse(args[2], out float pct))
					throw new UsageException();
				fp.SetMastery(target, pct / 100f);
				caller.Reply($"{target.Name} mastery {pct:0}%");
				break;

			case "revert":
				fp.Revert(false);
				break;

			default:
				throw new UsageException();
		}
	}

	static ModForm[] Match(string name) =>
		name.ToLowerInvariant() == "all"
			? FormLoader.Forms.ToArray()
			: FormLoader.Forms.Where(f => f.Name.ToLowerInvariant() == name.ToLowerInvariant()).ToArray();
}
