using System;
using System.Collections.Generic;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.Gps;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class GpsPoint
    {
        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("GPS Points");

        static readonly Dictionary<Guid, GpsPoint> GpsPoints = new Dictionary<Guid, GpsPoint>();

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

        private  GpsPoint()
        {}

        //TODO - Consider removing the FromGUID() family of initializers
        //BirdGroup.FromGuid() calls Observation.FromGuid() calls GpsPoint.FromGuid() calls TrackLog.FromGuid();
        //Nobody calls BirdGroup.FromGuid() or Transect.FromGuid()

        public static GpsPoint FromGuid(Guid guid)
        {
            if (GpsPoints.ContainsKey(guid))
                return GpsPoints[guid];

            var feature = MobileUtilities.GetFeature(FeatureLayer, guid);
            if (feature == null)
                return null;
            var gpsPoint = new GpsPoint {Feature = feature};
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
                return null;
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
            //Guid = new Guid(Feature.FeatureDataRow.GlobalId.ToByteArray());
            Guid = (Guid)Feature.FeatureDataRow["GpsPointID"];
            TrackLog = TrackLog.FromGuid((Guid)Feature.FeatureDataRow["TrackID"]);

            Latitude = (double)Feature.FeatureDataRow["Lat_dd"];
            Longitude = (double)Feature.FeatureDataRow["Long_dd"];
            Location = MobileApplication.Current.Project.SpatialReference.FromGps(Longitude, Latitude);
            GpsTime = (DateTime)Feature.FeatureDataRow["Time_utc"];
            LocalTime = (DateTime)Feature.FeatureDataRow["Time_local"];
            Hdop = (double)Feature.FeatureDataRow["HDOP"];
            SatelliteFixCount = (int)Feature.FeatureDataRow["Satellite_count"];
            SatelliteFixStatus = (GpsFixStatus)Feature.FeatureDataRow["GPS_Fix_Status"];
            Speed = (double)Feature.FeatureDataRow["Speed"];
            Bearing = (double)Feature.FeatureDataRow["Bearing"];
        }

        private void LoadAttributes(GpsConnection gpsConnection)
        {
            Latitude = gpsConnection.Latitude;
            Longitude = gpsConnection.Longitude;
            //Offset Regan's office to GLBA main dock
            Latitude -= 2.7618;
            Longitude += 13.9988;
            Location = MobileApplication.Current.Project.SpatialReference.FromGps(Longitude, Latitude);
            GpsTime = gpsConnection.DateTime;
            LocalTime = GpsTime.ToLocalTime();
            Hdop = gpsConnection.HorizontalDilutionOfPrecision;
            SatelliteFixCount = gpsConnection.FixSatelliteCount;
            SatelliteFixStatus = gpsConnection.FixStatus;
            Speed = gpsConnection.Speed;
            Bearing = gpsConnection.Course;
        }

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

    }
}
