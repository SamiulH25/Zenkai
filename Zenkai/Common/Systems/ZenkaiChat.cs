using Microsoft.Xna.Framework;
using Terraria;

namespace Zenkai.Common.Systems;

/// <summary>Player-facing chat lines from gameplay code. Test tooling mutes it so shots stay clean.</summary>
public static class ZenkaiChat
{
	/// <summary>Set by the shot runner; gameplay code never reads test tooling directly.</summary>
	public static bool Muted;

	public static void Say(string text, Color color)
	{
		if (Muted || Main.dedServ || string.IsNullOrEmpty(text))
			return;
		Main.NewText(text, color);
	}
}
