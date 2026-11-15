-- PC Widget Effekte: weicher Farbwechsel + Laufschrift fuer lange Songtitel
-- Laeuft ca. 30x pro Sekunde (UpdateDivider=1 bei Update=33)

function Initialize()
  cur = { 235, 245, 255 }
  tgt = { 235, 245, 255 }
  boxW = tonumber(SELF:GetOption('BoxW', '112')) or 112
  baseX = tonumber(SELF:GetOption('BaseX', '76')) or 76
  speed = 0.7          -- Pixel pro Bild
  lastTitle = nil
  off = 0
  phase = 0            -- 0 = Pause am Anfang, 1 = laeuft, 2 = Pause am Ende
  wait = 75            -- ca. 2,5 s Pause am Anfang
  lastX = nil
  first = true
end

local function parse(s)
  local r, g, b = string.match(s or '', '(%d+)%s*,%s*(%d+)%s*,%s*(%d+)')
  if r then return { tonumber(r), tonumber(g), tonumber(b) } end
  return nil
end

local function colorStep()
  local m = SKIN:GetMeasure('MeasureMusicColor')
  local t = m and parse(m:GetStringValue())
  if t then tgt = t end
  local changed = first
  for i = 1, 3 do
    local d = tgt[i] - cur[i]
    if math.abs(d) > 0.6 then
      cur[i] = cur[i] + d * 0.08
      changed = true
    elseif cur[i] ~= tgt[i] then
      cur[i] = tgt[i]
      changed = true
    end
  end
  if changed then
    first = false
    SKIN:Bang('!SetVariable', 'Col', string.format('%d,%d,%d', math.floor(cur[1] + 0.5), math.floor(cur[2] + 0.5), math.floor(cur[3] + 0.5)))
    SKIN:Bang('!UpdateMeterGroup', 'ColorFx')
  end
end

local function setX(x)
  x = math.floor(x + 0.5)
  if x ~= lastX then
    lastX = x
    SKIN:Bang('!SetOption', 'MeterMusicTitle', 'X', tostring(x))
    SKIN:Bang('!UpdateMeter', 'MeterMusicTitle')
  end
end

local function marqueeStep()
  local tm = SKIN:GetMeasure('MeasureMusicTitle')
  local title = tm and tm:GetStringValue() or ''
  if title ~= lastTitle then
    lastTitle = title
    off = 0; phase = 0; wait = 75
    setX(baseX)
    return
  end
  local meter = SKIN:GetMeter('MeterMusicTitle')
  local w = meter and meter:GetW() or 0
  local over = w - boxW
  if over <= 2 then
    off = 0; phase = 0; wait = 75
    setX(baseX)
    return
  end
  if phase == 0 then
    wait = wait - 1
    if wait <= 0 then phase = 1 end
  elseif phase == 1 then
    off = off + speed
    if off >= over then off = over; phase = 2; wait = 60 end
  else
    wait = wait - 1
    if wait <= 0 then off = 0; phase = 0; wait = 75 end
  end
  setX(baseX - off)
end

function Update()
  colorStep()
  marqueeStep()
  return 0
end
