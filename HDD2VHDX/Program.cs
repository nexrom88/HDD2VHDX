using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HDD2VHDX
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //args count valid?
            if (args.Length != 0 && args.Length != 2)
            {
                Console.WriteLine("Error: invalid arguments.");
                Console.WriteLine("Usage: HDD2VHDX.exe *source* *destination*");
                Console.WriteLine(@"Usage example: HDD2VHDX.exe c:\ d:\output.vhdx");
                Console.WriteLine("Hint: Destination may not contain whitespaces.");
                Console.Read();
                return;
            }

            //args given?
            bool argsGiven = false;
            string destinationPath = String.Empty;
            string selectedVolumeRoot = String.Empty;
            List<DriveInfo> availableVolumes = null;
            int selectedVolume = -1;
            if (args.Length == 2)
            {
                //list drives
                availableVolumes = DriveInfo.GetDrives().ToList();

                selectedVolumeRoot = args[0];
                destinationPath = args[1];
                argsGiven = true;

                //look for volume within availableVolumes list
                int volumeCounter = 0;
                bool volumeFound = false;
                foreach (DriveInfo dr in availableVolumes)
                {
                    if (dr.RootDirectory.ToString().ToLower() == selectedVolumeRoot.ToLower())
                    {
                        //drive found
                        selectedVolume = volumeCounter;
                        volumeFound = true;
                        break;
                    }
                    volumeCounter++;
                }

                if (!volumeFound)
                {
                    Console.WriteLine("Error: source volume cannot be found");
                    Console.Read();
                    return;
                }
            }

            if (!argsGiven)
            {
                //list volumes
                availableVolumes = new List<DriveInfo>();
                DriveInfo[] volumes = DriveInfo.GetDrives();
                int volumeCounter = 0;
                Console.WriteLine("Available volumes to convert to vhdx:");

                foreach (DriveInfo volume in volumes)
                {
                    //just use "fixed" volumes
                    if (volume.DriveType.ToString() == "Fixed")
                    {
                        try
                        {
                            string rootPath = volume.RootDirectory.ToString();
                            string volumeName = volume.VolumeLabel != "" ? volume.VolumeLabel : "nameless volume";
                            Console.WriteLine(volumeCounter + ") " + volumeName + " (" + rootPath + ")");
                        }
                        catch (Exception)
                        {
                            //go to next volume on error. e.g. Bitlocker encrypted volume can cause an exception here
                            continue;
                        }

                        volumeCounter++;
                        availableVolumes.Add(volume);
                    }

                }
                volumeCounter--;

                Console.WriteLine("Please select the volume to be converted to vhdx file (0 - " + volumeCounter + "):");
                string volumeString = Console.ReadLine();

                //parse input
                if (!int.TryParse(volumeString, out selectedVolume))
                {
                    Console.WriteLine("Error: invalid input. Terminating.");
                    Console.Read();
                    return;
                }

                //input is in the given range?
                if (selectedVolume > volumeCounter || selectedVolume < 0)
                {
                    Console.WriteLine("Error: invalid input. Terminating.");
                    Console.Read();
                    return;
                }

                selectedVolumeRoot = availableVolumes[selectedVolume].RootDirectory.ToString();

                //ask user for vhdx destination path
                Console.WriteLine("Where to put final vhdx file? (example: c:\\temp\\output.vhdx)");
                destinationPath = Console.ReadLine();

            }
            
            //add ".vhdx" if necessary
            if (!destinationPath.EndsWith(".vhdx"))
            {
                destinationPath += ".vhdx";
            }

            //is given destination path just a directory?
            if (destinationPath.EndsWith("\\.vhdx"))
            {
                Console.WriteLine("Error: the given destination path has no file name");
                Console.Read();
                return;
            }

            //get path from user input
            string path = System.IO.Path.GetDirectoryName(destinationPath);

            //create directory if necessary
            System.IO.Directory.CreateDirectory(path);

      
            //enough free space?
            DriveInfo destVolume = new DriveInfo(Path.GetPathRoot(destinationPath));
            if (destVolume.AvailableFreeSpace < availableVolumes[selectedVolume].AvailableFreeSpace)
            {
                Console.WriteLine("Error: not enough free disk space on destination volume");
                Console.Read();
                return;
            }

            //perform vss snapshot
            VSSWrapper vss = new VSSWrapper();
            string[] drivesToSnapshot = new string[1] { selectedVolumeRoot };
            SnapshotMeta meta = vss.performSnapshot(drivesToSnapshot);

            string devicePath = meta.snapshots[0].SnapshotDeviceObject;

            //read cluster size from snapshot source
            int sectorsPerCluster = 0;
            int bytesPerSector = 0;
            int dummy = 0;
            uint sourceClusterSize;
            int totalClusterCount = 0;
            DeviceIO.GetDiskFreeSpace(availableVolumes[selectedVolume].RootDirectory.FullName, out sectorsPerCluster, out bytesPerSector, out dummy, out totalClusterCount);
            sourceClusterSize = (UInt32)sectorsPerCluster * (UInt32)bytesPerSector;

            //read source volume size
            System.IO.DriveInfo drive = availableVolumes[selectedVolume];
            long sourceSize = drive.TotalSize;

            //create empty vhdx file on destination path
            vhdxCreator.createVHDX(destinationPath, sourceSize);

            //open the newly created file
            VirtualDiskHandler diskHandler = new VirtualDiskHandler(destinationPath);
            bool err;
            err = diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.All | VirtualDiskHandler.VirtualDiskAccessMask.MetaOperations);
            err = diskHandler.attach(VirtualDiskHandler.ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME);
            string mountedPath = diskHandler.getAttachedPath();

            //init vhdx file
            VirtualDiskHandler.WriteableRawVolume newVolume = diskHandler.initDisk(mountedPath);

            //cleanup when volumePath is not valid
            if (!newVolume.isValid || newVolume.handle.IsInvalid)
            {
                diskHandler.detach();
                diskHandler.close();

                vss.deleteSnapshot(meta.setID);
                Console.WriteLine("Error: could not initialize new vhdx file");
                Console.Read();
                return;
            }


            //now write data from source to vhdx file
            DeviceWrapper reader = new DeviceWrapper(devicePath, DeviceIO.GENERIC_READ);
            DeviceWrapper writer = new DeviceWrapper(newVolume.handle);

            //get cluster count
            UInt32 clusterCount = (UInt32)(availableVolumes[selectedVolume].TotalSize / (long)sourceClusterSize);

            //read source volume bitmap
            byte[] clusterBitmap = readClusterBitmap(reader.getVolumeHandle(), clusterCount);

            //System.IO.File.WriteAllBytes("e:\\output.bin", clusterBitmap);

            UInt64 currentCluster = 0;

            //prepare buffer and align it to cluster size
            byte[] buffer = new byte[sourceClusterSize];

            UInt64 bytesTotal = 0;
            float lastPercentage = 0.0f;
            Console.Write("Converting volume to vhdx:");
            while (currentCluster < (UInt64)clusterBitmap.Length * 8)
            {
                //is current cluster available?
                if (!isClusterAvailable(clusterBitmap, currentCluster))
                {
                    //not available, jump to next cluster
                    reader.setFilePointer(sourceClusterSize);
                    writer.setFilePointer(sourceClusterSize);
                    currentCluster++;
                    bytesTotal += sourceClusterSize;
                    continue;
                }

                reader.read(sourceClusterSize, buffer);
                writer.write(buffer, sourceClusterSize);


                bytesTotal += sourceClusterSize;
                currentCluster++;

                //calculate output
                double percentage = Math.Round((double)((double)bytesTotal / (double)sourceSize) * 100.0, 2);
                if (percentage != lastPercentage)
                {
                    Console.Write("\rConverting volume to vhdx: " + percentage + "%");
                }
            }
            Console.WriteLine("\rConverting volume to vhdx: completed");

            //close all handles
            reader.close();
            writer.close();
            diskHandler.detach();
            diskHandler.close();



            //reopen and reattach vhdx, then shrink vhdx file
            //Console.WriteLine("Trying to shrink output file. This might take some time...");
            //diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.MetaOperations | VirtualDiskHandler.VirtualDiskAccessMask.AttachReadOnly);
            //diskHandler.shrinkFile();
            //diskHandler.close();

            //delete vhdx snaphsot
            vss.deleteSnapshot(meta.setID);

        }

        private static bool isClusterAvailable(byte[] clusterBitmap, UInt64 clusterIndex)
        {
            UInt64 byteArrIndex = clusterIndex / 8;
            int clusterByte = clusterBitmap[byteArrIndex];
            UInt64 byteOffset = clusterIndex % 8;

            //shift, so that relevant bit is on the "right end"
            clusterByte = clusterByte >> (int)byteOffset;

            //compare to bitmask
            bool clusterAvailable = (clusterByte & 0b1) == 1;

            return clusterAvailable;
        }

        //reads the volume cluster bitmap from a given volume
        private unsafe static byte[] readClusterBitmap(DeviceIO.VolumeSafeHandle volumeHandle, UInt32 clusterCount)
        {
            byte[] rawBitmap;
            byte[] clusterBitmap = new byte[(clusterCount / 8) + 17]; // +17 because of 2x LARGE_INTEGER overhead + 1 byte alignment
            int bytesReturned = 0;

            fixed (byte* inputBufferPtr = new byte[8]) {
                fixed (byte* ptr = clusterBitmap) {
                    DeviceIO.DeviceIoControl(volumeHandle, DeviceIO.FSCTL_GET_VOLUME_BITMAP, (IntPtr)inputBufferPtr, 8, (IntPtr)ptr, clusterBitmap.Length, ref bytesReturned, IntPtr.Zero);
                    Int64 startingLCN = BitConverter.ToInt64(clusterBitmap, 0);                   
                    if (Marshal.GetLastWin32Error() == 0)
                    {
                        //build return byte arr
                        rawBitmap = new byte[bytesReturned -16];
                        Marshal.Copy(IntPtr.Add((IntPtr)ptr, 16), rawBitmap, 0, bytesReturned -16);
                        return rawBitmap;
                    }
                    else
                    {
                        return null; //on error return null
                    }
                    
                }
            }
        }
    }
}
