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
        internal const int SearchRadius = 10;

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

        //FIXME - Feature must be in an edit state to get the row.
        //TODO - check to see what happens if we create a Feature from a feature data row already open for editing

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
            Trace.TraceInformation("feature layer {0}; query {1}", featureLayer, query);
            FeatureDataTable table = featureLayer.GetDataTable(query);
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
