// XAML Map Control - https://github.com/ClemensFischer/XAML-Map-Control
// � 2022 Clemens Fischer
// Licensed under the Microsoft Public License (Ms-PL)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MapControl.Images
{
    public partial class GeoTaggedImage
    {
        private const string PixelScaleQuery = "/ifd/{ushort=33550}";
        private const string TiePointQuery = "/ifd/{ushort=33922}";
        private const string TransformationQuery = "/ifd/{ushort=34264}";
        private const string NoDataQuery = "/ifd/{ushort=42113}";

        public static Task<GeoTaggedImage> ReadGeoTiff(string imageFilePath)
        {
            return Task.Run(() =>
            {
                BitmapSource bitmap;
                Matrix transform;

                using (var stream = File.OpenRead(imageFilePath))
                {
                    bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }

                var mdata = bitmap.Metadata as BitmapMetadata;

                if (mdata.GetQuery(PixelScaleQuery) is double[] ps &&
                    mdata.GetQuery(TiePointQuery) is double[] tp &&
                    ps.Length == 3 && tp.Length >= 6)
                {
                    transform = new Matrix(ps[0], 0d, 0d, -ps[1], tp[3], tp[4]);
                }
                else if (mdata.GetQuery(TransformationQuery) is double[] tf && tf.Length == 16)
                {
                    transform = new Matrix(tf[0], tf[1], tf[4], tf[5], tf[3], tf[7]);
                }
                else
                {
                    throw new ArgumentException("No coordinate transformation found in \"" + imageFilePath + "\".");
                }

                if (mdata.GetQuery(NoDataQuery) is string noData && int.TryParse(noData, out int noDataValue))
                {
                    bitmap = ConvertTransparentPixel(bitmap, noDataValue);
                }

                return new GeoTaggedImage(bitmap, transform, null);
            });
        }

        public static BitmapSource ConvertTransparentPixel(BitmapSource source, int transparentPixel)
        {
            var targetFormat = source.Format;
            List<Color> colors = null;

            if (source.Format == PixelFormats.Indexed8 ||
                source.Format == PixelFormats.Indexed4 ||
                source.Format == PixelFormats.Indexed2)
            {
                targetFormat = source.Format;
                colors = source.Palette.Colors.ToList();
            }
            else if (source.Format == PixelFormats.Gray8)
            {
                targetFormat = PixelFormats.Indexed8;
                colors = BitmapPalettes.Gray256.Colors.ToList();
            }
            else if (source.Format == PixelFormats.Gray4)
            {
                targetFormat = PixelFormats.Indexed8;
                colors = BitmapPalettes.Gray16.Colors.ToList();
            }
            else if (source.Format == PixelFormats.Gray2)
            {
                targetFormat = PixelFormats.Indexed8;
                colors = BitmapPalettes.Gray4.Colors.ToList();
            }

            if (colors == null || transparentPixel >= colors.Count)
            {
                return source;
            }

            colors[transparentPixel] = Colors.Transparent;

            var stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
            var buffer = new byte[stride * source.PixelHeight];

            source.CopyPixels(buffer, stride, 0);

            var target = BitmapSource.Create(
                source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY,
                targetFormat, new BitmapPalette(colors), buffer, stride);

            target.Freeze();

            return target;
        }
    }
}
