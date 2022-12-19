using System;
using System.Collections;
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
            //list volumes
            DriveInfo[] volumes = DriveInfo.GetDrives();
            List<DriveInfo> availableVolumes = new List<DriveInfo>();
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
            int selectedVolume;
            
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

            string selectedVolumeRoot = availableVolumes[selectedVolume].RootDirectory.ToString();

            //ask user for vhdx destination path
            Console.WriteLine("Where to put final vhdx file? (example: c:\\temp\\output.vhdx)");
            string destinationPath = Console.ReadLine();
            
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

            System.IO.DriveInfo drive = availableVolumes[selectedVolume];
            long sourceSize = drive.TotalSize;

            //create empty vhdx file on destination path
            vhdxCreator.createVHDX(destinationPath, sourceSize);

            //open the newly created file
            VirtualDiskHandler diskHandler = new VirtualDiskHandler(destinationPath);
            bool err;
            err = diskHandler.open(VirtualDiskHandler.VirtualDiskAccessMask.All);
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



            byte[] buffer = new byte[16777216];
            byte[] buffert = Enumerable.Repeat((byte)1, 16777216).ToArray();

            UInt64 bytesTotal = 0;
            uint bytesRead = reader.read((uint)buffer.Length, buffer);
            float lastPercentage = 0.0f;
            Console.Write("Converting volume to vhdx:");
            while (bytesRead > 0)
            {
                writer.write(buffer, bytesRead);
                bytesRead = reader.read((uint)buffer.Length, buffer);
                bytesTotal += bytesRead;

                //calculate output
                double percentage = Math.Round((double)((double)bytesTotal / (double)sourceSize) * 100.0, 2);
                if (percentage != lastPercentage)
                {
                    Console.Write("\rConverting volume to vhdx: " + percentage + "%");
                }
            }
            Console.Write("\rConverting volume to vhdx: completed");

            //close all handles
            reader.close();
            writer.close();
            diskHandler.detach();
            diskHandler.close();

            //delete vhdx snaphsot
            vss.deleteSnapshot(meta.setID);


            //string[] files = System.IO.Directory.GetFileSystemEntries(meta.path);


        }
    }
}
