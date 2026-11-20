-- Builds the overhaul art: the icons for the extra skills and forms, and the sprites of the new
-- projectiles (piercing and final beam pieces, energy disc, ki wave, spirit bomb, ki barrier).
-- Run: aseprite -b --script-param root=<repo> --script art/skills2.lua   (tools/art.sh does this)
-- Authored at 1x and exported at 2x nearest-neighbour, flat bands plus a dark outline.

root = app.params["root"] -- global: art/lib.lua reads it
assert(root, "pass --script-param root=<repo path>")
local lib = dofile(root .. "/art/lib.lua")
local rgba, disc, cone, union = lib.rgba, lib.disc, lib.cone, lib.union

-- Palettes: outline, mid, hot, rim, core (edge inward). Ki blue matches skills.lua.
local KI = { o = rgba(0x14307a), m = rgba(0x2478e0), h = rgba(0x3cbcff), r = rgba(0x8ae8ff), w = rgba(0xeaffff) }
local VIO = { o = rgba(0x35105e), m = rgba(0x7a30d0), h = rgba(0xb468ff), r = rgba(0xdcb0ff), w = rgba(0xfff0ff) }
local GLD = { o = rgba(0x6a4208), m = rgba(0xe09a18), h = rgba(0xffd040), r = rgba(0xfff08a), w = rgba(0xffffe8) }
local RED = { o = rgba(0x5a0a14), m = rgba(0xe0242c), h = rgba(0xff5a3c), r = rgba(0xffb4a0), w = rgba(0xfff0e8) }
local SLV = { o = rgba(0x2c3244), m = rgba(0x8c98b4), h = rgba(0xc8d2e8), r = rgba(0xe8eeff), w = rgba(0xffffff) }
local VIOLET_W = { o = rgba(0x3a2a6a), m = rgba(0x9a8ae0), h = rgba(0xd0c6ff), r = rgba(0xece6ff), w = rgba(0xffffff) }

local function bands(p) return { { 1, p.m }, { 2, p.h }, { 3, p.r }, { 99, p.w } } end
local function thin(p) return { { 1, p.h }, { 2, p.r }, { 99, p.w } } end

---------------------------------------------------------------------------
-- Skill icons, 16x16.
---------------------------------------------------------------------------
-- Piercing beam: a thin violet lance with a sharp head.
lib.single("PiercingBeam", "Skills", 16, 16, function(c)
  c:shape(union(cone(0, 8, 1.4, 12, 8, 1.6), cone(11, 8, 2.8, 15.6, 8, 0.2)), bands(VIO))
  c:set(14, 5, VIO.r) c:set(14, 11, VIO.r) c:set(12, 3, VIO.h) c:set(12, 13, VIO.h)
  c:outline(VIO.o)
end)

-- Final beam: a fat golden beam with a huge head.
lib.single("FinalBeam", "Skills", 16, 16, function(c)
  c:shape(union(cone(0, 8, 3.4, 9, 8, 4.2), disc(11, 8, 5.2)), bands(GLD))
  c:outline(GLD.o)
  c:set(15, 1, GLD.r) c:set(15, 14, GLD.r) c:set(14, 0, GLD.h)
end)

-- Heavy strike: a golden impact star with a hard white core.
lib.single("HeavyStrike", "Skills", 16, 16, function(c)
  c:shape(function(x, y)
    local dx, dy = math.abs(x - 8), math.abs(y - 8)
    local straight = math.min(dx, dy) < 1.1 and math.max(dx, dy) < 7.6
    local diag = math.abs(dx - dy) < 1.1 and math.max(dx, dy) < 5
    return math.max(4.2 - (dx + dy) * 0.8, (straight or diag) and 0.5 or -1)
  end, { { 0.8, GLD.m }, { 1.8, GLD.h }, { 2.8, GLD.r }, { 99, GLD.w } })
  c:outline(GLD.o)
end)

-- Instant movement: two staggered chevrons with a streak behind.
lib.single("InstantMovement", "Skills", 16, 16, function(c)
  local chev = function(cx, cy, s)
    return function(x, y)
      local dx, dy = x - cx, math.abs(y - cy)
      return 1.2 - math.abs(dx + dy - s * 0) - (dy > s and 9 or 0) - (dx > 0.8 and 9 or 0) + 0.2
    end
  end
  -- Chevrons drawn as short thick diagonals.
  for _, ox in ipairs({ 5, 10 }) do
    c:shape(union(cone(ox - 2, 3, 1.1, ox + 1.5, 8, 1.1), cone(ox - 2, 13, 1.1, ox + 1.5, 8, 1.1)), thin(KI))
  end
  c:shape(cone(0, 8, 0.6, 4, 8, 0.9), thin(KI))
  c:outline(KI.o)
end)

-- Barrage: a fan of four small blasts.
lib.single("Barrage", "Skills", 16, 16, function(c)
  local pts = { { 12.5, 3.5 }, { 13.5, 7.5 }, { 12.5, 11.5 }, { 10, 14 } }
  for _, p in ipairs(pts) do
    c:shape(union(disc(p[1], p[2], 1.9), cone(2, 8, 0.4, p[1], p[2], 1.5)), thin(KI))
  end
  c:set(12, 3, KI.w) c:set(13, 7, KI.w) c:set(12, 11, KI.w)
  c:outline(KI.o)
end)

-- Homing blast: a ball curling round on a curved trail.
lib.single("HomingBlast", "Skills", 16, 16, function(c)
  c:shape(disc(11.5, 5.5, 3.4), bands(KI))
  for i = 0, 22 do
    local t = i / 22
    local x = 1.5 + 8 * t
    local y = 14 - 11 * math.sin(t * math.pi * 0.62) + 0
    c:shape(disc(x, y, 0.5 + 0.8 * t), thin(KI))
  end
  c:set(12, 4, KI.w)
  c:outline(KI.o)
end)

-- Energy disc: a spinning blade seen edge-on, with its speed streaks.
lib.single("EnergyDisc", "Skills", 16, 16, function(c)
  c:shape(function(x, y)
    local dx, dy = (x - 8) / 7.2, (y - 8) / 2.6
    return (1 - math.sqrt(dx * dx + dy * dy)) * 2.6
  end, bands(KI))
  c:shape(cone(0, 3.5, 0.4, 5, 3.5, 0.5), thin(KI))
  c:shape(cone(0, 12.5, 0.4, 5, 12.5, 0.5), thin(KI))
  c:outline(KI.o)
end)

-- Explosive wave: a ring blasting out from a hot core.
lib.single("ExplosiveWave", "Skills", 16, 16, function(c)
  c:shape(function(x, y)
    local d = math.sqrt((x - 8) ^ 2 + (y - 8) ^ 2)
    return math.max(1.5 - math.abs(d - 6), 2.6 - d)
  end, { { 0.8, KI.m }, { 1.4, KI.h }, { 2.2, KI.r }, { 99, KI.w } })
  c:outline(KI.o)
end)

-- Solar flash: a blinding white-yellow star.
lib.single("SolarFlash", "Skills", 16, 16, function(c)
  c:shape(function(x, y)
    local dx, dy = math.abs(x - 8), math.abs(y - 8)
    local straight = math.min(dx, dy) < 0.9 and math.max(dx, dy) < 7.8
    local diag = math.abs(dx - dy) < 0.9 and math.max(dx, dy) < 5.6
    return math.max(3.8 - math.sqrt(dx * dx + dy * dy), (straight or diag) and 0.5 or -1)
  end, { { 1, GLD.h }, { 2, GLD.r }, { 99, GLD.w } })
  c:outline(GLD.o)
end)

-- Ki barrier: a glass bubble with a glint.
lib.single("KiBarrier", "Skills", 16, 16, function(c)
  c:shape(function(x, y)
    local d = math.sqrt((x - 8) ^ 2 + (y - 8) ^ 2)
    return math.min(6.8 - d, d - 3.0 + 3) - (d < 4.4 and 9 or 0)
  end, { { 1.2, KI.h }, { 2.2, KI.m }, { 99, KI.m } })
  c:shape(cone(4.5, 5.5, 0.6, 7, 4, 0.6), { { 99, KI.w } })
  c:set(4, 8, KI.r) c:set(4, 9, KI.r)
  c:outline(KI.o)
end)

-- Spirit bomb: a big sphere with ki orbs being drawn in.
lib.single("SpiritBomb", "Skills", 16, 16, function(c)
  c:shape(disc(8, 8, 4.8), bands(KI))
  c:shape(disc(2.5, 2.5, 1.2), thin(KI))
  c:shape(disc(13.5, 2.5, 1.2), thin(KI))
  c:shape(disc(2.5, 13.5, 1.2), thin(KI))
  c:shape(disc(13.5, 13.5, 1.2), thin(KI))
  c:set(7, 6, KI.w)
  c:outline(KI.o)
end)

---------------------------------------------------------------------------
-- Form icons, 16x16.
---------------------------------------------------------------------------
-- Kaioken levels: the flame grows, gains tongues and finally a halo.
local function flame(n, halo)
  return function(c)
    local parts = { disc(8, 11.5, 3.6 + n * 0.5), cone(8, 10, 3.0 + n * 0.3, 8, 0.6 - n * 0.2, 0.5) }
    local spread = { { 4.5, 3.5 }, { 11.5, 3.5 }, { 2.5, 7 }, { 13.5, 7 } }
    for i = 1, n + 1 do
      local s = spread[i]
      parts[#parts + 1] = cone(8 + (s[1] - 8) * 0.35, 11, 2.0, s[1], s[2], 0.4)
    end
    c:shape(union(table.unpack(parts)), { { 1, RED.m }, { 2, RED.h }, { 3, RED.r }, { 99, RED.w } })
    if halo then
      c:shape(function(x, y)
        local d = math.sqrt((x - 8) ^ 2 + (y - 8.5) ^ 2)
        return 0.7 - math.abs(d - 7.2)
      end, { { 99, RED.h } })
    end
    c:outline(RED.o)
  end
end
lib.single("KaiokenX3", "Forms", 16, 16, flame(1, false))
lib.single("KaiokenX10", "Forms", 16, 16, flame(2, false))
lib.single("KaiokenX20", "Forms", 16, 16, flame(3, true))

-- A crown of spikes in a palette. spikes = { {x, y} tips }, all rooted at the head disc.
local function crown(p, tips, headR, base)
  return function(c)
    local parts = { disc(8, 12, headR or 4) }
    for _, t in ipairs(tips) do
      parts[#parts + 1] = cone(8 + (t[1] - 8) * 0.4, 11.5 + (t[2] - 11.5) * 0.15, 2.3, t[1], t[2], 0.4)
    end
    c:shape(union(table.unpack(parts)), base or { { 1, p.m }, { 2, p.h }, { 3, p.r }, { 99, p.w } })
    c:outline(p.o)
  end
end
local TIPS2 = { { 8, 0.6 }, { 3, 2 }, { 13, 2 }, { 0.8, 9 }, { 15.2, 9 } }
local TIPS3 = { { 8, 0 }, { 4, 0.4 }, { 12, 0.4 }, { 0.6, 4 }, { 15.4, 4 }, { 0.4, 12 }, { 15.6, 12 } }
local TIPS_GOD = { { 8, 0.8 }, { 4, 2.2 }, { 12, 2.2 }, { 1.2, 8.5 }, { 14.8, 8.5 } }

local GOLD_BANDS = { o = rgba(0x6a4208), d = rgba(0xc07c14), m = rgba(0xf0b020), h = rgba(0xffe060), r = rgba(0xfff6b0) }
local goldBands = { { 1, GOLD_BANDS.d }, { 2, GOLD_BANDS.m }, { 3, GOLD_BANDS.h }, { 99, GOLD_BANDS.r } }

-- Super Saiyan 2: the gold crown with blue sparks crackling round it.
lib.single("SuperSaiyan2", "Forms", 16, 16, function(c)
  crown(GOLD_BANDS, TIPS2, 4, goldBands)(c)
  for _, p in ipairs({ { 1, 5 }, { 14, 5 }, { 3, 12 }, { 13, 14 } }) do
    c:set(p[1], p[2], KI.h) c:set(p[1] + 1, p[2] + 1, KI.r)
  end
end)

-- Super Saiyan 3: a longer, wilder crown.
lib.single("SuperSaiyan3", "Forms", 16, 16, crown(GOLD_BANDS, TIPS3, 3.6, goldBands))

-- Super Saiyan God: a crimson crown.
lib.single("SuperSaiyanGod", "Forms", 16, 16, crown(RED, TIPS_GOD, 4))

-- Super Saiyan Blue: the same crown in ki blue.
lib.single("SuperSaiyanBlue", "Forms", 16, 16, crown(KI, TIPS_GOD, 4))

-- Potential Unleashed: a pale violet-white crown inside a ring of power.
lib.single("PotentialUnleashed", "Forms", 16, 16, function(c)
  crown(VIOLET_W, { { 8, 2.5 }, { 5, 4 }, { 11, 4 }, { 3, 9 }, { 13, 9 } }, 3.4)(c)
  c:shape(function(x, y)
    local d = math.sqrt((x - 8) ^ 2 + (y - 8) ^ 2)
    return 0.6 - math.abs(d - 7.4)
  end, { { 99, VIOLET_W.h } })
  c:outline(VIOLET_W.o)
end)

-- Ultra Instinct: a silver flame crown with a white eye.
lib.single("UltraInstinct", "Forms", 16, 16, function(c)
  crown(SLV, TIPS_GOD, 4)(c)
  c:set(7, 12, SLV.o) c:set(8, 12, SLV.o)
end)

---------------------------------------------------------------------------
-- Beam pieces. Same layout as skills.lua: Orb (3 sizes x 2 flicker rows, square cells),
-- Start/Body/End as 3 frames stacked vertically. Body width 8 so the ripple tiles seamlessly.
---------------------------------------------------------------------------
local function beamSet(prefix, p, halfBody, orbCell, orbR, startSize, endSize, k)
  k = k or 1
  local function bnd(q) return { { 1 * k, q.m }, { 2 * k, q.h }, { 3.2 * k, q.r }, { 99, q.w } } end
  -- Orb
  do
    local sprite = Sprite(orbCell * 3, orbCell * 2, ColorMode.RGB)
    local img = sprite.cels[1].image
    for size = 0, 2 do
      for f = 0, 1 do
        local c = lib.canvas(orbCell, orbCell)
        local r = orbR[size + 1] - f * 0.5
        c:shape(disc(orbCell / 2, orbCell / 2, r), bnd(p))
        if f == 1 then c:set(math.floor(orbCell / 2) - 2, math.floor(orbCell / 2) - 2, p.w) end
        c:outline(p.o)
        c:blit(img, size * orbCell, f * orbCell)
      end
    end
    lib.export(sprite, prefix .. "Orb", "Zenkai/Assets/Projectiles/" .. prefix .. "Orb.png")
    sprite:close()
  end
  -- Start cap
  lib.sheet(prefix .. "Start", "Projectiles", startSize, startSize, 3, function(i, c)
    local r = startSize / 2 - 1 - (i == 1 and 0.5 or 0)
    c:shape(disc(startSize / 2, startSize / 2, r), bnd(p))
    if i == 1 then c:set(startSize // 2 - 2, startSize // 2 - 2, p.w) end
    c:outline(p.o)
  end)
  -- Body tile
  local bh = halfBody * 2 + 4
  lib.sheet(prefix .. "Body", "Projectiles", 8, bh, 3, function(i, c)
    for x = 0, 7 do
      local half = halfBody - 0.5 + 0.7 * math.sin(2 * math.pi * (x + i * 8 / 3) / 8)
      for y = 0, bh - 1 do
        local d = half - math.abs(y + 0.5 - bh / 2)
        if d > 0 then
          local col = (d <= 1) and p.o or (d <= 1 + k) and p.m or (d <= 1 + 2 * k) and p.h or (d <= 1 + 3.4 * k) and p.r or p.w
          c:set(x, y, col)
        end
      end
    end
  end)
  -- End cap
  lib.sheet(prefix .. "End", "Projectiles", endSize, endSize, 3, function(i, c)
    local mid = endSize / 2
    local flare = i
    c:shape(union(
      disc(mid - 1, mid, mid - 2.6 - (i == 1 and 0.4 or 0)),
      cone(mid + 1, mid - mid * 0.45, 1.4, endSize - 3 + flare % 2, 3 + flare, 0.5),
      cone(mid + 1, mid + mid * 0.45, 1.4, endSize - 3 + (flare + 1) % 2, endSize - 4 - flare, 0.5),
      cone(mid + 3, mid, 2.0, endSize - 1, mid, 0.7)
    ), bnd(p))
    c:outline(p.o)
  end)
end
beamSet("PiercingBeam", VIO, 3, 12, { 2.8, 4.0, 5.2 }, 10, 14)
beamSet("FinalBeam", GLD, 15, 28, { 6, 10, 13.4 }, 36, 44, 3.2)

---------------------------------------------------------------------------
-- Energy disc: 12x6 blade seen edge-on, 3 frames of spin (wide, narrow, wide with a notch).
---------------------------------------------------------------------------
lib.sheet("EnergyDisc", "Projectiles", 14, 8, 3, function(i, c)
  local rx = ({ 6.2, 4.4, 5.6 })[i + 1]
  local ry = ({ 2.4, 3.0, 2.6 })[i + 1]
  c:shape(function(x, y)
    local dx, dy = (x - 7) / rx, (y - 4) / ry
    return (1 - math.sqrt(dx * dx + dy * dy)) * 2.4
  end, bands(KI))
  c:outline(KI.o)
end)

---------------------------------------------------------------------------
-- Ki wave: 96x96, 4 frames of an expanding ring. Ring radii (1x) are half of KiWave.Radius.
---------------------------------------------------------------------------
lib.sheet("KiWave", "Projectiles", 96, 96, 4, function(i, c)
  local r = ({ 18, 30, 40, 46 })[i + 1] - 1
  local t = ({ 3.4, 2.8, 2.0, 1.2 })[i + 1]
  c:shape(function(x, y)
    local d = math.sqrt((x - 48) ^ 2 + (y - 48) ^ 2)
    return t - math.abs(d - r)
  end, { { 0.8, KI.m }, { 1.6, KI.h }, { 2.6, KI.r }, { 99, KI.w } })
  if i == 0 then c:shape(disc(48, 48, 10), thin(KI)) end
  c:outline(KI.o)
end)

---------------------------------------------------------------------------
-- Spirit bomb: three sizes by two flicker rows, 36x36 cells.
---------------------------------------------------------------------------
do
  local cell = 36
  local sprite = Sprite(cell * 3, cell * 2, ColorMode.RGB)
  local img = sprite.cels[1].image
  for size = 0, 2 do
    for f = 0, 1 do
      local c = lib.canvas(cell, cell)
      local r = ({ 6.5, 11, 16 })[size + 1] - f * 0.6
      c:shape(disc(cell / 2, cell / 2, r), { { 1.2, KI.m }, { 2.6, KI.h }, { 4.5, KI.r }, { 99, KI.w } })
      if f == 1 then c:set(cell // 2 - 3, cell // 2 - 3, KI.w) c:set(cell // 2 + 2, cell // 2 + 3, KI.r) end
      c:outline(KI.o)
      c:blit(img, size * cell, f * cell)
    end
  end
  lib.export(sprite, "SpiritBomb", "Zenkai/Assets/Projectiles/SpiritBomb.png")
  sprite:close()
end

---------------------------------------------------------------------------
-- Ki barrier: 36x36 bubble shell, 3 frames, a glint travelling round the rim.
---------------------------------------------------------------------------
lib.sheet("KiBarrier", "Projectiles", 36, 36, 3, function(i, c)
  c:shape(function(x, y)
    local d = math.sqrt((x - 18) ^ 2 + (y - 18) ^ 2)
    return 1.6 - math.abs(d - 15.6)
  end, { { 0.7, KI.m }, { 1.4, KI.h }, { 99, KI.r } })
  local a = math.rad(-125 + i * 22)
  c:shape(disc(18 + math.cos(a) * 15.6, 18 + math.sin(a) * 15.6, 1.9), { { 99, KI.w } })
  c:outline(KI.o)
end)

print("wrote overhaul skill and form icons and projectile sprites")
