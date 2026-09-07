using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;

/// <summary>
/// Shared Hub 4K120 handshake. Used by HubButton and a future headless restorer.
/// Default path is CCD apply only — never live-restart FirePro unless HardRetrain.
/// </summary>
public static class HubRenegotiate {
    public const uint QDC_ALL_PATHS = 1;
    public const uint SDC_USE_SUPPLIED = 0x20;
    public const uint SDC_VALIDATE = 0x40;
    public const uint SDC_APPLY = 0x80;
    public const uint SDC_NO_OPTIMIZATION = 0x100;
    public const uint SDC_SAVE = 0x200;
    public const uint SDC_ALLOW_CHANGES = 0x400;
    public const uint SDC_FORCE_MODE_ENUMERATION = 0x1000;
    public const uint PATH_ACTIVE = 1;
    public const uint MODE_INVALID = 0xFFFFFFFF;
    public const uint PIXELFORMAT_32BPP = 4;
    public const uint ROTATION_IDENTITY = 1;
    public const uint SCALING_PREFERRED = 6;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;
    public const int SM_CMONITORS = 80;

    public const uint HUB_DESK_W = 3840;
    public const uint HUB_DESK_H = 2160;
    public const uint HUB_HZ = 120;
    public const uint SAMSUNG_MAX = 1280;
    public const uint HUB_TARGET_ID = 264;

    /// <summary>FirePro W7100 PCI ID. Instance path is discovered at runtime — never hard-code a machine serial.</summary>
    public const string FireProHardwareId = @"VEN_1002&DEV_692B";
    public const string TouchCompositeVidPid = @"USB\VID_2465&PID_6512\";

    [DllImport("user32.dll")]
    static extern int GetDisplayConfigBufferSizes(uint f, out uint np, out uint nm);
    [DllImport("user32.dll")]
    static extern int QueryDisplayConfig(uint f, ref uint np, [Out] DISPLAYCONFIG_PATH_INFO[] p, ref uint nm, [Out] DISPLAYCONFIG_MODE_INFO[] m, IntPtr t);
    [DllImport("user32.dll")]
    static extern int SetDisplayConfig(uint np, DISPLAYCONFIG_PATH_INFO[] p, uint nm, DISPLAYCONFIG_MODE_INFO[] m, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int DisplayConfigGetDeviceInfo(IntPtr r);
    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetPointerDevices(ref uint deviceCount, IntPtr pointerDevices);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, IntPtr lpdwResult);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE dm);

    public const string DigimonKeyPath = @"SOFTWARE\Microsoft\Wisp\Pen\Digimon";
    public const string HidIfaceGuid = "{4d1e55b2-f16f-11cf-88cb-001111000030}";
    public const string MonIfaceGuid = "{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    public const string HubHidVidPid = "VID_2465&PID_6512";
    public const string HubMonitorToken = @"DISPLAY#PPX0084#";
    public const string HubUid264Token = "UID264";
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_SOURCE_INFO { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_TARGET_INFO {
        public LUID adapterId; public uint id; public uint modeInfoIdx; public uint outputTechnology;
        public uint rotation; public uint scaling; public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering; public int targetAvailable; public uint statusFlags;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_INFO {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct POINTL { public int x; public int y; }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_2DREGION { public uint cx; public uint cy; }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO {
        public ulong pixelRate; public DISPLAYCONFIG_RATIONAL hSyncFreq; public DISPLAYCONFIG_RATIONAL vSyncFreq;
        public DISPLAYCONFIG_2DREGION activeSize; public DISPLAYCONFIG_2DREGION totalSize;
        public uint videoStandard; public uint scanLineOrdering;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_SOURCE_MODE { public uint width; public uint height; public uint pixelFormat; public POINTL position; }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_TARGET_MODE { public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo; }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECTL { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO { public POINTL PathSourceSize; public RECTL DesktopImageRegion; public RECTL DesktopImageClip; }
    public enum MODE_TYPE : uint { Source = 1, Target = 2, DesktopImage = 3 }
    [StructLayout(LayoutKind.Explicit)]
    public struct DISPLAYCONFIG_MODE_INFO {
        [FieldOffset(0)] public MODE_TYPE infoType;
        [FieldOffset(4)] public uint id;
        [FieldOffset(8)] public LUID adapterId;
        [FieldOffset(16)] public DISPLAYCONFIG_TARGET_MODE targetMode;
        [FieldOffset(16)] public DISPLAYCONFIG_SOURCE_MODE sourceMode;
        [FieldOffset(16)] public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_DEVICE_INFO_HEADER { public int type; public int size; public LUID adapterId; public uint id; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAYCONFIG_TARGET_DEVICE_NAME {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags; public int outputTechnology;
        public ushort edidManufactureId; public ushort edidProductCodeId; public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_TARGET_PREFERRED_MODE {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint width; public uint height;
        public DISPLAYCONFIG_TARGET_MODE targetMode;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    public class Result {
        public bool Ok;
        public bool RolledBack;
        public string Message;
        public int ApplyRc;
        public uint HubDeskW, HubDeskH;
        public double HubHz, HubSignalHz;
        public uint HubSignalW, HubSignalH;
        public uint SamsungW, SamsungH;
        public double SamsungHz;
        public bool SamsungOk;
        public bool HubDesktopGood;
        public bool MappingOk;
        public string MappingNote;
        public string MappingDetails;
        public LiveMonitor[] Monitors;
    }

    /// <summary>One currently attached desktop path. Name is EDID/friendly; size is GDI desktop, not MST signal.</summary>
    public class LiveMonitor {
        public string Name;
        public uint DeskW, DeskH;
        public double Hz;
        public bool Ok;
        public bool IsHub;
        public int PosX, PosY;
    }

    public class Snapshot {
        public DISPLAYCONFIG_PATH_INFO[] Paths;
        public DISPLAYCONFIG_MODE_INFO[] Modes;
        public uint PathCount;
        public uint ModeCount;
    }

    static string Dir() {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HubFix");
    }

    public static string LogPath() {
        return Path.Combine(Dir(), "hub-button.log");
    }

    public static string StatusPath() {
        return Path.Combine(Dir(), "hub-button-status.txt");
    }

    public static void Log(string line) {
        try {
            Directory.CreateDirectory(Dir());
            File.AppendAllText(LogPath(), DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + " " + line + "\r\n");
        } catch { }
    }

    public static bool IsAdmin() {
        try {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal p = new WindowsPrincipal(id);
            return p.IsInRole(WindowsBuiltInRole.Administrator);
        } catch { return false; }
    }

    static void TargetIdentity(DISPLAYCONFIG_PATH_INFO p, out string friendly, out string devicePath) {
        DISPLAYCONFIG_TARGET_DEVICE_NAME n = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
        n.header.type = 2;
        n.header.size = Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME));
        n.header.adapterId = p.targetInfo.adapterId;
        n.header.id = p.targetInfo.id;
        IntPtr m = Marshal.AllocHGlobal(n.header.size);
        try {
            Marshal.StructureToPtr(n, m, false);
            DisplayConfigGetDeviceInfo(m);
            n = (DISPLAYCONFIG_TARGET_DEVICE_NAME)Marshal.PtrToStructure(m, typeof(DISPLAYCONFIG_TARGET_DEVICE_NAME));
            friendly = n.monitorFriendlyDeviceName == null ? "" : n.monitorFriendlyDeviceName;
            devicePath = n.monitorDevicePath == null ? "" : n.monitorDevicePath;
        } finally { Marshal.FreeHGlobal(m); }
    }

    static string Tgt(DISPLAYCONFIG_PATH_INFO p) {
        string friendly, path;
        TargetIdentity(p, out friendly, out path);
        return friendly;
    }

    public static string ShortMonitorName(string name) {
        if (name == null || name.Length == 0) return "Monitor";
        if (name.IndexOf("Surface Hub", StringComparison.OrdinalIgnoreCase) >= 0) return "Hub";
        if (name.IndexOf("Samsung", StringComparison.OrdinalIgnoreCase) >= 0) return "Samsung";
        if (name.IndexOf("LS24", StringComparison.OrdinalIgnoreCase) >= 0) return "Samsung";
        return name;
    }

    static bool LooksLikeHub(string name, string devicePath) {
        if (name != null && name.IndexOf("Surface Hub", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (devicePath != null && (devicePath.IndexOf("PPX0084", StringComparison.OrdinalIgnoreCase) >= 0
            || devicePath.IndexOf("UID264", StringComparison.OrdinalIgnoreCase) >= 0)) return true;
        return false;
    }

    static bool LooksLikeSamsung(string name) {
        if (name == null) return false;
        return name.IndexOf("LS24", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Samsung", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static string Src(DISPLAYCONFIG_PATH_INFO p) {
        DISPLAYCONFIG_SOURCE_DEVICE_NAME n = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
        n.header.type = 1;
        n.header.size = Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME));
        n.header.adapterId = p.sourceInfo.adapterId;
        n.header.id = p.sourceInfo.id;
        IntPtr mem = Marshal.AllocHGlobal(n.header.size);
        try {
            Marshal.StructureToPtr(n, mem, false);
            DisplayConfigGetDeviceInfo(mem);
            n = (DISPLAYCONFIG_SOURCE_DEVICE_NAME)Marshal.PtrToStructure(mem, typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME));
            return n.viewGdiDeviceName == null ? "" : n.viewGdiDeviceName;
        } finally { Marshal.FreeHGlobal(mem); }
    }

    static bool Preferred(DISPLAYCONFIG_PATH_INFO p, out uint w, out uint h, out DISPLAYCONFIG_TARGET_MODE tm) {
        DISPLAYCONFIG_TARGET_PREFERRED_MODE n = new DISPLAYCONFIG_TARGET_PREFERRED_MODE();
        n.header.type = 3;
        n.header.size = Marshal.SizeOf(typeof(DISPLAYCONFIG_TARGET_PREFERRED_MODE));
        n.header.adapterId = p.targetInfo.adapterId;
        n.header.id = p.targetInfo.id;
        IntPtr mem = Marshal.AllocHGlobal(n.header.size);
        try {
            Marshal.StructureToPtr(n, mem, false);
            int rc = DisplayConfigGetDeviceInfo(mem);
            n = (DISPLAYCONFIG_TARGET_PREFERRED_MODE)Marshal.PtrToStructure(mem, typeof(DISPLAYCONFIG_TARGET_PREFERRED_MODE));
            w = n.width; h = n.height; tm = n.targetMode;
            return rc == 0 && w > 0;
        } finally { Marshal.FreeHGlobal(mem); }
    }

    static double Hz(DISPLAYCONFIG_RATIONAL r) {
        if (r.Denominator == 0) return 0;
        return (double)r.Numerator / r.Denominator;
    }

    static int QueryAll(out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes, out uint np, out uint nm) {
        int rc = GetDisplayConfigBufferSizes(QDC_ALL_PATHS, out np, out nm);
        paths = new DISPLAYCONFIG_PATH_INFO[np];
        modes = new DISPLAYCONFIG_MODE_INFO[nm];
        if (rc != 0) return rc;
        return QueryDisplayConfig(QDC_ALL_PATHS, ref np, paths, ref nm, modes, IntPtr.Zero);
    }

    public static Snapshot Capture() {
        Snapshot s = new Snapshot();
        uint np, nm;
        QueryAll(out s.Paths, out s.Modes, out np, out nm);
        s.PathCount = np;
        s.ModeCount = nm;
        return s;
    }

    static void FillView(Snapshot s, Result r) {
        r.SamsungOk = false;
        r.HubDesktopGood = false;
        r.SamsungW = 0; r.SamsungH = 0; r.SamsungHz = 0;
        r.HubDeskW = 0; r.HubDeskH = 0; r.HubHz = 0;
        r.HubSignalW = 0; r.HubSignalH = 0; r.HubSignalHz = 0;
        List<LiveMonitor> live = new List<LiveMonitor>();
        Dictionary<string, int> seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (s.Paths != null) {
            for (int i = 0; i < s.PathCount; i++) {
                DISPLAYCONFIG_PATH_INFO p = s.Paths[i];
                if ((p.flags & PATH_ACTIVE) == 0) continue;
                string name, devicePath;
                TargetIdentity(p, out name, out devicePath);
                uint si = p.sourceInfo.modeInfoIdx;
                uint ti = p.targetInfo.modeInfoIdx;
                uint dw = 0, dh = 0;
                int posX = 0, posY = 0;
                if (si < s.ModeCount && s.Modes[si].infoType == MODE_TYPE.Source) {
                    dw = s.Modes[si].sourceMode.width;
                    dh = s.Modes[si].sourceMode.height;
                    posX = s.Modes[si].sourceMode.position.x;
                    posY = s.Modes[si].sourceMode.position.y;
                }
                uint sw = 0, sh = 0; double shz = 0;
                if (ti < s.ModeCount && s.Modes[ti].infoType == MODE_TYPE.Target) {
                    DISPLAYCONFIG_VIDEO_SIGNAL_INFO v = s.Modes[ti].targetMode.targetVideoSignalInfo;
                    sw = v.activeSize.cx; sh = v.activeSize.cy; shz = Hz(v.vSyncFreq);
                }
                double phz = Hz(p.targetInfo.refreshRate);
                if (name.IndexOf("LS24", StringComparison.OrdinalIgnoreCase) >= 0) {
                    r.SamsungW = dw; r.SamsungH = dh;
                    r.SamsungHz = phz > 0 ? phz : shz;
                    r.SamsungOk = dw > 0 && dw <= SAMSUNG_MAX && dh <= SAMSUNG_MAX;
                }
                if (name.IndexOf("Surface Hub", StringComparison.OrdinalIgnoreCase) >= 0) {
                    r.HubDeskW = dw; r.HubDeskH = dh;
                    r.HubHz = phz > shz ? phz : shz;
                    r.HubSignalW = sw; r.HubSignalH = sh; r.HubSignalHz = shz;
                    r.HubDesktopGood = dw == HUB_DESK_W && dh == HUB_DESK_H && r.HubHz >= 100.0;
                }
                string gdi = Src(p);
                string key = gdi.Length > 0 ? gdi : (p.targetInfo.adapterId.LowPart.ToString("X") + ":" + p.targetInfo.id.ToString());
                LiveMonitor lm = new LiveMonitor();
                lm.Name = ShortMonitorName(name);
                lm.DeskW = dw;
                lm.DeskH = dh;
                lm.Hz = phz > 0 ? phz : shz;
                lm.IsHub = LooksLikeHub(name, devicePath);
                lm.PosX = posX;
                lm.PosY = posY;
                if (lm.IsHub)
                    lm.Ok = dw == HUB_DESK_W && dh == HUB_DESK_H && lm.Hz >= 100.0;
                else if (LooksLikeSamsung(name))
                    lm.Ok = dw > 0 && dw <= SAMSUNG_MAX && dh <= SAMSUNG_MAX;
                else
                    lm.Ok = dw > 0 && dh > 0;
                if (seen.ContainsKey(key)) {
                    int idx = seen[key];
                    ulong prev = (ulong)live[idx].DeskW * live[idx].DeskH;
                    ulong next = (ulong)lm.DeskW * lm.DeskH;
                    if (lm.IsHub || next > prev) live[idx] = lm;
                } else {
                    seen[key] = live.Count;
                    live.Add(lm);
                }
            }
        }
        if (live.Count == 0) FillFromGdi(live);
        live.Sort(CompareLiveMonitor);
        r.Monitors = live.ToArray();
    }

    static int CompareLiveMonitor(LiveMonitor a, LiveMonitor b) {
        int c = a.PosX.CompareTo(b.PosX);
        if (c != 0) return c;
        return a.PosY.CompareTo(b.PosY);
    }

    const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
    const int ENUM_CURRENT_SETTINGS = -1;

    static void FillFromGdi(List<LiveMonitor> live) {
        DISPLAY_DEVICE adapter = NewDisplayDevice();
        for (uint i = 0; EnumDisplayDevices(null, i, ref adapter, 1); i++) {
            if ((adapter.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) {
                adapter = NewDisplayDevice();
                continue;
            }
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            uint dw = 0, dh = 0;
            double hz = 0;
            int posX = 0, posY = 0;
            if (EnumDisplaySettings(adapter.DeviceName, ENUM_CURRENT_SETTINGS, ref dm)) {
                dw = (uint)dm.dmPelsWidth;
                dh = (uint)dm.dmPelsHeight;
                hz = dm.dmDisplayFrequency;
                posX = dm.dmPositionX;
                posY = dm.dmPositionY;
            }
            string monName = "";
            string monId = "";
            DISPLAY_DEVICE mon = NewDisplayDevice();
            if (EnumDisplayDevices(adapter.DeviceName, 0, ref mon, 1)) {
                monName = mon.DeviceString == null ? "" : mon.DeviceString;
                monId = mon.DeviceID == null ? "" : mon.DeviceID;
            }
            if (monName.Length == 0) monName = adapter.DeviceString == null ? "" : adapter.DeviceString;
            LiveMonitor lm = new LiveMonitor();
            lm.Name = ShortMonitorName(monName);
            lm.DeskW = dw;
            lm.DeskH = dh;
            lm.Hz = hz;
            lm.IsHub = LooksLikeHub(monName, monId);
            lm.PosX = posX;
            lm.PosY = posY;
            if (lm.IsHub)
                lm.Ok = dw == HUB_DESK_W && dh == HUB_DESK_H && lm.Hz >= 100.0;
            else if (LooksLikeSamsung(monName))
                lm.Ok = dw > 0 && dw <= SAMSUNG_MAX && dh <= SAMSUNG_MAX;
            else
                lm.Ok = dw > 0 && dh > 0;
            live.Add(lm);
            adapter = NewDisplayDevice();
        }
    }

    static DISPLAY_DEVICE NewDisplayDevice() {
        DISPLAY_DEVICE d = new DISPLAY_DEVICE();
        d.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
        return d;
    }

    public static bool IsProper(Result r) {
        if (r == null || !r.HubDesktopGood) return false;
        if (r.SamsungW == 0 && r.SamsungH == 0) return true;
        return r.SamsungOk;
    }

    public static string Describe(Result r) {
        if (r == null) return "(no status)";
        StringBuilder sb = new StringBuilder();
        sb.Append("Samsung ");
        sb.Append(r.SamsungW); sb.Append("x"); sb.Append(r.SamsungH);
        sb.Append(r.SamsungOk ? " OK" : " BAD");
        sb.Append(" | Hub desktop ");
        sb.Append(r.HubDeskW); sb.Append("x"); sb.Append(r.HubDeskH);
        sb.Append(" @ "); sb.Append(r.HubHz.ToString("0.##")); sb.Append("Hz");
        sb.Append(r.HubDesktopGood ? " OK" : " BAD");
        sb.Append(" | signal ");
        sb.Append(r.HubSignalW); sb.Append("x"); sb.Append(r.HubSignalH);
        sb.Append(" @ "); sb.Append(r.HubSignalHz.ToString("0.##")); sb.Append("Hz");
        sb.Append(" | monitors="); sb.Append(GetSystemMetrics(SM_CMONITORS));
        return sb.ToString();
    }

    public static string StatusSheet(Result r) {
        if (r == null) return "(no status)";
        StringBuilder sb = new StringBuilder();
        sb.Append("Samsung  ");
        sb.Append(Px(r.SamsungW, r.SamsungH));
        sb.Append("  ");
        sb.Append(HzText(r.SamsungHz));
        sb.Append(r.SamsungW == 0 ? "" : (r.SamsungOk ? "  OK" : "  BAD"));
        sb.AppendLine();
        sb.Append("Hub      ");
        sb.Append(Px(r.HubDeskW, r.HubDeskH));
        sb.Append("  ");
        sb.Append(HzText(r.HubHz));
        sb.Append(r.HubDeskW == 0 ? "" : (r.HubDesktopGood ? "  OK" : "  BAD"));
        sb.AppendLine();
        sb.Append("Signal   ");
        sb.Append(Px(r.HubSignalW, r.HubSignalH));
        sb.Append("  ");
        sb.Append(HzText(r.HubSignalHz));
        return sb.ToString();
    }

    public static string Px(uint w, uint h) {
        if (w == 0 && h == 0) return "—";
        return w.ToString() + "\u00D7" + h.ToString();
    }

    public static string HzText(double hz) {
        if (hz <= 0) return "—";
        return hz.ToString("0.##") + " Hz";
    }

    public static string PathStatus(uint w, bool ok) {
        if (w == 0) return "—";
        return ok ? "OK" : "BAD";
    }

    public static string SignalFootnote(Result r) {
        if (r == null) return "";
        if (r.HubSignalW == 0 && r.HubSignalH == 0) return "";
        return "Signal " + Px(r.HubSignalW, r.HubSignalH) + "  " + HzText(r.HubSignalHz) + "  (MST tile; desktop is 4K)";
    }

    public static string UiStatus(string lead, Result r) {
        string sheet = StatusSheet(r);
        if (lead == null || lead.Length == 0) return sheet;
        return lead + "\r\n" + sheet;
    }

    public static Result CurrentStatus() {
        Result r = new Result();
        Snapshot s = Capture();
        FillView(s, r);
        r.Ok = IsProper(r);
        r.Message = Describe(r);
        try { File.WriteAllText(StatusPath(), r.Message + "\r\n"); } catch { }
        return r;
    }

    static int SetCfg(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes, uint extra, bool apply) {
        uint nm = (uint)(modes == null ? 0 : modes.Length);
        uint flags = SDC_USE_SUPPLIED | SDC_ALLOW_CHANGES | extra;
        int v = SetDisplayConfig((uint)paths.Length, paths, nm, modes, flags | SDC_VALIDATE);
        Log("VALIDATE extra=0x" + extra.ToString("X") + " rc=" + v);
        if (v != 0) return v;
        if (!apply) return 0;
        int a = SetDisplayConfig((uint)paths.Length, paths, nm, modes, flags | SDC_APPLY | SDC_SAVE);
        Log("APPLY extra=0x" + extra.ToString("X") + " rc=" + a);
        return a;
    }

    static int Restore(Snapshot snap) {
        if (snap == null || snap.Paths == null) return 99;
        int nActive = 0;
        for (int i = 0; i < snap.PathCount; i++) {
            if ((snap.Paths[i].flags & PATH_ACTIVE) != 0) nActive++;
        }
        DISPLAYCONFIG_PATH_INFO[] active = new DISPLAYCONFIG_PATH_INFO[nActive];
        int k = 0;
        for (int i = 0; i < snap.PathCount; i++) {
            if ((snap.Paths[i].flags & PATH_ACTIVE) != 0) active[k++] = snap.Paths[i];
        }
        DISPLAYCONFIG_MODE_INFO[] modes = new DISPLAYCONFIG_MODE_INFO[snap.ModeCount];
        Array.Copy(snap.Modes, modes, (int)snap.ModeCount);
        int rc = SetCfg(active, modes, 0, true);
        if (rc != 0) rc = SetCfg(active, modes, SDC_NO_OPTIMIZATION, true);
        Log("RESTORE rc=" + rc);
        return rc;
    }

    static bool FindSamsungHub(Snapshot s, out int sIdx, out int hIdx) {
        sIdx = -1; hIdx = -1;
        int hubSid0 = -1;
        for (int i = 0; i < s.PathCount; i++) {
            DISPLAYCONFIG_PATH_INFO p = s.Paths[i];
            string n = Tgt(p);
            string dpath;
            string friendly;
            TargetIdentity(p, out friendly, out dpath);
            if (n.IndexOf("LS24", StringComparison.OrdinalIgnoreCase) >= 0 && (p.flags & PATH_ACTIVE) != 0)
                sIdx = i;
            bool hub = LooksLikeHub(friendly, dpath);
            if (hub && p.targetInfo.targetAvailable != 0) {
                if (hIdx < 0) hIdx = i;
                if (p.targetInfo.id == HUB_TARGET_ID && p.sourceInfo.id == 0) hIdx = i;
                if (p.sourceInfo.id == 0 && hubSid0 < 0) hubSid0 = i;
            }
        }
        if (hIdx < 0 && hubSid0 >= 0) hIdx = hubSid0;
        return hIdx >= 0;
    }

    static bool GuardOrRollback(Snapshot before, Result r, string why, bool samsungWasPresent) {
        Snapshot after = Capture();
        FillView(after, r);
        if (GetSystemMetrics(SM_CMONITORS) < 1 || r.HubDeskW == 0) {
            Log("ROLLBACK Hub image lost after " + why);
            Restore(before);
            Snapshot again = Capture();
            FillView(again, r);
            r.RolledBack = true;
            r.Ok = false;
            r.Message = "Rolled back: Hub image dropped. " + Describe(r);
            return false;
        }
        if (samsungWasPresent && !r.SamsungOk) {
            Log("ROLLBACK samsung lost after " + why);
            Restore(before);
            Snapshot again = Capture();
            FillView(again, r);
            r.RolledBack = true;
            r.Ok = false;
            r.Message = "Rolled back: Samsung path dropped. " + Describe(r);
            return false;
        }
        if (r.HubDeskW == 960) {
            Log("ROLLBACK 960 desktop after " + why);
            Restore(before);
            Snapshot again = Capture();
            FillView(again, r);
            r.RolledBack = true;
            r.Ok = false;
            r.Message = "Rolled back: refused 960 desktop (tile is signal, not desktop). " + Describe(r);
            return false;
        }
        return true;
    }

    static string ClassesToDosDevice(string classesName) {
        if (classesName == null) return null;
        if (classesName.StartsWith("##?#", StringComparison.Ordinal))
            return @"\\?\" + classesName.Substring(4);
        return classesName;
    }

    static List<string> DeviceInterfacePaths(string ifaceGuid, string mustContain, string uidMustContain) {
        List<string> list = new List<string>();
        string path = @"SYSTEM\CurrentControlSet\Control\DeviceClasses\" + ifaceGuid;
        try {
            using (RegistryKey root = Registry.LocalMachine.OpenSubKey(path)) {
                if (root == null) return list;
                string[] names = root.GetSubKeyNames();
                for (int i = 0; i < names.Length; i++) {
                    string n = names[i];
                    if (mustContain != null && n.IndexOf(mustContain, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (uidMustContain != null && n.IndexOf(uidMustContain, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    string dos = ClassesToDosDevice(n);
                    if (dos != null && dos.Length > 0) list.Add(dos);
                }
            }
        } catch (Exception ex) { Log("DeviceInterfacePaths EX " + ex.Message); }
        return list;
    }

    static string HubMonitorDosPath() {
        List<string> m = DeviceInterfacePaths(MonIfaceGuid, HubMonitorToken, HubUid264Token);
        for (int i = 0; i < m.Count; i++) {
            if (m[i].IndexOf("UID264", StringComparison.OrdinalIgnoreCase) >= 0 &&
                m[i].IndexOf("PPX0084", StringComparison.OrdinalIgnoreCase) >= 0)
                return m[i];
        }
        return m.Count > 0 ? m[0] : null;
    }

    static List<string> HubDigitizerDosPaths() {
        return DeviceInterfacePaths(HidIfaceGuid, HubHidVidPid, null);
    }

    static string HidDosToInstanceId(string hidDos) {
        if (hidDos == null) return null;
        string s = hidDos;
        if (s.StartsWith(@"\\?\", StringComparison.Ordinal)) s = s.Substring(4);
        int g = s.LastIndexOf("#{");
        if (g >= 0) s = s.Substring(0, g);
        return s.Replace('#', '\\');
    }

    public static bool DigimonMapsToHub() {
        List<string> hids = HubDigitizerDosPaths();
        if (hids.Count == 0) return false;
        try {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(DigimonKeyPath)) {
                if (key == null) return false;
                int mapped = 0;
                for (int i = 0; i < hids.Count; i++) {
                    object v = key.GetValue("20-" + hids[i]);
                    string s = v as string;
                    if (s != null && s.IndexOf("UID264", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        s.IndexOf("PPX0084", StringComparison.OrdinalIgnoreCase) >= 0)
                        mapped++;
                }
                return mapped >= hids.Count;
            }
        } catch { return false; }
    }

    static void NotifyDigitizerMappingChanged() {
        try {
            SendMessageTimeout((IntPtr)0xffff, WM_SETTINGCHANGE, IntPtr.Zero,
                "TabletPCDigitizerMappingChanged", SMTO_ABORTIFHUNG, 200, IntPtr.Zero);
        } catch (Exception ex) { Log("NotifyDigitizerMappingChanged EX " + ex.Message); }
    }

    /// <summary>
    /// Map Hub HID digitizer/pens to UID264, never Samsung.
    /// Not called from GentleHandshake — mapping stays on its own button.
    /// </summary>
    public static Result BindDigitizerToHub() {
        Result r = CurrentStatus();
        string monitor = HubMonitorDosPath();
        List<string> hids = HubDigitizerDosPaths();
        if (monitor == null) {
            r.MappingOk = false;
            r.MappingNote = "No UID264 monitor interface.";
            r.Message = "No UID264 monitor interface. Mapping was not applied.";
            r.MappingDetails = TouchMappingDetails();
            return r;
        }
        if (hids.Count == 0) {
            r.MappingOk = false;
            r.MappingNote = "Hub touch HID missing (USB cable?).";
            r.Message = "Hub touch HID missing (USB cable?). Mapping was not applied.";
            r.MappingDetails = TouchMappingDetails();
            return r;
        }
        if (monitor.IndexOf("SAM7435", StringComparison.OrdinalIgnoreCase) >= 0) {
            r.MappingOk = false;
            r.MappingNote = "Refused: would map touch to Samsung.";
            r.Message = "Refused: would map touch to Samsung.";
            r.MappingDetails = "Monitor DOS: " + monitor + "\r\n\r\n" + TouchMappingDetails();
            return r;
        }
        Log("BIND monitor=" + monitor + " hidCount=" + hids.Count);
        if (DigimonMapsToHub()) {
            NotifyDigitizerMappingChanged();
            r.MappingOk = true;
            r.MappingNote = "Digimon already UID264; notified input stack.";
            r.Ok = true;
            r.Message = MapTouchSuccessCopy();
            r.MappingDetails = r.MappingNote + "\r\nFirePro not restarted.\r\n\r\n" + TouchMappingDetails();
            return r;
        }
        if (!IsAdmin()) {
            r.MappingOk = false;
            r.MappingNote = "NEED_ADMIN";
            r.Message = "Touch mapping needs one UAC. Does not restart FirePro.";
            r.MappingDetails = "HKLM write to Wisp\\Pen\\Digimon requires Administrator.\r\n\r\n" + TouchMappingDetails();
            return r;
        }
        try {
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(DigimonKeyPath)) {
                if (key == null) {
                    r.MappingOk = false;
                    r.Message = "Cannot open HKLM Wisp\\Pen\\Digimon.";
                    r.MappingDetails = TouchMappingDetails();
                    return r;
                }
                string[] existing = key.GetValueNames();
                for (int i = 0; i < existing.Length; i++) {
                    if (existing[i].IndexOf("VID_2465&PID_6512", StringComparison.OrdinalIgnoreCase) >= 0)
                        key.DeleteValue(existing[i], false);
                }
                for (int i = 0; i < hids.Count; i++)
                    key.SetValue("20-" + hids[i], monitor, RegistryValueKind.String);
            }
        } catch (Exception ex) {
            r.MappingOk = false;
            r.Message = "Digimon write failed: " + ex.Message;
            r.MappingDetails = TouchMappingDetails();
            Log(r.Message);
            return r;
        }
        NotifyDigitizerMappingChanged();
        for (int i = 0; i < hids.Count; i++) {
            string inst = HidDosToInstanceId(hids[i]);
            if (inst == null) continue;
            RunHidden("pnputil.exe", "/restart-device \"" + inst + "\"", 15000);
        }
        r.MappingOk = true;
        r.Ok = r.SamsungOk;
        r.MappingNote = "Mapped " + hids.Count + " Hub HID collection(s) to UID264.";
        r.Message = MapTouchSuccessCopy();
        r.MappingDetails = r.MappingNote + "\r\nFirePro not restarted. HID collections restarted via pnputil (not GPU).\r\n\r\n" + TouchMappingDetails();
        Log(r.MappingNote + " " + Describe(r));
        return r;
    }

    public static string MapTouchSuccessCopy() {
        return "Mapped Hub touch to UID264. FirePro not restarted.\r\n\r\nTap the 84-inch: cursor on Hub 4K = success.";
    }

    public static string MapTouchConfirmCopy() {
        return "Touch USB is fine; Windows mapped it to the wrong screen.\r\n\r\n" +
            "Maps Hub HID to the 84-inch (UID264). Does not restart FirePro.\r\n\r\n" +
            "OK to apply. Then tap the Hub.";
    }

    /// <summary>CCD apply of known-good 4K120 Hub desktop. Samsung untouched. No adapter disable. Does not change touch mapping.</summary>
    public static Result GentleHandshake() {
        Result r = new Result();
        Directory.CreateDirectory(Dir());
        Log("GENTLE begin");
        Snapshot before = Capture();
        FillView(before, r);
        Log("before " + Describe(r));
        int sIdx, hIdx;
        if (!FindSamsungHub(before, out sIdx, out hIdx)) {
            r.Ok = false;
            r.Message = "Hub path not found. " + Describe(r);
            Log(r.Message);
            return r;
        }
        bool samsungWasPresent = sIdx >= 0;

        DISPLAYCONFIG_PATH_INFO hub = before.Paths[hIdx];
        uint pw, ph;
        DISPLAYCONFIG_TARGET_MODE pref;
        bool hasPref = Preferred(hub, out pw, out ph, out pref);
        Log("Hub tgt=" + hub.targetInfo.id + " src=" + Src(hub) + " samsung=" + sIdx + " pref=" + (hasPref ? (pw + "x" + ph) : "none"));

        hub.flags = PATH_ACTIVE;
        hub.sourceInfo.statusFlags = 1;
        hub.targetInfo.statusFlags = 1;
        hub.targetInfo.rotation = ROTATION_IDENTITY;
        hub.targetInfo.scaling = SCALING_PREFERRED;
        hub.targetInfo.refreshRate.Numerator = HUB_HZ;
        hub.targetInfo.refreshRate.Denominator = 1;
        if (hasPref && ((double)pref.targetVideoSignalInfo.vSyncFreq.Numerator / Math.Max(pref.targetVideoSignalInfo.vSyncFreq.Denominator, 1)) >= 100.0)
            hub.targetInfo.refreshRate = pref.targetVideoSignalInfo.vSyncFreq;

        DISPLAYCONFIG_PATH_INFO[] want;
        DISPLAYCONFIG_MODE_INFO[] modes;
        if (samsungWasPresent) {
            DISPLAYCONFIG_PATH_INFO samsung = before.Paths[sIdx];
            uint sw = 1280, sh = 720, spf = PIXELFORMAT_32BPP;
            uint ssi = samsung.sourceInfo.modeInfoIdx;
            if (ssi < before.ModeCount && before.Modes[ssi].infoType == MODE_TYPE.Source) {
                sw = before.Modes[ssi].sourceMode.width;
                sh = before.Modes[ssi].sourceMode.height;
                spf = before.Modes[ssi].sourceMode.pixelFormat;
            }
            DISPLAYCONFIG_TARGET_MODE samTgt = new DISPLAYCONFIG_TARGET_MODE();
            uint sti = samsung.targetInfo.modeInfoIdx;
            if (sti < before.ModeCount && before.Modes[sti].infoType == MODE_TYPE.Target)
                samTgt = before.Modes[sti].targetMode;
            samsung.flags = PATH_ACTIVE;
            samsung.sourceInfo.statusFlags = 1;
            modes = new DISPLAYCONFIG_MODE_INFO[4];
            modes[0].infoType = MODE_TYPE.Source;
            modes[0].id = samsung.sourceInfo.id;
            modes[0].adapterId = samsung.sourceInfo.adapterId;
            modes[0].sourceMode.width = sw;
            modes[0].sourceMode.height = sh;
            modes[0].sourceMode.pixelFormat = spf;
            modes[1].infoType = MODE_TYPE.Target;
            modes[1].id = samsung.targetInfo.id;
            modes[1].adapterId = samsung.targetInfo.adapterId;
            modes[1].targetMode = samTgt;
            modes[2].infoType = MODE_TYPE.Source;
            modes[2].id = hub.sourceInfo.id;
            modes[2].adapterId = hub.sourceInfo.adapterId;
            modes[2].sourceMode.width = HUB_DESK_W;
            modes[2].sourceMode.height = HUB_DESK_H;
            modes[2].sourceMode.pixelFormat = PIXELFORMAT_32BPP;
            modes[2].sourceMode.position.x = (int)sw;
            modes[3].infoType = MODE_TYPE.Target;
            modes[3].id = hub.targetInfo.id;
            modes[3].adapterId = hub.targetInfo.adapterId;
            if (hasPref) modes[3].targetMode = pref;
            samsung.sourceInfo.modeInfoIdx = 0;
            samsung.targetInfo.modeInfoIdx = 1;
            hub.sourceInfo.modeInfoIdx = 2;
            hub.targetInfo.modeInfoIdx = 3;
            want = new DISPLAYCONFIG_PATH_INFO[] { samsung, hub };
        } else {
            modes = new DISPLAYCONFIG_MODE_INFO[2];
            modes[0].infoType = MODE_TYPE.Source;
            modes[0].id = hub.sourceInfo.id;
            modes[0].adapterId = hub.sourceInfo.adapterId;
            modes[0].sourceMode.width = HUB_DESK_W;
            modes[0].sourceMode.height = HUB_DESK_H;
            modes[0].sourceMode.pixelFormat = PIXELFORMAT_32BPP;
            modes[1].infoType = MODE_TYPE.Target;
            modes[1].id = hub.targetInfo.id;
            modes[1].adapterId = hub.targetInfo.adapterId;
            if (hasPref) modes[1].targetMode = pref;
            hub.sourceInfo.modeInfoIdx = 0;
            hub.targetInfo.modeInfoIdx = 1;
            want = new DISPLAYCONFIG_PATH_INFO[] { hub };
        }

        int rc = SetCfg(want, modes, 0, true);
        if (rc != 0) rc = SetCfg(want, modes, SDC_NO_OPTIMIZATION, true);

        if (rc != 0) {
            Log("explicit 4K120 failed, CcdApplyGood-style invalid idx");
            DISPLAYCONFIG_PATH_INFO hubInv = before.Paths[hIdx];
            hubInv.flags = PATH_ACTIVE;
            hubInv.sourceInfo.statusFlags = 1;
            hubInv.targetInfo.statusFlags = 1;
            hubInv.sourceInfo.modeInfoIdx = MODE_INVALID;
            hubInv.targetInfo.modeInfoIdx = MODE_INVALID;
            if (samsungWasPresent)
                want = new DISPLAYCONFIG_PATH_INFO[] { before.Paths[sIdx], hubInv };
            else
                want = new DISPLAYCONFIG_PATH_INFO[] { hubInv };
            rc = SetCfg(want, before.Modes, 0, true);
            if (rc != 0) rc = SetCfg(want, before.Modes, SDC_NO_OPTIMIZATION, true);
        }

        r.ApplyRc = rc;
        if (rc != 0) {
            r.Ok = false;
            FillView(Capture(), r);
            r.Message = "CCD apply failed rc=" + rc + ". " + Describe(r);
            Log(r.Message);
            return r;
        }

        if (!GuardOrRollback(before, r, "gentle-apply", samsungWasPresent)) return r;

        if (!r.HubDesktopGood) {
            Log("still not 4K120, force-enum then retry explicit");
            SetCfg(want, modes, SDC_FORCE_MODE_ENUMERATION, true);
            if (!GuardOrRollback(before, r, "force-enum", samsungWasPresent)) return r;
            rc = SetCfg(want, modes, 0, true);
            r.ApplyRc = rc;
            if (rc == 0) {
                if (!GuardOrRollback(before, r, "post-enum-120", samsungWasPresent)) return r;
            } else {
                FillView(Capture(), r);
            }
        }

        r.Ok = IsProper(r);
        r.Message = (r.Ok ? "Gentle handshake applied. " : "Gentle handshake ran; Hub not fully 4K120. Hot retraining may be needed. ") + Describe(r);
        Log(r.Message);
        try { File.WriteAllText(StatusPath(), r.Message + "\r\n"); } catch { }
        return r;
    }

    static int RunHidden(string file, string args, int waitMs) {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = file;
        psi.Arguments = args;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        try {
            Process p = Process.Start(psi);
            if (p == null) return -1;
            if (!p.WaitForExit(waitMs)) {
                try { p.Kill(); } catch { }
                return -2;
            }
            string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            if (o.Length > 0) Log(file + " " + o.Replace("\r", " ").Replace("\n", " "));
            return p.ExitCode;
        } catch (Exception ex) {
            Log("RunHidden " + file + " EX " + ex.Message);
            return -3;
        }
    }

    /// <summary>Live FirePro adapter restart then gentle CCD. Can TDR / glitch 3D. Needs admin.</summary>
    public static Result HardRetrain() {
        Result r = new Result();
        if (!IsAdmin()) {
            r.Ok = false;
            r.Message = "Hot retraining needs Administrator (UAC). FirePro restart was not run.";
            Log(r.Message);
            return r;
        }
        Snapshot before = Capture();
        FillView(before, r);
        Log("HARD begin " + Describe(r));
        string firePro = FindFireProInstance();
        if (firePro == null) {
            r.Ok = false;
            r.Message = "FirePro W7100 (" + FireProHardwareId + ") not found. Hot retraining was not run.";
            Log(r.Message);
            return r;
        }
        int rc = RunHidden("pnputil.exe", "/restart-device \"" + firePro + "\"", 60000);
        Log("pnputil restart-device rc=" + rc);
        Thread.Sleep(4000);
        RunHidden("pnputil.exe", "/scan-devices", 30000);
        Thread.Sleep(3000);
        Result g = GentleHandshake();
        g.Message = "Hot retraining (FirePro restart) done. " + g.Message;
        Log(g.Message);
        return g;
    }

    public static string InspectTouch() {
        return TouchMappingDetails();
    }

    public static string TouchMappingDetails() {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Touch/USB is a separate path from DP video/audio. Do not live-restart FirePro to fix touch.");
        sb.AppendLine("Hub monitor container != touch HID container — Windows may map taps to the Samsung (primary) after a mode change.");
        sb.AppendLine("Digitizer is not Error/Disabled. This is not a UID264 disabled-device bug.");
        sb.AppendLine();
        sb.AppendLine("PnP (present): 84\" Touch Device, HID-compliant touch screen, HEAT touch, pens — all OK.");
        sb.AppendLine("USB cable: VID_2465 PID_6512 enumerates (connected).");
        sb.AppendLine("Surface Hub USB Audio is OK (audio-over-USB; DP audio can also be up).");
        sb.AppendLine();
        sb.AppendLine("Failed USB (not the digitizer): Hub internal hub VID_045E PID_02F1 — Device Descriptor Request Failed on unused Hub ports (often camera), not touch.");
        try {
            uint n = 0;
            GetPointerDevices(ref n, IntPtr.Zero);
            sb.AppendLine();
            sb.AppendLine("GetPointerDevices count=" + n);
        } catch (Exception ex) {
            sb.AppendLine();
            sb.AppendLine("GetPointerDevices: " + ex.Message);
        }
        sb.AppendLine();
        sb.AppendLine("UAC / HKLM");
        sb.AppendLine("Admin=" + IsAdmin());
        sb.AppendLine("HKLM\\" + DigimonKeyPath);
        try {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(DigimonKeyPath)) {
                if (key == null) sb.AppendLine("(Digimon key missing)");
                else {
                    string[] names = key.GetValueNames();
                    if (names.Length == 0) sb.AppendLine("(no Digimon values)");
                    for (int i = 0; i < names.Length; i++)
                        sb.AppendLine(names[i] + " = " + key.GetValue(names[i]));
                }
            }
        } catch (Exception ex) {
            sb.AppendLine("Digimon read: " + ex.Message + " (may need UAC)");
        }
        sb.AppendLine();
        string mon = HubMonitorDosPath();
        sb.AppendLine("Monitor UID264 DOS:");
        sb.AppendLine(mon == null ? "(none)" : mon);
        List<string> hids = HubDigitizerDosPaths();
        sb.AppendLine("Hub HID collections (" + hids.Count + "), VID_2465&PID_6512:");
        if (hids.Count == 0) sb.AppendLine("(none)");
        for (int i = 0; i < hids.Count; i++)
            sb.AppendLine(hids[i]);
        sb.AppendLine();
        Result view = new Result();
        FillView(Capture(), view);
        sb.AppendLine("Monitor mode dump: " + Describe(view));
        return sb.ToString().TrimEnd();
    }

    /// <summary>Restart only the 84" touch USB composite. Not the GPU. Needs admin.</summary>
    public static Result UsbTouchRescan() {
        Result r = CurrentStatus();
        if (!IsAdmin()) {
            r.Ok = false;
            r.Message = "USB touch rescan needs Administrator. GPU was not touched. " + r.Message;
            return r;
        }
        string inst = FindTouchComposite();
        if (inst == null) {
            r.Ok = false;
            r.Message = "84\" touch USB composite not found. Cable may be unplugged. " + r.Message;
            return r;
        }
        Log("USB touch restart " + inst);
        int rc = RunHidden("pnputil.exe", "/restart-device \"" + inst + "\"", 30000);
        RunHidden("pnputil.exe", "/scan-devices", 20000);
        Thread.Sleep(1500);
        Result after = CurrentStatus();
        after.ApplyRc = rc;
        after.Ok = rc == 0 && after.SamsungOk;
        after.Message = "Touch USB restart rc=" + rc + " (FirePro not restarted). " + after.Message;
        Log(after.Message);
        return after;
    }

    static string FindFireProInstance() {
        string[] found = FindEnumInstances("PCI", FireProHardwareId);
        return found.Length > 0 ? found[0] : null;
    }

    static string FindTouchComposite() {
        string[] found = FindEnumInstances("USB", HubHidVidPid);
        for (int i = 0; i < found.Length; i++) {
            if (found[i].IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) < 0)
                return found[i];
        }
        return found.Length > 0 ? found[0] : null;
    }

    static string[] FindEnumInstances(string bus, string hardwareToken) {
        List<string> list = new List<string>();
        string path = @"SYSTEM\CurrentControlSet\Enum\" + bus;
        using (RegistryKey root = Registry.LocalMachine.OpenSubKey(path)) {
            if (root == null) return new string[0];
            string[] devices = root.GetSubKeyNames();
            for (int i = 0; i < devices.Length; i++) {
                if (devices[i].IndexOf(hardwareToken, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                using (RegistryKey dev = root.OpenSubKey(devices[i])) {
                    if (dev == null) continue;
                    string[] insts = dev.GetSubKeyNames();
                    for (int j = 0; j < insts.Length; j++)
                        list.Add(bus + "\\" + devices[i] + "\\" + insts[j]);
                }
            }
        }
        return list.ToArray();
    }

    public static void OpenTouchMappingWizard() {
        try {
            Process.Start("tabcal.exe");
        } catch {
            try { Process.Start("control.exe", "tabletpc.cpl"); } catch { }
        }
    }
}
