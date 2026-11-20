using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Zenkai.Common.Systems;
using Zenkai.Common.Visuals;
using Zenkai.Content.Dusts;

namespace Zenkai.Common.Players;

/// <summary>
/// The ki resource: the pool, its stats, charging and the aura's strength.
/// Everything that costs ki goes through <see cref="TrySpendKi"/>; gear, buffs and forms change the
/// stats below, which are rebuilt every tick. Flight lives in <see cref="FlightPlayer"/>.
/// Singleplayer only for now; nothing here is synced yet (see docs/PLAN.md, phase 8).
/// </summary>
public class KiPlayer : ModPlayer
{
	public const int StartingMaxKi = 100;

	// Tuning, per second unless noted.
	const float NaturalRegen = 1.5f;
	const float ChargeFractionOfMax = 0.25f; // a full charge from empty takes 4 seconds
	const float ChargeDamageTaken = 1.25f;
	const int RegenDelayAfterSpend = 90; // ticks
	const float FlightAuraOpacity = 0.85f;
	const float AuraEase = 0.15f;
	const int SparksPerTick = 1;

	/// <summary>Permanent max ki, saved with the character.</summary>
	public int MaxKiBase = StartingMaxKi;
	/// <summary>Max ki after this tick's bonuses. Rebuilt from <see cref="MaxKiStat"/> every tick.</summary>
	public int MaxKi = StartingMaxKi;
	public float Ki = StartingMaxKi;

	// Stats, reset every tick. Gear and buffs change them in their update hooks.
	/// <summary>Applied to <see cref="MaxKiBase"/> to give <see cref="MaxKi"/>.</summary>
	public StatModifier MaxKiStat = StatModifier.Default;
	/// <summary>Multiplies natural ki regen (exhaustion lowers it).</summary>
	public float NaturalRegenMultiplier = 1f;
	/// <summary>Multiplies how fast holding the charge key fills the pool.</summary>
	public float ChargeRateMultiplier = 1f;
	/// <summary>Aura opacity when neither charging nor flying. Forms raise it so the aura stays up.</summary>
	public float IdleAuraOpacity;

	public bool Charging { get; private set; }
	/// <summary>How the aura looks. <see cref="FormPlayer"/> swaps in the active form's style.</summary>
	public AuraStyle AuraStyle = AuraStyle.Ki;
	/// <summary>0 to 1, eased. Drives the aura and the bar glow.</summary>
	public float AuraOpacity { get; private set; }

	/// <summary>Shot runner only: holds the charge key.</summary>
	public bool DevHoldCharge;

	bool chargeHeld;
	int regenDelay;

	public override void ResetEffects()
	{
		MaxKiStat = StatModifier.Default;
		NaturalRegenMultiplier = 1f;
		ChargeRateMultiplier = 1f;
		IdleAuraOpacity = 0f;
	}

	public override void SaveData(TagCompound tag)
	{
		tag["maxKiBase"] = MaxKiBase;
		tag["ki"] = Ki;
	}

	public override void LoadData(TagCompound tag)
	{
		MaxKiBase = tag.ContainsKey("maxKiBase") ? tag.GetInt("maxKiBase") : StartingMaxKi;
		MaxKi = MaxKiBase;
		Ki = tag.ContainsKey("ki") ? tag.GetFloat("ki") : MaxKiBase;
	}

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		chargeHeld = ZenkaiKeybinds.ChargeKi.Current;
	}

	public bool CanUseKi => !Player.dead && !Player.CCed && !Player.mount.Active && Player.grappling[0] < 0 && !Player.pulley;

	/// <summary>Spends ki if there is enough. Returns false and spends nothing otherwise.
	/// Any spend holds natural regen off for a moment.</summary>
	public bool TrySpendKi(float amount)
	{
		if (amount <= 0f)
			return true;
		if (Ki < amount)
			return false;
		Ki -= amount;
		regenDelay = RegenDelayAfterSpend;
		return true;
	}

	/// <summary>Spends one tick's share of a per-second drain. False when the pool cannot cover it.</summary>
	public bool TryDrain(float perSecond) => TrySpendKi(perSecond / 60f);

	bool WantsCharge => chargeHeld || DevHoldCharge;

	public override void SetControls()
	{
		// Charging roots the player: no walking, jumping or item use.
		if (!WantsCharge || !CanUseKi)
			return;
		Player.controlLeft = Player.controlRight = Player.controlJump = false;
		Player.controlUseItem = Player.controlUseTile = false;
	}

	public override void PostUpdateMiscEffects()
	{
		MaxKi = Math.Max(1, (int)MaxKiStat.ApplyTo(MaxKiBase));
		Charging = WantsCharge && CanUseKi;

		if (Charging) {
			Ki += MaxKi * ChargeFractionOfMax * ChargeRateMultiplier / 60f;
			regenDelay = 0;
		}
		else if (regenDelay > 0) {
			regenDelay--;
		}
		else {
			Ki += NaturalRegen * NaturalRegenMultiplier / 60f;
		}
		Ki = Math.Clamp(Ki, 0f, MaxKi);

		bool flying = Player.GetModPlayer<FlightPlayer>().Flying;
		float target = Charging ? 1f : flying ? Math.Max(FlightAuraOpacity, IdleAuraOpacity) : IdleAuraOpacity;
		AuraOpacity = MathHelper.Lerp(AuraOpacity, target, AuraEase);
		if (AuraOpacity < 0.02f && target == 0f)
			AuraOpacity = 0f;
	}

	public override void PreUpdateMovement()
	{
		if (Charging && !Player.GetModPlayer<FlightPlayer>().Flying)
			Player.velocity.X *= 0.8f;
	}

	public override void ModifyHurt(ref Player.HurtModifiers modifiers)
	{
		if (Charging)
			modifiers.FinalDamage *= ChargeDamageTaken;
	}

	public override void UpdateDead()
	{
		Charging = false;
		AuraOpacity = 0f;
	}

	public override void PostUpdate()
	{
		if (Main.dedServ || AuraOpacity <= 0f)
			return;

		// The aura lights its surroundings in its own colour.
		Lighting.AddLight(Player.Center, AuraStyle.Hot.ToVector3() * (0.9f * AuraOpacity));

		// Sparks rising off the aura while charging.
		if (Charging) {
			for (int i = 0; i < SparksPerTick; i++) {
				// Off the flame's sides only: dust draws over the player, so sparks on the body hide the face.
				float side = Main.rand.NextBool() ? 1f : -1f;
				Vector2 pos = Player.Bottom + new Vector2(side * Main.rand.NextFloat(14f, 20f), Main.rand.NextFloat(-34f, -4f));
				var velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-4f, -2f));
				if (AuraStyle == AuraStyle.Ki)
					Dust.NewDustPerfect(pos, ModContent.DustType<KiSpark>(), velocity);
				else
					Dust.NewDustPerfect(pos, ModContent.DustType<TintSpark>(), velocity, 0, AuraStyle.Hot);
			}
		}
	}
}
