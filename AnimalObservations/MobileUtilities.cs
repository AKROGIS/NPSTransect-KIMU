using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.FeatureCaching;

namespace AnimalObservations
{
    static class MobileUtilities
    {
        internal const int SearchRadius = 2; //mm in display units (based on map scale) on each side of cursor point to search

        internal static FeatureSource GetFeatureSource(string featureSourceName)
        {
             foreach (var layer in MobileApplication.Current.Project.EnumerateFeatureSources())
                if (layer.ServerFeatureClassName == featureSourceName)
                    return layer;
            throw new InvalidOperationException("Feature data set '" + featureSourceName + "' not found." );
        }

        #region Create New Features

        internal static Feature CreateNewFeature(FeatureSource featureSource, int subTypeIndex = 0)
        {
            if (subTypeIndex < 0 || subTypeIndex >= MobileApplication.Current.Project.FeatureTypeDictionary[featureSource].Count)
                throw new ArgumentOutOfRangeException("subTypeIndex");
            FeatureType featureType = MobileApplication.Current.Project.FeatureTypeDictionary[featureSource][subTypeIndex];
            return new Feature(featureType);
        }

        #endregion


        #region Get existing Features

#if BROKEN_WHERE_GUID
        //workaround for broken where clause on guid
        internal static Feature GetFeature(FeatureSource featureSource, Guid guid, int columnIndex)
        {
            Trace.TraceInformation("feature layer: {0}; guid: {1}; column: {2}", featureSource, guid, columnIndex);
            FeatureDataTable table = featureSource.GetDataTable(null);
            Trace.TraceInformation("found: {0}; row count = {1}", table != null, table == null ? 0 : table.Rows.Count);
            if (table == null)
                return null;

            var match = (from FeatureDataRow row in table.Rows
                         where row[columnIndex] is Guid && (Guid)row[columnIndex] == guid
                         select row).FirstOrDefault();

            return (match == null) ? null : new Feature(match);
        }

        internal static IEnumerable<FeatureDataRow> GetFeatureRows(FeatureSource featureSource, Guid guid, int columnIndex)
        {
            Trace.TraceInformation("feature layer: {0}; guid: {1}; column: {2}", featureSource, guid, columnIndex);
            FeatureDataTable table = featureSource.GetDataTable(null);
            Trace.TraceInformation("found: {0}; row count = {1}", table != null, table == null ? 0 : table.Rows.Count);
            if (table == null)
                return null;

            return from FeatureDataRow row in table.Rows
                   where row[columnIndex] is Guid && (Guid)row[columnIndex] == guid
                   select row;
        }
#else
        internal static Feature GetFeature(FeatureSource featureSource, string whereClause)
        {
            return GetFeatures(featureSource, whereClause).FirstOrDefault();
        }
#endif
        internal static Feature GetFeature(FeatureSource featureSource, Envelope extents)
        {
            return GetFeatures(featureSource, extents).FirstOrDefault();
        }

        internal static IEnumerable<Feature> GetFeatures(FeatureSource featureSource, string whereClause)
        {
            var query = new QueryFilter(whereClause);
            return GetFeatures(featureSource, query);
        }

        internal static IEnumerable<Feature> GetFeatures(FeatureSource featureSource, Envelope extents)
        {
            var query = new QueryFilter(extents, GeometricRelationshipType.Contain);
            return GetFeatures(featureSource, query);
        }

        private static IEnumerable<Feature> GetFeatures(FeatureSource featureSource, QueryFilter query)
        {
            Trace.TraceInformation("feature layer {0}; query {1} and {2} {3}", featureSource, query.WhereClause, query.GeometricRelationship, query.Geometry);
            //If query.WhereClause is invalid 'SQL' then invalid operation exception is thrown 'Operation is not valid due to the current state of the object.' message
            FeatureDataTable table = featureSource.GetDataTable(query);
            Trace.TraceInformation("found {0}; count {1}", table != null, table == null ? 0 : table.Rows.Count);
            if (table == null)
                return Enumerable.Empty<Feature>();
            return from FeatureDataRow row in table.Rows select new Feature(row);
        }

        #endregion


        #region domains/picklist

        public static IDictionary<T, string> GetCodedValueDictionary<T>(FeatureSource featureSource, string field)
        {
            return GetCodedValueDictionary<T>(GetCodedValueDomain(featureSource, field));
        }

        private static IDictionary<T, string> GetCodedValueDictionary<T>(CodedValueDomain cvd)
        {
            IDictionary<T, string> domain = new Dictionary<T, string>();
            foreach (DataRow row in cvd.Rows)
            {
                domain[(T)row.ItemArray[0]] = (string)row.ItemArray[1];
            }
            return domain;
        }

        private static CodedValueDomain GetCodedValueDomain(FeatureSource featureSource, string field, int subType = 0)
        {
            return featureSource.Columns[field].GetDomain(subType) as CodedValueDomain;
        }

        #endregion
    }
}
