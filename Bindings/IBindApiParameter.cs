using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api
{
    public interface IBindApiParameter<TProvider>
    {
        TResult Bind<TResult>(Type type, TProvider content,
            Func<object, TResult> onParsed,
            Func<string, TResult> onDidNotBind,
            Func<string, TResult> onBindingFailure);
    }
}
