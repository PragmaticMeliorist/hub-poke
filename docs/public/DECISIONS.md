# Hub 84" / FirePro W7100 — Architectural Decision Records

Each entry is an ADR: decision, rationale, implications, and status. Dates reflect the original recovery sessions (2026-08-19 and 2026-09-06).

Hardware context: Surface Hub **84"** (`PPX0084`), **Replacement PC** dual DisplayPort, **AMD FirePro W7100** (`VEN_1002` / `DEV_692B`), Radeon Pro Software Enterprise **21.Q2.1** / driver **27.20.21026.2006**, mixed with a secondary **Radeon 780M** on the same box.

---

## ADR 001 — Never Settings → Extend on mixed 780M + FirePro (2026-08-19)

- **Decision:** Do not use Windows Settings → Extend to “add” the Surface Hub. Treat Extend as forbidden on this desktop.
- **Rationale:** Dual DisplayPort Hub stitch is **on the FirePro W7100** (Eyefinity/SLS). Windows Extend on mixed 780M + FirePro blacks **all** monitors, including the side panel.
- **Implications:** Recovery is CCD/SetDisplayConfig on the one Hub path, or physical Hub power/input — never the Extend control.
- **Related:** ADR 002, ADR 006, Lesson 1
- **Status:** Accepted

---

## ADR 002 — Hub is one logical FirePro path (UID264); stitch is card-side SLS (2026-08-19)

- **Decision:** Treat the 84" as a single logical display `PPX0084\UID264` on FirePro `DISPLAY1`. Do not hunt a second Windows monitor or a missing second cable when MST tiles already show connected.
- **Rationale:** After NVIDIA leftover cleanup, Windows had **disabled UID264**. AMD already listed MST tiles 1_8 and 1_9 connected; `EnableSlsSupport=28`, `KMD_SlsConfigCount=1`. Phantom UIDs 265/256/268/269 are expected when the card stitches.
- **Implications:** Diagnose disabled CCD path vs missing GPU. Re-enable UID264 only. Known-good desktop is 3840×2160 @ (720,0) even when Settings shows active signal 960×2160 @ 120Hz (MST tile EDID).
- **Related:** ADR 001, ADR 005, Lesson 1, Lesson 2
- **Status:** Accepted

---

## ADR 003 — Keep side panel at 1280×720 on 780M (2026-08-19)

- **Decision:** Leave the Samsung side panel at **1280×720** on 780M `DISPLAY9` (rotated 720×1280 view is OK). Do not raise its mode during Hub recovery.
- **Rationale:** Low resolution reduces PCIe/bandwidth load. Hub recovery must not trade side-panel mode for a “fix.”
- **Implications:** Any SetDisplayConfig apply must keep the side panel active path and 1280×720.
- **Related:** ADR 002, Lesson 2
- **Status:** Accepted

---

## ADR 004 — Uninstall NVIDIA ghosts by provider + INF, leave AMD alone (2026-08-19)

- **Decision:** Remove leftover NVIDIA Graphics/HD Audio with `pnputil /remove-device` + `/delete-driver`, stop/delete NVIDIA services, remove NVIDIA Control Panel appx and Installer2/ProgramData leftovers. Identify packages by **Provider NVIDIA** and original names **`nv_dispwi.inf` / `nvhda.inf`**, not hard-coded `oemN.inf`. Do not touch AMD. Do not delete archived NVIDIA installer EXEs kept for reference.
- **Rationale:** A leftover NVIDIA display adapter was still registered (status Unknown); the card was **not** in the machine. NVIDIA services were running anyway. Published OEM names (`oem*.inf`) will change on the next install.
- **Implications:** Cleanup can make Windows disable UID264; that is expected collateral, not a reason to reinstall NVIDIA.
- **Related:** Lesson 3, Lesson 4
- **Status:** Accepted

---

## ADR 005 — Dark Hub with good Settings is wake/link, not topology (2026-08-19)

- **Decision:** If Windows Settings already match the known-good Hub signal/desktop and the side panel is still 1280×720, do **not** change display topology. Escalate physically: software DPMS (already failed) → Hub input cycle (failed this session) → **Hub power cycle**.
- **Rationale:** Software `SC_MONITORPOWER -1` returned success but did not visibly wake the panel. Input cycling did not make the Hub lock to the FirePro/Windows configuration while the PC still looked correct. Remaining physical step is power cycle.
- **Implications:** After power/hotplug, Windows may drop UID264 again. Repeat ADR 002 recovery. Never Extend.
- **Related:** ADR 006, Lesson 5, Lesson 6
- **Status:** Accepted

---

## ADR 006 — Pivot: input cycle failed; next step is Hub power cycle (2026-08-19)

- **Decision:** Stop treating input cycling as the likely wake. Operator power-cycles the Hub next. Agents must not SetDisplayConfig, Extend, or change topology during that physical step unless UID264 is shown disabled afterward.
- **Rationale:** Late session evidence overrides the earlier “cycle inputs to retrain” hope. PC-side config was already correct; the panel did not lock.
- **Implications:** Documented recovery order: confirm Settings/CCD → if UID264 disabled, re-enable only that path → if path is good and panel dark, power-cycle Hub → if Windows dropped UID264 on hotplug, re-enable again.
- **Related:** ADR 005, Lesson 6
- **Status:** Accepted

---

## ADR 007 — UID264 disable was NVIDIA leftover collateral, not flicker (2026-08-19)

- **Decision:** Treat the earlier Windows auto-disable of Hub `PPX0084\UID264` as **collateral from NVIDIA leftover cleanup** (ghost A5000 removal), not as a flicker/tearing bug. Flicker and Settings-disable are different events.
- **Rationale:** Session order: NVIDIA ghost uninstall → UID264 inactive while FirePro/780M stayed OK and card-side SLS was already `EnableSlsSupport=28`, `KMD_SlsConfigCount=1`. Black flicker, tearing, and live-GPU artifacting appeared later (including after FirePro `pnputil /restart-device` and a 4K@30 fallback). Same long DP cables had previously run stable 4K120.
- **Implications:** Do not “fix flicker” by hunting a second disable/Extend topology. Disabled path → CCD re-enable UID264 only, side panel 1280×720, never Extend. Flicker → gentle link renegotiate; keep live FirePro restart off the default poke.
- **Related:** ADR 002, ADR 004, Lesson 7, Lesson 8
- **Status:** Accepted

---

## ADR 008 — Default Hub poke is gentle CCD; hard FirePro restart is optional (2026-08-19)

- **Decision:** Default **Poke Hub** = `HubRenegotiate.GentleHandshake()`: CCD apply known-good Hub **desktop 3840×2160@120**, signal as AMD/EDID wants (often **960×2160@120**), side panel **1280×720**, never Extend, **no live FirePro restart**. Use after sleep, Hub input cycle, or live DP hot-swap. **Hot retraining** (`pnputil /restart-device` of the W7100 while Windows is live) stays off-by-default: separate button or Shift-click Poke, with UAC and a confirm dialog.
- **Rationale:** A live FirePro restart on a running desktop can TDR and leave flicker/tearing/artifacts. Gentle CCD is enough to renegotiate the Hub link after sleep / input cycle / hot-swap. Default poke must not change topology.
- **Implications:** Digitizer mapping is not on the default click (ADR 009). Do not live-restart FirePro to “fix touch.”
- **Related:** ADR 002, ADR 007, ADR 009, Lesson 8, Lesson 9
- **Status:** Accepted

---

## ADR 009 — Touch mapping stays a separate test until 84-inch taps are verified (2026-08-19)

- **Decision:** Keep **Map touch → Hub (test)** off the default Poke. USB/HID for the 84" is present (`VID_2465&PID_6512`; 84" Touch Device, HID-compliant touch screen, HEAT, pens all OK). Mapping is **not grouped** with monitor UID264; after a mode change Windows may send taps to the side panel (primary). The operator must tap the 84-inch and confirm the cursor lands on the 4K Hub before mapping is allowed on the default click.
- **Rationale:** Touch is a USB/HID path, not DP/FirePro. The digitizer is not Error/Disabled (unlike the earlier UID264 CCD disable). Failed Hub-internal USB ports (descriptor failures, likely camera) are not the digitizer.
- **Implications:** Do not live-restart FirePro to fix mapping.
- **Related:** ADR 008, ADR 014, Lesson 11, Lesson 15
- **Status:** Superseded by ADR 014 (test gate only; USB/HID separation remains)

---

## ADR 010 — Never apply 960 as Hub desktop; leave stock SLS (2026-08-19)

- **Decision:** Known-good is Hub **desktop 3840×2160@120** and **signal 960×2160@120**. Do **not** set the desktop to 960. Gentle handshake rolls back if desktop becomes 960 or the side panel path drops. Do not write extra Eyefinity/SLS; leave stock `EnableSlsSupport=28` and `KMD_SlsConfigCount=1`.
- **Rationale:** 960×2160 is MST-tile EDID, not the logical 4K desktop. The card already stitches; extra SLS writes are overkill.
- **Implications:** Diagnose flicker with gentle renegotiate first — not topology, not SLS rewrite, not live GPU restart, not “desktop=signal.”
- **Related:** ADR 002, ADR 003, ADR 007, ADR 008, ADR 011, Lesson 10
- **Status:** Accepted

---

## ADR 011 — Old DP cables confirmed SI/flicker root cause; live upgrade succeeded (2026-08-19)

- **Decision:** Treat the previous long/suspect DisplayPort cables as the real signal-integrity (SI) factor behind flicker and negotiation fights. Replacement cables (live hot-swap) immediately produced stable picture and instant negotiation on the same Hub, FirePro W7100, and stock SLS.
- **Rationale:** Session had deferred cable upgrade as “later hardware.” Live hot-swap proved the old cables were the problem — not a Windows Settings mystery, not missing SLS rewrite, not topology.
- **Implications:** Cable quality is confirmed first-class hardware for this path. Gentle Hub poke remains the software recovery after sleep/input/hotplug. Supersedes cable-deferral language in ADR 007 and ADR 010.
- **Related:** ADR 007, ADR 010, ADR 012, Lesson 12
- **Status:** Accepted

---

## ADR 012 — One DP cable = 4K@30; both DPs required for 4K@120 (2026-08-19)

- **Decision:** Document the dual-link requirement: with **one** DP cable plugged, the Hub negotiates **3840×2160 @ 30 Hz** (known single-link fallback). With **both** DP cables plugged, negotiation is immediately **3840×2160 @ 120 Hz** (desktop 4K120; active signal may still show 960×2160@120). This is card-side dual-DP / SLS behavior, not a Windows Settings bug.
- **Rationale:** Live hot-swap during cable upgrade showed stepwise mode change: one cable → instant 30 Hz; second cable → instant 120 Hz. Confirms both MST tiles/links must be present for 120 Hz.
- **Implications:** If only 4K30 appears after reboot, check both DP cables before fighting CCD. Software poke still the recovery tool if power cycle drops the path. Do not set desktop to 960.
- **Related:** ADR 002, ADR 010, ADR 011, Lesson 13
- **Status:** Accepted

---

## ADR 013 — Prefer cold power cycle over sleep for config-memory test (2026-08-19)

- **Decision:** For testing whether Windows/AMD **remember** 4K120 after reboot, use a **full power off → power on** (machine and/or Hub), **not sleep**. Sleep is deferred as unreliable for this persistence test.
- **Rationale:** Operator’s next validation step is cold-boot persistence after cable upgrade. Sleep renegotiation is a separate concern already covered by gentle poke.
- **Implications:** After cold boot: if UID264 disabled or stuck at 4K30, use Hub Poke (gentle CCD) and verify both DP links; **Map touch → Hub** if digitizer maps to the side panel. Never Extend. Side panel stays 1280×720. Agents must **not** run the power cycle for the operator.
- **Related:** ADR 008, ADR 012, ADR 014, Lesson 14
- **Status:** Accepted

---

## ADR 014 — Map touch → Hub verified; stays separate from default Poke (2026-08-19)

- **Decision:** **Map touch → Hub** is verified working (operator confirmed taps on the 84"). It remains a **separate** button from default **Poke Hub** — default poke did not break mapping. Hot retraining stays optional/off-default.
- **Rationale:** ADR 009 gated mapping behind a test until 84" taps were verified. That gate is passed. Keeping mapping off the default click avoids conflating link renegotiate with HID mapping writes.
- **Implications:** After cold boot, if taps land on the side panel, run Map touch → Hub — not default Poke. Supersedes ADR 009 test gate (not the USB/HID vs UID264 separation).
- **Related:** ADR 008, ADR 009, Lesson 15
- **Status:** Accepted

---

## ADR 015 — Windows update left two CCD sources; SLS keys were not wiped (2026-09-06)

- **Decision:** After the update, treat the 640×480 Settings “neck” as a second FirePro CCD target (`UID268` Default_Monitor), not a missing side panel and not a reason to Settings → Extend. Restore **one** active Hub path with SetDisplayConfig (side panel optional). Do **not** hide that stub in Hub Poke while a second GDI/CCD source still exists.
- **Rationale:** Stock SLS registry was still `EnableSlsSupport=28`, `KMD_SlsConfigCount=1`. FirePro and 780M stayed OK. Side panel was absent (KVM). GentleHandshake used to require the side panel and would no-op. CCD had UID264 4K@30 + UID268 640×480 extend. DAL still showed MST tiles 1_8 and 1_9 connected. Preferred mode for UID264 is 4K30 (no 960×2160@120 advertised).
- **Implications:** Hub-only CCD + disable Default_Monitor UID268 removes the extra **desktop** path (`SM_CMONITORS=1`). Settings may still list target 268 as available and WMI still shows UID265 as a second Active Hub until card-side SLS actually stitches. Do not filter Hub Poke rows. Do not Extend. 4K120 needs SLS; CCD cannot invent 120 Hz when the driver only lists 4K30.
- **Related:** ADR 001, ADR 002, ADR 010, ADR 012, Lesson 16
- **Status:** Accepted

---

## ADR 016 — UID265 extra Hub tile disabled; 4K120 blocked without live SLS (2026-09-06)

- **Decision:** Disable the extra Surface Hub PnP instance `PPX0084\UID265` (not UID264) when Settings/WMI still show a second Hub after UID268 Default_Monitor is already disabled. Do not treat 4K30 as success.
- **Rationale:** WMI had two Active Surface Hubs (264+265). UID265 disable stayed (Error); WMI dropped to one Hub. FirePro still advertises CCD target 268 as available 640×480. Driver mode list is only 4K30; ADL1/ADL2 `DisplayInfo` count is 0; no CCC/AMD Software on the machine. SLS registry is present but not runtime-live.
- **Implications:** 4K120 is **blocked** until the FirePro driver exposes MST/SLS modes or an ADL display list. Disabling UID265 hides the second Hub identity; it does not create 120 Hz. No further live FirePro restart without a new SLS control.
- **Related:** ADR 015, ADR 017, Lesson 16, Lesson 17
- **Status:** Superseded by ADR 017 (PnP-disable of UID265 was the wrong lever)

---

## ADR 017 — Do not PnP-disable the second Hub tile; card-side SLS needs both; reboot is the next lever (2026-09-06)

- **Decision:** Keep **both** Surface Hub MST identities visible to the FirePro (`PPX0084\UID264` and `PPX0084\UID265`). Re-enable UID265 (and Default_Monitor UID268 if it was disabled with it). Do **not** invent a Windows stitcher, do **not** install extra AMD GUI, do **not** add a Hub Poke “Restore SLS” button until 4K120 is actually live. If both tiles are present and the desktop is still **3840×2160@30** with only the 4K30 mode advertised, the next lever is a **clean reboot** so the card can apply stock SLS — agents must not reboot the operator.
- **Rationale:** Dual-DP 84" known-good is one-cable **4K30** vs both-cables + card SLS **4K120** (ADR 012). ADR 016 disabled UID265 to hide the Settings stub; that matches single-link 30 Hz. After re-enable: UID264+UID265+UID268 Started; WMI two Active Hubs; DAL MST **1_8** and **1_9** `ConnectionStatus=1`; Windows **one** desktop `3840×2160@30` (not Extend). FirePro-bound ADL sees displays; System32 ADL is 780M and reports count 0. `ADL_Display_SLSMapConfig_SetState(map=0)` returned 0 but did not add 120 Hz. Live FirePro restart **with tiles left enabled** stayed 4K30. `SLSGrid_Caps` rc=-1; `SLSMapConfig_Create` 2×1 rc=-3. CCC Slim / `RadeonSoftware.exe` exist on disk; do not install more software for this.
- **Implications:** Leave stock `EnableSlsSupport=28` / `KMD_SlsConfigCount=1`. Operator reboot was tried and **did not** restore 4K120 (ADR 018). Never Settings → Extend; never re-disable UID265 as a “one Settings row” fix.
- **Related:** ADR 002, ADR 010, ADR 012, ADR 016, ADR 018, Lesson 13, Lesson 17
- **Status:** Accepted (reboot lever superseded by ADR 018)

---

## ADR 018 — Reboot did not restore SLS; driver still 27.20.21026.2006; CCC sees one 4K30 Hub (2026-09-06)

- **Decision:** Treat post-reboot 4K30 as **SLS/MST not live**, not as a Windows Update FirePro swap. Do **not** reboot again as the solution. Do **not** add a Hub Poke Restore-SLS button. Do **not** DDU. Do **not** uninstall 780M Adrenalin. Do **not** run the full `Win10-Radeon-Pro-Software-Enterprise-21.Q2.1` Setup.exe unless a FirePro-only install can be proven not to replace `VEN_1002&DEV_15BF` (780M). Keep UID265/UID268 enabled. Keep Hub Poke unchanged.
- **Rationale:** After operator reboot: FirePro **27.20.21026.2006** (same as 2026-08-19 known-good), 780M **32.0.21030.2001**, WU history had no AMD display package. DAL tiles 1_8 and 1_9 connected. GDI **one** monitor `3840×2160@30`. FirePro ADL: Hub logical 8 mapped 4K30, logical 9 connected 640×480. DCE: last 960@120 pair was immediately before the earlier FirePro `pnputil /restart-device`; after that, DP-3 is 4K30 MST and DP-4 is 640×480. Radeon Pro Display (CNext, FirePro listed): one Surface Hub, **3840×2160 @ 30 Hz**, **5.4 Gbps × 4**. `SLSGrid_Caps` rc=-1; `SLSMapConfig_Create` 960-tile 2×1 still rc=-3. 21.Q2.1 installer exists on disk from the era kit.
- **Implications:** 4K120 is still blocked. The remaining software lever is restoring **two 960@120 tiles** into CCC/KMD (FirePro-only Pro/CCC repair, not another reboot). Hub Poke stays as-is until 120 Hz is actually live.
- **Related:** ADR 012, ADR 017, Lesson 18
- **Status:** Accepted (Setup/CCC repair path completed in ADR 019; SLS still dead)

---

## ADR 019 — 21.Q2.1 FORCE INF + Setup + CCC Slim repair did not restore SLS (2026-09-06)

- **Decision:** Treat live 4K120 as **blocked** after FirePro WHQL INF FORCE install, `Setup.exe -INSTALL -SILENT`, and CCC Slim MSI reconfigure. Do **not** CIM `EXPRESS_UNINSTALL` while the Hub is the only Settings monitor. Do **not** Settings → Extend. Do **not** set Hub desktop to 960. Do **not** change Hub Poke. Operator Settings 120 Hz blip was a cached mode revert — ignore it.
- **Rationale:** Extracted 21.Q2.1 INF SHA256 matches DriverStore package for **27.20.21026.2006**. FORCE install on `DEV_692B` returned True; GDI stayed `3840×2160@30` (tile `960×2160@120` listed, no `3840×2160@120`). AMD Software **21.Q2.1** was already installed; Setup silent exited 0 in ~1s and launched existing CCC/CNext. CCC Slim MSI `REINSTALL=ALL` reconfigured “AMD Settings” (exit 0). `SLSGrid_Caps` stayed **-1**. 780M stayed `32.0.21030.2001`. Stock SLS keys still `EnableSlsSupport=28` / `KMD_SlsConfigCount=1`. SM_CMONITORS=1. UID264+UID265 OK.
- **Implications:** Same-bits Pro/CCC repair is exhausted. A CIM clean uninstall/reinstall needs a second live display so a FirePro driver drop does not black the only monitor.
- **Related:** ADR 012, ADR 018, Lesson 19
- **Status:** Accepted
