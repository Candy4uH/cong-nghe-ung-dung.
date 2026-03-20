namespace Object_Detection;

// Vai tro: Cung cap ham hinh hoc, hien tai la tinh IoU cho bbox.
internal static class Geometry
{
    public static double IoU(Detection a, Detection b)
        => IoU(a.X1, a.Y1, a.X2, a.Y2, b.X1, b.Y1, b.X2, b.Y2);

    public static double IoU(Detection a, BoundingBox b)
        => IoU(a.X1, a.Y1, a.X2, a.Y2, b.X1, b.Y1, b.X2, b.Y2);

    public static double IoU(int ax1, int ay1, int ax2, int ay2, int bx1, int by1, int bx2, int by2)
    {
        var ix1 = Math.Max(ax1, bx1);
        var iy1 = Math.Max(ay1, by1);
        var ix2 = Math.Min(ax2, bx2);
        var iy2 = Math.Min(ay2, by2);

        if (ix2 < ix1 || iy2 < iy1)
        {
            return 0;
        }

        var inter = (ix2 - ix1 + 1.0) * (iy2 - iy1 + 1.0);
        var areaA = (ax2 - ax1 + 1.0) * (ay2 - ay1 + 1.0);
        var areaB = (bx2 - bx1 + 1.0) * (by2 - by1 + 1.0);
        return inter / Math.Max(1e-8, areaA + areaB - inter);
    }
}
