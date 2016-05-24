using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [DataContract]
    public class TLVector<T> : TLObject, IList<T> 
        where T : TLObject
    {
        public const uint Signature = TLConstructors.TLVector;        

        [DataMember]
        public IList<T> Items { get; set; }

        public TLVector()
        {
            Items = new List<T>();        
        }

        public TLVector(int count)
        {
            Items = new List<T>(count);
        }

        public TLVector(IList<T> items)
        {
            Items = items;
        } 

        public T this[int index]
        {
            get { return Items[index]; }
            set { Items[index] = value; }
        }

        public int IndexOf(T item)
        {
            return Items.IndexOf(item);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            var length = GetObject<TLInt>(bytes, ref position);
            Items = new List<T>(length.Value);

            for (var i = 0; i < length.Value; i++)
            {
                Items.Add(GetObject<T>(bytes, ref position));
            }

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                BitConverter.GetBytes(Items.Count),
                TLUtils.Combine(Items.Select(x => x.ToBytes()).ToArray())
                );
        }

        public override TLObject FromStream(Stream input)
        {
            var length = GetObject<TLInt>(input);
            Items = new List<T>(length.Value);

            try
            {
                for (var i = 0; i < length.Value; i++)
                {
                    Items.Add(GetObject<T>(input));
                }
            }
            catch (Exception ex)
            {
                
            }

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(new TLInt(Items.Count).ToBytes());
            for (var i = 0; i < Items.Count; i++)
            {
                Items[i].ToStream(output);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            Items.Add(item);
        }

        public void Clear()
        {
           Items.Clear();
        }

        public bool Contains(T item)
        {
            return Items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Items.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            return Items.Remove(item);
        }

        public void RemoveAt(int index)
        {
            Items.RemoveAt(index);
        }

        public void Insert(int index, T item)
        {
            Items.Insert(index, item);
        }

        public int Count { get { return Items.Count; } }
        public bool IsReadOnly { get { return Items.IsReadOnly; } }

        public int IndexOf(TLDCOption dcOption)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i] == dcOption)
                    return i;
            }

            return -1;
        }
    }
}
