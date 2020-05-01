using System;

namespace Unigram.Services.SettingsLegacy
{
    public interface IStoreConverter
    {
        string ToStore(object value, Type type);
        object FromStore(string value, Type type);
    }
}