using System;
using System.IO;
using System.Collections.Generic;

using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.ADF;  //for ComReleaser, requires ESRI.ArcGIS.ADF.Connection.Local.dll

namespace CSV_Export
{
    enum SegregateOutput
    {
        None,
        ByDate,
        ByTransect,
        ByDateAndTransect
    }

    class TranslatorOld
    {
        internal int Year { get; set; }
        internal string LayerFileName { get; set; }
        internal Stream Output { get; set; }
        
        internal string DateFieldName { get; set; }
        internal string TransectFieldName { get; set; }
        internal string TrackLogLengthField { get; set; }
        internal string TrackLogIdField { get; set; }

        private DateTime StartDate { get; set; }
        private DateTime EndDate { get; set; }
        private string DateQuery { get; set; }

        readonly Dictionary<string, Double> _transectLengths = new Dictionary<string, Double>();

        internal void TranslateOld()
        {
            if (string.IsNullOrEmpty(LayerFileName) || Output == null || Year < 2010)
                return;

            StartDate = new DateTime(Year,1,1);
            EndDate = new DateTime(Year+1,1,1);
            DateQuery = string.Format(
                "\"{0}\" >= date '{1}' AND \"{0}\" < date '{2}'",
                DateFieldName,
                StartDate.ToString("yyyy-MM-dd HH:mm:ss"),
                EndDate.ToString("yyyy-MM-dd HH:mm:ss")
                );

            GetArcGisLicense();

            var layerFile = new LayerFile();
            layerFile.Open(LayerFileName);
            if (layerFile.Layer == null)
                throw new Exception(LayerFileName + " is not a valid ArcGIS layerfile.");
            var layer = layerFile.Layer as IGeoFeatureLayer;
            if (layer == null)
                throw new Exception(LayerFileName + " is not a valid ArcGIS Geo-feature layer.");

            foreach (string transect in UniqueTransects(layer))
            {
                _transectLengths[transect] = 0;
                foreach (string tracklog in UniqueTrackLogs(layer, transect))
                    _transectLengths[transect] += GetTrackLogLength(layer, tracklog);
            }

            using (var tw = new StreamWriter(Output))
            {
                //uses ordered columns with alias names (does not try to sort records)
                WriteUnsortedTable(tw, layer);

                //The following is for testing the sorting options

                // for joined table  (CSV_Export_Join.lyr) - runs without exception, but does not sort results
                // for simple table (CSV_Export_NoJoin.lyr) - works great
                string fieldname = "GpsPoints.OBJECTID"; 
                if (LayerFileName.Contains("CSV_Export_NoJoin"))
                    fieldname = "ObjectID";
                TestOrderBy(tw, layer, fieldname);
                TestTableSort(tw, layer, fieldname);
            }
        }

        private double GetTrackLogLength(IGeoFeatureLayer layer, string tracklog)
        {
            var query = new QueryFilter
            {
                SubFields = TrackLogLengthField,
                WhereClause = string.Format("\"{0}\" = '{1}'", TrackLogIdField, tracklog)
            };

            using (var comReleaser = new ComReleaser())
            {
                var fCursor = layer.DisplayFeatureClass.Search(query, true);
                comReleaser.ManageLifetime(fCursor);
                var row = fCursor.NextFeature();
                int index = fCursor.FindField(TrackLogLengthField);
                //return 0 if we can't find a length for this tracklog;
                if (row == null || row.Value[index] == null)
                    return 0;
                return Convert.ToDouble(row.Value[index]);
            }
        }

        private IEnumerable<string> UniqueTransects(IGeoFeatureLayer layer)
        {
            var query = new QueryFilter
            {
                SubFields = TransectFieldName,
                WhereClause = DateQuery,
            };
            //((IQueryFilterDefinition2)query).PrefixClause = "DISTINCT";   //DISTINCT Not recognized by FGDB

            //Do not use yield here because we don't want to leave the COM cursor object open
            var results = new List<string>();

            using (var comReleaser = new ComReleaser())
            {
                var cursor = (ICursor)layer.DisplayFeatureClass.Search(query, true);
                comReleaser.ManageLifetime(cursor);
                var uniqueCursor = new DataStatistics
                {
                    Field = TransectFieldName,
                    Cursor = cursor
                };
                var values = uniqueCursor.UniqueValues;
                while (values.MoveNext())
                    results.Add(values.Current == null ? null : values.Current.ToString());
            }
            return results;
        }

        private IEnumerable<string> UniqueTrackLogs(IGeoFeatureLayer layer, string transect)
        {
            string where = string.Format(
                "{0} AND \"{1}\" = '{2}'",
                DateQuery,TransectFieldName, transect
                );

            var query = new QueryFilter
            {
                SubFields = TrackLogIdField,
                WhereClause = where,
            };
            //((IQueryFilterDefinition2)query).PrefixClause = "DISTINCT";   //DISTINCT Not recognized by FGDB

            //Do not use yield here because we don't want to leave the COM cursor object open
            var results = new List<string>();

            using (var comReleaser = new ComReleaser())
            {
                var cursor = (ICursor) layer.DisplayFeatureClass.Search(query, true);
                comReleaser.ManageLifetime(cursor);
                var uniqueCursor = new DataStatistics
                                       {
                                           Field = TrackLogIdField,
                                           Cursor = cursor
                                       };
                var values = uniqueCursor.UniqueValues;
                while (values.MoveNext())
                    results.Add(values.Current == null ? null : values.Current.ToString());
            }
            return results;
        }

        //For testing only

        private void TestOrderBy(StreamWriter tw, IGeoFeatureLayer layer, string fieldname)
        {
            var query = new QueryFilter();
            ((IQueryFilterDefinition)query).PostfixClause = string.Format("ORDER BY \"{0}\"", fieldname);

            //Write header
            tw.WriteLine("Testing ORDER BY query on fieldname = {0}", fieldname);

            //Write rows
            IFeatureCursor cursor = layer.DisplayFeatureClass.Search(query, true);
            IFeature feature = cursor.NextFeature();
            int index = feature.Fields.FindField(fieldname);
            while (feature != null)
            {
                tw.WriteLine("\"{0}\"", feature.Value[index]);
                feature = cursor.NextFeature();
            }
        }

        private void TestTableSort(StreamWriter tw, IGeoFeatureLayer layer, string fieldname)
        {
            var tablesort = new TableSortClass
                                {
                                    Table = ((IDisplayTable) layer).DisplayTable,
                                    Fields = fieldname
                                };
            tablesort.Sort(null);

            //Write header
            tw.WriteLine("Testing TableSortClass with fieldname = {0}", fieldname);

            //Write rows
            ICursor cursor = tablesort.Rows;
            IRow feature = cursor.NextRow();
            int index = feature.Fields.FindField(fieldname);
            while (feature != null)
            {
                tw.WriteLine("\"{0}\"", feature.Value[index]);
                feature = cursor.NextRow();
            }
        }


        private void WriteUnsortedTable(StreamWriter tw, IGeoFeatureLayer layer)
        {
            //Get an ordered list of visible fields
            var fieldInfo = (ILayerFields)layer;  //In database order (results are indexed in DB order)
            var orderedFieldIndices = new List<int>(); //In layer order
            var orderedFieldInfo = (IOrderedLayerFields)layer;
            IFieldInfoSet orderedFields = orderedFieldInfo.FieldInfos;
            for (int i = 0; i < orderedFields.Count; i++)
                if (orderedFields.FieldInfo[i].Visible)
                    orderedFieldIndices.Add(fieldInfo.FindField(orderedFields.FieldName[i]));

            
            //Write header
            foreach (var i in orderedFieldIndices)
                tw.Write("\"{0}\",", fieldInfo.FieldInfo[i].Alias);
            tw.WriteLine("\"TrackTotal\"");

            //Write rows
            IFeatureCursor cursor = layer.SearchDisplayFeatures(null, true); //get all fields of all records
            IFeature feature = cursor.NextFeature();
            int transectIndex = cursor.FindField(TransectFieldName);
            while (feature != null)
            {
                foreach (var i in orderedFieldIndices)
                    tw.Write("\"{0}\",", feature.Value[i]);
                tw.WriteLine("\"{0}\"", _transectLengths[feature.Value[transectIndex].ToString()]);

                feature = cursor.NextFeature();
            }
        }

        //private static void WriteUnsortedTable2(StreamWriter tw, IGeoFeatureLayer layer)
        //{
        //    //Get an ordered list of visible fields
        //    var fieldInfo = (ILayerFields)layer;  //In database order (results are indexed in DB order)
        //    var orderedFieldIndices = new List<int>(); //In layer order
        //    var orderedFieldInfo = (IOrderedLayerFields)layer;
        //    IFieldInfoSet orderedFields = orderedFieldInfo.FieldInfos;
        //    for (int i = 0; i < orderedFields.Count; i++)
        //        if (orderedFields.FieldInfo[i].Visible)
        //            orderedFieldIndices.Add(fieldInfo.FindField(orderedFields.FieldName[i]));

        //    //Treat the last field special (end with a newline instead of a comma)
        //    int last = orderedFieldIndices.Last();
        //    orderedFieldIndices.Remove(last);

        //    //Write header
        //    foreach (var i in orderedFieldIndices)
        //        tw.Write("\"{0}\",", fieldInfo.FieldInfo[i].Alias);
        //    tw.WriteLine(fieldInfo.FieldInfo[last].Alias);

        //    //Write rows
        //    IFeatureCursor cursor = layer.SearchDisplayFeatures(null, true); //get all fields of all records
        //    IFeature feature = cursor.NextFeature();
        //    while (feature != null)
        //    {
        //        foreach (var i in orderedFieldIndices)
        //            tw.Write("\"{0}\",", feature.Value[i]);
        //        tw.WriteLine("\"{0}\"", feature.Value[last]);

        //        feature = cursor.NextFeature();
        //    }
        //}

        private static void GetArcGisLicense()
        {
            //requires ArcGIS 10+ and reference to ESRI.ArcGIS.Version.dll
            ESRI.ArcGIS.RuntimeManager.BindLicense(ESRI.ArcGIS.ProductCode.EngineOrDesktop);
        }
    }

}
