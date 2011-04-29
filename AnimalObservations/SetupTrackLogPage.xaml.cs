using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Geometries;

namespace AnimalObservations
{
    public partial class SetupTrackLogPage
    {
        public SetupTrackLogPage()
        {
            InitializeData();

            InitializeComponent();

            Title = "Track Log Properties";
            Note = "Common observation attributes for <un-named> transect";

            // page icon
            var uri = new Uri("pack://application:,,,/AnimalObservations;Component/duck-icon.png");
            ImageSource = new System.Windows.Media.Imaging.BitmapImage(uri);

            // back button
            BackCommands.Add(BackCommand);

            // forward button
            OkCommand.Text = "Start Recording";
            ForwardCommands.Add(OkCommand);

            //Setup desired keyboard behavior
            Focusable = true;
            Loaded += (s, e) => Keyboard.Focus(vesselTextBox);
        }

        private void InitializeData()
        {
            Task = MobileApplication.Current.FindTask(typeof(CollectTrackLogTask)) as CollectTrackLogTask;
            UpdateSource();

            WeatherDomain = MobileUtilities.GetCodedValueDictionary<int>(TrackLog.FeatureLayer, "Weather");
            VisibilityDomain = MobileUtilities.GetCodedValueDictionary<int>(TrackLog.FeatureLayer, "Visibility");
            BeaufortDomain = MobileUtilities.GetCodedValueDictionary<int>(TrackLog.FeatureLayer, "Beaufort");
        }

        internal void UpdateSource()
        {
            //Before page was created, we ensured there were transects on the map,
            //when page is updated, we may not have any transects on the map (zoomed in, or panned over)
            //In this case , maintain the previous list of transects.
            Envelope mapExtents = MobileApplication.Current.Map.GetExtent();
            IList<Transect> newTransects = Transect.GetWithin(mapExtents).ToList();
            if (newTransects.Count > 0)
                Transects = Transect.GetWithin(mapExtents).ToList();

            Debug.Assert(Transects != null, "Fail, Transects list is null on SetupTrackLogPage ");
            Debug.Assert(Transects.Count > 0, "Fail, Transects list is empty on SetupTrackLogPage ");

            if (Task.DefaultTrackLog == null)
            {
                Task.CurrentTrackLog = TrackLog.CreateWith(Transects.GetNearest(Task.MostRecentLocation));
            }
            else
            {
                Task.CurrentTrackLog = TrackLog.CloneFrom(Task.DefaultTrackLog);
                Task.CurrentTrackLog.Transect = Transects.GetNearest(Task.MostRecentLocation);
            }

            Debug.Assert(Task.CurrentTrackLog.Transect != null, "Fail, CurrentTransect is null on SetupTrackLogPage ");

        }

        //These are public properties so they can be used in XAML
        public CollectTrackLogTask Task { get; set; }
        public IList<Transect> Transects { get; set; }
        public IDictionary<int, string> WeatherDomain { get; set; }
        public IDictionary<int, string> VisibilityDomain { get; set; }
        public IDictionary<int, string> BeaufortDomain { get; set; }

        protected override void OnBackCommandExecute()
        {
            //Cancel editing tracklog attributes, quits task.

            //TODO - Is there anything I need to do to dispose of the unfinished tracklog?
            Task.DefaultTrackLog = Task.CurrentTrackLog;
            Task.CurrentTrackLog = null;
            MobileApplication.Current.Transition(PreviousPage);
        }

        //TODO - this can be removed, since data is always valid, however it doesn't seem to hurt
        protected override bool CanExecuteOkCommand()
        {
            if (Task == null || Task.CurrentTrackLog == null)
                return false;
            Task.CurrentTrackLog.SyncPropertiesToFeature();
            return Task.CurrentTrackLog.Feature.HasValidAttributes;
        }

        protected override void OnOkCommandExecute()
        {
            //Save tracklog attributes, and begin gps logging, transition to new page

            Task.StartRecording();
            Task.CurrentTrackLog.StartingTime = DateTime.Now;
            //At this point geometry is invalid, so we just sync the attributes.
            //As points are collected, the feature will be periodically saved to disk
            Task.CurrentTrackLog.SyncPropertiesToFeature();
            MobileApplication.Current.Transition(new RecordTrackLogPage());
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            //FIXME - Capture Tabs and don't let the user tab out of the content area.
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OnOkCommandExecute();
                return;
            }
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                OnBackCommandExecute();
                return;
            }
            if (beaufortComboBox.IsFocused && e.Key == Key.Tab)
            {
                e.Handled = true;
                Keyboard.Focus(vesselTextBox);
                return;
            }
            if (vesselTextBox.IsFocused && e.KeyboardDevice.Modifiers == ModifierKeys.Shift && e.Key == Key.Tab)
            {
                e.Handled = true;
                Keyboard.Focus(beaufortComboBox);
                return;
            }
            base.OnKeyDown(e);
        }

    }
}
