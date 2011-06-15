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
        internal int Year { get; set; }
        internal string WorkspacePath { get; set; }
        internal Stream Output { get; set; }

        private struct Line : IComparable
        {
            public DateTime Date { get; set; }
            public string Text { get; set; }


            public int CompareTo(object obj)
            {
                var other = (Line)obj;
                return this.Date.CompareTo(other.Date);
            }
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

        internal void WriteTable(StreamWriter tw, IFeatureWorkspace workspace, string dateWhereClause)
        {
            //Get the Featureclasses
            IFeatureClass gpsFC = workspace.OpenFeatureClass("GpsPoints");
            IFeatureClass obsFC = workspace.OpenFeatureClass("Observations");
            IFeatureClass bgFC = workspace.OpenFeatureClass("BirdGroups");
            IFeatureClass trkFC = workspace.OpenFeatureClass("Tracks");

            // Get the Relationship Class Factory
            Type memRelClassFactoryType = Type.GetTypeFromProgID("esriGeodatabase.MemoryRelationshipClassFactory");
            var memRelClassFactory = (IMemoryRelationshipClassFactory)Activator.CreateInstance(memRelClassFactoryType);
            
            // Get the RelQueryTable factory.
            Type rqtFactoryType = Type.GetTypeFromProgID("esriGeodatabase.RelQueryTableFactory");
            var rqtFactory = (IRelQueryTableFactory)Activator.CreateInstance(rqtFactoryType);

            //Must create the relationship class in memory (don't load from fgdb), else NO outer join.
            //IRelationshipClass rc = featureWorkspace.OpenRelationshipClass("GpsPoint_Observation");

            IRelationshipClass rc1 = memRelClassFactory.Open("Obs_Gps",
                obsFC, "GpsPointID", gpsFC, "GpsPointID",
                "", "", esriRelCardinality.esriRelCardinalityOneToOne);

            //Last parameter must be true to get outer join, last parameter must be true to get sorting.
            var table1 = (ITable)rqtFactory.Open(rc1, false, null, null, String.Empty, false, true);

            IRelationshipClass rc2 = memRelClassFactory.Open("BG_Obs_Gps",
                bgFC, "ObservationID", (IObjectClass)table1, "Observations.ObservationID",
                String.Empty, String.Empty, esriRelCardinality.esriRelCardinalityOneToOne);

            var table2 = (ITable)rqtFactory.Open(rc2, false, null, null, String.Empty, false, true);

            IRelationshipClass rc3 = memRelClassFactory.Open("Track_BG_Obs_Gps",
                trkFC, "TrackID", (IObjectClass)table2, "GpsPoints.TrackID",
                String.Empty, String.Empty, esriRelCardinality.esriRelCardinalityOneToOne);

            var table3 = (ITable)rqtFactory.Open(rc3, false, null, null, String.Empty, false, true);


            //query must have a where clause or else the postfix will be ignored
            var query = new QueryFilter();
            query.WhereClause = dateWhereClause;
            ((IQueryFilterDefinition)query).PostfixClause = "ORDER BY GpsPoints.Time_local";

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
                ICursor cursor = table3.Search(query, true);
                comReleaser.ManageLifetime(cursor);
                IRow row;
                while ((row = cursor.NextRow()) != null)
                {
                    string transect = row.Value[40].ToString();
                    bool ontransect = Boolean.Parse(row.Value[42].ToString());
                    string trackId = row.Value[29].ToString();
                    var tracklength = (double)row.Value[28];
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


                //Second search to format output
                cursor = table3.Search(query, true);
                while ((row = cursor.NextRow()) != null)
                {
                    var utm = (IPoint)row.Value[1];
                    var localDateTime = (DateTime) row.Value[6]; //try 5 to verify 24 time
                    string date = localDateTime.ToString("yyyy-MM-dd");
                    string time = localDateTime.ToString("HH:mm:ss");
                    string transect = row.Value[40].ToString();
                    var sb = new StringBuilder();
                    sb.AppendFormat("\"{0}\",\"{1}\",\"{2}\",\"{3}\",", transect, date, time, row.Value[30]);
                    sb.AppendFormat("\"{0}\",\"{1}\",\"{2}\",\"{3}\",", row.Value[31], row.Value[32], row.Value[33], row.Value[38]);
                    sb.AppendFormat("\"{0}\",{1},{2},{3},", row.Value[37], row.Value[36], row.Value[3], row.Value[4]);
                    sb.AppendFormat("{0},{1},{2},{3},", utm.X, utm.Y, row.Value[10], row.Value[11]);
                    sb.AppendFormat("{0},{1},\"{2}\",{3},", row.Value[16], row.Value[17], row.Value[22], row.Value[23]);
                    sb.AppendFormat("\"{0}\",\"{1}\",\"{2}\",{3},", row.Value[24], row.Value[42], row.Value[41], row.Value[12]);
                    sb.AppendFormat("{0},{1},{2},\"{3}\"", row.Value[8], row.Value[7], transectLengths[transect], row.Value[25]);
                    sb.Append(",,");
                    lines.Add(new Line { Date = localDateTime, Text = sb.ToString() });
                }
            }
            lines.Sort();
            foreach (var line in lines)
                tw.WriteLine(line.Text);
        }

        public static IWorkspace FileGdbWorkspaceFromPath(String path)
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
    }
}
