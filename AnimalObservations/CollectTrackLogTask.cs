using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;
using ESRI.ArcGIS.Mobile.Gps;
using ESRI.ArcGIS.Mobile.WPF;

namespace AnimalObservations
{
    public class CollectTrackLogTask : Task
    {
        public CollectTrackLogTask()
        {
            Name = "Collect Observations";  
            Description = "Begin data logging along a transect";
            //Can't set the ImageSource property because of threading issues.  Use GetImageSource() override
        }


        #region Overrides

        protected override ImageSource GetImageSource()
        {
            var uri = new Uri("pack://application:,,,/AnimalObservations;Component/duck-icon.png");
            return new BitmapImage(uri);
        }

        protected override void OnOwnerInitialized()
        {
            ValidateDatabaseSchema();
            if (DatabaseSchemaIsInvalid)
                return;
            InitializeObservationQueue();
            InitializeGpsConnection();
            SetupBoatLayer();
        }

        public override void Execute()
        {
            //Tasks can always be executed - no override for CanExecute() - so check valididty here.
            if (DatabaseSchemaIsInvalid)
                return;

            var mapExtents = MobileApplication.Current.Map.GetExtent();
            if (mapExtents == null)
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                    "Unable to determine extents of the map.", "Unexpected Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            if (Transect.GetWithin(mapExtents).Count() < 1)
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                    "No transects could be found within the visible map.\n" +
                    "Either zoom out or wait until a transect comes into view.\n" + 
                    "Make sure the GPS is on and the map is tracking your location.", "No Transects Found",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            if (!_gpsConnection.IsOpen || MostRecentLocation == null)
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                    "GPS is disconnected or doesn't yet have a fix on the satellites.\n" + 
                    "Correct the problems with the GPS and try again.", "No GPS Fix",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            MobileApplication.Current.Transition(new SetupTrackLogPage());
        }

        #endregion


        #region Manage TrackLog Recording

        //If CurrentTrackLog == null then we are not recording.
        //while current tracklog is not null recording may be turned on/off.
        //CurrentTrackLog will never be changed unless recording is off.

        private bool _isRecording;
        private readonly object _tracklogLock = new object();

        public bool StartRecording()
        {
            if (CurrentTrackLog == null || !_gpsConnection.IsOpen)
            {
                _isRecording = false;
            }
            else
            {
                _isRecording = true;
                //FIXME - the following seems to cause a conflicting event.  Removed for now

                //Collect our first point now, don't wait for an event. 
                //Connection_GpsChanged(null, null);
            }
            return _isRecording;
        }

        public void StopRecording()
        {
            //Make sure the GPS event isn't using the CurrentTrackLog 
            lock (_tracklogLock)
            {
                _isRecording = false;
                DefaultTrackLog = CurrentTrackLog;
                CurrentTrackLog = null;
            }
        }

        public TrackLog DefaultTrackLog { get; set; }
        //Use INotifyPropertyChanged to keep UI linked
        public TrackLog CurrentTrackLog
        {
            get { return _currentTrackLog; }
            set
            {
                if (_isRecording)
                    throw new InvalidOperationException("Cannot change current track log while recording.");
                if (value != _currentTrackLog)
                {
                    _currentTrackLog = value;
                    OnPropertyChanged("CurrentTrackLog");
                }
            }
        }
        private TrackLog _currentTrackLog;

        #endregion


        #region Manage Observation Queue

        //Use ObservableCollection to propegate changes to the XAML UI
        public ObservableCollection<Observation> OpenObservations { get; private set; }
        public Observation ActiveObservation
        {
            get { return _activeObservation; }
            set
            { 
                if (value != _activeObservation)
                {
                   _activeObservation = value;
                    OnPropertyChanged("ActiveObservation");
                }
            }
        }
        private Observation _activeObservation;


        private void InitializeObservationQueue()
        {
            OpenObservations = new ObservableCollection<Observation>();
            ActiveObservation = null;
        }

        public void AddObservation(Observation observation)
        {
            OpenObservations.Add(observation);
            if (ActiveObservation == null)
                ActiveObservation = observation;
        }

        public void RemoveObservation(Observation observation)
        {
            //If we are deleting the active record, make the first record active
            OpenObservations.Remove(observation);
            if (observation == ActiveObservation)
                ActiveObservation = (OpenObservations.Count == 0) ? null : OpenObservations[0];
        }

        public void RemoveObservationAlt(Observation observation)
        {
            //If we are deleting the active record, make the next record active
            int deletedIndex = OpenObservations.IndexOf(observation);
            int activeIndex = OpenObservations.IndexOf(ActiveObservation);
            OpenObservations.Remove(observation);
            if (deletedIndex <= activeIndex)
                activeIndex--;
            ActiveObservation = (activeIndex == -1) ? null : OpenObservations[activeIndex];
        }

        public void NextObservation()
        {
            int activeIndex = OpenObservations.IndexOf(ActiveObservation);
            activeIndex++;
            if (activeIndex == OpenObservations.Count)
                activeIndex = 0;
            ActiveObservation = OpenObservations[activeIndex];
        }

        public void PreviousObservation()
        {
            int activeIndex = OpenObservations.IndexOf(ActiveObservation);
            activeIndex--;
            if (activeIndex == -1)
                activeIndex = OpenObservations.Count - 1;
            ActiveObservation = OpenObservations[activeIndex];
        }

        #endregion


        #region Manage GPS connection and events

        /// <summary>
        /// This is updated only when we are recording.  These objects are saved to disk
        /// </summary>
        public GpsPoint CurrentGpsPoint { get; set; }

        /// <summary>
        /// This is updated whenever the GPS is receiving a position fix.  It is only used for map updates/queries
        /// </summary>
        public Coordinate MostRecentLocation { get; set; }

        private GpsConnection _gpsConnection;

        private void InitializeGpsConnection()
        {
            // Get GPS Connection and set up for change events
            _gpsConnection = MobileApplication.Current.GpsConnectionManager.Connection;
            //FIXME - Do I want to do this, or rely on application settings/user control
            //GpsMgr.OpenAsync();
            //FIXME - if the connection is closed and re-opened.  Do I need to get a new connection object?

            _gpsConnection.GpsClosed += ProcessGpsClosedEventFromConnection;
            _gpsConnection.GpsError += ProcessGpsErrorEventFromConnection;
            _gpsConnection.GpsChanged += ProcessGpsChangedEventFromConnection;
        }

        void ProcessGpsErrorEventFromConnection(object sender, GpsErrorEventArgs e)
        {
            //Save, but don't close any open edits
            PostChanges();
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(e.Exception.Message, "GPS Error");
            //Wait for the GPS to recover
            //FIXME - Change logging status?  How do toggle it back on if connection returns? 
        }

        void ProcessGpsClosedEventFromConnection(object sender, EventArgs e)
        {
            //FIXME - Close any open pages???
            if (_isRecording)
            {
                PostChanges();
                StopRecording();
            }
        }

        void PostChanges()
        {
            //ignore save errors here (there should be none), we will check/report when the tracklog is finalized.
            //TODO - add try/catch; various exceptions may be thrown.
            CurrentTrackLog.Save();
            foreach (var observation in OpenObservations)
            {
                observation.Save();
            }
        }
        void ProcessGpsChangedEventFromConnection(object sender, EventArgs e)
        {
            if (_gpsConnection.FixStatus == GpsFixStatus.Invalid)
                return;
            if (!LocationChanged(_gpsConnection))
                return;

            //TODO - multithreaded locking needs a lot more thought
            //Make sure another thread doesn't change the CurrentTrackLog while I'm working.
            lock (_tracklogLock)
            {
                if (_isRecording)
                {
                    //TODO - Add try/catch - CreateWith() and Save() may throw exceptions
                    CurrentGpsPoint = GpsPoint.CreateWith(CurrentTrackLog, _gpsConnection);
                    CurrentGpsPoint.Save();
                    CurrentTrackLog.AddPoint(CurrentGpsPoint.Location);
                    MostRecentLocation = CurrentGpsPoint.Location;
                }
                else
                {
                    double latitude = _gpsConnection.Latitude;
                    double longitude = _gpsConnection.Longitude;
                    //Offset Regan's office to GLBA main dock
                    latitude -= 2.7618;
                    longitude += 13.9988;
                    MostRecentLocation = MobileApplication.Current.Project.SpatialReference.FromGps(longitude, latitude);
                    //MostRecentLocation = MobileApplication.Current.Project.SpatialReference.FromGps(_gpsConnection.Longitude, _gpsConnection.Latitude);
                }
            }
            //FIXME - don't do this when collecting attributes
            DrawBoat(MostRecentLocation, _gpsConnection.Course);
        }

        private static bool LocationChanged(GpsConnection gpsConnection)
        {
            return (gpsConnection.GpsChangeType & GpsChangeType.Position) != 0;
        }

        #endregion


        #region Draw the boat

        private Image _boat;

        private void SetupBoatLayer()
        {
            if (!MobileApplication.Current.Dispatcher.CheckAccess())
            {
                MobileApplication.Current.Dispatcher.BeginInvoke((System.Threading.ThreadStart)SetupBoatLayer);
                return;
            }

            //Get Boat Icon
            var uri = new Uri("pack://application:,,,/AnimalObservations;Component/Boat-icon.png");
 
            _boat = new Image
                        {
                            Source = new BitmapImage(uri), 
                            Margin = new System.Windows.Thickness(-100, -100, 0, 0)
                        };

            // add graphic layer
            var overlay = new GraphicLayer();
            MobileApplication.Current.Map.MapGraphicLayers.Add(overlay);
            overlay.Children.Add(_boat);
        }

        private void DrawBoat(Coordinate location, double heading)
        {
            //Only update the UI on the UI thread
            //FIXME - this sometimes fails on startup
            if (!_boat.Dispatcher.CheckAccess())
            {
                _boat.Dispatcher.BeginInvoke((System.Threading.ThreadStart)(() => DrawBoat(location, heading)));
                return;
            }

            //heading = 0 is north (up); heading is in degrees clockwise
            //image without rotation is pointing E (270 degrees).
            System.Drawing.Point point = MobileApplication.Current.Map.ToClient(location);
            if (heading > 180)
                _boat.LayoutTransform = new RotateTransform(heading + 90.0);
            else
            {
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform(-1.0, 1.0));
                transformGroup.Children.Add(new RotateTransform(heading - 90.0));
                _boat.LayoutTransform = transformGroup;
            }
            _boat.Margin = new System.Windows.Thickness(point.X, point.Y, 0, 0);
            //TODO - pan map to center boat location
            //MobileApplication.Current.Map.CenterAt(location);
        }

        #endregion

        #region Test database Schema

        private bool ValidateDatabaseSchema()
        {
            return DatabaseSchemaIsInvalid;
        }

        private bool DatabaseSchemaIsInvalid
        {
            get
            {
                if (!_validDb.HasValue)
                    _validDb = !TestDatabaseSchema();
                return _validDb.Value;
            }
        }
        private bool? _validDb;

        private static bool TestDatabaseSchema()
        {
            const string errorMsg = "The structure of the database has been\n" +
                                    "modified. Field work cannot proceed\n" +
                                    "until the database is restored to\n" +
                                    "normal, or the application is updated.\n" +
                                    "Details:\n";

            try
            {
                //Initializes the transect Class
                Transect transect = Transect.AllTransects.FirstOrDefault();
                if (transect == null)
                {
                    ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                        "Field work cannot proceed until\n" +
                        "transects are downloaded to the\n" +
                        "mobile cache.",
                        "No Transects Found",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return false;
                }
                //Initialize other classes
                TrackLog trackLog = TrackLog.CreateWith(transect);
                GpsPoint gpsPoint = GpsPoint.CreateWith(trackLog);
                Observation observation = Observation.CreateWith(gpsPoint);
                BirdGroup birdGroup = BirdGroup.CreateWith(observation);
                //Unwind - destroy the temporary objects
                birdGroup.Delete();
                observation.Delete();
                gpsPoint.Delete();
                trackLog.Delete();
                return true;
            }
            catch (TypeInitializationException ex)
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                    errorMsg + ex.InnerException.Message,
                    "Invalid Database",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                    errorMsg + ex.Message,
                    "Invalid Database",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        #endregion

    }
}
