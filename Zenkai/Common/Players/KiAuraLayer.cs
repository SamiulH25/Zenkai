using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Zenkai.Common.Visuals;

namespace Zenkai.Common.Players;

/// <summary>
/// Draws the ki aura behind the player while charging, hovering or flying. The aura is generated
/// live by <see cref="AuraRenderer"/> from the player's <see cref="AuraStyle"/>: it always envelops
/// the whole body, and its flame tongues rise, or trail from the feet when flying head first.
/// </summary>
public class KiAuraLayer : PlayerDrawLayer
{
	// Intensity per state: drives tongue length, ripple strength and how fast it all moves.
	const float ChargeIntensity = 1f;
	const float FlightIntensity = 0.85f;
	const float HoverIntensity = 0.8f;

	static readonly AuraRenderer[] renderers = new AuraRenderer[Main.maxPlayers + 1];

	public override void Unload()
	{
		// Textures must be released on the main thread.
		Main.QueueMainThreadAction(() => {
			for (int i = 0; i < renderers.Length; i++) {
				renderers[i]?.Dispose();
				renderers[i] = null;
			}
		});
	}

	public override Position GetDefaultPosition() => PlayerDrawLayers.BeforeFirstVanillaLayer;

	public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
		=> drawInfo.shadow == 0f && drawInfo.drawPlayer.GetModPlayer<KiPlayer>().AuraOpacity > 0f;

	protected override void Draw(ref PlayerDrawSet drawInfo)
	{
		Player player = drawInfo.drawPlayer;
		KiPlayer ki = player.GetModPlayer<KiPlayer>();

		FlightPlayer flight = player.GetModPlayer<FlightPlayer>();
		bool headFirst = flight.Flying && flight.FlyingHeadFirst && !ki.Charging;
		float intensity = ki.Charging ? ChargeIntensity
			: flight.Flying && player.velocity.LengthSquared() > 1f ? FlightIntensity
			: HoverIntensity;
		var state = new AuraState(intensity, headFirst, player.velocity.Length());

		AuraRenderer renderer = renderers[player.whoAmI] ??= new AuraRenderer(player.whoAmI * 7919 + 17);
		renderer.Update(ki.AuraStyle, state);

		// Rotation is not set here: vanilla's TransformDrawData layer turns every DrawData by
		// player.fullRotation round the player's centre, so the aura follows the flight tilt
		// (adding it here as well doubled the tilt). Plain alpha blending: the navy edge band
		// darkens what is behind it.
		Color color = Color.White * ki.AuraOpacity;
		Vector2 centre = (drawInfo.Position + player.Size / 2f - Main.screenPosition).Floor();
		var origin = new Vector2(renderer.Texture.Width / 2, renderer.Texture.Height / 2);
		drawInfo.DrawDataCache.Add(new DrawData(renderer.Texture, centre, null, color, 0f, origin, 1f, SpriteEffects.None, 0));
	}
}
