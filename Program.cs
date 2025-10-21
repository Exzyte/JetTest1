using System;
using System.Runtime.InteropServices;
using System.Drawing;
using JetTest1;
using System.Timers;
using static System.Formats.Asn1.AsnWriter;

class Program
{
    private const int windowHeight = 720;
    private const int windowWidth = 1280;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetMessage(ref MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern short GetAsyncKeyState(int vKey);
    const int VK_LEFT = 0x25;
    const int VK_RIGHT = 0x27;
    const int VK_UP = 0x26;
    const int VK_DOWN = 0x28;

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("gdi32.dll")]
    static extern bool TextOut(IntPtr hdc, int x, int y, string lpString, int c);
    [DllImport("user32.dll")]
    static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    private const uint WM_CLOSE = 0x0010;

    private const int SW_SHOW = 5;
    private const int WM_DESTROY = 0x0002;
    private const int WM_PAINT = 0x000F;

    private static IntPtr hWnd;
    private static Renderer renderer;

    private static System.Timers.Timer scoreTimer; // fix issue
    private static int score = 0;
    private static int ifGoalReach = 0;

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private static WndProc windowProc = WindowProc;

    private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_DESTROY:
                Environment.Exit(0);
                return IntPtr.Zero;

            case WM_PAINT:
                return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hWnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public System.Drawing.Point pt;
    }

    static void Main() // Main window part
    {
        string className = "WindowClass";
        WNDCLASS wc = new WNDCLASS();
        wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(windowProc);
        wc.lpszClassName = className;

        RegisterClass(ref wc);

        hWnd = CreateWindowEx(0, className, "Jet Test 1", 0xCF0000, 100, 100, windowWidth, windowHeight, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Console.WriteLine("[DEBUG] Window Started");

        ShowWindow(hWnd, SW_SHOW);

        renderer = new Renderer(windowWidth, windowHeight);
        renderer.SetWindowHandle(hWnd);
        // -----
        int imgWidth, imgHeight;
        var imagePixels = Renderer.LoadImagePixels("plane.png", out imgWidth, out imgHeight);
        // -----
        int bgWidth, bgHeight;
        var backgroundPixels = Renderer.LoadImagePixels("water_background.png", out bgWidth, out bgHeight);
        Console.WriteLine("[DEBUG] Sprites created");

        scoreTimer = new System.Timers.Timer(5000);
        scoreTimer.Elapsed += OnTimedEvent;
        scoreTimer.AutoReset = true;
        scoreTimer.Enabled = true;

        int spriteX = 100;
        int spriteY = 100;
        int movementSpeed = 10;

        // -----------------------
        MSG msg = new MSG(); // Main rendering part
        Console.WriteLine("[DEBUG] Loop Started");
        while (GetMessage(ref msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);

            // movement for jet sprite
            // horizontal
            if ((GetAsyncKeyState(VK_LEFT) & 0x8000) != 0)
            {
                spriteX -= movementSpeed;
                if (spriteX < 0) spriteX = 0;
            }
            else if ((GetAsyncKeyState(VK_RIGHT) & 0x8000) != 0)
            {
                spriteX += movementSpeed;
                if (spriteX > windowWidth - imgWidth) spriteX = windowWidth - imgWidth;
            }
            // vertical
            if ((GetAsyncKeyState(VK_UP) & 0x8000) != 0)
            {
                spriteY -= movementSpeed;
                if (spriteY < 0) spriteY = 0;
            }
            else if ((GetAsyncKeyState(VK_DOWN) & 0x8000) != 0)
            {
                spriteY += movementSpeed;
                if (spriteY > windowHeight - imgHeight) spriteY = windowHeight - imgHeight;
            }

            renderer.Clear(Color.Black);
            renderer.DrawImage(backgroundPixels, bgWidth, bgHeight, 0, 0);
            renderer.DrawImage(imagePixels, imgWidth, imgHeight, spriteX, spriteY);

            IntPtr hdc = GetDC(hWnd);
            string scoreText = "Score: " + score.ToString();
            TextOut(hdc, 10, 10, scoreText, scoreText.Length);
            ReleaseDC(hWnd, hdc);

            if (score >= 100)
            {
                Console.WriteLine("[DEBUG] Score limit of 100 reached");
                ifGoalReach = 1;
            }
            if (ifGoalReach == 1)
            {
                Console.WriteLine("[DEBUG] Closing!");
                PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }

            renderer.Present();
        }
    }

    private static void OnTimedEvent(Object source, ElapsedEventArgs e)
    {
        score += 10;
    }

    // --------------------
    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASS
    {
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);
}
