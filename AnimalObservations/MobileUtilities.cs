#define BROKEN_WHERE_GUID

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using ESRI.ArcGIS.Mobile;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    class MobileUtilities
    {
        internal const int SearchRadius = 2; //mm in display units (based on map scale) on each side of cursor point to search

        internal static FeatureLayer GetFeatureLayer(string featureLayerName)
        {
             foreach (var layer in MobileApplication.Current.Project.EnumerateFeatureLayers())
                if (layer.ServerFeatureClassName == featureLayerName)
                    return layer;
            throw new InvalidOperationException("Feature data set '" + featureLayerName + "' not found." );
        }

        #region Create New Features

        internal static Feature CreateNewFeature(FeatureLayer featureLayer)
        {
            return CreateNewFeature(featureLayer, 0);
        }

        internal static Feature CreateNewFeature(FeatureLayer featureLayer, int subTypeIndex)
        {
            if (subTypeIndex < 0 || subTypeIndex >= MobileApplication.Current.Project.FeatureTypeDictionary[featureLayer].Count)
                throw new ArgumentOutOfRangeException("subTypeIndex");
            FeatureType featureType = MobileApplication.Current.Project.FeatureTypeDictionary[featureLayer][subTypeIndex];
            return new Feature(featureType);
        }

        #endregion

        #region Get existing Features

#if BROKEN_WHERE_GUID
        //workaround for broken where clause on guid
        internal static Feature GetFeature(FeatureLayer featureLayer, Guid guid, int columnIndex)
        {
            Trace.TraceInformation("feature layer: {0}; guid: {1}; column: {2}", featureLayer, guid, columnIndex);
            FeatureDataTable table = featureLayer.GetDataTable(null);
            Trace.TraceInformation("found: {0}; row count = {1}", table != null, table == null ? 0 : table.Rows.Count);
            if (table == null)
                return null;

            var match = (from FeatureDataRow row in table.Rows
                         where row[columnIndex] is Guid && (Guid)row[columnIndex] == guid
                         select row).FirstOrDefault();

            return (match == null) ? null : new Feature(match);
        }

        internal static IEnumerable<FeatureDataRow> GetFeatureRows(FeatureLayer featureLayer, Guid guid, int columnIndex)
        {
            Trace.TraceInformation("feature layer: {0}; guid: {1}; column: {2}", featureLayer, guid, columnIndex);
            FeatureDataTable table = featureLayer.GetDataTable(null);
            Trace.TraceInformation("found: {0}; row count = {1}", table != null, table == null ? 0 : table.Rows.Count);
            if (table == null)
                return null;

            return from FeatureDataRow row in table.Rows
                   where row[columnIndex] is Guid && (Guid)row[columnIndex] == guid
                   select row;
        }
#endif

        internal static Feature GetFeature(FeatureLayer featureLayer, string whereClause)
        {
            return GetFeatures(featureLayer, whereClause).FirstOrDefault();
        }

        internal static Feature GetFeature(FeatureLayer featureLayer, Envelope extents)
        {
            return GetFeatures(featureLayer, extents).FirstOrDefault();
        }

        internal static IEnumerable<Feature> GetFeatures(FeatureLayer featureLayer, string whereClause)
        {
            var query = new QueryFilter(whereClause);
            return GetFeatures(featureLayer, query);
        }

        internal static IEnumerable<Feature> GetFeatures(FeatureLayer featureLayer, Envelope extents)
        {
            var query = new QueryFilter(extents, GeometricRelationshipType.Contain);
            return GetFeatures(featureLayer, query);
        }

        private static IEnumerable<Feature> GetFeatures(FeatureLayer featureLayer, QueryFilter query)
        {
            Trace.TraceInformation("feature layer {0}; query {1} and {2} {3}", featureLayer, query.WhereClause, query.GeometricRelationship, query.Geometry);
            //If query.WhereClause is invalid 'SQL' then invalid operation exception is thrown 'Operation is not valid due to the current state of the object.' message
            FeatureDataTable table = featureLayer.GetDataTable(query);
            Trace.TraceInformation("found {0}; count {1}", table != null, table == null ? 0 : table.Rows.Count);
            if (table == null)
                return Enumerable.Empty<Feature>();
            return from FeatureDataRow row in table.Rows select new Feature(row);
            //return table.Rows.Cast<FeatureDataRow>().Select(row =>
            //{
            //    var f = new Feature(row);
            //    f.StartEditing();
            //    return f;
            //});
        }

        #endregion

        #region domains/picklist

        public static IDictionary<T, string> GetCodedValueDictionary<T>(FeatureLayer featureLayer, string field)
        {
            return GetCodedValueDictionary<T>(GetCodedValueDomain(featureLayer, field));
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

        private static CodedValueDomain GetCodedValueDomain(FeatureLayer featureLayer, string field, int subType = 0)
        {
            return featureLayer.GetDomain(subType, field) as CodedValueDomain;
        }

        #endregion


    }
}
