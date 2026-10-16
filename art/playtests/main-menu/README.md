# Main menu verification — 2026-10-16

Build: AsciiArena/Builds/MainMenu/AsciiArena.exe.

- editmode-tests.json: 65/65 passed (60 existing + 5 menu state/save tests).
- final-720p/menu-check.json: 1280x720, Direct3D11, errors=[], exactResume=true,
  measured speed ratio 4.0023. Fresh isolated profile. Startup, all home
  sections, locked Continue/training, weapon-choice pause, real keyboard Esc,
  blocked combat keys in menus, x2/x4 selection, settings-to-pause-to-combat,
  confirmation cancel, training preservation, save, keyboard arrows/Enter, Quit.
- final-1080p/menu-check.json: 1920x1080, Direct3D11, errors=[], exactResume=true.
  Separate process using final-save from the 720p run; profile speed x4 restored,
  Continue snapshot equals saved state exactly, in-game settings accessible.
  speedRatio=0 in this report means not measured in the resume scenario.
- Both scenarios validate active button screen bounds, menu text overflow,
  missing font warnings and actual GraphicRaycaster hit targets. Pointer events
  are dispatched via EventSystem down/up/click. Keyboard uses InputSystem device
  events through production ArenaInput and UIInputModule. PNGs are GPU captures.
- 720p/ contains the first diagnostic iteration: functional flow passed but
  title/rule text boxes flagged overflow. Corrected in final-* evidence.
- build.json: final incremental build succeeded, zero errors, one warning about
  unused RuntimePipelineConfig. Initial full build also had existing Extenject
  test-resource missing-script warnings. Dependency warnings were not repaired.
- Player shutdown retains the pre-existing ComputeBuffer disposal warning.
  No claim of an audio test, memory-leak fix or full 30-minute balance run.
- All test saves are under this directory, separate from the player's profile.

Source: ArenaMenuCapture (--menu-capture <output>, --menu-resume on second
process, --act-save-dir <isolated-save>), ArenaMenuTests. Without the explicit
capture flag the helper is dormant. The old act/tactics captures target the
historical panel and are not the verification route for this new UI.
