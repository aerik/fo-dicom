﻿// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using Dicom.IO.Buffer;
using System;
using System.Linq;

namespace Dicom.Imaging
{
    /// <summary>
    /// DICOM Pixel Data abstract class for reading and writing DICOM images pixel data according to the specified transfer syntax
    /// </summary>
    public abstract class DicomPixelData
    {
        /// <summary>
        /// Initialize new instance of <seealso cref="DicomPixelData"/> using passed <paramref name="dataset"/>
        /// </summary>
        /// <param name="dataset"></param>
        protected DicomPixelData(DicomDataset dataset)
        {
            Dataset = dataset;
            Syntax = dataset.InternalTransferSyntax;
        }

        /// <summary>
        /// Dicom Dataset
        /// </summary>
        public DicomDataset Dataset { get; private set; }

        /// <summary>
        /// The transfer syntax used to encode the DICOM iamge pixel data
        /// </summary>
        public DicomTransferSyntax Syntax { get; private set; }

        /// <summary>
        /// DICOM image width (columns) in pixels
        /// </summary>
        public ushort Width
        {
            get
            {
                return Dataset.Get<ushort>(DicomTag.Columns);
            }
            set
            {
                Dataset.AddOrUpdate(new DicomUnsignedShort(DicomTag.Columns, value));
            }
        }

        /// <summary>
        /// DICOM image height (rows) in pixels
        /// </summary>
        public ushort Height
        {
            get
            {
                return Dataset.Get<ushort>(DicomTag.Rows);
            }
            set
            {
                Dataset.AddOrUpdate(new DicomUnsignedShort(DicomTag.Rows, value));
            }
        }


        /// <summary>
        /// DICOM image Number of frames. This value usually equals 1 for single frame images
        /// </summary>
        public uint NumberOfFrames
        {
            get
            {
                return Dataset.Get<uint>(DicomTag.NumberOfFrames, 0, 1);
            }
            set
            {
                Dataset.AddOrUpdate(new DicomIntegerString(DicomTag.NumberOfFrames, value));
            }
        }

        /// <summary>
        /// Return new instance of <seealso cref="BitDepth"/> using dataset information
        /// </summary>
        public BitDepth BitDepth
        {
            get
            {
                return new BitDepth(
                    BitsAllocated,
                    BitsStored,
                    HighBit,
                    PixelRepresentation == PixelRepresentation.Signed);
            }
        }

        /// <summary>
        /// Number of bits allocated per pixel sample (0028,0100)
        /// </summary>
        public ushort BitsAllocated
        {
            get
            {
                return Dataset.Get<ushort>(DicomTag.BitsAllocated);
            }
            set
            {
                _BytesAllocated = -1;
                Dataset.AddOrUpdate(new DicomUnsignedShort(DicomTag.BitsAllocated, value));
            }
        }

        /// <summary>
        /// Number of bits stored per pixel sample (0028,0101)
        /// </summary>
        public ushort BitsStored
        {
            get
            {
                return Dataset.Get<ushort>(DicomTag.BitsStored);
            }
            set
            {
                Dataset.AddOrUpdate(new DicomUnsignedShort(DicomTag.BitsStored, value));
            }
        }

        /// <summary>
        /// Index of the most signficant bit (MSB) of pixel sample(0028,0102)
        /// </summary>
        public ushort HighBit
        {
            get
            {
                return Dataset.Get<ushort>(DicomTag.HighBit);
            }
            set
            {
                Dataset.AddOrUpdate(new DicomUnsignedShort(DicomTag.HighBit, value));
            }
        }

        /// <summary>
        /// Number of samples per pixel (0028,0002), usually 1 for grayscale and 3 for color (RGB and YBR)
        /// </summary> 
        public ushort SamplesPerPixel
        {
            get
            {
                return Dataset.Get<ushort>(DicomTag.SamplesPerPixel, 0, 1);
            }
            set
            {
                Dataset.AddOrUpdate(new DicomUnsignedShort(DicomTag.SamplesPerPixel, value));
            }
        }

        /// <summary>
        /// Pixel Representation (0028,0103) represents signed/unsigned data of the pixel samples 
        /// </summary>
        public PixelRepresentation PixelRepresentation
        {
            get
            {
                return Dataset.Get<PixelRepresentation>(DicomTag.PixelRepresentation);
            }
            set
            {
                Dataset.AddOrUpdate(new DicomUnsignedShort(DicomTag.PixelRepresentation, (ushort)value));
            }
        }

        /// <summary>
        /// Planar Configuration (0028,0006) indicates whether the color pixel data are sent color-by-plane
        /// or color-by-pixel
        /// </summary>
        public PlanarConfiguration PlanarConfiguration
        {
            get
            {
                return Dataset.Get(DicomTag.PlanarConfiguration, PlanarConfiguration.Interleaved);
            }
            set
            {
                Dataset.AddOrUpdate(new DicomUnsignedShort(DicomTag.PlanarConfiguration, (ushort)value));
            }
        }

        /// <summary>
        /// Photometric Interpretation
        /// </summary>
        public PhotometricInterpretation PhotometricInterpretation
        {
            get
            {
                return Dataset.Get<PhotometricInterpretation>(DicomTag.PhotometricInterpretation);
            }
            set
            {
                Dataset.AddOrUpdate(new DicomCodeString(DicomTag.PhotometricInterpretation, value.Value));
            }
        }

        /// <summary>
        /// Lossy image compression (0028,2110), returns true if stored value is "01"
        /// </summary>
        public bool IsLossy
        {
            get
            {
                return Dataset.Get<string>(DicomTag.LossyImageCompression, "00") == "01";
            }
        }

        /// <summary>
        /// Lossy image compression method (0028,2114)
        /// </summary>
        public string LossyCompressionMethod
        {
            get
            {
                return Dataset.Get<string>(DicomTag.LossyImageCompressionMethod);
            }
        }

        /// <summary>
        /// Lossy image compression ration (0028,2112)
        /// </summary>
        public decimal LossyCompressionRatio
        {
            get
            {
                return Dataset.Get<decimal>(DicomTag.LossyImageCompressionRatio);
            }
        }

        private int _BytesAllocated = -1;
        /// <summary>
        /// Number of bytes allocated per pixel sample
        /// </summary>
        public int BytesAllocated
        {
            get
            {
                if (_BytesAllocated < 0)
                {
                    int bytes = BitsAllocated / 8;
                    if ((BitsAllocated % 8) > 0) bytes++;
                    _BytesAllocated = bytes;
                }
                return _BytesAllocated;
            }
        }

        /// <summary>
        /// Uncompressed frame size in bytes
        /// </summary>
        public int UncompressedFrameSize
        {
            get
            {
                if (BitsAllocated == 1)
                {
                    var bytes = (Width * Height) / 8;
                    if (((Width * Height) % 8) > 0) bytes++;
                    return bytes;
                }
                return BytesAllocated * SamplesPerPixel * Width * Height;
            }
        }

        /// <summary>
        /// Plaette color LUT, valid for PALETTE COLOR <seealso cref="PhotometricInterpretation"/>
        /// </summary>
        public Color32[] PaletteColorLUT
        {
            get
            {
                return GetPaletteColorLUT();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Extracts the palette color LUT from DICOM dataset, valid for PALETTE COLOR <seealso cref="PhotometricInterpretation"/>
        /// </summary>
        /// <returns>Palette color LUT</returns>
        /// <exception cref="DicomImagingException">Invalid photometric interpretation or plaette color lUT missing from database</exception>
        private Color32[] GetPaletteColorLUT()
        {
            if (PhotometricInterpretation != PhotometricInterpretation.PaletteColor)
                throw new DicomImagingException(
                    "Attempted to get Palette Color LUT from image with invalid photometric interpretation.");

            if (!Dataset.Contains(DicomTag.RedPaletteColorLookupTableDescriptor)) throw new DicomImagingException("Palette Color LUT missing from dataset.");

            int size = Dataset.Get<int>(DicomTag.RedPaletteColorLookupTableDescriptor, 0);
            int first = Dataset.Get<int>(DicomTag.RedPaletteColorLookupTableDescriptor, 1);
            int bits = Dataset.Get<int>(DicomTag.RedPaletteColorLookupTableDescriptor, 2);

            var r = Dataset.Get<byte[]>(DicomTag.RedPaletteColorLookupTableData);
            var g = Dataset.Get<byte[]>(DicomTag.GreenPaletteColorLookupTableData);
            var b = Dataset.Get<byte[]>(DicomTag.BluePaletteColorLookupTableData);

            // If the LUT size is 0, that means it's 65536 in size.
            if (size == 0) size = 65536;

            var lut = new Color32[size];

            if (r.Length == size)
            {
                // 8-bit LUT entries
                for (int i = 0; i < size; i++) lut[i] = new Color32(0xff, r[i], g[i], b[i]);
            }
            else
            {
                // 16-bit LUT entries... we only support 8-bit until someone can find a sample image with a 16-bit palette

                // 8-bit entries with 16-bits allocated
                int offset = 0;

                // 16-bit entries with 8-bits stored
                if (bits == 16) offset = 1;

                for (int i = 0; i < size; i++, offset += 2) lut[i] = new Color32(0xff, r[offset], g[offset], b[offset]);
            }

            return lut;
        }

        /// <summary>
        /// Abstract GetFrame method to extract specific frame byte buffer <paramref name="frame"/> dataset
        /// </summary>
        /// <param name="frame">Frame index</param>
        /// <returns>Frame byte buffer</returns>
        public abstract IByteBuffer GetFrame(int frame);

        /// <summary>
        /// Abstract AddFrame method to add new frame into dataset pixel dataset. new frame will be appended to existing frames
        /// </summary>
        /// <param name="data">Frame byte buffer</param>
        public abstract void AddFrame(IByteBuffer data);

        /// <summary>
        /// A factory method to initialize new instance of <seealso cref="DicomPixelData"/> implementation either 
        /// <seealso cref="OtherWordPixelData"/>, <seealso cref="OtherBytePixelData"/>, or <seealso cref="EncapsulatedPixelData"/>
        /// </summary>
        /// <param name="dataset">Source DICOM Dataset</param>
        /// <param name="newPixelData">true if new <seealso cref="DicomPixelData"/>will be created for current dataset,
        /// false to read <seealso cref="DicomPixelData"/> from <paramref name="dataset"/>.
        /// Default is false (read)</param>
        /// <returns>New instance of DicomPixelData</returns>
        public static DicomPixelData Create(DicomDataset dataset, bool newPixelData = false)
        {
            var syntax = dataset.InternalTransferSyntax;

            if (newPixelData)
            {
                if (syntax == DicomTransferSyntax.ImplicitVRLittleEndian) return new OtherWordPixelData(dataset, true);

                if (syntax.IsEncapsulated) return new EncapsulatedPixelData(dataset, true);

                if (dataset.Get<ushort>(DicomTag.BitsAllocated) == 16) return new OtherWordPixelData(dataset, true);
                else return new OtherBytePixelData(dataset, true);
            }
            else
            {
                var item = dataset.Get<DicomItem>(DicomTag.PixelData);
                if (item == null) throw new DicomImagingException("DICOM dataset is missing pixel data element.");

                if (!syntax.IsEncapsulated)
                {
                    if (item is DicomOtherByte) return new OtherBytePixelData(dataset, false);
                    if (item is DicomOtherWord) return new OtherWordPixelData(dataset, false);
                }
                else
                {
                    if (item is DicomOtherByteFragment || item is DicomOtherWordFragment) return new EncapsulatedPixelData(dataset, false);
                }

                throw new DicomImagingException("Unexpected or unhandled pixel data element type for syntax {0}: {1}", syntax.UID.Name, item.GetType());
            }
        }

        public static bool FixBrokenCompression(DicomDataset dataset)
        {
            if (dataset.InternalTransferSyntax.IsEncapsulated && dataset.Contains(DicomTag.PixelData))
            {
                uint numFrames = dataset.Get<uint>(DicomTag.NumberOfFrames, 0, 1);
                var item = dataset.Get<DicomItem>(DicomTag.PixelData);
                IByteBuffer buf = null;
                if (item is DicomOtherByte)
                {
                    buf = ((DicomOtherByte)item).Buffer;
                }
                if (item is DicomOtherWord)
                {
                    buf = ((DicomOtherWord)item).Buffer;
                }
                if (buf != null && buf.Size > 0)
                {
                    if (numFrames > 1)
                    {
                        string sop = dataset.Get<string>(DicomTag.SOPInstanceUID, 0, "unknown");
                        Log.LogManager.GetLogger("DicomPixelData").Error("Broken pixel data compression detected in " + sop);
                        return false; //can't fix
                    }
                    DicomPixelData dpd = DicomPixelData.Create(dataset, true);
                    dpd.AddFrame(buf);
                }
            }
            return true;
        }

        /// <summary>
        /// Other Byte (OB) implementation of <seealso cref="DicomPixelData"/>
        /// </summary>
        private class OtherBytePixelData : DicomPixelData
        {
            /// <summary>
            /// Initialize new instance of OtherBytePixelData
            /// </summary>
            /// <param name="dataset">The source dataset to extract from or create new pixel data for</param>
            /// <param name="newPixelData">True to create new pixel data, false to read pixel data</param>
            public OtherBytePixelData(DicomDataset dataset, bool newPixelData)
                : base(dataset)
            {
                if (newPixelData)
                {
                    NumberOfFrames = 0;
                    Element = new DicomOtherByte(DicomTag.PixelData, new CompositeByteBuffer());
                    Dataset.AddOrUpdate(Element);
                }
                else Element = dataset.Get<DicomOtherByte>(DicomTag.PixelData);
            }

            /// <summary>
            /// The pixel data other byte (OB) element
            /// </summary>
            public DicomOtherByte Element { get; private set; }

            public override IByteBuffer GetFrame(int frame)
            {
                if (frame == -1) return Element.Buffer;
                if (frame < 0 || frame >= NumberOfFrames) throw new IndexOutOfRangeException("Requested frame out of range!");

                int offset = UncompressedFrameSize * frame;
                return new RangeByteBuffer(Element.Buffer, (uint)offset, (uint)UncompressedFrameSize);
            }

            public override void AddFrame(IByteBuffer data)
            {
                if (!(Element.Buffer is CompositeByteBuffer))
                {
                    string strType = Element.Buffer == null ? "(null)" : Element.Buffer.GetType().Name;
                    throw new DicomImagingException("Expected pixel data element to have a CompositeByteBuffer, found " + strType);
                }

                CompositeByteBuffer buffer = Element.Buffer as CompositeByteBuffer;
                buffer.Buffers.Add(data);

                NumberOfFrames++;
            }
        }

        /// <summary>
        /// Other Word (OW) implementation of <seealso cref="DicomPixelData"/>
        /// </summary>
        private class OtherWordPixelData : DicomPixelData
        {
            /// <summary>
            /// Initialize new instance of OtherWordPixelData
            /// </summary>
            /// <param name="dataset">The source dataset to extract from or create new pixel data for</param>
            /// <param name="newPixelData">True to create new pixel data, false to read pixel data</param>
            public OtherWordPixelData(DicomDataset dataset, bool newPixelData)
                : base(dataset)
            {
                if (newPixelData)
                {
                    NumberOfFrames = 0;
                    Element = new DicomOtherWord(DicomTag.PixelData, new CompositeByteBuffer());
                    Dataset.AddOrUpdate(Element);
                }
                else Element = dataset.Get<DicomOtherWord>(DicomTag.PixelData);
            }

            /// <summary>
            /// The pixel data other word (OW) element
            /// </summary>
            public DicomOtherWord Element { get; private set; }

            public override IByteBuffer GetFrame(int frame)
            {
                if (frame == -1) return Element.Buffer;
                if (frame < 0 || frame >= NumberOfFrames) throw new IndexOutOfRangeException("Requested frame out of range!");

                int offset = UncompressedFrameSize * frame;
                IByteBuffer buffer = new RangeByteBuffer(Element.Buffer, (uint)offset, (uint)UncompressedFrameSize);

                // mainly for GE Private Implicit VR Big Endian
                if (Syntax.SwapPixelData) buffer = new SwapByteBuffer(buffer, 2);

                return buffer;
            }

            public override void AddFrame(IByteBuffer data)
            {
                if (!(Element.Buffer is CompositeByteBuffer))
                {
                    string strType = Element.Buffer == null ? "(null)" : Element.Buffer.GetType().Name;
                    throw new DicomImagingException("Expected pixel data element to have a CompositeByteBuffer, found " + strType);
                }

                CompositeByteBuffer buffer = Element.Buffer as CompositeByteBuffer;

                if (Syntax.SwapPixelData) data = new SwapByteBuffer(data, 2);

                buffer.Buffers.Add(data);
                NumberOfFrames++;
            }
        }

        /// <summary>
        /// Other Byte Fragment implementation of <seealso cref="DicomPixelData"/>, used for handling encapsulated (compressed)
        /// pixel data
        /// </summary>
        private class EncapsulatedPixelData : DicomPixelData
        {
            /// <summary>
            /// Initialize new instance of EncapsulatedPixelData
            /// </summary>
            /// <param name="dataset">The source dataset to extract from or create new pixel data for</param>
            /// <param name="newPixelData">True to create new pixel data, false to read pixel data</param>
            public EncapsulatedPixelData(DicomDataset dataset, bool newPixelData)
                : base(dataset)
            {
                if (newPixelData)
                {
                    NumberOfFrames = 0;
                    Element = new DicomOtherByteFragment(DicomTag.PixelData);
                    Dataset.AddOrUpdate(Element);
                }
                else Element = dataset.Get<DicomFragmentSequence>(DicomTag.PixelData);
            }

            /// <summary>
            /// The pixel data framgent sequence element
            /// </summary>
            public DicomFragmentSequence Element { get; private set; }

            public override IByteBuffer GetFrame(int frame)
            {
                //special case - get the entire buffer
                if (frame == -1) return new CompositeByteBuffer(Element.Fragments.ToArray());
                //assume that if NumberOfFrames is broken, there are no MORE frames than NumberOfFrames
                if (frame < -1) throw new ArgumentOutOfRangeException("frame", frame, "Frame index cannot be negative");
                if (frame >= NumberOfFrames) throw new ArgumentOutOfRangeException("frame", frame, "Frame index must be less than NumberOfFrames: " + NumberOfFrames);

                IByteBuffer buffer = null;

                uint numFrames = NumberOfFrames;
                //see if there's an offset table, if so, we must use it
                if (Element.OffsetTable.Count > 0)
                {
                    numFrames = (uint)Element.OffsetTable.Count;
                    if (frame >= numFrames)
                    {
                        throw new IndexOutOfRangeException("Requested frame offset (" + frame + ") out of range (" + numFrames + ") !");
                    }
                    else
                    {
                        //if everything agrees there is one frame, keep it simple
                        if (frame == 0 && numFrames == 1 && Element.Fragments.Count == 1)
                        {
                            buffer = buffer = Element.Fragments[0];
                        }
                        else if (Element.Fragments.Count == numFrames)//not sure if this is exactly correct, but it is convenient
                        {
                            buffer = Element.Fragments[frame];
                        }
                        else
                        {
                            uint start = Element.OffsetTable[frame];
                            uint stop = (Element.OffsetTable.Count == (frame + 1))
                                            ? uint.MaxValue
                                            : Element.OffsetTable[frame + 1];

                            var composite = new CompositeByteBuffer();

                            uint pos = 0;
                            int frag = 0;

                            while (pos < start && frag < Element.Fragments.Count)
                            {
                                pos += 8;
                                pos += Element.Fragments[frag].Size;
                                frag++;
                            }

                            if (pos != start) throw new DicomImagingException("Fragment start position does not match offset table.");

                            while (pos < stop && frag < Element.Fragments.Count)
                            {
                                composite.Buffers.Add(Element.Fragments[frag]);

                                pos += 8;
                                pos += Element.Fragments[frag].Size;
                                frag++;
                            }

                            if (pos < stop && stop != uint.MaxValue)
                            {
                                string dets = "Frame: " + frame + "/" + numFrames + "/" + Element.Fragments.Count + " pos: " + pos + " stop: " + stop;

                                throw new DicomImagingException(
                                    "Image frame truncated while reading fragments from offset table.", new Exception(dets));
                            }
                            buffer = composite;
                        }
                    }
                }
                else
                {
                    //no offset table
                    //not sure why this is necessary... mulit-fragment image with no offset table?
                    if (NumberOfFrames == 1 && frame == 0)
                    {
                        if (Element.Fragments.Count == 1) buffer = Element.Fragments[0];
                        else buffer = new CompositeByteBuffer(Element.Fragments.ToArray());
                    }
                    else
                    {
                        numFrames = (uint)Element.Fragments.Count;
                        if (frame >= numFrames)
                        {
                            throw new IndexOutOfRangeException("Requested frame (" + frame + ") out of range (" + numFrames + ") !");
                        }
                        else
                        {
                            buffer = Element.Fragments[frame];//fix for mangled NumberOfFrames
                        }
                    }
                }

                if (buffer == null) buffer = new EmptyBuffer();//could throw instead...?

                // mainly for GE Private Implicit VR Little Endian
                if (!Syntax.IsEncapsulated && BitsAllocated == 16 && Syntax.SwapPixelData) buffer = new SwapByteBuffer(buffer, 2);

                return EndianByteBuffer.Create(buffer, Syntax.Endian, BytesAllocated);
            }

            public override void AddFrame(IByteBuffer data)
            {
                NumberOfFrames++;

                long pos = Element.Fragments.Sum(x => (long)x.Size + 8);
                if (pos < uint.MaxValue)
                {
                    Element.OffsetTable.Add((uint)pos);
                }
                else
                {
                    // do not create an offset table for very large datasets
                    Element.OffsetTable.Clear();
                }

                data = EndianByteBuffer.Create(data, Syntax.Endian, BytesAllocated);
                Element.Fragments.Add(data);
            }
        }
    }
}
