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
        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("GpsPoints");
        private static readonly Dictionary<Guid, GpsPoint> GpsPoints = new Dictionary<Guid, GpsPoint>();

        private Feature Feature { get; set; }
        internal Guid Guid { get; private set; }

        internal TrackLog TrackLog { get; private set; }
        internal double Latitude { get; private set; }
        internal double Longitude { get; private set; }
        internal Coordinate Location { get; private set; }
        internal DateTime GpsTime { get; private set; }
        internal DateTime LocalTime { get; private set; }
        internal double Hdop { get; private set; }
        internal int SatelliteFixCount { get; private set; }
        internal GpsFixStatus SatelliteFixStatus { get; private set; }
        internal double Speed { get; private set; }
        internal double Bearing { get; private set; }


        #region constructors

        private  GpsPoint()
        {}

        //May return null if no feature is found with matching guid
        internal static GpsPoint FromGuid(Guid guid)
        {
            if (GpsPoints.ContainsKey(guid))
                return GpsPoints[guid];

            string whereClause = string.Format("GpsPointID = {0}", guid);
            GpsPoint gpsPoint = CreateFromFeature(MobileUtilities.GetFeature(FeatureLayer, whereClause));
            if (gpsPoint != null && gpsPoint.TrackLog == null)
                throw new ApplicationException("Existing gps point has no track log");
            return gpsPoint;
        }

        internal static GpsPoint CreateWith(TrackLog trackLog, GpsConnection gpsConnection)
        {
            if (trackLog == null)
                throw new ArgumentNullException("trackLog");
            if (gpsConnection == null)
                throw new ArgumentNullException("gpsConnection");
            if (!gpsConnection.IsOpen)
                throw new InvalidOperationException("GPS connection is closed");

            //May throw an exception, but should never return null
            var gpsPoint = CreateFromFeature(MobileUtilities.CreateNewFeature(FeatureLayer));
            gpsPoint.TrackLog = trackLog;
            gpsPoint.LoadAttributes(gpsConnection);
            return gpsPoint;
        }

        //ONLY USE FOR TESTING THE DB SCHEMA!  RESULTING OBJECT WILL NOT HAVE VALID PROPERTY VALUES!
        internal static GpsPoint CreateWith(TrackLog trackLog)
        {
            if (trackLog == null)
                throw new ArgumentNullException("trackLog");

            //May throw an exception, but should never return null
            var gpsPoint = CreateFromFeature(MobileUtilities.CreateNewFeature(FeatureLayer));
            gpsPoint.TrackLog = trackLog;
            return gpsPoint;
        }

        private static GpsPoint CreateFromFeature(Feature feature)
        {
            if (feature == null)
                return null;
            var gpsPoint = new GpsPoint { Feature = feature };
            gpsPoint.LoadAttributes();
            GpsPoints[gpsPoint.Guid] = gpsPoint;
            return gpsPoint;
        }

        private void LoadAttributes()
        {
            bool existing = Feature.FeatureDataRow["GpsPointID"] is Guid;

            if (existing)
                Guid = (Guid)Feature.FeatureDataRow["GpsPointID"];
            else
                Guid = new Guid(Feature.FeatureDataRow.GlobalId.ToByteArray());

            //For new features, TrackID will be null, so we can't load a TrackLog feature from the database
            //For new features, We will rely on the caller to set the TrackLog property 
            if (existing && Feature.FeatureDataRow["TrackID"] is Guid)
                TrackLog = TrackLog.FromGuid((Guid)Feature.FeatureDataRow["TrackID"]);

            //Simple Attributes
            if (Feature.FeatureDataRow["Lat_dd"] is double)
                Latitude = (double)Feature.FeatureDataRow["Lat_dd"];
            if (Feature.FeatureDataRow["Lat_dd"] is double)
                Longitude = (double)Feature.FeatureDataRow["Long_dd"];
            if (Feature.FeatureDataRow["Time_utc"] is DateTime)
                GpsTime = (DateTime)Feature.FeatureDataRow["Time_utc"];
            if (Feature.FeatureDataRow["Time_utc"] is DateTime)
                LocalTime = (DateTime)Feature.FeatureDataRow["Time_local"];
            if (Feature.FeatureDataRow["HDOP"] is double)
                Hdop = (double)Feature.FeatureDataRow["HDOP"];
            if (Feature.FeatureDataRow["Satellite_count"] is int)
                SatelliteFixCount = (int)Feature.FeatureDataRow["Satellite_count"];
            if (Feature.FeatureDataRow["GPS_Fix_Status"] is int)
                SatelliteFixStatus = (GpsFixStatus)Feature.FeatureDataRow["GPS_Fix_Status"];
            if (Feature.FeatureDataRow["Speed"] is double)
                Speed = (double)Feature.FeatureDataRow["Speed"];
            if (Feature.FeatureDataRow["Bearing"] is double)
                Bearing = (double)Feature.FeatureDataRow["Bearing"];

            Location = MobileApplication.Current.Project.SpatialReference.FromGps(Longitude, Latitude);
        }

        private void LoadAttributes(GpsConnection gpsConnection)
        {
            Latitude = gpsConnection.Latitude;
            Longitude = gpsConnection.Longitude;
            GpsTime = gpsConnection.DateTime;
            LocalTime = GpsTime.ToLocalTime();
            Hdop = gpsConnection.HorizontalDilutionOfPrecision;
            SatelliteFixCount = gpsConnection.FixSatelliteCount;
            SatelliteFixStatus = gpsConnection.FixStatus;
            Speed = gpsConnection.Speed;
            Bearing = gpsConnection.Course;

            //Offset Regan's office to GLBA main dock
            //Latitude -= 2.7618;
            //Longitude += 13.9988;
            //Location = MobileApplication.Current.Project.SpatialReference.FromGps(Longitude, Latitude);

            Location = MobileApplication.Current.Project.SpatialReference.FromGps(gpsConnection.Longitude, gpsConnection.Latitude);
        }

        #endregion

        #region Save/Delete

        internal void Save()
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

        internal void Delete()
        {
            GpsPoints.Remove(Guid);
            Feature.Delete(); //Deletes the feature data row corresponding to this feature and saves the changes to the feature layer
        }

        #endregion

        //public for XAML binding
        public string DisplayTime
        {
            get { return LocalTime.ToLongTimeString(); }
        }   

    }
}
