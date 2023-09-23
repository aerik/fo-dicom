// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;

namespace Dicom
{
    public class DicomDataException : DicomException
    {
        public DicomDataException(string message)
            : base(message)
        {
        }

        public DicomDataException(string format, params object[] args)
            : base(format, args)
        {
        }

        public DicomDataException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class DicomValidationException : DicomDataException
    {

        /// <summary>The string-content that does not validate.</summary>
        public string Content { get; private set; }

        /// <summary>The value representation that validates.</summary>
        public DicomVR VR { get; private set; }


        public DicomValidationException(string content, DicomVR vr, string message)
           : base(message)
        {
            Content = content;
            VR = vr;
        }


        public override string Message => $"Content \"{Content}\" does not validate VR {VR.Code}: {base.Message}";

    }
}
