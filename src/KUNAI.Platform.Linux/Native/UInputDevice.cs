using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace KUNAI.Platform.Linux.Native;

/// <summary>/dev/uinput に仮想キーボードを作り、キーイベントを書き込む(従来方式の uinput_user_dev による作成)。</summary>
internal sealed class UInputDevice : IUInputDevice
{
    private const string DevicePath = "/dev/uinput";

    // _IOW('U', 100, int) / _IOW('U', 101, int) / _IO('U', 1) / _IO('U', 2)
    private const uint UiSetEvBit = 0x40045564;
    private const uint UiSetKeyBit = 0x40045565;
    private const uint UiDevCreate = 0x5501;
    private const uint UiDevDestroy = 0x5502;

    private const int EvSyn = 0x00;
    private const int EvKey = 0x01;
    private const int BusUsb = 0x03;

    private const int UinputMaxNameSize = 80;
    private const int AbsCnt = 64;

    private readonly int _fd;
    private bool _disposed;

    private UInputDevice(int fd)
    {
        _fd = fd;
    }

    /// <summary>仮想キーボードを作成する。権限が無い場合は設定手順つきの例外を投げる。</summary>
    public static UInputDevice Create(string deviceName, IEnumerable<ushort> keyCodes)
    {
        var fd = LibC.open(DevicePath, LibC.ORdWr | LibC.ONonBlock);
        if (fd < 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new IOException(
                $"Cannot open {DevicePath} ({new Win32Exception(errno).Message}). " +
                "Grant access with a udev rule such as: KERNEL==\"uinput\", GROUP=\"input\", MODE=\"0660\" " +
                "and add your user to the 'input' group (then log in again). " +
                "If the module is missing, run: sudo modprobe uinput");
        }

        try
        {
            Ioctl(fd, UiSetEvBit, EvKey);
            Ioctl(fd, UiSetEvBit, EvSyn);
            foreach (var code in keyCodes)
            {
                Ioctl(fd, UiSetKeyBit, code);
            }

            var setup = BuildUserDev(deviceName);
            if (LibC.write(fd, setup, (nuint)setup.Length) != setup.Length)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to write uinput device setup.");
            }

            Ioctl(fd, UiDevCreate, 0);
        }
        catch
        {
            LibC.close(fd);
            throw;
        }

        return new UInputDevice(fd);
    }

    public void Emit(LinuxKeyEvent keyEvent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Write(EvKey, keyEvent.Code, keyEvent.IsDown ? 1 : 0);
        Write(EvSyn, 0, 0); // SYN_REPORT
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        LibC.ioctl(_fd, UiDevDestroy, 0);
        LibC.close(_fd);
    }

    /// <summary>struct uinput_user_dev(1116 バイト)を組み立てる。</summary>
    internal static byte[] BuildUserDev(string deviceName)
    {
        // name[80] + input_id(u16 x4) + ff_effects_max(int) + absmax/absmin/absfuzz/absflat(int[64] x4)
        var buffer = new byte[UinputMaxNameSize + 8 + 4 + 4 * AbsCnt * 4];

        var name = Encoding.ASCII.GetBytes(deviceName);
        Array.Copy(name, buffer, Math.Min(name.Length, UinputMaxNameSize - 1));

        var offset = UinputMaxNameSize;
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 2), (ushort)BusUsb);     // bustype
        BitConverter.TryWriteBytes(buffer.AsSpan(offset + 2, 2), (ushort)0x4B55); // vendor
        BitConverter.TryWriteBytes(buffer.AsSpan(offset + 4, 2), (ushort)0x0001); // product
        BitConverter.TryWriteBytes(buffer.AsSpan(offset + 6, 2), (ushort)0x0001); // version
        return buffer;
    }

    /// <summary>struct input_event を組み立てる。タイムスタンプは 0 のままで、カーネルが設定する。</summary>
    internal static byte[] BuildInputEvent(ushort type, ushort code, int value)
    {
        // struct timeval(long sec, long usec) + u16 type + u16 code + s32 value
        var buffer = new byte[2 * IntPtr.Size + 8];
        var offset = 2 * IntPtr.Size;
        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 2), type);
        BitConverter.TryWriteBytes(buffer.AsSpan(offset + 2, 2), code);
        BitConverter.TryWriteBytes(buffer.AsSpan(offset + 4, 4), value);
        return buffer;
    }

    private void Write(ushort type, ushort code, int value)
    {
        var buffer = BuildInputEvent(type, code, value);
        if (LibC.write(_fd, buffer, (nuint)buffer.Length) != buffer.Length)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Failed to write uinput event.");
        }
    }

    private static void Ioctl(int fd, uint request, int arg)
    {
        if (LibC.ioctl(fd, request, arg) < 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"uinput ioctl 0x{request:X} failed.");
        }
    }
}
