-- Builds the phase 2 skill art: skill slot frames, skill icons and the ki projectiles.
-- Run: aseprite -b --script-param root=<repo> --script art/skills.lua   (tools/art.sh does this)
-- Authored at 1x and exported at 2x nearest-neighbour. Shapes are computed from signed
-- distances and cut into flat bands (no anti-aliasing), then given a dark outline.

local root = app.params["root"]
assert(root, "pass --script-param root=<repo path>")

local function rgba(hex, a)
  return app.pixelColor.rgba((hex >> 16) & 255, (hex >> 8) & 255, hex & 255, a or 255)
end

-- Ki palette, shared with the aura (Common/Visuals/AuraStyle.cs) and the ki bar.
local O  = rgba(0x14307a) -- outline navy
local I  = rgba(0x1c52c4) -- inner deep blue
local M  = rgba(0x2478e0) -- mid blue
local H  = rgba(0x3cbcff) -- hot cyan
local R  = rgba(0x8ae8ff) -- rim pale cyan
local W  = rgba(0xeaffff) -- white core
-- UI gold, from the ki bar frame.
local K  = rgba(0x15122b)
local G_HI, G, G_LO = rgba(0xf8dc7a), rgba(0xd6a23a), rgba(0x8c5f1e)
local WELL, WELL_LO = rgba(0x1d2447), rgba(0x10142e)

local function export(sprite, name, outPng)
  sprite:saveAs(root .. "/art/" .. name .. ".aseprite")
  local src = Image(sprite.spec)
  src:drawSprite(sprite, 1)
  local big = Sprite(sprite.width * 2, sprite.height * 2, ColorMode.RGB)
  local dst = big.cels[1].image
  for y = 0, sprite.height - 1 do
    for x = 0, sprite.width - 1 do
      local c = src:getPixel(x, y)
      if app.pixelColor.rgbaA(c) > 0 then
        dst:drawPixel(x * 2, y * 2, c)
        dst:drawPixel(x * 2 + 1, y * 2, c)
        dst:drawPixel(x * 2, y * 2 + 1, c)
        dst:drawPixel(x * 2 + 1, y * 2 + 1, c)
      end
    end
  end
  big:saveCopyAs(root .. "/" .. outPng)
  big:close()
end

---------------------------------------------------------------------------
-- Canvas: a w x h grid of colours (nil = clear), drawn into a sprite later.
---------------------------------------------------------------------------
local function canvas(w, h)
  local c = { w = w, h = h, px = {} }
  function c:get(x, y)
    if x < 0 or y < 0 or x >= self.w or y >= self.h then return nil end
    return self.px[y * self.w + x]
  end
  function c:set(x, y, col)
    if x < 0 or y < 0 or x >= self.w or y >= self.h then return end
    self.px[y * self.w + x] = col
  end
  -- Paints every pixel whose signed depth (px inside the shape, >0) is positive,
  -- choosing the colour from bands = { {depth, colour}, ... } (first band whose depth exceeds d).
  function c:shape(depthFn, bands)
    for y = 0, self.h - 1 do
      for x = 0, self.w - 1 do
        local d = depthFn(x + 0.5, y + 0.5)
        if d and d > 0 then
          local col = bands[#bands][2]
          for _, b in ipairs(bands) do
            if d <= b[1] then col = b[2] break end
          end
          self:set(x, y, col)
        end
      end
    end
  end
  -- Adds an outline of colour col round everything painted (4-neighbour).
  function c:outline(col)
    local add = {}
    for y = 0, self.h - 1 do
      for x = 0, self.w - 1 do
        if not self:get(x, y) and (self:get(x - 1, y) or self:get(x + 1, y) or self:get(x, y - 1) or self:get(x, y + 1)) then
          add[#add + 1] = { x, y }
        end
      end
    end
    for _, p in ipairs(add) do self:set(p[1], p[2], col) end
  end
  function c:blit(img, ox, oy)
    for y = 0, self.h - 1 do
      for x = 0, self.w - 1 do
        local col = self:get(x, y)
        if col then img:drawPixel(ox + x, oy + y, col) end
      end
    end
  end
  return c
end

-- Signed depth helpers.
local function disc(cx, cy, r)
  return function(x, y) return r - math.sqrt((x - cx) ^ 2 + (y - cy) ^ 2) end
end
-- Tapered capsule from (ax, ay) radius ra to (bx, by) radius rb.
local function cone(ax, ay, ra, bx, by, rb)
  return function(x, y)
    local dx, dy = bx - ax, by - ay
    local t = ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)
    t = math.max(0, math.min(1, t))
    local r = ra + (rb - ra) * t
    return r - math.sqrt((x - ax - dx * t) ^ 2 + (y - ay - dy * t) ^ 2)
  end
end
local function union(...)
  local fs = { ... }
  return function(x, y)
    local best = -1e9
    for _, f in ipairs(fs) do best = math.max(best, f(x, y)) end
    return best
  end
end

-- Energy bands, edge inward. The outline is added separately.
local KI = { { 1, M }, { 2, H }, { 3, R }, { 99, W } }
local KI_THIN = { { 1, H }, { 2, R }, { 99, W } }

-- Saves a sheet of frames stacked vertically; draw(i, canvas) paints frame i.
local function sheet(name, folder, w, h, frames, draw)
  local sprite = Sprite(w, h * frames, ColorMode.RGB)
  local img = sprite.cels[1].image
  for i = 0, frames - 1 do
    local c = canvas(w, h)
    draw(i, c)
    c:blit(img, 0, i * h)
  end
  export(sprite, name, "Zenkai/Assets/" .. folder .. "/" .. name .. ".png")
  sprite:close()
end

---------------------------------------------------------------------------
-- Skill slot frames, 24x24. 4 px border round a 16x16 icon well, styled like the ki bar.
---------------------------------------------------------------------------
local function slot(name, ring1, ring2, ring2lo, ring3, well, wellTop)
  local sprite = Sprite(24, 24, ColorMode.RGB)
  local img = sprite.cels[1].image
  for y = 0, 23 do
    for x = 0, 23 do
      local ring = math.min(x, y, 23 - x, 23 - y)
      local lit = (x + y) < 23 -- top-left half catches the light
      local c
      if ring == 0 then c = K
      elseif ring == 1 then c = lit and ring1 or ring2lo
      elseif ring == 2 then c = lit and ring2 or ring2lo
      elseif ring == 3 then c = ring3
      else c = (y == 4) and wellTop or well end
      img:drawPixel(x, y, c)
    end
  end
  -- Rounded outer corners.
  for _, p in ipairs({ { 0, 0 }, { 23, 0 }, { 0, 23 }, { 23, 23 } }) do
    img:drawPixel(p[1], p[2], rgba(0, 0))
  end
  img:drawPixel(1, 1, K) img:drawPixel(22, 1, K) img:drawPixel(1, 22, K) img:drawPixel(22, 22, K)
  export(sprite, name, "Zenkai/Assets/UI/" .. name .. ".png")
  sprite:close()
end
slot("SkillSlot", G_HI, G, G_LO, K, WELL, WELL_LO)
slot("SkillSlotActive", R, H, M, O, rgba(0x24305e), rgba(0x182044))

---------------------------------------------------------------------------
-- Skill icons, 16x16, drawn into the slot's well.
---------------------------------------------------------------------------
local function icon(name, draw)
  local sprite = Sprite(16, 16, ColorMode.RGB)
  local c = canvas(16, 16)
  draw(c)
  c:blit(sprite.cels[1].image, 0, 0)
  export(sprite, name, "Zenkai/Assets/Skills/" .. name .. ".png")
  sprite:close()
end

-- Ki Blast: a ball flying up and right with a tapering tail.
icon("KiBlast", function(c)
  c:shape(union(disc(10, 6, 4.2), cone(2.5, 13.5, 0.8, 10, 6, 3.4)), KI)
  c:set(11, 4, W) c:set(9, 5, W)
  c:outline(O)
end)

-- Charged Beam: a thick beam crossing the slot with a round head on the right.
icon("ChargedBeam", function(c)
  c:shape(union(cone(0, 8, 2.6, 10, 8, 3.2), disc(11, 8, 4.3)), KI)
  c:outline(O)
  -- Sparks off the head.
  c:set(15, 2, R) c:set(15, 13, R) c:set(14, 1, H)
end)

-- Rush: a clenched fist seen from the front: four curled fingers, the thumb across them,
-- and a ki-blue wristband.
icon("Rush", function(c)
  local k = rgba(0x3a1e1a)
  local S, s, d, l = rgba(0xf2c39a), rgba(0xd08e66), rgba(0xa8603e), rgba(0xffe2c8)
  local map = {
    "................",
    "................",
    "...kk.kk.kk.kk..",
    "..kllkllklSkSSk.",
    "..klSkSSkSSkSSk.",
    "..kSSkSSkSSkSsk.",
    "..kSSkSSkSSkSsk.",
    "..ksSkSSkSskssk.",
    "..kkkkkkkkkkssk.",
    ".kllSSSSSSSkssk.",
    ".kSSSSSSSSsksdk.",
    ".kssssssssskddk.",
    "..kkkkkkkkkdddk.",
    "...ORHHHHHHHHO..",
    "...OMMMMMMMMMO..",
    "....OOOOOOOOO...",
  }
  local pal = { k = k, S = S, s = s, d = d, l = l, H = H, R = R, M = M, I = I, O = O }
  for y = 1, 16 do
    for x = 1, 16 do
      local ch = map[y]:sub(x, x)
      if pal[ch] then c:set(x - 1, y - 1, pal[ch]) end
    end
  end
end)

-- Vanish: a bright blink star with fading streaks behind it.
icon("Vanish", function(c)
  local star = function(x, y)
    local dx, dy = math.abs(x - 11), math.abs(y - 6)
    local arm = (math.min(dx, dy) < 0.9 and math.max(dx, dy) < 5) and 1 or 0
    local core = 2.6 - (dx + dy)
    return math.max(core, arm > 0 and 0.5 or -1)
  end
  c:shape(star, { { 0.6, H }, { 1.6, R }, { 99, W } })
  c:shape(cone(1, 10, 0.7, 7, 10, 1.1), KI_THIN)
  c:shape(cone(3, 13, 0.7, 9, 13, 1.1), KI_THIN)
  c:outline(O)
end)

---------------------------------------------------------------------------
-- Ki Blast bolt, 14x8, 3 frames. Head on the right, tail flickering behind.
---------------------------------------------------------------------------
sheet("KiBlastProjectile", "Projectiles", 14, 8, 3, function(i, c)
  local tail = ({ 1.2, 0.6, 1.6 })[i + 1]
  c:shape(union(disc(9.5, 4, 2.9), cone(tail, 4, 0.6, 9.5, 4, 2.3)), { { 1, H }, { 2, R }, { 99, W } })
  c:outline(O)
end)

-- Muzzle flash, 12x12, 3 frames: a star that opens and breaks up.
sheet("KiFlash", "Projectiles", 12, 12, 3, function(i, c)
  local r = ({ 2.4, 4.4, 4.8 })[i + 1]
  c:shape(function(x, y)
    local dx, dy = math.abs(x - 6), math.abs(y - 6)
    local arm = math.min(dx, dy) < 0.9 and math.max(dx, dy) < r and 0.5 or -1
    local core = (i == 2) and -1 or (r * 0.6 - (dx + dy))
    if i == 2 and math.max(dx, dy) < 2 then return -1 end
    return math.max(core, arm)
  end, { { 0.6, H }, { 1.4, R }, { 99, W } })
  c:outline(O)
end)

-- Impact burst, 16x16, 4 frames: flash, swell, ring, scattered bits.
sheet("KiBurst", "Projectiles", 16, 16, 4, function(i, c)
  if i == 0 then
    c:shape(disc(8, 8, 3.6), KI_THIN)
  elseif i == 1 then
    c:shape(disc(8, 8, 5.6), KI)
  elseif i == 2 then
    c:shape(function(x, y) local d = math.sqrt((x - 8) ^ 2 + (y - 8) ^ 2) return 1.3 - math.abs(d - 5.6) end, { { 0.7, H }, { 99, R } })
  else
    for a = 0, 5 do
      local ang = a * math.pi / 3 + 0.4
      c:shape(disc(8 + math.cos(ang) * 6, 8 + math.sin(ang) * 6, 1.1), { { 99, H } })
    end
  end
  c:outline(O)
end)

-- Melee hit spark, 12x12, 3 frames, warm white and gold so hits read apart from ki.
sheet("StrikeSpark", "Projectiles", 12, 12, 3, function(i, c)
  local Y, Gd, Ow = rgba(0xfff2a0), rgba(0xffc84a), rgba(0x6a3010)
  local r = ({ 4.2, 5.4, 5.4 })[i + 1]
  c:shape(function(x, y)
    local dx, dy = x - 6, y - 6
    local adx, ady = math.abs(dx), math.abs(dy)
    local straight = math.min(adx, ady) < 0.9 and math.max(adx, ady) < r
    local diag = i > 0 and math.abs(adx - ady) < 0.9 and math.max(adx, ady) < r * 0.6
    if i == 2 and math.max(adx, ady) < 3 then return -1 end
    local core = (i < 2) and (2.2 - (adx + ady)) or -1
    return math.max(core, (straight or diag) and 0.5 or -1)
  end, { { 0.6, Gd }, { 1.4, Y }, { 99, W } })
  c:outline(Ow)
end)

-- Melee swoosh, 20x20, 3 frames: a crescent opening toward the strike direction (right).
sheet("RushStrike", "Projectiles", 20, 20, 3, function(i, c)
  local from = ({ -75, -70, 10 })[i + 1]
  local to = ({ 0, 70, 75 })[i + 1]
  local thick = ({ 1.6, 2.4, 1.2 })[i + 1]
  c:shape(function(x, y)
    local dx, dy = x - 7, y - 10
    local d = math.sqrt(dx * dx + dy * dy)
    local ang = math.deg(math.atan(dy, dx))
    if ang < from or ang > to then return -1 end
    -- Thicker in the middle of the swing, thin at the ends.
    local mid = (from + to) / 2
    local t = 1 - math.abs(ang - mid) / ((to - from) / 2)
    return thick * (0.45 + 0.55 * t) - math.abs(d - 8.5)
  end, { { 0.7, R }, { 99, W } })
  c:outline(H)
end)

---------------------------------------------------------------------------
-- Charged beam pieces. Body half-thickness 7 (28 screen px across).
---------------------------------------------------------------------------
-- Charge orb: three sizes side by side, two flicker frames stacked, 16x16 cells.
do
  local sprite = Sprite(48, 32, ColorMode.RGB)
  local img = sprite.cels[1].image
  for size = 0, 2 do
    for f = 0, 1 do
      local c = canvas(16, 16)
      local r = ({ 3.4, 5.4, 7.0 })[size + 1] - f * 0.5
      c:shape(disc(8, 8, r), KI)
      if f == 1 then c:set(6, 6, W) end
      c:outline(O)
      c:blit(img, size * 16, f * 16)
    end
  end
  export(sprite, "BeamOrb", "Zenkai/Assets/Projectiles/BeamOrb.png")
  sprite:close()
end

-- Start cap at the hands: a bright orb, 18x18, 3 frames.
sheet("BeamStart", "Projectiles", 18, 18, 3, function(i, c)
  local r = ({ 8.0, 7.5, 8.0 })[i + 1]
  c:shape(disc(9, 9, r), KI)
  if i == 1 then c:set(7, 7, W) c:set(11, 11, R) end
  c:outline(O)
end)

-- Body tile, 8x18, 3 frames. The ripple's period equals the tile width, so tiles join seamlessly.
sheet("BeamBody", "Projectiles", 8, 18, 3, function(i, c)
  for x = 0, 7 do
    local half = 6.5 + 0.8 * math.sin(2 * math.pi * (x + i * 8 / 3) / 8)
    for y = 0, 17 do
      local d = half - math.abs(y + 0.5 - 9)
      if d > 0 then
        local col = (d <= 1) and O or (d <= 2) and M or (d <= 3) and H or (d <= 4.2) and R or W
        c:set(x, y, col)
      end
    end
  end
end)

-- End cap, 22x22, 3 frames: the beam's round head with flares pushing forward.
sheet("BeamEnd", "Projectiles", 22, 22, 3, function(i, c)
  local flare = ({ 0, 1, 2 })[i + 1]
  local shape = union(
    disc(10, 11, 8.4 - (i == 1 and 0.4 or 0)),
    cone(12, 11 - 5, 1.6, 19 + flare % 2, 3 + flare, 0.6),
    cone(12, 11 + 5, 1.6, 19 + (flare + 1) % 2, 19 - flare, 0.6),
    cone(14, 11, 2.2, 21, 11, 0.8)
  )
  c:shape(shape, KI)
  c:outline(O)
end)

print("wrote skill slots, icons and projectiles")
