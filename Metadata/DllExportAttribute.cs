using System;

namespace HuajiTech.UnmanagedExports
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class DllExportAttribute : Attribute
    {
        public string EntryPoint { get; set; }
    }
}
