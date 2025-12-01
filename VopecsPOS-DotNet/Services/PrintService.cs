using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Threading.Tasks;

namespace VopecsPOS.Services
{
    public class PrintService
    {
        private string? _imagePath;
        private Image? _printImage;
        private string _paperSize = "80mm";
        private double _scale = 0.8;

        // Paper dimensions in hundredths of an inch
        private static readonly (int Width, int Height) Paper58mm = (228, 0);  // 58mm = ~2.28 inches
        private static readonly (int Width, int Height) Paper80mm = (315, 0);  // 80mm = ~3.15 inches
        private static readonly (int Width, int Height) PaperA4 = (827, 1169); // A4 = 210mm x 297mm

        public async Task<bool> PrintImageAsync(string imagePath, string paperSize, double scale)
        {
            _imagePath = imagePath;
            _paperSize = paperSize;
            _scale = scale;

            return await Task.Run(() =>
            {
                try
                {
                    LogService.Info($"PrintService: Starting print with paper={paperSize}, scale={scale}");

                    using var printDoc = new PrintDocument();

                    // Get default printer
                    var defaultPrinter = new PrinterSettings().PrinterName;
                    printDoc.PrinterSettings.PrinterName = defaultPrinter;
                    LogService.Info($"PrintService: Using printer: {defaultPrinter}");

                    // Set paper size based on selection
                    SetPaperSize(printDoc, paperSize);

                    // Handle print page event
                    printDoc.PrintPage += PrintDoc_PrintPage;

                    // Print silently (no dialog)
                    printDoc.Print();

                    LogService.Info("PrintService: Print job sent successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    LogService.Error("PrintService: Print failed", ex);
                    return false;
                }
                finally
                {
                    _printImage?.Dispose();
                    _printImage = null;
                }
            });
        }

        private void SetPaperSize(PrintDocument printDoc, string paperSize)
        {
            var ps = printDoc.DefaultPageSettings;

            // Set margins to minimum
            ps.Margins = new Margins(0, 0, 0, 0);

            switch (paperSize)
            {
                case "58mm":
                    ps.PaperSize = new PaperSize("Receipt 58mm", Paper58mm.Width, 1100);
                    break;
                case "80mm":
                    ps.PaperSize = new PaperSize("Receipt 80mm", Paper80mm.Width, 1100);
                    break;
                case "A4":
                    ps.PaperSize = new PaperSize("A4", PaperA4.Width, PaperA4.Height);
                    break;
                default:
                    ps.PaperSize = new PaperSize("Receipt 80mm", Paper80mm.Width, 1100);
                    break;
            }

            LogService.Info($"PrintService: Paper size set to {ps.PaperSize.Width}x{ps.PaperSize.Height} (1/100 inch)");
        }

        private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            try
            {
                if (_imagePath == null || !File.Exists(_imagePath))
                {
                    LogService.Error("PrintService: Image file not found");
                    e.HasMorePages = false;
                    return;
                }

                if (_printImage == null)
                {
                    _printImage = Image.FromFile(_imagePath);
                }

                var g = e.Graphics;
                if (g == null)
                {
                    e.HasMorePages = false;
                    return;
                }

                // Get print area
                var printArea = e.PageBounds;
                var marginBounds = e.MarginBounds;

                // Calculate scaled dimensions
                var scaledWidth = (int)(_printImage.Width * _scale);
                var scaledHeight = (int)(_printImage.Height * _scale);

                // For receipt printers, fit width to paper
                float ratio;
                if (_paperSize == "58mm" || _paperSize == "80mm")
                {
                    ratio = (float)marginBounds.Width / _printImage.Width;
                    scaledWidth = marginBounds.Width;
                    scaledHeight = (int)(_printImage.Height * ratio * _scale);
                }

                LogService.Info($"PrintService: Printing image {scaledWidth}x{scaledHeight} to area {marginBounds.Width}x{marginBounds.Height}");

                // Draw image
                g.DrawImage(_printImage, 0, 0, scaledWidth, scaledHeight);

                e.HasMorePages = false;
            }
            catch (Exception ex)
            {
                LogService.Error("PrintService: Error in PrintPage", ex);
                e.HasMorePages = false;
            }
        }

        public static string GetDefaultPrinterName()
        {
            return new PrinterSettings().PrinterName;
        }

        public static string[] GetAvailablePrinters()
        {
            var printers = new string[PrinterSettings.InstalledPrinters.Count];
            PrinterSettings.InstalledPrinters.CopyTo(printers, 0);
            return printers;
        }
    }
}
