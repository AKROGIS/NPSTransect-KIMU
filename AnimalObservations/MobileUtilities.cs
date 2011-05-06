using System;
using System.Collections.Generic;
using System.Data;
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
            foreach (FeatureLayerInfo fli in MobileApplication.Current.Project.EnumerateFeatureLayerInfos())
                if (fli.Name == featureLayerName)
                    return fli.FeatureLayer;
            //FIXME - Provide a message box then quit.  This is called by multiple class constructors 
            throw new InvalidOperationException("Feature layer featureLayerName not found: " + featureLayerName);
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

        internal static Feature GetFeature(FeatureLayer featureLayer, Guid guid)
        {
            string whereClause = string.Format("{0} = {1}", featureLayer.GlobalIdColumnName, guid);
            return GetFeature(featureLayer, whereClause);
        }



        internal static Feature GetFeature(FeatureLayer featureLayer, string whereClause)
        {
            throw new NotImplementedException();
            //var query = new QueryFilter(whereClause);
            //FeatureDataReader data = featureLayer.GetDataReader(query);
            //FeatureDataTable table = featureLayer.GetDataTable(query);
            //if (table.Rows.Count < 1)
            //    return null;
            //if (table.Rows.Count > 1)
            //    //Ambiguous results, best to not return anything.
            //    return null;
            //return new Feature(table[0]);
        }

        internal static Feature GetFeature(FeatureLayer featureLayer, Envelope extents)
        {
            var query = new QueryFilter(extents, GeometricRelationshipType.Within);
            FeatureDataTable table = featureLayer.GetDataTable(query);
            if (table.Rows.Count < 1)
                return null;
            //If more than one record is returned, ignore all but the first.
            //user will need to zoom in to make search more selective.
            return new Feature(table[0]);
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
