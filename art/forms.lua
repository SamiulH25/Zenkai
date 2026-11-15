-- Builds the phase 3 transformation art: form icons, Super Saiyan hair, the transformation burst,
-- the tintable spark dust and the exhaustion debuff icon.
-- Run: aseprite -b --script-param root=<repo> --script art/forms.lua   (tools/art.sh does this)

root = app.params["root"] -- global: art/lib.lua reads it
assert(root, "pass --script-param root=<repo path>")
local lib = dofile(root .. "/art/lib.lua")
local rgba, disc, cone, union = lib.rgba, lib.disc, lib.cone, lib.union

-- Palettes. Kaioken and gold match AuraStyle.Kaioken and AuraStyle.SuperSaiyan.
local RED = { o = rgba(0x5a0a14), m = rgba(0xe0242c), h = rgba(0xff5a3c), r = rgba(0xffb4a0), w = rgba(0xfff0e8) }
local GOLD = { o = rgba(0x6a4208), d = rgba(0xc07c14), m = rgba(0xf0b020), h = rgba(0xffe060), r = rgba(0xfff6b0) }
local BLUE = { o = rgba(0x14307a), m = rgba(0x2478e0), h = rgba(0x3cbcff), r = rgba(0x8ae8ff) }
-- Grey sprites are tinted in code, so white becomes the tint colour.
local G = { o = rgba(0x3c3c3c), d = rgba(0x8c8c8c), m = rgba(0xc8c8c8), w = rgba(0xffffff) }

---------------------------------------------------------------------------
-- Icons, 16x16.
---------------------------------------------------------------------------
-- Kaioken: a red flame, three tongues licking up.
lib.single("Kaioken", "Forms", 16, 16, function(c)
  c:shape(union(
    disc(8, 11, 4.4),
    cone(8, 10, 3.4, 8, 0.6, 0.5),
    cone(5.5, 11, 2.4, 2.2, 3.5, 0.5),
    cone(10.5, 11, 2.4, 13.6, 4.5, 0.5)
  ), { { 1, RED.m }, { 2, RED.h }, { 3, RED.r }, { 99, RED.w } })
  c:outline(RED.o)
end)

-- Super Saiyan: a crown of golden spikes.
lib.single("SuperSaiyan", "Forms", 16, 16, function(c)
  c:shape(union(
    disc(8, 12, 4),
    cone(8, 11, 2.6, 8, 0.6, 0.5),
    cone(6, 11, 2.4, 1.6, 2.6, 0.5),
    cone(10, 11, 2.4, 14.4, 2.6, 0.5),
    cone(5, 12.5, 2, 0.8, 9, 0.4),
    cone(11, 12.5, 2, 15.2, 9, 0.4)
  ), { { 1, GOLD.d }, { 2, GOLD.m }, { 3, GOLD.h }, { 99, GOLD.r } })
  c:outline(GOLD.o)
end)

-- Exhaustion debuff: a ki orb run dry, the bottom half drained grey.
lib.single("KiExhaustion", "Buffs", 16, 16, function(c)
  local grey, greyLo = rgba(0x5a6074), rgba(0x3e4256)
  c:shape(disc(8, 8, 6.4), { { 1, BLUE.m }, { 2.2, BLUE.h }, { 99, BLUE.r } })
  for y = 8, 15 do
    for x = 0, 15 do
      if c:get(x, y) then c:set(x, y, (y == 8) and greyLo or grey) end
    end
  end
  c:outline(BLUE.o)
  -- A drip falling from the empty half.
  c:set(8, 15, BLUE.h)
end)

---------------------------------------------------------------------------
-- Super Saiyan hair: 20x34. Rows 6.. line up with a vanilla 20x28 player frame (FormHairLayer
-- draws it 6 art rows higher), facing right. The face area is kept clear except for bangs.
---------------------------------------------------------------------------
lib.single("SuperSaiyanHair", "Forms", 20, 34, function(c)
  local hair = union(
    disc(10.5, 12.5, 4.6),
    cone(10, 12, 3.2, 9.5, 0.5, 0.6),
    cone(8, 12, 3.0, 2.5, 2.5, 0.6),
    cone(7, 14, 2.6, 0.5, 9.5, 0.5),
    cone(7, 16, 2.0, 1.5, 20, 0.5),
    cone(12.5, 12, 2.6, 16.5, 3.5, 0.5),
    cone(14, 13, 1.8, 19, 9, 0.4)
  )
  local face = function(x, y) return x >= 9 and y >= 13.5 end
  local bands = { { 1, GOLD.d }, { 2, GOLD.m }, { 3.2, GOLD.h }, { 99, GOLD.r } }
  c:shape(function(x, y)
    if face(x, y) then return -1 end
    return hair(x, y)
  end, bands)
  -- Bangs over the forehead.
  c:shape(union(cone(11, 11.5, 1.4, 11.5, 14.6, 0.4), cone(13.5, 12, 1.2, 14.5, 14.2, 0.3)), bands)
  c:outline(GOLD.o)
end)

---------------------------------------------------------------------------
-- Transformation burst: 48x48, 4 frames, grey (tinted by the form's colour in code).
---------------------------------------------------------------------------
lib.sheet("FormBurst", "Projectiles", 48, 48, 4, function(i, c)
  local ring = function(r, t)
    return function(x, y) return t - math.abs(math.sqrt((x - 24) ^ 2 + (y - 24) ^ 2) - r) end
  end
  if i == 0 then
    c:shape(disc(24, 24, 9), { { 1, G.d }, { 2.5, G.m }, { 99, G.w } })
  elseif i == 1 then
    c:shape(ring(14, 2.6), { { 0.8, G.d }, { 1.6, G.m }, { 99, G.w } })
  elseif i == 2 then
    c:shape(ring(19, 1.8), { { 0.8, G.m }, { 99, G.w } })
  else
    c:shape(function(x, y)
      local a = math.atan(y - 24, x - 24)
      if math.sin(a * 6) < 0.1 then return -1 end
      return 1.1 - math.abs(math.sqrt((x - 24) ^ 2 + (y - 24) ^ 2) - 22)
    end, { { 99, G.m } })
  end
  c:outline(G.o)
end)

---------------------------------------------------------------------------
-- Tintable spark dust: three 2x2 frames stacked, bright to dim.
---------------------------------------------------------------------------
do
  local sprite = Sprite(2, 6, ColorMode.RGB)
  local img = sprite.cels[1].image
  for y = 0, 1 do for x = 0, 1 do img:drawPixel(x, y, G.w) end end
  for y = 2, 3 do for x = 0, 1 do img:drawPixel(x, y, (x + y) % 2 == 0 and G.w or G.m) end end
  img:drawPixel(0, 4, G.m) img:drawPixel(1, 4, G.d) img:drawPixel(0, 5, G.d)
  lib.export(sprite, "TintSpark", "Zenkai/Assets/Dusts/TintSpark.png")
  sprite:close()
end

print("wrote form icons, hair, burst, spark and debuff icon")
