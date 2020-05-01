using System;

namespace Unigram.Services.SettingsLegacy
{
    public class JsonMapping : IPropertyMapping
    {
        protected IStoreConverter jsonConverter = new JsonConverter();
        public IStoreConverter GetConverter(Type type) => this.jsonConverter;
    }
}