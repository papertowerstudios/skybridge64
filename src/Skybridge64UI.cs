// Oberflaeche von SKYBRIDGE 64 im Stil eines N64-Cockpit-HUD:
// abgeschraegte Rahmen, Eckklammern, schmale Versalien, Cyan auf Nachtblau.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Skybridge64
{
    internal static class Farbe
    {
        public static readonly Color Grund = Color.FromArgb(5, 7, 15);
        public static readonly Color Karte = Color.FromArgb(10, 21, 38);
        public static readonly Color Linie = Color.FromArgb(46, 127, 168);
        public static readonly Color Cyan = Color.FromArgb(79, 216, 255);
        public static readonly Color Schrift = Color.FromArgb(214, 236, 255);
        public static readonly Color Blass = Color.FromArgb(110, 140, 166);
        public static readonly Color Gruen = Color.FromArgb(140, 255, 90);
        public static readonly Color Gelb = Color.FromArgb(255, 197, 61);
        public static readonly Color Rot = Color.FromArgb(255, 90, 80);
        public static readonly Color Blau = Color.FromArgb(79, 216, 255);
    }

    // Zeichenwerkzeug fuer den HUD-Stil
    internal static class Hud
    {
        public const string Titelschrift = "Bahnschrift SemiBold Condensed";
        public const string Textschrift = "Bahnschrift SemiCondensed";
        public const string Datenschrift = "Consolas";

        public static Font T(float g) { return new Font(Titelschrift, g); }
        public static Font S(float g) { return new Font(Textschrift, g); }
        public static Font D(float g) { return new Font(Datenschrift, g); }

        // Rechteck mit abgeschraegten Ecken - die Grundform jeder N64-HUD-Tafel
        public static GraphicsPath Kante(RectangleF r, float s)
        {
            GraphicsPath p = new GraphicsPath();
            if (s <= 0.5f || r.Width < s * 3 || r.Height < s * 3) { p.AddRectangle(r); return p; }
            p.AddLine(r.X + s, r.Y, r.Right - s, r.Y);
            p.AddLine(r.Right - s, r.Y, r.Right, r.Y + s);
            p.AddLine(r.Right, r.Y + s, r.Right, r.Bottom - s);
            p.AddLine(r.Right, r.Bottom - s, r.Right - s, r.Bottom);
            p.AddLine(r.Right - s, r.Bottom, r.X + s, r.Bottom);
            p.AddLine(r.X + s, r.Bottom, r.X, r.Bottom - s);
            p.AddLine(r.X, r.Bottom - s, r.X, r.Y + s);
            p.CloseFigure();
            return p;
        }

        // Eckklammern wie im Zielsucher
        public static void Klammern(Graphics g, RectangleF r, Color c, float laenge, float dicke)
        {
            using (Pen p = new Pen(c, dicke))
            {
                g.DrawLine(p, r.X, r.Y + laenge, r.X, r.Y); g.DrawLine(p, r.X, r.Y, r.X + laenge, r.Y);
                g.DrawLine(p, r.Right - laenge, r.Y, r.Right, r.Y); g.DrawLine(p, r.Right, r.Y, r.Right, r.Y + laenge);
                g.DrawLine(p, r.Right, r.Bottom - laenge, r.Right, r.Bottom); g.DrawLine(p, r.Right, r.Bottom, r.Right - laenge, r.Bottom);
                g.DrawLine(p, r.X + laenge, r.Bottom, r.X, r.Bottom); g.DrawLine(p, r.X, r.Bottom, r.X, r.Bottom - laenge);
            }
        }

        // Versalien mit Sperrung - im HUD steht alles gesperrt und gross
        // Zeichenweise setzen, damit sich die Sperrung steuern laesst. Das Leerzeichen
        // braucht eine feste Breite - gemessen faellt es sonst auf null zusammen.
        public static float Gesperrt(Graphics g, string text, Font f, Color c, float x, float y, float sperre)
        {
            using (SolidBrush b = new SolidBrush(c))
                foreach (char ch in text)
                {
                    if (ch == ' ') { x += f.Size * 0.62f + sperre; continue; }
                    string s = ch.ToString();
                    g.DrawString(s, f, b, x, y, StringFormat.GenericTypographic);
                    x += g.MeasureString(s, f, PointF.Empty, StringFormat.GenericTypographic).Width + sperre;
                }
            return x;
        }

        public static float BreiteGesperrt(Graphics g, string text, Font f, float sperre)
        {
            float w = 0;
            foreach (char ch in text)
            {
                if (ch == ' ') { w += f.Size * 0.62f + sperre; continue; }
                w += g.MeasureString(ch.ToString(), f, PointF.Empty, StringFormat.GenericTypographic).Width + sperre;
            }
            return w;
        }

        // Hintergrund: Nachtblau, feines Gitter, ein paar Sterne
        public static void Hintergrund(Graphics g, int w, int h, int ox, int oy)
        {
            using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, w), Math.Max(1, h)),
                       Color.FromArgb(9, 14, 28), Color.FromArgb(3, 4, 10), 90f))
                g.FillRectangle(bg, 0, 0, w, h);

            using (Pen gitter = new Pen(Color.FromArgb(15, 70, 130, 180)))
            {
                for (int x = -(ox % 44); x < w; x += 44) g.DrawLine(gitter, x, 0, x, h);
                for (int y = -(oy % 44); y < h; y += 44) g.DrawLine(gitter, 0, y, w, y);
            }
        }
    }

    // Knopf im HUD-Stil
    internal class HudKnopf : Button
    {
        bool drueber, gedrueckt;

        public HudKnopf()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            MouseEnter += delegate { drueber = true; Invalidate(); };
            MouseLeave += delegate { drueber = false; gedrueckt = false; Invalidate(); };
            MouseDown += delegate { gedrueckt = true; Invalidate(); };
            MouseUp += delegate { gedrueckt = false; Invalidate(); };
            EnabledChanged += delegate { Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Hud.Hintergrund(g, Width, Height, Left, Top);

            RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
            bool aus = !Enabled;
            Color rand = aus ? Color.FromArgb(42, 56, 72) : (drueber ? Farbe.Cyan : Farbe.Linie);
            Color fuell = gedrueckt ? Color.FromArgb(46, 79, 216, 255)
                                    : (drueber ? Color.FromArgb(24, 79, 216, 255) : Color.FromArgb(130, 10, 21, 38));

            using (GraphicsPath p = Hud.Kante(r, 8))
            using (SolidBrush b = new SolidBrush(fuell))
            using (Pen pen = new Pen(rand, 1.4f))
            { g.FillPath(b, p); g.DrawPath(pen, p); }

            using (Font f = Hud.T(9.5f))
            {
                string s = (Text ?? "").ToUpperInvariant();
                // Der Sperrabstand hinter dem letzten Zeichen gehoert nicht zur Breite,
                // sonst rutscht die Zeile nach links.
                float breite = Hud.BreiteGesperrt(g, s, f, 1.2f) - 1.2f;
                // Hoehe ueber die Glyphen messen, nicht ueber Font.Height - das enthaelt
                // den Zeilendurchschuss und schiebt den Text nach oben.
                float hoehe = g.MeasureString("H", f, PointF.Empty, StringFormat.GenericTypographic).Height;
                Hud.Gesperrt(g, s, f, aus ? Color.FromArgb(76, 92, 108) : Farbe.Schrift,
                             (float)Math.Round((Width - breite) / 2f),
                             (float)Math.Round((Height - hoehe) / 2f), 1.2f);
            }
        }
    }

    // ------------------------------------------------------------------ Pad-Foto

    internal class PadView : Panel
    {
        public HashSet<string> Aktiv = new HashSet<string>();
        public double StickX, StickY;
        public string Highlight;
        bool blink;
        Image foto;

        const float ImgW = 1246f, ImgH = 982f;

        class Punkt { public float X, Y, R; public Punkt(float x, float y, float r) { X = x; Y = y; R = r; } }
        static readonly Dictionary<string, Punkt> punkte = new Dictionary<string, Punkt>();
        static readonly Punkt stick = new Punkt(557, 388, 60);
        static readonly Punkt chipL = new Punkt(262, 86, 30);
        static readonly Punkt chipR = new Punkt(1120, 168, 30);
        static readonly Punkt chipZ = new Punkt(650, 748, 30);

        static PadView()
        {
            punkte["A"] = new Punkt(905, 412, 52);
            punkte["B"] = new Punkt(855, 321, 47);
            punkte["Start"] = new Punkt(649, 296, 42);
            punkte["CUp"] = new Punkt(1063, 278, 39);
            punkte["CDown"] = new Punkt(1015, 375, 39);
            punkte["CLeft"] = new Punkt(985, 297, 39);
            punkte["CRight"] = new Punkt(1092, 358, 39);
            punkte["DUp"] = new Punkt(372, 152, 33);
            punkte["DDown"] = new Punkt(345, 245, 33);
            punkte["DLeft"] = new Punkt(300, 190, 33);
            punkte["DRight"] = new Punkt(415, 202, 33);
        }

        public PadView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Farbe.Grund;
            // Foto steckt in der EXE - so bleibt das Programm eine einzelne Datei.
            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream("n64-pad.png"))
                    if (s != null)
                    {
                        byte[] roh = new byte[s.Length];
                        s.Read(roh, 0, roh.Length);
                        foto = Image.FromStream(new MemoryStream(roh));
                    }
            }
            catch { foto = null; }
            if (foto == null)
                try
                {
                    string p = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "img");
                    p = Path.Combine(p, "n64-pad.png");
                    if (File.Exists(p)) foto = Image.FromStream(new MemoryStream(File.ReadAllBytes(p)));
                }
                catch { foto = null; }

            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 380; t.Tick += delegate { blink = !blink; Invalidate(); }; t.Start();
        }

        bool An(string id) { return Aktiv.Contains(id); }
        bool Hi(string id) { return Highlight == id && blink; }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int w = Math.Max(1, Width), h = Math.Max(1, Height);
            Hud.Hintergrund(g, w, h, Left, Top);

            RectangleF rahmen = new RectangleF(1, 1, w - 3, h - 3);
            using (GraphicsPath p = Hud.Kante(rahmen, 16))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 8, 16, 30)))
            using (Pen pen = new Pen(Color.FromArgb(70, Farbe.Linie), 1.2f))
            { g.FillPath(b, p); g.DrawPath(pen, p); }
            Hud.Klammern(g, new RectangleF(10, 10, w - 21, h - 21), Color.FromArgb(110, Farbe.Cyan), 16, 1.5f);

            if (foto == null)
            {
                using (Font f = Hud.S(10f))
                using (SolidBrush b = new SolidBrush(Farbe.Blass))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("img\\n64-pad.png missing", f, b, new RectangleF(0, 0, w, h), sf);
                }
                return;
            }

            float s = Math.Min(w / ImgW, h / ImgH) * 0.94f;
            float ox = (w - ImgW * s) / 2f, oy = (h - ImgH * s) / 2f;
            g.DrawImage(foto, ox, oy, ImgW * s, ImgH * s);

            foreach (KeyValuePair<string, Punkt> kv in punkte) Marke(g, kv.Key, kv.Value, ox, oy, s);
            Stick(g, ox, oy, s);
            Plakette(g, "L", chipL, ox, oy, s);
            Plakette(g, "R", chipR, ox, oy, s);
            Plakette(g, "Z", chipZ, ox, oy, s);
        }

        void Marke(Graphics g, string id, Punkt p, float ox, float oy, float s)
        {
            bool an = An(id), hi = Hi(id);
            if (!an && !hi) return;
            Color c = hi ? Farbe.Gelb : Farbe.Cyan;
            float cx = ox + p.X * s, cy = oy + p.Y * s, r = p.R * s;
            Schein(g, cx, cy, r * 2.2f, c);
            using (Pen pen = new Pen(Color.FromArgb(240, c), Math.Max(2f, 3.2f * s)))
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
            Hud.Klammern(g, new RectangleF(cx - r * 1.55f, cy - r * 1.55f, r * 3.1f, r * 3.1f),
                         Color.FromArgb(160, c), r * 0.65f, Math.Max(1.2f, 1.7f * s));
        }

        static void Schein(Graphics g, float cx, float cy, float r, Color c)
        {
            if (r <= 1) return;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using (PathGradientBrush pb = new PathGradientBrush(p))
                {
                    pb.CenterPoint = new PointF(cx, cy);
                    pb.CenterColor = Color.FromArgb(140, c);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, c) };
                    g.FillPath(pb, p);
                }
            }
        }

        void Stick(Graphics g, float ox, float oy, float s)
        {
            float cx = ox + stick.X * s, cy = oy + stick.Y * s;
            float reach = 62f * s, squash = 0.62f;
            bool hi = Hi("StickUp") || Hi("StickDown") || Hi("StickLeft") || Hi("StickRight");
            bool bewegt = Math.Abs(StickX) > 0.03 || Math.Abs(StickY) > 0.03;

            using (Pen p = new Pen(Color.FromArgb(hi ? 190 : 90, hi ? Farbe.Gelb : Farbe.Cyan), Math.Max(1.2f, 1.6f * s)))
            {
                p.DashStyle = DashStyle.Dot;
                g.DrawEllipse(p, cx - reach, cy - reach * squash, reach * 2, reach * 2 * squash);
            }
            if (!bewegt && !hi) return;

            float kx = cx + (float)(StickX * reach), ky = cy + (float)(StickY * reach * squash);
            Color c = hi ? Farbe.Gelb : Farbe.Cyan;
            using (Pen p = new Pen(Color.FromArgb(170, c), Math.Max(1.5f, 2f * s))) g.DrawLine(p, cx, cy, kx, ky);
            Schein(g, kx, ky, 44f * s, c);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(245, c)))
                g.FillEllipse(b, kx - 9f * s, ky - 9f * s, 18f * s, 18f * s);
        }

        void Plakette(Graphics g, string id, Punkt p, float ox, float oy, float s)
        {
            bool an = An(id), hi = Hi(id);
            float cx = ox + p.X * s, cy = oy + p.Y * s;
            float rw = 48f * s, rh = 30f * s;
            RectangleF r = new RectangleF(cx - rw / 2, cy - rh / 2, rw, rh);
            Color akzent = an ? Farbe.Cyan : (hi ? Farbe.Gelb : Farbe.Linie);
            Color fuell = an || hi ? Color.FromArgb(70, akzent) : Color.FromArgb(170, 8, 16, 30);
            if (an || hi) Schein(g, cx, cy, 44f * s, akzent);

            using (GraphicsPath gp = Hud.Kante(r, 7f * s))
            using (SolidBrush b = new SolidBrush(fuell))
            using (Pen pen = new Pen(Color.FromArgb(an || hi ? 245 : 190, akzent), Math.Max(1.2f, 1.6f * s)))
            { g.FillPath(b, gp); g.DrawPath(pen, gp); }

            using (Font f = Hud.T(Math.Max(7f, 14f * s)))
            using (SolidBrush b = new SolidBrush(an || hi ? Color.White : Farbe.Schrift))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center;
                g.DrawString(id, f, b, r, sf);
            }
        }
    }

    // ------------------------------------------------------------------ Statusband

    internal class StatusBand : Panel
    {
        public Color Ton = Farbe.Gelb;
        public string Kopf = "";
        public string Zeile = "";
        bool puls;

        public StatusBand()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Farbe.Grund;
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 620; t.Tick += delegate { puls = !puls; Invalidate(); }; t.Start();
        }

        public void Setze(Color ton, string kopf, string zeile)
        {
            if (Ton == ton && Kopf == kopf && Zeile == zeile) return;
            Ton = ton; Kopf = kopf; Zeile = zeile; Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Hud.Hintergrund(g, Width, Height, Left, Top);

            RectangleF r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (GraphicsPath p = Hud.Kante(r, 12))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(28, Ton)))
            using (Pen pen = new Pen(Color.FromArgb(150, Ton), 1.4f))
            { g.FillPath(b, p); g.DrawPath(pen, p); }
            Hud.Klammern(g, new RectangleF(9, 9, Width - 19, Height - 19), Color.FromArgb(90, Ton), 14, 1.4f);

            float cy = Height / 2f;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(puls ? 60 : 30, Ton)))
                g.FillEllipse(b, 18, cy - 13, 26, 26);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(puls ? 255 : 170, Ton)))
                g.FillEllipse(b, 25, cy - 6, 12, 12);
            using (Pen p = new Pen(Color.FromArgb(110, Ton), 1.2f))
                g.DrawLine(p, 58, 13, 58, Height - 13);

            using (Font f1 = Hud.T(15f))
            using (Font f2 = Hud.S(10f))
            {
                if (string.IsNullOrEmpty(Zeile))
                    Hud.Gesperrt(g, Kopf.ToUpperInvariant(), f1, Farbe.Schrift, 72, cy - f1.Height / 2f, 1.5f);
                else
                {
                    Hud.Gesperrt(g, Kopf.ToUpperInvariant(), f1, Farbe.Schrift, 72, cy - 22, 1.5f);
                    using (SolidBrush b = new SolidBrush(Farbe.Blass))
                        g.DrawString(Zeile, f2, b, 73, cy + 2);
                }
            }
        }
    }
}
