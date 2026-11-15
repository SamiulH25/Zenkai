using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Common.Visuals;

namespace Zenkai.Common.Forms;

/// <summary>
/// A transformation. Subclasses are discovered automatically: adding a form is one class, its icon
/// in Assets/Forms/&lt;Name&gt;.png, optional hair in Assets/Forms/&lt;Name&gt;Hair.png, and its text in the
/// localisation file. <see cref="FormPlayer"/> applies the stats every tick while it is active.
/// Forms sit on one ladder ordered by <see cref="Tier"/>: the transform key steps up one rung.
/// </summary>
public abstract class ModForm : ModType, ILocalizedModType
{
	public int Type { get; private set; }
	public string LocalizationCategory => "Forms";

	public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);
	public virtual LocalizedText Description => this.GetLocalization(nameof(Description), () => "");
	/// <summary>Shown in the skill menu before the form is unlocked.</summary>
	public virtual LocalizedText UnlockHint => this.GetLocalization(nameof(UnlockHint), () => "");
	/// <summary>Chat line when the form is first unlocked.</summary>
	public virtual LocalizedText UnlockMessage => this.GetLocalization(nameof(UnlockMessage), () => "");

	public virtual string IconPath => $"{Mod.Name}/Assets/Forms/{Name}";
	public Asset<Texture2D> Icon => ModContent.Request<Texture2D>(IconPath);
	/// <summary>Replacement hair sprite, or null to keep the player's own hair.</summary>
	public virtual string HairPath => null;

	// Definition. Set these in SetStaticDefaults.
	public int Tier = 1;
	/// <summary>Added to ki damage (0.2 = +20%).</summary>
	public float KiDamageBonus;
	/// <summary>Added to movement speed (0.1 = +10%); also speeds flight.</summary>
	public float SpeedBonus;
	public int DefenseBonus;
	/// <summary>Ki drained per second at zero and at full mastery.</summary>
	public float KiDrainAtZero, KiDrainAtFull;
	/// <summary>Life drained per second at zero and at full mastery.</summary>
	public float LifeDrainAtZero, LifeDrainAtFull;
	/// <summary>Seconds in combat in this form to go from zero to full mastery.</summary>
	public float SecondsToMaster = 900f;
	public AuraStyle Aura = AuraStyle.Ki;
	/// <summary>Aura opacity while the form is held without charging or flying.</summary>
	public float IdleAura = 0.7f;
	/// <summary>Hair colour tint for players' own hair, when the form has no hair sprite. Null keeps it.</summary>
	public Color? HairColor;
	public Color? EyeColor;
	/// <summary>Reserved for phase 7 (races). Null means any race.</summary>
	public string RequiredRace;
	/// <summary>Leave the form automatically below this share of max life (life-draining forms).</summary>
	public float RevertBelowLife;

	protected sealed override void Register()
	{
		ModTypeLookup<ModForm>.Register(this);
		Type = FormLoader.Add(this);
	}

	public sealed override void SetupContent()
	{
		SetStaticDefaults();
		_ = DisplayName;
		_ = Description;
		_ = UnlockHint;
		_ = UnlockMessage;
	}

	public float KiDrain(float mastery) => MathHelper.Lerp(KiDrainAtZero, KiDrainAtFull, mastery);
	public float LifeDrain(float mastery) => MathHelper.Lerp(LifeDrainAtZero, LifeDrainAtFull, mastery);

	/// <summary>Called every tick for each form the player has not unlocked. Call
	/// <see cref="FormPlayer.Unlock"/> when its trigger is met.</summary>
	public virtual void CheckUnlock(FormPlayer fp) { }

	/// <summary>Called when the player is hurt, for forms not yet unlocked (near-death triggers).</summary>
	public virtual void CheckUnlockOnHurt(FormPlayer fp, Player.HurtInfo info) { }

	public virtual bool CanTransform(FormPlayer fp) => true;
}
