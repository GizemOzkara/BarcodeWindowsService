using ImageMagick;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZXing;
using SD = System.Drawing;

public static class BarcodeReaderEngine
{
    // ========== IMAGE ==========
    public static List<string> ReadFromImage(string path)
    {
        using (IMagickImage img = new MagickImage(path))
        {
            img.Resize(2500, 0);
            using (SD.Bitmap bmp = ToBitmap(img))
            {
                return DecodeAll(bmp);
            }
        }
    }

    // ========== PDF ==========
    public static List<string> ReadFromPdf(string pdfPath)
    {
        var list = new List<string>();

        var settings = new MagickReadSettings
        {
            Density = new Density(300)
        };

        using (var images = new MagickImageCollection())
        {
            images.Read(pdfPath, settings);

            foreach (IMagickImage img in images)
            {
                img.Resize(2500, 0);
                using (SD.Bitmap bmp = ToBitmap(img))
                {
                    list.AddRange(DecodeAll(bmp));
                }
            }
        }

        return list;
    }

    // ========== ZXING ==========
    private static List<string> DecodeAll(SD.Bitmap bmp)
    {
        var results = new List<string>();
        var reader = CreateReader();

        var r1 = reader.DecodeMultiple(bmp);
        if (r1 != null)
            results.AddRange(r1.Select(x => x.Text));

        using (SD.Bitmap padded = AddPadding(bmp, 40))
        {
            var r2 = reader.DecodeMultiple(padded);
            if (r2 != null)
                results.AddRange(r2.Select(x => x.Text));
        }

        return results;
    }

    private static BarcodeReader<SD.Bitmap> CreateReader()
    {
        return new BarcodeReader<SD.Bitmap>(
            bmp => new ZXing.BitmapLuminanceSource(bmp)
        )
        {
            AutoRotate = true,
            Options =
            {
                TryHarder = true,
                TryInverted = true
            }
        };
    }

    // ========== HELPERS ==========
    private static SD.Bitmap AddPadding(SD.Bitmap src, int pad)
    {
        SD.Bitmap bmp = new SD.Bitmap(
            src.Width + pad * 2,
            src.Height + pad * 2
        );

        using (SD.Graphics g = SD.Graphics.FromImage(bmp))
        {
            g.Clear(SD.Color.White);
            g.DrawImage(src, pad, pad);
        }

        return bmp;
    }

    private static SD.Bitmap ToBitmap(IMagickImage image)
    {
        using (var ms = new MemoryStream())
        {
            image.Format = MagickFormat.Bmp;
            image.Write(ms);
            ms.Position = 0;
            return new SD.Bitmap(ms);
        }
    }
}
