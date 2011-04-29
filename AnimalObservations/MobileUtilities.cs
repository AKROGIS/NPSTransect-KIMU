using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using ESRI.ArcGIS.Mobile;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    class MobileUtilities
    {
        internal static FeatureLayer GetFeatureLayer(string featureLayerName)
        {
            foreach (FeatureLayerInfo fli in MobileApplication.Current.Project.EnumerateFeatureLayerInfos())
                if (fli.Name == featureLayerName)
                    return fli.FeatureLayer;
            throw new InvalidOperationException("Feature layer featureLayerName not found: " + featureLayerName);
        }

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

        internal static Feature GetFeature(FeatureLayer featureLayer, Guid guid)
        {
            string whereClause = featureLayer.GlobalIdColumnName + " = " + guid.ToString();
            return GetFeature(featureLayer, whereClause);
        }

        internal static Feature GetFeature(FeatureLayer featureLayer, string whereClause)
        {
            QueryFilter query = new QueryFilter(whereClause);
            FeatureDataReader data = featureLayer.GetDataReader(query);
            //FeatureDataTable table = featureLayer.GetDataTable(query);
            //if (data.Rows.Count < 1)
            //    return null;
            //if (table.Rows.Count > 1)
                //Ambiguous results, best to not return anything.
                return null;
            //return new Feature(table[0]);
        }

        internal static CodedValueDomain GetCodedValueDomain(FeatureLayer featureLayer, string field)
        {
            return GetCodedValueDomain(featureLayer, field, 0);
        }

        internal static CodedValueDomain GetCodedValueDomain(FeatureLayer featureLayer, string field, int subType)
        {
            return featureLayer.GetDomain(subType, field) as CodedValueDomain;
        }

        internal static IDictionary<T, string> GetCodedValueDictionary<T>(FeatureLayer featureLayer, string field)
        {
            return GetCodedValueDictionary<T>(GetCodedValueDomain(featureLayer, field));
        }

        internal static IDictionary<T, string> GetCodedValueDictionary<T>(CodedValueDomain cvd)
        {
            IDictionary<T, string> domain = new Dictionary<T, string>();
            foreach (DataRow row in cvd.Rows)
            {
                domain[(T)row.ItemArray[0]] = (string)row.ItemArray[1];
            }
            return domain;
        }

        #region deprecated

        [Obsolete]
        internal static Feature CreateNewFeature(string featureLayerName)
        {
            return CreateNewFeature(featureLayerName, 0);
        }

        [Obsolete]
        internal static Feature CreateNewFeature(string featureLayerName, int subTypeIndex)
        {
            FeatureLayer featureLayer = GetFeatureLayer(featureLayerName);
            if (subTypeIndex < 0 || subTypeIndex >= MobileApplication.Current.Project.FeatureTypeDictionary[featureLayer].Count)
                throw new ArgumentOutOfRangeException("subTypeIndex");
            FeatureType featureType = MobileApplication.Current.Project.FeatureTypeDictionary[featureLayer][subTypeIndex];
            return new Feature(featureType);
        }

        [Obsolete]
        internal static Feature GetFeature(string featureLayerName, Guid guid)
        {
            FeatureLayer featureLayer = GetFeatureLayer(featureLayerName);
            return GetFeature(featureLayer, guid);
        }

        [Obsolete]
        internal static Feature GetFeature(string featureLayerName, string whereClause)
        {
            FeatureLayer featureLayer = GetFeatureLayer(featureLayerName);
            return GetFeature(featureLayer, whereClause);
        }

        [Obsolete]
        internal static IEnumerable<GlobalId> GetGlobalIds(string featureLayerName)
        {
            FeatureLayer featureLayer = GetFeatureLayer(featureLayerName);
            List<GlobalId> ret = new List<GlobalId>();
            using (FeatureDataReader data = featureLayer.GetDataReader(new QueryFilter(), EditState.Current))
            {
                data.Reset();
                while (data.Read())
                    ret.Add(data.GetGlobalId());
                data.Close();
            }
            return ret;
        }

        #endregion
    }
}
