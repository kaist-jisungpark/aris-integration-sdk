using System;
using System.IO;

namespace extract_frame_headers
{
    class Program
    {
        static void Main(string[] args)
        {
            Arguments arguments;
            if (ParseArguments(args, out arguments))
            {
                if (arguments.ShowFieldNames)
                {
                    Console.WriteLine("Available field names:");

                    foreach (var name in ArisFile.GetOrderedFrameHeaderFieldNames())
                    {
                        Console.WriteLine($"  {name}");
                    }
                }
                else
                {
                    if (ArisFile.Create(arguments.FilePath, out var arisFile))
                    {
                        using (var api = arisFile)
                        {
                            api.ExportCsv(Console.Out);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"Couldn't process file '${arguments.FilePath}'");
                    }
                }
            }
            else
            {
                Console.Error.WriteLine("Invalid or missing arguments.");
                ShowUsage(Console.Error);
                Environment.Exit(1);
            }
        }

        private static bool ParseArguments(string[] clArgs, out Arguments args)
        {
            if (clArgs.Length == 0)
            {
                args = null;
                return false;
            }

            var showFieldNames = clArgs[0] == "--names";
            var filePath = clArgs[0];
            var fieldsOfInterest = new string[0];

            args = new Arguments { ShowFieldNames = showFieldNames, FilePath = filePath, FieldsOfInterest = fieldsOfInterest };
            return true;
        }

        private static void ShowUsage(TextWriter output)
        {
            output.WriteLine("USAGE: extract-frame-headers <aris-recording-path>");
        }
    }

    internal class Arguments
    {
        public bool ShowFieldNames;
        public string FilePath { get; set; }
        public string[] FieldsOfInterest { get; set; }
    }
}
