# Design References

- https://github.com/TheBigEye/Fake-Update : Windows-themed state selection and focused simulation screens.
- https://github.com/svenstaro/genact : lightweight simulated activity, selectable scenes and exit-after-time option.

These projects were reviewed for product ideas. No source code or assets were copied.
Windowed preview and optional secondary-screen coverage are local additions.
The animation is a local approximation, not an extracted Windows animation.

## 0.5.0 Update Flow

Reviewed on 2026-10-28:

- https://docs.github.com/en/rest/releases/releases?apiVersion=2022-11-28 : stable release metadata and latest release endpoint.
- https://docs.github.com/en/rest/releases/assets?apiVersion=2022-11-28 : release attachment URLs, size and digest.
- https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=netframework-4.8.1 : same-volume replacement with an original-file backup.

The updater is locally implemented. HTTPS and SHA-256 provide transport/integrity checks; they are not an Authenticode publisher signature. Updates are opt-in and do not add analytics or import/export features. The original application icon is retained after the proposed replacement was rejected.

## 0.4.0 Desktop Integration and Win11 Animation

Reviewed on 2026-10-26:

- https://learn.microsoft.com/en-us/windows/winui/api/microsoft.ui.xaml.controls.progressring : modern indeterminate ring reference. Win11 now uses a locally drawn continuous arc; Win10 keeps the older dots. This supersedes the earlier five-dot preference for Win11 per the latest user request.
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey : global shortcut registration and MOD_NOREPEAT.
- https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys : current-user login startup.

No Microsoft animation asset or library code was copied. Start-menu handling is a best-effort foreground-process check with a deadline; it is not a documented universal Shell close API.

## 0.3.0 Scene Selection

Reviewed https://github.com/TheBigEye/Fake-Update on 2026-09-27, including the README, index.html and repository tree.
Its homepage selects Windows versions through visual scene entries. The repository currently contains XP, Vista and 7 pages; its README says Windows 8, 10 and 11 are not yet available.
Moyu adopts the version-first selection idea with working native Win10/Win11-style scenes and independent color choices. No code or assets were copied; this reference is not evidence of pixel-exact Windows rendering.
Multi-display targeting, native settings, DPI handling and the light customization are local features.

## 0.2.0 Feature Mapping

- Custom duration: inspired by genact's `--exit-after-time` and configurable breaks in Stretchly.
- Speed presets: inspired by genact's `--speed-factor`; mapped to three simple desktop choices.
- Cancellable 5-second start: local adaptation of advance notice and postponement in https://github.com/hovancik/stretchly . Not a copy of its behavior.
- Session elapsed summary: local companion feature to the duration controls, not claimed to exist in these references.
- CLI quick start: inspired by genact's module/speed/time arguments, mapped to Windows state presets.

https://github.com/tom-james-watson/breaktimer-app was also reviewed for configurable break workflows.
