using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;

namespace CSV_Export
{
    class Program
    {
        static int _verbosity;

        static void Main(string[] args)
        {

            bool showHelp = false;
            const string defaultOutDir = "C:\\KIMU\\CSV";
            const string defaultInFile = "C:\\KIMU\\murrelets.gdb";

            string inFile = null;
            string outFile = null;
            int year = DateTime.Today.Year;

            var commandOptions = new OptionSet
                        {
			                "Usage: exportCSV [OPTIONS]+ year",
			                "Read ArcGIS data of a given year and create a CSV file",
			                "If year is not specified, the current year is used.",
			                "",
			                "Options:",
			                { "i|in=", "The file geodatabase to use.  Default is " + defaultInFile,
			                  v => { if (v != null) inFile = v; }
                            },
			                { "o|out=", "The name of the CSV file to create.  Default is " + defaultOutDir + "[year].csv.",
			                  v => { if (v != null) outFile = v; }
                            },
			                { "v|verbose", "Increase debug message verbosity",
			                  v => { if (v != null) ++_verbosity; } 
                            },
			                { "h|help",  "Show this message and exit", 
			                  v => showHelp = v != null
                            },
		                };

            List<string> commandArguments;
            try
            {
                commandArguments = commandOptions.Parse(args);
            }
            catch (OptionException ex)
            {
                Error(ex.Message);
                return;
            }

            if (commandArguments.Count > 1)
            {
                Error("Only one year may be specified.  {0} provided.", commandArguments.Count);
                return;
            }

            if (showHelp)
            {
                commandOptions.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (commandArguments.Count == 1)
            {
                if (!int.TryParse(commandArguments[0], out year))
                {
                    Error("Year is not an integer.");
                    return;
                }
                if (year < 2010 || year > 2099)
                {
                    Error("Year must be in the range 2010-2099.");
                    return;
                }
            }

            if (string.IsNullOrEmpty(inFile))
                inFile = defaultInFile;

            if (!Directory.Exists(inFile))
            {
                Error("Error: File Geodatabase Not found = {0}", inFile);
                return;
            }

            if (string.IsNullOrEmpty(outFile))
                outFile = Path.Combine(defaultOutDir, year + ".csv");

            string directory = Path.GetDirectoryName(outFile);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                Error("Error: directory for output ({0}) does not exist.", outFile);
                return;
            }

            Debug("Processing Year = {0}", year);
            Debug("Reading = {0}", inFile);
            Debug("Writing = {0}", outFile);

            try
            {
                using (var output = new FileStream(outFile, FileMode.Create))
                {
                    var translator = new Translator
                                         {
                                             Year = year,
                                             WorkspacePath = inFile,
                                             Output = output,
                                         };
                    translator.Translate();
                }
                Debug("CSV created successfully.");
            }
            catch (Exception ex)
            {
                Debug("CSV creation failed: {0}", ex.Message);
            }
        }

        static void Debug(string format, params object[] args)
        {
            if (_verbosity <= 0) return;
            Console.Write("# ");
            Console.WriteLine(format, args);
        }

        static void Error(string format, params object[] args)
        {
            Console.Write("exportCSV: ");
            Console.WriteLine(format, args);
            Console.WriteLine("Try `exportCSV --help' for more information.");
        }

    }
}
