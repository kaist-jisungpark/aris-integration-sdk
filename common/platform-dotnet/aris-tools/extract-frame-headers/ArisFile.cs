using Aris.FileTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace extract_frame_headers
{
    public sealed class ArisFile : IDisposable
    {
        private readonly Stream stream;
        private readonly int frameCount;
        private readonly int fullFrameSize;

        private bool disposed;

        public static bool Create(string path, out ArisFile arisFile)
        {
            try
            {
                var stream = File.OpenRead(path);
                if (CheckFile(stream) && CountFrames(stream, out var frameCount, out var frameSize))
                {
                    arisFile = new ArisFile(stream, frameCount, frameSize);
                    return true;
                }
                else
                {
                    arisFile = null;
                    return false;
                }
            }
            catch (IOException)
            {
                arisFile = null;
                return false;
            }
        }

        public void ExportCsv(TextWriter output)
        {
            var fields = GetFieldInfos(new HashSet<string>()).ToArray();
            ExportColumnHeaders(fields, output);

            for (int i = 0; i < int.MaxValue; ++i)
            {
                if (ReadFrameHeaderByIndex(i, out var frameHeader))
                {
                    ExportFrameHeaderFields(fields, output, frameHeader);
                }
                else
                {
                    break;
                }
            }
        }

        private void ExportColumnHeaders(FieldInfo[] fis, TextWriter output)
        {
            var buf = new StringBuilder();
            bool isFirst = true;

            foreach (var fi in fis)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    buf.Append(",");
                }

                buf.Append(fi.Name);
            }

            output.WriteLine(buf.ToString());
        }

        private void ExportFrameHeaderFields(FieldInfo[] fis, TextWriter output, object boxedHeader)
        {
            var buf = new StringBuilder();
            bool isFirst = true;

            foreach (var fi in fis)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    buf.Append(",");
                }

                buf.Append(fi.GetValue(boxedHeader).ToString());
            }

            output.WriteLine(buf.ToString());
        }

        private ArisFile(Stream stream, int frameCount, int frameSize)
        {
            this.stream = stream;
            this.frameCount = frameCount;
            this.fullFrameSize = frameSize;
        }

        ~ArisFile()
        {
            Dispose(false);
        }

        public int FrameCount { get { return frameCount; } }

        private bool ReadFileHeader(out ArisFileHeader header) =>
            ReadFileHeader(stream, out header);

        private bool ReadFrameHeaderByIndex(int index, out ArisFrameHeader header) =>
            ReadFrameHeaderByIndex(stream, index, fullFrameSize, out header);


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
                Console.Error.WriteLine("Couldn't read the file header");
                return false;
            }

            if (fileHeader.Version != ArisFileHeader.ArisFileSignature)
            {
                Console.Error.WriteLine("File is not an ARIS file");
                return false;
            }

            return true;
        }

        private static uint PingModeToNumBeams(uint pingMode)
        {
            if (pingMode == 1)
            {
                return 48;
            }
            else if (pingMode == 3)
            {
                return 96;
            }
            else if (pingMode == 6)
            {
                return 64;
            }
            else if (pingMode == 9)
            {
                return 128;
            }

            throw new ArgumentOutOfRangeException("pingMode");
        }

        private static bool CountFrames(Stream stream, out int frameCount, out int frameSize)
        {
            if (ReadAt<ArisFrameHeader>(stream, Marshal.SizeOf<ArisFileHeader>(), out var frameHeader)
                && frameHeader.Version == ArisFrameHeader.ArisFrameSignature)
            {
                var beamCount = PingModeToNumBeams(frameHeader.PingMode);
                var sampleCount = frameHeader.SamplesPerBeam;
                var frameDataSize = beamCount * sampleCount;
                var fullFrameSize = (int)frameDataSize + Marshal.SizeOf<ArisFrameHeader>();
                var availableFrameData = stream.Length - Marshal.SizeOf<ArisFileHeader>();
                frameCount = (int)(availableFrameData / fullFrameSize);
                frameSize = fullFrameSize;
                return true;
            }
            else
            {
                Console.Error.WriteLine("Bad frame header");
                frameCount = 0;
                frameSize = 0;
                return false;
            }
        }

        private static bool ReadFileHeader(Stream stream, out ArisFileHeader header) =>
            ReadAt<ArisFileHeader>(stream, 0L, out header);

        private static bool ReadFrameHeaderByIndex(Stream stream, int index, int fullFrameSize, out ArisFrameHeader header)
        {
            var pos = Marshal.SizeOf<ArisFileHeader>() + (index * fullFrameSize);

            return ReadAt<ArisFrameHeader>(stream, pos, out header);
        }

        private static bool ReadAt<T>(Stream stream, long position, out T t)
        {
            var buf = new byte[Marshal.SizeOf<T>()];

            if (stream.Seek(position, SeekOrigin.Begin) != position
                || stream.Read(buf, 0, buf.Length) != buf.Length)
                    {
                t = default(T);
                return false;
            }

            unsafe
            {
                fixed (byte* pB = buf)
                {
                    var newValue = Marshal.PtrToStructure<T>(new IntPtr(pB));
                    t = newValue;
                    return true;
                }
            }
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

        private static IEnumerable<string> GetOrderedFrameHeaderFieldNames()
        {
            foreach (var f in typeof(ArisFrameHeader).GetFields().Where(f => !f.IsStatic && f.Name != "padding"))
            {
                yield return f.Name;
            }
        }

        private static IEnumerable<FieldInfo> GetFieldInfos(HashSet<string> filter)
        {
            bool useAll = filter.Count == 0;

            var t = typeof(ArisFrameHeader);
            var d = new Dictionary<string, FieldInfo>();

            foreach (var name in GetOrderedFrameHeaderFieldNames().Where(name => useAll || filter.Contains(name)))
            {
                yield return t.GetField(name);
            }
        }
    }
}
