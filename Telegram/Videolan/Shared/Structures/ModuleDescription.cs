using System;
using System.Runtime.InteropServices;

namespace LibVLCSharp.Shared.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ModuleDescriptionStructure
    {
        internal readonly IntPtr Name;
        internal readonly IntPtr ShortName;
        internal readonly IntPtr LongName;
        internal readonly IntPtr Help;
        internal readonly IntPtr Next;
    }

    /// <summary>
    /// Description of a module.
    /// </summary>
    public readonly struct ModuleDescription
    {
        /// <summary>
        /// Default constructor for ModuleDescription
        /// </summary>
        /// <param name="name">Module name</param>
        /// <param name="shortName">Module short name</param>
        /// <param name="longName">Module long name</param>
        /// <param name="help">Module help</param>
        internal ModuleDescription(string name, string shortName, string longName, string help)
        {
            Name = name;
            ShortName = shortName;
            LongName = longName;
            Help = help;
        }

        /// <summary>
        /// Module name
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Module short name
        /// </summary>
        public readonly string ShortName;

        /// <summary>
        /// Module long name
        /// </summary>
        public readonly string LongName;

        /// <summary>
        /// Module help
        /// </summary>
        public readonly string Help;
    }
}
