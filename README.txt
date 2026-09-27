摸鱼 0.2.0

Full documentation: README.md
Release history: CHANGELOG.md
New: custom 1-240 minute duration, animation speed, cancellable 5-second start,
session elapsed summary and CLI quick start. See README.md for arguments.

Run:
  Double-click 摸鱼.exe

Build from source:
  Right-click build_exe.ps1 and run it with PowerShell, or run:
    powershell -ExecutionPolicy Bypass -File build_exe.ps1

Exit:
  On the intro page, close the app normally.
  In fish mode, press Esc to return to the intro page.

Notes:
  Preview opens an animated window of the chosen state without starting a
  fullscreen session. Close it normally or press Esc.
  Secondary-display blackout can be disabled; this option is saved too.
  Status and auto-return duration are saved automatically in
  %LOCALAPPDATA%\Moyu\settings.xml. Missing or invalid settings use defaults.
  Copying the exe elsewhere on the same account preserves these preferences.

  Repeated launches activate the existing intro or fullscreen window.
  Display changes rebuild fullscreen coverage, retaining the selected status
  and the original auto-return deadline. Physical monitor unplug/replug has not
  been tested; the display-change event/rebuild path has been tested.

  Auto-return: unlimited (default), 15, 30, or 60 minutes.
  Alt+F4 is blocked during fullscreen mode; Esc returns to the intro.

  The app starts with an intro page. Click a status button, then click the
  start button to enter the selected Windows 11 style black screen. The primary
  display shows the selected status, and secondary displays are covered with
  plain black windows.

  It does not install anything, persist in the background, change system
  settings, or block Windows system shortcuts such as Ctrl+Alt+Del, Alt+Tab,
  or the Windows key.
