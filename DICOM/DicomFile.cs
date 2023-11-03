// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom
{
    using System;
    using System.IO;
    using System.Text;

#if !NET35
    using System.Threading.Tasks;
#endif

    using Dicom.IO;
    using Dicom.IO.Reader;
    using Dicom.IO.Writer;
    using System.Collections.Generic;

    /// <summary>
    /// Container class for DICOM file parsing states.
    /// </summary>
    public sealed class ParseState
    {
        #region PROPERTIES

        /// <summary>
        /// Gets or sets the DICOM tag associated with the parse state.
        /// </summary>
        public DicomTag Tag { get; set; }

        /// <summary>
        /// Gets or sets the sequence depth (zero-based) associated with the parse state.
        /// </summary>
        public int SequenceDepth { get; set; }

        #endregion
    }

    /// <summary>
    /// Representation of one DICOM file.
    /// </summary>
    public class DicomFile : IDisposable
    {
        #region CONSTRUCTORS
        //copied from DicomFileReader
        private static readonly DicomTag FileMetaInfoStopTag = new DicomTag(0x0002, 0xffff);

        public DicomFile()
        {
            FileMetaInfo = new DicomFileMetaInformation();
            Dataset = new DicomDataset();
            Format = DicomFileFormat.DICOM3;
            IsPartial = false;
        }

        public DicomFile(DicomDataset dataset)
        {
            Dataset = dataset;
            FileMetaInfo = new DicomFileMetaInformation(Dataset);
            Format = DicomFileFormat.DICOM3;
            IsPartial = false;
        }

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the file reference of the DICOM file.
        /// </summary>
        public IFileReference File { get; protected set; }

        /// <summary>
        /// Gets the DICOM file format.
        /// </summary>
        public DicomFileFormat Format { get; protected set; }

        /// <summary>
        /// Gets the DICOM file meta information of the file.
        /// </summary>
        public DicomFileMetaInformation FileMetaInfo { get; protected set; }

        /// <summary>
        /// Gets the DICOM dataset of the file.
        /// </summary>
        public DicomDataset Dataset { get; protected set; }

        /// <summary>
        /// Gets whether the parsing of the file ended prematurely.
        /// </summary>
        public bool IsPartial { get; protected set; }

        #endregion

        #region METHODS

        /// <summary>
        /// Save DICOM file.
        /// </summary>
        /// <param name="fileName">File name.</param>
        /// <param name="options">Options to apply during writing.</param>
        public void Save(string fileName, DicomWriteOptions options = null)
        {
            this.PreprocessFileMetaInformation();

            this.File = IOManager.CreateFileReference(fileName);
            this.File.Delete();

            this.OnSave();

            using (var target = new FileByteTarget(this.File))
            {
                var writer = new DicomFileWriter(options);
                writer.Write(target, this.FileMetaInfo, this.Dataset);
            }
        }

        /// <summary>
        /// Save DICOM file to stream.
        /// </summary>
        /// <param name="stream">Stream on which to save DICOM file.</param>
        /// <param name="options">Options to apply during writing.</param>
        public void Save(Stream stream, DicomWriteOptions options = null)
        {
            this.PreprocessFileMetaInformation();
            this.OnSave();

            var target = new StreamByteTarget(stream);
            var writer = new DicomFileWriter(options);
            writer.Write(target, this.FileMetaInfo, this.Dataset);
        }

#if !NET35
        /// <summary>
        /// Save to file asynchronously.
        /// </summary>
        /// <param name="fileName">Name of file.</param>
        /// <param name="options">Options to apply during writing.</param>
        /// <returns>Awaitable <see cref="Task"/>.</returns>
        public async Task SaveAsync(string fileName, DicomWriteOptions options = null)
        {
            this.PreprocessFileMetaInformation();

            this.File = IOManager.CreateFileReference(fileName);
            this.File.Delete();

            this.OnSave();

            using (var target = new FileByteTarget(this.File))
            {
                var writer = new DicomFileWriter(options);
                await writer.WriteAsync(target, this.FileMetaInfo, this.Dataset).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously save DICOM file to stream.
        /// </summary>
        /// <param name="stream">Stream on which to save DICOM file.</param>
        /// <param name="options">Options to apply during writing.</param>
        /// <returns>Awaitable task.</returns>
        public async Task SaveAsync(Stream stream, DicomWriteOptions options = null)
        {
            this.PreprocessFileMetaInformation();
            this.OnSave();

            var target = new StreamByteTarget(stream);
            var writer = new DicomFileWriter(options);
            await writer.WriteAsync(target, this.FileMetaInfo, this.Dataset).ConfigureAwait(false);
        }
#endif

        /// <summary>
        /// Reads the specified filename and returns a DicomFile object.  Note that the values for large
        /// DICOM elements (e.g. PixelData) are read in "on demand" to conserve memory.  Large DICOM elements
        /// are determined by their size in bytes - see the default value for this in the FileByteSource._largeObjectSize
        /// </summary>
        /// <param name="fileName">The filename of the DICOM file</param>
        /// <returns>DicomFile instance</returns>
        public static DicomFile Open(string fileName)
        {
            return Open(fileName, DicomEncoding.Default);
        }

        /// <summary>
        /// Reads the specified filename and returns a DicomFile object.  Note that the values for large
        /// DICOM elements (e.g. PixelData) are read in "on demand" to conserve memory.  Large DICOM elements
        /// are determined by their size in bytes - see the default value for this in the FileByteSource._largeObjectSize
        /// </summary>
        /// <param name="fileName">The filename of the DICOM file</param>
        /// <param name="fallbackEncoding">Encoding to apply when attribute Specific Character Set is not available.</param>
        /// <param name="stop">Stop criterion in dataset.</param>
        /// <returns>DicomFile instance</returns>
        public static DicomFile Open(string fileName, Encoding fallbackEncoding, Func<ParseState, bool> stop = null)
        {
            if (fallbackEncoding == null)
            {
                throw new ArgumentNullException("fallbackEncoding");
            }
            DicomFile df = new DicomFile();

            try
            {
                df.File = IOManager.CreateFileReference(fileName);

                using (var source = new FileByteSource(df.File))
                {
                    var reader = new DicomFileReader();
                    var result = reader.Read(
                        source,
                        new DicomDatasetReaderObserver(df.FileMetaInfo),
                        new DicomDatasetReaderObserver(df.Dataset, fallbackEncoding),
                        stop);

                    if (result == DicomReaderResult.Processing)
                    {
                        throw new DicomFileException(df, "Invalid read return state: {0}", result);
                    }
                    if (result == DicomReaderResult.Error)
                    {
                        return null;
                    }
                    df.IsPartial = result == DicomReaderResult.Stopped || result == DicomReaderResult.Suspended;

                    df.Format = reader.FileFormat;

                    df.Dataset.InternalTransferSyntax = reader.Syntax;

                    df.PreprocessFileMetaInformation();
                    return df;
                }
            }
            catch (Exception e)
            {
                throw new DicomFileException(df, e.Message, e);
            }
        }

        /// <summary>
        /// Read a DICOM file from stream.
        /// </summary>
        /// <param name="stream">Stream to read.</param>
        /// <returns>Read <see cref="DicomFile"/>.</returns>
        public static DicomFile Open(Stream stream)
        {
            return Open(stream, DicomEncoding.Default);
        }

        /// <summary>
        /// Read a DICOM file from stream.
        /// </summary>
        /// <param name="stream">Stream to read.</param>
        /// <param name="fallbackEncoding">Encoding to use if encoding cannot be obtained from DICOM file.</param>
        /// <param name="stop">Stop criterion in dataset.</param>
        /// <returns>Read <see cref="DicomFile"/>.</returns>
        public static DicomFile Open(Stream stream, Encoding fallbackEncoding, Func<ParseState, bool> stop = null)
        {
            if (fallbackEncoding == null)
            {
                throw new ArgumentNullException("fallbackEncoding");
            }
            var df = new DicomFile();

            try
            {
                var source = new StreamByteSource(stream);

                var reader = new DicomFileReader();
                var result = reader.Read(
                    source,
                    new DicomDatasetReaderObserver(df.FileMetaInfo),
                    new DicomDatasetReaderObserver(df.Dataset, fallbackEncoding),
                    stop);

                if (result == DicomReaderResult.Processing)
                {
                    throw new DicomFileException(df, "Invalid read return state: {0}", result);
                }
                if (result == DicomReaderResult.Error)
                {
                    return null;
                }
                df.IsPartial = result == DicomReaderResult.Stopped || result == DicomReaderResult.Suspended;

                df.Format = reader.FileFormat;

                df.Dataset.InternalTransferSyntax = reader.Syntax;

                df.PreprocessFileMetaInformation();
                return df;
            }
            catch (Exception e)
            {
                throw new DicomFileException(df, e.Message, e);
            }
        }

#if !NET35
        /// <summary>
        /// Asynchronously reads the specified filename and returns a DicomFile object.  Note that the values for large
        /// DICOM elements (e.g. PixelData) are read in "on demand" to conserve memory.  Large DICOM elements
        /// are determined by their size in bytes - see the default value for this in the FileByteSource._largeObjectSize
        /// </summary>
        /// <param name="fileName">The filename of the DICOM file</param>
        /// <returns>Awaitable <see cref="DicomFile"/> instance.</returns>
        public static Task<DicomFile> OpenAsync(string fileName)
        {
            return OpenAsync(fileName, DicomEncoding.Default);
        }

        /// <summary>
        /// Asynchronously reads the specified filename and returns a DicomFile object.  Note that the values for large
        /// DICOM elements (e.g. PixelData) are read in "on demand" to conserve memory.  Large DICOM elements
        /// are determined by their size in bytes - see the default value for this in the FileByteSource._largeObjectSize
        /// </summary>
        /// <param name="fileName">The filename of the DICOM file</param>
        /// <param name="fallbackEncoding">Encoding to apply when attribute Specific Character Set is not available.</param>
        /// <param name="stop">Stop criterion in dataset.</param>
        /// <returns>Awaitable <see cref="DicomFile"/> instance.</returns>
        public static async Task<DicomFile> OpenAsync(string fileName, Encoding fallbackEncoding, Func<ParseState, bool> stop = null)
        {
            if (fallbackEncoding == null)
            {
                throw new ArgumentNullException("fallbackEncoding");
            }
            var df = new DicomFile();

            try
            {
                df.File = IOManager.CreateFileReference(fileName);

                using (var source = new FileByteSource(df.File))
                {
                    var reader = new DicomFileReader();
                    var result =
                        await
                        reader.ReadAsync(
                            source,
                            new DicomDatasetReaderObserver(df.FileMetaInfo),
                            new DicomDatasetReaderObserver(df.Dataset, fallbackEncoding),
                            stop).ConfigureAwait(false);

                    if (result == DicomReaderResult.Processing)
                    {
                        throw new DicomFileException(df, "Invalid read return state: {0}", result);
                    }
                    if (result == DicomReaderResult.Error)
                    {
                        return null;
                    }
                    df.IsPartial = result == DicomReaderResult.Stopped || result == DicomReaderResult.Suspended;

                    df.Format = reader.FileFormat;
                    df.Dataset.InternalTransferSyntax = reader.Syntax;

                    df.PreprocessFileMetaInformation();
                    return df;
                }
            }
            catch (Exception e)
            {
                throw new DicomFileException(df, e.Message, e);
            }
        }

        /// <summary>
        /// Asynchronously read a DICOM file from stream.
        /// </summary>
        /// <param name="stream">Stream to read.</param>
        /// <returns>Awaitable <see cref="DicomFile"/> instance.</returns>
        public static Task<DicomFile> OpenAsync(Stream stream)
        {
            return OpenAsync(stream, DicomEncoding.Default);
        }

        /// <summary>
        /// Asynchronously read a DICOM file from stream.
        /// </summary>
        /// <param name="stream">Stream to read.</param>
        /// <param name="fallbackEncoding">Encoding to use if encoding cannot be obtained from DICOM file.</param>
        /// <param name="stop">Stop criterion in dataset.</param>
        /// <returns>Awaitable <see cref="DicomFile"/> instance.</returns>
        public static async Task<DicomFile> OpenAsync(Stream stream, Encoding fallbackEncoding, Func<ParseState, bool> stop = null)
        {
            if (fallbackEncoding == null)
            {
                throw new ArgumentNullException("fallbackEncoding");
            }
            var df = new DicomFile();

            try
            {
                var source = new StreamByteSource(stream);

                var reader = new DicomFileReader();
                var result =
                    await
                    reader.ReadAsync(
                        source,
                        new DicomDatasetReaderObserver(df.FileMetaInfo),
                        new DicomDatasetReaderObserver(df.Dataset, fallbackEncoding),
                        stop).ConfigureAwait(false);

                if (result == DicomReaderResult.Processing)
                {
                    throw new DicomFileException(df, "Invalid read return state: {0}", result);
                }
                if (result == DicomReaderResult.Error)
                {
                    return null;
                }
                df.IsPartial = result == DicomReaderResult.Stopped || result == DicomReaderResult.Suspended;

                df.Format = reader.FileFormat;
                df.Dataset.InternalTransferSyntax = reader.Syntax;

                df.PreprocessFileMetaInformation();
                return df;
            }
            catch (Exception e)
            {
                throw new DicomFileException(df, e.Message, e);
            }
        }
#endif

        /// <summary>
        /// Test if file has a valid preamble and DICOM 3.0 header.
        /// </summary>
        /// <param name="path">Path to file</param>
        /// <returns>True if valid DICOM 3.0 file header is detected.</returns>
        public static bool HasValidHeader(string path)
        {
            try
            {
                var file = IOManager.CreateFileReference(path);
                using (var fs = file.OpenRead())
                {
                    fs.Seek(128, SeekOrigin.Begin);
                    return fs.ReadByte() == 'D' && fs.ReadByte() == 'I' && fs.ReadByte() == 'C' && fs.ReadByte() == 'M';
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToString()
        {
            return string.Format("DICOM File [{0}]", this.Format);
        }

        /// <summary>
        /// Reads the specified file and returns a DicomFile object.  Note that the values for large
        /// DICOM elements (e.g. PixelData) are read in "on demand" to conserve memory.  Large DICOM elements
        /// are determined by their size in bytes - see the default value for this in the FileByteSource._largeObjectSize
        /// </summary>
        /// <param name="file">The file reference of the DICOM file</param>
        /// <param name="fallbackEncoding">Encoding to apply when attribute Specific Character Set is not available.</param>
        /// <returns>DicomFile instance</returns>
        internal static DicomFile Open(IFileReference file, Encoding fallbackEncoding)
        {
            if (fallbackEncoding == null)
            {
                throw new ArgumentNullException("fallbackEncoding");
            }
            DicomFile df = new DicomFile();

            try
            {
                df.File = file;

                using (var source = new FileByteSource(file))
                {
                    DicomFileReader reader = new DicomFileReader();
                    var result = reader.Read(
                        source,
                        new DicomDatasetReaderObserver(df.FileMetaInfo),
                        new DicomDatasetReaderObserver(df.Dataset, fallbackEncoding));

                    if (result == DicomReaderResult.Processing)
                    {
                        throw new DicomFileException(df, "Invalid read return state: {0}", result);
                    }
                    if (result == DicomReaderResult.Error)
                    {
                        return null;
                    }
                    df.IsPartial = result == DicomReaderResult.Stopped || result == DicomReaderResult.Suspended;

                    df.Format = reader.FileFormat;

                    df.Dataset.InternalTransferSyntax = reader.Syntax;

                    df.PreprocessFileMetaInformation();
                    return df;
                }
            }
            catch (Exception e)
            {
                throw new DicomFileException(df, e.Message, e);
            }
        }

        /// <summary>
        /// Method to call before performing the actual saving.
        /// </summary>
        /// <exception cref="DicomFileException">If file dataset does not contain at least a few required tags</exception>
        protected virtual void OnSave()
        {
            //perform super minimal validation
            var test1 = FileMetaInfo.ImplementationClassUID;
            var test2 = FileMetaInfo.InternalTransferSyntax;
            var test3 = FileMetaInfo.MediaStorageSOPInstanceUID;

            if (!this.Dataset.Contains(DicomTag.SOPInstanceUID))
            {
                throw new DicomFileException(this, "Unable to save file with no SOPInstanceUID");
            }
            if (!this.Dataset.Contains(DicomTag.SOPClassUID))
            {
                throw new DicomFileException(this, "Unable to save file with no SOPClassUID");
            }
        }

        /// <summary>
        /// Preprocess file meta information before save.
        /// </summary>
        /// <exception cref="DicomFileException">If file format is ACR-NEMA version 2 or 3.</exception>
        private void PreprocessFileMetaInformation()
        {
            if (this.Format == DicomFileFormat.ACRNEMA1 || this.Format == DicomFileFormat.ACRNEMA2)
            {
                throw new DicomFileException(this, "Unable to save ACR-NEMA file");
            }

            // create file meta information from dataset or update existing file meta information.
            this.FileMetaInfo = this.Format == DicomFileFormat.DICOM3NoFileMetaInfo
                                    ? new DicomFileMetaInformation(this.Dataset)
                                    : new DicomFileMetaInformation(this.FileMetaInfo);

            this.FileMetaInfo.Version = new byte[] { 0x00, 0x01 };
            if (this.Dataset.Contains(DicomTag.SOPClassUID))
            {
                this.FileMetaInfo.MediaStorageSOPClassUID = this.Dataset.Get<DicomUID>(DicomTag.SOPClassUID);
            }
            if (this.Dataset.Contains(DicomTag.SOPInstanceUID))
            {
                this.FileMetaInfo.MediaStorageSOPInstanceUID = this.Dataset.Get<DicomUID>(DicomTag.SOPInstanceUID);
            }
            //make sure there are no duplicates between Dataset and FileMetaInfo
            List<DicomTag> toDelete = new List<DicomTag>();
            foreach (DicomItem item in this.Dataset)
            {
                if (item.Tag.CompareTo(FileMetaInfoStopTag) < 0)
                {
                    //use the dataset value if it's not empty
                    if (!String.IsNullOrEmpty(this.Dataset.Get<string>(item.Tag, -1, null)))
                    {
                        this.FileMetaInfo.AddOrUpdate(item);
                    }
                    if (!toDelete.Contains(item.Tag)) toDelete.Add(item.Tag);
                }
                else
                {
                    break;
                }
            }
            foreach (DicomTag tag in toDelete)
            {
                this.Dataset.Remove(tag);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        if (File.IsTempFile)
                        {
                            TemporaryFileRemover.Delete(File);
                        }
                        Dataset.Close(true);
                    }
                    catch { }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~DicomFile() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        #endregion
    }
}
