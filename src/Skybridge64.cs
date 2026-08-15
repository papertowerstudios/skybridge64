// ============================================================================
//  SKYBRIDGE 64
//  Macht aus einem N64-Controller einen Xbox-Controller mit der Steuerung von
//  des Vorbilds - fuer Wild Blue Skies. Doppelklick, fertig.
//
//  Kein Einrichten: Das Programm erkennt N64-Controller und -Adapter selbst,
//  waehlt die passende Belegung, meldet einen virtuellen Xbox-Pad an und
//  faengt an. Wird der Controller abgezogen, wartet es einfach wieder.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Skybridge64
{
    // ------------------------------------------------------------------ Windows

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct JOYCAPS
        {
            public ushort wMid; public ushort wPid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szPname;
            public uint wXmin, wXmax, wYmin, wYmax, wZmin, wZmax;
            public uint wNumButtons, wPeriodMin, wPeriodMax;
            public uint wRmin, wRmax, wUmin, wUmax, wVmin, wVmax;
            public uint wCaps, wMaxAxes, wNumAxes, wMaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szRegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szOEMVxD;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct JOYINFOEX
        {
            public uint dwSize, dwFlags, dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos;
            public uint dwButtons, dwButtonNumber, dwPOV, dwReserved1, dwReserved2;
        }

        public const uint JOY_RETURNALL = 0xFF;
        [DllImport("winmm.dll", CharSet = CharSet.Ansi)] public static extern uint joyGetDevCapsA(uint id, ref JOYCAPS caps, uint size);
        [DllImport("winmm.dll")] public static extern uint joyGetPosEx(uint id, ref JOYINFOEX info);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    }

    internal class RawState
    {
        public bool Ok;
        public int X = 32767, Y = 32767, Z = 32767, R = 32767, U = 32767, V = 32767;
        public uint Buttons;
        public uint Pov = 65535;

        public int Axis(int i)
        {
            switch (i) { case 0: return X; case 1: return Y; case 2: return Z; case 3: return R; case 4: return U; default: return V; }
        }
        public RawState Clone()
        {
            RawState s = new RawState();
            s.Ok = Ok; s.X = X; s.Y = Y; s.Z = Z; s.R = R; s.U = U; s.V = V;
            s.Buttons = Buttons; s.Pov = Pov;
            return s;
        }
    }

    // ------------------------------------------------------------------ Geraete

    internal class Pad
    {
        public int Id, Vid, Pid, Buttons, Axes;

        public string Kennung { get { return string.Format("{0:X4}:{1:X4}", Vid, Pid); } }

        public static List<Pad> Alle()
        {
            List<Pad> l = new List<Pad>();
            for (uint id = 0; id < 16; id++)
            {
                Native.JOYCAPS c = new Native.JOYCAPS();
                if (Native.joyGetDevCapsA(id, ref c, (uint)Marshal.SizeOf(typeof(Native.JOYCAPS))) != 0) continue;
                Native.JOYINFOEX i = new Native.JOYINFOEX();
                i.dwSize = (uint)Marshal.SizeOf(typeof(Native.JOYINFOEX));
                i.dwFlags = Native.JOY_RETURNALL;
                if (Native.joyGetPosEx(id, ref i) != 0) continue;   // antwortet nicht
                Pad p = new Pad();
                p.Id = (int)id; p.Vid = c.wMid; p.Pid = c.wPid;
                p.Buttons = (int)c.wNumButtons; p.Axes = (int)c.wNumAxes;
                l.Add(p);
            }
            return l;
        }
    }

    // Bekannte N64-Controller und -Adapter. Whitelist statt Raten: was nicht drinsteht,
    // gilt nicht automatisch als N64. Quellen: Linux-Kernel, SDL-Gamepad-Datenbank,
    // RetroArch-Autoconfigs, raphnet-Firmware.
    internal static class N64Geraete
    {
        class Eintrag
        {
            public int Vid, Pid; public string Name;
            public Eintrag(int v, int p, string n) { Vid = v; Pid = p; Name = n; }
        }

        static readonly Eintrag[] Liste = new Eintrag[] {
            new Eintrag(0x057E, 0x2019, "Nintendo N64 controller"),
            new Eintrag(0x0079, 0x0006, "N64 USB adapter (DragonRise)"),
            new Eintrag(0x0079, 0x954E, "Hyperkin N64 adapter"),
            new Eintrag(0x0079, 0x1879, "Mayflash N64 adapter Rev.2"),
            new Eintrag(0x2F24, 0x00F5, "Mayflash MF103 (DInput)"),
            new Eintrag(0x2F24, 0x00F4, "Mayflash MF103 (XInput mode)"),
            new Eintrag(0x2DC8, 0x2869, "8BitDo N64 Modkit"),
            new Eintrag(0x2DC8, 0x3019, "8BitDo 64 Bluetooth controller"),
            new Eintrag(0x2DC8, 0x9002, "Retro-Bit RB8-64 (USB)"),
            new Eintrag(0x2DC8, 0x3830, "Retro-Bit RB8-64 (Bluetooth)"),
            new Eintrag(0x2E24, 0x200B, "Hyperkin Admiral N64"),
            new Eintrag(0x2E24, 0x0BFF, "Hyperkin N64 adapter"),
            new Eintrag(0x06F7, 0x0001, "N64 Adaptoid"),
            new Eintrag(0x1234, 0x0004, "N64 RetroPort"),
            new Eintrag(0x0E8F, 0x3013, "Mayflash N64 adapter Rev.1"),
        };

        // raphnet/Dracal: dort ist die Produktkennung der Betriebsmodus.
        static readonly int[] RaphnetN64 = new int[] {
            0x000F, 0x0020, 0x0033, 0x0039, 0x0061, 0x0091,
            0x0030, 0x0036, 0x003C, 0x0064, 0x0094,
            0x0001, 0x0004, 0x000C, 0x0017, 0x001D, 0x0022,
            0x0032, 0x0035, 0x0038, 0x003B, 0x0060, 0x0063, 0x0090, 0x0093,
        };

        public static string Name(Pad p)
        {
            if (p.Vid == 0x045E) return null;    // Xbox-Pads nie - auch der virtuelle nicht
            if (p.Buttons > 16) return null;     // ueber 16 Knoepfe ist kein N64-Pad
            foreach (Eintrag e in Liste)
                if (e.Vid == p.Vid && e.Pid == p.Pid) return e.Name;
            if (p.Vid == 0x289B)
                foreach (int pid in RaphnetN64)
                    if (pid == p.Pid) return "raphnet N64 adapter";
            return null;
        }
    }
    // ------------------------------------------------------------------ Belegung

    // Woher kommt ein Signal: Knopf-Bit, POV-Richtung oder Achse in eine Richtung
    internal class Quelle
    {
        public string Art = "none";     // "btn" | "pov" | "axis"
        public int Nr;
        public int Vorzeichen;
        public uint PovWert;

        public static Quelle Btn(int n) { Quelle q = new Quelle(); q.Art = "btn"; q.Nr = n; return q; }
        public static Quelle Pov(uint w) { Quelle q = new Quelle(); q.Art = "pov"; q.PovWert = w; return q; }
        public static Quelle Ax(int n, int v) { Quelle q = new Quelle(); q.Art = "axis"; q.Nr = n; q.Vorzeichen = v; return q; }

        public string Text()
        {
            if (Art == "btn") return "Knopf " + (Nr + 1);
            if (Art == "pov") return "POV " + (PovWert / 100) + "°";
            if (Art == "axis") return "Achse " + Nr + (Vorzeichen > 0 ? " +" : " -");
            return "-";
        }
        public string Speichern()
        {
            if (Art == "btn") return "btn:" + Nr;
            if (Art == "pov") return "pov:" + PovWert;
            if (Art == "axis") return "axis:" + Nr + ":" + Vorzeichen;
            return "none";
        }
        public static Quelle Lesen(string s)
        {
            Quelle q = new Quelle();
            if (string.IsNullOrEmpty(s)) return q;
            string[] t = s.Split(':');
            if (t[0] == "btn" && t.Length > 1) { q.Art = "btn"; q.Nr = int.Parse(t[1]); }
            else if (t[0] == "pov" && t.Length > 1) { q.Art = "pov"; q.PovWert = uint.Parse(t[1]); }
            else if (t[0] == "axis" && t.Length > 2) { q.Art = "axis"; q.Nr = int.Parse(t[1]); q.Vorzeichen = int.Parse(t[2]); }
            return q;
        }

        // mitte = tatsaechliche Ruhelage der Achse. N64-Sticks zentrieren mechanisch
        // nie exakt auf 32767 - ohne diese Korrektur bleibt ein Rest stehen und die
        // beiden Seiten bekommen ungleich viel Weg.
        public bool An(RawState s, int totzone, int mitte)
        {
            if (!s.Ok) return false;
            if (Art == "btn") return (s.Buttons & (1u << Nr)) != 0;
            if (Art == "pov")
            {
                if (s.Pov > 36000) return false;
                int d = (int)s.Pov - (int)PovWert;
                while (d > 18000) d -= 36000;
                while (d < -18000) d += 36000;
                return Math.Abs(d) <= 6000;      // Diagonalen zaehlen fuer beide Nachbarn
            }
            if (Art == "axis")
            {
                int v = s.Axis(Nr) - mitte;
                return Vorzeichen > 0 ? v > totzone : v < -totzone;
            }
            return false;
        }

        public double Menge(RawState s, int totzone, double verstaerkung, int mitte)
        {
            if (!s.Ok) return 0;
            if (Art != "axis") return An(s, totzone, mitte) ? 1.0 : 0.0;
            int v = (s.Axis(Nr) - mitte) * Vorzeichen;
            if (v <= totzone) return 0;
            // Weg bis zum Anschlag auf DIESER Seite, nicht die halbe Skala
            double weg = Vorzeichen > 0 ? (65535.0 - mitte) : mitte;
            if (weg < 8000) weg = 8000;
            double a = (v - totzone) / (weg - totzone) * verstaerkung;
            return a > 1 ? 1 : a;
        }
    }

    // Die 18 Eingaben eines N64-Controllers
    internal static class Ctrl
    {
        public static readonly string[][] Liste = new string[][] {
            new string[]{"StickUp",    "Stick up"},
            new string[]{"StickDown",  "Stick down"},
            new string[]{"StickLeft",  "Stick left"},
            new string[]{"StickRight", "Stick right"},
            new string[]{"DUp",        "D-Pad up"},
            new string[]{"DDown",      "D-Pad down"},
            new string[]{"DLeft",      "D-Pad left"},
            new string[]{"DRight",     "D-Pad right"},
            new string[]{"A",          "A"},
            new string[]{"B",          "B"},
            new string[]{"Z",          "Z"},
            new string[]{"L",          "L"},
            new string[]{"R",          "R"},
            new string[]{"Start",      "START"},
            new string[]{"CUp",        "C up"},
            new string[]{"CDown",      "C down"},
            new string[]{"CLeft",      "C left"},
            new string[]{"CRight",     "C right"},
        };
    }

    // Die feste Steuerung des Vorbilds. Nicht aenderbar - das ist der Sinn des Programms.
    internal static class Flug
    {
        public static readonly Dictionary<string, string> Ziel = new Dictionary<string, string>();
        public static readonly List<string[]> Karte = new List<string[]>();

        public const string LadeQuelle = "A";           // Halten laedt
        public const string LadeZiel = "btn:A";         // Ladeschuss-Taste des Spiels
        public const int LadeMs = 370;                  // Schwelle wie im Original

        static Flug()
        {
            Ziel["StickUp"] = "axis:LeftThumbY:1";
            Ziel["StickDown"] = "axis:LeftThumbY:-1";
            Ziel["StickLeft"] = "axis:LeftThumbX:-1";
            Ziel["StickRight"] = "axis:LeftThumbX:1";
            Ziel["DUp"] = "btn:Up"; Ziel["DDown"] = "btn:Down";
            Ziel["DLeft"] = "btn:Left"; Ziel["DRight"] = "btn:Right";
            Ziel["A"] = "btn:X";            // Laser
            Ziel["B"] = "btn:B";                 // Smart Bomb wie im Original
            Ziel["Z"] = "btn:LeftShoulder";      // Tilt links, 2x = Rolle
            Ziel["L"] = "btn:LeftShoulder";
            Ziel["R"] = "btn:RightShoulder";     // Tilt rechts, 2x = Rolle
            Ziel["Start"] = "btn:Start";
            Ziel["CUp"] = "none";                // unbelegt
            Ziel["CDown"] = "sld:LeftTrigger";   // Bremse
            Ziel["CLeft"] = "sld:RightTrigger";  // Boost
            Ziel["CRight"] = "btn:Y";            // Funk - im Spiel geprueft

            Karte.Add(new string[] { "Stick", "Steer your ship", "analog" });
            Karte.Add(new string[] { "A", "Laser · hold to charge", "" });
            Karte.Add(new string[] { "B", "Smart Bomb", "" });
            Karte.Add(new string[] { "Z / L", "Tilt left · double-tap to roll", "" });
            Karte.Add(new string[] { "R", "Tilt right · double-tap to roll", "" });
            Karte.Add(new string[] { "C ←", "Boost", "" });
            Karte.Add(new string[] { "C ↓", "Brake", "" });
            Karte.Add(new string[] { "C →", "Radio", "" });
            Karte.Add(new string[] { "START", "Pause", "" });
        }
    }

    // Was ein bestimmtes Geraet sendet - je Hersteller-Kennung
    internal class PadLayout
    {
        public string Kennung = "";
        public string Name = "";
        public int Totzone = 3000;
        public int Verstaerkung = 100;
        public Dictionary<string, Quelle> Q = new Dictionary<string, Quelle>();

        // Gemessene Ruhelage je Achse. Wird beim Verbinden gesetzt und danach
        // langsam nachgefuehrt, damit der Stick nicht dauerhaft zieht.
        public int[] Mitte = new int[] { 32767, 32767, 32767, 32767, 32767, 32767 };

        public int MitteVon(Quelle q)
        {
            if (q.Art != "axis" || q.Nr < 0 || q.Nr > 5) return 32767;
            return Mitte[q.Nr];
        }

        public bool Vollstaendig
        {
            get
            {
                foreach (string[] c in Ctrl.Liste)
                {
                    Quelle q;
                    if (!Q.TryGetValue(c[0], out q) || q.Art == "none") return false;
                }
                return true;
            }
        }

        public Quelle Get(string id)
        {
            Quelle q;
            if (Q.TryGetValue(id, out q)) return q;
            return new Quelle();
        }

        // Die beiden Geraete, die wir vermessen haben - fuer die ist nichts einzurichten.
        public static PadLayout Bekannt(Pad p)
        {
            if (p.Vid == 0x0079 && p.Pid == 0x0006)
            {
                // Das Vorbild legt GAR KEINE Totzone auf den Flugstick: in fox_play.c
                // geht stick_x/stick_y roh in Math_SmoothStepToF (Ziel = Stick * 0.68),
                // es gibt dort sogar einen Vergleich auf exakt 0. Das geht, weil der
                // echte N64-Pad seinen Nullpunkt beim Einstecken selbst kalibriert.
                // Wir kommen dem am naechsten mit der kleinsten Schwelle, die der
                // Adapter zulaesst: Rauschen in Ruhe = 0 gemessen, Schrittweite ~257,
                // also 600 = zwei Schritte. (War 4500 = 15 % des Weges, dann 1200.)
                PadLayout l = Standard("0079:0006", "N64 USB adapter (DragonRise)", 600, 100);
                l.Q["A"] = Quelle.Btn(5); l.Q["B"] = Quelle.Btn(4);
                l.Q["Z"] = Quelle.Btn(8); l.Q["L"] = Quelle.Btn(6); l.Q["R"] = Quelle.Btn(7);
                l.Q["Start"] = Quelle.Btn(9);
                l.Q["CUp"] = Quelle.Btn(0); l.Q["CDown"] = Quelle.Btn(2);
                l.Q["CLeft"] = Quelle.Btn(3); l.Q["CRight"] = Quelle.Btn(1);
                return l;
            }
            if (p.Vid == 0x057E && p.Pid == 0x2019 && p.Buttons <= 16)
            {
                // ueber Bluetooth: 16 Knoepfe. Am USB-Kabel meldet er 18 und sendet Muell.
                PadLayout l = Standard("057E:2019", "Nintendo N64 controller (wireless)", 1500, 150);
                l.Q["A"] = Quelle.Btn(1); l.Q["B"] = Quelle.Btn(0);
                l.Q["Z"] = Quelle.Btn(6); l.Q["L"] = Quelle.Btn(4); l.Q["R"] = Quelle.Btn(5);
                l.Q["Start"] = Quelle.Btn(9);
                l.Q["CUp"] = Quelle.Btn(2); l.Q["CDown"] = Quelle.Btn(7);
                l.Q["CLeft"] = Quelle.Btn(3); l.Q["CRight"] = Quelle.Btn(8);
                return l;
            }
            return null;
        }

        static PadLayout Standard(string kennung, string name, int totzone, int verst)
        {
            PadLayout l = new PadLayout();
            l.Kennung = kennung; l.Name = name;
            l.Totzone = totzone; l.Verstaerkung = verst;
            l.Q["StickUp"] = Quelle.Ax(1, -1); l.Q["StickDown"] = Quelle.Ax(1, 1);
            l.Q["StickLeft"] = Quelle.Ax(0, -1); l.Q["StickRight"] = Quelle.Ax(0, 1);
            l.Q["DUp"] = Quelle.Pov(0); l.Q["DRight"] = Quelle.Pov(9000);
            l.Q["DDown"] = Quelle.Pov(18000); l.Q["DLeft"] = Quelle.Pov(27000);
            return l;
        }

        // ---- Speicher fuer selbst eingelernte Geraete

        public static string Ordner
        {
            get
            {
                string d = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Skybridge64");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }
        static string Datei(string kennung) { return Path.Combine(Ordner, kennung.Replace(':', '-') + ".txt"); }

        public void Speichern()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("name=" + Name);
            sb.AppendLine("totzone=" + Totzone);
            sb.AppendLine("verstaerkung=" + Verstaerkung);
            foreach (string[] c in Ctrl.Liste) sb.AppendLine(c[0] + "=" + Get(c[0]).Speichern());
            File.WriteAllText(Datei(Kennung), sb.ToString(), Encoding.UTF8);
        }

        public static PadLayout Laden(Pad p)
        {
            string f = Datei(p.Kennung);
            if (!File.Exists(f)) return null;
            try
            {
                PadLayout l = new PadLayout();
                l.Kennung = p.Kennung;
                foreach (string line in File.ReadAllLines(f))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string k = line.Substring(0, i), v = line.Substring(i + 1);
                    if (k == "name") l.Name = v;
                    else if (k == "totzone") l.Totzone = int.Parse(v);
                    else if (k == "verstaerkung") l.Verstaerkung = int.Parse(v);
                    else l.Q[k] = Quelle.Lesen(v);
                }
                return l.Vollstaendig ? l : null;
            }
            catch { return null; }
        }
    }

    // ------------------------------------------------------------------ Ausgabe

    internal class XPad
    {
        Nefarius.ViGEm.Client.ViGEmClient client;
        Nefarius.ViGEm.Client.Targets.IXbox360Controller pad;
        public string Fehler = "";
        public bool Verbunden;
        public long Berichte;

        // Prueft, ob der ViGEmBus-Treiber ueberhaupt da ist - ohne ihn laeuft nichts.
        public static bool TreiberVorhanden()
        {
            try
            {
                using (Nefarius.ViGEm.Client.ViGEmClient c = new Nefarius.ViGEm.Client.ViGEmClient()) { }
                return true;
            }
            catch { return false; }
        }

        public bool Verbinden()
        {
            Trennen();
            try
            {
                client = new Nefarius.ViGEm.Client.ViGEmClient();
                pad = client.CreateXbox360Controller();
                pad.AutoSubmitReport = false;
                pad.Connect();
                pad.ResetReport(); pad.SubmitReport();
                Verbunden = true; Fehler = "";
                return true;
            }
            catch (Exception ex) { Fehler = ex.Message; Trennen(); return false; }
        }

        public void Trennen()
        {
            try { if (pad != null) { pad.ResetReport(); pad.SubmitReport(); pad.Disconnect(); } } catch { }
            try { if (client != null) client.Dispose(); } catch { }
            pad = null; client = null; Verbunden = false;
        }

        public void Absenden() { if (pad != null) { try { pad.SubmitReport(); Berichte++; } catch (Exception ex) { Fehler = ex.Message; } } }

        // Platz, den XInput dem virtuellen Pad gegeben hat. 0 = Spieler 1.
        public int SpielerNr
        {
            get
            {
                if (pad == null) return -1;
                try { return pad.UserIndex; } catch { return -1; }
            }
        }

        public void Knopf(string n, bool an)
        {
            if (pad == null) return;
            try { var b = Btn(n); if (b != null) pad.SetButtonState(b, an); } catch { }
        }
        public void Regler(string n, double m)
        {
            if (pad == null) return;
            try
            {
                int v = (int)Math.Round(m * 255); if (v < 0) v = 0; if (v > 255) v = 255;
                if (n == "LeftTrigger") pad.SetSliderValue(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Slider.LeftTrigger, (byte)v);
                else if (n == "RightTrigger") pad.SetSliderValue(Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Slider.RightTrigger, (byte)v);
            }
            catch { }
        }
        public void Achse(string n, double w)
        {
            if (pad == null) return;
            try
            {
                if (w < -1) w = -1; if (w > 1) w = 1;
                var a = Ax(n);
                if (a != null) pad.SetAxisValue(a, (short)Math.Round(w * 32767));
            }
            catch { }
        }

        static Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button Btn(string n)
        {
            switch (n)
            {
                case "A": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.A;
                case "B": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.B;
                case "X": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.X;
                case "Y": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Y;
                case "Up": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Up;
                case "Down": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Down;
                case "Left": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Left;
                case "Right": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Right;
                case "Start": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Start;
                case "Back": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.Back;
                case "LeftShoulder": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.LeftShoulder;
                case "RightShoulder": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Button.RightShoulder;
            }
            return null;
        }
        static Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Axis Ax(string n)
        {
            switch (n)
            {
                case "LeftThumbX": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Axis.LeftThumbX;
                case "LeftThumbY": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Axis.LeftThumbY;
                case "RightThumbX": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Axis.RightThumbX;
                case "RightThumbY": return Nefarius.ViGEm.Client.Targets.Xbox360.Xbox360Axis.RightThumbY;
            }
            return null;
        }
    }
}
