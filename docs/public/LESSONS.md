# Agent Lessons Learned — Hub 84" / FirePro W7100 Era

APPEND ONLY in the private lab copy; this public export is a snapshot.

Recurring mistakes and corrective patterns from display/GPU recovery on a mixed **780M + FirePro W7100** desktop driving a Surface Hub **84"** via dual DisplayPort and card-side SLS.

---

## Known-good reference

| Display | GPU | GDI | Desktop | Notes |
|---------|-----|-----|---------|--------|
| Side panel (Samsung LS24) | Radeon 780M | `DISPLAY9` | **1280×720** | Keep low-res; optional during Hub-only recovery |
| Surface Hub 84" | FirePro W7100 | `DISPLAY1` | **3840×2160 @ 120** | One logical path `PPX0084\UID264` |

Windows Settings can show Hub **active signal 960×2160 @ 120Hz** while the desktop is full 4K @ 120Hz. That is MST-tile EDID + card-side SLS stitch — not a bug.

| Cables plugged | Result |
|----------------|--------|
| **One** DP | **3840×2160 @ 30 Hz** — single-link fallback |
| **Both** DPs | **3840×2160 @ 120 Hz** desktop (signal may still show 960×2160@120) |

FirePro driver **27.20.21026.2006** (21.Q2.1). Stock SLS: `EnableSlsSupport=28`, `KMD_SlsConfigCount=1`. MST tiles **1_8** and **1_9** connected when healthy.

---

## Lessons

### 1) Distinguish Hub CCD-disabled from GPU-missing (2026-08-19)

- **Problem:** After NVIDIA leftover cleanup the 84" was wrong/dark; easy to assume a missing second DP cable or a dead FirePro.
- **Root Cause:** Windows had disabled the **one** logical Hub path `PPX0084\UID264`. FirePro was healthy; AMD already saw MST tiles 1_8 and 1_9; SLS config count was 1.
- **Pattern:** Check adapter PnP (FirePro/780M OK?) vs monitor/CCD path (UID264 active?). Phantom Hub UIDs 265/256/268/269 are expected with card-side stitch, not extra cables. Related: ADR 002.

### 2) Re-enable UID264 only; never Settings → Extend (2026-08-19)

- **Problem:** Mixed-GPU “Extend” blacks the side panel and everything else. First SetDisplayConfig mixed-GPU attempts returned error 87.
- **Root Cause:** Windows Extend topology is the wrong tool for a card-side Eyefinity/SLS Hub. Enabling the Hub must keep the side panel 1280×720 path when present.
- **Pattern:** Apply known-good CCD (side panel path + Hub target 264). Success: Hub 3840×2160 @ (720,0) on `DISPLAY1`; side panel unchanged. Related: ADR 001–003.

### 3) NVIDIA leftover OEM names will change (2026-08-19)

- **Problem:** Hard-coding `oemN.inf` will miss the next ghost install.
- **Root Cause:** Published names are assigned by the driver store. The stable identity is Provider **NVIDIA** + original INF **`nv_dispwi.inf`** / **`nvhda.inf`**.
- **Pattern:** Enumerate `pnputil /enum-drivers`, match provider + original name, then `/delete-driver`. Related: ADR 004.

### 4) Temp is not a runbook (2026-08-19)

- **Problem:** Diagnostics and uninstall logs lived in `%TEMP%` and were deleted by a cleanup pass.
- **Root Cause:** Throwaway Temp helpers look like leftovers and may not survive Recycle Bin recovery.
- **Pattern:** Keep diagnostics, scripts, and session records in a persistent project folder — not Temp.

### 5) Software DPMS does not wake this Surface Hub (2026-08-19)

- **Problem:** Panel stayed dark while Settings looked known-good.
- **Root Cause:** `SC_MONITORPOWER -1` via HWND_BROADCAST plus a mouse nudge did not visibly wake the 84".
- **Pattern:** Do not keep sending monitor-power messages. Do not “fix” it with Extend. Escalate to physical Hub input/power, then re-check UID264. Related: ADR 005.

### 6) Hub input cycle failed; power cycle is the remaining physical step (2026-08-19)

- **Problem:** Operator cycled Hub inputs; the panel still did not lock to the FirePro/Windows configuration. Settings on the PC stayed correct.
- **Root Cause:** Retrain-by-input-cycle did not bind the sink. This is panel/link, not a Windows layout miss.
- **Pattern:** Next action is **physical power cycle**. After hotplug, Windows may disable UID264 again — re-enable that path only; keep side panel 1280×720 when present; never Extend. Related: ADR 006.

### 7) NVIDIA cleanup can disable the Hub path as collateral (2026-08-19)

- **Problem:** Hub path went inactive after ghost NVIDIA uninstall even though AMD was never uninstalled.
- **Root Cause:** Windows display config can drop a non-primary CCD path across driver-store churn.
- **Pattern:** After NVIDIA leftover removal, immediately verify UID264 / CCD active paths before assuming cabling or SLS broke.

### 8) UID264 disable ≠ Hub flicker (2026-08-19)

- **Problem:** Easy to treat later Hub black flicker/tearing as “why Windows auto-disabled the Hub in Settings.”
- **Root Cause:** UID264 went inactive earlier after **NVIDIA leftover uninstall**. AMD SLS was already saved. That disable is CCD collateral from driver-store churn, not flicker. Flicker/tearing showed up later — a different event.
- **Pattern:** Do not “fix flicker” by treating Settings disable as the same bug. Disabled Hub → re-enable UID264 only (never Extend). Flicker → gentle CCD renegotiate, no extra SLS, do not live-reset FirePro as the default. Related: ADR 004, ADR 007, Lesson 7.

### 9) Live FirePro restart can TDR; default poke is gentle CCD (2026-08-19)

- **Problem:** Live `pnputil /restart-device` of the W7100 on a running desktop can TDR and leave flicker/tearing/artifacts.
- **Root Cause:** Hot retraining disables/enables the adapter while DWM/3D contexts are live.
- **Pattern:** Default Hub poke = CCD only (`GentleHandshake` via Hub Poke). Hot retraining is optional (dedicated button or Shift-click). Related: ADR 008.

### 10) Do not set Hub desktop to 960 (2026-08-19)

- **Problem:** Applying the MST tile size as the Windows desktop looks “right” in Settings signal and wrong as a 960-wide desktop.
- **Root Cause:** Known-good **signal** is 960×2160@120 (tile EDID); known-good **desktop** is 3840×2160@120 (card-side stitch).
- **Pattern:** Target desktop 4K120; leave signal as AMD wants. Rollback if desktop becomes 960. Leave stock SLS (`EnableSlsSupport=28`). Related: ADR 010.

### 11) Hub touch HID is present but not grouped with UID264 (2026-08-19)

- **Problem:** Easy to treat missing Hub taps as a disabled device or a FirePro/DP problem.
- **Root Cause:** USB composite `VID_2465&PID_6512` enumerates; digitizer is not Error/Disabled. Monitor container ≠ touch HID container, so after a mode change Windows may map taps to the side panel.
- **Pattern:** Do not live-restart FirePro to fix touch. Use **Map touch → Hub** (separate button; verified working — see Lesson 15). Related: ADR 009, ADR 014.

### 12) Old DP cables were the SI/flicker factor (2026-08-19)

- **Problem:** Session treated cable upgrade as deferred hardware; flicker was blamed on GPU restart, topology, or Windows disable events.
- **Root Cause:** Long/suspect DisplayPort cables were a real signal-integrity problem on this path.
- **Pattern:** Live hot-swap to new cables: instant negotiation, stable picture. Same Hub + FirePro W7100 + stock SLS. Related: ADR 011.

### 13) One DP = 4K30; both DPs = 4K120 (2026-08-19)

- **Problem:** 4K@30 with one cable plugged looks like a Windows Settings or CCD mystery.
- **Root Cause:** Dual-DP / SLS needs **both** links for 120 Hz; a single cable is the known single-link fallback at 30 Hz.
- **Pattern:** One cable → immediate 4K30; second cable → immediate 4K120 (desktop; signal may still show 960×2160@120). Check both cables before CCD fights. Related: ADR 012.

### 14) Cold power cycle over sleep for config-memory test (2026-08-19)

- **Problem:** Sleep/resume can leave display config in a bad state and is not a clean test of cold-boot persistence.
- **Root Cause:** Sleep renegotiation differs from full power-off state.
- **Pattern:** Test whether Windows/AMD remember 4K120 with **power off → power on**, not sleep. After boot: Hub Poke if UID264 disabled or 4K30 sticks; Map touch if taps map to the side panel. Never Extend. Related: ADR 013.

### 15) Map touch → Hub verified; run separately after reboot if needed (2026-08-19)

- **Problem:** ADR 009 treated mapping as an unverified test; easy to conflate with default poke or FirePro restart.
- **Root Cause:** Touch is USB/HID, not DP. Mapping is verified working; default poke did not break it.
- **Pattern:** If cold boot sends taps to the side panel, use **Map touch → Hub** (separate button). Default Poke = gentle CCD only. Related: ADR 014.

### 16) Do not hide a 640×480 MST stub in Hub Poke (2026-09-06)

- **Problem:** Easy to collapse/filter the 640×480 row so the app looks “one Hub” while Settings still has Display 2.
- **Root Cause:** After a Windows update, CCD exposed UID264 (4K30) and UID268 (Default_Monitor 640×480) as two sources. SLS keys were not wiped; the stitch is not live. Hub Poke keys by GDI source — two sources is a real topology bug.
- **Pattern:** Deactivate the extra path (CCD Hub-only apply, disable Default_Monitor UID268, card-side SLS). Never Settings → Extend. Never omit the stub from the table while it is still an active/available source. Side panel optional. Related: ADR 015.

### 17) Do not PnP-disable UID265 to “fix” two Settings rows (2026-09-06)

- **Problem:** Disabling the second Surface Hub PnP (`UID265`) and Default_Monitor `UID268` hid the stub and left a single 4K@30 desktop. Easy to treat that as success or as “SLS already one monitor.”
- **Root Cause:** The 84" stitch is **card-side SLS**. Both MST tiles must stay visible to the FirePro. One tile/link is the known **4K30** fallback (ADR 012). Stock SLS keys were still present; they were not live. A custom ADL 2×1 create failed (`SLSMapConfig_Create` rc=-3). Live FirePro restart with tiles enabled did not invent 120 Hz modes.
- **Pattern:** Re-enable UID265/UID268 if disabled. Keep one Windows desktop via the **card**, not Settings → Extend and not Hub Poke row-hiding. If still 4K30 with both tiles OK, **reboot** (operator) rather than writing a new topology engine or shipping a fake Restore-SLS button. Related: ADR 017.

### 18) Reboot did not restore 4K120; FirePro driver was not swapped (2026-09-06)

- **Problem:** After Windows update then restart, then an operator reboot, the Hub stayed **3840×2160@30**. Easy to blame a WHQL generic driver replacing `27.20.21026.2006`.
- **Root Cause:** FirePro stayed on **27.20.21026.2006**. 780M stayed **32.0.21030.2001**. No AMD display package in recent WU history. DAL **1_8/1_9** still connected. The live 960@120 MST modes in DCE dropped at the **FirePro KMD restart** (log reset), not at a driver-store swap. After reboot, CCC/ADL still see **one** Hub at 4K30 (HBR2×4, 297 MHz) plus an unmapped 640×480 tile. `SLSGrid_Caps`=-1; vendor Create still -3.
- **Pattern:** Verify DriverVersion against 27.20.21026.2006 **before** assuming WU ate Eyefinity. If the version matches, hunt live SLS/MST (DCE 960 vs 4K30, CCC Display specs, ADL mapped vs unmapped tiles) — do not reboot again as the fix, do not DDU, do not uninstall 780M. Related: ADR 018.

### 19) Same-bits 21.Q2.1 FORCE + Setup + CCC repair does not revive SLS (2026-09-06)

- **Problem:** Last software lever was FirePro/Pro repair from on-disk 21.Q2.1. Easy to keep blocking on 780M UMD clobber, or to treat Setup.exe exit 0 as a real reinstall.
- **Root Cause:** WHQL INF is already in DriverStore (**27.20.21026.2006**). FORCE `UpdateDriverForPlugAndPlayDevices` on W7100 succeeded and did not add 4K120. AMD Software 21.Q2.1 is already installed; `Setup.exe -INSTALL -SILENT` is a stub that starts CCC. CCC Slim MSI reconfigure succeeded; `SLSGrid_Caps` stayed -1. Desktop stayed `3840×2160@30`, SM_CMONITORS=1. 780M stayed `32.0.21030.2001`.
- **Pattern:** If INF matches DriverStore, Setup silent will not restitch SLS. Do not CIM EXPRESS_UNINSTALL while the Hub is the only monitor. Do not chase a Settings 120 Hz cached-mode blip. Related: ADR 019.

### 20) AtiSetup native install is error 206 until reboot; unquoted -LOG paths break (2026-09-06)

- **Problem:** `Setup.exe -INSTALL -SILENT -LOG` with an install path containing a space wrote the log to the wrong directory (unquoted path split at the space). Direct `BIN64\AtiSetup.exe -INSTALL` (Windows-native, no WSL) then failed with **error 206**.
- **Root Cause:** InstallMan detects W7100 already on 21.Q2.1 (legacy driver-only mode, eligible) but `PendingFileRenameOperations` (atieclxx.exe in-use rename leftovers) makes the installer demand a reboot first. During detection GDI briefly became **960×2160@120 desktop**; rollback restored 4K30. `SLSGrid_Caps` stayed -1.
- **Pattern:** Quote AMD `-LOG` paths or use a space-free log directory. Do not use WSL for native setup. Do not leave 960 as the desktop. Error 206 is not “already installed success.” Related: ADR 019.

---

## AMD CCC / W7100 Advanced — healthy vs unhealthy (from runbook)

When troubleshooting dual-DP SLS on the W7100:

| CCC Advanced look | Meaning |
|-------------------|---------|
| Empty **Connected ()** on a DP port | Leftover HPD / bad state — not “buy cables” |
| Two **Unsupported Type (MST)** tiles with red X | **Healthy** 4K120 target — AMD’s MST UI uses red X |
| One tile, 4K30 | Single-link fallback — check second DP cable |

**Cold Retraining:** Hub fully off, **both** Replacement-PC DP cables seated, then Hub on. Do not hot-plug the second DP after a 4K30 lock and expect 120 Hz.

---

## Deployment checklist (quick scan)

- [ ] Do **not** Settings → Extend (mixed 780M + FirePro).
- [ ] Confirm FirePro + 780M still OK; side panel still 1280×720 when present.
- [ ] If Hub missing in Settings: check UID264 disabled vs GPU missing.
- [ ] If UID264 disabled: re-enable that path only (not during an in-progress Hub power cycle unless the path dropped).
- [ ] If Settings look known-good (960×2160 signal / 4K desktop @ 120Hz) and panel is dark: physical Hub power cycle; software DPMS and input cycle already failed.
- [ ] After Hub hotplug: if Windows dropped UID264, re-enable that path again.
- [ ] NVIDIA ghosts: identify by Provider + `nv_dispwi`/`nvhda`; do not touch AMD or archived NVIDIA EXEs.
- [ ] UID264 Settings-disable ≠ later Hub flicker (Lesson 8 / ADR 007).
- [ ] Hub poke default = gentle CCD only; after sleep, input cycle, or live DP hot-swap.
- [ ] Do not set Hub desktop to 960 (tile is signal). Leave stock SLS (`EnableSlsSupport=28`).
- [ ] Hot retraining (live FirePro restart) is optional / Shift-click only — can TDR.
- [ ] Touch: USB/HID present (`VID_2465`/`PID_6512`); **Map touch → Hub** verified — separate button, not default Poke. Re-run after cold boot if taps land on the side panel.
- [ ] Both DP cables required for 4K120; one cable = 4K30 fallback (ADR 012 / Lesson 13).
- [ ] Old long DP cables were confirmed SI/flicker factor; replacement cables fixed negotiation (ADR 011).
- [ ] Config-memory test: **cold power cycle** (power off → on), not sleep (ADR 013).
- [ ] After a Windows update two-source Hub: do **not** PnP-disable UID265 (that locks 4K30). Do **not** hide the 640×480 row in Hub Poke. Side panel optional. 4K120 is card-side SLS. Operator reboot **already failed**. Same-bits 21.Q2.1 FORCE INF + Setup silent + CCC Slim repair **already failed** (`SLSGrid_Caps`=-1, desktop 4K30) — do not CIM EXPRESS_UNINSTALL while the Hub is the only Settings monitor (Lesson 19 / ADR 019).

---

## Usage

- Read this doc before planning or implementing display/GPU changes on an era-matched Hub + W7100 box.
- Cross-reference [DECISIONS.md](DECISIONS.md) for ADR 001–019.
- Add new lessons after incidents in your private lab copy; refresh the public export when ready to publish again.
