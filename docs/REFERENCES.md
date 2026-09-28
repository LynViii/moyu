# Design References

- https://github.com/TheBigEye/Fake-Update : Windows-themed state selection and focused simulation screens.
- https://github.com/svenstaro/genact : lightweight simulated activity, selectable scenes and exit-after-time option.

These projects were reviewed for product ideas. No source code or assets were copied.
Windowed preview and optional secondary-screen coverage are local additions.
The animation is a local approximation, not an extracted Windows animation.

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
