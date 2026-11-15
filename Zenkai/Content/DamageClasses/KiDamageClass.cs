using Terraria.ModLoader;

namespace Zenkai.Content.DamageClasses;

/// <summary>
/// Damage dealt by ki skills. Takes only generic bonuses from other classes, so melee,
/// ranged, magic and summon gear do not boost it; ki gear will modify it directly through
/// <c>Player.GetDamage&lt;KiDamageClass&gt;()</c> and friends.
/// </summary>
public class KiDamageClass : DamageClass
{
	public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
	{
		return damageClass == Generic ? StatInheritanceData.Full : StatInheritanceData.None;
	}

	public override bool GetEffectInheritance(DamageClass damageClass) => false;
}
