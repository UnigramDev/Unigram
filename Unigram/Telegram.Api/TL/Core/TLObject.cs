using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract class TLObject
    {
        public virtual TLType TypeId
        {
            get
            {
                return TLType.None;
            }
        }

        public virtual void Read(TLBinaryReader from) { }

        public virtual void Write(TLBinaryWriter to) { }

        //public virtual void ReadFromCache(TLBinaryReader from) { }

        //public virtual void WriteToCache(TLBinaryWriter to) { }

        public byte[] ToArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var to = new TLBinaryWriter(stream))
                {
                    Write(to);
                    return stream.ToArray();
                }
            }
        }






        public virtual void RaisePropertyChanged([CallerMemberName] string propertyName = "") { }

        public void RaisePropertyChanged<TProperty>(Expression<Func<TProperty>> property)
        {
            RaisePropertyChanged(GetMemberInfo(property).Name);
        }

        private MemberInfo GetMemberInfo(Expression expression)
        {
            var lambdaExpression = (LambdaExpression)expression;
            return (!(lambdaExpression.Body is UnaryExpression) ? (MemberExpression)lambdaExpression.Body : (MemberExpression)((UnaryExpression)lambdaExpression.Body).Operand).Member;
        }
    }
}
