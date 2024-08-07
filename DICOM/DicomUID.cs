﻿// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;

    public enum DicomUidType
    {
        TransferSyntax,
        SOPClass,
        MetaSOPClass,
        ServiceClass,
        SOPInstance,
        ApplicationContextName,
        ApplicationHostingModel,
        CodingScheme,
        FrameOfReference,
        LDAP,
        MappingResource,
        ContextGroupName,
        Unknown
    }

    public enum DicomStorageCategory
    {
        None,
        Image,
        PresentationState,
        StructuredReport,
        Waveform,
        Document,
        Raw,
        Other,
        Private,
        Volume
    }

    public sealed partial class DicomUID : DicomParseable
    {
        public static string RootUID { get; set; }

        private string _uid;

        private string _name;

        private DicomUidType _type;

        private bool _retired;

        public DicomUID(string uid, string name, DicomUidType type, bool retired = false)
        {
            _uid = uid;
            _name = name;
            _type = type;
            _retired = retired;
        }

        public string UID
        {
            get
            {
                return _uid;
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public DicomUidType Type
        {
            get
            {
                return _type;
            }
        }

        public bool IsRetired
        {
            get
            {
                return _retired;
            }
        }

        public static void Register(DicomUID uid)
        {
            _uids.Add(uid.UID, uid);
        }

        public static DicomUID Generate(string name)
        {
            if (string.IsNullOrEmpty(RootUID))
            {
                RootUID = "1.2.826.0.1.3680043.2.1343.1";
            }

            var uid = $"{RootUID}.{DateTime.UtcNow}.{DateTime.UtcNow.Ticks}";

            return new DicomUID(uid, name, DicomUidType.SOPInstance);
        }

        public static DicomUID Generate()
        {
            var generator = new DicomUIDGenerator();
            return generator.Generate();
        }

        public static DicomUID Append(DicomUID baseUid, long nextSeq)
        {
            StringBuilder uid = new StringBuilder();
            uid.Append(baseUid.UID).Append('.').Append(nextSeq);
            return new DicomUID(uid.ToString(), "SOP Instance UID", DicomUidType.SOPInstance);
        }

        public static bool IsValid(string uid)
        {
            if (String.IsNullOrEmpty(uid)) return false;

            // only checks that the UID contains valid characters
            foreach (char c in uid)
            {
                if (c != '.' && !Char.IsDigit(c)) return false;
            }

            return true;
        }

        public static DicomUID Parse(string s)
        {
            string u = s.TrimEnd(' ', '\0');

            DicomUID uid = null;
            if (_uids.TryGetValue(u, out uid)) return uid;

            //if (!IsValid(u))
            //	throw new DicomDataException("Invalid characters in UID string ['" + u + "']");

            return new DicomUID(u, "Unknown", DicomUidType.Unknown);
        }

        private static IDictionary<string, DicomUID> _uids;

        static DicomUID()
        {
            _uids = new ConcurrentDictionary<string, DicomUID>();
            LoadInternalUIDs();
            LoadPrivateUIDs();
        }

        public static IEnumerable<DicomUID> Enumerate()
        {
            return _uids.Values;
        }

        public bool IsImageStorage
        {
            get
            {
                return StorageCategory == DicomStorageCategory.Image;
            }
        }

        public DicomStorageCategory StorageCategory
        {
            get
            {
                if (!UID.StartsWith("1.2.840.10008") && Type == DicomUidType.SOPClass)
                {
                    return DicomStorageCategory.Private;
                }

                if (Type != DicomUidType.SOPClass || Name.StartsWith("Storage Commitment") || !Name.Contains("Storage"))
                {
                    return DicomStorageCategory.None;
                }

                if (Name.Contains("Image Storage"))
                {
                    return DicomStorageCategory.Image;
                }

                if (Name.Contains("Volume Storage"))
                {
                    return DicomStorageCategory.Volume;
                }

                if (this == BlendingSoftcopyPresentationStateStorage
                    || this == ColorSoftcopyPresentationStateStorage
                    || this == GrayscaleSoftcopyPresentationStateStorage
                    || this == PseudoColorSoftcopyPresentationStateStorage)
                {
                    return DicomStorageCategory.PresentationState;
                }

                if (this == AudioSRStorageTrialRETIRED
                    || this == BasicTextSRStorage
                    || this == ChestCADSRStorage
                    || this == ComprehensiveSRStorage
                    || this == ComprehensiveSRStorageTrialRETIRED
                    || this == DetailSRStorageTrialRETIRED
                    || this == EnhancedSRStorage
                    || this == MammographyCADSRStorage
                    || this == TextSRStorageTrialRETIRED
                    || this == XRayRadiationDoseSRStorage)
                {
                    return DicomStorageCategory.StructuredReport;
                }

                if (this == AmbulatoryECGWaveformStorage
                    || this == BasicVoiceAudioWaveformStorage
                    || this == CardiacElectrophysiologyWaveformStorage
                    || this == GeneralECGWaveformStorage
                    || this == HemodynamicWaveformStorage
                    || this == TwelveLeadECGWaveformStorage
                    || this == WaveformStorageTrialRETIRED)
                {
                    return DicomStorageCategory.Waveform;
                }

                if (this == EncapsulatedCDAStorage
                    || this == EncapsulatedPDFStorage)
                {
                    return DicomStorageCategory.Document;
                }

                if (this == RawDataStorage)
                {
                    return DicomStorageCategory.Raw;
                }

                return DicomStorageCategory.Other;
            }
        }

        public static bool operator ==(DicomUID a, DicomUID b)
        {
            if (((object)a == null) && ((object)b == null)) return true;
            if (((object)a == null) || ((object)b == null)) return false;
            return a.UID == b.UID;
        }

        public static bool operator !=(DicomUID a, DicomUID b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (!(obj is DicomUID)) return false;
            return (obj as DicomUID).UID == UID;
        }

        public override int GetHashCode()
        {
            return UID.GetHashCode();
        }

        public override string ToString()
        {
            return String.Format("{0} [{1}]", Name, UID);
        }
    }
}
