using System;

namespace Unigram.Services.SettingsLegacy
{
    public interface IPropertyMapping
    {
        IStoreConverter GetConverter(Type type);
    }
}