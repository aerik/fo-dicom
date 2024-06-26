﻿// Copyright (c) 2012-2017 fo-dicom contributors.
// Licensed under the Microsoft Public License (MS-PL).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Dicom.IO.Buffer
{
    public class CompositeByteBuffer : IByteBuffer
    {
        public CompositeByteBuffer(params IByteBuffer[] buffers)
        {
            Buffers = new List<IByteBuffer>(buffers);
        }

        public IList<IByteBuffer> Buffers { get; private set; }

        public bool IsMemory
        {
            get
            {
                return true;
            }
        }

        public uint Size
        {
            get
            {
                return (uint)Buffers.Sum(x => x.Size);
            }
        }

        public byte[] Data
        {
            get
            {
                byte[] data = new byte[Size];
                int offset = 0;
                foreach (IByteBuffer buffer in Buffers)
                {
                    System.Buffer.BlockCopy(buffer.Data, 0, data, offset, (int)buffer.Size);
                    offset += (int)buffer.Size;
                }
                return data;
            }
        }

        public void Close()
        {
            for (int i = 0; i < Buffers.Count; i++)
            {
                var buf = Buffers[i];
                buf.Close();
            }
        }

        public byte[] GetByteRange(int offset, int count)
        {
            if (offset < 0 || count < 0)
            {
                throw new ArgumentOutOfRangeException("Offset and count cannot be less than zero");
            }
            return GetByteRange((uint)offset, count);
        }

        public byte[] GetByteRange(uint uoffset, int count)
        {
            int pos = 0;
            for (; pos < Buffers.Count && uoffset > Buffers[pos].Size; pos++) uoffset -= Buffers[pos].Size;

            int offset = (int)uoffset;

            int offset2 = 0;
            byte[] data = new byte[count];
            for (; pos < Buffers.Count && count > 0; pos++)
            {
                int remain = Math.Min((int)Buffers[pos].Size - offset, count);

                if (Buffers[pos].IsMemory)
                {
                    try
                    {
                        System.Buffer.BlockCopy(Buffers[pos].Data, offset, data, offset2, remain);
                    }
                    catch (Exception)
                    {
                        data = Buffers[pos].Data.ToArray();
                    }

                }

                else
                {
                    byte[] temp = Buffers[pos].GetByteRange(offset, remain);
                    System.Buffer.BlockCopy(temp, 0, data, offset2, remain);
                }

                count -= remain;
                offset2 += remain;
                if (offset > 0) offset = 0;
            }

            return data;
        }
    }
}
