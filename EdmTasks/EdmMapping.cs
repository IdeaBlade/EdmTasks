/*
 * Portions of this file are from EdmGen2, as described in the ADO.NET team blog,
 * http://blogs.msdn.com/b/adonet/archive/2008/06/26/edm-tools-options-part-3-of-4.aspx .
 * Please see the license terms described in 
 * http://archive.msdn.microsoft.com/EdmGen2/Project/License.aspx
 */
using System;
using System.Collections.Generic;
using System.Data.Entity.Design;
using System.Data.Mapping;
using System.Data.Metadata.Edm;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace EdmTasks
{
    /// <summary>
    /// Class that deals with generating the StorageMappingItemCollection from an EDMX.
    /// </summary>
    class EdmMapping
    {
        // a class that understands what the different XML namespaces are for the different EF versions. 
        private static readonly NamespaceManager _namespaceManager = new NamespaceManager();

        /// <summary>
        /// Build the mappings from the EDMX provided by the TextReader
        /// </summary>
        /// <param name="edmxReader">TextReader over EDMX XML</param>
        /// <param name="schemaErrors">(out) Any errors that occurred in parsing the EDMX</param>
        /// <returns>The mappings built from the EDMX</returns>
        public static StorageMappingItemCollection BuildMapping(TextReader edmxReader, out List<EdmSchemaError> schemaErrors)
        {
            XDocument doc = XDocument.Load(edmxReader);
            XElement c = GetCsdlFromEdmx(doc);
            XElement s = GetSsdlFromEdmx(doc);
            XElement m = GetMslFromEdmx(doc);

            // load the csdl
            XmlReader[] cReaders = { c.CreateReader() };
            IList<EdmSchemaError> cErrors = null;
            EdmItemCollection edmItemCollection =
                MetadataItemCollectionFactory.CreateEdmItemCollection(cReaders, out cErrors);

            // load the ssdl 
            XmlReader[] sReaders = { s.CreateReader() };
            IList<EdmSchemaError> sErrors = null;
            StoreItemCollection storeItemCollection =
                MetadataItemCollectionFactory.CreateStoreItemCollection(sReaders, out sErrors);

            // load the msl
            XmlReader[] mReaders = { m.CreateReader() };
            IList<EdmSchemaError> mErrors = null;
            StorageMappingItemCollection mappingItemCollection =
                MetadataItemCollectionFactory.CreateStorageMappingItemCollection(
                edmItemCollection, storeItemCollection, mReaders, out mErrors);

            // write errors & return errors
            var allErrors = new List<EdmSchemaError>();
            if (cErrors != null) allErrors.AddRange(cErrors);
            if (sErrors != null) allErrors.AddRange(sErrors);
            if (mErrors != null) allErrors.AddRange(mErrors);
            schemaErrors = allErrors;
            return mappingItemCollection;
        }

        #region Extracting the csdl, ssdl & msl sections from an EDMX file

        private static XElement GetCsdlFromEdmx(XDocument xdoc)
        {
            Version version = _namespaceManager.GetVersionFromEDMXDocument(xdoc);
            string csdlNamespace = _namespaceManager.GetCSDLNamespaceForVersion(version).NamespaceName;
            return (from item in xdoc.Descendants(
                        XName.Get("Schema", csdlNamespace))
                    select item).First();
        }

        private static XElement GetSsdlFromEdmx(XDocument xdoc)
        {
            Version version = _namespaceManager.GetVersionFromEDMXDocument(xdoc);
            string ssdlNamespace = _namespaceManager.GetSSDLNamespaceForVersion(version).NamespaceName;
            return (from item in xdoc.Descendants(
                        XName.Get("Schema", ssdlNamespace))
                    select item).First();
        }

        private static XElement GetMslFromEdmx(XDocument xdoc)
        {
            Version version = _namespaceManager.GetVersionFromEDMXDocument(xdoc);
            string mslNamespace = _namespaceManager.GetMSLNamespaceForVersion(version).NamespaceName;
            return (from item in xdoc.Descendants(
                        XName.Get("Mapping", mslNamespace))
                    select item).First();
        }

        #endregion
    }

    #region NamespaceManager

    /// <summary>
    /// a class that understands what the different XML namespaces are for the different EF versions. 
    /// </summary>
    internal class NamespaceManager
    {
        private static Version v1 = EntityFrameworkVersions.Version1;
        private static Version v2 = EntityFrameworkVersions.Version2;
        private static Version v3 = EntityFrameworkVersions.Version3;

        private Dictionary<Version, XNamespace> _versionToCSDLNamespace = new Dictionary<Version, XNamespace>() 
        { 
        { v1, XNamespace.Get("http://schemas.microsoft.com/ado/2006/04/edm") }, 
        { v2, XNamespace.Get("http://schemas.microsoft.com/ado/2008/09/edm") },
        { v3, XNamespace.Get("http://schemas.microsoft.com/ado/2009/11/edm") } 
        };

        private Dictionary<Version, XNamespace> _versionToSSDLNamespace = new Dictionary<Version, XNamespace>() 
        { 
        { v1, XNamespace.Get("http://schemas.microsoft.com/ado/2006/04/edm/ssdl") }, 
        { v2, XNamespace.Get("http://schemas.microsoft.com/ado/2009/02/edm/ssdl") },
        { v3, XNamespace.Get("http://schemas.microsoft.com/ado/2009/11/edm/ssdl") } 
        };

        private Dictionary<Version, XNamespace> _versionToMSLNamespace = new Dictionary<Version, XNamespace>() 
        { 
        { v1, XNamespace.Get("urn:schemas-microsoft-com:windows:storage:mapping:CS") }, 
        { v2, XNamespace.Get("http://schemas.microsoft.com/ado/2008/09/mapping/cs") },
        { v3, XNamespace.Get("http://schemas.microsoft.com/ado/2009/11/mapping/cs") } 
        };


        private Dictionary<Version, XNamespace> _versionToEDMXNamespace = new Dictionary<Version, XNamespace>() 
        { 
        { v1, XNamespace.Get("http://schemas.microsoft.com/ado/2007/06/edmx") }, 
        { v2, XNamespace.Get("http://schemas.microsoft.com/ado/2008/10/edmx") } ,
        { v3, XNamespace.Get("http://schemas.microsoft.com/ado/2009/11/edmx") } 
        };

        private Dictionary<XNamespace, Version> _namespaceToVersion = new Dictionary<XNamespace, Version>();

        internal NamespaceManager()
        {
            foreach (KeyValuePair<Version, XNamespace> kvp in _versionToCSDLNamespace)
            {
                _namespaceToVersion.Add(kvp.Value, kvp.Key);
            }

            foreach (KeyValuePair<Version, XNamespace> kvp in _versionToSSDLNamespace)
            {
                _namespaceToVersion.Add(kvp.Value, kvp.Key);
            }

            foreach (KeyValuePair<Version, XNamespace> kvp in _versionToMSLNamespace)
            {
                _namespaceToVersion.Add(kvp.Value, kvp.Key);
            }

            foreach (KeyValuePair<Version, XNamespace> kvp in _versionToEDMXNamespace)
            {
                _namespaceToVersion.Add(kvp.Value, kvp.Key);
            }
        }

        internal Version GetVersionFromEDMXDocument(XDocument xdoc)
        {
            XElement el = xdoc.Root;
            if (el.Name.LocalName.Equals("Edmx") == false)
            {
                throw new ArgumentException("Unexpected root node local name for edmx document");
            }
            return this.GetVersionForNamespace(el.Name.Namespace);
        }

        internal Version GetVersionFromCSDLDocument(XDocument xdoc)
        {
            XElement el = xdoc.Root;
            if (el.Name.LocalName.Equals("Schema") == false)
            {
                throw new ArgumentException("Unexpected root node local name for csdl document");
            }
            return this.GetVersionForNamespace(el.Name.Namespace);
        }

        internal XNamespace GetMSLNamespaceForVersion(Version v)
        {
            XNamespace n;
            _versionToMSLNamespace.TryGetValue(v, out n);
            if (n == null)
                throw new NotImplementedException("Unknown msl namespace: " + v.ToString());
            return n;
        }

        internal XNamespace GetCSDLNamespaceForVersion(Version v)
        {
            XNamespace n;
            _versionToCSDLNamespace.TryGetValue(v, out n);
            if (n == null)
                throw new NotImplementedException("Unknown csdl namespace: " + v.ToString());
            return n;
        }

        internal XNamespace GetSSDLNamespaceForVersion(Version v)
        {
            XNamespace n;
            _versionToSSDLNamespace.TryGetValue(v, out n);
            if (n == null)
                throw new NotImplementedException("Unknown ssdl namespace: " + v.ToString());
            return n;
        }

        internal XNamespace GetEDMXNamespaceForVersion(Version v)
        {
            XNamespace n;
            _versionToEDMXNamespace.TryGetValue(v, out n);
            if (n == null)
                throw new NotImplementedException("Unknown edmx namespace: " + v.ToString());
            return n;
        }

        internal Version GetVersionForNamespace(XNamespace n)
        {
            Version v;
            _namespaceToVersion.TryGetValue(n, out v);
            if (v == null)
                throw new NotImplementedException("Unknown namespace: " + n.NamespaceName);
            return v;
        }
    }
    #endregion
}
