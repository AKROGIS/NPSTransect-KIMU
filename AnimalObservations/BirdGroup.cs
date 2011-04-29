using System;
using System.Collections.Generic;
using System.Linq;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.MobileServices;

namespace AnimalObservations
{
    public class BirdGroup
    {

        //FIXME - Should update location based on a property change event on Observation.Angle/Distance.

        internal static readonly FeatureLayer FeatureLayer = MobileUtilities.GetFeatureLayer("Murrelets");

        static readonly Dictionary<Guid, BirdGroup> BirdGroups = new Dictionary<Guid, BirdGroup>();

        public Feature Feature { get; private set; }
        public Guid Guid { get; private set; }

        public Observation Observation { get; private set; }
        public int Size { get; set; }
        public char Behavior { get; set; }
        public char Species { get; set; }
        public string Comments { get; set; }


        private BirdGroup()
        { }

        public static BirdGroup FromGuid(Guid guid)
        {
            if (BirdGroups.ContainsKey(guid))
                return BirdGroups[guid];

            var feature = MobileUtilities.GetFeature(FeatureLayer, guid);
            if (feature == null)
                return null;
            var birdGroup = new BirdGroup { Feature = feature };
            birdGroup.LoadAttributes1();
            birdGroup.LoadAttributes2();
            BirdGroups[birdGroup.Guid] = birdGroup;
            return birdGroup;
        }

        public static BirdGroup CreateWith(Observation observation)
        {
            if (observation == null)
                throw new ArgumentNullException("observation");

            var feature = MobileUtilities.CreateNewFeature(FeatureLayer);
            if (feature == null)
                return null;
            var birdGroup = new BirdGroup
                                {
                                    Feature = feature,
                                    Guid = new Guid(feature.FeatureDataRow.GlobalId.ToByteArray()),
                                    Observation = observation
                                };

            //get default attributes
            birdGroup.LoadAttributes2();
            BirdGroups[birdGroup.Guid] = birdGroup;
            //birdGroup.Save();
            return birdGroup;
        }

        private void LoadAttributes1()
        {
            Guid = new Guid(Feature.FeatureDataRow.GlobalId.ToByteArray());
            Observation = Observation.FromGuid((Guid)Feature.FeatureDataRow["ObservationID"]);
        }

        private void LoadAttributes2()
        {
            if (Feature.FeatureDataRow["GroupSize"] is int)
                Size = (int)Feature.FeatureDataRow["GroupSize"];
            if (Feature.FeatureDataRow["Behavior"] is string)
                Behavior = ((string)Feature.FeatureDataRow["Behavior"])[0];
            if (Feature.FeatureDataRow["Species"] is string)
                Species = ((string)Feature.FeatureDataRow["Species"])[0];
            if (Feature.FeatureDataRow["Comments"] is string)
                Comments = (string)Feature.FeatureDataRow["Comments"];
        }

        public bool Save()
        {
            Feature.Geometry = GetLocation();
            Feature.FeatureDataRow["ObservationID"] = Observation.Guid;
            Feature.FeatureDataRow["GroupSize"] = Size;
            Feature.FeatureDataRow["Behavior"] = Behavior.ToString();
            Feature.FeatureDataRow["Species"] = Species.ToString();
            Feature.FeatureDataRow["Comments"] = Comments ?? (object)DBNull.Value;
            return Feature.SaveEdits();
        }

        //FIXME - sort out the difference between:
        //  1) aborting the creation of a new object
        //  2) canceling changes to an existing object
        //  3) deleting an existing object

        public void Delete()
        {
            BirdGroups.Remove(Guid);
            Feature.CancelEdit();
        }

        public Point GetLocation()
        {
            return GetLocationRelativeToBoatHeading();
            //return GetLocationRelativeToTransectBearing();
        }

        private Point GetLocationRelativeToBoatHeading()
        {
            double gpsX = Observation.GpsPoint.Location.X;
            double gpsY = Observation.GpsPoint.Location.Y;
            //heading of boat (per GPS) as Azimuth (0=N, 90=E, 180=S, W=270)
            double bearing = Observation.GpsPoint.Bearing;
            //Add observation angle: clockwise from stern(0 = stern, 90 = port 180 = bow, 270 = starboard)
            bearing = bearing + (Observation.Angle - 180);
            //bearing is now a number between -180 and 540
            //Convert to a trig angle (0=E, 90=N, 180=W, ...)
            bearing = 90 - bearing; //bearing = (90 - bearing) < 0 ? 450 - bearing : 90 - bearing;
            //bearing is now a number between -450 and 270
            //No need to normalize to 0..360, as trig functions don't care
            //Trig functions do expect radians not degrees.
            bearing = bearing * Math.PI / 180.0;
            double birdX = gpsX + Observation.Distance * Math.Cos(bearing);
            double birdY = gpsY + Observation.Distance * Math.Sin(bearing);
            return new Point(birdX, birdY);
        }

        //private Point GetLocationRelativeToTransectBearing()
        //{
        //    double gpsX = Observation.GpsPoint.Location.X;
        //    double gpsY = Observation.GpsPoint.Location.Y;
        //    //heading of transect as trig angle in radians (0=E, pi/2=N, pi=W, 3pi/2=S)
        //    double bearing = Observation.GpsPoint.TrackLog.Transect.Bearing;
        //    //convert observation angle: clockwise from stern(0 = stern, 90 = port 180 = bow, 270 = starboard) to radians
        //    double observationAngle = (Observation.Angle - 180) * Math.PI / 180.0;
        //    //subtract the observation angle since it is clockwise and the bearing (trig) angle is counterclockwise
        //    bearing = bearing - observationAngle;
        //    double birdX = gpsX + Observation.Distance * Math.Cos(bearing);
        //    double birdY = gpsY + Observation.Distance * Math.Sin(bearing);
        //    return new Point(birdX, birdY);
        //}

        internal static BirdGroup FromPoint(Coordinate point)
        {
            return BirdGroups.Values.FirstOrDefault(birdGroups => birdGroups.Feature.FeatureDataRow.Geometry.Within(new Envelope(point,20,20)));
        }
    }
}
