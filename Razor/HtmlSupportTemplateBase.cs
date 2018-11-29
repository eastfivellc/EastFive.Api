﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Razor
{
    public class HtmlSupportTemplateBase<T> : RazorEngine.Templating.TemplateBase<T>
    {
        public HtmlSupportTemplateBase()
        {
            Html = new RazorHtmlHelper();
            HttpUtility = new HttpUtilityHelper();
            Url = new RazorUrlHelper();
        }

        public RazorHtmlHelper Html { get; set; }
        public HttpUtilityHelper HttpUtility { get; set; }
        public RazorUrlHelper Url { get; set; }
        
        public class RazorUrlHelper
        {
            public RazorEngine.Text.IEncodedString Content(string htmlText)
            {
                return new RazorEngine.Text.RawString(htmlText);
            }

            public string Encode(string value)
            {
                return System.Net.WebUtility.HtmlEncode(value);
            }

            public string Encode(object value)
            {
                return "do whatever";
            }
        }

        public class HttpUtilityHelper
        {
            public RazorEngine.Text.IEncodedString HtmlDecode(string htmlText)
            {
                return new RazorEngine.Text.RawString(htmlText);
            }

            public string Encode(string value)
            {
                return System.Net.WebUtility.HtmlEncode(value);
            }

            public string Encode(object value)
            {
                return "do whatever";
            }
        }

        public class RazorHtmlHelper
        {
            /// <summary>
            /// Instructs razor to render a string without applying html encoding.
            /// </summary>
            /// <param name="htmlString"></param>
            /// <returns></returns>
            public RazorEngine.Text.IEncodedString Raw(RazorEngine.Text.IEncodedString htmlString)
            {
                return new RazorEngine.Text.RawString(htmlString.ToEncodedString());
            }

            public string Encode(string value)
            {
                return System.Net.WebUtility.HtmlEncode(value);
            }

            public string Encode(object value)
            {
                return "do whatever";
            }
        }
    }
}
