using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Linq;
using BlackBarLabs.Linq.Async;
using BlackBarLabs.Api.Resources;
using BlackBarLabs.Extensions;
using EastFive;
using EastFive.Linq.Expressions;
using EastFive.Sheets;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using EastFive.Extensions;
using EastFive.Linq.Async;
using EastFive.Api.Controllers;
using EastFive.Api;
using EastFive.Collections.Generic;

namespace BlackBarLabs.Api
{
    public static class ResponseExtensions
    {
        public static HttpResponseMessage AddReason(this HttpResponseMessage response, string reason)
        {
            var reasonPhrase = reason.Replace('\n', ';').Replace("\r", "");
            if (reasonPhrase.Length > 510)
                reasonPhrase = new string(reasonPhrase.Take(510).ToArray());
            response.ReasonPhrase = reasonPhrase;
            // TODO: Check user agent and only set this on iOS and other crippled systems
            response.Headers.Add("Reason", reasonPhrase);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                response.Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(new { Message = reason }));
            return response;
        }

        public static HttpResponseMessage CreatePdfResponse(this HttpRequestMessage request, System.IO.Stream stream,
            string filename = default(string), bool inline = false)
        {
            var result = stream.ToBytes(
                (pdfData) => request.CreatePdfResponse(pdfData, filename, inline));
            return result;
        }

        public static HttpResponseMessage CreatePdfResponse(this HttpRequestMessage request, byte[] pdfData,
            string filename = default(string), bool inline = false)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(pdfData);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment")
            {
                FileName =
                            default(string) == filename ?
                                Guid.NewGuid().ToString("N") + ".pdf" :
                                filename,
            };
            return response;
        }

        #region Xlsx

        public static HttpResponseMessage CreateXlsxResponse(this HttpRequestMessage request, Stream xlsxData, string filename = "")
        {
            var content = new StreamContent(xlsxData);
            return request.CreateXlsxResponse(content, filename);
        }

        public static HttpResponseMessage CreateXlsxResponse(this HttpRequestMessage request, byte[] xlsxData, string filename = "")
        {
            var content = new ByteArrayContent(xlsxData);
            return request.CreateXlsxResponse(content, filename);
        }


        public static HttpResponseMessage CreateXlsxResponse(this HttpRequestMessage request, HttpContent xlsxContent, string filename = "")
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = xlsxContent;
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.template");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = String.IsNullOrWhiteSpace(filename) ? $"sheet.xlsx" : filename,
            };
            return response;
        }
        
        public static HttpResponseMessage CreateXlsxResponse<TResource>(this HttpRequestMessage request,
            IDictionary<string, string> properties, IEnumerable<TResource> resources,
            string filename = "")
        {
            try
            {
                var responseStream = ConvertToXlsxStreamAsync(properties, resources,
                    (stream) => stream);
                var response = request.CreateXlsxResponse(responseStream, filename);
                return response;
            }
            catch (Exception ex)
            {
                return request.CreateResponse(HttpStatusCode.Conflict, ex.StackTrace).AddReason(ex.Message);
            }
        }

        public static HttpResponseMessage CreateMultisheetXlsxResponse<TResource>(this HttpRequestMessage request,
            IDictionary<string, string> properties, IEnumerable<TResource> resources,
            string filename = "")
            where TResource : ResourceBase
        {
            try
            {
                var responseStream = ConvertToMultisheetXlsxStreamAsync(properties, resources,
                    (stream) => stream);
                var response = request.CreateXlsxResponse(responseStream, filename);
                return response;
            }
            catch (Exception ex)
            {
                return request.CreateResponse(HttpStatusCode.Conflict, ex.StackTrace).AddReason(ex.Message);
            }
        }
        
        private static TResult ConvertToXlsxStreamAsync<TResource, TResult>(
            IDictionary<string, string> properties, IEnumerable<TResource> resources,
            Func<byte[], TResult> callback)
        {
            var guidReferences = resources
                .Select(
                    (obj, index) =>
                    {
                        if (typeof(ResourceBase).IsInstanceOfType(obj))
                        {
                            var resourceId = (obj as ResourceBase).Id.ToGuid();
                            if (resourceId.HasValue)
                                return resourceId.Value.PairWithValue($"A{index}");
                        }
                        return default(KeyValuePair<Guid, string>?);
                    })
                .SelectWhereHasValue()
                .ToDictionary();

            using (var stream = new MemoryStream())
            {
                OpenXmlWorkbook.Create(stream,
                    (workbook) =>
                    {
                        #region Custom properties

                        workbook.WriteCustomProperties(
                            (writeProp) =>
                            {
                                properties.Select(
                                    prop =>
                                    {
                                        writeProp(prop.Key, prop.Value);
                                        return true;
                                    }).ToArray();
                                return true;
                            });

                        #endregion

                        workbook.WriteSheetByRow(
                            (writeRow) =>
                            {
                                var propertyOrder = typeof(TResource).GetProperties();
                                if(!propertyOrder.Any() && resources.Any())
                                {
                                    propertyOrder = resources.First().GetType().GetProperties();
                                }

                                #region Header 

                                var headers = propertyOrder
                                    .Select(
                                        propInfo => propInfo.GetCustomAttribute<JsonPropertyAttribute, string>(
                                            attr => attr.PropertyName,
                                            () => propInfo.Name))
                                    .ToArray();
                                writeRow(headers);

                                #endregion

                                #region Body

                                var rows = resources.Select(
                                    (result, index) =>
                                    {
                                        var values = propertyOrder
                                            .Select(
                                                property => property.GetValue(result).CastToXlsSerialization(property, guidReferences))
                                            .ToArray();
                                        writeRow(values);
                                        return true;
                                    }).ToArray();

                                #endregion

                                return true;
                            });

                        return true;
                    });

                var buffer = stream.ToArray();
                return callback(buffer);
            }
        }

        private static TResult ConvertToMultisheetXlsxStreamAsync<TResource, TResult>(
            IDictionary<string, string> properties, IEnumerable<TResource> resources,
            Func<byte[], TResult> callback)
            where TResource : ResourceBase
        {
            var resourceGroups = resources
                .GroupBy(
                    (res) =>
                    {
                        var resourceId = res.Id.ToGuid();
                        if (!resourceId.HasValue)
                            return "Unknown";
                        if (res.Id.URN.IsDefaultOrNull())
                            return "Unknown";
                        if (!res.Id.URN.TryParseUrnNamespaceString(out string[] nss, out string ns))
                            return "Unknown";
                        return ns;
                    });

            var guidReferences = resourceGroups
                .SelectMany(
                    grp =>
                        grp
                            .Select(
                                (res, index) =>
                                {
                                    var resourceId = res.Id.ToGuid();
                                    if (!resourceId.HasValue)
                                        return default(KeyValuePair<Guid, string>?);
                                    return resourceId.Value.PairWithValue($"{grp.Key}!A{index+2}");
                                })
                            .SelectWhereHasValue())
                .ToDictionary();

            using (var stream = new MemoryStream())
            {
                bool wroteRows = OpenXmlWorkbook.Create(stream,
                    (workbook) =>
                    {
                        #region Custom properties

                        workbook.WriteCustomProperties(
                            (writeProp) =>
                            {
                                properties.Select(
                                    prop =>
                                    {
                                        writeProp(prop.Key, prop.Value);
                                        return true;
                                    }).ToArray();
                                return true;
                            });

                        #endregion
                        
                        foreach (var resourceGrp in resourceGroups)
                        {
                            bool wroteRow = workbook.WriteSheetByRow(
                                (writeRow) =>
                                {
                                    var resourcesForSheet = resourceGrp.ToArray();
                                    if (!resourcesForSheet.Any())
                                        return false;
                                    var resource = resourcesForSheet.First();
                                    if (resource.IsDefault())
                                        return false;
                                    var propertyOrder = resource.GetType().GetProperties().Reverse().ToArray();

                                    #region Header 

                                    var headers = propertyOrder
                                        .Select(
                                            propInfo => propInfo.GetCustomAttribute<JsonPropertyAttribute, string>(
                                                attr => attr.PropertyName,
                                                () => propInfo.Name))
                                        .ToArray();
                                    writeRow(headers);

                                    #endregion

                                    #region Body

                                    var rows = resourcesForSheet.Select(
                                        (result, index) =>
                                        {
                                            var values = propertyOrder
                                            .Select(
                                                property => property.GetValue(result).CastToXlsSerialization(property, guidReferences))
                                            .ToArray();
                                            writeRow(values);
                                            return true;
                                        }).ToArray();

                                    #endregion

                                    return true;
                                },
                                resourceGrp.Key);
                        }

                        return true;
                    });

                stream.Flush();
                var buffer = stream.ToArray();
                return callback(buffer);
            }
        }

        private static object CastToXlsSerialization(this object obj, PropertyInfo property, IDictionary<Guid, string> lookups)
        {
            if (obj is WebId)
            {
                var webId = obj as WebId;
                var objIdMaybe = webId.ToGuid();
                if (!objIdMaybe.HasValue)
                    return string.Empty;

                webId.URN.TryParseUrnNamespaceString(out string[] nss, out string nid);
                var objId = objIdMaybe.Value;
                var resourceDisplayValue = $"{nid}/{objId}";
                if (property.Name == "Id" || !lookups.ContainsKey(objId)) // TODO: Use custom property attributes
                    return resourceDisplayValue;

                return new OpenXmlWorkbook.CellReference
                {
                    value = resourceDisplayValue,
                    formula = lookups[objId],
                };
            }
            return obj;
        }

        private static TResult TryCastFromXlsSerialization<TResult>(PropertyInfo property, string valueString,
            Func<object, TResult> onParsed,
            Func<TResult> onNotParsed)
        {
            if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                return TryCastFromXlsSerialization(propertyType, valueString,
                    (value) =>
                    {
                        dynamic objValue = System.Activator.CreateInstance(property.PropertyType);
                        objValue = value;
                        return onParsed((object)objValue);
                    },
                    onNotParsed);
            }

            if (property.PropertyType.IsEnum)
            {
                var enumUnderlyingType = Enum.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (int.TryParse(valueString, out int enumValueInt))
                {
                    object underlyingValueInt = System.Convert.ChangeType(enumValueInt, enumUnderlyingType);
                    return onParsed(underlyingValueInt);
                }
                var values = Enum.GetValues(property.PropertyType);
                var names = Enum.GetNames(property.PropertyType);
                if (!names.Contains(valueString))
                    return onNotParsed();

                return names.IndexOf(valueString, (s1, s2) => String.Compare(s1, s2, true) == 0,
                    (valueIndex) =>
                    {
                        var enumValue = values.GetValue(valueIndex);
                        object underlyingValue = System.Convert.ChangeType(enumValue, enumUnderlyingType);
                        return onParsed(underlyingValue);
                    },
                    onNotParsed);
            }

            return TryCastFromXlsSerialization(property.PropertyType, valueString,
                    onParsed, onNotParsed);
        }

        private static TResult TryCastFromXlsSerialization<TResult>(Type type, string valueString,
            Func<object, TResult> onParsed,
            Func<TResult> onNotParsed)
        {
            if (type.GUID == typeof(WebId).GUID)
                if (Uri.TryCreate(valueString, UriKind.Absolute, out Uri urn))
                    return urn.TryParseWebUrn(
                        (nid, ns, uuid) => onParsed(new WebId
                        {
                            UUID = uuid,
                        }),
                        (why) => onNotParsed());
                else
                    return onNotParsed();

            if (type.GUID == typeof(string).GUID)
                return onParsed(valueString);

            return type.TryParse<bool, TResult>(valueString, bool.TryParse, onParsed,
                () => type.TryParse<Guid, TResult>(valueString, Guid.TryParse, onParsed,
                () => type.TryParse<Int32, TResult>(valueString, Int32.TryParse, onParsed,
                () => type.TryParse<decimal, TResult>(valueString, decimal.TryParse, onParsed,
                () => type.TryParse<double, TResult>(valueString, double.TryParse, onParsed,
                () => type.TryParse<float, TResult>(valueString, float.TryParse, onParsed,
                () => type.TryParse<byte, TResult>(valueString, byte.TryParse, onParsed,
                () => type.TryParse<long, TResult>(valueString, long.TryParse, onParsed,
                () => type.TryParse<DateTime, TResult>(valueString, DateTime.TryParse, onParsed,
                onNotParsed)))))))));
        }

        private delegate bool FuncOut<T>(string valueString, out T value);

        private static TResult TryParse<T, TResult>(this Type type, string valueString, FuncOut<T> callback,
            Func<object, TResult> onParsed,
            Func<TResult> onNotParsed)
        {
            if (type.GUID == typeof(T).GUID)
                if (callback(valueString, out T value))
                    return onParsed(value);

            return onNotParsed();
        }

        public static TResult ParseXlsx<TResource, TResult>(this HttpRequestMessage request, System.Web.Http.Routing.UrlHelper urlHelper,
                Stream xlsx,
                Func<TResource, KeyValuePair<string, string>[], Task<HttpResponseMessage>> executePost,
                Func<TResource, KeyValuePair<string, string>[], Task<HttpResponseMessage>> executePut,
            Func<HttpResponseMessage, TResult> onComplete)
            where TResource : ResourceBase
        {
            return OpenXmlWorkbook.Read(xlsx,
                (workbook) =>
                {
                    return workbook.ReadCustomValues(
                        (customValues) =>
                        {
                            var rowsFromAllSheets = workbook.ReadSheets()
                                .SelectMany(
                                    sheet =>
                                    {
                                        var rows = sheet
                                            .ReadRows()
                                            .ToArray();
                                        if (!rows.Any())
                                            return rows;
                                        return rows.Skip(1);
                                    }).ToArray();
                            return request.ParseXlsxBackground(urlHelper, customValues, rowsFromAllSheets,
                                executePost, executePut, onComplete);
                        });
                });
        }

        private static TResult ParseXlsxBackground<TResource, TResult>(this HttpRequestMessage request, System.Web.Http.Routing.UrlHelper urlHelper,
                KeyValuePair<string, string>[] customValues, string[][] rows,
                Func<TResource, KeyValuePair<string, string>[], Task<HttpResponseMessage>> executePost,
                Func<TResource, KeyValuePair<string, string>[], Task<HttpResponseMessage>> executePut,
            Func<HttpResponseMessage, TResult> onComplete)
            where TResource : ResourceBase
        {
            var response = request.CreateResponsesBackground(urlHelper,
                (updateProgress) =>
                {
                    var propertyOrder = typeof(TResource)
                        .GetProperties()
                        .OrderBy(propInfo =>
                            propInfo.GetCustomAttribute(
                                (SheetColumnAttribute sheetColumn) => sheetColumn.GetSortValue(propInfo),
                                () => propInfo.Name));

                    return rows
                        .Select(
                            async (row) =>
                            {
                                var resource = propertyOrder
                                    .Aggregate(Activator.CreateInstance<TResource>(),
                                        (aggr, property, index) =>
                                        {
                                            var value = row.Length > index ?
                                                row[index] : default(string);
                                            TryCastFromXlsSerialization(property, value,
                                                (valueCasted) =>
                                                {
                                                    property.SetValue(aggr, valueCasted);
                                                    return true;
                                                },
                                                () => false);
                                            return aggr;
                                        });
                                if (resource.Id.IsEmpty())
                                {
                                    resource.Id = Guid.NewGuid();
                                    var postResponse = await executePost(resource, customValues);
                                    return updateProgress(postResponse);
                                }
                                var putResponse = await executePut(resource, customValues);
                                return updateProgress(putResponse);
                            })
                       .WhenAllAsync(10);
                },
                rows.Length);
            return onComplete(response);
        }

        public static TResult ParseXlsx<TResource, TResult>(this HttpRequestMessage request,
                Stream xlsx,
                Func<KeyValuePair<string, string>[], KeyValuePair<string, TResource[]>[], TResult> execute)
            where TResource : ResourceBase
        {
            var result = OpenXmlWorkbook.Read(xlsx,
                (workbook) =>
                {
                    var x = workbook.ReadCustomValues(
                        (customValues) =>
                        {
                            var propertyOrder = typeof(TResource).GetProperties();
                            var resourceList = workbook.ReadSheets()
                                .SelectReduce(
                                    (sheet, next, skip) =>
                                    {
                                        var rows = sheet
                                            .ReadRows()
                                            .ToArray();
                                        if (!rows.Any())
                                            return skip();

                                        var resources = rows
                                            .Skip(1)
                                            .Select(
                                                row =>
                                                {
                                                    var resource = propertyOrder
                                                        .Aggregate(Activator.CreateInstance<TResource>(),
                                                            (aggr, property, index) =>
                                                            {
                                                                var value = row.Length > index ?
                                                                    row[index] : default(string);
                                                                TryCastFromXlsSerialization(property, value,
                                                                    (valueCasted) =>
                                                                    {
                                                                        property.SetValue(aggr, valueCasted);
                                                                        return true;
                                                                    },
                                                                    () => false);
                                                                return aggr;
                                                            });
                                                    return resource;
                                                })
                                            .ToArray();
                                        return next(sheet.Name.PairWithValue(resources));
                                    },
                                    (KeyValuePair<string, TResource[]>[] resourceLists) =>
                                    {
                                        return execute(customValues, resourceLists);
                                    });
                            return resourceList;
                        });
                    return x;
                });
            return result;
        }

        public static async Task<TResult> ParseXlsxAsync<TResource, TResult>(this HttpRequestMessage request,
                Stream xlsx,
                Func<TResource, KeyValuePair<string, string>[], Task<HttpResponseMessage>> executePost,
                Func<TResource, KeyValuePair<string, string>[], Task<HttpResponseMessage>> executePut,
            Func<HttpResponseMessage[], TResult> onComplete)
            where TResource : ResourceBase
        {
            var result = await OpenXmlWorkbook.Read(xlsx,

            async (workbook) =>
            {
                return await workbook.ReadCustomValues(
                    async (customValues) =>
                    {
                        var propertyOrder = typeof(TResource).GetProperties();
                        var x = await workbook.ReadSheets()
                            .Select(
                                sheet =>
                                {
                                    var rows = sheet
                                        .ReadRows()
                                        .ToArray();
                                    if (!rows.Any())
                                        return (new HttpResponseMessage[] { }).ToTask();

                                    return rows
                                        .Skip(1)
                                        .Select(
                                            row =>
                                            {
                                                var resource = propertyOrder
                                                    .Aggregate(Activator.CreateInstance<TResource>(),
                                                        (aggr, property, index) =>
                                                        {
                                                            var value = row.Length > index ?
                                                                row[index] : default(string);
                                                            TryCastFromXlsSerialization(property, value,
                                                                (valueCasted) =>
                                                                {
                                                                    property.SetValue(aggr, valueCasted);
                                                                    return true;
                                                                },
                                                                () => false);
                                                            return aggr;
                                                        });
                                                if (resource.Id.IsEmpty())
                                                {
                                                    resource.Id = Guid.NewGuid();
                                                    return executePost(resource, customValues);
                                                }
                                                return executePut(resource, customValues);
                                            })
                                        .WhenAllAsync(10);
                                })
                           .WhenAllAsync()
                           .SelectManyAsync()
                           .ToArrayAsync();
                        return onComplete(x);
                    });
            });
            return result;
        }

        #endregion

        public static HttpResponseMessage CreateImageResponse(this HttpRequestMessage request, byte[] imageData,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?),
            string filename = default(string), string contentType = default(string))
        {
            if (width.HasValue || height.HasValue || fill.HasValue)
            {
                var image = System.Drawing.Image.FromStream(new MemoryStream(imageData));
                return request.CreateImageResponse(image, width, height, fill, filename);
            }
            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Content = new ByteArrayContent(imageData);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(String.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType);
            return response;
        }

        public static HttpResponseMessage CreateImageResponse(this HttpRequestMessage request, Image image,
            int? width = default(int?), int? height = default(int?), bool? fill = default(bool?),
            string filename = default(string))
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
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

            var newImage = new Bitmap(newWidth, newHeight);

            //set the new resolution
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            //start the resizing
            using (var graphics = Graphics.FromImage(newImage))
            {
                //set some encoding specs
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            var encoder = getEncoderInfo("image/jpeg");
            response.Content = new PushStreamContent(
                async (outputStream, httpContent, transportContext) =>
                {
                    var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 80L);

                    newImage.Save(outputStream, encoder, encoderParameters);
                    outputStream.Close();
                }, new MediaTypeHeaderValue(encoder.MimeType));
            return response;
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

        public static HttpResponseMessage CreateResponsesBackground(this HttpRequestMessage request,
                System.Web.Http.Routing.UrlHelper urlHelper,
            Func<Func<HttpResponseMessage, BackgroundProgressController.Process>, Task<BackgroundProgressController.Process[]>> callback,
            int? estimatedProcessLength = default(int?))
        {
            var processId = BackgroundProgressController.CreateProcess(callback, estimatedProcessLength);
            var response = request.CreateResponse(HttpStatusCode.Accepted);
            response.Headers.Add("Access-Control-Expose-Headers", "x-backgroundprocess");
            response.Headers.Add("x-backgroundprocess", urlHelper.GetLocation<BackgroundProgressController>(processId).AbsoluteUri);
            return response;
        }

        public static HttpResponseMessage CreateResponseBackground(this HttpRequestMessage request,
                System.Web.Http.Routing.UrlHelper urlHelper,
            Func<Action<double>, Task<HttpResponseMessage>> callback,
            double? estimatedProcessLength = default(double?))
        {
            var processId = BackgroundProgressController.CreateProcess(callback, estimatedProcessLength);
            var response = request.CreateResponse(HttpStatusCode.Accepted);
            response.Headers.Add("Access-Control-Expose-Headers", "x-backgroundprocess");
            response.Headers.Add("x-backgroundprocess", urlHelper.GetLocation<BackgroundProgressController>(processId).AbsoluteUri);
            return response;
        }

        public static HttpResponseMessage CreateResponseVideoStream(this HttpRequestMessage request,
            byte [] video, string contentType)
        {
            var response = request.CreateResponse(HttpStatusCode.PartialContent);
            var ranges =
                (
                    request.Headers.Range.IsDefaultOrNull() ?
                        default(RangeItemHeaderValue[])
                        :
                        request.Headers.Range.Ranges
                )
                .NullToEmpty();
            if (!ranges.Any())
                ranges = new RangeItemHeaderValue[]
                    {
                        new RangeItemHeaderValue(0, video.LongLength-1)
                    };

            response.Content = new PushStreamContent(
                async (outputStream, httpContent, transportContext) =>
                {
                    try
                    {
                        foreach (var range in ranges)
                        {
                            if (!range.From.HasValue)
                                continue;
                            var length = range.To.HasValue ?
                                (range.To.Value - range.From.Value)
                                :
                                (video.LongLength - range.From.Value);
                            await outputStream.WriteAsync(video, (int)range.From.Value, (int)length);
                        }
                    }
                    catch (System.Web.HttpException ex)
                    {
                        return;
                    }
                    finally
                    {
                        outputStream.Close();
                    }
                }, new MediaTypeHeaderValue(contentType));
            response.Headers.AcceptRanges.Add("bytes");
            response.Headers.CacheControl = new CacheControlHeaderValue() { MaxAge = TimeSpan.FromSeconds(10368000), };
            response.Content.Headers.ContentLength = video.LongLength;
            var rangeFirst = ranges.First();
            var to = rangeFirst.To.HasValue ?
                rangeFirst.To.Value
                :
                video.LongLength;
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(rangeFirst.From.Value, to, video.Length);
            return response;
        }

        public static HttpResponseMessage CreateHtmlResponse(this HttpRequestMessage request, string html)
        {
            var response = request.CreateResponse(HttpStatusCode.OK);
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(html);
            writer.Flush();
            stream.Position = 0;
            response.Content = new StreamContent(stream);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }

        public static HttpResponseMessage CreateRedirectResponse<TController>(this HttpRequestMessage request, System.Web.Http.Routing.UrlHelper url,
            string routeName = null)
        {
            var location = url.GetLocation<TController>(routeName);
            return request.CreateRedirectResponse(location);
        }

        public static HttpResponseMessage CreateRedirectResponse(this HttpRequestMessage request, Uri location,
            string routeName = null)
        {
            var response = request
                        .CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Location = location;
            return response;
        }

        public static HttpResponseMessage CreateAlreadyExistsResponse<TController>(this HttpRequestMessage request, Guid existingResourceId, System.Web.Http.Routing.UrlHelper url,
            string routeName = null)
        {
            var location = url.GetLocation<TController>(existingResourceId, routeName);
            var reason = $"There is already a resource with ID = [{existingResourceId}]";
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict)
                        .AddReason(reason);
            response.Headers.Location = location;
            return response;
        }

        public static HttpResponseMessage CreateAlreadyExistsResponse(this HttpRequestMessage request, Type controllerType, Guid existingResourceId, System.Web.Http.Routing.UrlHelper url,
            string routeName = null)
        {
            var location = url.GetLocation(controllerType, existingResourceId, routeName);
            var reason = $"There is already a resource with ID = [{existingResourceId}]";
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict)
                        .AddReason(reason);
            response.Headers.Location = location;
            return response;
        }

        public static HttpResponseMessage CreateResponseNotFound(this HttpRequestMessage request, Guid resourceId)
        {
            var reason = $"The resource with ID = [{resourceId}] was not found";
            var response = request
                .CreateResponse(HttpStatusCode.NotFound)
                .AddReason(reason);
            return response;
        }

        public static HttpResponseMessage CreateResponseConfiguration(this HttpRequestMessage request, string configParameterName, string why)
        {
            var response = request
                .CreateResponse(HttpStatusCode.ServiceUnavailable)
                .AddReason(why);
            return response;
        }
        
        public static HttpResponseMessage CreateResponseUnexpectedFailure(this HttpRequestMessage request, string why)
        {
            var response = request
                .CreateResponse(HttpStatusCode.InternalServerError)
                .AddReason(why);
            return response;
        }

        public static HttpResponseMessage CreateResponseEmptyId<TQuery, TProperty>(this HttpRequestMessage request,
            TQuery query, Expression<Func<TQuery, TProperty>> propertyFailing)
        {
            var value = string.Empty;
            var reason = $"Property [{propertyFailing}] must have value.";
            var response = request
                .CreateResponse(HttpStatusCode.BadRequest)
                .AddReason(reason);
            return response;
        }

        public static HttpResponseMessage CreateResponseValidationFailure<TQuery, TProperty>(this HttpRequestMessage request, 
            TQuery query, Expression<Func<TQuery, TProperty>> propertyFailing)
        {
            var value = string.Empty;
            try
            {
                value = propertyFailing.Compile().Invoke(query).ToString();
            } catch (Exception)
            {

            }
            var reason = $"Property [{propertyFailing}] Value = [{value}] is not valid";
            var response = request
                .CreateResponse(HttpStatusCode.BadRequest)
                .AddReason(reason);
            return response;
        }

        /// <summary>
        /// The resource could not be created or updated due to a link to a resource that no longer exists.
        /// </summary>
        /// <typeparam name="TController"></typeparam>
        /// <param name="request"></param>
        /// <param name="brokenResourceId"></param>
        /// <param name="url"></param>
        /// <param name="routeName"></param>
        /// <returns></returns>
        public static HttpResponseMessage CreateBrokenReferenceResponse<TController>(this HttpRequestMessage request, Guid? brokenResourceId, System.Web.Http.Routing.UrlHelper url,
            string routeName = null)
        {
            var reference = url.GetWebId<TController>(brokenResourceId, routeName);
            var reason = $"The resource with ID = [{brokenResourceId}] at [{reference.Source}] is not available";
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict, reference)
                        .AddReason(reason);
            return response;
        }

        /// <summary>
        /// A query parameter makes reference to a secondary resource that does not exist.
        /// </summary>
        /// <example>/Foo?bar=ABC where the resource referenced by parameter bar does not exist</example>
        /// <typeparam name="TController">The Controller that represents the parameter linked resource</typeparam>
        /// <param name="request"></param>
        /// <param name="brokenResourceProperty"></param>
        /// <param name="url"></param>
        /// <param name="routeName"></param>
        /// <returns></returns>
        public static HttpResponseMessage[] CreateLinkedDocumentNotFoundResponse<TController, TQueryResource>(this HttpRequestMessage request,
            TQueryResource query,
            Expression<Func<TQueryResource, WebIdQuery>> brokenResourceProperty,
            System.Web.Http.Routing.UrlHelper url,
            string routeName = null)
        {
            var reference = default(WebIdQuery);
            try
            {
                reference = brokenResourceProperty.Compile().Invoke(query);
            } catch(Exception)
            {

            }

            var reason = reference.IsDefault()?
                $"The referenced [{typeof(TController).Name}] resource is not found"
                :
                $"The resource with ID = [{reference.UUIDs}] at [{reference.Source}] is not available";
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict, reference)
                        .AddReason(reason);
            return response.AsEnumerable().ToArray();
        }

        public static HttpResponseMessage[] CreateLinkedDocumentNotFoundResponse<TController, TQueryResource>(this HttpRequestMessage request,
            Guid value,
            Expression<Func<TQueryResource, WebIdQuery>> brokenResourceProperty,
            System.Web.Http.Routing.UrlHelper url,
            string routeName = null)
        {
            var reason = brokenResourceProperty.PropertyName(
                propertyName => $"The referenced [{typeof(TController).Name}] resource [Property({propertyName}) = {value}] is not found",
                () => $"The referenced {typeof(TController).Name} resource = [{value}] is not found");
            var response = request
                        .CreateResponse(HttpStatusCode.Conflict)
                        .AddReason(reason);
            return response.AsEnumerable().ToArray();
        }

        /// <summary>
        /// WARNING: This isn't really baked
        /// </summary>
        /// <param name="viewName"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public static HttpResponseMessage CreateResponseHtml(string viewName, dynamic model)
        {
            // var view = File.ReadAllText(Path.Combine(viewDirectory, viewName + ".cshtml"));
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            var template = new RazorEngine.Templating.NameOnlyTemplateKey(viewName, RazorEngine.Templating.ResolveType.Global, null);
            var parsedView = RazorEngine.Engine.Razor.Run(template, model.GetType(), model);
            response.Content = new StringContent(parsedView);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }
    }
}