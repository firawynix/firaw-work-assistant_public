using System.Drawing;
using System.Runtime.InteropServices;

namespace Firaw.WorkAssistant;

internal static class CursorMotion
{
    private const uint InputMouse = 0;
    private const uint MouseMove = 0x0001;
    private const uint MouseVirtualDesktop = 0x4000;
    private const uint MouseAbsolute = 0x8000;

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, ref Input input, int inputSize);

    public static bool MoveTo(Point target, Rectangle virtualScreen)
    {
        if (virtualScreen.Width < 2 || virtualScreen.Height < 2) return false;

        var x = Math.Clamp(target.X, virtualScreen.Left, virtualScreen.Right - 1);
        var y = Math.Clamp(target.Y, virtualScreen.Top, virtualScreen.Bottom - 1);
        var input = new Input
        {
            Type = InputMouse,
            Mouse = new MouseInput
            {
                X = (int)((long)(x - virtualScreen.Left) * 65535 / (virtualScreen.Width - 1)),
                Y = (int)((long)(y - virtualScreen.Top) * 65535 / (virtualScreen.Height - 1)),
                Flags = MouseMove | MouseAbsolute | MouseVirtualDesktop
            }
        };
        return SendInput(1, ref input, Marshal.SizeOf<Input>()) == 1;
    }
}
