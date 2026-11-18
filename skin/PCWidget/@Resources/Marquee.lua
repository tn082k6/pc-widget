-- Laufschrift fuer einen Text-Meter in einem Clip-Container (z. B. Song in der Spiel-Leiste)
-- Optionen am Script-Measure: Meter, Measure, Measure2, BoxW, BaseX, Speed
-- Laeuft bei jedem Skin-Update (UpdateDivider=1), bei Update=33 also ca. 30x pro Sekunde.

function Initialize()
  meterName = SELF:GetOption('Meter', 'MeterSong')
  measName  = SELF:GetOption('Measure', '')
  meas2Name = SELF:GetOption('Measure2', '')
  boxW  = tonumber(SELF:GetOption('BoxW', '150')) or 150
  baseX = tonumber(SELF:GetOption('BaseX', '78')) or 78
  speed = tonumber(SELF:GetOption('Speed', '0.7')) or 0.7
  lastKey = nil; off = 0; phase = 0; wait = 75; lastX = nil
end

local function text(name)
  if name == '' then return '' end
  local m = SKIN:GetMeasure(name)
  return m and m:GetStringValue() or ''
end

local function setX(x)
  x = math.floor(x + 0.5)
  if x ~= lastX then
    lastX = x
    SKIN:Bang('!SetOption', meterName, 'X', tostring(x))
    SKIN:Bang('!UpdateMeter', meterName)
  end
end

function Update()
  local key = text(measName) .. '|' .. text(meas2Name)
  if key ~= lastKey then          -- neuer Song: von vorn
    lastKey = key; off = 0; phase = 0; wait = 75
    setX(baseX)
    return 0
  end
  local meter = SKIN:GetMeter(meterName)
  local over = (meter and meter:GetW() or 0) - boxW
  if over <= 2 then off = 0; phase = 0; wait = 75; setX(baseX); return 0 end
  if phase == 0 then             -- am Anfang kurz stehen bleiben
    wait = wait - 1; if wait <= 0 then phase = 1 end
  elseif phase == 1 then         -- durchlaufen
    off = off + speed
    if off >= over then off = over; phase = 2; wait = 60 end
  else                           -- am Ende kurz warten, dann von vorn
    wait = wait - 1; if wait <= 0 then off = 0; phase = 0; wait = 75 end
  end
  setX(baseX - off)
  return 0
end
