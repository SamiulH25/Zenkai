-- Builds the phase 1 ki art: HUD bar frame, bar fill and the spark dust.
-- Run: aseprite -b --script-param root=<repo> --script art/ki_core.lua   (tools/art.sh does this)
-- Art is authored at 1x and exported at 2x, the way Terraria's own sprites are.

local root = app.params["root"]
assert(root, "pass --script-param root=<repo path>")

local function rgba(hex, a)
  return app.pixelColor.rgba((hex >> 16) & 255, (hex >> 8) & 255, hex & 255, a or 255)
end

-- Saves the 1x editable source and a 2x nearest-neighbour PNG.
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
-- Ki bar: 96x16 at 1x. Orb emblem on the left, fill window x 18..89, y 5..10.
-- Sized so that at 2x it stands as tall as the vanilla life hearts.
---------------------------------------------------------------------------
local W, H = 96, 16
local OUT = rgba(0x15122b)
local GOLD_HI, GOLD, GOLD_LO = rgba(0xf8dc7a), rgba(0xd6a23a), rgba(0x8c5f1e)
local WELL, WELL_LO = rgba(0x1d2447), rgba(0x10142e)

do
  local sprite = Sprite(W, H, ColorMode.RGB)
  local img = sprite.cels[1].image

  -- Bar body, rows 1..14: outline, two gold rows, a dark inner line, the well (5..10),
  -- dark inner line, two gold rows, outline. As heavy as the minimap's gold frame.
  for x = 12, 93 do
    img:drawPixel(x, 1, OUT)
    img:drawPixel(x, 2, GOLD_HI)
    img:drawPixel(x, 3, GOLD)
    img:drawPixel(x, 4, OUT)
    for y = 5, 10 do img:drawPixel(x, y, y == 5 and WELL_LO or WELL) end
    img:drawPixel(x, 11, OUT)
    img:drawPixel(x, 12, GOLD)
    img:drawPixel(x, 13, GOLD_LO)
    img:drawPixel(x, 14, OUT)
  end
  -- Right cap: inner line, three gold columns lit from the top left, outline with rounded corners
  for y = 2, 13 do
    img:drawPixel(90, y, (y >= 4 and y <= 11) and OUT or (y < 4 and GOLD or GOLD_LO))
    img:drawPixel(91, y, y >= 12 and GOLD_LO or GOLD_HI)
    img:drawPixel(92, y, y <= 2 and GOLD_HI or (y >= 12 and GOLD_LO or GOLD))
    img:drawPixel(93, y, y <= 2 and GOLD or GOLD_LO)
    img:drawPixel(94, y, OUT)
  end
  img:drawPixel(93, 1, 0) img:drawPixel(93, 14, 0)
  img:drawPixel(93, 2, OUT) img:drawPixel(93, 13, OUT)
  -- Left collar where the bar meets the orb
  for y = 4, 11 do
    img:drawPixel(16, y, GOLD)
    img:drawPixel(17, y, OUT)
  end
  -- Quarter notches: small dents in the gold
  for _, x in ipairs({ 36, 54, 72 }) do
    img:drawPixel(x, 2, GOLD_LO)
    img:drawPixel(x, 3, GOLD_LO)
    img:drawPixel(x, 12, GOLD_LO)
    img:drawPixel(x, 13, OUT)
  end

  -- Orb emblem, 16 px across: dark outline, gold ring, blue ki core with a spark
  local RING_IN = rgba(0x12307e)
  local CORE_HI, CORE, CORE_LO = rgba(0x6fd2ff), rgba(0x2f9cf2), rgba(0x1a62cc)
  for y = 0, 15 do
    for x = 0, 15 do
      local r = math.sqrt((x - 7.5) ^ 2 + (y - 7.5) ^ 2)
      local c
      if r > 8.0 then c = nil
      elseif r > 6.9 then c = OUT
      elseif r > 5.5 then
        c = (x + y < 12) and GOLD_HI or ((x + y > 19) and GOLD_LO or GOLD)
      elseif r > 4.6 then c = RING_IN
      else
        local shade = (y - 7.5) + (x - 7.5) * 0.5
        c = shade < -2.2 and CORE_HI or (shade > 1.8 and CORE_LO or CORE)
      end
      if c then img:drawPixel(x, y, c) end
    end
  end
  local WHITE, GLINT = rgba(0xf2feff), rgba(0xa8f0ff)
  img:drawPixel(8, 8, WHITE)
  img:drawPixel(8, 7, WHITE) img:drawPixel(8, 9, WHITE) img:drawPixel(7, 8, WHITE) img:drawPixel(9, 8, WHITE)
  img:drawPixel(8, 6, GLINT) img:drawPixel(8, 10, GLINT) img:drawPixel(6, 8, GLINT) img:drawPixel(10, 8, GLINT)
  img:drawPixel(5, 5, WHITE)

  export(sprite, "KiBarFrame", "Zenkai/Assets/UI/KiBarFrame.png")
  sprite:close()
end

do
  local sprite = Sprite(W, H, ColorMode.RGB)
  local img = sprite.cels[1].image
  local rows = { rgba(0xd4fbff), rgba(0x7fe3ff), rgba(0x45c2fb), rgba(0x2a9df2), rgba(0x1f7fe0), rgba(0x1a5fc4) }
  for x = 18, 89 do
    for i = 1, 6 do img:drawPixel(x, 4 + i, rows[i]) end
  end
  export(sprite, "KiBarFill", "Zenkai/Assets/UI/KiBarFill.png")
  sprite:close()
end

-- The ki aura is not drawn here: Zenkai/Common/Visuals/AuraRenderer.cs generates it
-- live from an AuraStyle, so forms can restyle it without new sheets.

---------------------------------------------------------------------------
-- Spark dust: three 2x2 frames stacked vertically, bright to dim, no fade.
---------------------------------------------------------------------------
do
  local sprite = Sprite(2, 6, ColorMode.RGB)
  local img = sprite.cels[1].image
  -- Cyan, not white: white specks read as frost next to the aura.
  local WHITE, CYAN, BLUE = rgba(0xb4f2ff), rgba(0x5ad8ff), rgba(0x2f8ff0)
  for y = 0, 1 do for x = 0, 1 do img:drawPixel(x, y, WHITE) end end
  for y = 2, 3 do for x = 0, 1 do img:drawPixel(x, y, (x + y) % 2 == 0 and WHITE or CYAN) end end
  img:drawPixel(0, 4, CYAN) img:drawPixel(1, 4, BLUE) img:drawPixel(0, 5, BLUE)
  export(sprite, "KiSpark", "Zenkai/Assets/Dusts/KiSpark.png")
  sprite:close()
end

print("wrote KiBarFrame, KiBarFill, KiSpark")
