using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace HDD2VHDX
{

    public class vhdxCreator
    {
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        private static extern Int32 CreateVirtualDisk(ref VIRTUAL_STORAGE_TYPE VirtualStorageType, String Path, VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask, IntPtr SecurityDescriptor, CREATE_VIRTUAL_DISK_FLAG Flags, Int32 ProviderSpecificFlags, ref CREATE_VIRTUAL_DISK_PARAMETERS Parameters, IntPtr Overlapped, ref VirtualDiskSafeHandle Handle);

        private const int VIRTUAL_STORAGE_TYPE_DEVICE_VHDX = 3;
        private static readonly Guid VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT = new Guid("EC984AEC-A0F9-47e9-901F-71415A66345B");
        private const int CREATE_VIRTUAL_DISK_PARAMETERS_DEFAULT_SECTOR_SIZE = 0x200;

        public static void createVHDX(string path, Int64 size)
        {
            VirtualDiskSafeHandle diskHandle = new VirtualDiskSafeHandle();
            //build storage type
            VIRTUAL_STORAGE_TYPE storageType = new VIRTUAL_STORAGE_TYPE();
            storageType.DeviceId = VIRTUAL_STORAGE_TYPE_DEVICE_VHDX;
            storageType.VendorId = VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT;

            //build parameters
            var parameters = new CREATE_VIRTUAL_DISK_PARAMETERS();
            parameters.Version = CREATE_VIRTUAL_DISK_VERSION.CREATE_VIRTUAL_DISK_VERSION_1;
            parameters.Version1.BlockSizeInBytes = 0;
            parameters.Version1.MaximumSize = size + 512000;
            parameters.Version1.ParentPath = IntPtr.Zero;
            parameters.Version1.SectorSizeInBytes = CREATE_VIRTUAL_DISK_PARAMETERS_DEFAULT_SECTOR_SIZE;
            parameters.Version1.SourcePath = IntPtr.Zero;
            parameters.Version1.UniqueId = Guid.Empty;

            CreateVirtualDisk(ref storageType, path, VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_ALL, IntPtr.Zero, CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_NONE, 0, ref parameters, IntPtr.Zero, ref diskHandle);
            diskHandle.Close();
        }
    }

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

    public enum CREATE_VIRTUAL_DISK_FLAG : int
    {
        /// <summary>
        /// No special creation conditions; system defaults are used.
        /// </summary>
        CREATE_VIRTUAL_DISK_FLAG_NONE = 0x00000000,

        /// <summary>
        /// Pre-allocate all physical space necessary for the size of the virtual disk.
        /// </summary>
        CREATE_VIRTUAL_DISK_FLAG_FULL_PHYSICAL_ALLOCATION = 0x00000001
    }

    public struct CREATE_VIRTUAL_DISK_PARAMETERS
    {
        /// <summary>
        /// A CREATE_VIRTUAL_DISK_VERSION enumeration that specifies the version of the CREATE_VIRTUAL_DISK_PARAMETERS structure being passed to or from the virtual hard disk (VHD) functions.
        /// </summary>
        public CREATE_VIRTUAL_DISK_VERSION Version; //CREATE_VIRTUAL_DISK_VERSION

        /// <summary>
        /// A structure.
        /// </summary>
        public CREATE_VIRTUAL_DISK_PARAMETERS_Version1 Version1;
    }

    public enum CREATE_VIRTUAL_DISK_VERSION : int
    {
        /// <summary>
        /// </summary>
        CREATE_VIRTUAL_DISK_VERSION_UNSPECIFIED = 0,

        /// <summary>
        /// </summary>
        CREATE_VIRTUAL_DISK_VERSION_1 = 1
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CREATE_VIRTUAL_DISK_PARAMETERS_Version1
    {
        /// <summary>
        /// Unique identifier to assign to the virtual disk object. If this member is set to zero, a unique identifier is created by the system.
        /// </summary>
        public Guid UniqueId; //GUID

        /// <summary>
        /// The maximum virtual size of the virtual disk object. Must be a multiple of 512. If a ParentPath is specified, this value must be zero. If a SourcePath is specified, this value can be zero to specify the size of the source VHD to be used, otherwise the size specified must be greater than or equal to the size of the source disk.
        /// </summary>
        public Int64 MaximumSize; //ULONGLONG

        /// <summary>
        /// Internal size of the virtual disk object blocks. If value is 0, block size will be automatically matched to the parent or source disk's setting if ParentPath or SourcePath is specified (otherwise a block size of 2MB will be used).
        /// </summary>
        public Int32 BlockSizeInBytes; //ULONG

        /// <summary>
        /// Internal size of the virtual disk object sectors. Must be set to 512.
        /// </summary>
        public Int32 SectorSizeInBytes; //ULONG

        /// <summary>
        /// Optional path to a parent virtual disk object. Associates the new virtual disk with an existing virtual disk. If this parameter is not NULL, SourcePath must be NULL.
        /// </summary>
        public IntPtr ParentPath; //PCWSTR

        /// <summary>
        /// Optional path to pre-populate the new virtual disk object with block data from an existing disk. This path may refer to a VHD or a physical disk. If this parameter is not NULL, ParentPath must be NULL.
        /// </summary>
        public IntPtr SourcePath; //PCWSTR
    }

    [SecurityPermission(SecurityAction.Demand)]
    public class VirtualDiskSafeHandle : SafeHandle
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA5122:PInvokesShouldNotBeSafeCriticalFxCopRule", Justification = "Warning is bogus.")]
        [DllImportAttribute("kernel32.dll", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern Boolean CloseHandle(IntPtr hObject);

        public VirtualDiskSafeHandle()
            : base(IntPtr.Zero, true) { }


        public override bool IsInvalid
        {
            get { return (this.IsClosed) || (base.handle == IntPtr.Zero); }
        }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(this.handle);
        }

        public override string ToString()
        {
            return this.handle.ToString();
        }

        public IntPtr Handle
        {
            get { return handle; }
        }

    }


}
