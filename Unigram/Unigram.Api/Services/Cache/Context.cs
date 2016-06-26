﻿using System;
using System.Collections.Generic;

namespace Telegram.Api.Services.Cache
{
    public class Context<T> : Dictionary<long, T>
    {
        public Context()
        {
            
        }

        public Context(IEnumerable<T> items, Func<T, long> keyFunc)
        {
            foreach (var item in items)
            {
                this[keyFunc(item)] = item;
            }
        }

        public new T this[long index]
        {
            get
            {
                if (ContainsKey(index))
                {
                    return base[index];
                }

                return default(T);

                //try
                //{
                //    return base[index];
                //}
                //catch (Exception e)
                //{
                //    return default(T);
                //}
            }

            set
            {
                base[index] = value;
            }
        }
    }
}