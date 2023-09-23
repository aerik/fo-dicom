// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;

namespace Dicom
{
    public abstract class DicomItem : IComparable<DicomItem>, IComparable
    {
        protected DicomItem(DicomTag tag)
        {
            Tag = tag;

            //DicomDictionaryEntry entry = DicomDictionary.Default[Tag];
            //if (entry != null && !entry.ValueRepresentations.Contains(ValueRepresentation))
            //	throw new DicomDataException("{0} is not a valid value representation for {1}", ValueRepresentation, Tag);
        }

        public DicomTag Tag { get; internal set; }

        //OPTIONAL stream position of element, Aerik
        public long StreamPosition { get; internal set; }

        public abstract DicomVR ValueRepresentation { get; }

        public int CompareTo(DicomItem other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            return Tag.CompareTo(other.Tag);
        }

        public int CompareTo(object obj)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            if (!(obj is DicomItem)) throw new ArgumentException("Only comparison with DICOM items is supported.", nameof(obj));
            return CompareTo(obj as DicomItem);
        }

        public override string ToString()
        {
            //if (Tag.DictionaryEntry != null) return String.Format("{0} {1} {2}", Tag, ValueRepresentation, Tag.DictionaryEntry.Name);
            //return String.Format("{0} {1} Unknown", Tag, ValueRepresentation);

            return Tag.DictionaryEntry != null
                ? $"{Tag} {ValueRepresentation} {Tag.DictionaryEntry.Name}"
                : $"{Tag} {ValueRepresentation} Unknown";
        }
        public virtual void Validate()
        { }
    }
}
