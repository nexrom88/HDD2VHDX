﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Xml.Schema;
using HDD2VHDX;

namespace HDD2VHDX
{
    public class VirtualDiskHandler
    {
        [DllImportAttribute("kernel32.dll", EntryPoint = "FindFirstVolumeW", SetLastError = true)]
        public static extern SearchSafeHandle FindFirstVolume([OutAttribute()][MarshalAsAttribute(UnmanagedType.LPWStr)] StringBuilder lpszVolumeName, Int32 cchBufferLength);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA5122:PInvokesShouldNotBeSafeCriticalFxCopRule", Justification = "Warning is bogus.")]
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 GetVirtualDiskPhysicalPath(VirtualDiskSafeHandle VirtualDiskHandle, ref Int32 DiskPathSizeInBytes, StringBuilder DiskPath);

        [DllImportAttribute("kernel32.dll", EntryPoint = "FindNextVolumeW", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern Boolean FindNextVolume(SearchSafeHandle hFindVolume, [OutAttribute()][MarshalAsAttribute(UnmanagedType.LPWStr)] StringBuilder lpszVolumeName, Int32 cchBufferLength);


        [DllImport("kernel32.dll", SetLastError = true)]
        static unsafe extern bool WriteFile(VirtualDiskSafeHandle virtualDiskHandle, ref byte lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, System.Threading.NativeOverlapped* lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static unsafe extern bool ReadFile(VirtualDiskSafeHandle virtualDiskHandle, ref byte lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, System.Threading.NativeOverlapped* lpOverlapped);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern Int32 AttachVirtualDisk(VirtualDiskSafeHandle VirtualDiskHandle, IntPtr SecurityDescriptor, ATTACH_VIRTUAL_DISK_FLAG Flags, Int32 ProviderSpecificFlags, ref ATTACH_VIRTUAL_DISK_PARAMETERS Parameters, IntPtr Overlapped);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 DetachVirtualDisk(VirtualDiskSafeHandle VirtualDiskHandle, DETACH_VIRTUAL_DISK_FLAG flags, uint ProviderSpecificFlags);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 CompactVirtualDisk(VirtualDiskSafeHandle VirtualDiskHandle, COMPACT_VIRTUAL_DISK_FLAG flags, IntPtr parameters, IntPtr overlapped);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 OpenVirtualDisk(ref VIRTUAL_STORAGE_TYPE type,
        string Path,
        VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask,
        OPEN_VIRTUAL_DISK_FLAG Flags,
        ref OPEN_VIRTUAL_DISK_PARAMETERS Parameters,
        ref VirtualDiskSafeHandle Handle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetVolumeNameForVolumeMountPointW")]
        static extern bool GetVolumeNameForVolumeMountPoint(string
            lpszVolumeMountPoint, [Out] StringBuilder lpszVolumeName,
            uint cchBufferLength);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 GetVirtualDiskInformation(VirtualDiskSafeHandle VirtualDiskHandle, ref uint virtualDiskInfoSize, ref GetVirtualDiskInfo virtualDiskInfo, ref uint sizeUsed);

        [StructLayout(LayoutKind.Sequential)]
        public struct VOLUME_DISK_EXTENTS
        {
            public Int32 NumberOfDiskExtents;
            public DISK_EXTENT Extents;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISK_EXTENT
        {
            public Int32 DiskNumber;
            public Int64 StartingOffset;
            public Int64 ExtentLength;
        }

        [SecurityPermission(SecurityAction.Demand)]
        public class SearchSafeHandle : SafeHandleMinusOneIsInvalid
        {

            public SearchSafeHandle()
                : base(true) { }


            protected override bool ReleaseHandle()
            {
                return FindVolumeClose(this.handle);
            }

            public override string ToString()
            {
                return this.handle.ToString();
            }

            [DllImportAttribute("kernel32.dll", EntryPoint = "FindVolumeClose")]
            [return: MarshalAsAttribute(UnmanagedType.Bool)]
            public static extern bool FindVolumeClose([InAttribute()] IntPtr hFindVolume);

        }

        //read state
        private bool readInProgress;
        private uint readErrorCode;

        //write state
        private bool writeInProgress;
        private uint writeErrorCode;
        private uint bytesWritten;

        private string path;
        private VirtualDiskSafeHandle diskHandle;

        public VirtualDiskHandler(string path)
        {
            this.path = path;

        }


        //opens the virtual disk
        public bool open(VirtualDiskAccessMask accessMask)
        {
            return openvhdx(this.path, accessMask);
        }

        //releases the vhd handle
        public void close()
        {
            this.diskHandle.Close();
            this.diskHandle.SetHandleAsInvalid();
        }

        private bool openvhdx(string fileName, VirtualDiskAccessMask fileAccessMask)
        {
            var parameters = new OPEN_VIRTUAL_DISK_PARAMETERS();
            parameters.Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1;
            parameters.Version1.RWDepth = 1;

            var storageType = new VIRTUAL_STORAGE_TYPE();
            storageType.DeviceId = VIRTUAL_STORAGE_TYPE_DEVICE_VHDX;
            storageType.VendorId = VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT;


            //fileAccessMask = ((fileAccessMask & VirtualDiskAccessMask.GetInfo) == VirtualDiskAccessMask.GetInfo) ?VirtualDiskAccessMask.GetInfo : 0;
            //fileAccessMask |= VirtualDiskAccessMask.AttachReadOnly;

            VirtualDiskSafeHandle handle = new VirtualDiskSafeHandle();

            int res = OpenVirtualDisk(ref storageType, fileName,
                (VIRTUAL_DISK_ACCESS_MASK)fileAccessMask,
                OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE, ref parameters, ref handle);

            if (res == ERROR_SUCCESS)
            {
                this.diskHandle = handle;
                return true;
            }
            else
            {
                handle.SetHandleAsInvalid();
                if ((res == ERROR_FILE_NOT_FOUND) || (res == ERROR_PATH_NOT_FOUND))
                {
                    return false;
                }
                else if (res == ERROR_ACCESS_DENIED)
                {
                    return false;
                }
                else if (res == ERROR_FILE_CORRUPT)
                {
                    return false;
                }
                else
                {
                    return false;
                }
            }
        }

        //attaches the current virtual disk
        public bool attach(ATTACH_VIRTUAL_DISK_FLAG flags)
        {
            var attachParameters = new ATTACH_VIRTUAL_DISK_PARAMETERS();
            attachParameters.Version = ATTACH_VIRTUAL_DISK_VERSION.ATTACH_VIRTUAL_DISK_VERSION_1;
            int attachResult = AttachVirtualDisk(this.diskHandle, IntPtr.Zero, flags, 0, ref attachParameters, IntPtr.Zero);
            return attachResult == ERROR_SUCCESS;
        }

        //detaches the current virtual disk
        public bool detach()
        {
            int retVal = DetachVirtualDisk(this.diskHandle, DETACH_VIRTUAL_DISK_FLAG.DETACH_VIRTUAL_DISK_FLAG_NONE, 0);
            return retVal == ERROR_SUCCESS;
        }

        //shrinks the current vhdx file
        public bool shrinkFile()
        {
            int errorCode = CompactVirtualDisk(this.diskHandle, COMPACT_VIRTUAL_DISK_FLAG.COMPACT_VIRTUAL_DISK_FLAG_NONE, IntPtr.Zero, IntPtr.Zero);
            return errorCode == ERROR_SUCCESS;
        }

        //gets the virtual hard disk size
        public GetVirtualDiskInfoSize getSize()
        {
            var info = new GetVirtualDiskInfo { Version = GetVirtualDiskInfoVersion.Size };
            uint infoSize = (uint)Marshal.SizeOf(info);
            uint sizeUsed = 0;
            var result = GetVirtualDiskInformation(this.diskHandle, ref infoSize, ref info, ref sizeUsed);
            if (result != 0) { return new GetVirtualDiskInfoSize(); }
            return info.Union.Size;
        }

        //initilaizes a raw vhdx and returns path to newly created volume
        public WriteableRawVolume initDisk(string diskPath)
        {
            var signature = new byte[4];
            RandomNumberGenerator.Create().GetBytes(signature); //lazy way to generate "unique" signature

            using (SafeFileHandle handle = DeviceIO.CreateFile(diskPath, DeviceIO.GENERIC_READ | DeviceIO.GENERIC_WRITE, 0, IntPtr.Zero, DeviceIO.OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (handle.IsInvalid) { throw new Win32Exception(); }

                var cd = new DeviceIO.CREATE_DISK();
                cd.PartitionStyle = DeviceIO.PARTITION_STYLE.PARTITION_STYLE_MBR;
                cd.MbrGpt.Mbr.Signature = BitConverter.ToInt32(signature, 0);
                Int32 bytesOut = 0;
                if (DeviceIO.DeviceIoControl(handle, DeviceIO.IOCTL_DISK_CREATE_DISK, ref cd, Marshal.SizeOf(cd), IntPtr.Zero, 0, ref bytesOut, IntPtr.Zero) == false) { throw new Win32Exception(); }

                //update cache
                if (DeviceIO.DeviceIoControl(handle, DeviceIO.IOCTL_DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesOut, IntPtr.Zero) == false) { throw new Win32Exception(); } //just update cache

                var pi = new DeviceIO.PARTITION_INFORMATION();
                if (DeviceIO.DeviceIoControl(handle, DeviceIO.IOCTL_DISK_GET_PARTITION_INFO, IntPtr.Zero, 0, ref pi, Marshal.SizeOf(pi), ref bytesOut, IntPtr.Zero) == false) { throw new Win32Exception(); }

                var dli = new DeviceIO.DRIVE_LAYOUT_INFORMATION_EX();
                dli.PartitionStyle = DeviceIO.PARTITION_STYLE.PARTITION_STYLE_MBR;
                dli.PartitionCount = 1;
                dli.Partition1.PartitionStyle = DeviceIO.PARTITION_STYLE.PARTITION_STYLE_MBR;
                dli.Partition1.StartingOffset = 65536;
                dli.Partition1.PartitionLength = pi.PartitionLength - dli.Partition1.StartingOffset;
                dli.Partition1.PartitionNumber = 1;
                dli.Partition1.RewritePartition = true;
                dli.Partition1.Mbr.PartitionType = DeviceIO.PARTITION_IFS;
                dli.Partition1.Mbr.BootIndicator = true;
                dli.Partition1.Mbr.RecognizedPartition = true;
                dli.Partition1.Mbr.HiddenSectors = 0;
                dli.Mbr.Signature = BitConverter.ToInt32(signature, 0);

                if (DeviceIO.DeviceIoControl(handle, DeviceIO.IOCTL_DISK_SET_DRIVE_LAYOUT_EX, ref dli, Marshal.SizeOf(dli), IntPtr.Zero, 0, ref bytesOut, IntPtr.Zero) == false) { throw new Win32Exception(); }

                if (DeviceIO.DeviceIoControl(handle, DeviceIO.IOCTL_DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesOut, IntPtr.Zero) == false) { throw new Win32Exception(); } //just update cache

                System.Threading.Thread.Sleep(5000);



                //now look for newly created volume
                string volumePath = string.Empty;
                var sb = new StringBuilder(50);

                var volumeSearchHandle = FindFirstVolume(sb, sb.Capacity);
                OpenedVolume newVolume = new OpenedVolume();
                if (volumeSearchHandle.IsInvalid == false)
                {
                    do
                    {
                        string currentVolume = sb.ToString();
                        newVolume = getDiskNumberFromVolumePath(currentVolume.Remove(currentVolume.Length - 1));


                        if (newVolume.diskNumber == int.Parse(diskPath.Substring(diskPath.Length - 1)))
                        {
                            volumePath = currentVolume;
                            break;
                        }
                    } while (FindNextVolume(volumeSearchHandle, sb, sb.Capacity));
                }
                volumeSearchHandle.Close();

                if (newVolume.volumeHandle != null)
                {
                    //dismount volume
                    bool dismounted = DeviceIO.DeviceIoControl(newVolume.volumeHandle, DeviceIO.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, ref bytesOut, IntPtr.Zero);
                    if (!dismounted)
                    {
                        int error = Marshal.GetLastWin32Error();
                        return new WriteableRawVolume();
                    }
                }

                WriteableRawVolume retVal = new WriteableRawVolume();
                retVal.isValid = true;
                retVal.handle = newVolume.volumeHandle;
                retVal.volumePath = volumePath;

                return retVal;
            }
        }

        private OpenedVolume getDiskNumberFromVolumePath(string volumeName)
        {
            OpenedVolume volume = new OpenedVolume();
            var volumeHandle = DeviceIO.CreateVolumeFile(volumeName, DeviceIO.GENERIC_READ | DeviceIO.GENERIC_WRITE, DeviceIO.FILE_SHARE_READ | DeviceIO.FILE_SHARE_WRITE, IntPtr.Zero, DeviceIO.OPEN_EXISTING, 0, IntPtr.Zero);
            if (volumeHandle.IsInvalid == false)
            {
                var de = new VOLUME_DISK_EXTENTS();
                volume.volumeHandle = volumeHandle;
                de.NumberOfDiskExtents = 1;
                int bytesReturned = 0;
                if (DeviceIO.DeviceIoControl(volumeHandle, DeviceIO.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, ref de, Marshal.SizeOf(de), ref bytesReturned, IntPtr.Zero))
                {
                    if (bytesReturned > 0)
                    {
                        volume.diskNumber = de.Extents.DiskNumber;
                        return volume;
                    }
                }
            }
            return volume;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "This method may not return immediately when called.")]
        public string getAttachedPath()
        {
            int res = 0;
            int pathSize = 200; //size in bytes (200) - not in chars (100)
            var path = new StringBuilder(pathSize / 2); //unicode

            res = GetVirtualDiskPhysicalPath(this.diskHandle, ref pathSize, path);

            if (res != ERROR_SUCCESS)
            {
                throw new Win32Exception(res);
            }
            else
            {
                return path.ToString();
            }
        }

        //returns the file handle
        public IntPtr getHandle()
        {
            return this.diskHandle.Handle;
        }

        //writes a given block to the vhd
        public unsafe bool write(ulong offset, byte[] buffer)
        {
            this.writeInProgress = true;

            //create overlapped structure
            System.Threading.Overlapped ov = new System.Threading.Overlapped();
            int[] offsetValues = long2doubleInt(offset);
            ov.OffsetHigh = offsetValues[1];
            ov.OffsetLow = offsetValues[0];

            uint bytesWritten = 0;

            //start read
            bool err = WriteFile(this.diskHandle, ref buffer[0], (uint)buffer.Length, out bytesWritten, ov.Pack(new System.Threading.IOCompletionCallback(writeCompleted)));
            int errCode = Marshal.GetLastWin32Error();
            //wait for completion of write process
            while (this.writeInProgress)
            {
                errCode = Marshal.GetLastWin32Error();
                System.Threading.Thread.Sleep(10);
            }

            return this.writeErrorCode == 0;
        }

        //reads a given block from the vhd
        public unsafe byte[] read(ulong offset, ulong length)
        {
            this.readInProgress = true;

            //create overlapped structure
            System.Threading.Overlapped ov = new System.Threading.Overlapped();
            int[] offsetValues = long2doubleInt(offset);
            ov.OffsetHigh = offsetValues[1];
            ov.OffsetLow = offsetValues[0];

            byte[] buffer = new byte[length];
            uint bytesRead = 0;

            //start read
            bool err = ReadFile(this.diskHandle, ref buffer[0], (uint)length, out bytesRead, ov.Pack(new System.Threading.IOCompletionCallback(readCompleted)));
            int errCode = Marshal.GetLastWin32Error();



            //wait for completion of read process
            while (this.readInProgress)
            {
                errCode = Marshal.GetLastWin32Error();
                System.Threading.Thread.Sleep(10);
            }


            if (this.readErrorCode == 0) //return buffer
            {
                return buffer;
            }
            else //error, return null
            {
                return null;
            }

        }

        //read completed event handler
        private unsafe void readCompleted(uint errorCode, uint numBytes, System.Threading.NativeOverlapped* pOVERLAP)
        {
            this.readErrorCode = errorCode;
            this.readInProgress = false;
        }


        //write completed event handler
        private unsafe void writeCompleted(uint errorCode, uint numBytes, System.Threading.NativeOverlapped* pOVERLAP)
        {
            this.writeErrorCode = errorCode;
            this.writeInProgress = false;
            this.bytesWritten = numBytes;
        }

        //converts a long to two ints
        private int[] long2doubleInt(ulong a)
        {
            int a1 = (int)(a & uint.MaxValue); //MSB
            int a2 = (int)(a >> 32); //LSB
            return new int[] { a1, a2 };
        }

        public struct WriteableRawVolume
        {
            public bool isValid;
            public string volumePath;
            public DeviceIO.VolumeSafeHandle handle;
        }

        public struct OpenedVolume
        {
            public int diskNumber;
            public DeviceIO.VolumeSafeHandle volumeHandle;
        }

        public enum DETACH_VIRTUAL_DISK_FLAG
        {
            DETACH_VIRTUAL_DISK_FLAG_NONE
        }

        public enum COMPACT_VIRTUAL_DISK_FLAG
        {
            COMPACT_VIRTUAL_DISK_FLAG_NONE,
            COMPACT_VIRTUAL_DISK_FLAG_NO_ZERO_SCAN,
            COMPACT_VIRTUAL_DISK_FLAG_NO_BLOCK_MOVES
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct GetVirtualDiskInfo
        {
            public GetVirtualDiskInfoVersion Version; //GET_VIRTUAL_DISK_INFO_VERSION
            public GetVirtualDiskInfoUnion Union;
        }

        public enum GetVirtualDiskInfoVersion
        {
            Unspecified = 0,
            Size = 1,
            Identifier = 2,
            ParentLocation = 3,
            ParentIdentifier = 4,
            ParentTimestamp = 5,
            VirtualStorageType = 6,
            ProviderSubtype = 7,
            Is4KAligned = 8,
            PhysicalDisk = 9,
            VhdPhysicalSectorSize = 10,
            SmallestSafeVirtualSize = 11,
            Fragmentation = 12,
            IsLoaded = 13,
            VirtualDiskId = 14,
            ChangeTrackingState = 15
        }

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        public struct GetVirtualDiskInfoUnion
        {
            [FieldOffset(0)] public GetVirtualDiskInfoSize Size;
            [FieldOffset(0)] public Guid Identifier; //GUID
            [FieldOffset(0)] public GetVirtualDiskInfoParentLocation ParentLocation;
            [FieldOffset(0)] public Guid ParentIdentifier; //GUID
            [FieldOffset(0)] public uint ParentTimestamp; //ULONG
            [FieldOffset(0)] public VirtualStorageType VirtualStorageType; //VIRTUAL_STORAGE_TYPE
            [FieldOffset(0)] public uint ProviderSubtype; //ULONG
            [FieldOffset(0)] public bool Is4kAligned; //BOOL
            [FieldOffset(0)] public bool IsLoaded; //BOOL
            [FieldOffset(0)] public GetVirtualDiskInfoPhysicalDisk PhysicalDisk;
            [FieldOffset(0)] public uint VhdPhysicalSectorSize; //ULONG
            [FieldOffset(0)] public ulong SmallestSafeVirtualSize; //ULONGLONG
            [FieldOffset(0)] public uint FragmentationPercentage; //ULONG
            [FieldOffset(0)] public Guid VirtualDiskId; //GUID
            [FieldOffset(0)] public GetVirtualDiskInfoChangeTrackingState ChangeTrackingState;
            [FieldOffset(0)] public uint Reserved; //ULONG 
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct GetVirtualDiskInfoChangeTrackingState
        {
            public bool Enabled; //BOOL
            public bool NewerChanges; //BOOL
            public char MostRecentId; //WCHAR[1] //TODO
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct VirtualStorageType
        {
            public VirtualStorageDeviceType DeviceId; //ULONG
            public Guid VendorId; //GUID
        }

        public enum VirtualStorageDeviceType
        {
            Unknown = 0,
            Iso = 1,
            Vhd = 2,
            Vhdx = 3,
            Vhdset = 4
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct GetVirtualDiskInfoSize
        {
            public ulong VirtualSize; //ULONGLONG
            public ulong PhysicalSize; //ULONGLONG
            public uint BlockSize; //ULONG
            public uint SectorSize; //ULONG
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct GetVirtualDiskInfoParentLocation
        {
            public bool ParentResolved; //BOOL
            public char ParentLocationBuffer; //WCHAR[1] //TODO
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct GetVirtualDiskInfoPhysicalDisk
        {
            public uint LogicalSectorSize; //ULONG
            public uint PhysicalSectorSize; //ULONG
            public bool IsRemote; //BOOL
        }

        public enum ATTACH_VIRTUAL_DISK_FLAG : int
        {
            ATTACH_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
            ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY = 0x00000001,
            ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER = 0x00000002,
            ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME = 0x00000004,
            ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST = 0x00000008
        }


        public enum ATTACH_VIRTUAL_DISK_VERSION : int
        {
            ATTACH_VIRTUAL_DISK_VERSION_UNSPECIFIED = 0,
            ATTACH_VIRTUAL_DISK_VERSION_1 = 1
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ATTACH_VIRTUAL_DISK_PARAMETERS_Version1
        {
            public Int32 Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ATTACH_VIRTUAL_DISK_PARAMETERS
        {
            public ATTACH_VIRTUAL_DISK_VERSION Version;
            public ATTACH_VIRTUAL_DISK_PARAMETERS_Version1 Version1;
        }


        public static readonly Guid VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT = new Guid("EC984AEC-A0F9-47e9-901F-71415A66345B");
        public const int OPEN_VIRTUAL_DISK_RW_DEPTH_DEFAULT = 1;

        public const Int32 ERROR_SUCCESS = 0;
        public const Int32 ERROR_FILE_CORRUPT = 1392;
        public const Int32 ERROR_FILE_NOT_FOUND = 2;
        public const Int32 ERROR_PATH_NOT_FOUND = 3;
        public const Int32 ERROR_ACCESS_DENIED = 5;

        /// CD or DVD image file device type. (.iso file)
        /// </summary>
        public const int VIRTUAL_STORAGE_TYPE_DEVICE_ISO = 1;

        /// <summary>
        /// Device type is unknown or not valid.
        /// </summary>
        public const int VIRTUAL_STORAGE_TYPE_DEVICE_UNKNOWN = 0;

        /// <summary>
        /// Virtual hard disk device type. (.vhd file)
        /// </summary>
        public const int VIRTUAL_STORAGE_TYPE_DEVICE_VHD = 2;

        /// <summary>
        /// VHDX format virtual hard disk device type. (.vhdx file)
        /// </summary>
        public const int VIRTUAL_STORAGE_TYPE_DEVICE_VHDX = 3;


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct VIRTUAL_STORAGE_TYPE
        {
            /// <summary>
            /// Device type identifier.
            /// </summary>
            public Int32 DeviceId; //ULONG

            /// <summary>
            /// Vendor-unique identifier.
            /// </summary>
            public Guid VendorId; //GUID
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OPEN_VIRTUAL_DISK_PARAMETERS
        {
            /// <summary>
            /// An OPEN_VIRTUAL_DISK_VERSION enumeration that specifies the version of the OPEN_VIRTUAL_DISK_PARAMETERS structure being passed to or from the VHD functions.
            /// </summary>
            public OPEN_VIRTUAL_DISK_VERSION Version; //OPEN_VIRTUAL_DISK_VERSION

            /// <summary>
            /// A structure.
            /// </summary>
            public OPEN_VIRTUAL_DISK_PARAMETERS_Version1 Version1;
            public OPEN_VIRTUAL_DISK_PARAMETERS_Version2 Version2;
        }

        public enum OPEN_VIRTUAL_DISK_VERSION : int
        {
            /// <summary>
            /// </summary>
            OPEN_VIRTUAL_DISK_VERSION_UNSPECIFIED = 0,

            /// <summary>
            /// </summary>
            OPEN_VIRTUAL_DISK_VERSION_1 = 1,

            OPEN_VIRTUAL_DISK_VERSION_2 = 2
        }

        [Flags]
        public enum VirtualDiskAccessMask : int
        {
            /// <summary>
            /// Open the virtual disk for read-only attach access. The caller must have READ access to the virtual disk image file. If used in a request to open a virtual disk that is already open, the other handles must be limited to either VIRTUAL_DISK_ACCESS_DETACH or VIRTUAL_DISK_ACCESS_GET_INFO access, otherwise the open request with this flag will fail.
            /// </summary>
            AttachReadOnly = 0x00010000,
            /// <summary>
            /// Open the virtual disk for read-write attaching access. The caller must have (READ | WRITE) access to the virtual disk image file. If used in a request to open a virtual disk that is already open, the other handles must be limited to either VIRTUAL_DISK_ACCESS_DETACH or VIRTUAL_DISK_ACCESS_GET_INFO access, otherwise the open request with this flag will fail. If the virtual disk is part of a differencing chain, the disk for this request cannot be less than the RWDepth specified during the prior open request for that differencing chain.
            /// </summary>
            AttachReadWrite = 0x00020000,
            /// <summary>
            /// Open the virtual disk to allow detaching of an attached virtual disk. The caller must have (FILE_READ_ATTRIBUTES | FILE_READ_DATA) access to the virtual disk image file.
            /// </summary>
            Detach = 0x00040000,
            /// <summary>
            /// Information retrieval access to the VHD. The caller must have READ access to the virtual disk image file.
            /// </summary>
            GetInfo = 0x00080000,
            /// <summary>
            /// VHD creation access.
            /// </summary>
            Create = 0x00100000,
            /// <summary>
            /// Open the VHD to perform offline meta-operations. The caller must have (READ | WRITE) access to the virtual disk image file, up to RWDepth if working with a differencing chain. If the VHD is part of a differencing chain, the backing store (host volume) is opened in RW exclusive mode up to RWDepth.
            /// </summary>
            MetaOperations = 0x00200000,
            /// <summary>
            /// Allows unrestricted access to the VHD. The caller must have unrestricted access rights to the virtual disk image file.
            /// </summary>
            All = 0x003f0000,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OPEN_VIRTUAL_DISK_PARAMETERS_Version1
        {
            /// <summary>
            /// Indicates the number of stores, beginning with the child, of the backing store chain to open as read/write. The remaining stores in the differencing chain will be opened read-only. This is necessary for merge operations to succeed.
            /// </summary>
            public Int32 RWDepth; //ULONG
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OPEN_VIRTUAL_DISK_PARAMETERS_Version2
        {
            /// <summary>If TRUE, indicates the handle is only to be used to get information on the virtual disk.</summary>
            [MarshalAs(UnmanagedType.Bool)]
            public bool GetInfoOnly;

            /// <summary>If TRUE, indicates the file backing store is to be opened as read-only.</summary>
            [MarshalAs(UnmanagedType.Bool)]
            public bool ReadOnly;

            /// <summary>Resiliency GUID to specify when opening files.</summary>
            public Guid ResiliencyGuid;
        }


        public enum OPEN_VIRTUAL_DISK_FLAG : int
        {
            /// <summary>
            /// No flag specified.
            /// </summary>
            OPEN_VIRTUAL_DISK_FLAG_NONE = 0x00000000,

            /// <summary>
            /// Open the backing store without opening any differencing-chain parents. Used to correct broken parent links.
            /// </summary>
            OPEN_VIRTUAL_DISK_FLAG_NO_PARENTS = 0x00000001,

            /// <summary>
            /// Reserved.
            /// </summary>
            OPEN_VIRTUAL_DISK_FLAG_BLANK_FILE = 0x00000002,

            /// <summary>
            /// Reserved.
            /// </summary>
            OPEN_VIRTUAL_DISK_FLAG_BOOT_DRIVE = 0x00000004
        }

        public enum VIRTUAL_DISK_ACCESS_MASK : int
        {
            /// <summary>
            /// Open the virtual disk for read-only attach access. The caller must have READ access to the virtual disk image file. If used in a request to open a virtual disk that is already open, the other handles must be limited to either VIRTUAL_DISK_ACCESS_DETACH or VIRTUAL_DISK_ACCESS_GET_INFO access, otherwise the open request with this flag will fail.
            /// </summary>
            VIRTUAL_DISK_ACCESS_ATTACH_RO = 0x00010000,

            /// <summary>
            /// Open the virtual disk for read-write attaching access. The caller must have (READ | WRITE) access to the virtual disk image file. If used in a request to open a virtual disk that is already open, the other handles must be limited to either VIRTUAL_DISK_ACCESS_DETACH or VIRTUAL_DISK_ACCESS_GET_INFO access, otherwise the open request with this flag will fail. If the virtual disk is part of a differencing chain, the disk for this request cannot be less than the RWDepth specified during the prior open request for that differencing chain.
            /// </summary>
            VIRTUAL_DISK_ACCESS_ATTACH_RW = 0x00020000,

            /// <summary>
            /// Open the virtual disk to allow detaching of an attached virtual disk. The caller must have (FILE_READ_ATTRIBUTES | FILE_READ_DATA) access to the virtual disk image file.
            /// </summary>
            VIRTUAL_DISK_ACCESS_DETACH = 0x00040000,

            /// <summary>
            /// Information retrieval access to the VHD. The caller must have READ access to the virtual disk image file.
            /// </summary>
            VIRTUAL_DISK_ACCESS_GET_INFO = 0x00080000,

            /// <summary>
            /// VHD creation access.
            /// </summary>
            VIRTUAL_DISK_ACCESS_CREATE = 0x00100000,

            /// <summary>
            /// Open the VHD to perform offline meta-operations. The caller must have (READ | WRITE) access to the virtual disk image file, up to RWDepth if working with a differencing chain. If the VHD is part of a differencing chain, the backing store (host volume) is opened in RW exclusive mode up to RWDepth.
            /// </summary>
            VIRTUAL_DISK_ACCESS_METAOPS = 0x00200000,

            /// <summary>
            /// Reserved.
            /// </summary>
            VIRTUAL_DISK_ACCESS_READ = 0x000d0000,

            /// <summary>
            /// Allows unrestricted access to the VHD. The caller must have unrestricted access rights to the virtual disk image file.
            /// </summary>
            VIRTUAL_DISK_ACCESS_ALL = 0x003f0000,

            /// <summary>
            /// Reserved.
            /// </summary>
            VIRTUAL_DISK_ACCESS_WRITABLE = 0x00320000
        }

    }



}
