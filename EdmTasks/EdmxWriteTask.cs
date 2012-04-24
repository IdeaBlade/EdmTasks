using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace EdmTasks
{
    /// <summary>
    /// MSBuild task to create an EDMX file from an Entity Framework DbContext implementation.
    /// </summary>
    public class EdmxWriteTask : AppDomainIsolatedTask
    {
        /// <summary>
        /// Full file name to assembly which should contain a DbContext.
        /// </summary>
        [Required]
        public ITaskItem Assembly { get; set; }

        /// <summary>
        /// Optional.  Name of the DbContext implementation class.  If omitted, the first DbContext found in the Assembly will be used.
        /// </summary>
        public string DbContext { get; set; }

        /// <summary>
        /// Optional.  Name of the file to write the EDMX.  Default is the name of the DbContext + ".edmx".  Path is relative to project directory.
        /// </summary>
        public string EdmxFile { get; set; }

        /// <summary>
        /// Create an edmx file from the DbContext class in the assembly.
        /// </summary>
        /// <returns>
        /// true if the task successfully executed; otherwise, false.
        /// </returns>
        public override bool Execute()
        {
            Log.LogMessage("EdmxWriteTask: Assembly={0}, DbContext={1}, EdmxFile={2}", Assembly.ItemSpec, DbContext, EdmxFile);
            var g = new EdmxGenerator(Log);
            return g.GenerateEdmx(Assembly.ItemSpec, DbContext, EdmxFile);
        }

    }
}
