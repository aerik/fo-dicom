﻿// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

namespace Dicom.IO.Buffer
{
    public class RangeByteBuffer : IByteBuffer
    {
        public RangeByteBuffer(IByteBuffer buffer, uint offset, uint length)
        {
            Internal = buffer;
            Offset = offset;
            Length = length;
        }

        public IByteBuffer Internal { get; private set; }

        public uint Offset { get; private set; }

        public uint Length { get; private set; }

        public bool IsMemory
        {
            get
            {
                return Internal.IsMemory;
            }
        }

        public uint Size
        {
            get
            {
                return Length;
            }
        }

        public byte[] Data
        {
            get
            {
                if (Internal is CompositeByteBuffer)
                {
                    CompositeByteBuffer cbuf = (CompositeByteBuffer)Internal;
                    return cbuf.GetByteRange(Offset, (int)Length);
                }
                else
                {
                    return Internal.GetByteRange((int)Offset, (int)Length);
                }
            }
        }

        public void Close()
        {
            Internal.Close();
        }

        public byte[] GetByteRange(int offset, int count)
        {
            return Internal.GetByteRange((int)Offset + offset, count);
        }
    }
}
