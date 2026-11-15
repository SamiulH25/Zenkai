-- Builds the mod icons: icon.png (80x80) and icon_small.png (30x30), at 1x then exported 2x.
-- A blue ki orb with a gold ring (the ki bar's emblem, larger) in front of a ki flame,
-- on a dark panel with a gold frame. Run through tools/art.sh.

local root = app.params["root"]
assert(root, "pass --script-param root=<repo path>")

local function rgba(hex, a)
  return app.pixelColor.rgba((hex >> 16) & 255, (hex >> 8) & 255, hex & 255, a or 255)
end

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
        for dy = 0, 1 do for dx = 0, 1 do dst:drawPixel(x * 2 + dx, y * 2 + dy, c) end end
      end
    end
  end
  big:saveCopyAs(root .. "/" .. outPng)
  big:close()
end

local OUT = rgba(0x15122b)
local GOLD_HI, GOLD, GOLD_LO = rgba(0xf8dc7a), rgba(0xd6a23a), rgba(0x8c5f1e)
local PANEL, PANEL_IN = rgba(0x1b1f45), rgba(0x232a5c)
local EDGE, RIM, HOT, MID, INNER = rgba(0x1a2f8f), rgba(0xd2f8ff), rgba(0x5ccfff), rgba(0x2f8ff0), rgba(0x2266d8)
local RING_IN = rgba(0x12307e)
local CORE_HI, CORE, CORE_LO = rgba(0x6fd2ff), rgba(0x2f9cf2), rgba(0x1a62cc)
local WHITE, GLINT = rgba(0xf2feff), rgba(0xa8f0ff)

-- Orb centred at (cx, cy) with outer radius r (outline, gold ring, dark ring, banded core, glint).
local function orb(img, cx, cy, r)
  for y = math.floor(cy - r - 1), math.ceil(cy + r + 1) do
    for x = math.floor(cx - r - 1), math.ceil(cx + r + 1) do
      local dx, dy = x + 0.5 - cx, y + 0.5 - cy
      local d = math.sqrt(dx * dx + dy * dy)
      local c
      if d > r then c = nil
      elseif d > r - 1.1 then c = OUT
      elseif d > r - 2.6 then
        local lit = dx + dy
        c = lit < -r * 0.5 and GOLD_HI or (lit > r * 0.6 and GOLD_LO or GOLD)
      elseif d > r - 3.6 then c = RING_IN
      else
        local shade = dy + dx * 0.5
        c = shade < -r * 0.28 and CORE_HI or (shade > r * 0.25 and CORE_LO or CORE)
      end
      if c then img:drawPixel(x, y, c) end
    end
  end
end

-- Four-point glint centred on a pixel
local function star(img, x, y, arm)
  img:drawPixel(x, y, WHITE)
  for i = 1, arm do
    local c = i == arm and GLINT or WHITE
    img:drawPixel(x + i, y, c) img:drawPixel(x - i, y, c)
    img:drawPixel(x, y + i, c) img:drawPixel(x, y - i, c)
  end
end

---------------------------------------------------------------------------
-- icon.png: 40x40 at 1x
---------------------------------------------------------------------------
do
  local S = 40
  local sprite = Sprite(S, S, ColorMode.RGB)
  local img = sprite.cels[1].image

  -- Panel with a gold frame and cut corners
  for y = 0, S - 1 do
    for x = 0, S - 1 do
      local e = math.min(x, y, S - 1 - x, S - 1 - y)
      local corner = math.min(x, S - 1 - x) + math.min(y, S - 1 - y)
      local c
      if corner < 2 then c = nil
      elseif e == 0 or corner == 2 then c = OUT
      elseif e == 1 then c = (x + y < S) and GOLD_HI or GOLD_LO
      elseif e == 2 then c = GOLD
      elseif e == 3 then c = OUT
      else
        local dx, dy = x + 0.5 - S / 2, y + 0.5 - S / 2
        c = (dx * dx + dy * dy < 13 * 13) and PANEL_IN or PANEL
      end
      if c then img:drawPixel(x, y, c) end
    end
  end

  -- Ki flame behind the orb, rising from the panel floor to near the top
  local cx, base, H = 20, 33, 28
  local TAU = 2 * math.pi
  for y = 5, 34 do
    local h = base - y - 0.5
    local t = h / H
    if t >= 0 and t <= 1 then
      local w = t < 0.25 and (13.5 - t * 6) or (t < 0.55 and (12 + (t - 0.25) * 1) or (12.3 * (1 - ((t - 0.55) / 0.45) ^ 1.6)))
      for x = 6, 33 do
        local px = x + 0.5 - cx
        local ph = px < 0 and math.pi or 0
        local wave = 2.0 * math.sin(TAU * h / 9 + ph) * (0.25 + 0.75 * t)
        local d = w + wave - math.abs(px)
        local c
        if d < 0 then c = nil
        elseif d < 1 then c = EDGE
        elseif d < 2 then c = RIM
        elseif d < 3 then c = HOT
        elseif d < 5 then c = MID
        else c = INNER end
        if c then img:drawPixel(x, y, c) end
      end
    end
  end

  orb(img, 20, 20.5, 10)
  star(img, 21, 21, 2)
  img:drawPixel(16, 17, WHITE) img:drawPixel(17, 17, WHITE) img:drawPixel(16, 18, WHITE)

  export(sprite, "Icon", "Zenkai/icon.png")
  sprite:close()
end

---------------------------------------------------------------------------
-- icon_small.png: 15x15 at 1x, orb only
---------------------------------------------------------------------------
do
  local sprite = Sprite(15, 15, ColorMode.RGB)
  local img = sprite.cels[1].image
  orb(img, 7.5, 7.5, 7.5)
  star(img, 8, 8, 1)
  img:drawPixel(5, 5, WHITE)
  export(sprite, "IconSmall", "Zenkai/icon_small.png")
  sprite:close()
end

print("wrote icon, icon_small")
