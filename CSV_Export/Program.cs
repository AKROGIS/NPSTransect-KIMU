using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Mono.Options;

namespace CSV_Export
{
    class Program
    {
        static void Main(string[] args)
        {

            bool show_help = false;
            //string layerFile = "C:\\KIMU\\CSV_Export.lyr";
            string fgdb = "C:\\KIMU\\murrelets.gdb";
            string outDir = "C:\\KIMU\\CSV";
            string infile = null;
            string outfile = null;
            string defaultInFile = fgdb;
            int defaultYear = DateTime.Today.Year;

            //FIXME - outfile should be a directory, and file name is dynamic.

            var p = new OptionSet
                        {
			                "Usage: exportCSV [OPTIONS]+ year",
			                "Read ArcGIS data of a given year and create a CSV file",
			                "If year is not specified, the current year is used.",
			                "",
			                "Options:",
			                { "i|in=", "The layerfile/geodatabase to use.  Default is C:\\KIMU\\murrelets.gdb",
			                  v => { if (v != null) infile = v; }
                            },
			                { "o|out=", "The name of the CSV file to create.  Default is C:\\KIMU\\CSV\\[year].csv.",
			                  v => { if (v != null) outfile = v; }
                            },
			                { "v|verbose", "Increase debug message verbosity",
			                  v => { if (v != null) ++_verbosity; } 
                            },
			                { "h|help",  "Show this message and exit", 
			                  v => show_help = v != null
                            },
		                };

            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Error(e.Message);
                return;
            }

            int year = defaultYear;

            if (extra.Count == 1)
            {
                if (!int.TryParse(extra[0], out year))
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

            if (extra.Count > 1)
            {
                Error("Only one year may be specified.  {0} provided.", extra.Count);
                return;
            }

            if (show_help)
            {
                p.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (string.IsNullOrEmpty(infile))
                infile = defaultInFile;

            if (!Directory.Exists(infile))
            {
                Error("Error: Geodatabase Not found = {0}", infile);
                return;
            }

            string defaultOutfile = Path.Combine(outDir, year + ".csv");
            if (string.IsNullOrEmpty(outfile))
                outfile = defaultOutfile;

            string directory = Path.GetDirectoryName(outfile);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                Error("Error: directory for output ({0}) does not exist.", outfile);
                return;
            }

            Debug("Processing Year = {0}", year);
            Debug("Reading = {0}", infile);
            Debug("Writing = {0}", outfile);

            try
            {
                using (var output = new FileStream(outfile, FileMode.Create))
                {
                    var translator = new Translator
                                         {
                                             Year = year,
                                             WorkspacePath = infile,
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

        static int _verbosity;

        static void Debug(string format, params object[] args)
        {
            if (_verbosity > 0)
            {
                Console.Write("# ");
                Console.WriteLine(format, args);
            }
        }

        static void Error(string format, params object[] args)
        {
            Console.Write("exportCSV: ");
            Console.WriteLine(format, args);
            Console.WriteLine("Try `exportCSV --help' for more information.");
        }

    }
}
