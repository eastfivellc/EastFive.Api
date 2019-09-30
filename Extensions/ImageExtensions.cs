using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public static class ImageExtensions
    {
        public static Image ResizeImage(this Image image,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?))
        {
            var ratio = ((double)image.Size.Width) / ((double)image.Size.Height);
            var newWidth = (int)Math.Round(width.HasValue ?
                    width.Value
                    :
                    height.HasValue ?
                        height.Value * ratio
                        :
                        image.Size.Width);
            var newHeight = (int)Math.Round(height.HasValue ?
                    height.Value
                    :
                    width.HasValue ?
                        width.Value / ratio
                        :
                        image.Size.Width);

            var newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = Graphics.FromImage(newImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                var brush = System.Drawing.Brushes.White;
                graphics.FillRectangle(brush, 0, 0, newWidth, newHeight);

                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
                return newImage;
            }
        }

        public static void Save(this Image image, Stream outputStream, 
            string encodingMimeType = "image/jpeg", long encoderQuality = 80L)
        {
            var encoder = getEncoderInfo(encodingMimeType);

            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, encoderQuality);

            image.Save(outputStream, encoder, encoderParameters);
        }

        private static ImageCodecInfo getEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();

            for (int j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType.ToLower() == mimeType.ToLower())
                {
                    return encoders[j];
                }
            }

            return null;
        }

        public static Image Crop(this Image image, int x, int y, int w, int h)
        {
            var newImage = new Bitmap(w, h, PixelFormat.Format32bppArgb);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = Graphics.FromImage(newImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                var brush = System.Drawing.Brushes.Transparent;
                graphics.FillRectangle(brush, 0, 0, w, h);

                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0,
                    new System.Drawing.Rectangle(x, y, w, h),
                    GraphicsUnit.Pixel);
            }
            return newImage;
        }

        public static Image Crop(this Image image, double x, double y, double w, double h)
        {
            return image.Crop(
                (int)(x * image.Width),
                (int)(y * image.Height),
                (int)(w * image.Width),
                (int)(h * image.Height));
        }

        public static Image Scale(this Image image,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?))
        {
            bool KeepOriginal()
            {
                if (width.HasValue || height.HasValue || fill.HasValue)
                    return false;
                return true;
            }
            if (KeepOriginal())
                return image;

            var ratio = ((double)image.Size.Width) / ((double)image.Size.Height);
            var newWidth = (int)Math.Round(width.HasValue ?
                    width.Value
                    :
                    height.HasValue ?
                        height.Value * ratio
                        :
                        image.Size.Width);
            var newHeight = (int)Math.Round(height.HasValue ?
                    height.Value
                    :
                    width.HasValue ?
                        width.Value / ratio
                        :
                        image.Size.Width);

            var newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = Graphics.FromImage(newImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                var brush = System.Drawing.Brushes.Transparent;
                graphics.FillRectangle(brush, 0, 0, newWidth, newHeight);

                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }
            return newImage;
        }
    }
}
