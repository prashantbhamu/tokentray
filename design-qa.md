# Circular popup design QA

final result: passed

## Evidence and scope

- Source visual truth: `design/reference.png`, the user's supplied 1405 x 1120 screenshot.
- Implementation: `design/implementation.png` (360 x 293 native pixels); dark variant `design/dark.png`.
- Combined comparison: `design/comparison.png`. Both designs were opened together in this one comparison image before this report.
- Normalization: remove the source's surrounding white margin using crop (39,39,1328,1059), then scale uniformly to 360 x 287. Implementation remains native 360 x 293 without stretching. The six-pixel height difference is intentional native text/padding allowance.
- Viewport: 360px-wide native WinForms popup, SystemAware DPI, AutoScaleMode.None. CSS/deviceScaleFactor do not apply; these are direct app-rendered pixels, not browser screenshots.
- State: Codex weekly 40%, Claude session 20%, Claude weekly 19%; resets 4d16h, 16m, 5d6h. Capture time is current rather than the reference's historical 19:23. Live preview uses real provider data, not these samples.
- Native desktop scope: existing WinForms app, not a web rebuild. No web scaffolding or browser verification is applicable.

## Findings and required fidelity surfaces

No remaining actionable P0/P1/P2 findings.

- Fonts/typography: Segoe UI, bold provider and meter headings, centered percentage labels, smaller reset text/footer. No clipped, wrapped, or overlapping text at the reference sample state. The native text rendering is slightly heavier than the downsampled source, acceptable for tray-popup legibility.
- Spacing/layout rhythm: one enclosing rounded panel, top Codex row, thin divider, Claude heading, session and weekly rows, bottom update line. Same 60px ring scale, consistent left edge and right-side labels. Six additional pixels of native panel height preserve a clear footer inset.
- Colors/tokens: black Codex and orange Claude intentionally replace reference green. Gray tracks, dark text, muted secondary text, accent plan, subtle border. Fully opaque background. Dark Windows theme retained; Codex stays black as explicitly requested rather than silently changing to white.
- Image quality/assets: data-driven rings are live chart primitives rather than raster assets. Refresh uses the real Font Awesome ArrowsRotate library icon, not a hand-drawn or text-symbol approximation. No decorative images exist in the reference. The slightly different refresh arrow geometry is an acceptable library-icon substitution.
- Copy/content: provider names, Claude plan, weekly/session labels, percentage values, reset descriptions, and update/footer wording retained. Codex plan omitted from top row to match the reference. Current clock and provider values in live mode are expected differences.

## Comparison history

1. First capture: 360 x 315. [P2] Excess vertical spacing versus the normalized reference. Reduced row pitch 66 to 64, Claude heading allowance 32 to 26, top padding 22 to 20, footer allowance 36 to 28. Increased refresh icon size from 19 to 24 to better match reference visibility.
2. Revised capture: 360 x 293. Opened `design/comparison.png` and inspected at readable scale. Earlier density issue resolved; no actionable P0/P1/P2 differences remain. Focused crops unnecessary because all rings, labels, divider, refresh icon, and footer are readable in the combined 760px-wide comparison. Dark variant opened separately for contrast and clipping inspection.

## Functional verification

- 99 automated checks pass: tray regression coverage plus native popup sizing, opacity, taskbar exclusion, refresh callback, refresh disabled/re-enabled during fetch, black/orange ring pixels in both themes, loading state and error state.
- Refresh button is a real focusable IconButton with tooltip and accessible name. F5 refreshes; Escape dismisses.
- Live preview uses original provider clients and periodic refresh. Separate mutex permits the installed main app to continue running. Preview settings changes disabled; no startup registration is performed.
- Remaining gap: no dark-mode visual source was supplied, so that variant preserves the established Windows-theme behavior rather than claiming a dark reference match.

## Implementation checklist

- [x] Reference-based native circular layout.
- [x] Requested provider colors and no status dots.
- [x] Functional refresh and live data wiring.
- [x] Light/dark, loading/error, and tray regression checks.
- [x] Combined source/implementation visual comparison.
- [x] Main installation and automatic startup preserved.

## Follow-up polish

Optional P3: adjust the native refresh icon or footer font weight after the user's in-tray review. No blocking work remains.
