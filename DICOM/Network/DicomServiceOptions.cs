// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.Network
{
    /// <summary>
    /// Options to control the behavior of the <see cref="DicomService"/> base class.
    /// </summary>
    public class DicomServiceOptions
    {
        #region INNER TYPES

        private static readonly object _locker = new object();

        private static DicomServiceOptions _Instance = new DicomServiceOptions();

        public static DicomServiceOptions Default
        {
            get
            {
                lock (_locker)
                {
                    //copy to prevent multi-thread access to member fields
                    return new DicomServiceOptions(_Instance);
                }
            }
        }

        public static void SetDefault(DicomServiceOptions newOpts)
        {
            if (newOpts != null)
            {
                lock (_locker)
                {
                    _Instance = new DicomServiceOptions(newOpts);
                }
            }
        }
        

        /// <summary>Default options for use with the <see cref="DicomService"/> base class.</summary>
        private static class DefaultValues
        {
            public static readonly bool LogDataPDUs = false;

            public static readonly bool LogDimseDatasets = false;

            public static readonly bool UseRemoteAEForLogName = false;

            public static readonly uint MaxCommandBuffer = 1 * 1024; //1KB

            public static readonly uint MaxDataBuffer = 1 * 1024 * 1024; //1MB

            public static readonly bool IgnoreSslPolicyErrors = false;

            public static readonly bool TcpNoDelay = true;
        }

        #endregion

        #region CONSTRUCTORS

        /// <summary>Constructor</summary>
        public DicomServiceOptions()
        {
            LogDataPDUs = DefaultValues.LogDataPDUs;
            LogDimseDatasets = DefaultValues.LogDimseDatasets;
            UseRemoteAEForLogName = DefaultValues.UseRemoteAEForLogName;
            MaxCommandBuffer = DefaultValues.MaxCommandBuffer;
            MaxDataBuffer = DefaultValues.MaxDataBuffer;
            IgnoreSslPolicyErrors = DefaultValues.IgnoreSslPolicyErrors;
            TcpNoDelay = DefaultValues.TcpNoDelay;
        }

        public DicomServiceOptions(DicomServiceOptions src)
        {
            LogDataPDUs = src.LogDataPDUs;
            LogDimseDatasets = src.LogDimseDatasets;
            UseRemoteAEForLogName = src.UseRemoteAEForLogName;
            MaxCommandBuffer = src.MaxCommandBuffer;
            MaxDataBuffer = src.MaxDataBuffer;
            IgnoreSslPolicyErrors = src.IgnoreSslPolicyErrors;
            TcpNoDelay = src.TcpNoDelay;
        }
        #endregion

        #region PROPERTIES

        /// <summary>Write message to log for each P-Data-TF PDU sent or received.</summary>
        public bool LogDataPDUs { get; set; }

        /// <summary>Write command and data datasets to log.</summary>
        public bool LogDimseDatasets { get; set; }

        /// <summary>Use the AE Title of the remote host as the log name.</summary>
        public bool UseRemoteAEForLogName { get; set; }

        /// <summary>Maximum buffer length for command PDVs when generating P-Data-TF PDUs.</summary>
        public uint MaxCommandBuffer { get; set; }

        /// <summary>Maximum buffer length for data PDVs when generating P-Data-TF PDUs.</summary>
        public uint MaxDataBuffer { get; set; }

        /// <summary>DICOM client should ignore SSL certificate errors.</summary>
        public bool IgnoreSslPolicyErrors { get; set; }

        /// <summary>Enable or disable TCP Nagle algorithm.</summary>
        public bool TcpNoDelay { get; set; }

        #endregion
    }
}
