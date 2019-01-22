using Aris.FileTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace extract_frame_headers
{
    public sealed class ArisFile : IDisposable
    {
        private readonly Stream stream;
        private readonly int frameCount;

        private bool disposed;

        public static (bool, ArisFile) Create(string path)
        {
            try
            {
                var stream = File.OpenRead(path);
                if (CheckFile(stream))
                {
                    return (true, new ArisFile(stream));
                }
                else
                {
                    return (false, null);
                }
            }
            catch (IOException)
            {
                return (false, null);
            }
        }

        private ArisFile(Stream stream)
        {
            this.stream = stream;
            frameCount = CountFrames(stream);
        }

        ~ArisFile()
        {
            Dispose(false);
        }

        public int FrameCount { get { return frameCount; } }

        public bool ReadFileHeader(out ArisFileHeader header) =>
            ReadFileHeader(stream, out header);

        public bool ReadFrameHeaderByIndex(int index, ref ArisFrameHeader header) =>
            ReadFrameHeaderByIndex(stream, index, out header);


        private static bool CheckFile(Stream stream)
        {
            var minLength = Marshal.SizeOf<ArisFileHeader>() + Marshal.SizeOf<ArisFrameHeader>();

            if (stream.Length < minLength)
            {
                Console.Error.WriteLine("File is too small to be a valid ARIS file.");
                return false;
            }

            ArisFileHeader fileHeader;

            if (!ReadFileHeader(stream, out fileHeader))
            {
            }

            throw new Exception();
        }

        private static int CountFrames(Stream stream)
        {
            throw new Exception();
        }

        private static bool ReadFileHeader(Stream stream, out ArisFileHeader header) =>
            ReadAt<ArisFileHeader>(0L, out header);

        private static bool ReadFrameHeaderByIndex(Stream stream, int index, out ArisFrameHeader header) =>
            throw new Exception();

        private static bool ReadAt<T>(long position, out T t)
        {
            throw new Exception();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException("ArisFile");
                }

                disposed = true;

                // Dispose managed resources
                stream.Dispose();
            }

            // Dispose non-managed resources
        }
    }
}
