using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Zenkai.Common.Systems;

/// <summary>
/// Keybinds. Skills have no keys of their own: the ki stance key hands the mouse and the hotbar
/// keys to the skill bar (see <see cref="Players.SkillPlayer"/>).
/// Gotcha: in tModLoader 1.4.4 a mod keybind starts unbound; the default key below is only
/// applied when the player resets it in Settings, Controls. <see cref="KeybindReminderPlayer"/> tells
/// the player once per session if any are unbound.
/// </summary>
public class ZenkaiKeybinds : ModSystem
{
	public static ModKeybind ChargeKi { get; private set; }
	public static ModKeybind ToggleFlight { get; private set; }
	public static ModKeybind KiStance { get; private set; }
	public static ModKeybind SkillMenu { get; private set; }
	public static ModKeybind Transform { get; private set; }

	/// <summary>Internal name and default key of every keybind.</summary>
	public static readonly (string Name, string Key)[] Defaults = {
		("ChargeKi", "C"),
		("ToggleFlight", "F"),
		("KiStance", "V"),
		("SkillMenu", "K"),
		("Transform", "T"),
	};

	public static IEnumerable<ModKeybind> All => new[] { ChargeKi, ToggleFlight, KiStance, SkillMenu, Transform };

	public override void Load()
	{
		var binds = Defaults.ToDictionary(d => d.Name, d => KeybindLoader.RegisterKeybind(Mod, d.Name, d.Key));
		ChargeKi = binds["ChargeKi"];
		ToggleFlight = binds["ToggleFlight"];
		KiStance = binds["KiStance"];
		SkillMenu = binds["SkillMenu"];
		Transform = binds["Transform"];
	}

	public override void Unload()
	{
		ChargeKi = null;
		ToggleFlight = null;
		KiStance = null;
		SkillMenu = null;
		Transform = null;
	}

	/// <summary>The first key bound to a keybind, for prompts; "-" when unbound.</summary>
	public static string KeyName(ModKeybind bind)
	{
		List<string> keys = bind?.GetAssignedKeys(InputMode.Keyboard);
		return keys == null || keys.Count == 0 ? "-" : keys[0];
	}
}

public class KeybindReminderPlayer : ModPlayer
{
	static bool remindedThisSession;

	public override void OnEnterWorld()
	{
		if (remindedThisSession || Player.whoAmI != Main.myPlayer)
			return;
		int unbound = ZenkaiKeybinds.All.Count(k => k.GetAssignedKeys(InputMode.Keyboard).Count == 0);
		if (unbound == 0)
			return;
		remindedThisSession = true;
		ZenkaiChat.Say(Language.GetTextValue("Mods.Zenkai.Keybinds.Unbound", unbound), new Microsoft.Xna.Framework.Color(255, 200, 90));
	}
}
