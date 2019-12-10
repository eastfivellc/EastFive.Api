using BlackBarLabs.Extensions;
using EastFive.Api.Resources;
using EastFive.Extensions;
using EastFive.Linq.Expressions;
using EastFive.Reflection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ApiPropertyAttribute : System.Attribute, IProvideApiValue, IDocumentProperty
    {
        public ApiPropertyAttribute()
        {
        }

        public string PropertyName { get; set; }

        public Property GetProperty(MemberInfo member, HttpApplication httpApp)
        {
            string GetName()
            {
                if (this.PropertyName.HasBlackSpace())
                    return this.PropertyName;
                return member.GetCustomAttribute<JsonPropertyAttribute, string>(
                    (attr) => attr.PropertyName.HasBlackSpace() ? attr.PropertyName : member.Name,
                    () => member.Name);
            }
            string GetDescription()
            {
                return member.GetCustomAttribute<System.ComponentModel.DescriptionAttribute, string>(
                    (attr) => attr.Description,
                    () => string.Empty);
            }
            string GetType()
            {
                var type = member.GetPropertyOrFieldType();
                return Parameter.GetTypeName(type, httpApp);
            }
            var name = GetName();
            var description = GetDescription();
            var options = new KeyValuePair<string, string>[] { };
            return new Property()
            {
                IsIdentfier = member.ContainsAttributeInterface<IIdentifyResource>(),
                IsTitle = member.ContainsAttributeInterface<ITitleResource>(),
                Name = name,
                Description = description,
                Options = options,
                Type = GetType(),
            };
        }
    }
}
