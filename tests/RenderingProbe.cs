using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class RenderingProbe
{
    [DllImport("user32.dll")] public static extern int GetGuiResources(IntPtr process, int flag);
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    public static void Paint(Form form, Bitmap target, Rectangle clip)
    {
        using (Graphics g = Graphics.FromImage(target))
        using (var args = new PaintEventArgs(g, clip))
        {
            g.SetClip(clip);
            form.GetType().GetMethod("OnPaint", Flags).Invoke(form, new object[] { args });
        }
    }

    public static int Differences(Bitmap first, Bitmap second, Rectangle region, bool outside)
    {
        Rectangle bounds = new Rectangle(Point.Empty, first.Size);
        BitmapData a = first.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData b = second.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int[] left = new int[first.Width], right = new int[first.Width];
            int count = 0;
            for (int y = 0; y < first.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(a.Scan0, y * a.Stride), left, 0, left.Length);
                Marshal.Copy(IntPtr.Add(b.Scan0, y * b.Stride), right, 0, right.Length);
                for (int x = 0; x < first.Width; x++)
                    if (region.Contains(x, y) != outside && (left[x] & 0xFFFFFF) != (right[x] & 0xFFFFFF)) count++;
            }
            return count;
        }
        finally { first.UnlockBits(a); second.UnlockBits(b); }
    }
}
