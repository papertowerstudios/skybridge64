// Erzeugt das Programmsymbol fuer SKYBRIDGE 64 als .ico mit mehreren Groessen.
// Motiv: Deltafluegel-Silhouette auf dunkelblauem Grund - bei 16 Pixeln noch lesbar.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class IconBauer
{
    static readonly int[] Groessen = { 256, 128, 64, 48, 32, 24, 16 };

    static void Main(string[] a)
    {
        string ziel = a.Length > 0 ? a[0] : "skybridge.ico";
        List<byte[]> bilder = new List<byte[]>();
        List<bool> istPng = new List<bool>();
        foreach (int s in Groessen)
        {
            Bitmap b = Zeichne(s);
            // Nur 256 als PNG - kleinere Groessen klassisch als DIB, sonst koennen
            // .NET und aeltere Windows-Teile das Symbol nicht dekodieren.
            if (s >= 256) { bilder.Add(AlsPng(b)); istPng.Add(true); }
            else { bilder.Add(AlsDib(b)); istPng.Add(false); }
        }
        Schreibe(ziel, bilder);
        Console.WriteLine("Symbol geschrieben: {0} ({1} Groessen)", ziel, Groessen.Length);
    }

    static Bitmap Zeichne(int s)
    {
        Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            float rand = s * 0.02f;
            RectangleF box = new RectangleF(rand, rand, s - rand * 2, s - rand * 2);
            float radius = s * 0.22f;

            // Grund: tiefes Blau mit Verlauf
            using (GraphicsPath p = Rund(box, radius))
            using (LinearGradientBrush b = new LinearGradientBrush(box,
                       Color.FromArgb(255, 30, 58, 110), Color.FromArgb(255, 8, 16, 34), 65f))
                g.FillPath(b, p);

            // leichter Lichtrand oben
            using (GraphicsPath p = Rund(box, radius))
            using (Pen pen = new Pen(Color.FromArgb(70, 150, 190, 255), Math.Max(1f, s * 0.012f)))
                g.DrawPath(pen, p);

            // Sternenpunkte, nur bei groesseren Symbolen
            if (s >= 48)
            {
                using (SolidBrush st = new SolidBrush(Color.FromArgb(120, 210, 230, 255)))
                {
                    float d = s * 0.018f;
                    g.FillEllipse(st, s * 0.17f, s * 0.22f, d, d);
                    g.FillEllipse(st, s * 0.82f, s * 0.30f, d, d);
                    g.FillEllipse(st, s * 0.26f, s * 0.78f, d, d);
                    g.FillEllipse(st, s * 0.76f, s * 0.72f, d, d);
                }
            }

            // Deltafluegel: Pfeil mit gespreizten Fluegeln
            PointF[] wing = new PointF[] {
                P(s, 0.500f, 0.140f),
                P(s, 0.630f, 0.560f),
                P(s, 0.930f, 0.760f),
                P(s, 0.700f, 0.735f),
                P(s, 0.588f, 0.880f),
                P(s, 0.500f, 0.775f),
                P(s, 0.412f, 0.880f),
                P(s, 0.300f, 0.735f),
                P(s, 0.070f, 0.760f),
                P(s, 0.370f, 0.560f),
            };

            // Schein unter dem Flieger
            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddEllipse(s * 0.18f, s * 0.30f, s * 0.64f, s * 0.64f);
                using (PathGradientBrush pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = Color.FromArgb(90, 120, 190, 255);
                    pb.SurroundColors = new Color[] { Color.FromArgb(0, 120, 190, 255) };
                    g.FillPath(pb, gp);
                }
            }

            using (GraphicsPath gp = new GraphicsPath())
            {
                gp.AddPolygon(wing);
                using (LinearGradientBrush b = new LinearGradientBrush(
                           new RectangleF(0, s * 0.12f, s, s * 0.78f),
                           Color.White, Color.FromArgb(255, 138, 196, 255), 90f))
                    g.FillPath(b, gp);
                if (s >= 32)
                    using (Pen pen = new Pen(Color.FromArgb(160, 20, 40, 80), Math.Max(1f, s * 0.008f)))
                        g.DrawPath(pen, gp);
            }

            // Triebwerksglut
            if (s >= 24)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 196, 60)))
                    g.FillEllipse(b, s * 0.468f, s * 0.700f, s * 0.064f, s * 0.064f);
        }
        return bmp;
    }

    static PointF P(int s, float x, float y) { return new PointF(s * x, s * y); }

    static GraphicsPath Rund(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static byte[] AlsPng(Bitmap b)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            b.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    // Klassischer Symboleintrag: Kopf, 32-Bit-Pixel von unten nach oben, dann
    // die 1-Bit-Maske (bei Alphakanal komplett null).
    static byte[] AlsDib(Bitmap b)
    {
        int w = b.Width, h = b.Height;
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter x = new BinaryWriter(ms))
        {
            x.Write(40);                 // biSize
            x.Write(w);                  // biWidth
            x.Write(h * 2);              // biHeight: Bild + Maske
            x.Write((ushort)1);          // biPlanes
            x.Write((ushort)32);         // biBitCount
            x.Write(0);                  // biCompression
            x.Write(w * h * 4);          // biSizeImage
            x.Write(0); x.Write(0);      // Aufloesung
            x.Write(0); x.Write(0);      // Farbtabelle

            for (int y = h - 1; y >= 0; y--)
                for (int px = 0; px < w; px++)
                {
                    Color c = b.GetPixel(px, y);
                    x.Write(c.B); x.Write(c.G); x.Write(c.R); x.Write(c.A);
                }

            int maskZeile = ((w + 31) / 32) * 4;   // auf 4 Byte aufgerundet
            byte[] leer = new byte[maskZeile];
            for (int y = 0; y < h; y++) x.Write(leer);

            return ms.ToArray();
        }
    }

    // ICO-Datei
    static void Schreibe(string pfad, List<byte[]> bilder)
    {
        using (FileStream fs = new FileStream(pfad, FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write((ushort)0);                    // reserviert
            w.Write((ushort)1);                    // Typ: Symbol
            w.Write((ushort)bilder.Count);

            int offset = 6 + 16 * bilder.Count;
            for (int i = 0; i < bilder.Count; i++)
            {
                int s = Groessen[i];
                w.Write((byte)(s >= 256 ? 0 : s));  // Breite
                w.Write((byte)(s >= 256 ? 0 : s));  // Hoehe
                w.Write((byte)0);                   // Farbanzahl
                w.Write((byte)0);                   // reserviert
                w.Write((ushort)1);                 // Ebenen
                w.Write((ushort)32);                // Bit je Pixel
                w.Write(bilder[i].Length);
                w.Write(offset);
                offset += bilder[i].Length;
            }
            foreach (byte[] b in bilder) w.Write(b);
        }
    }
}
