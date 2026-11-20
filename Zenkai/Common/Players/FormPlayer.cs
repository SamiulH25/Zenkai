using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Zenkai.Common.Forms;
using Zenkai.Common.Systems;
using Zenkai.Common.UI;
using Zenkai.Common.Visuals;
using Zenkai.Content.Buffs;
using Zenkai.Content.DamageClasses;
using Zenkai.Content.Dusts;
using Zenkai.Content.Projectiles;
using Zenkai.Content.Skills;

namespace Zenkai.Common.Players;

public enum FormState { Normal, Transforming, Active }

/// <summary>
/// Unlocked forms, mastery per form, the active form and the transformation sequence.
///
/// Controls, all on the transform key: hold it to open the form wheel, point at a form and let go
/// to transform into it (the centre powers down); a quick tap powers down when transformed, or
/// goes back into the last form used. Any unlocked form can be entered from any other.
///
/// Forms drain ki (or life) every tick; at zero ki the player drops out and is exhausted for a while.
/// Singleplayer only for now; the state is plain fields so it can be synced in phase 8.
/// </summary>
public class FormPlayer : ModPlayer
{
	const int TransformTicks = 50;
	const int SwapTicks = 30;             // going from one form straight to another
	const int FirstUnlockTicks = 100;     // the one-time dramatic sequence
	const int WheelHoldTicks = 10;        // holding the key this long opens the wheel; shorter is a tap
	const int CombatWindow = 300;         // ticks after dealing or taking damage that count as combat
	const float MinKiToTransform = 0.1f;  // share of max ki needed to start
	const int ExhaustionTicks = 300;
	const float BurstRadius = 180f;
	const float BurstKnockback = 9f;
	const int DodgeImmuneTicks = 40;
	const int UnlockCheckInterval = 60;
	const float LightningRadius = 320f;
	const int LightningBaseDamage = 30;
	const int LightningFirstStrike = 30;  // ticks into combat before the first bolt
	const float CounterDamageMultiplier = 1.5f;
	const int BurstBaseDamage = 20;

	public readonly HashSet<string> Unlocked = new();
	readonly Dictionary<string, float> mastery = new();

	public ModForm Active { get; private set; }
	/// <summary>A stacking form (Kaioken) layered on <see cref="Active"/>, which is then a base form.</summary>
	public ModForm Overlay { get; private set; }
	/// <summary>The form being entered while <see cref="State"/> is Transforming.</summary>
	public ModForm Target { get; private set; }
	public FormState State { get; private set; }
	public int StateTicks { get; private set; }
	/// <summary>The form a tap of the transform key goes back into.</summary>
	public ModForm LastForm { get; private set; }
	/// <summary>True while the form wheel is up.</summary>
	public bool WheelOpen { get; private set; }
	int transformLength;
	int lastCombatTick = -100000;
	int holdTicks;
	bool keyWasDown;
	int lightningTimer;

	/// <summary>Shot runner only: counts as in combat.</summary>
	public bool DevInCombat;
	/// <summary>Shot runner only: every dodge succeeds.</summary>
	public bool DevForceDodge;

	/// <summary>The form whose look applies now: the target from halfway through a transformation.</summary>
	public ModForm Visible => State == FormState.Transforming && StateTicks >= transformLength / 2 ? Target : Active;

	/// <summary>The form that sets hair and eyes: the base form when a stacking form rides on it.</summary>
	public ModForm BaseLook => Visible is { Stacks: true } && Active is { Stacks: false } ? Active : Visible;

	/// <summary>The form that sets the aura: a stacking form shows over the base form.</summary>
	public ModForm AuraLook => Visible is { Stacks: true } ? Visible : Overlay ?? Visible;

	/// <summary>Every form whose effects apply now: the active form, then the stacked one.</summary>
	IEnumerable<ModForm> Running()
	{
		if (Active != null)
			yield return Active;
		if (Overlay != null)
			yield return Overlay;
	}

	public float Mastery(ModForm form) => form != null && mastery.TryGetValue(form.FullName, out float m) ? m : 0f;
	public void SetMastery(ModForm form, float value) => mastery[form.FullName] = MathHelper.Clamp(value, 0f, 1f);
	public bool IsUnlocked(ModForm form) => form != null && Unlocked.Contains(form.FullName);
	bool InCombat => DevInCombat || (int)Main.GameUpdateCount - lastCombatTick < CombatWindow;

	/// <summary>Unlocked forms, weakest first: what the wheel shows.</summary>
	public List<ModForm> UnlockedForms()
	{
		var list = new List<ModForm>();
		foreach (ModForm form in FormLoader.Ordered) {
			if (IsUnlocked(form))
				list.Add(form);
		}
		return list;
	}

	public override void Initialize()
	{
		Unlocked.Clear();
		mastery.Clear();
		Active = Target = LastForm = Overlay = null;
		State = FormState.Normal;
		WheelOpen = false;
		lightningTimer = 0;
		DevInCombat = DevForceDodge = false;
	}

	public override void SaveData(TagCompound tag)
	{
		tag["unlocked"] = Unlocked.ToList();
		tag["masteryNames"] = mastery.Keys.ToList();
		tag["masteryValues"] = mastery.Values.ToList();
		tag["lastForm"] = LastForm?.FullName ?? "";
	}

	public override void LoadData(TagCompound tag)
	{
		Unlocked.Clear();
		foreach (string name in tag.GetList<string>("unlocked"))
			Unlocked.Add(name);
		mastery.Clear();
		IList<string> names = tag.GetList<string>("masteryNames");
		IList<float> values = tag.GetList<float>("masteryValues");
		for (int i = 0; i < Math.Min(names.Count, values.Count); i++)
			mastery[names[i]] = values[i];
		LastForm = FormLoader.Find(tag.GetString("lastForm"));
	}

	/// <summary>Unlocks a form. With <paramref name="dramatic"/>, the player is filled with ki and
	/// thrown straight into it with the long sequence (first Super Saiyan and the like).</summary>
	public void Unlock(ModForm form, bool dramatic)
	{
		if (!Unlocked.Add(form.FullName))
			return;
		if (Player.whoAmI == Main.myPlayer)
			ZenkaiChat.Say(form.UnlockMessage.Value, form.Aura.Rim);
		if (dramatic) {
			KiPlayer ki = Player.GetModPlayer<KiPlayer>();
			ki.Ki = ki.MaxKi;
			Player.ClearBuff(ModContent.BuffType<KiExhaustion>());
			BeginTransform(form, FirstUnlockTicks);
		}
	}

	public void Lock(ModForm form)
	{
		Unlocked.Remove(form.FullName);
		if (Overlay == form)
			Overlay = null;
		if (Active == form || Target == form)
			Revert(false);
		if (LastForm == form)
			LastForm = null;
	}

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		bool down = ZenkaiKeybinds.Transform.Current;
		if (down) {
			holdTicks++;
			if (holdTicks == WheelHoldTicks)
				SetWheel(true);
		}
		else if (keyWasDown) {
			if (WheelOpen) {
				bool chose = FormWheelSystem.TryGetChoice(out ModForm choice);
				SetWheel(false);
				if (chose)
					Choose(choice);
			}
			else if (holdTicks < WheelHoldTicks) {
				Tap();
			}
			holdTicks = 0;
		}
		keyWasDown = down;
	}

	/// <summary>Opens or closes the form wheel. It only opens with a form to show.</summary>
	public void SetWheel(bool open)
	{
		if (open && (Player.dead || Unlocked.Count == 0 || State == FormState.Transforming))
			return;
		if (open != WheelOpen && Player.whoAmI == Main.myPlayer)
			SoundEngine.PlaySound(open ? SoundID.MenuOpen : SoundID.MenuClose);
		WheelOpen = open;
	}

	/// <summary>A wheel choice: a form to enter, or null to power down.</summary>
	public void Choose(ModForm form)
	{
		if (form == null)
			Revert(false);
		else
			TransformInto(form);
	}

	/// <summary>A quick press of the transform key: power down, or go back into the last form used.</summary>
	void Tap()
	{
		if (State == FormState.Transforming)
			return;
		if (State == FormState.Active) {
			Revert(false);
			return;
		}
		ModForm form = IsUnlocked(LastForm) ? LastForm : UnlockedForms().LastOrDefault(f => f.CanTransform(this));
		if (form == null)
			Deny();
		else
			TransformInto(form);
	}

	void Deny()
	{
		if (Player.whoAmI == Main.myPlayer)
			SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f }, Player.Center);
	}

	/// <summary>Starts the transformation into an unlocked form, from normal or from any other form.</summary>
	public bool TransformInto(ModForm form)
	{
		if (form == null || State == FormState.Transforming || Player.dead)
			return false;
		// Choosing the stacked form again takes it off.
		if (form == Overlay) {
			DropOverlay();
			return true;
		}
		if (form == Active)
			return false;
		KiPlayer ki = Player.GetModPlayer<KiPlayer>();
		bool exhausted = Player.HasBuff(ModContent.BuffType<KiExhaustion>());
		if (!IsUnlocked(form) || !form.CanTransform(this) || exhausted || !ki.CanUseKi || ki.Ki < ki.MaxKi * MinKiToTransform) {
			Deny();
			return false;
		}
		BeginTransform(form, State == FormState.Active ? SwapTicks : TransformTicks);
		return true;
	}

	/// <summary>Shot runner and dev command: enter a form at once, skipping the sequence.</summary>
	public void DevEnter(ModForm form)
	{
		Overlay = null;
		Active = form;
		Target = null;
		State = form == null ? FormState.Normal : FormState.Active;
		StateTicks = 0;
		lightningTimer = LightningFirstStrike;
		if (form != null)
			LastForm = form;
	}

	/// <summary>Shot runner and dev command: put a stacking form on the active form at once.</summary>
	public void DevStack(ModForm form) => Overlay = form is { Stacks: true } && Active is { Stacks: false } ? form : Overlay;

	/// <summary>Shot runner: start the transformation sequence into a form.</summary>
	public void DevBeginTransform(ModForm form) => BeginTransform(form, TransformTicks);

	void BeginTransform(ModForm form, int length)
	{
		Player.GetModPlayer<SkillPlayer>().Cancel();
		WheelOpen = false;
		Target = form;
		transformLength = length;
		State = FormState.Transforming;
		StateTicks = 0;
		SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.8f, Volume = 0.8f }, Player.Center);
	}

	public void Revert(bool exhausted)
	{
		if (State == FormState.Normal)
			return;
		bool wasActive = Active != null;
		State = FormState.Normal;
		Active = Target = Overlay = null;
		StateTicks = 0;
		lightningTimer = 0;
		if (exhausted)
			Player.AddBuff(ModContent.BuffType<KiExhaustion>(), ExhaustionTicks);
		if (wasActive && !Main.dedServ) {
			SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.5f, Volume = 0.6f }, Player.Center);
			for (int i = 0; i < 12; i++)
				Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(14f, 22f), ModContent.DustType<TintSpark>(),
					new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1f)), 0, new Color(200, 210, 230));
		}
	}

	public override void SetControls()
	{
		if (WheelOpen) {
			// The mouse is choosing a form.
			Player.controlUseItem = Player.controlUseTile = false;
		}
		if (State != FormState.Transforming)
			return;
		// Powering up roots the player.
		Player.controlLeft = Player.controlRight = Player.controlJump = false;
		Player.controlUp = Player.controlDown = false;
		Player.controlUseItem = Player.controlUseTile = false;
	}

	public override bool CanUseItem(Item item) => State != FormState.Transforming && !WheelOpen;

	public override void PostUpdateEquips()
	{
		KiPlayer ki = Player.GetModPlayer<KiPlayer>();
		ModForm look = Visible;
		ki.AuraStyle = AuraLook?.Aura ?? AuraStyle.Ki;

		if (State == FormState.Transforming) {
			ki.IdleAuraOpacity = 1f;
			return;
		}
		if (State != FormState.Active || Active == null)
			return;

		foreach (ModForm f in Running()) {
			Player.GetDamage<KiDamageClass>() *= f.KiDamageMultiplier;
			// The Kaioken surge: the closer to death, the harder it hits.
			if (f.SurgeBonus > 0f)
				Player.GetDamage<KiDamageClass>() *= 1f + f.SurgeBonus * (1f - Player.statLife / (float)Player.statLifeMax2);
			Player.GetCritChance<KiDamageClass>() += f.KiCritBonus;
			Player.moveSpeed += f.SpeedBonus;
			Player.statDefense += f.DefenseBonus;
			Player.endurance += f.EnduranceBonus;
			if (f.MaxKiBonus > 0)
				ki.MaxKiStat = ki.MaxKiStat.CombineWith(new StatModifier(1f, 1f, f.MaxKiBonus));
			ki.NaturalRegenMultiplier *= f.KiRegenMultiplier;
			ki.ChargeRateMultiplier *= f.ChargeRateMultiplier;
			Player.GetModPlayer<SkillPlayer>().KiCostMultiplier *= f.KiCostMultiplier;
			Player.GetModPlayer<FlightPlayer>().SpeedMultiplier *= 1f + f.SpeedBonus;
			ki.IdleAuraOpacity = Math.Max(ki.IdleAuraOpacity, f.IdleAura);
		}
	}

	public override void UpdateBadLifeRegen()
	{
		if (State != FormState.Active || Active == null)
			return;
		float drain = 0f;
		foreach (ModForm f in Running())
			drain += f.LifeDrain(Mastery(f));
		if (drain <= 0f)
			return;
		// lifeRegen counts half points of life per second, so -4 drains 2 life a second.
		if (Player.lifeRegen > 0)
			Player.lifeRegen = 0;
		Player.lifeRegenTime = 0;
		Player.lifeRegen -= Math.Max(1, (int)Math.Round(drain * 2f));
	}

	public override void UpdateLifeRegen()
	{
		if (State != FormState.Active || Active == null)
			return;
		float regen = 0f;
		foreach (ModForm f in Running())
			regen += f.LifeRegenBonus;
		// Runs after UpdateBadLifeRegen, so this nets against any life drain. Half points per second.
		if (regen > 0f)
			Player.lifeRegen += (int)Math.Round(regen * 2f);
	}

	public override void PostUpdateMiscEffects()
	{
		// Quiet unlocks: progression and mastery of earlier forms.
		if (Player.whoAmI == Main.myPlayer && Main.GameUpdateCount % UnlockCheckInterval == 0) {
			foreach (ModForm form in FormLoader.Forms) {
				if (!IsUnlocked(form) && form.UnlockCondition(this))
					Unlock(form, dramatic: false);
			}
		}
		if (WheelOpen && (Player.dead || State == FormState.Transforming))
			WheelOpen = false;

		KiPlayer ki = Player.GetModPlayer<KiPlayer>();
		StateTicks++;
		switch (State) {
			case FormState.Transforming:
				UpdateTransforming(ki);
				break;
			case FormState.Active:
				UpdateActive(ki);
				break;
		}
	}

	void UpdateTransforming(KiPlayer ki)
	{
		if (!ki.CanUseKi) {
			Revert(false);
			return;
		}
		Player.GetModPlayer<FlightPlayer>().ForcedVelocity = Vector2.Zero;
		float progress = StateTicks / (float)transformLength;
		Color tint = Target.Aura.Hot;

		// Motes drawn in from all around, faster as it builds.
		if (!Main.dedServ) {
			int count = 1 + (int)(progress * 3f);
			for (int i = 0; i < count; i++) {
				Vector2 off = Main.rand.NextVector2CircularEdge(70f, 70f) * Main.rand.NextFloat(0.7f, 1f);
				Dust.NewDustPerfect(Player.Center + off, ModContent.DustType<TintSpark>(), -off * (0.06f + progress * 0.04f), 0, tint);
			}
			if (StateTicks % 10 == 0)
				Player.GetModPlayer<SkillPlayer>().Shake(1f + progress * 4f, 12);
			if (StateTicks % 16 == 0)
				SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.8f + progress, Volume = 0.6f }, Player.Center);
		}
		Lighting.AddLight(Player.Center, tint.ToVector3() * (0.5f + progress));

		if (StateTicks >= transformLength)
			FinishTransform();
	}

	void FinishTransform()
	{
		ModForm entered = Target;
		if (entered.Stacks && Active is { Stacks: false }) {
			Overlay = entered;
		}
		else {
			// A base form keeps a stacked Kaioken that was already running; a Kaioken rank on its own
			// replaces any other rank.
			if (!entered.Stacks && Active is { Stacks: true })
				Overlay = Active;
			else if (entered.Stacks)
				Overlay = null;
			Active = entered;
		}
		LastForm = entered;
		Target = null;
		State = FormState.Active;
		StateTicks = 0;
		lightningTimer = LightningFirstStrike;

		SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.4f }, Player.Center);
		SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f, Volume = 0.7f }, Player.Center);
		Player.GetModPlayer<SkillPlayer>().Shake(9f, 24);
		if (Player.whoAmI == Main.myPlayer)
			Projectile.NewProjectile(Player.GetSource_Misc("Zenkai_Transform"), Player.Center, Vector2.Zero,
				ModContent.ProjectileType<FormBurst>(), 0, 0f, Player.whoAmI, 0f, FormBurst.Pack(Active.Aura.Hot));

		// The burst scorches and throws nearby enemies back.
		if (Main.netMode != NetmodeID.MultiplayerClient) {
			int burstDamage = (int)Player.GetTotalDamage<KiDamageClass>().ApplyTo(BurstBaseDamage);
			foreach (NPC npc in Main.ActiveNPCs) {
				if (npc.friendly || npc.dontTakeDamage || npc.life <= 0)
					continue;
				Vector2 away = npc.Center - Player.Center;
				if (away.Length() > BurstRadius)
					continue;
				npc.SimpleStrikeNPC(burstDamage, away.X >= 0f ? 1 : -1, false, 0f, ModContent.GetInstance<KiDamageClass>());
				if (!npc.boss && npc.knockBackResist > 0f) {
					npc.velocity = away.SafeNormalize(Vector2.UnitX) * BurstKnockback * npc.knockBackResist + new Vector2(0f, -3f);
					npc.netUpdate = true;
				}
			}
		}
	}

	void UpdateActive(KiPlayer ki)
	{
		float kiDrain = 0f;
		foreach (ModForm f in Running())
			kiDrain += f.KiDrain(Mastery(f));
		if (!ki.TryDrain(kiDrain)) {
			Revert(true);
			return;
		}
		// Low life drops a stacked Kaioken first, and only the form itself if it is the one running.
		if (Overlay != null && Overlay.RevertBelowLife > 0f && Player.statLife < Player.statLifeMax2 * Overlay.RevertBelowLife) {
			DropOverlay();
		}
		else if (Active.RevertBelowLife > 0f && Player.statLife < Player.statLifeMax2 * Active.RevertBelowLife) {
			Revert(false);
			return;
		}
		UpdateLightning();
		if (!InCombat)
			return;
		foreach (ModForm f in Running()) {
			float m = Mastery(f);
			if (m < 1f)
				SetMastery(f, m + 1f / (f.SecondsToMaster * 60f));
		}
	}

	/// <summary>While in combat, the storm forms arc lightning onto the nearest foe.</summary>
	void UpdateLightning()
	{
		ModForm storm = null;
		foreach (ModForm f in Running())
			if (f.LightningInterval > 0)
				storm = f;
		if (storm == null || !InCombat || --lightningTimer > 0)
			return;
		lightningTimer = storm.LightningInterval;
		NPC target = null;
		float best = LightningRadius;
		foreach (NPC npc in Main.ActiveNPCs) {
			if (npc.friendly || npc.dontTakeDamage || npc.life <= 0)
				continue;
			float dist = Vector2.Distance(npc.Center, Player.Center);
			if (dist < best) {
				best = dist;
				target = npc;
			}
		}
		if (target == null)
			return;
		if (Main.netMode != NetmodeID.MultiplayerClient) {
			lastCombatTick = (int)Main.GameUpdateCount;
			int damage = (int)Player.GetTotalDamage<KiDamageClass>().ApplyTo(LightningBaseDamage);
			target.SimpleStrikeNPC(damage, target.Center.X >= Player.Center.X ? 1 : -1, false, 0f, ModContent.GetInstance<KiDamageClass>());
		}
		if (Main.dedServ)
			return;
		SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap, target.Center);
		// A jagged column of sparks from high above down to the foe.
		Vector2 top = target.Center - new Vector2(0f, 220f);
		for (int i = 0; i < 14; i++) {
			Vector2 pos = Vector2.Lerp(top, target.Center, i / 13f) + Main.rand.NextVector2Circular(10f, 4f);
			Dust.NewDustPerfect(pos, DustID.Electric, Vector2.Zero, 0, storm.Aura.Hot);
		}
	}

	/// <summary>Takes the stacked form off and keeps the base form.</summary>
	void DropOverlay()
	{
		if (Overlay == null)
			return;
		Overlay = null;
		if (Player.whoAmI == Main.myPlayer)
			SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.5f, Volume = 0.5f }, Player.Center);
	}

	public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => lastCombatTick = (int)Main.GameUpdateCount;

	public override void OnHurt(Player.HurtInfo info) => lastCombatTick = (int)Main.GameUpdateCount;

	public override bool FreeDodge(Player.HurtInfo info)
	{
		if (State != FormState.Active || Active == null || Player.whoAmI != Main.myPlayer)
			return false;
		float dodge = 0f;
		foreach (ModForm f in Running())
			dodge += f.DodgeChance;
		if (dodge <= 0f || (!DevForceDodge && Main.rand.NextFloat() >= Math.Min(dodge, 0.9f)))
			return false;
		Player.SetImmuneTimeForAllTypes(DodgeImmuneTicks);
		lastCombatTick = (int)Main.GameUpdateCount;
		SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.8f, Volume = 0.5f }, Player.Center);
		for (int i = 0; i < 8; i++)
			Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(12f, 20f), ModContent.DustType<TintSpark>(),
				Main.rand.NextVector2Circular(2f, 2f), 0, Active.Aura.Rim);
		foreach (ModForm f in Running())
			if (f.DodgeCounter)
				CounterAttack(info);
		return true;
	}

	/// <summary>The body answers a slipped hit: a ki bolt at whatever threw it.</summary>
	void CounterAttack(Player.HurtInfo info)
	{
		if (!info.DamageSource.TryGetCausingEntity(out Entity source))
			return;
		Vector2? target = source switch {
			NPC npc when npc.active => npc.Center,
			Projectile proj when proj.active => proj.Center,
			_ => null,
		};
		if (target is not Vector2 aim)
			return;
		Vector2 velocity = (aim - Player.Center).SafeNormalize(Vector2.UnitX) * KiBlast.Speed;
		ModContent.GetInstance<KiBlast>().Spawn(Player.GetModPlayer<SkillPlayer>(), Player.Center, velocity,
			ModContent.ProjectileType<KiBlastProjectile>(), CounterDamageMultiplier);
	}

	public override void PostHurt(Player.HurtInfo info)
	{
		foreach (ModForm form in FormLoader.Forms) {
			if (!IsUnlocked(form))
				form.CheckUnlockOnHurt(this, info);
		}
	}

	public override void UpdateDead()
	{
		State = FormState.Normal;
		Active = Target = Overlay = null;
		WheelOpen = false;
	}

	public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
	{
		ModForm look = Visible;
		if (look == null)
			return;
		// The character keeps their own hair; the form shifts its colour.
		Color light = drawInfo.colorArmorHead;
		if (look.HairColor is Color hair)
			drawInfo.colorHair = Color.Lerp(drawInfo.colorHair, FormHairLayer.Lit(hair, light), look.HairBlend);
		if (look.EyeColor is Color eyes)
			drawInfo.colorEyes = eyes.MultiplyRGBA(light);
	}
}
