// Snacks you can give Clawd: little pixel-art items in their own draggable window, with gravity.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Clawd
{
    static class Food
    {
        public static readonly string[] Kinds = { "cookie", "pizza", "apple", "donut" };
        static int[] Palette(string kind)
        {
            switch (kind)
            {
                case "cookie": return new[] { Pal.Outline, Pal.C(214, 160, 92), Pal.C(96, 58, 34), Pal.C(236, 190, 120) };
                case "pizza": return new[] { Pal.Outline, Pal.C(196, 130, 60), Pal.C(250, 206, 90), Pal.C(214, 60, 52) };
                case "apple": return new[] { Pal.Outline, Pal.C(222, 48, 52), Pal.C(90, 170, 70), Pal.C(255, 160, 160) };
                default: return new[] { Pal.Outline, Pal.C(200, 140, 80), Pal.C(246, 140, 190), Pal.C(255, 240, 120) };
            }
        }
        // o = outline, a/b/c = the item's three colours
        static string[] Rows(string kind)
        {
            switch (kind)
            {
                case "cookie": return new[] { ".oooo.", "oaacao", "oabaao", "oaaabo", "ocabao", ".oooo." };
                case "pizza": return new[] { "oooooo", "oabcbo", ".ocbo.", ".obbo.", "..oo..", "......" };
                case "apple": return new[] { "...b..", ".oobo.", "oacaao", "oaaaao", "oaaaao", ".oooo." };
                default: return new[] { ".oooo.", "obbcbo", "ob..bo", "ob..bo", "oaaaao", ".oooo." };
            }
        }
        // draw at (x, y) in cells; 'left' (0..1) is how much is still uneaten (bites come off the right side)
        public static void DrawInto(Cells g, string kind, double x, double y, double left)
        {
            string[] rows = Rows(kind); int[] pal = Palette(kind);
            int keep = (int)Math.Ceiling(6 * Math.Max(0, Math.Min(1, left)));
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < keep && c < rows[r].Length; c++)
                {
                    int k = "oabc".IndexOf(rows[r][c]);
                    if (k >= 0) g.Dot(x + c, y + r, pal[k]);
                }
            if (keep < 6 && keep > 0) for (int r = 1; r < 5; r += 2) g.Dot(x + keep - 1, y + r, pal[0]);   // bite marks
        }
        public static string Pick(Random rng) { return Kinds[rng.Next(Kinds.Length)]; }
    }

    // A snack lying around (or being dragged by you). Clawd eats it when he reaches it.
    class FoodItem
    {
        public string Kind; public double X, Y, VX, VY; public bool Dragging, Grounded, Gone; public double Left = 1.0;
        public int Age;
        readonly LayeredWindow win = new LayeredWindow(false);
        readonly Cells cells = new Cells(8, 8);
        double dragDX, dragDY; Point lastCursor; double throwVX, throwVY;

        public FoodItem(string kind, double x, double y)
        {
            Kind = kind; X = x; Y = y;
            win.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { Dragging = true; Grounded = false; Point c = Cursor.Position; dragDX = X - c.X; dragDY = Y - c.Y; lastCursor = c; } };
            win.MouseUp += (s, e) => { if (Dragging) { Dragging = false; VX = throwVX; VY = throwVY; } };
            win.Cursor = Cursors.Hand;
        }
        public void Tick(World w)
        {
            Age++;
            if (Dragging)
            {
                Point c = Cursor.Position;
                throwVX = (c.X - lastCursor.X) * 0.8; throwVY = (c.Y - lastCursor.Y) * 0.8; lastCursor = c;
                X = c.X + dragDX; Y = c.Y + dragDY;
            }
            else if (!Grounded)
            {
                VY += 0.22 * w.K;
                double ground = w.GroundAt(X + VX, Y - 2, Y + VY + 2);   // whatever it would hit this step
                X += VX; Y += VY; VX *= 0.99;
                if (Y >= ground) { Y = ground; if (Math.Abs(VY) > 4 * w.K) { VY = -VY * 0.35; VX *= 0.6; } else { VY = 0; VX = 0; Grounded = true; } }
                if (X < w.Work.Left + 10) { X = w.Work.Left + 10; VX = Math.Abs(VX) * 0.5; }
                if (X > w.Work.Right - 10) { X = w.Work.Right - 10; VX = -Math.Abs(VX) * 0.5; }
            }
            else if (Y < w.Work.Bottom - 1 && w.GroundAt(X, Y - 2, Y + 2) > Y + 2) Grounded = false;   // its window moved away
        }
        public void Draw(int S)
        {
            cells.Clear(); Food.DrawInto(cells, Kind, 1, 1, Left);
            int size = 8 * S;
            win.Surf.Ensure(size, size); win.Surf.Clear();
            Graphics g = win.Surf.G;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(cells.ToBitmap(), new Rectangle(0, 0, size, size), new Rectangle(0, 0, 8, 8), GraphicsUnit.Pixel);
            win.Present((int)(X - size / 2), (int)(Y - 7 * S));
        }
        public void KeepOnTop() { win.KeepOnTop(); }
        public void Remove() { Gone = true; win.Close(); win.Surf.Dispose(); }
    }
}
