using EastFive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Api
{

    [AttributeUsage(AttributeTargets.Property)]
    public class SheetColumnAttribute : System.Attribute
    {
        public SheetColumnAttribute()
        {
        }

        public string ColumnName { get; set; }

        /// <summary>
        /// By default columns are sorted alphabetically by columns name.
        /// SortAs will cause the column to by sorted by this value instead of the column name.
        /// </summary>
        public string SortAs { get; set; }

        public string GetSortValue(PropertyInfo propertyInfo)
        {
            return SortAs.IsNullOrWhiteSpace() ?
                ColumnName.IsNullOrWhiteSpace() ?
                    propertyInfo.Name
                    :
                    ColumnName
                :
                SortAs;
        }
    }
}
