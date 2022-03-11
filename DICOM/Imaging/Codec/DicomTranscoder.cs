// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.Imaging.Codec
{
    using System;
    using System.Collections.Generic;

    using Dicom.Imaging.Render;
    using Dicom.IO.Buffer;
    using Dicom.IO.Writer;

    /// <summary>
    /// Generic DICOM transcoder.
    /// </summary>
    public class DicomTranscoder : IDicomTranscoder
    {
        /// <summary>
        /// Initializes an instance of <see cref="DicomTranscoder"/>.
        /// </summary>
        /// <param name="inputSyntax">Input transfer syntax.</param>
        /// <param name="outputSyntax">Output transfer syntax.</param>
        /// <param name="inputCodecParams">Input codec parameters.</param>
        /// <param name="outputCodecParams">Output codec parameters.</param>
        public DicomTranscoder(
            DicomTransferSyntax inputSyntax,
            DicomTransferSyntax outputSyntax,
            DicomCodecParams inputCodecParams = null,
            DicomCodecParams outputCodecParams = null)
        {
            InputSyntax = inputSyntax;
            OutputSyntax = outputSyntax;
            InputCodecParams = inputCodecParams ?? DefaultInputCodecParams(inputSyntax);
            OutputCodecParams = outputCodecParams;
        }

        /// <summary>
        /// Gets the transfer syntax of the input codec.
        /// </summary>
        public DicomTransferSyntax InputSyntax { get; private set; }

        /// <summary>
        /// Gets the parameters associated with the input codec.
        /// </summary>
        public DicomCodecParams InputCodecParams { get; private set; }

        private IDicomCodec _inputCodec;

        private IDicomCodec InputCodec
        {
            get
            {
                if (InputSyntax.IsEncapsulated && _inputCodec == null) _inputCodec = TranscoderManager.GetCodec(InputSyntax);
                return _inputCodec;
            }
        }

        /// <summary>
        /// Gets the transfer syntax of the output codec.
        /// </summary>
        public DicomTransferSyntax OutputSyntax { get; private set; }

        /// <summary>
        /// Gets the parameters associated with the output codec.
        /// </summary>
        public DicomCodecParams OutputCodecParams { get; private set; }

        private IDicomCodec _outputCodec;

        private IDicomCodec OutputCodec
        {
            get
            {
                if (OutputSyntax.IsEncapsulated && _outputCodec == null) _outputCodec = TranscoderManager.GetCodec(OutputSyntax);
                return _outputCodec;
            }
        }

        /// <summary>
        /// Transcode a <see cref="DicomFile"/> from <see cref="IDicomTranscoder.InputSyntax"/> to <see cref="IDicomTranscoder.OutputSyntax"/>.
        /// </summary>
        /// <param name="file">DICOM file.</param>
        /// <returns>New, transcoded, DICOM file.</returns>
        public DicomFile Transcode(DicomFile file)
        {
            var f = new DicomFile();
            f.FileMetaInfo.Add(file.FileMetaInfo);
            f.FileMetaInfo.TransferSyntax = OutputSyntax;
            f.Dataset.InternalTransferSyntax = OutputSyntax;
            f.Dataset.Add(Transcode(file.Dataset));
            return f;
        }

        /// <summary>
        /// Transcode a <see cref="DicomDataset"/> from <see cref="IDicomTranscoder.InputSyntax"/> to <see cref="IDicomTranscoder.OutputSyntax"/>.
        /// </summary>
        /// <param name="dataset">DICOM dataset.</param>
        /// <returns>New, transcoded, DICOM dataset.</returns>
        public DicomDataset Transcode(DicomDataset dataset)
        {
            if (!dataset.Contains(DicomTag.PixelData))
            {
                var newDataset = dataset.Clone();
                newDataset.InternalTransferSyntax = OutputSyntax;
                newDataset.RecalculateGroupLengths(false);
                return newDataset;
            }

            if (!InputSyntax.IsEncapsulated && !OutputSyntax.IsEncapsulated)
            {
                // transcode from uncompressed to uncompressed
                var newDataset = dataset.Clone();
                newDataset.InternalTransferSyntax = OutputSyntax;

                var oldPixelData = DicomPixelData.Create(dataset, false);
                var newPixelData = DicomPixelData.Create(newDataset, true);

                for (int i = 0; i < oldPixelData.NumberOfFrames; i++)
                {
                    try
                    {
                        var frame = oldPixelData.GetFrame(i);
                        newPixelData.AddFrame(frame);
                    }
                    catch (IndexOutOfRangeException iorx)
                    {
                        newPixelData.AddFrame(new EmptyBuffer());
                    }
                }

                ProcessOverlays(dataset, newDataset);

                newDataset.RecalculateGroupLengths(false);

                return newDataset;
            }

            if (InputSyntax.IsEncapsulated && OutputSyntax.IsEncapsulated)
            {
                // transcode from compressed to compressed
                var temp = DecodeDataset(dataset);
                DicomDataset encoded = EncodeDataset(temp);
                CloseTempDataset(temp);
                return encoded;
            }

            if (InputSyntax.IsEncapsulated)
            {
                // transcode from compressed to uncompressed
                var newDataset = DecodeDataset(dataset);
                newDataset.InternalTransferSyntax = OutputSyntax;
                return newDataset;
            }

            if (OutputSyntax.IsEncapsulated)
            {
                // transcode from uncompressed to compressed
                return EncodeDataset(dataset);
            }

            throw new DicomCodecException(
                "Unable to find transcoding solution for {0} to {1}",
                InputSyntax.UID.Name,
                OutputSyntax.UID.Name);
        }
        /// <summary>
        /// Assumes in temp buffers in the pixel data are not used anywhere else
        /// Any temp buffers in the pixeldata will be closed (deleted) and the dataset cleared
        /// </summary>
        /// <param name="temp"></param>
        public static void CloseTempDataset(DicomDataset temp)
        {
            if (temp.Contains(DicomTag.PixelData))
            {
                var tempPixelData = DicomPixelData.Create(temp);
                for (int i = 0; i < tempPixelData.NumberOfFrames; i++)
                {
                    try
                    {
                        var frame = tempPixelData.GetFrame(i);
                        if (frame != null)
                        {
                            var buffers = Dicom.DicomDatasetExtensions.GetRootBuffers(frame);
                            foreach (var buf in buffers)
                            {
                                if (buf is TempFileBuffer)
                                {
                                    TempFileBuffer tempBuffer = buf as TempFileBuffer;
                                    tempBuffer.Close();//Calls TemporaryFileRemover.Delete(File);
                                }
                            }
                        }
                    }catch(Exception x) { }
                }
            }
            //do NOT call temp.Close here - other buffers may be shared by other datasets
            temp.Clear();
        }

        /// <summary>
        /// Decompress single frame from DICOM dataset and return uncompressed frame buffer.
        /// </summary>
        /// <param name="dataset">DICOM dataset</param>
        /// <param name="frame">Frame number</param>
        /// <returns>Uncompressed frame buffer</returns>
        public IByteBuffer DecodeFrame(DicomDataset dataset, int frame)
        {
            // is pixel data already uncompressed?
            if (!dataset.InternalTransferSyntax.IsEncapsulated)
            {
                var pixelData = DicomPixelData.Create(dataset);
                try
                {
                    var buffer = pixelData.GetFrame(frame);
                    return buffer;
                }
                catch (IndexOutOfRangeException iorx)
                {
                    return new EmptyBuffer();
                }
            }
            DicomPixelData newPixelData = DecodePixels(dataset, frame);
            if (newPixelData == null) return null;//EmptyByteBuffer?
            return newPixelData.GetFrame(0);//IndexOutOfRangeException should have already been handled in DecodePixels
        }

        public IByteBuffer EncodeFrame(DicomDataset dataset, int frame)
        {
            // is pixel data already uncompressed?
            if (dataset.InternalTransferSyntax == OutputSyntax)
            {
                var pixelData = DicomPixelData.Create(dataset);
                try
                {
                    var buffer = pixelData.GetFrame(frame);
                    return buffer;
                }
                catch (IndexOutOfRangeException iorx)
                {
                    return new EmptyBuffer();
                }
            }
            DicomPixelData newPixelData = EncodePixels(dataset, frame);
            return newPixelData.GetFrame(0);
        }

        /// <summary>
        /// Decompress pixel data from DICOM dataset and return uncompressed pixel data.
        /// </summary>
        /// <param name="dataset">DICOM dataset.</param>
        /// <param name="frame">Frame number.</param>
        /// <returns>Uncompressed pixel data.</returns>
        public IPixelData DecodePixelData(DicomDataset dataset, int frame)
        {
            // is pixel data already uncompressed?
            if (!dataset.InternalTransferSyntax.IsEncapsulated)
            {
                return PixelDataFactory.Create(DicomPixelData.Create(dataset), frame);
            }
            DicomPixelData newPixelData = DecodePixels(dataset, frame);
            return PixelDataFactory.Create(newPixelData,frame);
        }

        private DicomPixelData DecodePixels(DicomDataset dataset, int frame)
        {
            var pixelData = DicomPixelData.Create(dataset);
            IByteBuffer buffer = null;
            try
            {
                buffer = pixelData.GetFrame(frame);
            }
            catch (IndexOutOfRangeException iorx)
            {
                buffer = new EmptyBuffer();
            }
            // clone dataset to prevent changes to source
            var cloneDataset = dataset.Clone();
            var newDataset = dataset.Clone();
            newDataset.InternalTransferSyntax = OutputSyntax;
            var oldPixelData = DicomPixelData.Create(cloneDataset, true);
            pixelData = DicomPixelData.Create(newDataset, true);
            oldPixelData.AddFrame(buffer);
            if (buffer.Size > 0) //don't try to decode empty buffer
            {
                InputCodec.Decode(oldPixelData, pixelData, InputCodecParams);
            }
            else
            {
                pixelData.AddFrame(buffer);
            }
            // temporary fix for JPEG compressed YBR images, according to enforcement above
            if ((InputSyntax == DicomTransferSyntax.JPEGProcess1
                 || InputSyntax == DicomTransferSyntax.JPEGProcess2_4) && pixelData.SamplesPerPixel == 3)
            {
                if(InputCodecParams != null && InputCodecParams is DicomJpegParams &&((DicomJpegParams)InputCodecParams).ConvertColorspaceToRGB)
                    // When converting to RGB in Dicom.Imaging.Codec.Jpeg.i, PlanarConfiguration is set to Interleaved
                    pixelData.PhotometricInterpretation = PhotometricInterpretation.Rgb;
                pixelData.PlanarConfiguration = PlanarConfiguration.Interleaved;
            }

            // temporary fix for JPEG 2000 Lossy images
            if ((InputSyntax == DicomTransferSyntax.JPEG2000Lossy
                 && pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrIct)
                || (InputSyntax == DicomTransferSyntax.JPEG2000Lossless
                    && pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrRct))
            {
                // Converted to RGB in Dicom.Imaging.Codec.Jpeg2000.cpp
                pixelData.PhotometricInterpretation = PhotometricInterpretation.Rgb;
            }

            // temporary fix for JPEG2000 compressed YBR images
            if ((InputSyntax == DicomTransferSyntax.JPEG2000Lossless
                 || InputSyntax == DicomTransferSyntax.JPEG2000Lossy)
                && (pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull
                    || pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrFull422
                    || pixelData.PhotometricInterpretation == PhotometricInterpretation.YbrPartial422))
            {
                // For JPEG2000 YBR type images in Dicom.Imaging.Codec.Jpeg2000.cpp, 
                // YBR_FULL is applied and PlanarConfiguration is set to Planar
                pixelData.PhotometricInterpretation = PhotometricInterpretation.YbrFull;
                pixelData.PlanarConfiguration = PlanarConfiguration.Planar;
            }
            pixelData.Dataset.InternalTransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
            return pixelData;
        }

        private DicomPixelData EncodePixels(DicomDataset dataset, int frame)
        {
            var pixelData = DicomPixelData.Create(dataset);
            IByteBuffer buffer = null;
            try
            {
                buffer = pixelData.GetFrame(frame);
            }
            catch (IndexOutOfRangeException iorx)
            {
                buffer = new EmptyBuffer();
            }
            // clone dataset to prevent changes to source
            var cloneDataset = dataset.Clone();
            var newDataset = dataset.Clone();
            var oldPixelData = DicomPixelData.Create(cloneDataset, true);
            var newPixelData = DicomPixelData.Create(newDataset, true);
            if (buffer.Size > 0) //only encode if not empty
            {
                oldPixelData.AddFrame(buffer);
                OutputCodec.Encode(oldPixelData, newPixelData, OutputCodecParams);
            }
            else
            {
                newPixelData.AddFrame(buffer);
            }
            newDataset.InternalTransferSyntax = OutputSyntax;
            if (OutputSyntax.IsLossy && newPixelData.NumberOfFrames > 0)
            {
                newDataset.AddOrUpdate(new DicomCodeString(DicomTag.LossyImageCompression, "01"));

                var methods = new List<string>();
                if (newDataset.Contains(DicomTag.LossyImageCompressionMethod))
                {
                    methods.AddRange(newDataset.Get<string[]>(DicomTag.LossyImageCompressionMethod));
                }

                methods.Add(OutputSyntax.LossyCompressionMethod);
                newDataset.AddOrUpdate(new DicomCodeString(DicomTag.LossyImageCompressionMethod, methods.ToArray()));

                double oldSize = oldPixelData.GetFrame(0).Size;
                double newSize = newPixelData.GetFrame(0).Size;
                var ratio = String.Format("{0:0.000}", oldSize / newSize);
                newDataset.AddOrUpdate(new DicomDecimalString(DicomTag.LossyImageCompressionRatio, ratio));
            }
            return newPixelData;
        }

        private DicomDataset DecodeDataset(DicomDataset oldDataset)
        {
            long maxSize = (long)(1024 * 1024 * 50);//50MB - use long; int32.MaxValue is only 2GB
            bool useTempFiles = false;
            var oldPixelData = DicomPixelData.Create(oldDataset, false);
            var newPixelData = DecodePixels(oldDataset, 0);
            if (newPixelData != null)
            {
                uint numberOfFrames = oldPixelData.NumberOfFrames;
                int frameSize = oldPixelData.UncompressedFrameSize;
                long allFramesSize = (long)numberOfFrames * (long)frameSize;
                if (allFramesSize > maxSize) useTempFiles = true;
                try
                {
                    for (int i = 1; i < numberOfFrames; i++)
                    {
                        IByteBuffer frame = DecodeFrame(oldDataset, i);
                        if (frame != null)
                        {
                            if (useTempFiles) frame = new TempFileBuffer(frame.Data);
                            newPixelData.AddFrame(frame);
                        }
                    }
                }
                catch (Exception x)
                {
                    throw;
                }
            }
            var newDataset = newPixelData.Dataset;
            ProcessOverlays(oldDataset, newDataset);
            newDataset.RecalculateGroupLengths(false);
            return newDataset;
        }

        private DicomDataset EncodeDataset(DicomDataset oldDataset)
        {
            long maxSize = (long)(1024 * 1024 * 50);//50MB - use long; int32.MaxValue is only 2GB
            bool useTempFiles = false;
            var oldPixelData = DicomPixelData.Create(oldDataset, false);
            uint numberOfFrames = oldPixelData.NumberOfFrames;
            DicomDataset newDataset;
            if (numberOfFrames == 0)
            {
                newDataset = oldDataset.Clone();
            }
            else
            {
                var newPixelData = EncodePixels(oldDataset, 0);
                newDataset = newPixelData.Dataset;
                uint maxFrameSize = (uint)(maxSize / numberOfFrames);
                for (int i = 1; i < numberOfFrames; i++)
                {
                    IByteBuffer frame = EncodeFrame(oldDataset, i);
                    if (frame.Size > maxFrameSize) useTempFiles = true;//if any one is larger, start using temp files
                    if (useTempFiles) frame = new TempFileBuffer(frame.Data);
                    newPixelData.AddFrame(frame);
                }
            }
            ProcessOverlays(oldDataset, newDataset);
            newDataset.InternalTransferSyntax = OutputSyntax;
            newDataset.RecalculateGroupLengths(false);
            return newDataset;
        }

        private static DicomCodecParams DefaultInputCodecParams(DicomTransferSyntax inputSyntax)
        {
            return inputSyntax == DicomTransferSyntax.JPEGProcess1 || inputSyntax == DicomTransferSyntax.JPEGProcess2_4
                       ? new DicomJpegParams { ConvertColorspaceToRGB = true }
                       : null;
        }


        private static void ProcessOverlays(DicomDataset input, DicomDataset output)
        {
            var overlays = DicomOverlayData.FromDataset(input.InternalTransferSyntax.IsEncapsulated ? output : input);

            foreach (var overlay in overlays)
            {
                var dataTag = new DicomTag(overlay.Group, DicomTag.OverlayData.Element);

                // Don't run conversion on non-embedded overlays.
                if (output.Contains(dataTag)) continue;

                // If embedded overlay, Overlay Bits Allocated should equal Bits Allocated (#110).
                var bitsAlloc = output.Get(DicomTag.BitsAllocated, (ushort)0);
                output.AddOrUpdate(new DicomTag(overlay.Group, DicomTag.OverlayBitsAllocated.Element), bitsAlloc);

                var data = overlay.Data;
                if (output.InternalTransferSyntax.IsExplicitVR) output.AddOrUpdate(new DicomOtherByte(dataTag, data));
                else output.AddOrUpdate(new DicomOtherWord(dataTag, data));
            }
        }

        public static DicomDataset ExtractOverlays(DicomDataset dataset)
        {
            if (!DicomOverlayData.HasEmbeddedOverlays(dataset)) return dataset;

            dataset = dataset.Clone();

            var input = dataset;
            bool cloned = false;
            if (input.InternalTransferSyntax.IsEncapsulated)
            {
                cloned = true;
                input = input.Clone(DicomTransferSyntax.ExplicitVRLittleEndian);
            }
            ProcessOverlays(input, dataset);
            if (cloned) DicomTranscoder.CloseTempDataset(input);
            return dataset;
        }

    }
}
