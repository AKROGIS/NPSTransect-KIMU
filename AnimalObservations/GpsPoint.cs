using System;
using System.Collections.Generic;
using System.Diagnostics;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.Gps;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class GpsPoint
    {
        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("GPS Points");
        private static readonly Dictionary<Guid, GpsPoint> GpsPoints = new Dictionary<Guid, GpsPoint>();

        public Feature Feature { get; private set; }
        public Guid Guid { get; private set; }

        public TrackLog TrackLog { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public Coordinate Location { get; private set; }
        public DateTime GpsTime { get; private set; }
        public DateTime LocalTime { get; private set; }
        public double Hdop { get; private set; }
        public int SatelliteFixCount { get; private set; }
        public GpsFixStatus SatelliteFixStatus { get; private set; }
        public double Speed { get; private set; }
        public double Bearing { get; private set; }


        #region constructors

        private  GpsPoint()
        {}

        public static GpsPoint FromGuid(Guid guid)
        {
            if (GpsPoints.ContainsKey(guid))
                return GpsPoints[guid];

            var feature = MobileUtilities.GetFeature(FeatureLayer, guid);
            if (feature == null)
            {
                Trace.TraceError("Fail! Unable to get feature with id = {0} from {1}", guid, FeatureLayer.Name);
                return null;
            }

            var gpsPoint = new GpsPoint { Feature = feature };
            gpsPoint.LoadAttributes();
            GpsPoints[gpsPoint.Guid] = gpsPoint;
            return gpsPoint;
        }

        public static GpsPoint CreateWith(TrackLog trackLog, GpsConnection gpsConnection)
        {
            if (trackLog == null)
                throw new ArgumentNullException("trackLog");
            if (gpsConnection == null)
                throw new ArgumentNullException("gpsConnection");
            if (!gpsConnection.IsOpen)
                throw new InvalidOperationException("GPS connection is closed");

            var feature = MobileUtilities.CreateNewFeature(FeatureLayer);
            if (feature == null)
            {
                Trace.TraceError("Fail! Unable to create a new feature in {0}", FeatureLayer.Name);
                return null;
            }

            var gpsPoint = new GpsPoint
                               {
                                   Feature = feature,
                                   Guid = new Guid(feature.FeatureDataRow.GlobalId.ToByteArray()),
                                   TrackLog = trackLog
                               };

            gpsPoint.LoadAttributes(gpsConnection);
            GpsPoints[gpsPoint.Guid] = gpsPoint;
            gpsPoint.Save();
            return gpsPoint;
        }

        private void LoadAttributes()
        {
            Guid = (Guid)Feature.FeatureDataRow["GpsPointID"];
            TrackLog = TrackLog.FromGuid((Guid)Feature.FeatureDataRow["TrackID"]);
            Latitude = (double)Feature.FeatureDataRow["Lat_dd"];
            Longitude = (double)Feature.FeatureDataRow["Long_dd"];
            GpsTime = (DateTime)Feature.FeatureDataRow["Time_utc"];
            LocalTime = (DateTime)Feature.FeatureDataRow["Time_local"];
            Hdop = (double)Feature.FeatureDataRow["HDOP"];
            SatelliteFixCount = (int)Feature.FeatureDataRow["Satellite_count"];
            SatelliteFixStatus = (GpsFixStatus)Feature.FeatureDataRow["GPS_Fix_Status"];
            Speed = (double)Feature.FeatureDataRow["Speed"];
            Bearing = (double)Feature.FeatureDataRow["Bearing"];

            Location = MobileApplication.Current.Project.SpatialReference.FromGps(Longitude, Latitude);
        }

        private void LoadAttributes(GpsConnection gpsConnection)
        {
                Latitude = gpsConnection.Latitude;
                Longitude = gpsConnection.Longitude;
                //Offset Regan's office to GLBA main dock
                Latitude -= 2.7618;
                Longitude += 13.9988;
                Location = MobileApplication.Current.Project.SpatialReference.FromGps(Longitude, Latitude);

            //Location = MobileApplication.Current.Project.SpatialReference.FromGps(gpsConnection.Longitude, gpsConnection.Latitude);
            GpsTime = gpsConnection.DateTime;
            LocalTime = GpsTime.ToLocalTime();
            Hdop = gpsConnection.HorizontalDilutionOfPrecision;
            SatelliteFixCount = gpsConnection.FixSatelliteCount;
            SatelliteFixStatus = gpsConnection.FixStatus;
            Speed = gpsConnection.Speed;
            Bearing = gpsConnection.Course;
        }

        #endregion

        #region update and save

        public void Save()
        {
            Feature.Geometry = new Point(Location);
            Feature.FeatureDataRow["GpsPointID"] = Guid;
            Feature.FeatureDataRow["TrackID"] = TrackLog.Guid;
            Feature.FeatureDataRow["Lat_dd"] = Latitude;
            Feature.FeatureDataRow["Long_dd"] = Longitude;
            Feature.FeatureDataRow["Time_utc"] = GpsTime;
            Feature.FeatureDataRow["Time_local"] = LocalTime;
            Feature.FeatureDataRow["HDOP"] = Hdop;
            Feature.FeatureDataRow["Satellite_count"] = SatelliteFixCount;
            Feature.FeatureDataRow["GPS_Fix_Status"] = (int)SatelliteFixStatus;
            Feature.FeatureDataRow["Speed"] = Speed;
            Feature.FeatureDataRow["Bearing"] = Bearing;
            Feature.SaveEdits();
        }

        #endregion
    }
}
