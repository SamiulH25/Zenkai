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
/// in Assets/Forms/&lt;Name&gt;.png and its text in the localisation file. <see cref="FormPlayer"/>
/// applies the stats every tick while it is active.
/// Forms belong to a <see cref="Line"/> (a family such as Kaioken or Super Saiyan) and are ordered
/// by <see cref="Tier"/>. The player picks any unlocked form directly from the form wheel.
/// A form never replaces the character's hair: it tints it and may lift it a little.
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
	Asset<Texture2D> icon;
	public Asset<Texture2D> Icon => icon ??= ModContent.Request<Texture2D>(IconPath);

	// Definition. Set these in SetStaticDefaults.
	/// <summary>The family this form belongs to. Forms of one line sit together in the menu and on the wheel.</summary>
	public string Line = "";
	/// <summary>Orders every form from weakest to strongest, across lines.</summary>
	public int Tier = 1;
	/// <summary>True for a form that can be layered on top of any other form (Kaioken). Its bonuses and
	/// drains add to the base form's, and its aura and glow show over it.</summary>
	public bool Stacks;
	/// <summary>Multiplies ki damage (5 = five times), gear included. Stacked forms multiply together.</summary>
	public float KiDamageMultiplier = 1f;
	/// <summary>Added to movement speed (0.1 = +10%); also speeds flight.</summary>
	public float SpeedBonus;
	public int DefenseBonus;
	/// <summary>Chance to slip an incoming hit entirely (0.3 = 30%).</summary>
	public float DodgeChance;
	/// <summary>Added to ki crit chance (0.1 = +10%).</summary>
	public float KiCritBonus;
	/// <summary>Added to damage reduction (0.1 = 10% less damage taken).</summary>
	public float EnduranceBonus;
	/// <summary>Flat max ki while the form is held.</summary>
	public int MaxKiBonus;
	/// <summary>Multiplies natural ki regen.</summary>
	public float KiRegenMultiplier = 1f;
	/// <summary>Multiplies the ki cost of skills.</summary>
	public float KiCostMultiplier = 1f;
	/// <summary>Multiplies how fast holding the charge key fills ki.</summary>
	public float ChargeRateMultiplier = 1f;
	/// <summary>Life recovered per second while the form is held.</summary>
	public float LifeRegenBonus;
	/// <summary>Extra ki damage at zero life, scaled by the share of life missing (the Kaioken surge).</summary>
	public float SurgeBonus;
	/// <summary>Ticks between lightning strikes on nearby foes while in combat. 0 = none.</summary>
	public int LightningInterval;
	/// <summary>A slipped hit answers with a ki bolt at the attacker.</summary>
	public bool DodgeCounter;
	/// <summary>Ki drained per second at zero and at full mastery.</summary>
	public float KiDrainAtZero, KiDrainAtFull;
	/// <summary>Life drained per second at zero and at full mastery.</summary>
	public float LifeDrainAtZero, LifeDrainAtFull;
	/// <summary>Seconds in combat in this form to go from zero to full mastery.</summary>
	public float SecondsToMaster = 900f;
	public AuraStyle Aura = AuraStyle.Ki;
	/// <summary>Aura opacity while the form is held without charging or flying.</summary>
	public float IdleAura = 0.7f;
	/// <summary>Colour the character's own hair shifts toward. Null keeps it.</summary>
	public Color? HairColor;
	/// <summary>How far the hair shifts to <see cref="HairColor"/> (1 = fully).</summary>
	public float HairBlend = 1f;
	/// <summary>Screen px the hair stands up by: a second copy of the same hair drawn that much higher.</summary>
	public int HairLift;
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

	public override void Unload() => icon = null;

	public float KiDrain(float mastery) => MathHelper.Lerp(KiDrainAtZero, KiDrainAtFull, mastery);
	public float LifeDrain(float mastery) => MathHelper.Lerp(LifeDrainAtZero, LifeDrainAtFull, mastery);

	/// <summary>True once this form's quiet unlock condition is met (progression, mastery of an earlier
	/// form). Checked about once a second for forms not yet unlocked.</summary>
	public virtual bool UnlockCondition(FormPlayer fp) => false;

	/// <summary>Called when the player is hurt, for forms not yet unlocked (near-death triggers). Call
	/// <see cref="FormPlayer.Unlock"/> when the trigger is met.</summary>
	public virtual void CheckUnlockOnHurt(FormPlayer fp, Player.HurtInfo info) { }

	public virtual bool CanTransform(FormPlayer fp) => true;

	/// <summary>True when the player is close to death with a boss alive: the trigger for awakenings.</summary>
	protected static bool NearDeathAgainstBoss(Player player, float lifeShare)
	{
		if (player.dead || player.statLife >= player.statLifeMax2 * lifeShare)
			return false;
		foreach (NPC npc in Main.ActiveNPCs) {
			if (npc.boss)
				return true;
		}
		return false;
	}
}
