// Entfernt die eingepraegte Herstellermarke aus dem Controllerfoto.
// Verfahren: je Spalte den Farbverlauf ober- und unterhalb des Bereichs abgreifen und
// dazwischen interpolieren. Dunkle Pixel (Kabel, Kante) werden als Stuetzpunkt uebersprungen.
using System;
using System.Drawing;
using System.Drawing.Imaging;

class LogoWeg
{
    static void Main(string[] a)
    {
        string src = a[0], dst = a[1];
        int x1 = int.Parse(a[2]), y1 = int.Parse(a[3]), x2 = int.Parse(a[4]), y2 = int.Parse(a[5]);
        bool quer = a.Length > 6 && a[6] == "h";   // waagerecht statt senkrecht interpolieren

        using (Bitmap orig = new Bitmap(src))
        {
            Bitmap bmp = new Bitmap(orig.Width, orig.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp)) g.DrawImage(orig, 0, 0, orig.Width, orig.Height);

            Func<Color, int> hell = delegate(Color c) { return (c.R + c.G + c.B) / 3; };

            if (quer)
            {
                for (int y = y1; y <= y2; y++)
                {
                    Color links = Color.Empty; int xl = 0;
                    for (int x = x1 - 1; x >= Math.Max(0, x1 - 80); x--)
                    { Color c = bmp.GetPixel(x, y); if (c.A > 200 && hell(c) > 110) { links = c; xl = x; break; } }
                    Color rechts = Color.Empty; int xr = 0;
                    for (int x = x2 + 1; x < Math.Min(bmp.Width, x2 + 80); x++)
                    { Color c = bmp.GetPixel(x, y); if (c.A > 200 && hell(c) > 110) { rechts = c; xr = x; break; } }
                    if (links == Color.Empty || rechts == Color.Empty) continue;
                    for (int x = x1; x <= x2; x++)
                    {
                        Color jetzt = bmp.GetPixel(x, y);
                        if (jetzt.A < 40) continue;
                        double t2 = (double)(x - xl) / (xr - xl);
                        bmp.SetPixel(x, y, Color.FromArgb(jetzt.A,
                            (int)Math.Round(links.R + (rechts.R - links.R) * t2),
                            (int)Math.Round(links.G + (rechts.G - links.G) * t2),
                            (int)Math.Round(links.B + (rechts.B - links.B) * t2)));
                    }
                }
            }
            else
            for (int x = x1; x <= x2; x++)
            {
                // Stuetzpunkt oben: erster ausreichend heller Pixel oberhalb
                Color oben = Color.Empty; int yo = 0;
                for (int y = y1 - 1; y >= Math.Max(0, y1 - 60); y--)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A > 200 && hell(c) > 110) { oben = c; yo = y; break; }
                }
                Color unten = Color.Empty; int yu = 0;
                for (int y = y2 + 1; y < Math.Min(bmp.Height, y2 + 60); y++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A > 200 && hell(c) > 110) { unten = c; yu = y; break; }
                }
                if (oben == Color.Empty || unten == Color.Empty) continue;

                for (int y = y1; y <= y2; y++)
                {
                    Color jetzt = bmp.GetPixel(x, y);
                    if (jetzt.A < 40) continue;              // freigestellter Rand bleibt
                    double t = (double)(y - yo) / (yu - yo);
                    int r = (int)Math.Round(oben.R + (unten.R - oben.R) * t);
                    int gr = (int)Math.Round(oben.G + (unten.G - oben.G) * t);
                    int b = (int)Math.Round(oben.B + (unten.B - oben.B) * t);
                    bmp.SetPixel(x, y, Color.FromArgb(jetzt.A, r, gr, b));
                }
            }

            // sanft glaetten, damit keine Spaltenstreifen stehenbleiben
            Bitmap kopie = (Bitmap)bmp.Clone();
            for (int x = x1; x <= x2; x++)
                for (int y = y1; y <= y2; y++)
                {
                    if (kopie.GetPixel(x, y).A < 40) continue;
                    int sr = 0, sg = 0, sb = 0, n = 0;
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= kopie.Width || ny >= kopie.Height) continue;
                            Color c = kopie.GetPixel(nx, ny);
                            if (c.A < 40) continue;
                            sr += c.R; sg += c.G; sb += c.B; n++;
                        }
                    if (n == 0) continue;
                    Color alt = bmp.GetPixel(x, y);
                    bmp.SetPixel(x, y, Color.FromArgb(alt.A, sr / n, sg / n, sb / n));
                }
            kopie.Dispose();

            bmp.Save(dst, ImageFormat.Png);
            bmp.Dispose();
            Console.WriteLine("Marke entfernt: {0},{1} bis {2},{3}", x1, y1, x2, y2);
        }
    }
}
