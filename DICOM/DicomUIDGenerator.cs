// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom
{
    using Dicom.Network;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Numerics;
    using System.Text;

    /// <summary>
    /// Class for generating DICOM UIDs.
    /// </summary>
    public class DicomUIDGenerator
    {
        #region FIELDS

        private static volatile DicomUID instanceRootUid = null;

        private static long lastTicks = 0;

        private static readonly object GenerateUidLock = new object();

        private static readonly DateTime Y2K = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly Dictionary<string, DicomUID> uidMap = new Dictionary<string, DicomUID>();

        #endregion

        #region PROPERTIES

        private static DicomUID InstanceRootUID
        {
            get
            {
                if (instanceRootUid == null)
                {
                    lock (GenerateUidLock)
                    {
                        if (instanceRootUid == null)
                        {
                            DicomUID dicomUid;
                            if (NetworkManager.TryGetNetworkIdentifier(out dicomUid)) return dicomUid;

                            instanceRootUid = DicomUID.Append(DicomImplementation.ClassUID, Environment.TickCount);
                        }
                    }
                }
                return instanceRootUid;
            }
        }

        #endregion

        #region METHODS

        /// <summary>
        /// Generate a new DICOM UID.
        /// </summary>
        /// <param name="sourceUid">Source UID.</param>
        /// <returns>Generated UID.</returns>
        public DicomUID Generate(DicomUID sourceUid = null)
        {
            lock (GenerateUidLock)
            {
                DicomUID destinationUid;
                if (sourceUid != null && this.uidMap.TryGetValue(sourceUid.UID, out destinationUid)) return destinationUid;

                var ticks = DateTime.UtcNow.Subtract(Y2K).Ticks;
                if (ticks == lastTicks) ++ticks;
                lastTicks = ticks;

                var str = ticks.ToString();
                if (str.EndsWith("0000")) str = str.Substring(0, str.Length - 4);

                //too long - 39 characters
                //Guid guid = Guid.NewGuid();
                //string str = ConvertGuidToUuidInteger(guid);

                var uid = new StringBuilder();
                uid.Append(InstanceRootUID.UID).Append('.').Append(str);

                destinationUid = new DicomUID(uid.ToString(), "SOP Instance UID", DicomUidType.SOPInstance);

                if (sourceUid != null) this.uidMap.Add(sourceUid.UID, destinationUid);

                return destinationUid;
            }
        }

        /// <summary>
        /// Regenerate all UIDs in a DICOM dataset.
        /// </summary>
        /// <param name="dataset">Dataset in which UIDs should be regenerated.</param>
        public void RegenerateAll(DicomDataset dataset)
        {
            foreach (var ui in dataset.Where(x => x.ValueRepresentation == DicomVR.UI).ToArray())
            {
                var uid = dataset.Get<DicomUID>(ui.Tag);
                if (uid.Type == DicomUidType.SOPInstance || uid.Type == DicomUidType.Unknown) dataset.AddOrUpdate(ui.Tag, this.Generate(uid));
            }

            foreach (var sq in dataset.Where(x => x.ValueRepresentation == DicomVR.SQ).Cast<DicomSequence>().ToArray())
            {
                foreach (var item in sq)
                {
                    this.RegenerateAll(item);
                }
            }
        }

#if !NET35
        /// <summary>
        /// Generate a UUID-derived UID, according to ftp://medical.nema.org/medical/dicom/current/output/html/part05.html#sect_B.2
        /// </summary>
        /// <returns>A new UID with 2.25 prefix</returns>
        //public static DicomUID GenerateDerivedFromUUID()
        //{            
        //    var guid = Guid.NewGuid().ToByteArray();
        //    var bigint = new System.Numerics.BigInteger(guid);
        //    if (bigint < 0) bigint = -bigint;
        //    var uid = "2.25." + bigint;

        //    return new DicomUID(uid, "Local UID", DicomUidType.Unknown);
        //}

        /// <summary>
        /// Generate a UUID-derived UID, according to http://medical.nema.org/medical/dicom/current/output/html/part05.html#sect_B.2
        /// </summary>
        /// <returns>A new UID with 2.25 prefix.</returns>
        public static DicomUID GenerateDerivedFromUUID()
        {
            var guid = Guid.NewGuid();
            return new DicomUID("2.25." + ConvertGuidToUuidInteger(guid), "Local UID", DicomUidType.Unknown);
        }

        /// <summary>
        /// Converts a .NET Guid to a UUID in OID format.
        /// </summary>
        /// <remarks>Method is internal to support access to unit tests.</remarks>
        internal static string ConvertGuidToUuidInteger(Guid value)
        {
            // ISO/IEC 9834-8, paragraph 6.3 (referenced by DICOM PS 3.5, B.2) defines how
            // to convert a UUID to a single integer value that can be converted back into a UUID.

            // The Guid.ToByteArray Method returns the array in a strange order (see .NET docs),
            // BigInteger expects the input array in little endian order.
            // The last byte controls the sign, add an additional zero to ensure
            // the array is parsed as a positive number.
            var octets = value.ToByteArray();
            var littleEndianOrder = new byte[]
                { octets[15], octets[14], octets[13], octets[12], octets[11], octets[10], octets[9], octets[8],
                  octets[6], octets[7], octets[4], octets[5], octets[0], octets[1], octets[2], octets[3], 0 };

            return (new BigInteger(littleEndianOrder)).ToString(CultureInfo.InvariantCulture);
        }
#endif
        #endregion
    }
}
