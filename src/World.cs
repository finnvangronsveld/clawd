// Where the pet is: which monitor, how big he should be there (DPI + size setting), where the floor is.
using System;

namespace Flippy
{
    class World
    {
        public Native.RECT Mon, Work;
        public int S = 3, Dpi = 96;
        public double K = 1;
        public double UserScale = 1.0;

        public void Update(double x, double y)
        {
            Native.MonitorAt((int)x, (int)Math.Min(y - 2, int.MaxValue), out Mon, out Work);
            Dpi = Native.DpiAt((int)x, (int)y - 2);
            S = Math.Max(2, (int)Math.Round(3.0 * Dpi / 96.0 * UserScale));
            K = S / 3.0;
        }
        public double Floor { get { return Work.Bottom; } }

        // the surface something falling from yFrom to yTo at x would land on (a window top, or the floor)
        public double GroundAt(double x, double yFrom, double yTo)
        {
            if (yTo >= yFrom)
            {
                IntPtr h = Native.FindTop((int)x, 4, (int)yFrom - 1, (int)yTo);
                if (h != IntPtr.Zero) return Native.Rect(h).Top;
            }
            return Work.Bottom;
        }
    }
}
