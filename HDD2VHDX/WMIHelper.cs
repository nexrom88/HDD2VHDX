using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace HDD2VHDX
{
    public class WMIHelper
    {
        //gets all current mounted drives with no drive letter
        public static List<GuestVolume> getMountedDrives()
        {
            List<GuestVolume> drives = new List<GuestVolume>();

            string scopeStr = @"\\.\root\cimv2";


            ManagementScope scope = new ManagementScope(scopeStr);
            scope.Connect();

            string queryString = "SELECT * FROM Win32_Volume WHERE DriveLetter IS NOT NULL";
            SelectQuery query = new SelectQuery(queryString);
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query))
            {
                foreach (ManagementObject disk in searcher.Get())
                {
                    GuestVolume volume = new GuestVolume();
                    volume.path = disk["Name"].ToString();

                    if (disk["Label"] == null)
                    {
                        volume.caption = "Unbenanntes Laufwerk";
                    }
                    else
                    {
                        volume.caption = disk["Label"].ToString();
                    }
                    drives.Add(volume);

                }
            }
            return drives;

        }

        public struct GuestVolume
        {
            public string path;
            public string caption;
        }
    }
}
