// Hauptfenster und Ablaufsteuerung von SKYBRIDGE 64.
// Sucht selbst nach einem N64-Controller, waehlt die Belegung, meldet den
// virtuellen Xbox-Pad an und faengt an. Ohne Zutun.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Skybridge64
{
    internal class MainForm : Form
    {
        PadView pad;
        StatusBand band;
        Panel karte;
        Button btLernen, btPause, btNeuAnmelden, btTreiber;


        Thread poll;
        volatile bool running = true;
        volatile bool pausiert;

        Pad aktuell;
        PadLayout layout;
        XPad xpad = new XPad();
        RawState letzte = new RawState();
        readonly object sperre = new object();

        volatile string zustand = "suchen";   // suchen | lernen_noetig | lernen | bereit | fehler
        volatile string meldung = "";
        volatile string geraetName = "";
        volatile bool treiberFehlt;

        volatile int lernIndex = -1;
        RawState lernBasis;
        PadLayout lernLayout;

        int fehlTakte, letzteSuche;
        uint vorigeKnoepfe; int unsinnZaehler, unsinnSeit;

        int ladeVon, laserSeit, laserFreiAb, letzteSalve, menuBis, salveStart, salveIdx;
        bool ladeVorher, laedt, menuImpuls;

        readonly HashSet<string> aktivJetzt = new HashSet<string>();
        readonly Dictionary<string, bool> knoepfe = new Dictionary<string, bool>();
        readonly Dictionary<string, double> regler = new Dictionary<string, double>();
        double stickX, stickY;

        const int HOTKEY = 0xC64;

        // -------------------------------------------------------------- Aufbau

        public MainForm()
        {
            Text = "Skybridge 64";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            ClientSize = new Size(1060, 706);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Farbe.Grund;
            DoubleBuffered = true;
            ForeColor = Farbe.Schrift;
            Font = Hud.S(9.5f);

            Kopf();
            band = new StatusBand();
            band.Location = new Point(24, 84); band.Size = new Size(1012, 66);
            Controls.Add(band);

            pad = new PadView();
            pad.Location = new Point(24, 164); pad.Size = new Size(620, 470);
            Controls.Add(pad);

            karte = new Panel();
            karte.Location = new Point(660, 164); karte.Size = new Size(376, 470);
            karte.BackColor = Farbe.Grund;
            karte.Paint += KartePaint;
            Controls.Add(karte);

            Fuss();

            treiberFehlt = !XPad.TreiberVorhanden();

            poll = new Thread(PollLoop); poll.IsBackground = true; poll.Start();

            System.Windows.Forms.Timer ui = new System.Windows.Forms.Timer(); ui.Interval = 33; ui.Tick += UiTakt; ui.Start();

            Native.RegisterHotKey(Handle, HOTKEY, 0, 0x77);   // F8
            FormClosing += delegate
            {
                running = false; Thread.Sleep(30);
                xpad.Trennen();
                Native.UnregisterHotKey(Handle, HOTKEY);
            };
        }

        // Kopf, Fusszeile und Hintergrund werden selbst gezeichnet - so lassen sich
        // gesperrte Versalien und das Gitter sauber setzen.
        void Kopf() { }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Hud.Hintergrund(g, ClientSize.Width, ClientSize.Height, 0, 0);

            using (Font ft = Hud.T(21f))
                Hud.Gesperrt(g, "SKYBRIDGE 64", ft, Farbe.Schrift, 24, 18, 2.6f);
            using (Font fu = Hud.S(10.5f))
            using (SolidBrush b = new SolidBrush(Farbe.Blass))
                g.DrawString("N64 CONTROLS FOR WILD BLUE SKIES", fu, b, 26, 54);

            using (Pen p = new Pen(Color.FromArgb(70, Farbe.Linie), 1.2f))
                g.DrawLine(p, 24, 76, ClientSize.Width - 24, 76);

            using (Font fc = Hud.S(9.5f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(92, 116, 138)))
            {
                string c = "© PAPERTOWERSTUDIOS";
                float w = g.MeasureString(c, fc).Width;
                g.DrawString(c, fc, b, ClientSize.Width - 24 - w, 34);
            }

            using (Font ff = Hud.S(9.5f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(92, 116, 138)))
            {
                g.DrawString("RUNS BY ITSELF — JUST CONNECT A CONTROLLER", ff, b, 650, 646);
                using (SolidBrush b2 = new SolidBrush(Color.FromArgb(66, 84, 102)))
                using (Font fz = Hud.S(8.5f))
                    g.DrawString("Not affiliated with or endorsed by Nintendo.", fz, b2, 650, 668);
            }
        }

        void Fuss()
        {
            btLernen = Knopf("Teach controller", 24, 650, 176);
            btLernen.Visible = false;
            btLernen.Click += delegate { LernenStarten(); };

            btPause = Knopf("Pause  (F8)", 212, 650, 130);
            btPause.Click += delegate { PauseUm(); };

            btNeuAnmelden = Knopf("Reconnect pad", 354, 650, 130);
            btNeuAnmelden.Click += delegate { NeuAnmelden(); };

            btTreiber = Knopf("Set up now", 24, 650, 176);
            btTreiber.Visible = false;
            btTreiber.Click += delegate
            {
                TreiberInstallieren();
            };
        }

        // Der Treiber steckt als Ressource in der EXE. Ein Klick schreibt ihn heraus,
        // installiert ihn still und macht danach ohne Neustart weiter. Klappt die
        // stille Installation nicht, laeuft dasselbe Setup noch einmal mit Fenster.
        volatile bool installiertGerade;
        volatile string treiberMeldung = "";

        void TreiberInstallieren()
        {
            if (installiertGerade || !treiberFehlt) return;
            installiertGerade = true;
            treiberMeldung = "";
            Thread t = new Thread(delegate()
            {
                string ziel = "";
                string melde = "";
                bool abgebrochen = false, neustart = false;
                // Kurzes Protokoll: auf einem fremden Rechner ist das der einzige Weg
                // herauszufinden, WARUM die Einrichtung nicht durchlief.
                List<string> log = new List<string>();
                try
                {
                    ziel = SetupFinden();
                    if (ziel == "")
                        throw new Exception("The ViGEmBus installer is missing. Keep all files from the ZIP together in one folder.");
                    log.Add("setup gefunden: " + ziel);
                    int code = SetupStarten(ziel, "/exenoui /qn");     // still, ohne Klickstrecke
                    log.Add("still  '/exenoui /qn'  -> exitcode " + code);
                    if (code == 1602 || code == 1223) abgebrochen = true;
                    if (code == 3010 || code == 1641) neustart = true;
                    log.Add("treiber danach da: " + XPad.TreiberVorhanden());

                    if (!abgebrochen && !neustart && !XPad.TreiberVorhanden())
                    {
                        // Manche Systeme lassen die stille Variante nicht durch -
                        // dann bekommt der Nutzer das normale Setupfenster.
                        code = SetupStarten(ziel, "");
                        log.Add("sichtbar  -> exitcode " + code);
                        if (code == 1602 || code == 1223) abgebrochen = true;
                        if (code == 3010 || code == 1641) neustart = true;
                    }
                }
                catch (System.ComponentModel.Win32Exception w)
                {
                    log.Add("Win32 " + w.NativeErrorCode + ": " + w.Message);
                    if (w.NativeErrorCode == 1223) abgebrochen = true;   // UAC weggeklickt
                    else melde = w.Message;
                }
                catch (Exception ex) { log.Add("Ausnahme: " + ex.Message); melde = ex.Message; }

                treiberFehlt = !XPad.TreiberVorhanden();
                log.Add("ergebnis: treiberFehlt=" + treiberFehlt + " abgebrochen=" + abgebrochen + " neustart=" + neustart);
                if (treiberFehlt)
                {
                    if (abgebrochen)
                        treiberMeldung = "Setup was cancelled. Press the button again and confirm the Windows prompt.";
                    else if (neustart)
                        treiberMeldung = "The driver is installed. Please restart Windows once, then start Skybridge 64 again.";
                    else
                        treiberMeldung = melde == "" ? "Setup did not finish. Please try once more." : melde;
                }
                try { System.IO.File.WriteAllLines(System.IO.Path.Combine(PadLayout.Ordner, "setup-log.txt"), log.ToArray()); }
                catch { }
                installiertGerade = false;
            });
            t.IsBackground = true;
            t.Start();
        }

        // Das Treiber-Setup liegt NEBEN der EXE, nicht darin.
        //
        // Frueher steckte es als Ressource in der EXE und wurde zur Laufzeit auf die
        // Platte geschrieben und erhoeht gestartet. Genau dieses Muster - eingebettete
        // zweite EXE, ablegen, mit Adminrechten ausfuehren - stufen Virenscanner als
        // Dropper ein; Defender meldete Trojan:Win32/Sabsik.EN.B!ml und loeschte die
        // Datei beim Entpacken. Eine sichtbare Datei daneben ist nicht nur unauffaellig,
        // sondern ehrlicher: sie ist von Nefarius signiert und jeder kann sie pruefen.
        static string SetupFinden()
        {
            try
            {
                string ordner = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                string[] treffer = System.IO.Directory.GetFiles(ordner, "ViGEmBus*.exe");
                if (treffer.Length > 0) return treffer[0];
            }
            catch { }
            return "";
        }

        static int SetupStarten(string datei, string args)
        {
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = datei;
            psi.Arguments = args;
            psi.UseShellExecute = true;
            psi.Verb = "runas";             // Treiber brauchen Adminrechte
            using (System.Diagnostics.Process p = System.Diagnostics.Process.Start(psi))
            {
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        Button Knopf(string text, int x, int y, int w)
        {
            HudKnopf b = new HudKnopf();
            b.Text = text; b.Location = new Point(x, y); b.Size = new Size(w, 34);
            Controls.Add(b);
            return b;
        }

        void KartePaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Hud.Hintergrund(g, karte.Width, karte.Height, karte.Left, karte.Top);

            RectangleF r = new RectangleF(1, 1, karte.Width - 3, karte.Height - 3);
            using (GraphicsPath p = Hud.Kante(r, 16))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, 10, 21, 38)))
            using (Pen pen = new Pen(Color.FromArgb(110, Farbe.Linie), 1.3f))
            { g.FillPath(b, p); g.DrawPath(pen, p); }
            Hud.Klammern(g, new RectangleF(10, 10, karte.Width - 21, karte.Height - 21),
                         Color.FromArgb(110, Farbe.Cyan), 16, 1.5f);

            using (Font fk = Hud.T(10f))
                Hud.Gesperrt(g, "CONTROLS", fk, Farbe.Cyan, 24, 20, 2.2f);
            using (Pen p = new Pen(Color.FromArgb(90, Farbe.Linie), 1.2f))
                g.DrawLine(p, 24, 46, karte.Width - 24, 46);

            float y = 60;
            using (Font f1 = Hud.T(11.5f))
            using (Font f2 = Hud.S(10f))
            using (SolidBrush b2 = new SolidBrush(Farbe.Blass))
            using (Pen linie = new Pen(Color.FromArgb(34, 70, 130, 180)))
            using (SolidBrush punkt = new SolidBrush(Color.FromArgb(160, Farbe.Cyan)))
            {
                foreach (string[] z in Flug.Karte)
                {
                    g.FillRectangle(punkt, 24, y + 9, 3, 3);
                    Hud.Gesperrt(g, z[0].ToUpperInvariant(), f1, Farbe.Schrift, 36, y, 1.1f);
                    g.DrawString(z[1], f2, b2, 132, y + 2);
                    y += 31;
                    if (y < karte.Height - 30) g.DrawLine(linie, 24, y - 6, karte.Width - 24, y - 6);
                }
            }
        }

        // -------------------------------------------------------------- Ablauf

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == HOTKEY) PauseUm();
            base.WndProc(ref m);
        }

        void PauseUm()
        {
            pausiert = !pausiert;
            if (pausiert) { xpad.Trennen(); }
            else if (zustand == "bereit" && aktuell != null) xpad.Verbinden();
            btPause.Text = pausiert ? "Resume  (F8)" : "Pause  (F8)";
        }

        // Meldet den virtuellen Pad ab und neu an. Er bekommt dann den niedrigsten
        // freien XInput-Platz - reicht, wenn Platz 0 inzwischen frei geworden ist.
        void NeuAnmelden()
        {
            if (aktuell == null || layout == null) return;
            xpad.Trennen();
            Thread.Sleep(400);
            if (!xpad.Verbinden()) { zustand = "fehler"; meldung = xpad.Fehler; return; }
            zustand = "bereit"; meldung = "";
        }

        void PollLoop()
        {
            while (running)
            {
                try { Takt(); }
                catch (Exception ex) { meldung = ex.Message; }
                Thread.Sleep(4);
            }
        }

        static RawState Lies(int id)
        {
            RawState s = new RawState();
            Native.JOYINFOEX i = new Native.JOYINFOEX();
            i.dwSize = (uint)Marshal.SizeOf(typeof(Native.JOYINFOEX));
            i.dwFlags = Native.JOY_RETURNALL;
            if (Native.joyGetPosEx((uint)id, ref i) != 0) return s;
            s.Ok = true;
            s.X = (int)i.dwXpos; s.Y = (int)i.dwYpos; s.Z = (int)i.dwZpos;
            s.R = (int)i.dwRpos; s.U = (int)i.dwUpos; s.V = (int)i.dwVpos;
            s.Buttons = i.dwButtons; s.Pov = i.dwPOV;
            return s;
        }

        void Takt()
        {
            if (treiberFehlt) return;
            if (aktuell == null)
            {
                if (Environment.TickCount - letzteSuche > 700)
                {
                    letzteSuche = Environment.TickCount;
                    Suchen();
                }
                return;
            }

            RawState s = Lies(aktuell.Id);
            lock (sperre) letzte = s;

            if (!s.Ok)
            {
                fehlTakte++;
                if (fehlTakte > 200) Loesen();
                return;
            }
            fehlTakte = 0;

            if (lernIndex >= 0) { LernSchritt(s); return; }

            if (layout != null) MitteNachfuehren(s);

            // Waechter gegen Geraete, die von selbst Unsinn senden
            if (s.Buttons != vorigeKnoepfe) { unsinnZaehler++; vorigeKnoepfe = s.Buttons; }
            if (Environment.TickCount - unsinnSeit > 2000)
            {
                if (unsinnZaehler > 80)
                {
                    zustand = "fehler";
                    meldung = "This pad fires dozens of button changes per second on its own. " +
                              "With Nintendo's wireless pad that means it is plugged into USB. Unplug the cable and connect by Bluetooth.";
                    xpad.Trennen();
                }
                unsinnZaehler = 0;
                unsinnSeit = Environment.TickCount;
            }

            if (zustand == "bereit" && !pausiert) Anwenden(s);
        }

        static bool IstN64(Pad p) { return N64Geraete.Name(p) != null; }

        static Pad Kandidat()
        {
            List<Pad> alle = Pad.Alle();
            foreach (Pad p in alle) if (IstN64(p)) return p;
            // unbekanntes Gerät: alles ausser Xbox-Pads (die sind auch der virtuelle Pad)
            foreach (Pad p in alle) if (p.Vid != 0x045E && p.Buttons >= 8) return p;
            return null;
        }

        void Suchen()
        {
            Pad p = Kandidat();
            if (p == null)
            {
                if (zustand != "suchen") { zustand = "suchen"; geraetName = ""; meldung = ""; }
                return;
            }

            // Nintendo-Funkpad am Kabel: meldet 18 Knoepfe und sendet Muell
            if (p.Vid == 0x057E && p.Pid == 0x2019 && p.Buttons > 16)
            {
                zustand = "fehler";
                geraetName = "Nintendo N64 controller on USB cable";
                meldung = "Over the cable this pad sends nothing but garbage. The cable is for charging — connect by Bluetooth to play.";
                return;
            }

            PadLayout l = PadLayout.Bekannt(p);
            if (l == null) l = PadLayout.Laden(p);

            aktuell = p;
            unsinnZaehler = 0; unsinnSeit = Environment.TickCount;

            if (l == null)
            {
                layout = null;
                geraetName = "Unknown controller  " + p.Kennung;
                meldung = "";
                zustand = "lernen_noetig";
                return;
            }

            layout = l;
            geraetName = l.Name;
            if (!pausiert && !xpad.Verbinden())
            {
                zustand = "fehler";
                meldung = "Cannot register the virtual Xbox controller: " + xpad.Fehler +
                          "  —  Is the ViGEmBus driver running?";
                return;
            }
            meldung = "";
            zustand = "bereit";
        }

        void Loesen()
        {
            aktuell = null; layout = null; fehlTakte = 0; mitteGesetzt = false;
            xpad.Trennen();
            lernIndex = -1;
            if (zustand != "fehler") { zustand = "suchen"; geraetName = ""; }
        }

        // Nimmt die tatsaechliche Ruhelage als Nullpunkt. Beim Verbinden einmal hart
        // gesetzt, danach nur noch sanft nachgezogen - und ausschliesslich in einem
        // engen Band, damit ein absichtlich gehaltener Stick nicht zur neuen Mitte wird.
        int ruheSeit;
        void MitteNachfuehren(RawState s)
        {
            if (!mitteGesetzt)
            {
                for (int a = 0; a < 6; a++) layout.Mitte[a] = s.Axis(a);
                mitteGesetzt = true;
                ruheSeit = Environment.TickCount;
                return;
            }
            // Zwei Geschwindigkeiten. INNERHALB der Totzone kommt beim Spiel ohnehin
            // nichts an - dort darf zuegig nachgezogen werden, das kann nichts wegnehmen.
            // Darueber, bis 2000, nur noch ein sehr langsames Sicherheitsnetz fuer den
            // Fall, dass die Ruhelage nach starkem Auslenken verschoben zurueckkommt;
            // zu schnell wuerde es eine absichtlich gehaltene Feinkorrektur auffressen.
            int weiteste = 0;
            for (int a = 0; a < 2; a++)
            {
                int d = Math.Abs(s.Axis(a) - layout.Mitte[a]);
                if (d > weiteste) weiteste = d;
            }

            double anteil; int wartems;
            if (weiteste <= layout.Totzone) { anteil = 0.30; wartems = 400; }
            else if (weiteste <= 2000) { anteil = 0.02; wartems = 2000; }
            else { ruheSeit = Environment.TickCount; return; }

            if (Environment.TickCount - ruheSeit < wartems) return;

            for (int a = 0; a < 2; a++)
                layout.Mitte[a] += (int)Math.Round((s.Axis(a) - layout.Mitte[a]) * anteil);
            ruheSeit = Environment.TickCount;
        }
        bool mitteGesetzt;

        void Anwenden(RawState s)
        {
            double lx = 0, ly = 0, rx = 0, ry = 0;
            knoepfe.Clear(); regler.Clear();
            bool ladeGedrueckt = false;
            aktivJetzt.Clear();

            foreach (string[] c in Ctrl.Liste)
            {
                string id = c[0];
                Quelle q = layout.Get(id);
                int mitte = layout.MitteVon(q);
                bool an = q.An(s, layout.Totzone, mitte);
                double menge = q.Menge(s, layout.Totzone, layout.Verstaerkung / 100.0, mitte);
                if (an) aktivJetzt.Add(id);

                if (id == Flug.LadeQuelle) { ladeGedrueckt = an; continue; }

                string ziel;
                if (!Flug.Ziel.TryGetValue(id, out ziel) || ziel == "none") continue;
                // Sammeln statt sofort schreiben: mehrere N64-Knoepfe duerfen auf
                // dasselbe Xbox-Ziel zeigen (Z und L auf LB, B und C-rechts auf Bombe).
                // Wer direkt schreibt, loescht mit dem losgelassenen Knopf den gedrueckten.
                if (ziel.StartsWith("btn:"))
                {
                    string k = ziel.Substring(4); bool alt;
                    knoepfe[k] = (knoepfe.TryGetValue(k, out alt) && alt) || an;
                }
                else if (ziel.StartsWith("sld:"))
                {
                    string k = ziel.Substring(4); double alt;
                    double bisher = regler.TryGetValue(k, out alt) ? alt : 0;
                    regler[k] = Math.Max(bisher, menge);
                }
                else if (ziel.StartsWith("axis:"))
                {
                    string[] pp = ziel.Split(':');
                    if (pp.Length >= 3)
                    {
                        double v = pp[2] == "-1" ? -menge : menge;
                        if (pp[1] == "LeftThumbX") lx += v;
                        else if (pp[1] == "LeftThumbY") ly += v;
                        else if (pp[1] == "RightThumbX") rx += v;
                        else if (pp[1] == "RightThumbY") ry += v;
                    }
                }
            }

            foreach (KeyValuePair<string, bool> kv in knoepfe) xpad.Knopf(kv.Key, kv.Value);
            foreach (KeyValuePair<string, double> kv in regler) xpad.Regler(kv.Key, kv.Value);

            Ladeschuss(ladeGedrueckt);

            stickX = lx; stickY = -ly;
            xpad.Achse("LeftThumbX", lx);
            xpad.Achse("LeftThumbY", ly);
            xpad.Achse("RightThumbX", rx);
            xpad.Achse("RightThumbY", ry);
            xpad.Absenden();
        }

        // Das Vorbild bedient Laser und Ladeschuss mit EINEM Knopf:
        // Druck feuert sofort, Halten laedt, Loslassen schickt den geladenen Schuss.
        void Ladeschuss(bool gedrueckt)
        {
            int jetzt = Environment.TickCount;
            bool flanke = gedrueckt && !ladeVorher;
            if (flanke)
            {
                ladeVon = jetzt; laedt = false;
                salveStart = jetzt; salveIdx = 0;
                menuImpuls = (jetzt - letzteSalve) > 250;   // vereinzelter Druck = Menue
                if (menuImpuls) menuBis = jetzt + 160;      // lang genug fuer Zwischensequenzen
            }
            if (!gedrueckt) { laedt = false; salveIdx = 3; }
            ladeVorher = gedrueckt;
            if (gedrueckt && !laedt && jetzt - ladeVon >= Flug.LadeMs) laedt = true;

            // 1:1 nach fox_play.c aus der Star-Fox-64-Decompilation:
            //   Druck  -> Laser sofort, shotTimer = 8
            //   Halten -> zwei Nachschuesse bei +3 und +7 Frames, dann Ruhe (es laedt)
            // Ein Frame = 33 ms, das Vorbild lief mit 30 Bildern pro Sekunde. Druck und
            // Luecke je ein Frame ergeben die Obergrenze des Originals: ~15 Schuss/s.
            const int Frame = 33, Puls = 33, Luecke = 33;
            int faellig = salveIdx == 0 ? 0 : (salveIdx == 1 ? 3 * Frame : 7 * Frame);
            if (!laedt && gedrueckt && salveIdx < 3
                && jetzt - salveStart >= faellig && jetzt >= laserFreiAb)
            {
                laserSeit = jetzt;
                laserFreiAb = jetzt + Puls + Luecke;
                salveIdx++;
                letzteSalve = jetzt;
                SchussGezaehlt(jetzt);
            }
            bool laser = !laedt && jetzt - laserSeit < Puls;
            Sende(Flug.Ziel[Flug.LadeQuelle], laser);

            // Der kurze Druck geht zusaetzlich auf die Ladeschuss-Taste - im Spiel ist
            // das Xbox-A, und genau damit werden Menues bestaetigt und Zwischensequenzen
            // uebersprungen. Zum Laden reicht ein so kurzer Impuls nicht.
            // Menue-Druck haengt NICHT mehr an der Laserdauer: er folgt dem Finger und
            // bleibt mindestens 160 ms stehen, damit Zwischensequenzen ihn sicher sehen.
            bool menuA = menuImpuls && (gedrueckt || jetzt < menuBis);
            if (!gedrueckt && jetzt >= menuBis) menuImpuls = false;
            Sende(Flug.LadeZiel, laedt || menuA);
        }

        // Zaehlt die tatsaechlich abgesetzten Schuesse, damit sichtbar wird, ob ein
        // gefuehlter Deckel von hier kommt oder vom Spiel.
        readonly Queue<int> schuesse = new Queue<int>();
        volatile int rateJetzt, ratePeak;
        void SchussGezaehlt(int jetzt)
        {
            schuesse.Enqueue(jetzt);
            while (schuesse.Count > 0 && jetzt - schuesse.Peek() > 1000) schuesse.Dequeue();
            rateJetzt = schuesse.Count;
            if (rateJetzt > ratePeak) ratePeak = rateJetzt;
        }

        void Sende(string token, bool an)
        {
            if (string.IsNullOrEmpty(token) || token == "none") return;
            if (token.StartsWith("btn:")) xpad.Knopf(token.Substring(4), an);
            else if (token.StartsWith("sld:")) xpad.Regler(token.Substring(4), an ? 1.0 : 0.0);
        }

        // -------------------------------------------------------------- Einlernen

        void LernenStarten()
        {
            if (aktuell == null) return;
            xpad.Trennen();
            lernLayout = new PadLayout();
            lernLayout.Kennung = aktuell.Kennung;
            lernLayout.Name = "Learned  " + aktuell.Kennung;
            lernLayout.Totzone = 3000;
            lernLayout.Verstaerkung = 100;
            lock (sperre) lernBasis = letzte.Clone();
            zustand = "lernen";
            lernIndex = 0;
        }

        void LernSchritt(RawState s)
        {
            if (lernBasis == null) { lernBasis = s.Clone(); return; }
            int idx = lernIndex;
            if (idx >= Ctrl.Liste.Length)
            {
                lernIndex = -1;
                lernLayout.Speichern();
                layout = lernLayout;
                geraetName = lernLayout.Name;
                if (!xpad.Verbinden()) { zustand = "fehler"; meldung = xpad.Fehler; return; }
                zustand = "bereit";
                return;
            }

            Quelle gefunden = null;
            uint neu = s.Buttons & ~lernBasis.Buttons;
            if (neu != 0)
                for (int b = 0; b < 32; b++)
                    if ((neu & (1u << b)) != 0) { gefunden = Quelle.Btn(b); break; }
            if (gefunden == null && s.Pov != lernBasis.Pov && s.Pov <= 36000) gefunden = Quelle.Pov(s.Pov);
            if (gefunden == null)
                for (int a = 0; a < 6; a++)
                {
                    int d = s.Axis(a) - lernBasis.Axis(a);
                    if (Math.Abs(d) > 16000) { gefunden = Quelle.Ax(a, d > 0 ? 1 : -1); break; }
                }
            if (gefunden == null) return;

            string id = Ctrl.Liste[idx][0];
            lernLayout.Q[id] = gefunden;
            if (gefunden.Art == "axis")
            {
                string gegen = Gegenrichtung(id);
                if (gegen != null && !lernLayout.Q.ContainsKey(gegen))
                    lernLayout.Q[gegen] = Quelle.Ax(gefunden.Nr, -gefunden.Vorzeichen);
            }
            lernIndex = idx + 1;

            // warten, bis wieder losgelassen
            int schutz = 0;
            while (running && schutz++ < 700)
            {
                RawState n = Lies(aktuell.Id);
                bool haelt = false;
                if (n.Ok)
                {
                    if (gefunden.Art == "btn") haelt = (n.Buttons & (1u << gefunden.Nr)) != 0;
                    else if (gefunden.Art == "pov") haelt = n.Pov <= 36000;
                    else haelt = Math.Abs(n.Axis(gefunden.Nr) - lernBasis.Axis(gefunden.Nr)) > 9000;
                }
                if (!haelt) break;
                Thread.Sleep(8);
            }
            lernBasis = Lies(aktuell.Id);
        }

        static string Gegenrichtung(string id)
        {
            switch (id)
            {
                case "StickUp": return "StickDown";
                case "StickDown": return "StickUp";
                case "StickLeft": return "StickRight";
                case "StickRight": return "StickLeft";
            }
            return null;
        }

        // -------------------------------------------------------------- Anzeige

        void UiTakt(object sender, EventArgs e)
        {
            pad.Aktiv = new HashSet<string>(aktivJetzt);
            pad.StickX = stickX; pad.StickY = stickY;
            pad.Highlight = (lernIndex >= 0 && lernIndex < Ctrl.Liste.Length) ? Ctrl.Liste[lernIndex][0] : null;
            pad.Invalidate();

            btLernen.Visible = (zustand == "lernen_noetig") || (zustand == "bereit" && aktuell != null);
            btLernen.Text = zustand == "lernen_noetig" ? "Teach controller" : "Teach again";
            btPause.Enabled = zustand == "bereit" || pausiert;
            btNeuAnmelden.Enabled = zustand == "bereit";

            btTreiber.Visible = treiberFehlt;
            if (treiberFehlt)
            {
                btLernen.Visible = false; btPause.Enabled = false;
                btNeuAnmelden.Enabled = false;
                btTreiber.Enabled = !installiertGerade;
                if (installiertGerade)
                    band.Setze(Farbe.Gelb, "Setting up",
                        "Installing the controller driver. Confirm the Windows prompt if it appears.");
                else
                    band.Setze(Farbe.Rot, "One click and you are ready",
                        treiberMeldung == ""
                            ? "Skybridge 64 needs a small driver for the virtual controller. It is in this folder - just press the button."
                            : treiberMeldung);
                return;
            }

            if (pausiert)
            {
                band.Setze(Farbe.Blass, "Paused", "Press F8 or the button below to resume.");
                return;
            }

            switch (zustand)
            {
                case "suchen":
                    band.Setze(Farbe.Gelb, "Waiting for an N64 controller",
                        "Plug in an adapter or connect the wireless pad — the rest happens by itself.");
                    break;
                case "lernen_noetig":
                    band.Setze(Farbe.Blau, geraetName,
                        "This model is new to me. Teach it once and it works instantly from then on.");
                    break;
                case "lernen":
                    if (lernIndex >= 0 && lernIndex < Ctrl.Liste.Length)
                        band.Setze(Farbe.Gelb, "Press:  " + Ctrl.Liste[lernIndex][1],
                            "Step " + (lernIndex + 1) + " of " + Ctrl.Liste.Length +
                            " — the blinking button on the picture shows which one.");
                    break;
                case "bereit":
                    // Die Spielernummer ist reine Auskunft. Das Spiel nimmt jeden
                    // angemeldeten Pad an, nicht nur Platz 1 - im Spiel nachgeprueft.
                    band.Setze(Farbe.Gruen, "Ready — Flight controls active",
                        geraetName
                        + (xpad.SpielerNr >= 0 ? "   ·   player " + (xpad.SpielerNr + 1) : "")
                        );
                    break;
                case "fehler":
                    band.Setze(Farbe.Rot, geraetName == "" ? "Problem" : geraetName, meldung);
                    break;
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Nefarius.ViGEm.Client.dll liegt neben der EXE und wird ganz normal
            // geladen. Sie steckte frueher als Ressource drin und wurde per
            // Assembly.Load(byte[]) aus dem Speicher geholt - ein Muster, das
            // Virenscanner von Packern kennen und entsprechend bewerten.
            //
            // Fehlt sie, scheitert schon das Laden des Typs XPad - vor jeder Zeile
            // eigenem Code. Ohne dieses Netz startet das Programm dann wortlos gar
            // nicht, und der haeufigste Anwenderfehler (nur die EXE aus der ZIP
            // herausgezogen) sieht aus wie ein kaputtes Programm.
            try
            {
                Starte();
            }
            catch (System.IO.FileNotFoundException ex) { Fehlt(ex); }
            catch (System.IO.FileLoadException ex) { Fehlt(ex); }
            catch (TypeInitializationException ex) { Fehlt(ex); }
            catch (BadImageFormatException ex) { Fehlt(ex); }
        }

        static void Fehlt(Exception ex)
        {
            MessageBox.Show(
                "Skybridge 64 cannot start because a file it needs is missing.\r\n\r\n" +
                "Keep all files from the ZIP together in one folder:\r\n\r\n" +
                "    Skybridge64.exe\r\n" +
                "    Nefarius.ViGEm.Client.dll\r\n" +
                "    ViGEmBus_1.22.0_x64_x86_arm64.exe\r\n\r\n" +
                "Details: " + ex.Message,
                "Skybridge 64", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void Starte()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
