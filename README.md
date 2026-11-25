# ALYSSA VR 6DOF

Add-on plugins for [ALYSSA VR](https://github.com/King-EJ/ALYSSA-VR) (the BepInEx VR mod for the Steam game *ALYSSA*).
They make the hands matter and the experience smoother:

- **Aim with your right hand.** Doors, notes and items are chosen by what the right controller points at, not by where you look (both the game's own interaction ray and Game Creator 2's interaction system).
- **The match in your hand.** The lit match (and the lighter) is held in your left controller, scaled to a sensible VR size, with the flame sitting on the match head. You can hold it up to walls and light up corners.
- **Room-scale walking.** Walking in your room moves the character, walls stop it. (ALYSSA VR alone leaves the body behind, so after stepping around or snap turning the invisible body got stuck on walls you could not see.)
- **Ladders work.** Ladders and climbable fences read W / S with Unity's old input, which ALYSSA VR's virtual keyboard doesn't reach, so you could get on a ladder but not move. Now the left stick climbs up / down.
- **No head through walls.** If you lean or step into a wall or door, the view stops at the wall instead of clipping through it.
- **Clean HUD.** Hides the crosshair, the "[F] light / put out the match" hint and the match counter (configurable list).
- **Film grain in VR.** `AlyssaVR_ScreenFX` copies the game's camera filters (TV noise / grain, posterize, LUT) onto the VR eyes, which ALYSSA VR otherwise loses.
- **Smoothness.** Turns desktop VSync off in VR and logs frame statistics. See *Direct3D 11* below - it is the biggest fix.

Tested with ALYSSA VR 0.1.9, BepInEx 5.4.23, a Quest headset over Meta Horizon Link, RTX 4070.

## Install

1. Install **BepInEx 5 (x64)** and **ALYSSA VR 0.1.9** as described in the ALYSSA VR readme, and check that VR works.
2. Download the zip from [Releases](../../releases) and copy its `BepInEx` folder into the game folder (merge).
3. In `BepInEx\config\alyssa.vr.cfg` set
   ```
   PointToInteract = false
   HeadAimFix = false
   ```
   (this plugin does the aiming; ALYSSA VR's own version would fight it).
4. **Steam -> ALYSSA -> Properties -> Launch options: `-force-d3d11`**. On Direct3D 12 the game ran at 18-36 fps in VR with CPU and GPU mostly idle (a sync stall) and the view juddered; on Direct3D 11 the same scenes run at 45-65 fps and feel smooth.
5. Use the headset's own OpenXR runtime if you can (for Quest: Meta Horizon Link -> Settings -> General -> OpenXR Runtime). SteamVR on top of Link adds another layer.

### Troubleshooting

- **VR does not start at all, `preloader_*.log` files appear in the game folder with `MethodAccessException ... PlatformHelper.set_Current`:** the *ReiPatcher* version of XUnity AutoTranslator put old `0Harmony.dll` / `MonoMod*.dll` / `Mono.Cecil.dll` into `ALYSSA_Data\Managed`, which breaks BepInEx. Remove those files (and restore `UnityEngine.CoreModule.dll` from the `.bak` ReiPatcher made), then use the **BepInEx** version of XUnity AutoTranslator instead.
- Everything is logged to `BepInEx\LogOutput.log` (lines starting with `ALYSSA VR Hands`).

## Controls (in addition to ALYSSA VR's)

| | |
|---|---|
| Point the **right** controller + **A** | interact with what you point at (the game shows its usual prompt) |
| **B** (= F) | light / put out a match - it appears in your **left** hand |
| Lean / walk into a wall | the view stops at the wall |
| Walk in your room | the character walks with you |
| On a ladder / fence (**A** to get on) | left stick up / down climbs |

## Settings

`BepInEx\config\alyssa.vr.hands.cfg` - re-read automatically while the game runs (no restart needed).

| Section | Key | Default | |
|---|---|---|---|
| General | `AimHand` | `Right` | controller used for aiming |
| Hand | `ItemHand` | `Left` | controller holding the match / lighter |
| Hand | `ItemScale` | `3` | size of the held match (1 = the game's size, tiny in VR) |
| Hand | `AttachRotation` / `AttachOffset` | `71,278,271` / `0,0,0` | how the match sits in the hand |
| Flame | `PutFlameOnMatchHead` / `FlameOffset` | `true` / `0,0,0` | flame placement fix / extra offset (m) |
| UI | `Hide` | crosshair, match hint, match counter | HUD pieces to hide: path endings or `*name*` patterns |
| Head | `StopAtWalls` / `HeadRadius` | `true` / `0.12` | head collision and head size (m) |
| RoomScale | `BodyFollowsHead` / `Deadzone` / `MaxSpeed` | `true` / `0` / `3` | the body follows the headset / how far the head may lean away first (m; try 0.1 if leaning over a table drags you) / catch-up speed (m/s) |
| Smoothness | `DisableVSync` / `LogFrameStats` | `true` / `true` | VSync off in VR / fps line in the log every 15 s |

`BepInEx\config\alyssa.vr.screenfx.cfg`: `Enabled`, `NoiseStrength` (grain strength, 1 = like the game).

## Known limitations

- A torch light on the left hand is ALYSSA VR's own flashlight: holding the left grip alone for 1.3 s switches it on. To make that impossible set `[Flashlight] On = false` and `HoldSeconds = 1000` in `alyssa.vr.cfg`.

- The game's head bobbing is a Game Creator setting in most levels and is not turned off by this plugin.
- Physics pick-up support (`[Pickup]`) is included, but ALYSSA has nothing to pick up that way.
- This depends on how ALYSSA VR 0.1.9 names its objects (`VR_Root/VR_Holder/VR_RightHand/Aim` ...). A different ALYSSA VR version may need changes.

## Build from source

```
build.cmd "D:\SteamLibrary\steamapps\common\ALYSSA"
```
Uses the .NET Framework 4 C# compiler that ships with Windows and references the game's and BepInEx's assemblies; output goes to `dist\BepInEx\plugins`. `tools\dumpil.ps1` prints fields / methods / IL of game classes (via Mono.Cecil from BepInEx) to find what to patch.

## Credits

- [ALYSSA VR](https://github.com/King-EJ/ALYSSA-VR) by King-EJ - the VR mod this builds on (not included here).
- [BepInEx](https://github.com/BepInEx/BepInEx), [HarmonyX](https://github.com/BepInEx/HarmonyX).
- Made with Claude Code.

---

# ALYSSA VR 6DOF (по-русски)

Дополнительные плагины к моду [ALYSSA VR](https://github.com/King-EJ/ALYSSA-VR) для игры *ALYSSA* (Steam):

- **Прицел правой рукой.** Двери, записки и предметы выбираются тем, на что указывает правый контроллер, а не взглядом.
- **Спичка в руке.** Зажжённая спичка (и зажигалка) в левом контроллере, увеличена для VR, пламя на головке спички. Можно подносить к стенам и светить в углы.
- **Ходьба по комнате.** Если физически ходить по комнате, персонаж идёт вместе с вами, стены его останавливают. (Без этого тело оставалось на месте, и после шагов или поворота стиком невидимое тело упиралось в стены, которых не видно.)
- **Лестницы работают.** Лестницы и сетки читают W / S через старый ввод Unity, до которого виртуальная клавиатура ALYSSA VR не доходит, поэтому залезть было можно, а двигаться нет. Теперь левый стик — вверх / вниз.
- **Голова не проходит сквозь стены.** Если наклониться или шагнуть в стену, вид останавливается у стены.
- **Чистый интерфейс.** Скрыты прицел, подсказка «[F] – зажечь/погасить спичку» и счётчик спичек (список настраивается).
- **Зерно в VR.** `AlyssaVR_ScreenFX` возвращает фильтры камеры игры (шум, постеризация, LUT), которые ALYSSA VR теряет.
- **Плавность.** Отключает VSync в VR, пишет статистику кадров в лог. Главное — Direct3D 11, см. ниже.

## Установка

1. Установите **BepInEx 5 (x64)** и **ALYSSA VR 0.1.9** по инструкции ALYSSA VR и убедитесь, что VR работает.
2. Скачайте архив из [Releases](../../releases) и скопируйте его папку `BepInEx` в папку игры (с объединением).
3. В `BepInEx\config\alyssa.vr.cfg` поставьте `PointToInteract = false` и `HeadAimFix = false` (прицеливанием занимается этот плагин).
4. **Steam → ALYSSA → Свойства → Параметры запуска: `-force-d3d11`.** На Direct3D 12 в VR было 18–36 кадров/с при почти простаивающих процессоре и видеокарте, картинка дёргалась; на Direct3D 11 — 45–65 кадров/с и плавно.
5. По возможности используйте OpenXR-рантайм самого шлема (Quest: Meta Horizon Link → Настройки → Общие → OpenXR Runtime), а не SteamVR поверх Link.

**VR не запускается, в папке игры появляются `preloader_*.log` с `MethodAccessException ... PlatformHelper.set_Current`** — это XUnity AutoTranslator в варианте *ReiPatcher*: он кладёт старые `0Harmony.dll` / `MonoMod*.dll` / `Mono.Cecil.dll` в `ALYSSA_Data\Managed` и ломает BepInEx. Уберите их (и верните `UnityEngine.CoreModule.dll` из `.bak`), а переводчик поставьте в варианте **для BepInEx**.

Управление: правый контроллер + **A** — взаимодействие с тем, на что указываете; **B** — зажечь/погасить спичку (она в левой руке). Настройки — `BepInEx\config\alyssa.vr.hands.cfg` (таблица выше), меняются прямо во время игры.
