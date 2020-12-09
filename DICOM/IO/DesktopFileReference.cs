// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.IO
{
    using System.IO;

    /// <summary>
    /// .NET/Windows Desktop implementation of the <see cref="IFileReference"/> interface.
    /// </summary>
    public sealed class DesktopFileReference : IFileReference
    {
        private bool isTempFile;

        //checking to see if streams aren't being closed
        private System.Collections.Generic.List<ReferenceStream> streamList = new System.Collections.Generic.List<ReferenceStream>();

        /// <summary>
        /// Initializes a <see cref="DesktopFileReference"/> object.
        /// </summary>
        /// <param name="fileName">File name.</param>
        public DesktopFileReference(string fileName)
        {
            this.Name = fileName;
            this.IsTempFile = false;
        }

        /// <summary>
        /// Destructor.
        /// </summary>
        ~DesktopFileReference()
        {
            bool hasOpenStream = false;
            for(int i= 0; i<streamList.Count; i++)
            {
                var stream = streamList[i];
                if (!stream.IsDisposed)
                {
                    hasOpenStream = true;
                    stream.Dispose();
                }
            }
            streamList.Clear();
            if (hasOpenStream)
            {
                var logger = Log.LogManager.GetLogger("FileReference");
                logger.Log(Log.LogLevel.Warning, "Desktopfilereference had open stream");
            }
            if (this.IsTempFile)
            {
                TemporaryFileRemover.Delete(this);
            }
        }

        /// <summary>
        /// Gets the file name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets whether the file exist or not.
        /// </summary>
        public bool Exists
        {
            get
            {
                return File.Exists(this.Name);
            }
        }

        /// <summary>Gets and sets whether the file is temporary or not.</summary>
        /// <remarks>Temporary file will be deleted when object is <c>Disposed</c>.</remarks>
        public bool IsTempFile
        {
            get
            {
                return this.isTempFile;
            }
            set
            {
                if (value && this.Exists)
                {
                    try
                    {
                        // set temporary file attribute so that the file system
                        // will attempt to keep all of the data in memory
                        File.SetAttributes(this.Name, File.GetAttributes(this.Name) | FileAttributes.Temporary);
                    }
                    catch
                    {
                        // sometimes fails with invalid argument exception
                    }
                }

                this.isTempFile = value;
            }
        }

        /// <summary>
        /// Gets the directory reference of the file.
        /// </summary>
        public IDirectoryReference Directory
        {
            get
            {
                return new DesktopDirectoryReference(Path.GetDirectoryName(this.Name));
            }
        }

        /// <summary>
        /// Creates a new file for reading and writing. Overwrites existing file.
        /// </summary>
        /// <returns>Stream to the created file.</returns>
        public Stream Create()
        {
            var stream = new ReferenceStream(File.Create(this.Name));
            streamList.Add(stream);
            return stream;
        }

        /// <summary>
        /// Open an existing file stream for reading and writing.
        /// </summary>
        /// <returns></returns>
        public Stream Open()
        {
            var stream = new ReferenceStream(File.Open(this.Name, FileMode.Open, FileAccess.ReadWrite));
            streamList.Add(stream);
            return stream;
        }

        /// <summary>
        /// Open a file stream for reading.
        /// </summary>
        /// <returns>Stream to the opened file.</returns>
        public Stream OpenRead()
        {
            var stream = new ReferenceStream(File.OpenRead(this.Name));
            streamList.Add(stream);
            return stream;
        }

        /// <summary>
        /// Open a file stream for writing, creates the file if not existing.
        /// </summary>
        /// <returns>Stream to the opened file.</returns>
        public Stream OpenWrite()
        {
            var stream = new ReferenceStream(File.OpenWrite(this.Name));
            streamList.Add(stream);
            return stream;
        }

        /// <summary>
        /// Delete the file.
        /// </summary>
        public void Delete()
        {
            File.Delete(this.Name);
        }

        /// <summary>
        /// Moves file and updates internal reference.
        /// 
        /// Calling this method will also remove set the <see cref="IsTempFile"/> property to <c>False</c>.
        /// </summary>
        /// <param name="dstFileName">Name of the destination file.</param>
        /// <param name="overwrite">True if <paramref name="dstFileName"/> should be overwritten if it already exists, false otherwise.</param>
        public void Move(string dstFileName, bool overwrite = false)
        {
            // delete if overwriting; let File.Move thow IOException if not
            if (File.Exists(dstFileName) && overwrite) File.Delete(dstFileName);

            File.Move(this.Name, dstFileName);
            this.Name = Path.GetFullPath(dstFileName);
            this.IsTempFile = false;
        }

        /// <summary>
        /// Gets a sub-range of the bytes in the file.
        /// </summary>
        /// <param name="offset">Offset from the start position of the file.</param>
        /// <param name="count">Number of bytes to select.</param>
        /// <returns>The specified sub-range of bytes in the file.</returns>
        public byte[] GetByteRange(int offset, int count)
        {
            byte[] buffer = new byte[count];

            using (var fs = this.OpenRead())
            {
                fs.Seek(offset, SeekOrigin.Begin);
                fs.Read(buffer, 0, count);
            }

            return buffer;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToString()
        {
            return this.IsTempFile ? string.Format("{0} [TEMP]", this.Name) : this.Name;
        }

        private class ReferenceStream : Stream, System.IDisposable
        {

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls
            private Stream innerStream;
            public bool IsDisposed
            {
                get
                {
                    return disposedValue;
                }
            }

            public ReferenceStream(Stream stream)
            {
                innerStream = stream;
            }

            public override bool CanRead => innerStream.CanRead;

            public override bool CanSeek => innerStream.CanSeek;

            public override bool CanWrite => innerStream.CanWrite;

            public override long Length => innerStream.Length;

            public override long Position {
                get {
                    return innerStream.Position;
                }
                set
                {
                    innerStream.Position = value;
                }
            }

            public override void Flush()
            {
                innerStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return innerStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return innerStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                innerStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                innerStream.Write(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        try
                        {
                            innerStream.Dispose();
                        }
                        catch { }
                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                    // TODO: set large fields to null.

                    disposedValue = true;
                }
            }

            // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
            // ~ReferenceStream() {
            //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            //   Dispose(false);
            // }

            // This code added to correctly implement the disposable pattern.
            public new void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
                // TODO: uncomment the following line if the finalizer is overridden above.
                // GC.SuppressFinalize(this);
            }
            #endregion



        }
    }
}
