using System;
using System.Collections.Generic;
using System.Data.Entity.Design;
using System.Data.Metadata.Edm;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace EdmTasks
{
    /// <summary>
    /// MSBuild task to create Entity Framework Pre-Generated Views from an EDMX file.
    /// </summary>
    public class ViewGenTask : AppDomainIsolatedTask
    {
        /// <summary>
        /// Name of the EDMX file.  Path is relative to project directory.
        /// </summary>
        [Required]
        public string EdmxFile { get; set; }

        /// <summary>
        /// Optional.  Language to write the pregenerated views file.  Must be "cs" or "vb".  Default is "cs".
        /// </summary>
        public string Lang { get; set; }

        /// <summary>
        /// Create the pre-generated views file from the edmx file.
        /// </summary>
        /// <returns>
        /// true if the task successfully executed; otherwise, false.
        /// </returns>
        public override bool Execute()
        {
            var lang = string.IsNullOrEmpty(Lang) ? "cs" : Lang;
            Log.LogMessage("ViewGenTask: EdmxFile={0}, Lang={1}", EdmxFile, lang);

            FileInfo edmxInfo = null;
            LanguageOption langOpt;
            if (!ViewGenerator.ParseLanguageOption(lang, out langOpt))
            {
                Log.LogError("Lang parameter invalid.  Must be \"cs\" or \"vb\", was {0}", lang);
                return false;
            }
            if (!ViewGenerator.ParseFileArguments(EdmxFile, out edmxInfo))
            {
                Log.LogError("EdmxFile parameter invalid.  Must be a valid EDMX file, was {0}", edmxInfo.FullName);
                return false;
            }

            var result = true;
            try
            {
                var errors = ViewGenerator.GenerateViewsFromEdmx(edmxInfo, langOpt);
                if (errors.Any())
                {
                    var severe = LogErrors(errors);
                    result = (!severe);
                }
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                result = false;
            }
            Log.LogMessage("ViewGenTask complete.");
            return result;
        }

        /// <summary>
        /// Log the errors.  Return true if any were Error severity, False if there were only warnings.
        /// </summary>
        /// <param name="errors"></param>
        /// <returns></returns>
        private bool LogErrors(IEnumerable<EdmSchemaError> errors)
        {
            if (errors == null) return false;
            bool severe = false;
            foreach (var e in errors)
            {
                if (e.Severity == EdmSchemaErrorSeverity.Error)
                {
                    Log.LogError(e.ToString());
                    severe = true;
                }
                else
                {
                    Log.LogWarning(e.ToString());
                }
            }
            return severe;
        }
    }
}
