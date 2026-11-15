using Terraria;
using Terraria.ModLoader;
using Zenkai.Common.Players;

namespace Zenkai.Content.Buffs;

/// <summary>After a form runs out of ki: natural ki regen is cut to a quarter and no transforming.</summary>
public class KiExhaustion : ModBuff
{
	const float RegenMultiplier = 0.25f;

	public override string Texture => "Zenkai/Assets/Buffs/KiExhaustion";

	public override void SetStaticDefaults()
	{
		Main.debuff[Type] = true;
		Main.buffNoSave[Type] = true;
	}

	public override void Update(Player player, ref int buffIndex)
	{
		player.GetModPlayer<KiPlayer>().NaturalRegenMultiplier *= RegenMultiplier;
	}
}
