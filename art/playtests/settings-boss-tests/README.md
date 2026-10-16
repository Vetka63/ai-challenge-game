# Settings and boss tests — verification, 2026-10-16

Build: AsciiArena/Builds/SettingsBossTests/AsciiArena.exe.

- editmode-tests.json: all 72 tests passed (65 existing + 7 preferences/test cases).
- preferences-tests-final.json: 7/7 passed after strengthening the boss test
  to kill a boss through an actual projectile and assert Victory before checking
  that no campaign victory or unlock was awarded.
- final-player/settings-check.json: Direct3D11, started at 1920x1080 windowed,
  switched via UI to 2560x1440 borderless, then via F11 to 1280x720 windowed.
  errors=[], exactResume=true, applied volume .36, CRT off and window mode saved.
- restart/settings-check.json: separate process WITHOUT any screen flags.
  Reopened at 1280x720 windowed, audio .36, CRT off, boss-test phase 2 restored.
  Continue exactly matched the saved expedition snapshot; errors=[].
- UI checks: GraphicRaycaster + pointer down/up/click, slider pointer coordinates
  with its press camera; real InputSystem keyboard events for arrows, M/F2/F11/Esc.
  Checked volume 0/.35/.36, mute/restoration, CRT shader bypass, fullscreen,
  keyboard and pointer volume adjustment, no paused combat advancement,
  visible button bounds and text overflow. PNGs are actual GPU captures.
- Boss checks: all three start without campaign encounters. Warden, Archivist
  phase 2 lanes, Bellkeeper phase 2 rings visibly execute in the Player. Retry
  retains phase; direct return to expedition preserves exact state. No campaign
  encounter/victory is recorded. Ready-made training build uses normal damage.
- Audio validation checks the actual master gain and mute state. Short preview
  cues use ArenaAudio's original sound; no subjective listening review or
  audio waveform/mix-quality measurement is claimed.
- player/ is the initial diagnostic pass. Slider assertions failed because the
  synthetic PointerEventData lacked pointerPressRaycast (and thus press camera)
  for the camera-space Canvas. Fixed in the QA helper; production slider code
  unchanged. final-player/ and restart/ are the authoritative passing runs.
- build.json: final incremental build succeeded, 0 errors, 1 existing Pipeline
  warning. build-initial* preserve the full build's existing Extenject test
  resource warnings (69 total). Pre-existing ComputeBuffer disposal warning
  at Player shutdown remains; this was not a memory-leak repair or balance run.
- Own test profile is final-save/, isolated with --act-save-dir. No player saves
  were deleted. All task-owned Editor/Player processes were closed afterward.

Run ArenaSettingsCapture only via --settings-capture <output> and an isolated
--act-save-dir <directory>; --settings-resume validates the second launch.
Normal game launches do not activate capture helpers.
