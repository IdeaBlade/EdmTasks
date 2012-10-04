using System;
using System.Data.Entity;
using System.Data.Entity.Design;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace EdmTasks
{
    /// <summary>
    /// MSBuild task to create Entity Framework Pre-Generated Views from a DbContext.
    /// </summary>
    public class ViewRefreshTask : AppDomainIsolatedTask
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
        /// Optional.  Language to write the pregenerated views file.  Must be "cs" or "vb".  Default is "cs".
        /// </summary>
        public string Lang { get; set; }

        public string Inputs { get; set; }

        /// <summary>
        /// Optional.  If supplied, passes the connection string to the context.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Create a pre-generated views file from the DbContext class in the assembly.
        /// Name of views file is the name of the DbContext with ".Views" added to the name.
        /// </summary>
        /// <returns>
        /// true if the task successfully executed; otherwise, false.
        /// </returns>
        public override bool Execute()
        {
            var startTime = DateTime.Now;
            string suffix = string.IsNullOrEmpty(DbContext) ? "DbContext" : DbContext;
            var lang = string.IsNullOrEmpty(Lang) ? "cs" : Lang;
            Log.LogMessage("ViewRefreshTask: Assembly={0}, DbContext={1}, Lang={2}, ConnectionString={3}", Assembly.ItemSpec, suffix, lang, ConnectionString);
            LanguageOption langOpt;
            if (!ViewGenerator.ParseLanguageOption(lang, out langOpt))
            {
                Log.LogError("Lang parameter invalid.  Must be \"cs\" or \"vb\", was {0}", lang);
                return false;
            }

            // Generate the EDMX as a string
            var eg = new EdmxGenerator(Log);
            DbContext dbContext = eg.GetDbContext(Assembly.ItemSpec, suffix, this.ConnectionString);
            if (dbContext == null) return false;

            var edmx = eg.WriteEdmxString(dbContext);
            if (edmx == null) return false;

            // Get the views file name
            string viewsFileName = dbContext.GetType().Name + ".Views" +
                               (langOpt == LanguageOption.GenerateCSharpCode ? ".cs" : ".vb");

            // Generate a new views file if the old does not have the current hash
            var vg = new ViewGenerator(Log);
            var oldHash = vg.GetHashFromViewsFile(viewsFileName);
            var newHash = vg.CreateHash(edmx);
            bool result;
            if (newHash.Equals(oldHash))
            {
                Log.LogMessage("Views are already current.  Touching file.");
                System.IO.File.SetLastWriteTimeUtc(viewsFileName, DateTime.UtcNow);
                result = true;
            }
            else
            {
                Log.LogMessage("Writing views to {0}", viewsFileName);
                result = vg.WriteViewsFile(viewsFileName, edmx, newHash, langOpt);
            }
            var timeSpan = DateTime.Now.Subtract(startTime);
            Log.LogMessage("ViewRefreshTask done.  Elapsed time={0}", timeSpan);
            return result;
        }
    }
}
