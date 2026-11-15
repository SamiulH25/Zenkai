using Zenkai.Common.Forms;
using Zenkai.Common.Visuals;

namespace Zenkai.Content.Forms;

/// <summary>
/// The first form, before Hardmode. Red aura, no hair change; drains life instead of ki, and drops
/// out on its own when life gets low. Taught by the trainer in phase 5; until then by /zform.
/// Later ranks (x3, x10...) and stacking on other forms come later.
/// </summary>
public class Kaioken : ModForm
{
	public override void SetStaticDefaults()
	{
		Tier = 1;
		KiDamageBonus = 0.20f;
		SpeedBonus = 0.10f;
		LifeDrainAtZero = 2f;
		LifeDrainAtFull = 1f;
		SecondsToMaster = 600f;
		RevertBelowLife = 0.1f;
		Aura = AuraStyle.Kaioken;
		IdleAura = 0.75f;
	}
}
