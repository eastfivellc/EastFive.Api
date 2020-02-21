using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Serialization
{
    public interface IParseToken
    {
        bool IsString { get; }

        string ReadString();

        byte[] ReadBytes();

        Stream ReadStream();

        IParseToken[] ReadArray();

        IDictionary<string, IParseToken> ReadDictionary();

        T ReadObject<T>();

        object ReadObject();
    }
}
