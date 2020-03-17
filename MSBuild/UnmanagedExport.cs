using dnlib.DotNet;
using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Diagnostics;
using System.IO;

namespace HuajiTech.UnmanagedExports
{
    public class UnmanagedExport : Task
    {
        [Required]
        public ITaskItem File { get; set; }

        public override bool Execute()
        {
            var file = File.ItemSpec;

            if (file.Length == 0)
            {
                Log.LogError("Requires a file to export.");
                return false;
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                Export(file);
            }
            catch (Exception ex)
            {
                Log.LogError(ex.Message);
                return false;
            }

            stopwatch.Stop();

            Log.LogMessage(MessageImportance.High, "Export succeeded in {0}ms.", stopwatch.ElapsedMilliseconds);

            return true;
        }

        private void Export(string dllName)
        {
            using var fileStream = new FileStream(dllName, FileMode.Open, FileAccess.ReadWrite);
            using var cache = new MemoryStream();

            fileStream.CopyTo(cache);
            fileStream.Position = 0;

            var module = ModuleDefMD.Load(cache);

            Log.LogMessage(MessageImportance.High, "Loaded assembly \"{0}\".", module.Assembly.FullName);

            var exports = from type in module.Types
                          from method in type.Methods
                          where method.IsStatic
                          let attr = method.CustomAttributes.Find(typeof(DllExportAttribute).FullName)
                          where !(attr is null)
                          select new
                          {
                              Method = method,
                              Attribute = attr
                          };

            if (!exports.Any())
            {
                throw new InvalidOperationException("No methods were found.");
            }

            Log.LogMessage(MessageImportance.High, "Found {0} methods, exporting...", exports.Count());

            foreach (var export in exports)
            {
                var method = export.Method;
                var attr = export.Attribute;
                var entryPoint = attr.GetProperty(nameof(DllExportAttribute.EntryPoint))?.Value?.ToString();

                if (string.IsNullOrEmpty(entryPoint))
                {
                    Log.LogMessage("Exported method \"{0}\".", method.FullName);
                    method.ExportInfo = new MethodExportInfo();
                }
                else
                {
                    Log.LogMessage("Exported method \"{0}\" using custom entry point \"{1}\".", method.FullName, entryPoint);
                    method.ExportInfo = new MethodExportInfo(entryPoint);
                }

                method.MethodSig.RetType = new CModOptSig(
                    module.CorLibTypes.GetTypeRef(
                        "System.Runtime.CompilerServices",
                        "CallConvStdcall"
                    ),
                    method.MethodSig.RetType
                );

                method.CustomAttributes.Remove(attr);
            }

            module.IsILOnly = false;

            Log.LogMessage(MessageImportance.High, "Writing exported assembly...");
            module.Write(fileStream);
        }
    }
}
