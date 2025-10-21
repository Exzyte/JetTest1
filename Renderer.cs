using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace JetTest1
{
    /// Simple Renderer with image loading and drawing support.
    /// Internally maintains a framebuffer and can present it to a Win32 window.
    public class Renderer
    {
        private readonly int width;
        private readonly int height;
        private readonly byte[] framebuffer; // BGRA format
        private IntPtr hwnd;

        public Renderer(int width, int height)
        {
            this.width = width;
            this.height = height;
            framebuffer = new byte[width * height * 4];
        }

        /// Sets the native window handle to target.
        public void SetWindowHandle(IntPtr hwnd)
        {
            this.hwnd = hwnd;
        }

        /// Clears framebuffer to a specific color.
        public void Clear(Color clearColor)
        {
            for (int i = 0; i < width * height; i++)
            {
                int idx = i * 4;
                framebuffer[idx + 0] = clearColor.B;
                framebuffer[idx + 1] = clearColor.G;
                framebuffer[idx + 2] = clearColor.R;
                framebuffer[idx + 3] = clearColor.A;
            }
        }

        /// Loads an image from file and returns pixel data as byte[].
        /// Pixel format is BGRA32.
        public static byte[] LoadImagePixels(string filePath, out int imgWidth, out int imgHeight)
        {
            using (var bmp = new Bitmap(filePath))
            {
                imgWidth = bmp.Width;
                imgHeight = bmp.Height;

                var rect = new Rectangle(0, 0, imgWidth, imgHeight);
                var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int bytes = Math.Abs(bmpData.Stride) * imgHeight;
                byte[] pixels = new byte[bytes];
                Marshal.Copy(bmpData.Scan0, pixels, 0, bytes);
                bmp.UnlockBits(bmpData);
                return pixels;
            }
        }

        /// Draws the image's pixel data onto framebuffer at (posX,posY).
        /// Performs clipping if image extends outside framebuffer bounds.
        /// Input pixels expected in BGRA 32bpp byte array with 4 bytes per pixel.
        public void DrawImage(byte[] pixels, int imgWidth, int imgHeight, int posX, int posY)
        {
            for (int y = 0; y < imgHeight; y++)
            {
                int fbY = posY + y;
                if (fbY < 0 || fbY >= height) continue;

                for (int x = 0; x < imgWidth; x++)
                {
                    int fbX = posX + x;
                    if (fbX < 0 || fbX >= width) continue;

                    int imgIdx = (y * imgWidth + x) * 4;
                    byte b = pixels[imgIdx + 0];
                    byte g = pixels[imgIdx + 1];
                    byte r = pixels[imgIdx + 2];
                    byte a = pixels[imgIdx + 3];

                    // Simple alpha blend over framebuffer pixel
                    int fbIdx = (fbY * width + fbX) * 4;
                    byte fbB = framebuffer[fbIdx + 0];
                    byte fbG = framebuffer[fbIdx + 1];
                    byte fbR = framebuffer[fbIdx + 2];
                    byte fbA = framebuffer[fbIdx + 3];

                    float alpha = a / 255f;
                    framebuffer[fbIdx + 0] = (byte)(b * alpha + fbB * (1 - alpha));
                    framebuffer[fbIdx + 1] = (byte)(g * alpha + fbG * (1 - alpha));
                    framebuffer[fbIdx + 2] = (byte)(r * alpha + fbR * (1 - alpha));
                    framebuffer[fbIdx + 3] = 255; // keep opaque for simplicity
                }
            }
        }

        /// Presents current framebuffer to the window using GDI BitBlt.
        public void Present()
        {
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException("Window handle is not set.");

            IntPtr hdc = WinApi.GetDC(hwnd);
            IntPtr memDC = WinApi.CreateCompatibleDC(hdc);

            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                BITMAPINFO bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = -height; // top-down bitmap
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0; // BI_RGB

                IntPtr ppvBits;
                hBitmap = WinApi.CreateDIBSection(memDC, ref bmi, 0, out ppvBits, IntPtr.Zero, 0);
                if (hBitmap == IntPtr.Zero)
                    throw new Exception("CreateDIBSection failed.");

                Marshal.Copy(framebuffer, 0, ppvBits, framebuffer.Length);

                oldBitmap = WinApi.SelectObject(memDC, hBitmap);
                WinApi.BitBlt(hdc, 0, 0, width, height, memDC, 0, 0, WinApi.SRCCOPY);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero)
                    WinApi.SelectObject(memDC, oldBitmap);
                if (hBitmap != IntPtr.Zero)
                    WinApi.DeleteObject(hBitmap);
                if (memDC != IntPtr.Zero)
                    WinApi.DeleteDC(memDC);
                if (hdc != IntPtr.Zero)
                    WinApi.ReleaseDC(hwnd, hdc);
            }
        }

        #region WinApi PInvoke

        private static class WinApi
        {
            public const int SRCCOPY = 0x00CC0020;

            [DllImport("user32.dll")]
            public static extern IntPtr GetDC(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

            [DllImport("gdi32.dll")]
            public static extern bool DeleteDC(IntPtr hdc);

            [DllImport("gdi32.dll")]
            public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

            [DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);

            [DllImport("gdi32.dll")]
            public static extern bool BitBlt(
                IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
                IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateDIBSection(
                IntPtr hdc,
                [In] ref BITMAPINFO pbmi,
                uint iUsage,
                out IntPtr ppvBits,
                IntPtr hSection,
                uint dwOffset);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public uint[] bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        #endregion
    }
}
