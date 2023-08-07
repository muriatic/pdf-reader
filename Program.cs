using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Text;

namespace PdfRead
{
    class Program
    {
        static int[] CreateChunks(int length, int n)
        {
            // create n number chunks
            int[] chunks = new int[n];

            int quotient = length / n;
            int remainder = length % n;

            // if its greater than one we can spread it out more
            if (remainder > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    int addition = 0;

                    if (remainder > 0)
                    {
                        addition = 1;
                        remainder--;
                    }

                    chunks[i] = quotient + addition;
                }
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    chunks[i] = quotient;
                }

            }

            return chunks;
        }
        static string ReadPDF(string fileName)
        {
            PdfReader pdfRead = new PdfReader(fileName);
            PdfDocument pdfDoc = new PdfDocument(pdfRead);

            ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();

            StringBuilder processed = new StringBuilder();

            for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
            {
                var page = pdfDoc.GetPage(i);
                string text = PdfTextExtractor.GetTextFromPage(page, strategy);
                processed.Append(text);
            }

            return processed.ToString();
        }
        static string[] CombinePaths(string DIRECTORY, string[] FILE_NAME)
        {
            string[] paths = new string[FILE_NAME.Length];

            for (int i = 0; i < FILE_NAME.Length; i++)
            {
                string combined_path = System.IO.Path.Combine(DIRECTORY, FILE_NAME[i]);
                paths[i] = combined_path;
            }

            return paths;
        }
        static string[][] NewThread(int[] IDs, string[] FileNames)
        {
            string[][] ID_asc_INFO = new string[IDs.Length][];
            for (int i = 0; i < IDs.Length; i++) 
            {
                // have to convert to string for the array to accept it
                ID_asc_INFO[i] = new string[] { IDs[i].ToString(), ReadPDF(FileNames[i]) };
            }

            return ID_asc_INFO;
        }
        static void OrderByUID (string[][][] identified_PDF_info, int[] IDs)
        {
            string[] PDFInfo1dArray = new string[IDs.Length];
            for (int x = 0; x < identified_PDF_info.Length; x++)
            {
                for (int y = 0; y < identified_PDF_info[x].Length; y++)
                {
                    int idx = Convert.ToUInt16(identified_PDF_info[x][y][0]);
                    PDFInfo1dArray[idx] = identified_PDF_info[x][y][1];
                }
            }

            // formatted like ["resume", ... , "resume"]
            // we'll split each PDF info with my own character (shouldn't be found in PDFs)
            string PDFInfoString = string.Join("", PDFInfo1dArray);

            // Write to Console (picked up by Python)
            Console.WriteLine(PDFInfoString);
        }
        static void Reader(string DIRECTORY, string[] UIDs, string[] PDFs, int threads)
        {
            // convert UIDs to int (should make it easier to sort)
            int[] IDs = Enumerable.Range(0, UIDs.Length).ToArray();
            int IDs_length = IDs.Length;
            int subprocesses;

            // if no value of threads is inputted; default to 2
            if (threads == 0)
            {
                subprocesses = 2;
            }
            else
            {
                subprocesses = threads;
            }

            // change # of subprocess to the length of IDs if length of IDs is less than the # of subprocesses
            if (IDs_length < subprocesses)
            {
                subprocesses = IDs_length;
            }

            // create array into even(ish) # chunks based on # of subprocesses
            int[] chunks = CreateChunks(IDs_length, subprocesses);

            // create nested arrays with subprocesses # of top nests for PDF names
            string[][] chunked_PDF_names = new string[subprocesses][];
            int[][] chunked_IDs = new int[subprocesses][];

            int last_idx = 0;
            for (int i = 0; i < subprocesses; i++) // O(N^2) time complexity
            {
                // get chunk of IDs after last cutoff and this one
                var string_chunk = PDFs[last_idx..(chunks[i] + last_idx)];

                string[] combined_PDF_paths = CombinePaths(DIRECTORY, string_chunk);

                chunked_PDF_names[i] = combined_PDF_paths;
                chunked_IDs[i] = IDs[last_idx..(chunks[i] + last_idx)];

                last_idx = chunks[i];
            }

            // nested arrays that will contain UIDs and resume info like this: [[UID1, resume info], [UID2, resume_info]]
            string[][][] identified_PDF_info = new string[subprocesses][][];
            Parallel.For(0, subprocesses, i =>
            {
                identified_PDF_info[i] = NewThread(chunked_IDs[i], chunked_PDF_names[i]);
            });

            OrderByUID(identified_PDF_info, IDs);
        }
        public static string[] ParseString(string delimited_str)
        {
            // split on semicolons
            string[] parsedString = delimited_str.Split(';');

            // trim down leading and trailing whitespaces
            for (int i = 0; i < parsedString.Length; i++)
            {
                parsedString[i] = parsedString[i].Trim();
            }
            return parsedString;
        }
        public static async Task<int> Main(string[] args)
        {
            /* ARGS:
             * -dir
             * -id
             * -pdfs
             * -threads (optional)
            */

            var DIRECTORY_option = new Option<string>(
                new string[] { "-dir" },
                "Where the PDFs are stored");

            var IDs_option = new Option<string>(
                new string[] { "-id" },
                "UID associated with each PDF");

            var PDFs_option = new Option<string>(
                new string[] { "-pdfs" },
                "PDF names");

            var threads_option = new Option<int>(
                new string[] { "-threads", "--subprocesses" },
                "Number of subprocesses or threads (default = 2)");

            var rootCommand = new RootCommand("Reads PDF files in given directory and returns a Python dictionary with associated UID")
            {
                DIRECTORY_option,
                IDs_option,
                PDFs_option,
                threads_option
            };

            rootCommand.SetHandler(
                (DIRECTORY_value, IDs_value, PDFs_value, threads_value) =>
                {
                    OnHandle(DIRECTORY_value, IDs_value, PDFs_value, threads_value);
                },
                DIRECTORY_option, IDs_option, PDFs_option, threads_option);

            var commandLineBuilder = new CommandLineBuilder(rootCommand)
                .UseDefaults();
            var parser = commandLineBuilder.Build();
            return await parser.InvokeAsync(args).ConfigureAwait(false);
        }

        private static void OnHandle(string DIRECTORY, string IDs, string PDFs, int threads)
        {
            if (string.IsNullOrEmpty(DIRECTORY))
            {
                throw new ArgumentNullException(
                    "-dir is required; please ensure that a value is provided");
            }

            if (string.IsNullOrEmpty(IDs))
            {
                throw new ArgumentNullException(
                    "-id is required; please ensure that a value is provided");
            }

            if (string.IsNullOrEmpty(PDFs))
            {
                throw new ArgumentNullException(
                    "-pdfs is required; please ensure that a value is provided");
            }

            Reader(DIRECTORY, ParseString(IDs), ParseString(PDFs), threads);
        }
    }
}