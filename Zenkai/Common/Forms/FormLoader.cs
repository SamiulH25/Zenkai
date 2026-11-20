using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;

namespace Zenkai.Common.Forms;

/// <summary>Registry of every loaded <see cref="ModForm"/>, indexed by <see cref="ModForm.Type"/>.</summary>
public static class FormLoader
{
	static readonly List<ModForm> forms = new();
	static List<ModForm> ordered;

	public static IReadOnlyList<ModForm> Forms => forms;

	/// <summary>Forms from weakest to strongest. Built once, after every form has set its defaults.</summary>
	public static IReadOnlyList<ModForm> Ordered => ordered ??= forms.OrderBy(f => f.Tier).ThenBy(f => f.Name).ToList();

	internal static int Add(ModForm form)
	{
		forms.Add(form);
		ordered = null;
		return forms.Count - 1;
	}

	public static ModForm Get(int type) => type >= 0 && type < forms.Count ? forms[type] : null;

	public static ModForm Find(string fullName) =>
		!string.IsNullOrEmpty(fullName) && ModContent.TryFind(fullName, out ModForm form) ? form : null;

	internal static void ResetOrder() => ordered = null;

	internal static void Unload()
	{
		forms.Clear();
		ordered = null;
	}
}

public class FormLoaderSystem : ModSystem
{
	// Tiers are set in SetStaticDefaults, which runs after registration; drop any order cached before that.
	public override void PostSetupContent() => FormLoader.ResetOrder();

	public override void Unload() => FormLoader.Unload();
}
