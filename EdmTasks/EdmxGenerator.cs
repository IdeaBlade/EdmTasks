using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using Microsoft.Build.Utilities;

namespace EdmTasks
{
    /// <summary>
    /// Class for generating an EDMX file from a DbContext.
    /// </summary>
    class EdmxGenerator
    {
        private TaskLoggingHelper Log;

        public EdmxGenerator(TaskLoggingHelper log)
        {
            this.Log = log;
        }
        #region Writing an EDMX

        /// <summary>
        /// Generate the Edmx file from the given parameters
        /// </summary>
        /// <param name="assemblyName">Name of the assembly containing the DbContext(s)</param>
        /// <param name="suffix">String to use to find a specific DbContext class by name</param>
        /// <param name="filename">Name of file to write the EDMX</param>
        /// <returns>true if generation was successful, false if not.</returns>
        public bool GenerateEdmx(string assemblyName, string suffix, string filename)
        {
            DbContext dbContext = GetDbContext(assemblyName, suffix);
            if (dbContext == null) return false;
            filename = string.IsNullOrEmpty(filename) ? dbContext.GetType().Name + ".edmx" : filename;

            return WriteEdmx(dbContext, filename);
        }

        /// <summary>
        /// Create an EDMX from the given DbContext and write the XML to the given file.
        /// </summary>
        /// <returns>true if file was written successfully, false if there was an error.</returns>
        private bool WriteEdmx(DbContext dbContext, String filepath)
        {
            try
            {
                Log.LogMessage("Writing Edmx to {0}", filepath);
                var xmlWriter = new XmlTextWriter(filepath, Encoding.Default);

                EdmxWriter.WriteEdmx(dbContext, xmlWriter);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("Unable to write EDMX file {0}: {1}", filepath, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Create an EDMX from the given DbContext and write the XML to a string.
        /// </summary>
        /// <returns>string containing XML if successful, null if there was an error.</returns>
        public string WriteEdmxString(DbContext dbContext)
        {
            try
            {
                Log.LogMessage("Writing Edmx to string");
                var sb = new StringBuilder(512 * 1024);
                var sw = new StringWriter(sb);
                var xmlStringer = new XmlTextWriter(sw);
                EdmxWriter.WriteEdmx(dbContext, xmlStringer);

                var xml = sb.ToString();
                Log.LogMessage("Wrote {0} characters", xml.Length);
                return xml;
            }
            catch (Exception ex)
            {
                Log.LogError("Unable to write EDMX: {0}", ex.Message);
                return null;
            }
        }
        #endregion

        #region Creating a DbContext instance from an Assembly

        /// <summary>
        /// Get the first DbContext that we are able to instantiate from the assembly.
        /// </summary>
        /// <param name="assemblyName">Assembly search for DbContext(s)</param>
        /// <param name="suffix">Suffix that should match the class name, e.g. "DbContext".  Ignored if null or empty.</param>
        /// <returns>the DbContext, or null if none was found in the assembly.</returns>
        public DbContext GetDbContext(string assemblyName, string suffix)
        {
            var assembly = GetAssembly(assemblyName);
            if (assembly == null) return null;
            Log.LogMessage("Resolved Assembly={0}", assembly.GetName());

            var dbcTypes = assembly.GetTypes().Where(t => typeof(DbContext).IsAssignableFrom(t));
            if (!string.IsNullOrEmpty(suffix))
            {
                dbcTypes = dbcTypes.Where(t => t.FullName.EndsWith(suffix));
            }
            DbContext dbContext = null;
            foreach (var dbcType in dbcTypes)
            {
                dbContext = Activate(dbcType);
                if (dbContext != null) break;
            }
            if (dbContext == null)
                Log.LogMessage("No DbContext could be created.");
            return dbContext;
        }

        /// <summary>
        /// Create an instance of the given DbContext subclass.
        /// </summary>
        /// <returns>The DbContext instance, or null if there was an error.</returns>
        private DbContext Activate(Type dbcType)
        {
            try
            {
                var dbContext = (DbContext)Activator.CreateInstance(dbcType);
                Log.LogMessage("DbContext type={0}", dbcType.FullName);
                return dbContext;
            }
            catch (Exception ex)
            {
                Log.LogError("Unable to create instance of type {0}: {1}", dbcType.FullName, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Load the assembly for the given file name.
        /// </summary>
        /// <param name="asmFileName"></param>
        /// <returns>The assembly, or null if there was an error.</returns>
        private Assembly GetAssembly(string asmFileName)
        {
            if (!File.Exists(asmFileName))
            {
                Log.LogError("Assembly '{0}' not found.  If this assembly contains a DbContext, be sure to build it first.", asmFileName);
                return null;
            }

            Assembly assembly = null;
            try
            {
                var name = AssemblyName.GetAssemblyName(asmFileName);
                assembly = Assembly.Load(name);
            }
            catch (Exception ex)
            {
                Log.LogError("Error loading {0}: {1}", asmFileName, ex.Message);
                return null;
            }

            return assembly;
        }
        #endregion

    }
}
