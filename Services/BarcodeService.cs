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

    public class BarcodeService : IBarcodeService
    {
        public byte[] GenerateBarcode(string content, BarcodeType type, int width = 300, int height = 100)
        {
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
