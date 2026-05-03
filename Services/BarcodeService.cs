using System;
using System.IO;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp;
using SkiaSharp;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services
{
    public interface IBarcodeService
    {
        byte[] GenerateBarcode(string content, BarcodeType type, int width = 300, int height = 100);
        byte[] GenerateLabel(string content, BarcodeType type, string partNumber, string description, int width = 400, int height = 200);
        string DecodeBarcode(byte[] imageData);
        string DecodeBarcodeFromBase64(string base64Image);
    }

    /// <summary>
    /// Thrown when the SkiaSharp native library cannot be loaded on this host.
    /// The controller layer should translate this into a 503 instead of letting
    /// it bubble up as an unhandled exception (which kills the process via the
    /// SkiaSharp finalizer).
    /// </summary>
    public class BarcodeServiceUnavailableException : Exception
    {
        public BarcodeServiceUnavailableException(string message, Exception inner) : base(message, inner) { }
    }

    public class BarcodeService : IBarcodeService
    {
        private static readonly Lazy<bool> _nativeAvailable = new(ProbeNative);

        private static bool ProbeNative()
        {
            // CRITICAL: do NOT construct any SkiaSharp object here. If the
            // native lib is missing, SkiaSharp's static initializer fails and
            // any partially-constructed managed wrapper will crash the process
            // from its finalizer thread (uncatchable). Instead, look for the
            // shared library on disk via the standard probe paths.
            var candidates = new List<string>();
            var baseDir = AppContext.BaseDirectory ?? string.Empty;
            if (!string.IsNullOrEmpty(baseDir))
            {
                candidates.Add(Path.Combine(baseDir, "libSkiaSharp.so"));
                candidates.Add(Path.Combine(baseDir, "runtimes", "linux-x64", "native", "libSkiaSharp.so"));
            }
            var ldPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? string.Empty;
            foreach (var dir in ldPath.Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                candidates.Add(Path.Combine(dir, "libSkiaSharp.so"));
            }
            return candidates.Any(File.Exists);
        }

        private static void EnsureNativeAvailable()
        {
            if (!_nativeAvailable.Value)
            {
                throw new BarcodeServiceUnavailableException(
                    "SkiaSharp native library (libSkiaSharp) is not available on this host.",
                    new DllNotFoundException("libSkiaSharp"));
            }
        }

        public byte[] GenerateBarcode(string content, BarcodeType type, int width = 300, int height = 100)
        {
            EnsureNativeAvailable();
            var barcodeFormat = MapBarcodeType(type);
            var writer = new BarcodeWriter
            {
                Format = barcodeFormat,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 10,
                    PureBarcode = false
                }
            };

            using var bitmap = writer.Write(content);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        public byte[] GenerateLabel(string content, BarcodeType type, string partNumber, string description, int width = 400, int height = 200)
        {
            EnsureNativeAvailable();
            var barcodeFormat = MapBarcodeType(type);
            var barcodeHeight = type == BarcodeType.QRCode || type == BarcodeType.DataMatrix ? 120 : 80;
            
            var writer = new BarcodeWriter
            {
                Format = barcodeFormat,
                Options = new EncodingOptions
                {
                    Width = width - 40,
                    Height = barcodeHeight,
                    Margin = 5,
                    PureBarcode = true
                }
            };

            using var barcodeBitmap = writer.Write(content);
            
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            using var borderPaint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1
            };
            canvas.DrawRect(0, 0, width - 1, height - 1, borderPaint);

            using var titlePaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 14,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };
            canvas.DrawText(partNumber, 10, 20, titlePaint);

            using var descPaint = new SKPaint
            {
                Color = SKColors.DarkGray,
                TextSize = 11,
                IsAntialias = true
            };
            var truncatedDesc = description.Length > 45 ? description.Substring(0, 42) + "..." : description;
            canvas.DrawText(truncatedDesc, 10, 38, descPaint);

            var barcodeX = (width - barcodeBitmap.Width) / 2;
            var barcodeY = 50;
            canvas.DrawBitmap(barcodeBitmap, barcodeX, barcodeY);

            using var codePaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 12,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center
            };
            canvas.DrawText(content, width / 2, height - 10, codePaint);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        public string DecodeBarcode(byte[] imageData)
        {
            EnsureNativeAvailable();
            using var stream = new MemoryStream(imageData);
            using var bitmap = SKBitmap.Decode(stream);
            
            if (bitmap == null)
                return string.Empty;

            var reader = new BarcodeReader();
            var result = reader.Decode(bitmap);
            return result?.Text ?? string.Empty;
        }

        public string DecodeBarcodeFromBase64(string base64Image)
        {
            if (string.IsNullOrEmpty(base64Image))
                return string.Empty;

            var base64Data = base64Image;
            if (base64Image.Contains(","))
                base64Data = base64Image.Split(',')[1];

            var imageBytes = Convert.FromBase64String(base64Data);
            return DecodeBarcode(imageBytes);
        }

        private static BarcodeFormat MapBarcodeType(BarcodeType type)
        {
            return type switch
            {
                BarcodeType.Code128 => BarcodeFormat.CODE_128,
                BarcodeType.Code39 => BarcodeFormat.CODE_39,
                BarcodeType.QRCode => BarcodeFormat.QR_CODE,
                BarcodeType.DataMatrix => BarcodeFormat.DATA_MATRIX,
                BarcodeType.EAN13 => BarcodeFormat.EAN_13,
                BarcodeType.UPC => BarcodeFormat.UPC_A,
                _ => BarcodeFormat.CODE_128
            };
        }
    }
}
