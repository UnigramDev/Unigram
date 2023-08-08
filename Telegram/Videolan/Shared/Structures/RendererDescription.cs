using System;

namespace LibVLCSharp.Shared
{
    internal readonly struct RendererDescriptionStructure
    {
        internal RendererDescriptionStructure(IntPtr name, IntPtr longName)
        {
            Name = name;
            LongName = longName;
        }

        internal readonly IntPtr Name;
        internal readonly IntPtr LongName;
    }

    /// <summary>
    /// Renderer description
    /// </summary>
    public readonly struct RendererDescription
    {
        /// <summary>
        /// Renderer Name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Renderer long name
        /// </summary>
        public string LongName { get; }

        internal RendererDescription(string name, string longName)
        {
            Name = name;
            LongName = longName;
        }
    }
}
