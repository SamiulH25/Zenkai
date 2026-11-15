using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Zenkai.Common.Players;
using Zenkai.Content.DamageClasses;

namespace Zenkai.Common.Skills;

public enum SkillKind { Blast, Beam, Melee, Movement, Utility }

/// <summary>
/// A learnable ki skill. Subclasses are discovered automatically: adding a skill is one class
/// plus its icon in Assets/Skills/&lt;Name&gt;.png and its text in the localisation file.
///
/// Lifecycle, driven by <see cref="SkillPlayer"/>: press the slot key, <see cref="OnStart"/>,
/// then (if <see cref="MaxCharge"/> &gt; 0) <see cref="OnChargeTick"/> while held, then on release
/// the ki cost is spent and <see cref="OnRelease"/> fires; skills with a sustain phase then get
/// <see cref="OnSustainTick"/> every tick, and finally <see cref="OnEnd"/>. Tap skills
/// (no charge) fire on the press.
/// All behaviour hooks run on every client for the owner's player; spawn projectiles only for
/// the owner (<see cref="Spawn"/> does this).
/// </summary>
public abstract class ModSkill : ModType, ILocalizedModType
{
	public int Type { get; private set; }
	public string LocalizationCategory => "Skills";

	public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);
	public virtual LocalizedText Description => this.GetLocalization(nameof(Description), () => "");
	/// <summary>Shown on the silhouette in the skill menu before the skill is learned.</summary>
	public virtual LocalizedText LearnHint => this.GetLocalization(nameof(LearnHint), () => "");

	public virtual string IconPath => $"{Mod.Name}/Assets/Skills/{Name}";
	public Asset<Texture2D> Icon => ModContent.Request<Texture2D>(IconPath);

	// Definition. Set these in SetStaticDefaults.
	public SkillKind Kind;
	public int Tier = 1;
	public bool KnownFromStart;
	/// <summary>Ki spent when the skill fires (on press for tap skills, on release for charged ones).</summary>
	public float KiCost;
	/// <summary>Ki per second while sustained.</summary>
	public float SustainDrain;
	/// <summary>Charge time in ticks. MaxCharge 0 means a tap skill.</summary>
	public int MinCharge;
	public int MaxCharge;
	/// <summary>Ticks after firing before the skill can start again.</summary>
	public int Cooldown;
	/// <summary>Ticks after the skill ends during which no other skill or item can be used.</summary>
	public int Recovery = 6;
	public int BaseDamage;
	public float Knockback;
	public bool UsableWhileFlying = true;
	/// <summary>Holding the key keeps firing whenever the cooldown allows.</summary>
	public bool AutoRepeat;
	/// <summary>The sustain phase ends when the key is let go.</summary>
	public bool HoldToSustain;
	/// <summary>Movement speed multiplier while charging (1 = unaffected).</summary>
	public float ChargeMoveSpeed = 1f;
	/// <summary>No walking, jumping or flying while sustained.</summary>
	public bool RootWhileSustaining;
	/// <summary>Max aim turn in radians per tick while sustained; 0 turns instantly.</summary>
	public float SustainTurnRate;

	protected sealed override void Register()
	{
		ModTypeLookup<ModSkill>.Register(this);
		Type = SkillLoader.Add(this);
	}

	public sealed override void SetupContent()
	{
		SetStaticDefaults();
		// Touch the text so the keys are registered with the localisation system.
		_ = DisplayName;
		_ = Description;
		_ = LearnHint;
	}

	/// <summary>Ki cost at this moment, before the player's cost multiplier.</summary>
	public virtual float GetCost(SkillPlayer sp) => KiCost;
	/// <summary>Length of the sustain phase in ticks for a given charge (0 to 1). 0 means no sustain.</summary>
	public virtual int SustainTicks(SkillPlayer sp, float charge) => 0;

	public virtual bool CanUse(SkillPlayer sp) => true;
	public virtual void OnStart(SkillPlayer sp) { }
	public virtual void OnChargeTick(SkillPlayer sp) { }
	public virtual void OnRelease(SkillPlayer sp, float charge) { }
	/// <summary>Return false to end the sustain phase early.</summary>
	public virtual bool OnSustainTick(SkillPlayer sp) => true;
	/// <summary>Called when the skill finishes or is cancelled.</summary>
	public virtual void OnEnd(SkillPlayer sp, bool cancelled) { }
	/// <summary>Called every tick while the skill is active, after movement, to pose the arms.</summary>
	public virtual void PoseArms(SkillPlayer sp)
	{
		float rotation = sp.Aim.ToRotation() - MathHelper.PiOver2;
		sp.Player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rotation);
	}

	public int DamageFor(Player player, float multiplier = 1f) =>
		(int)player.GetTotalDamage<KiDamageClass>().ApplyTo(BaseDamage * multiplier);

	public float KnockbackFor(Player player, float multiplier = 1f) =>
		player.GetTotalKnockback<KiDamageClass>().ApplyTo(Knockback * multiplier);

	/// <summary>Spawns a ki projectile for the owner only, with damage, knockback and crit from the ki class.
	/// Returns null on other clients.</summary>
	public Projectile Spawn(SkillPlayer sp, Vector2 position, Vector2 velocity, int type, float damageMultiplier = 1f,
		float knockbackMultiplier = 1f, float ai0 = 0f, float ai1 = 0f)
	{
		Player player = sp.Player;
		if (player.whoAmI != Main.myPlayer)
			return null;
		int index = Projectile.NewProjectile(player.GetSource_Misc("Zenkai_" + Name), position, velocity, type,
			DamageFor(player, damageMultiplier), KnockbackFor(player, knockbackMultiplier), player.whoAmI, ai0, ai1);
		Projectile projectile = Main.projectile[index];
		projectile.CritChance = (int)player.GetTotalCritChance<KiDamageClass>();
		return projectile;
	}
}
