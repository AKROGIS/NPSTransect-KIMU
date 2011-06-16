#define TESTINGWITHOUTGPS
//#define GPSINANCHORAGE

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
//using GpsDisplay = ESRI.ArcGIS.Mobile.WPF.GpsDisplay;

namespace AnimalObservations
{
    public class CollectTrackLogTask : Task
    {
        public CollectTrackLogTask()
        {
            Name = "Collect Observations";  
            Description = "Begin data logging along a transect";
            //Can't set the ImageSource (task icon) property because of threading issues.  Use GetImageSource() override

            NearbyTransects = new ObservableCollection<Transect>();

            MobileApplication.Current.ProjectClosing += (s, e) =>
            {
                CloseGpsConnection();
                StopRecording();
            };
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
            //Must set up the boat layer before the GPS connection; gps events assume boat is ready to draw.
            SetupBoatLayer();
            InitializeGpsConnection();
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
            RefreshNearbyTransects();
            if (NearbyTransects.Count < 1)
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                    "No transects could be found within the visible map. " +
                    "Either zoom out or wait until a transect comes into view. " + 
                    "Make sure the GPS is on and the map is tracking your location.", "No Transects Found",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
#if TESTINGWITHOUTGPS
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Program is operating in Test mode.", "No GPS Fix");
#else
            //Not used in production code - testing workaround  Also see StartRecording()
            if (!_gpsConnection.IsOpen || MostRecentLocation == null)
            {
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(
                    "GPS is disconnected or doesn't yet have a fix on the satellites. " +
                    "Correct the problems with the GPS and try again.", "No GPS Fix",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            //End of testing hack
#endif
            MobileApplication.Current.Transition(new SetupTrackLogPage());
        }

        #endregion


        #region Manage Transects

        //Use a public Observable Collection for XAML Binding
        public ObservableCollection<Transect> NearbyTransects { get; private set; }

        //The list starts out empty, and we cannot begin observations if there are no nearby transects.

        internal void RefreshNearbyTransects()
        {
            Envelope mapExtents = MobileApplication.Current.Map.GetExtent();
            bool transectsAreNearby = (Transect.GetWithin(mapExtents).FirstOrDefault() != null);
            //Leave the list unchanged if there are no nearby transects.
            //We don't want the pick list to go blank if we are changing tracklog properties in the
            //middle of a transect, but we happen to be zoomed in and can't see the transect.
            if (transectsAreNearby)
            {
                NearbyTransects.Clear();
                foreach (var transect in Transect.GetWithin(mapExtents))
                    NearbyTransects.Add(transect);
            }
        }

        #endregion


        #region Manage TrackLog Recording

        //If CurrentTrackLog == null then we are not recording.
        //while current tracklog is not null recording may be turned on/off.
        //CurrentTrackLog will never be changed unless recording is off.

        internal bool StartRecording()
        {
            //FIXME - Until I can fix locking,  I'm dispatching GPS events to the main UI thread.
            //lock (_gpsLock)
            //{
#if TESTINGWITHOUTGPS
                //Create a bogus GPS location
                CurrentGpsPoint = GpsPoint.FromGpsConnection(CurrentTrackLog, _gpsConnection);
                //MostRecentLocation = new Coordinate(448262, 6479766);  //Main dock
                MostRecentLocation = new Coordinate(443759, 6484291);  //East end of MainBay19
                return (IsRecording = true);
#else
                if (CurrentTrackLog == null || !_gpsConnection.IsOpen)
                {
                    IsRecording = false;
                }
                else
                {
                    IsRecording = true;
                    //Collect our first point now, don't wait for an event. 
                    //FIXME - the following seems to cause a conflicting event.  Removed for now
                    //Connection_GpsChanged(null, null);
                }
                return IsRecording;
#endif
            //}
        }

        internal void StopRecording()
        {
            //Stop recording is currently called in the following situations:
            // 1) if the project is closing - could happen at any time
            // 2) if there is a GPS close event - could happen at any time
            // 3) when the record tracklog page clicks the stop button
            // In case 3, the UI will handle the page transitions
            // In case 1, the host will handle the page transition to a blank screen
            // However, in case 2 I may get stuck with an open observation page and a closed observation object.

            //Make sure the GPS event doesn't change anything
            //FIXME - Until I can fix locking,  I'm dispatching GPS events to the main UI thread.
            //lock (_gpsLock)
            //{
                if (IsRecording)
                {
#if !TESTINGWITHOUTGPS
                    //Save any outstanding changes
                    if (!PostChanges())
                        ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Error saving changes", "Save Failed");
#endif
                    //Close any open observations
                    while (ActiveObservation != null)
                        OpenObservations.Remove(ActiveObservation);

                    IsRecording = false;
                    DefaultTrackLog = CurrentTrackLog;
                    CurrentTrackLog = null;
                }
            //}
        }

        internal bool IsRecording { get; private set;}

        private bool PostChanges()
        {
            //Save() may throw exceptions, but that would be catastrophic, so let the app handle it.
            bool saved = CurrentTrackLog.Save();
            foreach (var observation in OpenObservations)
                saved = saved && observation.Save();
            return saved;
        }


        internal TrackLog DefaultTrackLog { get; set; }

        //Use INotifyPropertyChanged to keep UI linked
        public TrackLog CurrentTrackLog
        {
            get { return _currentTrackLog; }
            set
            {
                //FIXME - Until I can fix locking,  I'm dispatching GPS events to the main UI thread.
                //lock (_gpsLock)
                //{
                    if (IsRecording)
                        throw new InvalidOperationException("Cannot change current track log while recording.");
                    if (value != _currentTrackLog)
                    {
                        _currentTrackLog = value;
                        OnPropertyChanged("CurrentTrackLog");
                    }
                //}
            }
        }
        private TrackLog _currentTrackLog;

        #endregion


        #region Manage Observation Queue

        //Use ObservableCollection to propagate changes to the XAML UI
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

        public System.Windows.Visibility ObservationQueueVisibility
        {
            get {
                return OpenObservations.Count > 1 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        private void InitializeObservationQueue()
        {
            OpenObservations = new ObservableCollection<Observation>();
            ActiveObservation = null;
        }

        public void AddObservationAsActive(Observation observation)
        {
            AddObservationAsInactive(observation);
            ActiveObservation = observation;
        }

        public void AddObservationAsInactive(Observation observation)
        {
            OpenObservations.Add(observation);
            if (ActiveObservation == null)
                ActiveObservation = observation;
        }

        public void RemoveObservation(Observation observation)
        {
            OpenObservations.Remove(observation);
            //If we are deleting the active record, make the first record active
            //If observation == ActiveObservation, then XAML bindings will set ActiveObservation to null
            if (ActiveObservation == null || observation == ActiveObservation)
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

        //ALL GPS EVENTS HAPPEN ON A BACKGROUND THREAD
        //As a basic rule, I need to lock around accessing any writable shared field.
        //That is, any field the the GPS event reads or writes must be locked by all readers/writers
        //TODO - multithreaded locking needs a lot more thought
        //private readonly object _gpsLock = new object();

        /// <summary>
        /// This is updated only when we are recording.  These objects are saved to disk
        /// </summary>
        public GpsPoint CurrentGpsPoint { get; set; }

        /// <summary>
        /// This is updated whenever the GPS is receiving a position fix.  It is only used for map updates/queries
        /// </summary>
        public Coordinate MostRecentLocation { get; set; }

        private GpsConnection _gpsConnection;

        private void CloseGpsConnection()
        {
            if (_gpsConnection == null)
                return;
            _gpsConnection.GpsClosed -= ProcessGpsClosedEventFromConnection;
            _gpsConnection.GpsError -= ProcessGpsErrorEventFromConnection;
            _gpsConnection.GpsChanged -= ProcessGpsChangedEventFromConnection;
            _gpsConnection = null;
        }

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

            //FIXME - Until I can fix locking,  I'm dispatching GPS events to the main UI thread.
            if (MobileApplication.Current.Dispatcher.CheckAccess())
            {
                //lock (_gpsLock)
                //{
                PostChanges();
                //}
                //TODO Can I call this message block on the GPS thread?
                //TODO Does the gps connection object put up it own error message
                ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(e.Exception.Message, "GPS Error");
                //Wait for the GPS to recover
                //no new gps coordinates will be received or posted to an open track log while the error persists
                //when the error clears, new gps events will be received, and any open tracklog will be updated appropriately.
                //User can close any open observation, or stop recording, but they cannot add any new observations
                //FIXME - prohibit adding new observation.  How do I detect the absense of the error condition?
            }
            else
            {
                MobileApplication.Current.Dispatcher.Invoke(new EventHandler<GpsErrorEventArgs>(ProcessGpsErrorEventFromConnection),
                    System.Windows.Threading.DispatcherPriority.Normal, sender, e);
            }
        }

        void ProcessGpsClosedEventFromConnection(object sender, EventArgs e)
        {
            //FIXME - Until I can fix locking,  I'm dispatching GPS events to the main UI thread.
            if (MobileApplication.Current.Dispatcher.CheckAccess())
            {
                //Note C# supports reentrent locks, so this thread can also call lock in StopRecording()
                //lock (_gpsLock)
                //{
                if (IsRecording)
                {
                    StopRecording();
                    //TODO - can we do these transitions from the gps thread?
                    //Make sure we transition away from any pages that may have open objects
                    if (PreviousPage is RecordTrackLogPage)
                        TransitionToPreviousPage();
                    if (PreviousPage is SetupTrackLogPage)
                        TransitionToPreviousPage();
                }
                //}
            }
            else
            {
                MobileApplication.Current.Dispatcher.Invoke(new EventHandler(ProcessGpsClosedEventFromConnection),
                    System.Windows.Threading.DispatcherPriority.Normal, sender, e);
            }
            
        }

        void ProcessGpsChangedEventFromConnection(object sender, EventArgs e)
        {
            if (_gpsConnection.FixStatus == GpsFixStatus.Invalid ||
                !LocationChanged(_gpsConnection))
                return;

            //FIXME - Until I can fix locking,  I'm dispatching GPS events to the main UI thread.
            if (MobileApplication.Current.Dispatcher.CheckAccess())
            {
                //lock (_gpsLock)
                //{
                if (IsRecording)
                {
                    //CreateWith()/Save() may throw exceptions, but that would be catastrophic, so let the app handle it.
                    CurrentGpsPoint = GpsPoint.FromGpsConnection(CurrentTrackLog, _gpsConnection);
                    CurrentGpsPoint.Save();
                    CurrentTrackLog.AddPoint(CurrentGpsPoint.Location);
                    MostRecentLocation = CurrentGpsPoint.Location;
                }
                else
                {
                    double latitude = _gpsConnection.Latitude;
                    double longitude = _gpsConnection.Longitude;
#if GPSINANCHORAGE
                    //Offset Regan's office to end of Transect MainBay19
                    latitude -= (61.217311111 - 58.495580);
                    longitude += (149.885638889 - 135.964885);
#endif
                    MostRecentLocation = MobileApplication.Current.Project.SpatialReference.FromGps(longitude, latitude);
                }
                //}
                if (OpenObservations.Count == 0)
                    DrawBoat(MostRecentLocation, _gpsConnection.Course);
            }
            else
            {
                MobileApplication.Current.Dispatcher.Invoke(new EventHandler(ProcessGpsChangedEventFromConnection),
                    System.Windows.Threading.DispatcherPriority.Normal, sender, e);
            }
        }

        private static bool LocationChanged(GpsConnection gpsConnection)
        {
            return (gpsConnection.GpsChangeType & GpsChangeType.Position) != 0;
        }


        //public GpsDisplay 

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
            if (!_boat.Dispatcher.CheckAccess())
            {
                _boat.Dispatcher.BeginInvoke((System.Threading.ThreadStart)(() => DrawBoat(location, heading)));
                return;
            }

            //pan map to center boat location
            Envelope env = MobileApplication.Current.Map.GetExtent();
            env = env.Resize(0.7);
            if (!env.Contains(location))
                MobileApplication.Current.Map.CenterAt(location);

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
            _boat.Margin = new System.Windows.Thickness(point.X-24, point.Y-30, 0, 0);
            
        }

        #endregion


        #region Test database Schema

        private void ValidateDatabaseSchema()
        {
            _validDb = !TestDatabaseSchema();
            return;
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
                TrackLog trackLog = TrackLog.FromTransect(transect);
                GpsPoint gpsPoint = GpsPoint.FromTrackLog(trackLog);
                Observation observation = Observation.FromGpsPoint(gpsPoint);
                BirdGroup birdGroup = BirdGroup.FromObservation(observation);
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
