using Alphaleonis.Win32.Vss;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HDD2VHDX
{
    public class VSSSnapshot : IDisposable
    {
        /// <summary>A reference to the VSS context.</summary>
        IVssBackupComponents _backup;

        /// <summary>Metadata about this object's snapshot.</summary>
        VssSnapshotProperties _props;

        /// <summary>Identifier for the overall shadow copy.</summary>
        public Guid SnapshotSetID { get; }

        /// <summary>Identifier for our single snapshot volume.</summary>
        Guid _snap_id;

        /// <summary>
        /// Initializes a snapshot.  We save the GUID of this snap in order to
        /// refer to it elsewhere in the class.
        /// </summary>
        /// <param name="backup">A VssBackupComponents implementation for the current OS.</param>
        public VSSSnapshot(IVssBackupComponents backup)
        {
            _backup = backup;
            SnapshotSetID = backup.StartSnapshotSet();
        }

        /// <summary>
        /// Dispose of the shadow copies created by this instance.
        /// </summary>
        public void Dispose()
        {
            try { Delete(); } catch { }
        }

        /// <summary>
        /// Adds a volume to the current snapshot.
        /// </summary>
        /// <param name="volumeName">Name of the volume to add (eg. "C:\").</param>
        /// <remarks>
        /// Note the IsVolumeSupported check prior to adding each volume.
        /// </remarks>
        public Guid AddVolume(string volumeName)
        {
            if (_backup.IsVolumeSupported(volumeName))
                return _backup.AddToSnapshotSet(volumeName);
            else
                throw new VssVolumeNotSupportedException(volumeName);
        }

        /// <summary>
        /// Create the actual snapshot.  This process can take around 10s.
        /// </summary>
        public void Copy()
        {
            _backup.DoSnapshotSet();
        }

        /// <summary>
        /// Remove all snapshots.
        /// </summary>
        public void Delete()
        {
            _backup.DeleteSnapshotSet(SnapshotSetID, false);
        }

        /// <summary>
        /// Gets the string that identifies the root of this snapshot.
        /// </summary>
        public string Root
        {
            get
            {
                if (_props == null)
                    _props = _backup.GetSnapshotProperties(_snap_id);
                return _props.SnapshotDeviceObject;
            }
        }
    }
}
