using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using Zenkai.Common.Forms;
using Zenkai.Common.Players;
using Zenkai.Common.Skills;

namespace Zenkai.Common.UI;

/// <summary>Opens and closes the skill menu and hosts its <see cref="UserInterface"/>.</summary>
public class SkillMenuSystem : ModSystem
{
	static UserInterface ui;
	static SkillMenuState state;
	static readonly GameTime NoTime = new();

	public static bool IsOpen => ui?.CurrentState != null;

	public override void Load()
	{
		if (Main.dedServ)
			return;
		ui = new UserInterface();
		state = new SkillMenuState();
		state.Activate();
	}

	public override void Unload()
	{
		ui = null;
		state = null;
	}

	public static void Toggle() => SetOpen(!IsOpen);

	public static void SetOpen(bool open)
	{
		if (ui == null || open == IsOpen)
			return;
		ui.SetState(open ? state : null);
		SoundEngine.PlaySound(open ? SoundID.MenuOpen : SoundID.MenuClose);
	}

	/// <summary>Selects a skill as if it had been clicked. Used by the shot runner.</summary>
	public static void DevSelect(ModSkill skill) => state.Selected = skill;

	/// <summary>Selects a form as if it had been clicked. Used by the shot runner.</summary>
	public static void DevSelectForm(ModForm form) => state.SelectedForm = form;

	public override void UpdateUI(GameTime gameTime)
	{
		if (IsOpen && (Main.LocalPlayer.dead || Main.gameMenu))
			SetOpen(false);
		if (IsOpen)
			ui.Update(gameTime);
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
		if (index < 0)
			return;
		layers.Insert(index, new LegacyGameInterfaceLayer("Zenkai: Skill Menu", () => {
			if (IsOpen)
				ui.Draw(Main.spriteBatch, NoTime);
			return true;
		}, InterfaceScaleType.UI));
	}
}

public class SkillMenuState : UIState
{
	SkillMenuPanel panel;

	public ModSkill Selected
	{
		get => panel.Selected;
		set {
			panel.Selected = value;
			panel.SelectedForm = null;
		}
	}

	public ModForm SelectedForm
	{
		get => panel.SelectedForm;
		set {
			panel.SelectedForm = value;
			panel.Selected = null;
		}
	}

	public override void OnInitialize()
	{
		panel = new SkillMenuPanel();
		Append(panel);
	}
}

/// <summary>
/// The skill menu: a grid of every skill (unlearned ones as silhouettes), a grid of every form with
/// its mastery, details for whatever is hovered or selected, and the loadout: the eight bar slots
/// and the right-button slot. Click a skill, then a slot, to assign it; right-click a slot to
/// clear it. The grids hold <see cref="SkillRows"/> and <see cref="FormRows"/> rows of
/// <see cref="Columns"/>; raise those (and the panel grows with them) when content outgrows them.
/// </summary>
public class SkillMenuPanel : UIPanel
{
	const int Pad = 16;
	const int Columns = 6;
	const int SkillRows = 3;
	const int FormRows = 2;
	const int Cell = SkillSlotDrawer.Size + 8;
	const int SlotStep = SkillSlotDrawer.Size + 4;
	const int GridTop = 46;
	const int FormsLabelTop = GridTop + SkillRows * Cell + 2;
	const int FormsTop = FormsLabelTop + 24;
	const int LoadoutLabelTop = FormsTop + FormRows * Cell + 10;
	const int LoadoutTop = LoadoutLabelTop + 26;
	const int DetailLeft = Pad + Columns * Cell + 8;
	const int DetailWidth = 328;
	const int W = DetailLeft + DetailWidth + Pad;
	const int H = LoadoutTop + SkillSlotDrawer.Size + Pad;
	const int SecondaryGap = 16;
	const float TextScale = 0.85f;
	const int LineHeight = 20;

	static readonly Color Muted = new(160, 165, 190);
	static readonly Color Body = new(200, 210, 235);
	static readonly Color Hint = new(190, 200, 230);
	static readonly Color Question = new(150, 160, 200);

	public ModSkill Selected;
	public ModForm SelectedForm;

	static string T(string key, params object[] args) => Language.GetTextValue("Mods.Zenkai.UI.SkillMenu." + key, args);

	public SkillMenuPanel()
	{
		Width.Set(W, 0f);
		Height.Set(H, 0f);
		HAlign = 0.5f;
		VAlign = 0.4f;
		BackgroundColor = new Color(33, 43, 79) * 0.92f;
		BorderColor = new Color(13, 13, 30);
	}

	static IReadOnlyList<ModSkill> Skills => SkillLoader.Ordered;
	static IReadOnlyList<ModForm> Forms => FormLoader.Ordered;

	Vector2 Origin
	{
		get {
			CalculatedStyle d = GetDimensions();
			return new Vector2((int)d.X & ~1, (int)d.Y & ~1);
		}
	}

	static Rectangle Slot(int x, int y) => new(x, y, SkillSlotDrawer.Size, SkillSlotDrawer.Size);
	static Rectangle SkillCell(int i) => Slot(Pad + i % Columns * Cell, GridTop + i / Columns * Cell);
	static Rectangle FormCell(int i) => Slot(Pad + i % Columns * Cell, FormsTop + i / Columns * Cell);
	/// <summary>Bar slots 0..7, then the right-button slot at index <see cref="SkillPlayer.SlotCount"/>.</summary>
	static Rectangle LoadoutSlot(int i) => Slot(Pad + i * SlotStep + (i >= SkillPlayer.SlotCount ? SecondaryGap - 4 : 0), LoadoutTop);
	static Rectangle CloseButton => new(W - 34, 8, 24, 24);

	static Rectangle Offset(Rectangle r, Vector2 o) => new(r.X + (int)o.X, r.Y + (int)o.Y, r.Width, r.Height);

	/// <summary>Index of the first of <paramref name="count"/> cells under the mouse, or -1.</summary>
	int Hovered(Vector2 mouse, int count, Func<int, Rectangle> cell)
	{
		Point p = mouse.ToPoint();
		Vector2 o = Origin;
		for (int i = 0; i < count; i++) {
			if (Offset(cell(i), o).Contains(p))
				return i;
		}
		return -1;
	}

	int HoveredSkill(Vector2 mouse) => Hovered(mouse, Math.Min(Skills.Count, Columns * SkillRows), SkillCell);
	int HoveredForm(Vector2 mouse) => Hovered(mouse, Math.Min(Forms.Count, Columns * FormRows), FormCell);
	int HoveredSlot(Vector2 mouse) => Hovered(mouse, SkillPlayer.SlotCount + 1, LoadoutSlot);

	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		if (ContainsPoint(Main.MouseScreen))
			Main.LocalPlayer.mouseInterface = true;
	}

	public override void LeftClick(UIMouseEvent evt)
	{
		base.LeftClick(evt);
		SkillPlayer sp = Main.LocalPlayer.GetModPlayer<SkillPlayer>();
		Vector2 mouse = evt.MousePosition;
		if (Offset(CloseButton, Origin).Contains(mouse.ToPoint())) {
			SkillMenuSystem.SetOpen(false);
			return;
		}
		int cell = HoveredSkill(mouse);
		if (cell >= 0) {
			ModSkill skill = Skills[cell];
			Selected = Selected == skill ? null : skill;
			SelectedForm = null;
			SoundEngine.PlaySound(SoundID.MenuTick);
			return;
		}
		int formCell = HoveredForm(mouse);
		if (formCell >= 0) {
			ModForm form = Forms[formCell];
			SelectedForm = SelectedForm == form ? null : form;
			Selected = null;
			SoundEngine.PlaySound(SoundID.MenuTick);
			return;
		}
		int slot = HoveredSlot(mouse);
		if (slot >= 0 && Selected != null && sp.Knows(Selected)) {
			Assign(sp, slot, Selected);
			Selected = null;
			SoundEngine.PlaySound(SoundID.Grab);
		}
	}

	public override void RightClick(UIMouseEvent evt)
	{
		base.RightClick(evt);
		int slot = HoveredSlot(evt.MousePosition);
		if (slot >= 0) {
			Assign(Main.LocalPlayer.GetModPlayer<SkillPlayer>(), slot, null);
			SoundEngine.PlaySound(SoundID.MenuTick);
		}
	}

	static void Assign(SkillPlayer sp, int slot, ModSkill skill)
	{
		if (slot == SkillPlayer.SlotCount)
			sp.SetSecondary(skill);
		else
			sp.SetSlot(slot, skill);
	}

	protected override void DrawSelf(SpriteBatch sb)
	{
		base.DrawSelf(sb);
		SkillPlayer sp = Main.LocalPlayer.GetModPlayer<SkillPlayer>();
		FormPlayer fp = Main.LocalPlayer.GetModPlayer<FormPlayer>();
		Vector2 o = Origin;
		Vector2 mouse = Main.MouseScreen;
		DynamicSpriteFont font = FontAssets.MouseText.Value;

		Utils.DrawBorderString(sb, T("Title"), o + new Vector2(Pad, 14), SkillSlotDrawer.Gold, 1.1f);

		Rectangle close = Offset(CloseButton, o);
		bool closeHover = close.Contains(mouse.ToPoint());
		Utils.DrawBorderString(sb, "X", new Vector2(close.X + 6, close.Y + 2), closeHover ? SkillSlotDrawer.Gold : Color.White, 1f);

		// Skill grid.
		IReadOnlyList<ModSkill> skills = Skills;
		int hoverSkill = HoveredSkill(mouse);
		for (int i = 0; i < skills.Count && i < Columns * SkillRows; i++) {
			ModSkill skill = skills[i];
			Vector2 pos = Offset(SkillCell(i), o).TopLeft();
			bool known = sp.Knows(skill);
			SkillSlotDrawer.DrawFrame(sb, pos, skill == Selected || i == hoverSkill);
			SkillSlotDrawer.DrawIcon(sb, pos, skill, false, !known);
			if (!known)
				Utils.DrawBorderString(sb, "?", pos + new Vector2(19, 13), Question, 1f);
		}

		// Form grid, with a mastery bar under each.
		Utils.DrawBorderString(sb, T("Forms"), o + new Vector2(Pad, FormsLabelTop), Color.White, 0.9f);
		IReadOnlyList<ModForm> forms = Forms;
		int hoverForm = HoveredForm(mouse);
		for (int i = 0; i < forms.Count && i < Columns * FormRows; i++) {
			ModForm form = forms[i];
			Vector2 pos = Offset(FormCell(i), o).TopLeft();
			bool unlocked = fp.IsUnlocked(form);
			SkillSlotDrawer.DrawFrame(sb, pos, form == SelectedForm || i == hoverForm || fp.Active == form || fp.Overlay == form);
			SkillSlotDrawer.DrawFormIcon(sb, pos, form, !unlocked);
			if (!unlocked)
				Utils.DrawBorderString(sb, "?", pos + new Vector2(19, 13), Question, 1f);
			else
				SkillSlotDrawer.DrawMastery(sb, pos, fp.Mastery(form), form.Aura.Hot);
		}

		// Details panel.
		var detail = new Rectangle((int)o.X + DetailLeft, (int)o.Y + GridTop, DetailWidth, FormsTop + FormRows * Cell - GridTop);
		Utils.DrawInvBG(sb, detail, new Color(18, 24, 52) * 0.9f);
		ModSkill shown = hoverSkill >= 0 ? skills[hoverSkill] : hoverForm >= 0 ? null : Selected;
		ModForm shownForm = hoverForm >= 0 ? forms[hoverForm] : hoverSkill >= 0 ? null : SelectedForm;
		if (shown != null)
			DrawDetails(sb, shown, sp, detail, font);
		else if (shownForm != null)
			DrawFormDetails(sb, shownForm, fp, detail, font);
		else
			DrawWrapped(sb, T("Help"), new Vector2(detail.X + 12, detail.Y + 12), detail.Width - 24, Hint, font);

		// Loadout: the bar, then the right-button slot.
		Utils.DrawBorderString(sb, T("Loadout"), o + new Vector2(Pad, LoadoutLabelTop), Color.White, 0.9f);
		int hoverSlot = HoveredSlot(mouse);
		bool placing = Selected != null && sp.Knows(Selected);
		for (int i = 0; i <= SkillPlayer.SlotCount; i++) {
			bool secondary = i == SkillPlayer.SlotCount;
			Vector2 pos = Offset(LoadoutSlot(i), o).TopLeft();
			SkillSlotDrawer.DrawFrame(sb, pos, placing && i == hoverSlot);
			SkillSlotDrawer.DrawIcon(sb, pos, secondary ? sp.SecondarySkill : sp.SkillInSlot(i), false, false);
			SkillSlotDrawer.DrawKey(sb, pos, secondary ? Language.GetTextValue("Mods.Zenkai.UI.SkillBar.RightButton") : SkillSlotDrawer.KeyLabel(i));
		}
	}

	static void DrawDetails(SpriteBatch sb, ModSkill skill, SkillPlayer sp, Rectangle area, DynamicSpriteFont font)
	{
		var pos = new Vector2(area.X + 12, area.Y + 10);
		int width = area.Width - 24;

		if (!sp.Knows(skill)) {
			Utils.DrawBorderString(sb, T("Unlearned"), pos, Muted, 1f);
			pos.Y += 30;
			DrawWrapped(sb, skill.LearnHint.Value, pos, width, Hint, font);
			return;
		}

		Utils.DrawBorderString(sb, skill.DisplayName.Value, pos, SkillSlotDrawer.Gold, 1.05f);
		pos.Y += 26;
		Utils.DrawBorderString(sb, T("Kind." + skill.Kind), pos, new Color(140, 210, 255), TextScale);
		pos.Y += 24;

		void Line(string text)
		{
			Utils.DrawBorderString(sb, text, pos, Color.White, TextScale);
			pos.Y += LineHeight;
		}
		Line(skill.SustainDrain > 0f
			? T("CostSustain", (int)sp.CostOf(skill), (int)(skill.SustainDrain * sp.KiCostMultiplier))
			: T("Cost", (int)sp.CostOf(skill)));
		if (skill.BaseDamage > 0)
			Line(T("Damage", skill.DamageFor(sp.Player)));
		if (skill.Cooldown > 0)
			Line(T("Cooldown", (skill.Cooldown / 60f).ToString("0.#")));
		pos.Y += 8;
		DrawWrapped(sb, skill.Description.Value, pos, width, Body, font);
	}

	static void DrawFormDetails(SpriteBatch sb, ModForm form, FormPlayer fp, Rectangle area, DynamicSpriteFont font)
	{
		var pos = new Vector2(area.X + 12, area.Y + 10);
		int width = area.Width - 24;
		if (!fp.IsUnlocked(form)) {
			Utils.DrawBorderString(sb, T("Locked"), pos, Muted, 1f);
			pos.Y += 30;
			DrawWrapped(sb, form.UnlockHint.Value, pos, width, Hint, font);
			return;
		}

		Utils.DrawBorderString(sb, form.DisplayName.Value, pos, form.Aura.Rim, 1.05f);
		pos.Y += 26;
		float m = fp.Mastery(form);
		Utils.DrawBorderString(sb, T("FormMastery", (int)(m * 100f)), pos, form.Aura.Hot, TextScale);
		pos.Y += 24;

		void Line(string text)
		{
			Utils.DrawBorderString(sb, text, pos, Color.White, TextScale);
			pos.Y += LineHeight;
		}
		Line(T("FormDamage", form.KiDamageMultiplier.ToString("0.#")));
		if (form.SpeedBonus > 0f)
			Line(T("FormSpeed", (int)Math.Round(form.SpeedBonus * 100f)));
		if (form.DefenseBonus > 0)
			Line(T("FormDefense", form.DefenseBonus));
		if (form.DodgeChance > 0f)
			Line(T("FormDodge", (int)Math.Round(form.DodgeChance * 100f)));
		if (form.KiCritBonus > 0f)
			Line(T("FormCrit", (int)Math.Round(form.KiCritBonus * 100f)));
		if (form.EnduranceBonus > 0f)
			Line(T("FormEndurance", (int)Math.Round(form.EnduranceBonus * 100f)));
		if (form.MaxKiBonus > 0)
			Line(T("FormMaxKi", form.MaxKiBonus));
		if (form.KiRegenMultiplier != 1f)
			Line(T("FormKiRegen", form.KiRegenMultiplier.ToString("0.#")));
		if (form.KiCostMultiplier != 1f)
			Line(T("FormKiCost", form.KiCostMultiplier.ToString("0.##")));
		if (form.ChargeRateMultiplier != 1f)
			Line(T("FormChargeRate", form.ChargeRateMultiplier.ToString("0.#")));
		if (form.LifeRegenBonus > 0f)
			Line(T("FormLifeRegen", form.LifeRegenBonus.ToString("0.#")));
		if (form.SurgeBonus > 0f)
			Line(T("FormSurge", (int)Math.Round(form.SurgeBonus * 100f)));
		if (form.LightningInterval > 0)
			Line(T("FormLightning"));
		if (form.DodgeCounter)
			Line(T("FormCounter"));
		if (form.KiDrainAtZero > 0f)
			Line(T("FormKiDrain", form.KiDrain(m).ToString("0.#"), form.KiDrainAtFull.ToString("0.#")));
		if (form.LifeDrainAtZero > 0f)
			Line(T("FormLifeDrain", form.LifeDrain(m).ToString("0.#"), form.LifeDrainAtFull.ToString("0.#")));
		pos.Y += 8;
		DrawWrapped(sb, form.Description.Value, pos, width, Body, font);
	}

	static void DrawWrapped(SpriteBatch sb, string text, Vector2 pos, int width, Color color, DynamicSpriteFont font)
	{
		string[] lines = Utils.WordwrapString(text, font, (int)(width / TextScale), 12, out int count);
		for (int i = 0; i <= count && i < lines.Length; i++) {
			if (string.IsNullOrEmpty(lines[i]))
				continue;
			Utils.DrawBorderString(sb, lines[i], pos.Floor(), color, TextScale);
			pos.Y += LineHeight;
		}
	}
}
