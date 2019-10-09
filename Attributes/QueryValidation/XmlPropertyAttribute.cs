﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using EastFive.Extensions;
using System.Xml;
using EastFive.Linq;
using BlackBarLabs.Extensions;
using System.Xml.Serialization;
using EastFive.Serialization;
using EastFive.Api.Serialization;
using System.IO;
using EastFive.Collections.Generic;

namespace EastFive.Api
{
    public class XmlPropertyAttribute : PropertyAttribute, IBindApiValue, IBindXmlApiValue
    {
        public string NSPrefix { get; set; }

        public string NSUri { get; set; }

        public async Task<TResult> ParseContentDelegateAsync<TResult>(XmlDocument xmlDoc,
                ParameterInfo parameterInfo, IApplication httpApp, HttpRequestMessage request,
            Func<object, TResult> onParsed,
            Func<string, TResult> onFailure)
        {
            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(XmlDocument)))
                return onParsed(xmlDoc);

            var mgr = new XmlNamespaceManager(xmlDoc.NameTable);
            if(NSPrefix.HasBlackSpace())
                mgr.AddNamespace(NSPrefix, NSUri);
            var key = this.GetKey(parameterInfo);
            var node = xmlDoc.SelectSingleNode(key, mgr);

            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(XmlNode)))
                return onParsed(node);

            if (parameterInfo.ParameterType.IsAssignableFrom(typeof(XmlNode[])))
            {
                if (node.IsDefaultOrNull())
                    return onParsed(new XmlNode[] { });
                return onParsed(
                    XmlContent
                        .Enumerate(node.ChildNodes)
                        .AsArray());
            }

            return httpApp.Bind(parameterInfo.ParameterType,
                new XmlContent(node),
                onParsed,
                onFailure);
        }

        public RequestMessage<TResource> BindContent<TResource>(RequestMessage<TResource> request,
            MethodInfo method, ParameterInfo parameter, object contentObject)
        {
            var contentJsonString = JsonConvert.SerializeObject(contentObject, new Serialization.Converter());
            var stream = contentJsonString.ToStream();
            var content = new StreamContent(stream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/xml");
            return request.SetContent(content);
        }

        private class XmlContent : IParseToken
        {
            private XmlNode node;

            public XmlContent(XmlNode node)
            {
                this.node = node;
            }

            public bool IsString => !node.HasChildNodes;

            public IParseToken[] ReadArray()
            {
                return Enumerate(node.ChildNodes)
                    .Select(node => new XmlContent(node))
                    .ToArray();
            }

            public static IEnumerable<XmlNode> Enumerate(XmlNodeList list)
            {
                foreach (var node in list)
                    yield return (XmlNode)node;
            }

            public byte[] ReadBytes()
            {
                throw new NotImplementedException();
            }

            public IDictionary<string, IParseToken> ReadDictionary()
            {
                return Enumerate(this.node.ChildNodes)
                    .Select(node => node.Name.PairWithValue<string, IParseToken>(new XmlContent(node.FirstChild)))
                    .ToDictionary();
            }

            public T ReadObject<T>()
            {
                var serializer = new XmlSerializer(typeof(T));
                return (T)serializer.Deserialize(new XmlNodeReader(node));
            }

            public object ReadObject()
            {
                throw new NotImplementedException();
            }

            public Stream ReadStream()
            {
                throw new NotImplementedException();
            }

            public string ReadString()
            {
                return node.InnerText;
            }
        }
    }
}
