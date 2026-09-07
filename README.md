# Hub Poke

**Make your 2016 dumpster dive actually usable.**

You pulled a Surface Hub 84" out of a conference-room grave. You want a **dedicated machine** to drive it — not a laptop docked as a guest, not a 2026 gaming card “because 4K is 4K.” This is the poke panel for that wall.

The payoff, when hardware and software are from the **same era**: a **120 Hz 4K 84-inch TOUCH** display.

## This is an era kit

Microsoft’s own 4K120 path for the 84" was **two DisplayPort 1.2 links + AMD Eyefinity / SLS** on a workstation card of that generation. New GPUs, new Adrenalin, and Windows Settings → Extend will not invent that stitch.

**What this machine actually runs**

| Piece | Known-good here |
| --- | --- |
| Panel | Surface Hub **84"** (`PPX0084`), **Replacement PC** dual DP (not Guest) |
| GPU | **AMD FirePro W7100** (`VEN_1002` / `DEV_692B`), four DP 1.2 |
| Stitch | Card-side **SLS / Eyefinity**, one Windows desktop |
| FirePro driver | **27.20.21026.2006** (Radeon Pro Software Enterprise **21.Q2.1**) |
| OS | Windows 11 can host it — the **GPU stack** still has to be 21.Q2-era |
| Cables | **Two** DP 1.2, both Replacement-PC jacks seated **before** the Hub powers on |
| Touch | Hub HID `VID_2465` / `PID_6512` (not a DisplayPort trick) |

One DP = **4K @ 30**. Both DPs + live SLS = **3840×2160 @ 120** desktop (the MST tile *signal* may still read 960×2160@120 — that is not the desktop).

Want a second monitor? Fine as a side panel on another GPU. Do not let Windows treat the Hub as “extend these mixed adapters.”

## Things we learned to avoid

In our experience this **only works with old-school AMD cards on older drivers**. Match the hardware **and** the software to that era. Then do not do these:

- **Don’t buy a modern GPU for this job.** You want a dedicated box with a DP 1.2 workstation card that still does MST tiles + Eyefinity.
- **Don’t put current Adrenalin on the W7100.** Keep **21.Q2.1** / **27.20.21026.2006**.
- **Don’t Windows Settings → Extend** on mixed iGPU + FirePro. It blacks the wrong screens.
- **Don’t set the Hub desktop to 960.** That’s the tile. Desktop is **4K120**.
- **Don’t PnP-disable the second Hub tile** (UID265) to “clean up” Settings. That locks **4K30**.
- **Don’t hide 640×480 stubs** in this app so it looks like one display. The stub is a symptom.
- **Don’t DDU / don’t rip the iGPU** as the first “fix.”
- **Don’t live-restart the FirePro** as the default poke. That’s **Hot Retraining**; it can TDR.
- **Don’t hot-plug the second DP** after a single-link 4K30 lock and expect 120 Hz.
- **Don’t treat AMD’s red X + “Unsupported Type (MST)” as a dead port.** Two of those is the *healthy* 4K120 look.
- **Don’t treat empty `Connected ()` as “buy cables.”** That’s leftover HPD. **Cold Retraining:** Hub fully off, **both** cables already in, Hub on.
- **Don’t use software DPMS** to wake the 84". Power-cycle the Hub.
- **Don’t Clone** hoping it invents 4K120.
- **Don’t assume a Windows Update ate the FirePro driver** until you check the version. Ours stayed 21.Q2.1 while SLS was simply not live.

## Hub Poke

| Control | What it does |
| --- | --- |
| **Hub** | Gentle CCD renegotiate after sleep, input cycle, or a live cable hot-swap. No GPU restart. |
| **Map Touch → Hub** | Put taps on the 84-inch. Separate from poke. |
| **Hot Retraining** | Live FirePro disable/enable. Last software resort. |
| **Tried all three, and it still isn't working?** | **Cold Retraining** walkthrough (Hub power cycle with both DPs seated). |

## Build (Windows)

```bat
build-button.cmd
```

Needs the .NET Framework 4.x `csc.exe` that ships with Windows. Keep `assets\` next to `HubButton.exe`.

This working tree may also contain a private HubFix recovery lab (drivers, logs, one-off tools). Those are gitignored. Do not force-add them. Hub Poke finds the W7100 (`VEN_1002&DEV_692B`) and Hub touch (`VID_2465&PID_6512`) at runtime — it does not ship machine instance serials.
