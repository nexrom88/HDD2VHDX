using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HDD2VHDX
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string disk = @"c:\";
            VSSWrapper vss = new VSSWrapper();

            string[] drivesToSnapshot = new string[1] { disk };
            SnapshotMeta meta = vss.performSnapshot(drivesToSnapshot);

            string devicePath = meta.snapshots[0].SnapshotDeviceObject;


            System.IO.DriveInfo drive = new DriveInfo(disk);

            vhdxCreator.createVHDX(@"e:\test.vhdx", drive.TotalSize);

            VirtualDiskHandler diskHandler = new VirtualDiskHandler(@"e:\test.vhdx");


            bool err;
            err = diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.All);
            err = diskHandler.attach(VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME);


            string mountedPath = diskHandler.getAttachedPath();

            VirtualDiskHandler.WriteableRawVolume newVolume = diskHandler.initDisk(mountedPath);

            //cleanup when volumePath is not valid
            if (!newVolume.isValid || newVolume.handle.IsInvalid)
            {
                diskHandler.detach();
                diskHandler.close();

                vss.deleteSnapshot(meta.setID);
                return;
            }

            DeviceWrapper reader = new DeviceWrapper(devicePath, DeviceIO.GENERIC_READ);
            DeviceWrapper writer = new DeviceWrapper(newVolume.handle);
            byte[] buffer = new byte[16777216];
            byte[] buffert = Enumerable.Repeat((byte)1, 16777216).ToArray();

            UInt64 bytesTotal = 0;
            uint bytesRead = reader.read((uint)buffer.Length, buffer);
            while (bytesRead > 0)
            {
                writer.write(buffer, bytesRead);
                bytesRead = reader.read((uint)buffer.Length, buffer);
                bytesTotal += bytesRead;
                Console.WriteLine(bytesTotal);
            }

            reader.close();
            writer.close();
            diskHandler.detach();
            diskHandler.close();

            vss.deleteSnapshot(meta.setID);




            //string[] files = System.IO.Directory.GetFileSystemEntries(meta.path);


        }
    }
}
