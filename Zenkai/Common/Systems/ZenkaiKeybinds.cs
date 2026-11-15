using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Zenkai.Common.Systems;

/// <summary>
/// Keybinds. Gotcha: in tModLoader 1.4.4 a mod keybind starts unbound; the default key below is only
/// applied when the player resets it in Settings, Controls. <see cref="KeybindReminderPlayer"/> tells
/// the player once per session if any are unbound.
/// </summary>
public class ZenkaiKeybinds : ModSystem
{
	public static ModKeybind ChargeKi { get; private set; }
	public static ModKeybind ToggleFlight { get; private set; }
	public static ModKeybind[] SkillSlots { get; private set; }
	public static ModKeybind SkillMenu { get; private set; }
	public static ModKeybind Transform { get; private set; }
	public static ModKeybind Revert { get; private set; }

	/// <summary>Internal name and default key of every keybind. Slot, menu and form keys chosen by the user on 2026-11-14.</summary>
	public static readonly (string Name, string Key)[] Defaults = {
		("ChargeKi", "C"),
		("ToggleFlight", "F"),
		("SkillSlot1", "Z"),
		("SkillSlot2", "X"),
		("SkillSlot3", "V"),
		("SkillSlot4", "G"),
		("SkillMenu", "K"),
		("Transform", "T"),
		("Revert", "Y"),
	};

	public static IEnumerable<ModKeybind> All => new[] { ChargeKi, ToggleFlight, SkillMenu, Transform, Revert }.Concat(SkillSlots);

	public override void Load()
	{
		var binds = Defaults.ToDictionary(d => d.Name, d => KeybindLoader.RegisterKeybind(Mod, d.Name, d.Key));
		ChargeKi = binds["ChargeKi"];
		ToggleFlight = binds["ToggleFlight"];
		SkillSlots = new[] { binds["SkillSlot1"], binds["SkillSlot2"], binds["SkillSlot3"], binds["SkillSlot4"] };
		SkillMenu = binds["SkillMenu"];
		Transform = binds["Transform"];
		Revert = binds["Revert"];
	}

	public override void Unload()
	{
		ChargeKi = null;
		ToggleFlight = null;
		SkillSlots = null;
		SkillMenu = null;
		Transform = null;
		Revert = null;
	}
}

public class KeybindReminderPlayer : ModPlayer
{
	static bool remindedThisSession;

	public override void OnEnterWorld()
	{
		if (remindedThisSession || Player.whoAmI != Main.myPlayer || Dev.ShotRunner.Active)
			return;
		int unbound = ZenkaiKeybinds.All.Count(k => k.GetAssignedKeys(InputMode.Keyboard).Count == 0);
		if (unbound == 0)
			return;
		remindedThisSession = true;
		Main.NewText(Language.GetTextValue("Mods.Zenkai.Keybinds.Unbound", unbound), 255, 200, 90);
	}
}
