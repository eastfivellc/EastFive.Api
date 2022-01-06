﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

using EastFive.Extensions;
using EastFive.Images;

namespace EastFive.Api
{
    public class ImageSharpHttpResponse : HttpResponse
    {
        protected Image newImage;
        protected IImageFormat encoder;

        public ImageSharpHttpResponse(IHttpRequest request, HttpStatusCode statusCode,
            Image image,
            int? width = default(int?), int? height = default(int?), bool? fill = default,
            string fileName = default, string contentType = default, bool? inline = default)
            : base(request, statusCode)
        {
            this.encoder = contentType.ParseImageEncoder();
            this.SetFileHeaders(fileName, encoder.DefaultMimeType, inline);

            //var xForm = image.FixOrientation();
            var ratio = ((double)image.Width) / ((double)image.Height);
            var newWidth = (int)Math.Round(width.HasValue ?
                    width.Value
                    :
                    height.HasValue ?
                        height.Value * ratio
                        :
                        image.Width);
            var newHeight = (int)Math.Round(height.HasValue ?
                    height.Value
                    :
                    width.HasValue ?
                        width.Value / ratio
                        :
                        image.Width);

            image.Mutate(x => x.Resize(newWidth, newHeight));
            this.newImage = image;
        }

        public override async Task WriteResponseAsync(Stream responseStream)
        {
            await this.newImage.SaveAsync(responseStream, this.encoder);
            await responseStream.FlushAsync();
        }

    }
}
