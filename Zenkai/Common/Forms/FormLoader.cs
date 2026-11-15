using System.Collections.Generic;
using System.Linq;
using Terraria.ModLoader;

namespace Zenkai.Common.Forms;

/// <summary>Registry of every loaded <see cref="ModForm"/>.</summary>
public static class FormLoader
{
	static readonly List<ModForm> forms = new();

	public static IReadOnlyList<ModForm> Forms => forms;

	/// <summary>Forms in ladder order: lowest tier first.</summary>
	public static IEnumerable<ModForm> Ordered => forms.OrderBy(f => f.Tier).ThenBy(f => f.Name);

	internal static int Add(ModForm form)
	{
		forms.Add(form);
		return forms.Count - 1;
	}

	public static ModForm Find(string fullName) =>
		!string.IsNullOrEmpty(fullName) && ModContent.TryFind(fullName, out ModForm form) ? form : null;

	internal static void Unload() => forms.Clear();
}

public class FormLoaderSystem : ModSystem
{
	public override void Unload() => FormLoader.Unload();
}
