using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
				ui.Draw(Main.spriteBatch, new GameTime());
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
/// The skill menu: a grid of every skill (unlearned ones as silhouettes), details for the hovered
/// or selected skill, and the four loadout slots. Click a skill, then a slot, to assign it;
/// right-click a slot to clear it.
/// </summary>
public class SkillMenuPanel : UIPanel
{
	const int W = 560;
	const int H = 336;
	const int Pad = 16;
	const int Columns = 4;
	const int Cell = SkillSlotDrawer.Size + 8;
	const int GridTop = 46;
	const int DetailLeft = Pad + Columns * Cell + 8;

	public ModSkill Selected;
	public ModForm SelectedForm;

	const int FormsLabelTop = GridTop + Cell + 2;
	const int FormsTop = FormsLabelTop + 24;

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

	List<ModSkill> Skills => SkillLoader.Ordered.ToList();

	Vector2 Origin
	{
		get {
			CalculatedStyle d = GetDimensions();
			return new Vector2((int)d.X & ~1, (int)d.Y & ~1);
		}
	}

	Rectangle GridCell(int i) => new(Pad + i % Columns * Cell, GridTop + i / Columns * Cell, SkillSlotDrawer.Size, SkillSlotDrawer.Size);
	static Rectangle LoadoutSlot(int i) => new(Pad + i * Cell, H - Pad - SkillSlotDrawer.Size, SkillSlotDrawer.Size, SkillSlotDrawer.Size);
	static Rectangle CloseButton => new(W - 34, 8, 24, 24);

	static Rectangle Offset(Rectangle r, Vector2 o) => new(r.X + (int)o.X, r.Y + (int)o.Y, r.Width, r.Height);

	int HoveredCell(Vector2 mouse)
	{
		List<ModSkill> skills = Skills;
		for (int i = 0; i < skills.Count; i++) {
			if (Offset(GridCell(i), Origin).Contains(mouse.ToPoint()))
				return i;
		}
		return -1;
	}

	List<ModForm> Forms => FormLoader.Ordered.ToList();

	static Rectangle FormCell(int i) => new(Pad + i % Columns * Cell, FormsTop + i / Columns * Cell, SkillSlotDrawer.Size, SkillSlotDrawer.Size);

	int HoveredForm(Vector2 mouse)
	{
		List<ModForm> forms = Forms;
		for (int i = 0; i < forms.Count; i++) {
			if (Offset(FormCell(i), Origin).Contains(mouse.ToPoint()))
				return i;
		}
		return -1;
	}

	int HoveredSlot(Vector2 mouse)
	{
		for (int i = 0; i < SkillPlayer.SlotCount; i++) {
			if (Offset(LoadoutSlot(i), Origin).Contains(mouse.ToPoint()))
				return i;
		}
		return -1;
	}

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
		int cell = HoveredCell(mouse);
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
			sp.SetSlot(slot, Selected);
			Selected = null;
			SoundEngine.PlaySound(SoundID.Grab);
		}
	}

	public override void RightClick(UIMouseEvent evt)
	{
		base.RightClick(evt);
		int slot = HoveredSlot(evt.MousePosition);
		if (slot >= 0) {
			Main.LocalPlayer.GetModPlayer<SkillPlayer>().SetSlot(slot, null);
			SoundEngine.PlaySound(SoundID.MenuTick);
		}
	}

	protected override void DrawSelf(SpriteBatch sb)
	{
		base.DrawSelf(sb);
		SkillPlayer sp = Main.LocalPlayer.GetModPlayer<SkillPlayer>();
		Vector2 o = Origin;
		Vector2 mouse = Main.MouseScreen;
		var font = FontAssets.MouseText.Value;

		Utils.DrawBorderString(sb, T("Title"), o + new Vector2(Pad, 14), SkillSlotDrawer.Gold, 1.1f);

		Rectangle close = Offset(CloseButton, o);
		bool closeHover = close.Contains(mouse.ToPoint());
		Utils.DrawBorderString(sb, "X", new Vector2(close.X + 6, close.Y + 2), closeHover ? SkillSlotDrawer.Gold : Color.White, 1f);

		// Skill grid.
		List<ModSkill> skills = Skills;
		int hoverCell = HoveredCell(mouse);
		for (int i = 0; i < skills.Count; i++) {
			ModSkill skill = skills[i];
			Vector2 pos = Offset(GridCell(i), o).TopLeft();
			bool known = sp.Knows(skill);
			SkillSlotDrawer.DrawFrame(sb, pos, skill == Selected || i == hoverCell);
			SkillSlotDrawer.DrawIcon(sb, pos, skill, false, !known);
			if (!known)
				Utils.DrawBorderString(sb, "?", pos + new Vector2(19, 13), new Color(150, 160, 200), 1f);
		}

		// Forms row, with a mastery bar under each.
		FormPlayer fp = Main.LocalPlayer.GetModPlayer<FormPlayer>();
		Utils.DrawBorderString(sb, T("Forms"), o + new Vector2(Pad, FormsLabelTop), Color.White, 0.9f);
		List<ModForm> forms = Forms;
		int hoverForm = HoveredForm(mouse);
		for (int i = 0; i < forms.Count; i++) {
			ModForm form = forms[i];
			Vector2 pos = Offset(FormCell(i), o).TopLeft();
			bool unlocked = fp.IsUnlocked(form);
			SkillSlotDrawer.DrawFrame(sb, pos, form == SelectedForm || i == hoverForm || fp.Active == form);
			SkillSlotDrawer.DrawFormIcon(sb, pos, form, !unlocked);
			if (!unlocked)
				Utils.DrawBorderString(sb, "?", pos + new Vector2(19, 13), new Color(150, 160, 200), 1f);
			else
				SkillSlotDrawer.DrawMastery(sb, pos, fp.Mastery(form), form.Aura.Hot);
		}

		// Details panel.
		var detail = new Rectangle((int)o.X + DetailLeft, (int)o.Y + GridTop, W - DetailLeft - Pad, H - GridTop - Pad);
		Utils.DrawInvBG(sb, detail, new Color(18, 24, 52) * 0.9f);
		ModSkill shown = hoverCell >= 0 ? skills[hoverCell] : hoverForm >= 0 ? null : Selected;
		ModForm shownForm = hoverForm >= 0 ? forms[hoverForm] : hoverCell >= 0 ? null : SelectedForm;
		if (shown != null)
			DrawDetails(sb, shown, sp, detail, font);
		else if (shownForm != null)
			DrawFormDetails(sb, shownForm, fp, detail, font);
		else
			DrawWrapped(sb, T("Help"), new Vector2(detail.X + 12, detail.Y + 12), detail.Width - 24, new Color(190, 200, 230), font);

		// Loadout.
		Utils.DrawBorderString(sb, T("Loadout"), o + new Vector2(Pad, H - Pad - SkillSlotDrawer.Size - 26), Color.White, 0.9f);
		int hoverSlot = HoveredSlot(mouse);
		for (int i = 0; i < SkillPlayer.SlotCount; i++) {
			Vector2 pos = Offset(LoadoutSlot(i), o).TopLeft();
			bool target = Selected != null && sp.Knows(Selected) && i == hoverSlot;
			SkillSlotDrawer.DrawFrame(sb, pos, target);
			SkillSlotDrawer.DrawIcon(sb, pos, sp.SkillInSlot(i), false, false);
			SkillSlotDrawer.DrawKey(sb, pos, SkillSlotDrawer.KeyLabel(i));
		}
	}

	void DrawDetails(SpriteBatch sb, ModSkill skill, SkillPlayer sp, Rectangle area, ReLogic.Graphics.DynamicSpriteFont font)
	{
		bool known = sp.Knows(skill);
		var pos = new Vector2(area.X + 12, area.Y + 10);
		int width = area.Width - 24;

		if (!known) {
			Utils.DrawBorderString(sb, T("Unlearned"), pos, new Color(160, 165, 190), 1f);
			pos.Y += 30;
			DrawWrapped(sb, skill.LearnHint.Value, pos, width, new Color(190, 200, 230), font);
			return;
		}

		Utils.DrawBorderString(sb, skill.DisplayName.Value, pos, SkillSlotDrawer.Gold, 1.05f);
		pos.Y += 26;
		Utils.DrawBorderString(sb, T("Kind." + skill.Kind), pos, new Color(140, 210, 255), 0.85f);
		pos.Y += 24;

		string cost = skill.SustainDrain > 0f
			? T("CostSustain", (int)sp.CostOf(skill), (int)(skill.SustainDrain * sp.KiCostMultiplier))
			: T("Cost", (int)sp.CostOf(skill));
		Utils.DrawBorderString(sb, cost, pos, Color.White, 0.85f);
		pos.Y += 20;
		if (skill.BaseDamage > 0) {
			Utils.DrawBorderString(sb, T("Damage", skill.DamageFor(sp.Player)), pos, Color.White, 0.85f);
			pos.Y += 20;
		}
		if (skill.Cooldown > 0) {
			Utils.DrawBorderString(sb, T("Cooldown", (skill.Cooldown / 60f).ToString("0.#")), pos, Color.White, 0.85f);
			pos.Y += 20;
		}
		pos.Y += 8;
		DrawWrapped(sb, skill.Description.Value, pos, width, new Color(200, 210, 235), font);
	}

	void DrawFormDetails(SpriteBatch sb, ModForm form, FormPlayer fp, Rectangle area, ReLogic.Graphics.DynamicSpriteFont font)
	{
		var pos = new Vector2(area.X + 12, area.Y + 10);
		int width = area.Width - 24;
		if (!fp.IsUnlocked(form)) {
			Utils.DrawBorderString(sb, T("Locked"), pos, new Color(160, 165, 190), 1f);
			pos.Y += 30;
			DrawWrapped(sb, form.UnlockHint.Value, pos, width, new Color(190, 200, 230), font);
			return;
		}

		Utils.DrawBorderString(sb, form.DisplayName.Value, pos, form.Aura.Rim, 1.05f);
		pos.Y += 26;
		float m = fp.Mastery(form);
		Utils.DrawBorderString(sb, T("FormTier", form.Tier, (int)(m * 100f)), pos, form.Aura.Hot, 0.85f);
		pos.Y += 24;

		void Line(string text)
		{
			Utils.DrawBorderString(sb, text, pos, Color.White, 0.85f);
			pos.Y += 20;
		}
		Line(T("FormDamage", (int)Math.Round(form.KiDamageBonus * 100f)));
		if (form.SpeedBonus > 0f)
			Line(T("FormSpeed", (int)Math.Round(form.SpeedBonus * 100f)));
		if (form.DefenseBonus > 0)
			Line(T("FormDefense", form.DefenseBonus));
		if (form.KiDrainAtZero > 0f)
			Line(T("FormKiDrain", form.KiDrain(m).ToString("0.#"), form.KiDrainAtFull.ToString("0.#")));
		if (form.LifeDrainAtZero > 0f)
			Line(T("FormLifeDrain", form.LifeDrain(m).ToString("0.#"), form.LifeDrainAtFull.ToString("0.#")));
		pos.Y += 8;
		DrawWrapped(sb, form.Description.Value, pos, width, new Color(200, 210, 235), font);
	}

	static void DrawWrapped(SpriteBatch sb, string text, Vector2 pos, int width, Color color, ReLogic.Graphics.DynamicSpriteFont font)
	{
		const float scale = 0.85f;
		string[] lines = Utils.WordwrapString(text, font, (int)(width / scale), 12, out int count);
		for (int i = 0; i <= count && i < lines.Length; i++) {
			if (string.IsNullOrEmpty(lines[i]))
				continue;
			Utils.DrawBorderString(sb, lines[i], pos.Floor(), color, scale);
			pos.Y += 20;
		}
	}
}
