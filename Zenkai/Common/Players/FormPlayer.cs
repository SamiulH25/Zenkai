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
using Zenkai.Common.Visuals;
using Zenkai.Content.Buffs;
using Zenkai.Content.DamageClasses;
using Zenkai.Content.Dusts;
using Zenkai.Content.Projectiles;

namespace Zenkai.Common.Players;

public enum FormState { Normal, Transforming, Active }

/// <summary>
/// Unlocked forms, mastery per form, the active form and the transformation sequence.
/// Transform (T) steps one rung up the form ladder, Revert (Y) drops out. Forms drain ki (or
/// life) every tick; at zero ki the player drops out and is exhausted for a while.
/// Singleplayer only for now; the state is plain fields so it can be synced in phase 8.
/// </summary>
public class FormPlayer : ModPlayer
{
	const int TransformTicks = 50;
	const int FirstUnlockTicks = 100;     // the one-time dramatic sequence
	const int CombatWindow = 300;         // ticks after dealing or taking damage that count as combat
	const float MinKiToTransform = 0.1f;  // share of max ki needed to start
	const int ExhaustionTicks = 300;
	const float BurstRadius = 180f;
	const float BurstKnockback = 9f;

	public readonly HashSet<string> Unlocked = new();
	readonly Dictionary<string, float> mastery = new();

	public ModForm Active { get; private set; }
	/// <summary>The form being entered while <see cref="State"/> is Transforming.</summary>
	public ModForm Target { get; private set; }
	public FormState State { get; private set; }
	public int StateTicks { get; private set; }
	int transformLength;
	int lastCombatTick = -100000;

	/// <summary>The form whose look applies now: the target from halfway through a transformation.</summary>
	public ModForm Visible => State == FormState.Transforming && StateTicks >= transformLength / 2 ? Target : Active;

	public float Mastery(ModForm form) => form != null && mastery.TryGetValue(form.FullName, out float m) ? m : 0f;
	public void SetMastery(ModForm form, float value) => mastery[form.FullName] = MathHelper.Clamp(value, 0f, 1f);
	public bool IsUnlocked(ModForm form) => form != null && Unlocked.Contains(form.FullName);
	bool InCombat => (int)Main.GameUpdateCount - lastCombatTick < CombatWindow;

	public override void Initialize()
	{
		Unlocked.Clear();
		mastery.Clear();
		Active = Target = null;
		State = FormState.Normal;
	}

	public override void SaveData(TagCompound tag)
	{
		tag["unlocked"] = Unlocked.ToList();
		tag["masteryNames"] = mastery.Keys.ToList();
		tag["masteryValues"] = mastery.Values.ToList();
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
	}

	/// <summary>Unlocks a form. With <paramref name="dramatic"/>, the player is filled with ki and
	/// thrown straight into it with the long sequence (first Super Saiyan and the like).</summary>
	public void Unlock(ModForm form, bool dramatic)
	{
		if (!Unlocked.Add(form.FullName))
			return;
		if (Player.whoAmI == Main.myPlayer && !Dev.ShotRunner.Active) {
			string message = form.UnlockMessage.Value;
			if (!string.IsNullOrEmpty(message))
				Main.NewText(message, form.Aura.Rim);
		}
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
		if (Active == form || Target == form)
			Revert(false);
	}

	public override void ProcessTriggers(TriggersSet triggersSet)
	{
		if (ZenkaiKeybinds.Transform.JustPressed)
			StepUp();
		if (ZenkaiKeybinds.Revert.JustPressed && State != FormState.Normal)
			Revert(false);
	}

	/// <summary>The next unlocked form above the current one on the ladder, or null.</summary>
	public ModForm NextForm()
	{
		int tier = Active?.Tier ?? 0;
		return FormLoader.Ordered.FirstOrDefault(f => f.Tier > tier && IsUnlocked(f) && f.CanTransform(this));
	}

	public bool StepUp()
	{
		if (State == FormState.Transforming || Player.dead)
			return false;
		ModForm next = NextForm();
		KiPlayer ki = Player.GetModPlayer<KiPlayer>();
		bool exhausted = Player.HasBuff(ModContent.BuffType<KiExhaustion>());
		if (next == null || exhausted || !ki.CanUseKi || ki.Ki < ki.MaxKi * MinKiToTransform) {
			if (Player.whoAmI == Main.myPlayer)
				SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f }, Player.Center);
			return false;
		}
		BeginTransform(next, TransformTicks);
		return true;
	}

	/// <summary>Shot runner and dev command: enter a form at once, skipping the sequence.</summary>
	public void DevEnter(ModForm form)
	{
		Active = form;
		Target = null;
		State = form == null ? FormState.Normal : FormState.Active;
		StateTicks = 0;
	}

	/// <summary>Shot runner: start the transformation sequence into a form.</summary>
	public void DevBeginTransform(ModForm form) => BeginTransform(form, TransformTicks);

	void BeginTransform(ModForm form, int length)
	{
		Player.GetModPlayer<SkillPlayer>().Cancel();
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
		Active = Target = null;
		StateTicks = 0;
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
		if (State != FormState.Transforming)
			return;
		// Powering up roots the player.
		Player.controlLeft = Player.controlRight = Player.controlJump = false;
		Player.controlUp = Player.controlDown = false;
		Player.controlUseItem = Player.controlUseTile = false;
	}

	public override bool CanUseItem(Item item) => State != FormState.Transforming;

	public override void PostUpdateEquips()
	{
		KiPlayer ki = Player.GetModPlayer<KiPlayer>();
		ModForm look = Visible;
		ki.AuraStyle = look?.Aura ?? AuraStyle.Ki;

		if (State == FormState.Transforming) {
			ki.IdleAuraOpacity = 1f;
			return;
		}
		if (State != FormState.Active || Active == null)
			return;

		ModForm f = Active;
		Player.GetDamage<KiDamageClass>() += f.KiDamageBonus;
		Player.moveSpeed += f.SpeedBonus;
		Player.statDefense += f.DefenseBonus;
		ki.FlightSpeedMultiplier *= 1f + f.SpeedBonus;
		ki.IdleAuraOpacity = Math.Max(ki.IdleAuraOpacity, f.IdleAura);
	}

	public override void UpdateBadLifeRegen()
	{
		if (State != FormState.Active || Active == null)
			return;
		float drain = Active.LifeDrain(Mastery(Active));
		if (drain <= 0f)
			return;
		// lifeRegen counts half points of life per second, so -4 drains 2 life a second.
		if (Player.lifeRegen > 0)
			Player.lifeRegen = 0;
		Player.lifeRegenTime = 0;
		Player.lifeRegen -= Math.Max(1, (int)Math.Round(drain * 2f));
	}

	public override void PostUpdateMiscEffects()
	{
		// Unlock triggers for forms not yet unlocked.
		foreach (ModForm form in FormLoader.Forms) {
			if (!IsUnlocked(form))
				form.CheckUnlock(this);
		}

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
		ki.ForcedVelocity = Vector2.Zero;
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
		Active = Target;
		Target = null;
		State = FormState.Active;
		StateTicks = 0;

		SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.4f }, Player.Center);
		SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f, Volume = 0.7f }, Player.Center);
		Player.GetModPlayer<SkillPlayer>().Shake(9f, 24);
		if (Player.whoAmI == Main.myPlayer)
			Projectile.NewProjectile(Player.GetSource_Misc("Zenkai_Transform"), Player.Center, Vector2.Zero,
				ModContent.ProjectileType<FormBurst>(), 0, 0f, Player.whoAmI, 0f, Active.Type);

		// The burst throws nearby enemies back.
		if (Main.netMode != NetmodeID.MultiplayerClient) {
			foreach (NPC npc in Main.ActiveNPCs) {
				if (npc.friendly || npc.boss || npc.dontTakeDamage || npc.knockBackResist <= 0f)
					continue;
				Vector2 away = npc.Center - Player.Center;
				if (away.Length() > BurstRadius)
					continue;
				npc.velocity = away.SafeNormalize(Vector2.UnitX) * BurstKnockback * npc.knockBackResist + new Vector2(0f, -3f);
			}
		}
	}

	void UpdateActive(KiPlayer ki)
	{
		ModForm f = Active;
		float m = Mastery(f);
		float drain = f.KiDrain(m) / 60f;
		if (drain > 0f && !ki.TrySpendKi(drain)) {
			Revert(true);
			return;
		}
		if (f.RevertBelowLife > 0f && Player.statLife < Player.statLifeMax2 * f.RevertBelowLife) {
			Revert(false);
			return;
		}
		if (InCombat && m < 1f)
			SetMastery(f, m + 1f / (f.SecondsToMaster * 60f));
	}

	public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => lastCombatTick = (int)Main.GameUpdateCount;

	public override void OnHurt(Player.HurtInfo info) => lastCombatTick = (int)Main.GameUpdateCount;

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
		Active = Target = null;
	}

	public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
	{
		ModForm look = Visible;
		if (look == null)
			return;
		Color light = drawInfo.colorArmorHead;
		if (look.HairPath != null && FormHairLayer.DrawsHair(ref drawInfo))
			drawInfo.colorHair = Color.Transparent; // the form's hair replaces it
		else if (look.HairColor is Color hair)
			drawInfo.colorHair = hair.MultiplyRGBA(light);
		if (look.EyeColor is Color eyes)
			drawInfo.colorEyes = eyes.MultiplyRGBA(light);
	}
}
