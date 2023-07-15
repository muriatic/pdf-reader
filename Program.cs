using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;

namespace PdfRead
{
    class Program
    {
        static string ReadPdf(string fileName)
        {
            // string fileName = @"C:\Users\comur\Documents\GitHub\pdf-reader\resume.pdf";

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
        static void Main (string[] args)
        {
            // convertHTMLtoPDF(output, input);
            int totalCount = args.Length;

            if (totalCount != 1)
            {
                throw new ArgumentException(
                    "An unacceptable amount of arguments [" + totalCount + "] was provided, this program requires one arg, {pdf file}");
            }

            string writeTostdOut = ReadPdf(args[0]);

            Console.WriteLine(writeTostdOut);
        }
    }
}