using System;
using System.Collections.Generic;
using System.Data.Entity.Design;
using System.Data.Mapping;
using System.Data.Metadata.Edm;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Utilities;

namespace EdmTasks
{
    /// <summary>
    /// Class for creating the pre-generated views from an EDMX file.
    /// </summary>
    class ViewGenerator
    {
        private TaskLoggingHelper Log;
        private const string HashPrefix = "// ViewGenHash=";

        public ViewGenerator(TaskLoggingHelper log)
        {
            this.Log = log;
        }

        #region Reading and writing the pre-generated views file

        /// <summary>
        /// Write the pre-generated views file.
        /// </summary>
        /// <param name="viewsFileName">Name of the file to create.  If it exists, it is overwritten.</param>
        /// <param name="edmx">String containing the EDMX XML</param>
        /// <param name="hash">Hash code to put at the top of the views file</param>
        /// <param name="languageOption">Language (cs or vb) in which to write the views file</param>
        /// <returns>true if the write was successful, false if not.</returns>
        public bool WriteViewsFile(string viewsFileName, string edmx, string hash, LanguageOption languageOption)
        {
            bool result = true;
            try
            {
                var writer = new StreamWriter(viewsFileName, false);
                writer.WriteLine(HashPrefix + hash);

                var errors = GenerateViewsFromEdmx(edmx, languageOption, writer);
                if (Enumerable.Any(errors))
                {
                    var severe = LogErrors(errors);
                    result = (!severe);
                }
                writer.Close();
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                result = false;
            }
            return result;
        }

        /// <summary>
        /// Generate a hash from the given string.
        /// </summary>
        /// <param name="data">Input data to hash</param>
        /// <returns>The hash as a Base64 encoded string</returns>
        public string CreateHash(string data)
        {
            var hasher = SHA256.Create();
            byte[] result = hasher.ComputeHash(Encoding.UTF8.GetBytes(data));
            var hash = Convert.ToBase64String(result);
            Log.LogMessage("Created hash {0}", hash);
            return hash;
        }

        /// <summary>
        /// If the pre-generated views file exists, return the hash stored in the file.
        /// </summary>
        /// <param name="filepath">Name of the pre-generated views file</param>
        /// <returns>The hash, if it was found in the file, empty string if file did not contain hash, null if file not found or other error.</returns>
        public string GetHashFromViewsFile(string filepath)
        {
            try
            {
                if (!File.Exists(filepath))
                {
                    Log.LogMessage("Views File {0} does not exist.", filepath);
                    return null;
                }

                // Get the first line of the file
                string line = File.ReadLines(filepath).First();
                string hash;
                if (line.StartsWith(HashPrefix))
                {
                    // remove prefix from beginning of line
                    hash = line.Substring(HashPrefix.Length);
                }
                else
                {
                    hash = String.Empty;
                }
                Log.LogMessage("Views File {0} has hash '{1}'.", filepath, hash);
                return hash;
            }
            catch (Exception ex)
            {
                Log.LogError("Error opening file {0}: {1}", filepath, ex.Message);
                return null;
            }
        }

        #endregion
        #region Validating an EDMX

        /// <summary>
        /// Validate the EDMX file
        /// </summary>
        /// <param name="edmxFile">File containing EDMX</param>
        /// <returns>List of errors that occurred while building the mapping or while validating</returns>
        public static IList<EdmSchemaError> ValidateEdmx(FileInfo edmxFile)
        {
            var edmxReader = new StreamReader(edmxFile.FullName);
            var errors = ValidateEdmx(edmxReader);
            return errors;
        }

        /// <summary>
        /// Validate the EDMX string.
        /// </summary>
        /// <param name="edmx">String containing EDMX XML</param>
        /// <returns>List of errors that occurred while building the mapping or while validating</returns>
        public static IList<EdmSchemaError> ValidateEdmx(String edmx)
        {
            var textReader = new StringReader(edmx);
            return ValidateEdmx(textReader);
        }

        /// <summary>
        /// Validate the ECMX from the given EDMX TextReader
        /// </summary>
        /// <param name="edmxReader">TextReader providing the EDMX XML</param>
        /// <returns>List of errors that occurred while building the mapping or while validating</returns>
        public static IList<EdmSchemaError> ValidateEdmx(TextReader edmxReader)
        {
            List<EdmSchemaError> allErrors = null;
            StorageMappingItemCollection mappingItemCollection = EdmMapping.BuildMapping(edmxReader, out allErrors);

            // validate the mappings
            var viewGenerationErrors = EntityViewGenerator.Validate(mappingItemCollection);
            if (viewGenerationErrors != null) allErrors.AddRange(viewGenerationErrors);

            return allErrors;
        }

        #endregion
        #region Generating Views from an EDMX

        /// <summary>
        /// Generate views from the EDMX file
        /// </summary>
        /// <param name="edmxFile">File containing EDMX</param>
        /// <param name="languageOption">C# or VB</param>
        /// <returns>List of errors that occurred while building the mapping or while generating views</returns>
        public static IList<EdmSchemaError> GenerateViewsFromEdmx(FileInfo edmxFile, LanguageOption languageOption)
        {
            TextWriter viewsWriter = null;
            var ext = (languageOption == LanguageOption.GenerateCSharpCode) ? ".cs" : ".vb";
            var outputFile = GetFileNameWithNewExtension(edmxFile, ".Views" + ext);

            viewsWriter = new StreamWriter((string) outputFile);
            var edmxReader = new StreamReader(edmxFile.FullName);
            var errors = GenerateViewsFromEdmx(edmxReader, languageOption, viewsWriter);
            return errors;
        }

        /// <summary>
        /// Generate views from the given EDMX string.
        /// </summary>
        /// <param name="edmx">String containing EDMX XML</param>
        /// <param name="languageOption">C# or VB</param>
        /// <param name="viewsWriter">TextWriter to write the views into.</param>
        /// <returns>List of errors that occurred while building the mapping or while generating</returns>
        public static IList<EdmSchemaError> GenerateViewsFromEdmx(String edmx, LanguageOption languageOption, TextWriter viewsWriter)
        {
            var textReader = new StringReader(edmx);
            return GenerateViewsFromEdmx(textReader, languageOption, viewsWriter);
        }

        /// <summary>
        /// Generate views from the given EDMX TextReader
        /// </summary>
        /// <param name="edmxReader">TextReader providing the EDMX XML</param>
        /// <param name="languageOption">C# or VB</param>
        /// <param name="viewsWriter">TextWriter that the views will be written into.  If null, only validation will be performed.</param>
        /// <returns>List of errors that occurred while building the mapping or while validating/generating</returns>
        public static IList<EdmSchemaError> GenerateViewsFromEdmx(TextReader edmxReader, LanguageOption languageOption, TextWriter viewsWriter)
        {
            List<EdmSchemaError> allErrors = null;
            StorageMappingItemCollection mappingItemCollection = EdmMapping.BuildMapping(edmxReader, out allErrors);

            // generate views & write them out to a file
            var evg = new EntityViewGenerator(languageOption);
            var viewGenerationErrors = evg.GenerateViews(mappingItemCollection, viewsWriter);

            if (viewGenerationErrors != null) allErrors.AddRange(viewGenerationErrors);

            return allErrors;
        }

        #endregion
        #region Parsing file names and extensions

        /// <summary>
        /// Build a new file name by replacing the extension on the file with a new one
        /// </summary>
        /// <param name="file">FileInfo representing the current file name, e.g. MyDbContext.cs</param>
        /// <param name="extension">New extension to use, e.g. ".Views.cs"</param>
        /// <returns>File name with new extension, e.g. "MyDbContext.Views.cs"</returns>
        private static string GetFileNameWithNewExtension(FileSystemInfo file, string extension)
        {
            string prefix = file.Name.Substring(
                0, file.Name.Length - file.Extension.Length);
            return prefix + extension;
        }

        /// <summary>
        /// Parse the string to get the LanguageOption
        /// </summary>
        /// <param name="ext">String that should be either "cs" or "vb"</param>
        /// <param name="langOption">output parameter for returning LanguageOption</param>
        /// <returns>True if ext was successfully parsed into a LanguageOption, false otherwise</returns>
        public static bool ParseLanguageOption(string ext, out LanguageOption langOption)
        {
            langOption = LanguageOption.GenerateCSharpCode;
            if ("vb".Equals(ext, StringComparison.OrdinalIgnoreCase))
            {
                langOption = LanguageOption.GenerateVBCode;
                return true;
            }
            else if ("cs".Equals(ext, StringComparison.OrdinalIgnoreCase))
            {
                langOption = LanguageOption.GenerateCSharpCode;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Get the FileInfo from the given file name.
        /// </summary>
        /// <param name="filename">path to the file</param>
        /// <param name="fileInfo">output returning the FileInfo for the given file</param>
        /// <returns>true if the file exists, false if not</returns>
        public static bool ParseFileArguments(string filename, out FileInfo fileInfo)
        {
            string edmxFile = filename;
            fileInfo = new FileInfo(edmxFile);
            return fileInfo.Exists;
        }

        #endregion
        #region Logging

        /// <summary>
        /// Write the errors to the Log, and determine if there were any severe errors.
        /// </summary>
        /// <param name="errors"></param>
        /// <returns>Return true if any were Error severity, False if there were only warnings.</returns>
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
        #endregion
    }
}
