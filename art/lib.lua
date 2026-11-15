-- Shared helpers for the art scripts: colours, the 1x canvas with signed-distance shapes, and
-- the 2x export. Load with: local lib = dofile(root .. "/art/lib.lua") after setting the global root.

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

-- One 16x16 image.
local function single(name, folder, w, h, draw)
  local sprite = Sprite(w, h, ColorMode.RGB)
  local c = canvas(w, h)
  draw(c)
  c:blit(sprite.cels[1].image, 0, 0)
  export(sprite, name, "Zenkai/Assets/" .. folder .. "/" .. name .. ".png")
  sprite:close()
end

return { rgba = rgba, export = export, canvas = canvas, disc = disc, cone = cone, union = union, sheet = sheet, single = single }
