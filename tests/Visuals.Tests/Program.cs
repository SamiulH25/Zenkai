using System.Reflection;
using Microsoft.Xna.Framework;
using Zenkai.Common.Visuals;

// No graphics device, packages or game save needed. Exercise the real CPU rasterizer.
var styles = new (string Name, AuraStyle Style)[] {
    ("Ki", AuraStyle.Ki), ("Kaioken", AuraStyle.Kaioken),
    ("KaiokenX3", AuraStyle.KaiokenRank(1)), ("KaiokenX10", AuraStyle.KaiokenRank(2)),
    ("KaiokenX20", AuraStyle.KaiokenRank(3)), ("SuperSaiyan", AuraStyle.Gold(0)),
    ("SuperSaiyan2", AuraStyle.Gold(1)), ("SuperSaiyan3", AuraStyle.Gold(2)),
    ("Potential", AuraStyle.Potential), ("God", AuraStyle.God),
    ("Blue", AuraStyle.Blue), ("Instinct", AuraStyle.Instinct),
};
var states = new[] {
    new AuraState(1f, false, 0f), new AuraState(0.8f, false, 0f),
    new AuraState(0.85f, true, 7f), new AuraState(1f, true, 30f),
};
const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
var type = typeof(AuraRenderer);
var pixelsField = type.GetField("pixels", Private)!;
var tonguesField = type.GetField("tongues", Private)!;
var render = type.GetMethod("Render", Private)!;
int checks = 0, maxWidth = 0, maxHeight = 0;
foreach (var (name, style) in styles) {
    for (int seed = 0; seed < 3; seed++) {
        using var renderer = new AuraRenderer(seed * 7919 + 17);
        foreach (var state in states) {
            for (int frame = 0; frame < 24; frame++) {
                Terraria.Main.GameUpdateCount++;
                renderer.Update(style, state);
                CheckBorder(renderer, name, state);
            }
            // Force the longest possible lengths for this palette, with every tongue at peak life.
            var tongues = (Array)tonguesField.GetValue(renderer)!;
            for (int i = 0; i < tongues.Length; i++) {
                object tongue = tongues.GetValue(i)!;
                Type t = tongue.GetType();
                float life = (float)t.GetField("MaxLife")!.GetValue(tongue)!;
                t.GetField("Life")!.SetValue(tongue, life / 2f);
                t.GetField("MaxLength")!.SetValue(tongue, style.TongueLengthMax * 1.1f * (1f + style.CrestBoost));
                tongues.SetValue(tongue, i);
            }
            render.Invoke(renderer, new object[] { style, state });
            CheckBorder(renderer, name + " peak", state);
            var texture = renderer.Texture;
            renderer.Update(style, state);
            if (!ReferenceEquals(texture, renderer.Texture))
                throw new Exception("Same-tick update recreated the texture");
        }
        var lastTexture = renderer.Texture;
        renderer.Dispose();
        if (!lastTexture.IsDisposed || renderer.Texture != null)
            throw new Exception("Dispose did not release the texture");
    }
}
Console.WriteLine($"PASS: {checks} aura frames; all 12 palettes, 3 seeds, charge/hover/flight/overspeed and forced peaks. Largest canvas: {maxWidth}x{maxHeight} texture pixels.");

void CheckBorder(AuraRenderer renderer, string name, AuraState state)
{
    var texture = renderer.Texture;
    var pixels = (Color[])pixelsField.GetValue(renderer)!;
    int w = texture.Width, h = texture.Height;
    maxWidth = Math.Max(maxWidth, w);
    maxHeight = Math.Max(maxHeight, h);
    bool filled = false;
    // Two art pixels of transparent padding on every side, not just the outermost texel.
    for (int y = 0; y < h; y++) {
        for (int x = 0; x < w; x++) {
            if (pixels[y * w + x].A == 0) continue;
            filled = true;
            if (x < 4 || y < 4 || x >= w - 4 || y >= h - 4)
                throw new Exception($"Clipped aura: {name}, {state}, pixel ({x},{y}) in {w}x{h}");
        }
    }
    if (!filled) throw new Exception($"Empty aura: {name}");
    checks++;
}
