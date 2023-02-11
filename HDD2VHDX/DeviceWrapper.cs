using HDD2VHDX;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HDD2VHDX
{
    public class DeviceWrapper
    {
        private DeviceIO.VolumeSafeHandle volumeHandle;

        public DeviceWrapper(string devicePath, uint accessMode)
        {
            //open vsc in read-only mode
            this.volumeHandle = DeviceIO.CreateVolumeFile(
            devicePath,
            accessMode,
            DeviceIO.FILE_SHARE_READ | DeviceIO.FILE_SHARE_WRITE,
            IntPtr.Zero,
            DeviceIO.OPEN_EXISTING,
            DeviceIO.FILE_FLAG_NO_BUFFERING | DeviceIO.FILE_FLAG_WRITE_THROUGH,
            IntPtr.Zero);
        }

        public DeviceWrapper(DeviceIO.VolumeSafeHandle volumeHandle)
        {
            this.volumeHandle = volumeHandle;
        }

        //gets the underlying volume handle
        public DeviceIO.VolumeSafeHandle getVolumeHandle()
        {
            return this.volumeHandle;
        }

        //sets the current filepointer, relative to the current position
        public void setFilePointer(Int64 length)
        {
            long newPointer;
            DeviceIO.SetFilePointerEx(this.volumeHandle, length, out newPointer, 1);
        }

        //reads a given range of bytes
        unsafe public uint read(uint count, byte[] buffer)
        {
            uint n = 0;
            fixed (byte* ptr = buffer)
            {
                DeviceIO.ReadFile(this.volumeHandle, ptr, count, &n, IntPtr.Zero);
            }

            return n;
        }

        //writes a given range of bytes
        unsafe public void write(byte[] buffer, uint length)
        {
            uint bytesWritten = 0;
            fixed (byte* p = buffer)
            {
                DeviceIO.WriteFile(this.volumeHandle, p, (uint)length, &bytesWritten, IntPtr.Zero);
                //System.IO.File.WriteAllBytes(@"c:\output.bin", buffer);
            }
        }

        //closes the current device handle
        public void close()
        {
            if (this.volumeHandle != null)
            {
                DeviceIO.CloseHandle(this.volumeHandle);
                this.volumeHandle.SetHandleAsInvalid();
                this.volumeHandle = null;
            }
        }

    }
}
