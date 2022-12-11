using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Vss;

namespace HDD2VHDX
{
    public class VSSWrapper
    {

        private IVssFactory vss;
        private IVssBackupComponents backupComponent;

        //init vss system and backup component
        public VSSWrapper()
        {
            this.vss = VssFactoryProvider.Default.GetVssFactory();

            this.backupComponent = vss.CreateVssBackupComponents();

            backupComponent.InitializeForBackup(null);
        }


        //performs a vss snapshot and returns the set id
        public SnapshotMeta performSnapshot(string[] drivesToSnapshot)
        {

            backupComponent.GatherWriterMetadata();

            backupComponent.FreeWriterMetadata();

            VSSSnapshot snapshot = new VSSSnapshot(backupComponent);

            List<Guid> snapshotGuidList = new List<Guid>();
            //add all volumes to list
            foreach (string volume in drivesToSnapshot)
            {
                snapshotGuidList.Add(snapshot.AddVolume(volume));
            }

            backupComponent.SetBackupState(false, true, VssBackupType.Full, false);

            backupComponent.PrepareForBackup();

            snapshot.Copy();


            //build retVal
            SnapshotMeta meta = new SnapshotMeta();
            meta.setID = snapshot.SnapshotSetID.ToString();

            //iterate snapshots (every volume is a single snapshot)
            List<VssSnapshotProperties> snapshotList = new List<VssSnapshotProperties>();
            foreach (Guid g in snapshotGuidList)
            {
                VssSnapshotProperties properties = backupComponent.GetSnapshotProperties(g);
                snapshotList.Add(properties);
            }
            meta.snapshots = snapshotList.ToArray();

            return meta;

        }

        //deletes a given vss snaphot identified by its id
        public void deleteSnapshot(string setID)
        {
            Guid snapshotGUID = new Guid(setID);

            backupComponent.DeleteSnapshotSet(snapshotGUID, true);
        }
    }

    public struct SnapshotMeta
    {
        public string setID;
        public VssSnapshotProperties[] snapshots;
    }
}
