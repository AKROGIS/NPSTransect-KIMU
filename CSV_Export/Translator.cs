using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ESRI.ArcGIS.ADF;  //for ComReleaser, requires ESRI.ArcGIS.ADF.Connection.Local.dll
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;  // For Shape coordiantes

namespace CSV_Export
{
    class Translator
    {
        internal int Year { private get; set; }
        internal string WorkspacePath { private get; set; }
        internal Stream Output { private get; set; }

        private struct Line : IComparable
        {
            internal DateTime Date { private get; set; }
            internal DateTime TrackDate { private get; set; }
            internal string Text { get; set; }


            public int CompareTo(object obj)
            {
                var other = (Line)obj;
                int dateCompare = Date.CompareTo(other.Date);
                return dateCompare == 0 ? TrackDate.CompareTo(other.TrackDate) : dateCompare;
            }
        }

        private struct Observation
        {
            internal string Data { get; set; }
            internal string Comment { get; set; }            
        }

        internal void Translate()
        {
            if (string.IsNullOrEmpty(WorkspacePath) || Output == null)
                return;

            var startDate = new DateTime(Year, 1, 1);
            var endDate = new DateTime(Year + 1, 1, 1);
            string dateWhereClause = string.Format(
                "\"{0}\" >= date '{1}' AND \"{0}\" < date '{2}'",
                "GpsPoints.Time_local",
                startDate.ToString("yyyy-MM-dd HH:mm:ss"),
                endDate.ToString("yyyy-MM-dd HH:mm:ss")
                );

            GetArcGisLicense();

            IWorkspace workspace = FileGdbWorkspaceFromPath(WorkspacePath);
            var featureWorkspace = workspace as IFeatureWorkspace;
            if (featureWorkspace == null)
                return;

            using (var tw = new StreamWriter(Output))
            {
                //uses ordered columns with alias names (does not try to sort records)
                WriteTable(tw, featureWorkspace, dateWhereClause);
            }
        }

        private static void WriteTable(StreamWriter tw, IFeatureWorkspace workspace, string dateWhereClause)
        {
            //Get the Featureclasses
            IFeatureClass gpsPoints = workspace.OpenFeatureClass("GpsPoints");
            IFeatureClass observations = workspace.OpenFeatureClass("Observations");
            IFeatureClass birdGroups = workspace.OpenFeatureClass("BirdGroups");
            IFeatureClass tracks = workspace.OpenFeatureClass("Tracks");

            // Get the Relationship Class Factory
            Type memRelClassFactoryType = Type.GetTypeFromProgID("esriGeodatabase.MemoryRelationshipClassFactory");
            var memRelClassFactory = (IMemoryRelationshipClassFactory)Activator.CreateInstance(memRelClassFactoryType);
            
            // Get the RelQueryTable factory.
            Type rqtFactoryType = Type.GetTypeFromProgID("esriGeodatabase.RelQueryTableFactory");
            var rqtFactory = (IRelQueryTableFactory)Activator.CreateInstance(rqtFactoryType);

            //Must create the relationship class in memory (don't load from fgdb), else NO outer join.
            //IRelationshipClass rc = featureWorkspace.OpenRelationshipClass("GpsPoint_Observation");

            // You can't do an left outer join with a one-to-many relationship
            //(many-to-one relationships are not supported and right outer joins are not supported)
            // must set first parameter to false to make the many table the sourece (i.e. to get all the records in the many table)
            // you must leave the first parameter to true to get all the records in the left table for the outer join.
            //
            //Solution is to create two tables and do the join in my code.  Yuck!

            IRelationshipClass relationship1 = memRelClassFactory.Open("Obs_Bg",
                observations, "ObservationID", birdGroups, "ObservationID",
                String.Empty, String.Empty, esriRelCardinality.esriRelCardinalityOneToMany);

            //Last parameter must be true to get a left outer join (right outer join if first param is false).
            //if first parameter is false, then tables are swapped.  Must swap tables to get many to 1 (source/origin/left to dest/foriegn/right) 
            var table1 = (ITable)rqtFactory.Open(relationship1, false, null, null, String.Empty, false, false);

#if DEBUG
            tw.WriteLine("table1"); DebugPrintTable(tw, table1);
#endif
            var observationData = GetObservations(table1);

            IRelationshipClass relationship2 = memRelClassFactory.Open("Gps_Tracks",
                gpsPoints, "TrackID", tracks, "TrackID", 
                String.Empty, String.Empty, esriRelCardinality.esriRelCardinalityOneToOne);
            //Last parameter must be true in order to do a query on the table.  Observed, not documented.
            var table2 = (ITable)rqtFactory.Open(relationship2, true, null, null, String.Empty, false, true);

#if DEBUG
            tw.WriteLine("table2"); DebugPrintTable(tw, table2);
#endif
            //filter results to only the year requested.
            var query = new QueryFilter {WhereClause = dateWhereClause};
            //Query does not sort correctly when doing outer joins
            //Can't use query when sorting by time point time and transect start
            //((IQueryFilterDefinition)query).PostfixClause = "ORDER BY GpsPoints.Time_local";

            tw.WriteLine(
                "TRANSECT_ID,DATE_LOCAL,TIME_LOCAL,VESSEL,"+
                "RECORDER,OBSERVER_1,OBSERVER_2,BEAUFORT,"+
                "WEATHER_CODE,VISIBILITY,LATITUDE_WGS84,LONGITUDE_WGS84," +
                "UTM8_EASTING,UTM8_NORTHING,SPEED,BEARING," +
                "ANGLE,DISTANCE,BEHAVIOR,GROUP_SIZE,"+
                "SPECIES,ON_TRANSECT,PROTOCOL_ID,GPS_STATUS,"+
                "SATELLITES,HDOP,TRACK_LENGTH,COMMENTS,"+
                "DATA_QUALITY,DATA_QUALITY_CODE");

            var lines = new List<Line>();
            var lengths = new Dictionary<string, Dictionary<string, double>>();
            var transectLengths = new Dictionary<string, double>();

            using (var comReleaser = new ComReleaser())
            {
                //ICursor is a one pass object.  We search twice, once to get tracklengths by transect;
                ICursor cursor = table2.Search(query, true);
                comReleaser.ManageLifetime(cursor);
                IRow row;
                while ((row = cursor.NextRow()) != null)
                {
                    string transect = row.Value[27].ToString();
                    bool ontransect = Boolean.Parse(row.Value[29].ToString());
                    string trackId = row.Value[26].ToString();
                    var tracklength = (double)row.Value[15];
                    if (!lengths.ContainsKey(transect))
                        lengths[transect] = new Dictionary<string, double>();
                    //skip tracks I've already seen
                    if (!lengths[transect].ContainsKey(trackId))
                        if (ontransect)
                            lengths[transect][trackId] = tracklength;
                }
                //Sum up the on-transect tracks for each transect
                foreach (var length in lengths)
                {
                    if (!transectLengths.ContainsKey(length.Key))
                        transectLengths[length.Key] = 0;
                    foreach (var trackLength in length.Value)
                    {
                        transectLengths[length.Key] += trackLength.Value;
                    }
                }

                var emptyObservationList = new List<Observation>{new Observation{Data = ",,,,", Comment=""}};

                //Second search to format output
                cursor = table2.Search(query, true);
                while ((row = cursor.NextRow()) != null)
                {
                    var utm = (IPoint)row.Value[1];
                    var localDateTime = (DateTime) row.Value[6];
                    var trackDateTime = (DateTime) row.Value[21];
                    string date = localDateTime.ToString("yyyy-MM-dd");
                    string time = localDateTime.ToString("HH:mm:ss");
                    string transect = row.Value[27].ToString();
                    object gpsId = row.Value[13];
                    List<Observation> observationList = observationData.ContainsKey(gpsId) ? observationData[gpsId] : emptyObservationList;
                    foreach (var observation in observationList)
                    {
                        var sb = new StringBuilder();
                        sb.AppendFormat("\"{0}\",\"{1}\",\"{2}\",\"{3}\",", transect, date, time, row.Value[17]);
                        sb.AppendFormat("\"{0}\",\"{1}\",\"{2}\",\"{3}\",", row.Value[18], row.Value[19], row.Value[20], row.Value[25]);
                        sb.AppendFormat("\"{0}\",{1},{2},{3},", row.Value[24], row.Value[23], row.Value[3], row.Value[4]);
                        sb.AppendFormat("{0},{1},{2},{3},", utm.X, utm.Y, row.Value[10], row.Value[11]);
                        sb.AppendFormat("{0},", observation.Data);
                        sb.AppendFormat("\"{0}\",\"{1}\",{2},", row.Value[29], row.Value[28], row.Value[12]);
                        sb.AppendFormat("{0},{1},{2},{3},", row.Value[8], row.Value[7], transectLengths[transect], observation.Comment);
                        sb.Append(",,");
                        lines.Add(new Line { Date = localDateTime, TrackDate = trackDateTime, Text = sb.ToString() });                        
                    }
                }
            }
            lines.Sort();
            foreach (var line in lines)
                tw.WriteLine(line.Text);
        }

        private static IWorkspace FileGdbWorkspaceFromPath(String path)
        {
            Type factoryType = Type.GetTypeFromProgID("esriDataSourcesGDB.FileGDBWorkspaceFactory");
            var workspaceFactory = (IWorkspaceFactory)Activator.CreateInstance(factoryType);
            return workspaceFactory.OpenFromFile(path, 0);
        }

        private static void GetArcGisLicense()
        {
            //requires ArcGIS 10+ and reference to ESRI.ArcGIS.Version.dll
            ESRI.ArcGIS.RuntimeManager.BindLicense(ESRI.ArcGIS.ProductCode.EngineOrDesktop);
        }

        private static Dictionary<object,List<Observation>> GetObservations(ITable table)
        {
            var observationData = new Dictionary<object, List<Observation>>();
            using (var comReleaser = new ComReleaser())
            {
                ICursor cursor = table.Search(null, true);
                comReleaser.ManageLifetime(cursor);
                IRow row;
                while ((row = cursor.NextRow()) != null)
                {
                    string data = string.Format("{0},{1},\"{2}\",{3},\"{4}\"", row.Value[10], row.Value[11], row.Value[3], row.Value[4], row.Value[5]);
                    string comment = string.Format("\"{0}\"", row.Value[6]);
                    object gpsId = row.Value[12];
                    if (observationData.ContainsKey(gpsId))
                        observationData[gpsId].Add(new Observation {Data = data, Comment = comment});
                    else
                        observationData[gpsId] = new List<Observation>
                                                     {new Observation {Data = data, Comment = comment}};
                }
            }
            return observationData;
        }

#if DEBUG
        static private void DebugPrintTable(StreamWriter tw, ITable table)
        {
            using (var comReleaser = new ComReleaser())
            {
                ICursor cursor = table.Search(null, true);
                comReleaser.ManageLifetime(cursor);
                IRow row;
                var sb = new StringBuilder();
                int c = table.Fields.FieldCount;

                for (int i = 0; i < c; i++)
                    sb.AppendFormat("\"{0}\",", table.Fields.Field[i].Name);
                tw.WriteLine(sb.ToString());

                while ((row = cursor.NextRow()) != null)
                {
                    sb = new StringBuilder();
                    for (int i = 0; i < c; i++)
                        sb.AppendFormat("\"{0}\",", row.Value[i]);
                    tw.WriteLine(sb.ToString());
                }
            }
        }
#endif
    }
}
